using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using NAudio.CoreAudioApi;
using VolumeKeeper.Services.Managers;

namespace VolumeKeeper.Models;

public sealed partial class ObservableAudioSession : INotifyPropertyChanged
{
    private AudioSession? _audioSession;
    private int? _pinnedVolume;
    public DateTimeOffset? LastTimeVolumeOrMuteWereManuallySet { get; private set; }
    public ConfigurableAudioSessionEventsHandler? EventHandler { get; set; }
    private static readonly TimeSpan VolumeChangedFromProgramThreshold = TimeSpan.FromMilliseconds(200);

    public bool WasAudioChangedFromWithinThisProgram
    {
        get
        {
            var value = LastTimeVolumeOrMuteWereManuallySet;
            if (value == null) return false;
            return DateTimeOffset.Now - value < VolumeChangedFromProgramThreshold;
        }
    }

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
            if (!Equals(oldValue?.Icon, value.Icon)) changedProperties.Add(nameof(Icon));
            if (!Equals(oldValue?.SessionControl, value.SessionControl)) changedProperties.Add(nameof(SessionControl));
            if (!Equals(oldValue?.AppId, value.AppId)) changedProperties.Add(nameof(AppId));
            if (oldValue?.Volume != value.Volume)
            {
                changedProperties.Add(nameof(Volume));
                changedProperties.Add(nameof(VolumeDisplayText));
                changedProperties.Add(nameof(HasUnsavedChanges));
                changedProperties.Add(nameof(VolumeIcon));
                changedProperties.Add(nameof(RevertButtonVisibility));
            }
            IsMuted = value.IsMuted;

            foreach (var changedProperty in changedProperties)
            {
                OnPropertyChanged(changedProperty);
            }
        }
    }

    public int ProcessId => _audioSession?.ProcessId ?? 0;

    public string ProcessDisplayName => _audioSession?.ProcessDisplayName ?? throw new InvalidOperationException("AudioSession is not set.");

    public string ExecutableName => _audioSession?.ExecutableName ?? throw new InvalidOperationException("AudioSession is not set.");

    public string ExecutablePath => _audioSession?.ExecutablePath ?? throw new InvalidOperationException("AudioSession is not set.");

    public int Volume
    {
        get => _audioSession?.Volume ?? 0;
        set => SetVolume(value);
    }

    public void SetVolume(int value, bool setLastSet = true)
    {
        var currentValue = AudioSession.Volume;
        if (currentValue == value) return;
        if (setLastSet) LastTimeVolumeOrMuteWereManuallySet = DateTimeOffset.Now;
        AudioSession.Volume = value;
        NotifyVolumeOrMuteChanged();

        if (value > 0 && IsMuted)
        {
            SetMute(false, setLastSet: setLastSet);
        }
    }

    public bool IsMuted
    {
        get => _audioSession?.IsMuted ?? false;
        set => SetMute(value);
    }

    public void SetMute(bool value, bool setLastSet = true)
    {
        var currentValue = AudioSession.IsMuted;
        if (currentValue == value) return;
        if (setLastSet) LastTimeVolumeOrMuteWereManuallySet = DateTimeOffset.Now;
        AudioSession.IsMuted = value;
        NotifyVolumeOrMuteChanged();
    }

    public string IconPath => _audioSession?.IconPath ?? string.Empty;

    public BitmapImage? Icon => _audioSession?.Icon;

    public AudioSessionControl SessionControl => _audioSession?.SessionControl ?? throw new InvalidOperationException("AudioSession is not set.");

    public int? PinnedVolume
    {
        get => _pinnedVolume;
        set
        {
            if (SetField(ref _pinnedVolume, value))
            {
                OnPropertyChanged(nameof(HasUnsavedChanges));
                OnPropertyChanged(nameof(PinnedVolumeDisplay));
                OnPropertyChanged(nameof(RevertButtonVisibility));
            }
        }
    }

    public VolumeApplicationId AppId => _audioSession?.AppId ?? throw new InvalidOperationException("AudioSession is not set.");

    public bool HasUnsavedChanges => PinnedVolume.HasValue && PinnedVolume.Value != Volume;

    public string PinnedVolumeDisplay => !PinnedVolume.HasValue ? "No pinned volume" : $"Pinned: {PinnedVolume}%";

    public string VolumeDisplayText => $"{Volume}%";

    public Visibility RevertButtonVisibility => HasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;

    public Symbol VolumeIcon => Volume == 0 || IsMuted ? Symbol.Mute : Symbol.Volume;

    public void NotifyVolumeOrMuteChanged()
    {
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(IsMuted));
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(VolumeDisplayText));
        OnPropertyChanged(nameof(VolumeIcon));
        OnPropertyChanged(nameof(RevertButtonVisibility));
    }

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

    public override string ToString() =>
        $"ExecutableName={ExecutableName}, ProcessId={ProcessId}, Volume={Volume}, IsMuted={IsMuted}, PinnedVolume={PinnedVolume?.ToString()}, IconPath={IconPath}, HasIcon={Icon != null}";
}
