using System;
using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace VolumeKeeper.Models.UI;

public sealed partial class ApplicationVolume : INotifyPropertyChanged
{
    private AudioSession _session = null!; // Initialized via property
    private string _applicationName = string.Empty;
    private int _volume;
    private int? _savedVolume;
    private string _status = string.Empty;
    private string _lastSeen = string.Empty;
    private BitmapImage? _icon;
    private const double VolumeDifferenceTolerance = 0.01;

    public VolumeApplicationId AppId => _session.AppId;
    public string ProcessName => _session.ProcessName;
    public string ExecutableName => _session.ExecutableName;
    public string? ExecutablePath => _session.ExecutablePath;
    public bool IsMuted => _session.IsMuted;

    public AudioSession Session
    {
        get => _session;
        set
        {
            _session = value;
            OnPropertyChanged(nameof(AppId));
            OnPropertyChanged(nameof(ProcessName));
            OnPropertyChanged(nameof(ExecutableName));
            OnPropertyChanged(nameof(ExecutablePath));
            OnPropertyChanged(nameof(IsMuted));
        }
    }

    public string ApplicationName
    {
        get => _applicationName;
        set
        {
            if (_applicationName == value) return;
            _applicationName = value;
            OnPropertyChanged(nameof(ApplicationName));
        }
    }


    public int Volume
    {
        get => _volume;
        set
        {
            if (!(Math.Abs(_volume - value) > VolumeDifferenceTolerance)) return;
            _volume = value;
            OnPropertyChanged(nameof(Volume));
            OnPropertyChanged(nameof(VolumeIcon));
            OnPropertyChanged(nameof(VolumeDisplayText));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(SaveButtonVisibility));
            OnPropertyChanged(nameof(RevertButtonVisibility));
            OnPropertyChanged(nameof(SavedVolumeDisplay));
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (_status != value)
            {
                _status = value;
                OnPropertyChanged(nameof(Status));
            }
        }
    }

    public string LastSeen
    {
        get => _lastSeen;
        set
        {
            if (_lastSeen != value)
            {
                _lastSeen = value;
                OnPropertyChanged(nameof(LastSeen));
            }
        }
    }


    public BitmapImage? Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                OnPropertyChanged(nameof(Icon));
            }
        }
    }


    public int? SavedVolume
    {
        get => _savedVolume;
        set
        {
            if (_savedVolume == value) return;
            _savedVolume = value;
            OnPropertyChanged(nameof(SavedVolume));
            OnPropertyChanged(nameof(SavedVolumeDisplay));
            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(SaveButtonVisibility));
            OnPropertyChanged(nameof(RevertButtonVisibility));
        }
    }

    public bool HasUnsavedChanges => SavedVolume.HasValue && Math.Abs(SavedVolume.Value - Volume) > 1.0;

    public string SavedVolumeDisplay => !SavedVolume.HasValue ? "No saved volume" : $"Saved: {SavedVolume}%";

    public string VolumeDisplayText => $"{(int)Volume}%";

    public Visibility SaveButtonVisibility => HasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;
    public Visibility RevertButtonVisibility => HasUnsavedChanges ? Visibility.Visible : Visibility.Collapsed;

    public Symbol VolumeIcon =>
        Volume == 0 ? Symbol.Mute : Symbol.Volume;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
