using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using NAudio.CoreAudioApi;

namespace VolumeKeeper.Models;

public partial class ObservableAudioSession : INotifyPropertyChanged
{
    private AudioSession? _audioSession;
    private string _status = "Active";
    private string _lastSeen = "Just now";
    private int? _pinnedVolume;

    public AudioSession AudioSession
    {
        get => _audioSession ?? throw new InvalidOperationException("AudioSession is not set.");
        set
        {
            var oldValue = _audioSession;
            if (!SetField(ref _audioSession, value)) return;

            var changedProperties = new HashSet<string>();

            if (!Equals(oldValue?.ProcessId, value.ProcessId)) changedProperties.Add(nameof(ProcessId));
            if (!Equals(oldValue?.ProcessDisplayName, value.ProcessDisplayName)) changedProperties.Add(nameof(ProcessDisplayName));
            if (!Equals(oldValue?.ExecutableName, value.ExecutableName)) changedProperties.Add(nameof(ExecutableName));
            if (!Equals(oldValue?.ExecutablePath, value.ExecutablePath)) changedProperties.Add(nameof(ExecutablePath));
            if (!Equals(oldValue?.IconPath, value.IconPath)) changedProperties.Add(nameof(IconPath));
            if (!Equals(oldValue?.SessionControl, value.SessionControl)) changedProperties.Add(nameof(SessionControl));
            if (!Equals(oldValue?.AppId, value.AppId)) changedProperties.Add(nameof(AppId));
            if (oldValue?.Volume != value.Volume)
            {
                changedProperties.Add(nameof(Volume));
                changedProperties.Add(nameof(VolumeDisplayText));
                changedProperties.Add(nameof(HasUnsavedChanges));
                changedProperties.Add(nameof(VolumeIcon));
            }
            IsMuted = value.IsMuted;

            foreach (var changedProperty in changedProperties)
            {
                OnPropertyChanged(changedProperty);
            }
        }
    }

    public int ProcessId => _audioSession?.ProcessId ?? 0;

    public string ProcessDisplayName => _audioSession?.ProcessDisplayName ?? string.Empty;

    public string ExecutableName => _audioSession?.ExecutableName ?? string.Empty;

    public string? ExecutablePath => _audioSession?.ExecutablePath;

    public int Volume
    {
        get => _audioSession?.Volume ?? 0;
        set
        {
            var currentValue = AudioSession.Volume;
            if (currentValue == value) return;
            AudioSession.Volume = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumeDisplayText));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(VolumeIcon));

            if (value > 0 && IsMuted)
            {
                IsMuted = false;
            }
        }
    }

    public bool IsMuted
    {
        get => _audioSession?.IsMuted ?? false;
        set
        {
            var currentValue = AudioSession.SessionControl.SimpleAudioVolume.Mute;
            if (currentValue == value) return;
            AudioSession.SessionControl.SimpleAudioVolume.Mute = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VolumeIcon));
        }
    }

    public string IconPath => _audioSession?.IconPath ?? string.Empty;

    public BitmapImage? Icon => _audioSession?.Icon;

    public AudioSessionControl SessionControl => _audioSession?.SessionControl ?? throw new InvalidOperationException("AudioSession is not set.");

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public string LastSeen
    {
        get => _lastSeen;
        set => SetField(ref _lastSeen, value);
    }

    public int? PinnedVolume
    {
        get => _pinnedVolume;
        set
        {
            if (SetField(ref _pinnedVolume, value))
            {
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(PinnedVolumeDisplay));
            }
        }
    }

    public VolumeApplicationId AppId => _audioSession?.AppId ?? throw new InvalidOperationException("AudioSession is not set.");

    public bool HasUnsavedChanges => PinnedVolume.HasValue && Math.Abs(PinnedVolume.Value - Volume) > 1.0;

    public string PinnedVolumeDisplay => !PinnedVolume.HasValue ? "No pinned volume" : $"Pinned: {PinnedVolume}%";

    public string VolumeDisplayText => $"{Volume}%";

    public Visibility PinButtonVisibility => Visibility.Visible;
    public Visibility RevertButtonVisibility => HasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
    public Symbol VolumeIcon => Volume == 0 || IsMuted ? Symbol.Mute : Symbol.Volume;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return _audioSession != null
           && _audioSession.ProcessId.Equals(((ObservableAudioSession)obj)._audioSession?.ProcessId);
    }

    // ReSharper disable once NonReadonlyMemberInGetHashCode
    public override int GetHashCode() => _audioSession?.ProcessId ?? 0;
}
