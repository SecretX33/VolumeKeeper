using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using VolumeKeeper.Models;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;

namespace VolumeKeeper.Services.Managers;

public partial class AudioSessionManager(IconService iconService) : IDisposable
{
    private readonly MMDeviceEnumerator _deviceEnumerator = new();
    private readonly AtomicReference<MMDevice?> _defaultDevice = new(null);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly AtomicReference<bool> _isDisposed = new(false);
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    private volatile SessionCollection? _sessions;
    public ObservableCollection<ObservableAudioSession> AudioSessions { get; } = [];

    public void Initialize()
    {
        UpdateAllSessions();
    }

    public ObservableAudioSession? GetSessionById(VolumeApplicationId volumeApplicationId) =>
        AudioSessions.FirstOrDefault(session => Equals(session.AppId, volumeApplicationId));

    public ObservableAudioSession? GetSessionByProcessId(int processId) =>
        AudioSessions.FirstOrDefault(session => session.ProcessId == processId);

    private void RefreshDevice()
    {
        try
        {
            var newDefaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _defaultDevice.GetAndSet(newDefaultDevice)?.Dispose();
            _sessions = newDefaultDevice.AudioSessionManager.Sessions;
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to get default audio device", ex, "AudioSessionDataManager");
        }
    }

    private List<AudioSession> GetAllAudioSessions()
    {
        var sessions = new List<AudioSession>();
        RefreshDevice();

        if (_sessions == null)
            return sessions;

        try
        {
            for (int i = 0; i < _sessions.Count; i++)
            {
                var sessionControl = _sessions[i];
                if (sessionControl == null)
                    continue;

                var simpleVolume = sessionControl.SimpleAudioVolume;
                if (simpleVolume == null)
                    continue;

                var session = CreateAudioSession(sessionControl, simpleVolume);
                if (session != null && !string.IsNullOrWhiteSpace(session.ExecutableName))
                {
                    sessions.Add(session);
                }
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to enumerate audio sessions", ex, "AudioSessionDataManager");
        }

        return sessions;
    }

    public void UpdateAllSessions()
    {
        try
        {
            var currentSessions = GetAllAudioSessions();
            var currentSessionIds = currentSessions.Select(s => s.AppId).ToHashSet();

            _dispatcherQueue.TryEnqueue(() =>
            {
                foreach (var session in currentSessions)
                {
                    AddOrUpdateSession(session);
                }

                var sessionsToRemove = AudioSessions.Where(s => !currentSessionIds.Contains(s.AppId)).ToList();
                foreach (var session in sessionsToRemove)
                {
                    App.Logger.LogInfo($"Audio session ended for {session.ProcessDisplayName}", "AudioSessionManager");
                    AudioSessions.Remove(session);
                }
            });
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to update audio sessions", ex, "AudioSessionManager");
        }
    }

    private AudioSession? CreateAudioSession(
        AudioSessionControl sessionControl,
        SimpleAudioVolume simpleVolume,
        ProcessInfo? fetchedProcessInfo = null
    ) {
        try
        {
            var processId = (int)sessionControl.GetProcessID;
            var processInfo = fetchedProcessInfo ?? GetProcessInfoOrNull(processId);
            if (processInfo == null) return null;

#if DEBUG
            App.Logger.LogDebug($"Found audio session: PID={processId}, Name={processInfo.DisplayName} ({processInfo.ExecutableName}), Path={processInfo.ExecutablePath}");
#endif

            return new AudioSession
            {
                ProcessId = processId,
                ProcessDisplayName = processInfo.DisplayName,
                ExecutableName = processInfo.ExecutableName,
                ExecutablePath = processInfo.ExecutablePath,
                Volume = (int)Math.Round(simpleVolume.Volume * 100),
                IsMuted = simpleVolume.Mute,
                IconPath = sessionControl.IconPath ?? string.Empty,
                SessionControl = sessionControl
            };
        }
        catch
        {
            return null;
        }
    }

    private AudioSessionControl? GetAudioSessionByProcessId(int processId)
    {
        if (_sessions == null) return null;
        for (int i = 0; i < _sessions.Count; i++)
        {
            var sessionControl = _sessions[i];
            if (sessionControl?.GetProcessID == processId) return sessionControl;
        }
        return null;
    }

    public void OnProcessStarted(ProcessInfo processInfo)
    {
        RefreshDevice();

        var audioSessionControl = GetAudioSessionByProcessId(processInfo.Id);
        if (audioSessionControl == null) return;

        var simpleVolume = audioSessionControl.SimpleAudioVolume;
        if (simpleVolume == null) return;

        var session = CreateAudioSession(audioSessionControl, simpleVolume, processInfo);
        if (session == null) return;

        _dispatcherQueue.TryEnqueue(() => AddOrUpdateSession(session));
    }

    private void AddOrUpdateSession(AudioSession session)
    {
        if (!_dispatcherQueue.HasThreadAccess)
            throw new InvalidOperationException("AddOrUpdateSession must be called on the UI thread.");

        var savedVolume = App.VolumeSettingsManager.GetVolume(session.AppId);
        var observableSession = GetSessionByProcessId(session.ProcessId);
        if (observableSession == null)
        {
            var newSession = new ObservableAudioSession
            {
                AudioSession = session,
                SavedVolume = savedVolume
            };
            LoadApplicationIconAsync(newSession);
            observableSession = newSession;
            AudioSessions.Add(observableSession);
        }

        observableSession.AudioSession = session;
        observableSession.SavedVolume = savedVolume;
        observableSession.Status = "Active";
        observableSession.LastSeen = "Just now";
    }

    public void OnProcessStopped(int processId)
    {
        _dispatcherQueue.TryEnqueue(() =>
        {
            foreach (var session in AudioSessions.Where(it => it.ProcessId == processId))
            {
                AudioSessions.Remove(session);
            }
        });
    }

    private void LoadApplicationIconAsync(ObservableAudioSession app)
    {
        Task.Run(async () =>
        {
            try
            {
                var icon = await iconService.GetApplicationIconAsync(app.IconPath, app.ProcessDisplayName);
                if (icon != null)
                {
                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        app.AudioSession = app.AudioSession.With(icon: icon);
                    });
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogWarning($"Failed to load icon for {app.ExecutableName}: {ex.Message}", "HomePage");
            }
        });
    }

    public void Dispose()
    {
        if (!_isDisposed.CompareAndSet(false, true))
            return;

        try
        {
            DisposeAll(_deviceEnumerator, _defaultDevice.Get(), _cacheLock);
        }
        catch
        {
            /* Ignore exceptions during dispose */
        }
        GC.SuppressFinalize(this);
    }
}
