using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using VolumeKeeper.Models;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;

namespace VolumeKeeper.Services.Managers;

public partial class AudioSessionManager(
    IconService iconService,
    VolumeSettingsManager volumeSettingsManager
) : IDisposable
{
    private readonly TimeSpan _volumeChangedFromProgramThreshold = TimeSpan.FromMilliseconds(200);
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
            _sessions = newDefaultDevice.AudioSessionManager.Sessions;
            newDefaultDevice.AudioSessionManager.OnSessionCreated += (sender, session) =>
            {
                // The session object doesn't contain any useful info, so we need to fallback to UpdateAllSessions
                App.Logger.LogDebug("New audio session created, refreshing audio sessions", "AudioSessionManager");
                _ = Task.Run(UpdateAllSessions);
            };
            _defaultDevice.GetAndSet(newDefaultDevice)?.Dispose();
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

                var session = CreateAudioSession(sessionControl);
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
                    App.Logger.LogInfo($"Audio session ended for {session.ExecutableName} (PID: {session.ProcessId})", "AudioSessionManager");
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
                IconPath = sessionControl.IconPath ?? string.Empty,
                SessionControl = sessionControl
            };
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to create audio session", ex, "AudioSessionDataManager");
            return null;
        }
    }

    private void AddOrUpdateSession(AudioSession session)
    {
        if (!_dispatcherQueue.HasThreadAccess)
            throw new InvalidOperationException("AddOrUpdateSession must be called on the UI thread.");

        var savedVolume = volumeSettingsManager.GetVolume(session.AppId);
        var observableSession = GetSessionById(session.AppId);
        if (observableSession == null)
        {
            var newSession = new ObservableAudioSession
            {
                AudioSession = session,
                PinnedVolume = savedVolume
            };
            session.SessionControl.RegisterEventClient(new ConfigurableAudioSessionEventsHandler
            {
                OnVolumeChangedHandler = (_, _) =>
                {
                    App.Logger.LogDebug($"App volume {newSession.ExecutableName} (PID: {newSession.ProcessId}) changed to {newSession.Volume}",
                        "AudioSessionManager");
                    RestoreSessionVolume(newSession);
                },

                OnSessionDisconnectedHandler = _ =>
                {
                    App.Logger.LogDebug($"Audio session disconnected for {newSession.ExecutableName} (PID: {newSession.ProcessId})", "AudioSessionManager");
                    _dispatcherQueue.TryEnqueue(() => AudioSessions.Remove(newSession));
                },

                OnStateChangedHandler = state =>
                {
                    if (state != AudioSessionState.AudioSessionStateExpired) return;

                    App.Logger.LogDebug($"Audio session expired for {newSession.ExecutableName} (PID: {newSession.ProcessId})", "AudioSessionManager");
                    _dispatcherQueue.TryEnqueue(() => AudioSessions.Remove(newSession));
                }
            });
            RestoreSessionVolume(newSession);
            LoadApplicationIconAsync(newSession);
            observableSession = newSession;
            AudioSessions.Add(observableSession);
        }

        observableSession.AudioSession = session;
        observableSession.PinnedVolume = savedVolume;
    }

    private void RestoreSessionVolume(ObservableAudioSession newSession)
    {
        if (!volumeSettingsManager.AutoRestoreEnabled) return;

        var pinnedVolume = newSession.PinnedVolume;
        var wasSetFromProgram = newSession.LastTimeVolumeOrMuteWereManuallySet.HasValue
            && (DateTimeOffset.Now - newSession.LastTimeVolumeOrMuteWereManuallySet).Value < _volumeChangedFromProgramThreshold;

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (!wasSetFromProgram && pinnedVolume != null && newSession.Volume != pinnedVolume)
            {
                App.Logger.LogDebug($"Reverting volume for {newSession.ExecutableName} (PID: {newSession.ProcessId}) to pinned volume {pinnedVolume}%",
                    "AudioSessionManager");
                newSession.SetVolume(pinnedVolume.Value, setLastSet: false);
            }
            else
            {
                newSession.NotifyVolumeOrMuteChanged();
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
                App.Logger.LogWarning($"Failed to load icon for {app.ExecutableName}", ex, "HomePage");
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
