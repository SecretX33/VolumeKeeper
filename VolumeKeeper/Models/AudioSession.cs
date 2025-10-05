using System;
using Microsoft.UI.Xaml.Media.Imaging;
using NAudio.CoreAudioApi;

namespace VolumeKeeper.Models;

public sealed class AudioSession
{
    public required int ProcessId { get; init; }
    public required string ProcessDisplayName { get; init; }
    public required string ExecutableName { get; init; }
    public required string ExecutablePath { get; init; }
    public required string IconPath { get; init; }
    public BitmapImage? Icon { get; init; }
    public required AudioSessionControl SessionControl { get; init; }

    public int Volume
    {
        get => (int)Math.Round(SessionControl.SimpleAudioVolume.Volume * 100);
        set => SessionControl.SimpleAudioVolume.Volume = value / 100f;
    }

    public bool IsMuted
    {
        get => SessionControl.SimpleAudioVolume.Mute;
        set => SessionControl.SimpleAudioVolume.Mute = value;
    }

    public VolumeApplicationId AppId => new(ExecutablePath);

    public AudioSession With(
        int? processId = null,
        string? processDisplayName = null,
        string? executableName = null,
        string? executablePath = null,
        string? iconPath = null,
        BitmapImage? icon = null,
        AudioSessionControl? sessionControl = null
    ) => new()
        {
            ProcessId = processId ?? ProcessId,
            ProcessDisplayName = processDisplayName ?? ProcessDisplayName,
            ExecutableName = executableName ?? ExecutableName,
            ExecutablePath = executablePath ?? ExecutablePath,
            IconPath = iconPath ?? IconPath,
            Icon = icon ?? Icon,
            SessionControl = sessionControl ?? SessionControl
        };

    public override string ToString() => $"{ExecutableName} [PID: {ProcessId}]";
}
