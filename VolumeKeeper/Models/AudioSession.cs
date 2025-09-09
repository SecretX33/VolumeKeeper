using NAudio.CoreAudioApi;

namespace VolumeKeeper.Models;

public class AudioSession
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public float Volume { get; set; }
    public bool IsMuted { get; set; }
    public string IconPath { get; set; } = string.Empty;
    public AudioSessionControl SessionControl { get; set; } = null!;
}

public class AudioSessionInfo
{
    public string ApplicationName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public double Volume { get; set; }
    public bool IsMuted { get; set; }
    public bool IsActive { get; set; }
    public AudioSessionControl? Session { get; set; }
    public string? IconPath { get; set; }
}
