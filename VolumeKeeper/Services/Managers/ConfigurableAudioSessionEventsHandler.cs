using System;
using NAudio.CoreAudioApi.Interfaces;

namespace VolumeKeeper.Services.Managers;

public class ConfigurableAudioSessionEventsHandler : IAudioSessionEventsHandler
{
    public Action<float, bool>? OnVolumeChangedHandler { get; set; }
    public Action<string>? OnDisplayNameChangedHandler { get; set; }
    public Action<string>? OnIconPathChangedHandler { get; set; }
    public Action<uint, IntPtr, uint>? OnChannelVolumeChangedHandler { get; set; }
    public Action<Guid>? OnGroupingParamChangedHandler { get; set; }
    public Action<AudioSessionState>? OnStateChangedHandler { get; set; }
    public Action<AudioSessionDisconnectReason>? OnSessionDisconnectedHandler { get; set; }

    public void OnVolumeChanged(float volume, bool isMuted)
        => OnVolumeChangedHandler?.Invoke(volume, isMuted);

    public void OnDisplayNameChanged(string displayName)
        => OnDisplayNameChangedHandler?.Invoke(displayName);

    public void OnIconPathChanged(string iconPath)
        => OnIconPathChangedHandler?.Invoke(iconPath);

    public void OnChannelVolumeChanged(uint channelCount, IntPtr newVolumes, uint channelIndex)
        => OnChannelVolumeChangedHandler?.Invoke(channelCount, newVolumes, channelIndex);

    public void OnGroupingParamChanged(ref Guid groupingId)
        => OnGroupingParamChangedHandler?.Invoke(groupingId);

    public void OnStateChanged(AudioSessionState state)
        => OnStateChangedHandler?.Invoke(state);

    public void OnSessionDisconnected(AudioSessionDisconnectReason disconnectReason)
        => OnSessionDisconnectedHandler?.Invoke(disconnectReason);
}
