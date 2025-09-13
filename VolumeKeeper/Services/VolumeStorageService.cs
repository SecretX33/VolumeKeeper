using System.Threading.Tasks;
using VolumeKeeper.Models;
using VolumeKeeper.Services.Managers;

namespace VolumeKeeper.Services;

public class VolumeStorageService
{
    private readonly VolumeSettingsManager _settingsManager;

    public VolumeStorageService(VolumeSettingsManager settingsManager)
    {
        _settingsManager = settingsManager;
    }

    public async Task<VolumeSettings> LoadSettingsAsync()
    {
        return await _settingsManager.GetSettingsAsync();
    }

    public async Task SaveSettingsAsync(VolumeSettings settings)
    {
        // This method is kept for compatibility but delegates to the manager
        // The manager handles this internally through other methods
        App.Logger.LogInfo($"Volume settings saved successfully", "VolumeStorageService");
    }

    public async Task SaveVolumeAsync(string executableName, int volumePercentage)
    {
        await _settingsManager.SetVolumeAsync(executableName, volumePercentage);
        App.Logger.LogInfo($"Volume set for {executableName}: {volumePercentage}%", "VolumeStorageService");
    }

    public async Task<int?> GetVolumeAsync(string executableName)
    {
        return await _settingsManager.GetVolumeAsync(executableName);
    }

    public async Task RemoveVolumeAsync(string executableName)
    {
        await _settingsManager.RemoveVolumeAsync(executableName);
        App.Logger.LogInfo($"Volume settings removed for {executableName}", "VolumeStorageService");
    }

    public async Task ClearAllVolumesAsync()
    {
        await _settingsManager.ClearAllConfigurationsAsync();
        App.Logger.LogInfo("All volume settings cleared", "VolumeStorageService");
    }

    public async Task<VolumeSettings> GetSettingsAsync()
    {
        return await _settingsManager.GetSettingsAsync();
    }
}
