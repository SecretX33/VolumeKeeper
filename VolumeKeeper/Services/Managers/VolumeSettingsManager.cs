using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VolumeKeeper.Models;
using VolumeKeeper.Util;
using VolumeKeeper.Util.Converter;

namespace VolumeKeeper.Services.Managers;

public class VolumeSettingsManager
{
    private static readonly TimeSpan NormalSaveDelay = TimeSpan.FromSeconds(2);
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly AtomicReference<CancellationTokenSource?> _saveDebounceTokenSource = new(null);
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true
    };
    private readonly ConcurrentDictionary<VolumeApplicationId, int> _applicationVolumes = new();
    private readonly ConcurrentDictionary<VolumeApplicationId, int> _applicationLastVolumeBeforeMute = new();
    private volatile bool _autoRestoreEnabled = true;
    private volatile bool _autoScrollLogsEnabled = true;

    public bool AutoRestoreEnabled => _autoRestoreEnabled;
    public bool AutoScrollLogsEnabled => _autoScrollLogsEnabled;

    public VolumeSettingsManager()
    {
        _jsonSerializerOptions.Converters.Add(new ApplicationIdJsonConverter());
    }

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
            var parsedValue = JsonSerializer.Deserialize<VolumeSettings>(json, _jsonSerializerOptions);
            if (parsedValue == null) return;

            foreach (var config in parsedValue.ApplicationVolumes)
            {
                if (config.Volume.HasValue) {
                    _applicationVolumes[config.Id] = config.Volume.Value;
                }

                if (config.LastVolumeBeforeMute.HasValue) {
                    _applicationLastVolumeBeforeMute[config.Id] = config.LastVolumeBeforeMute.Value;
                }
            }
            _autoRestoreEnabled = parsedValue.AutoRestoreEnabled;
            _autoScrollLogsEnabled = parsedValue.AutoScrollLogsEnabled;
        } catch (Exception ex) {
            App.Logger.LogError("Failed to initialize volume settings", ex, "VolumeSettingsManager");
        }
    }

    public int? GetVolume(VolumeApplicationId id) => id.GetAllVariants()
        .Select(_applicationVolumes.GetOrNullValue)
        .First(it => it != null);

    public void SetVolumeAndSave(VolumeApplicationId id, int value)
    {
        // validate volume range and name
        if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0 and 100");

        DeleteVolume(id);
        _applicationVolumes[id] = value;
        ScheduleSave(NormalSaveDelay);
    }

    private bool DeleteVolume(VolumeApplicationId id) => id.GetAllVariants()
        .Aggregate(false, (current, volumeApplicationId) => _applicationVolumes.TryRemove(volumeApplicationId, out _) || current);

    public bool DeleteVolumeAndSave(VolumeApplicationId id)
    {
        var removed = DeleteVolume(id);
        ScheduleSave(NormalSaveDelay);
        return removed;
    }

    public int? GetLastVolumeBeforeMute(VolumeApplicationId id) => _applicationLastVolumeBeforeMute.GetOrNullValue(id);

    public void SetLastVolumeBeforeMuteAndSave(VolumeApplicationId id, int value)
    {
        // validate volume range and name
        if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0 and 100");

        DeleteLastVolumeBeforeMute(id);
        _applicationLastVolumeBeforeMute[id] = value;
        ScheduleSave(NormalSaveDelay);
    }

    private bool DeleteLastVolumeBeforeMute(VolumeApplicationId id) => id.GetAllVariants()
        .Aggregate(false, (current, volumeApplicationId) => _applicationLastVolumeBeforeMute.TryRemove(volumeApplicationId, out _) || current);

    public bool DeleteLastVolumeBeforeMuteAndSave(VolumeApplicationId id)
    {
        var removed = DeleteLastVolumeBeforeMute(id);
        ScheduleSave(NormalSaveDelay);
        return removed;
    }

    public void SetAutoRestoreEnabledAndSave(bool enabled)
    {
        _autoRestoreEnabled = enabled;
        ScheduleSave(NormalSaveDelay);
    }

    public void SetAutoScrollLogsEnabledAndSave(bool enabled)
    {
        _autoScrollLogsEnabled = enabled;
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

            var existingKeys = _applicationVolumes.Keys.Concat(_applicationLastVolumeBeforeMute.Keys).Distinct();

            foreach (var volumeApplicationId in existingKeys)
            {
                var volume = _applicationVolumes.GetOrNullValue(volumeApplicationId);
                var lastVolumeBeforeMute = _applicationLastVolumeBeforeMute.GetOrNullValue(volumeApplicationId);
                if (volume == null && lastVolumeBeforeMute == null) continue;

                var config = new ApplicationVolumeConfig(
                    Id: volumeApplicationId,
                    Volume: volume,
                    LastVolumeBeforeMute: lastVolumeBeforeMute
                );
                applicationVolumeConfigs.Add(config);
            }

            var settingsToSave = new VolumeSettings
            {
                ApplicationVolumes = applicationVolumeConfigs,
                AutoRestoreEnabled = _autoRestoreEnabled,
                AutoScrollLogsEnabled = _autoScrollLogsEnabled,
            };

            var json = JsonSerializer.Serialize(settingsToSave, _jsonSerializerOptions);
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
