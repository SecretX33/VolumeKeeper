using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VolumeKeeper.Models;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services;

public class WindowSettingsService
{
    private static readonly TimeSpan NormalSaveDelay = TimeSpan.FromSeconds(2);
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly ConcurrentDictionary<WindowId, WindowSettings> _cachedSettings = new();
    private volatile AtomicReference<CancellationTokenSource?> _saveDebounceTokenSource = new(null);

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VolumeKeeper",
        "configs",
        "window_settings.json"
    );

    public async void InitializeAsync()
    {
        try {
            var json = await File.ReadAllTextAsync(SettingsPath).ConfigureAwait(false);
            var parsedValue = JsonSerializer.Deserialize<ConcurrentDictionary<WindowId, WindowSettings>>(json)
                ?? new ConcurrentDictionary<WindowId, WindowSettings>();

            foreach (var kvp in parsedValue)
            {
                _cachedSettings[kvp.Key] = kvp.Value;
            }
        } catch (Exception ex) {
            App.Logger.LogError("Failed to initialize window settings", ex, "WindowStorageService");
        }
    }

    public WindowSettings Get(WindowId windowId) => _cachedSettings.GetOrAdd(windowId, _ => new WindowSettings());

    public void Set(WindowId windowId, WindowSettings settings) => _cachedSettings[windowId] = settings;

    public void SetAndSave(WindowId windowId, WindowSettings settings, bool saveImmediately = true)
    {
        Set(windowId, settings);
        ScheduleSave(saveImmediately ? TimeSpan.Zero : NormalSaveDelay);
    }

    // Debounce save operations to avoid excessive disk writes
    // If multiple calls happen within 2 seconds, only the last one will trigger a save
    private void ScheduleSave(TimeSpan saveDelay)
    {
        var cancellationTokenSource = new CancellationTokenSource();
        var oldCancellationTokenSource = _saveDebounceTokenSource.GetAndSet(cancellationTokenSource);
        var cancellationToken = cancellationTokenSource.Token;

        Task.Run(async () =>
        {
            if (oldCancellationTokenSource != null)
            {
                await oldCancellationTokenSource.CancelAsync().ConfigureAwait(false);
            }

            try
            {
                await Task.Delay(saveDelay, cancellationToken).ConfigureAwait(false);

                if (!cancellationToken.IsCancellationRequested)
                {
                    await SaveSettingsToDiskAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when debounce is cancelled
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
            App.Logger.LogInfo("Window settings saved successfully", "WindowSettingsService");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to save window settings", ex, "WindowSettingsService");
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
