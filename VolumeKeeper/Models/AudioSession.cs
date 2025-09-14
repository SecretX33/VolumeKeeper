using NAudio.CoreAudioApi;

namespace VolumeKeeper.Models;

public class AudioSession
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public string? ExecutablePath { get; set; } = null;
    public float Volume { get; set; }
    public bool IsMuted { get; set; }
    public string IconPath { get; set; } = string.Empty;
    public AudioSessionControl SessionControl { get; set; } = null!;

    public VolumeApplicationId VolumeId => ExecutablePath != null
        ? new PathVolumeApplicationId(ExecutablePath)
        : new NamedVolumeApplicationId(ExecutableName);
}
