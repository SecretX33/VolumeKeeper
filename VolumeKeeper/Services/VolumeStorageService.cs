using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using VolumeKeeper.Models;

namespace VolumeKeeper.Services;

public class VolumeStorageService
{
    private readonly string _settingsPath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private VolumeSettings? _cachedSettings;

    public VolumeStorageService()
    {
        var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VolumeKeeper");
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "volume_settings.json");
    }

    public async Task<VolumeSettings> LoadSettingsAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (_cachedSettings != null)
                return _cachedSettings;

            if (!File.Exists(_settingsPath))
            {
                _cachedSettings = new VolumeSettings();
                return _cachedSettings;
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
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
            await File.WriteAllTextAsync(_settingsPath, json);
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

    public async Task SetVolumeAsync(string executableName, int volumePercentage)
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
}
