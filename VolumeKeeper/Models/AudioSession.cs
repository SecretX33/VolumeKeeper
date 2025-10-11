using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml.Media.Imaging;
using NAudio.CoreAudioApi;

namespace VolumeKeeper.Models;

public sealed partial class AudioSession : IDisposable
{
    public required int ProcessId { get; init; }
    public required string ProcessDisplayName { get; init; }
    public required string ExecutableName { get; init; }
    public required string ExecutablePath { get; init; }
    public required string IconPath { get; init; }
    public BitmapImage? Icon { get; init; }
    public required IReadOnlyList<AudioSessionControl> SessionControls { get; init; }
    public AudioSessionControl MainSessionControl => SessionControls.FirstOrDefault() ?? throw new InvalidOperationException("No AudioSessionControl available.");

    public int Volume
    {
        get => (int)Math.Round(MainSessionControl.SimpleAudioVolume.Volume * 100);
        set
        {
            foreach (var audioSessionControl in SessionControls)
            {
                audioSessionControl.SimpleAudioVolume.Volume = value / 100f;
            }
        }
    }

    public bool IsMuted
    {
        get => MainSessionControl.SimpleAudioVolume.Mute;
        set
        {
            foreach (var audioSessionControl in SessionControls)
            {
                audioSessionControl.SimpleAudioVolume.Mute = value;
            }
        }
    }

    public VolumeApplicationId AppId => new(ExecutablePath);

    public AudioSession With(
        int? processId = null,
        string? processDisplayName = null,
        string? executableName = null,
        string? executablePath = null,
        string? iconPath = null,
        BitmapImage? icon = null,
        IReadOnlyList<AudioSessionControl>? sessionControls = null
    ) => new()
        {
            ProcessId = processId ?? ProcessId,
            ProcessDisplayName = processDisplayName ?? ProcessDisplayName,
            ExecutableName = executableName ?? ExecutableName,
            ExecutablePath = executablePath ?? ExecutablePath,
            IconPath = iconPath ?? IconPath,
            Icon = icon ?? Icon,
            SessionControls = sessionControls ?? SessionControls
        };

    public override string ToString() => $"{ExecutableName} [PID: {ProcessId}]";

    public void Dispose()
    {
        try
        {
            foreach (var audioSessionControl in SessionControls)
            {
                try
                {
                    audioSessionControl.Dispose();
                }
                catch
                {
                    // Ignore exceptions on dispose of individual session controls
                }
            }
        }
        catch
        {
            // Ignore exceptions on dispose
        }
    }
}
