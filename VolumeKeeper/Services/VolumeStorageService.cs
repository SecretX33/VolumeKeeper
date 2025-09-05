using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VolumeKeeper.Models;

namespace VolumeKeeper.Services;

public class VolumeStorageService
{
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private volatile VolumeSettings? _cachedSettings;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VolumeKeeper",
        "configs",
        "volume_settings.json"
    );

    public async Task<VolumeSettings> LoadSettingsAsync()
    {
        // First check if we have cached settings without acquiring the lock
        var cached = _cachedSettings;
        if (cached != null)
            return cached;

        await _fileLock.WaitAsync();
        try
        {
            // Double-check pattern: check again after acquiring the lock
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
            App.Logger.LogError("Failed to load volume settings", ex, "VolumeStorageService");
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
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(SettingsPath, json);
            App.Logger.LogInfo($"Volume settings saved successfully", "VolumeStorageService");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to save volume settings", ex, "VolumeStorageService");
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveVolumeAsync(string executableName, int volumePercentage)
    {
        var settings = await LoadSettingsAsync();
        settings.SetVolume(executableName, volumePercentage);
        await SaveSettingsAsync(settings);
        App.Logger.LogInfo($"Volume set for {executableName}: {volumePercentage}%", "VolumeStorageService");
    }

    public async Task<int?> GetVolumeAsync(string executableName)
    {
        var settings = await LoadSettingsAsync();
        return settings.GetVolume(executableName);
    }

    public async Task RemoveVolumeAsync(string executableName)
    {
        var settings = await LoadSettingsAsync();
        settings.RemoveVolume(executableName);
        await SaveSettingsAsync(settings);
        App.Logger.LogInfo($"Volume settings removed for {executableName}", "VolumeStorageService");
    }

    public async Task ClearAllVolumesAsync()
    {
        var settings = await LoadSettingsAsync();
        settings.ApplicationVolumes.Clear();
        settings.LastVolumeBeforeMute.Clear();
        await SaveSettingsAsync(settings);
        App.Logger.LogInfo("All volume settings cleared", "VolumeStorageService");
    }

    public async Task<VolumeSettings> GetSettingsAsync()
    {
        return await LoadSettingsAsync();
    }
}
