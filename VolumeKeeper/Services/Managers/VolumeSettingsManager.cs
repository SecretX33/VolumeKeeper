using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VolumeKeeper.Models;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services.Managers;

public class VolumeSettingsManager
{
    private static readonly TimeSpan NormalSaveDelay = TimeSpan.FromSeconds(2);
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly AtomicReference<VolumeSettings> _cachedSettings = new(new VolumeSettings());
    private readonly AtomicReference<CancellationTokenSource?> _saveDebounceTokenSource = new(null);

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VolumeKeeper",
        "configs",
        "volume_settings.json"
    );

    public async void InitializeAsync()
    {
        try {
            var json = await File.ReadAllTextAsync(SettingsPath).ConfigureAwait(false);
            var parsedValue = JsonSerializer.Deserialize<VolumeSettings>(json);
            if (parsedValue == null) return;
            _cachedSettings.Set(parsedValue);
        } catch (Exception ex) {
            App.Logger.LogError("Failed to initialize volume settings", ex, "VolumeSettingsManager");
        }
    }

    private VolumeSettings Get() => _cachedSettings.Get();

    private void Set(VolumeSettings settings) => _cachedSettings.Set(settings);

    public void SetAndSave(VolumeSettings settings, bool saveImmediately = true)
    {
        Set(settings);
        var task = ScheduleSave(saveImmediately ? TimeSpan.Zero : NormalSaveDelay);
        if (saveImmediately)
        {
            // Await immediately to ensure save is done before proceeding
            task.GetAwaiter().GetResult();
        }
    }

    // Debounce save operations to avoid excessive disk writes
    // If multiple calls happen within 2 seconds, only the last one will trigger a save
    private Task ScheduleSave(TimeSpan saveDelay)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var oldCancellationTokenSource = _saveDebounceTokenSource.GetAndSet(cancellationTokenSource);
        var cancellationToken = cancellationTokenSource.Token;

        return Task.Run(async () =>
        {
            try
            {
                if (oldCancellationTokenSource != null)
                {
                    await oldCancellationTokenSource.CancelAsync().ConfigureAwait(false);
                }

                await Task.Delay(saveDelay, cancellationToken).ConfigureAwait(false);
                if (cancellationToken.IsCancellationRequested) return;

                await SaveSettingsToDiskAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when debounce is cancelled
            }
            finally
            {
                _saveDebounceTokenSource.CompareAndSet(cancellationTokenSource, null);
                cancellationTokenSource.Dispose();
            }
        }, cancellationToken);
    }

    private async Task SaveSettingsToDiskAsync(CancellationToken cancellationToken)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        // Lock has been acquired, do NOT cancel from this point on
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_cachedSettings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(SettingsPath, json).ConfigureAwait(false);
            App.Logger.LogInfo("Volume settings saved successfully", "VolumeSettingsManager");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to save volume settings", ex, "VolumeSettingsManager");
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
