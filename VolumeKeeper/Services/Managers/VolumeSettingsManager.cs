using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VolumeKeeper.Models;
using VolumeKeeper.Services.Log;
using VolumeKeeper.Util;
using VolumeKeeper.Util.Converter;

namespace VolumeKeeper.Services.Managers;

public sealed class VolumeSettingsManager
{
    private readonly Logger _logger = App.Logger.Named();
    private static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly AtomicReference<CancellationTokenSource?> _saveDebounceTokenSource = new(null);
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        WriteIndented = true
    };
    private readonly ConcurrentDictionary<VolumeApplicationId, int> _applicationVolumes = new();
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

    public async Task InitializeAsync()
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
            }
            _autoRestoreEnabled = parsedValue.AutoRestoreEnabled;
            _autoScrollLogsEnabled = parsedValue.AutoScrollLogsEnabled;
        } catch (Exception ex) {
            _logger.Error("Failed to initialize volume settings", ex);
        }
    }

    public int? GetVolume(VolumeApplicationId id) => _applicationVolumes.GetOrNullValue(id);

    public void SetVolumeAndSave(VolumeApplicationId id, int value)
    {
        // validate volume range and name
        if (value is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(value), "Volume must be between 0 and 100");

        DeleteVolume(id);
        _applicationVolumes[id] = value;
        ScheduleSave();
    }

    private bool DeleteVolume(VolumeApplicationId id) => _applicationVolumes.TryRemove(id, out _);

    public bool DeleteVolumeAndSave(VolumeApplicationId id)
    {
        var removed = DeleteVolume(id);
        ScheduleSave();
        return removed;
    }

    public void SetAutoRestoreEnabledAndSave(bool enabled)
    {
        _autoRestoreEnabled = enabled;
        ScheduleSave();
    }

    public void SetAutoScrollLogsEnabledAndSave(bool enabled)
    {
        _autoScrollLogsEnabled = enabled;
        ScheduleSave();
    }

    // Debounce save operations to avoid excessive disk writes
    // If multiple calls happen within 2 seconds, only the last one will trigger a save
    private Task ScheduleSave()
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

                await Task.Delay(SaveDelay, cancellationToken).ConfigureAwait(false);
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

            var existingKeys = _applicationVolumes.Keys;
            var applicationVolumeConfigs = new List<ApplicationVolumeConfig>();

            foreach (var volumeApplicationId in existingKeys)
            {
                var volume = _applicationVolumes.GetOrNullValue(volumeApplicationId);
                if (volume == null) continue;

                var config = new ApplicationVolumeConfig(
                    Id: volumeApplicationId,
                    Volume: volume
                );
                applicationVolumeConfigs.Add(config);
            }

            var sortedApplicationVolumeConfigs = applicationVolumeConfigs
                .OrderBy(x => x.Id.Path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var settingsToSave = new VolumeSettings
            {
                ApplicationVolumes = sortedApplicationVolumeConfigs,
                AutoRestoreEnabled = _autoRestoreEnabled,
                AutoScrollLogsEnabled = _autoScrollLogsEnabled,
            };

            var json = JsonSerializer.Serialize(settingsToSave, _jsonSerializerOptions);
            await File.WriteAllTextAsync(SettingsPath, json).ConfigureAwait(false);
            _logger.Debug("Volume settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to save volume settings", ex);
        }
        finally
        {
            _fileLock.Release();
        }
    }
}
