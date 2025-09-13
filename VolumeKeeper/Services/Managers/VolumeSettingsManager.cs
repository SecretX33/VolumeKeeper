using System;
using System.Collections.Concurrent;
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
    private readonly AtomicReference<CancellationTokenSource?> _saveDebounceTokenSource = new(null);

    private readonly ConcurrentDictionary<string, int> _applicationVolumes = new();
    private readonly ConcurrentDictionary<string, int> _applicationLastVolumeBeforeMute = new();
    private volatile bool _autoRestoreEnabled = true;
    private volatile bool _autoScrollLogsEnabled = true;

    public bool AutoRestoreEnabled => _autoRestoreEnabled;
    public bool AutoScrollLogsEnabled => _autoScrollLogsEnabled;

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

            foreach (var config in parsedValue.ApplicationVolumes)
            {
                _applicationVolumes[config.Name] = config.Volume;
                if (config.LastVolumeBeforeMute != null) {
                    _applicationLastVolumeBeforeMute[config.Name] = config.LastVolumeBeforeMute.Value;
                }
            }
            _autoRestoreEnabled = parsedValue.AutoRestoreEnabled;
            _autoScrollLogsEnabled = parsedValue.AutoScrollLogsEnabled;
        } catch (Exception ex) {
            App.Logger.LogError("Failed to initialize volume settings", ex, "VolumeSettingsManager");
        }
    }

    private int? GetVolume(string name) => _applicationVolumes.GetOrNull(name);

    private void SetVolumeAndSave(string name, int value)
    {
        // validate volume range and name
        if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0 and 100");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or whitespace", nameof(name));

        _applicationVolumes[name] = value;
        ScheduleSave(NormalSaveDelay);
    }

    public int? GetLastVolumeBeforeMute(string name) => _applicationLastVolumeBeforeMute.GetOrNull(name);

    public void SetLastVolumeBeforeMuteAndSave(string name, int value)
    {
        // validate volume range and name
        if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0 and 100");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name cannot be null or whitespace", nameof(name));

        _applicationLastVolumeBeforeMute[name] = value;
        ScheduleSave(NormalSaveDelay);
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

            var applicationVolumeConfigs = new List<ApplicationVolumeConfig>();
            foreach (var (appName, appVolume) in _applicationVolumes)
            {
                var config = new ApplicationVolumeConfig
                {
                    Name = appName,
                    Volume = appVolume,
                };
                if (_applicationLastVolumeBeforeMute.TryGetValue(appName, out var lastVolume))
                {
                    config.LastVolumeBeforeMute = lastVolume;
                }
                applicationVolumeConfigs.Add(config);
            }


            var settingsToSave = new VolumeSettings
            {
                ApplicationVolumes = applicationVolumeConfigs,
                AutoRestoreEnabled = _autoRestoreEnabled,
                AutoScrollLogsEnabled = _autoScrollLogsEnabled,
            };

            var json = JsonSerializer.Serialize(settingsToSave, new JsonSerializerOptions { WriteIndented = true });
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
