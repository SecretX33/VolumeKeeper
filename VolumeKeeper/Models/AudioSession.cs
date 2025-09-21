using System;
using Microsoft.UI.Xaml.Media.Imaging;
using NAudio.CoreAudioApi;

namespace VolumeKeeper.Models;

public class AudioSession
{
    public int ProcessId { get; init; }
    public string ProcessDisplayName { get; init; } = string.Empty;
    public string ExecutableName { get; init; } = string.Empty;
    public string? ExecutablePath { get; init; } = null;
    public bool IsMuted { get; init; }
    public string IconPath { get; init; } = string.Empty;
    public BitmapImage? Icon { get; init; }
    public AudioSessionControl SessionControl { get; init; } = null!;

    public int Volume
    {
        get => (int)Math.Round(SessionControl.SimpleAudioVolume.Volume * 100);
        set => SessionControl.SimpleAudioVolume.Volume = value / 100f;
    }

    public VolumeApplicationId AppId => VolumeApplicationId.Create(ExecutablePath, ExecutableName);

    public AudioSession With(
        int? processId = null,
        string? processDisplayName = null,
        string? executableName = null,
        string? executablePath = null,
        int? volume = null,
        bool? isMuted = null,
        string? iconPath = null,
        BitmapImage? icon = null,
        AudioSessionControl? sessionControl = null
    ) => new()
        {
            ProcessId = processId ?? ProcessId,
            ProcessDisplayName = processDisplayName ?? ProcessDisplayName,
            ExecutableName = executableName ?? ExecutableName,
            ExecutablePath = executablePath ?? ExecutablePath,
            Volume = volume ?? Volume,
            IsMuted = isMuted ?? IsMuted,
            IconPath = iconPath ?? IconPath,
            Icon = icon ?? Icon,
            SessionControl = sessionControl ?? SessionControl
        };
}
