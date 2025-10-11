using System;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace VolumeKeeper.Services.Managers;

// ReSharper disable PropertyCanBeMadeInitOnly.Global UnusedAutoPropertyAccessor.Global InconsistentNaming
public sealed class ConfigurableIMMNotificationClient : IMMNotificationClient
{
    public Action<string, DeviceState>? OnDeviceStateChangedHandler { get; set; }
    public Action<string>? OnDeviceAddedHandler { get; set; }
    public Action<string>? OnDeviceRemovedHandler { get; set; }
    public Action<DataFlow, Role, string>? OnDefaultDeviceChangedHandler { get; set; }
    public Action<string, PropertyKey>? OnPropertyValueChangedHandler { get; set; }

    public void OnDeviceStateChanged(string deviceId, DeviceState newState)
        => OnDeviceStateChangedHandler?.Invoke(deviceId, newState);

    public void OnDeviceAdded(string pwstrDeviceId)
        => OnDeviceAddedHandler?.Invoke(pwstrDeviceId);

    public void OnDeviceRemoved(string deviceId)
        => OnDeviceRemovedHandler?.Invoke(deviceId);

    public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId)
        => OnDefaultDeviceChangedHandler?.Invoke(flow, role, defaultDeviceId);

    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        => OnPropertyValueChangedHandler?.Invoke(pwstrDeviceId, key);
}
