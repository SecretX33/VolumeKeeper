using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VolumeKeeper.Models;

namespace VolumeKeeper.Services.Managers;

public class VolumeSettingsManager
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private volatile VolumeSettings? _cachedSettings;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VolumeKeeper",
        "configs",
        "volume_settings.json"
    );

    public async Task<bool> SetVolumeAsync(string executableName, int volumePercentage)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return false;

        if (volumePercentage is < 0 or > 100)
            return false;

        var settings = await LoadSettingsAsync();
        settings.SetVolume(executableName, volumePercentage);
        await SaveSettingsAsync(settings);
        return true;
    }

    public async Task<int?> GetVolumeAsync(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return null;

        var settings = await LoadSettingsAsync();
        return settings.GetVolume(executableName);
    }

    public async Task<bool> RemoveVolumeAsync(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return false;

        var settings = await LoadSettingsAsync();
        settings.RemoveVolume(executableName);
        await SaveSettingsAsync(settings);
        return true;
    }

    public async Task<IReadOnlyDictionary<string, int>> GetAllConfigurationsAsync()
    {
        var settings = await LoadSettingsAsync();
        return new Dictionary<string, int>(settings.ApplicationVolumes);
    }

    public async Task<bool> SetMuteStateAsync(string executableName, int lastVolumeBeforeMute)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return false;

        var settings = await LoadSettingsAsync();
        settings.LastVolumeBeforeMute[executableName.ToLowerInvariant()] = lastVolumeBeforeMute;
        await SaveSettingsAsync(settings);
        return true;
    }

    public async Task<int?> GetLastVolumeBeforeMuteAsync(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return null;

        var settings = await LoadSettingsAsync();
        return settings.LastVolumeBeforeMute.TryGetValue(executableName.ToLowerInvariant(), out var volume)
            ? volume
            : null;
    }

    public async Task ClearMuteStateAsync(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return;

        var settings = await LoadSettingsAsync();
        settings.LastVolumeBeforeMute.TryRemove(executableName.ToLowerInvariant(), out _);
        await SaveSettingsAsync(settings);
    }

    public async Task ClearAllConfigurationsAsync()
    {
        var settings = await LoadSettingsAsync();
        settings.ApplicationVolumes.Clear();
        settings.LastVolumeBeforeMute.Clear();
        await SaveSettingsAsync(settings);
    }

    public async Task<bool> GetAutoRestoreEnabledAsync()
    {
        var settings = await LoadSettingsAsync();
        return settings.AutoRestoreEnabled;
    }

    public async Task SetAutoRestoreEnabledAsync(bool enabled)
    {
        var settings = await LoadSettingsAsync();
        settings.AutoRestoreEnabled = enabled;
        await SaveSettingsAsync(settings);
    }

    public async Task<bool> GetAutoScrollLogsEnabledAsync()
    {
        var settings = await LoadSettingsAsync();
        return settings.AutoScrollLogsEnabled;
    }

    public async Task SetAutoScrollLogsEnabledAsync(bool enabled)
    {
        var settings = await LoadSettingsAsync();
        settings.AutoScrollLogsEnabled = enabled;
        await SaveSettingsAsync(settings);
    }

    public async Task<VolumeSettings> GetSettingsAsync()
    {
        return await LoadSettingsAsync();
    }

    private async Task<VolumeSettings> LoadSettingsAsync()
    {
        var cached = _cachedSettings;
        if (cached != null)
            return cached;

        await _fileLock.WaitAsync();
        try
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            if (!File.Exists(SettingsPath))
            {
                _cachedSettings = new VolumeSettings();
                return _cachedSettings;
            }

            var json = await File.ReadAllTextAsync(SettingsPath);
            _cachedSettings = JsonSerializer.Deserialize<VolumeSettings>(json) ?? new VolumeSettings();
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to load volume settings", ex, "VolumeConfigurationManager");
            _cachedSettings = new VolumeSettings();
            return _cachedSettings;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveSettingsAsync(VolumeSettings settings)
    {
        await _fileLock.WaitAsync();
        try
        {
            _cachedSettings = settings;

            var directory = Path.GetDirectoryName(SettingsPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(SettingsPath, json);
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to save volume settings", ex, "VolumeConfigurationManager");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public void InvalidateCache()
    {
        _cachedSettings = null;
    }
}
