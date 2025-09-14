using NAudio.CoreAudioApi;

namespace VolumeKeeper.Models;

public class AudioSession
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public string ExecutableName { get; init; } = string.Empty;
    public string? ExecutablePath { get; init; } = null;
    public float Volume { get; init; }
    public bool IsMuted { get; init; }
    public string IconPath { get; init; } = string.Empty;
    public AudioSessionControl SessionControl { get; init; } = null!;

    public VolumeApplicationId AppId => VolumeApplicationId.Create(ExecutablePath, ExecutableName);
}
