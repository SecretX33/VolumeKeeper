using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using VolumeKeeper.Models;
using VolumeKeeper.Services.Log;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;

namespace VolumeKeeper.Services.Managers;

public sealed partial class AudioSessionManager(
    IconService iconService,
    VolumeSettingsManager volumeSettingsManager,
    DispatcherQueue mainThreadQueue
) : IDisposable
{
    private readonly Logger _logger = App.Logger.Named();
    private readonly MMDeviceEnumerator _deviceEnumerator = new();
    private readonly AtomicReference<MMDevice?> _defaultDevice = new(null);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly AtomicReference<bool> _isDisposed = new(false);

    private IMMNotificationClient? _audioDeviceChangeCallback;
    public ObservableCollection<ObservableAudioSession> AudioSessions { get; } = [];

    public void Initialize()
    {
        _audioDeviceChangeCallback = new ConfigurableIMMNotificationClient
        {
            OnDefaultDeviceChangedHandler = (dataFlow, role, _) =>
            {
                if (dataFlow != DataFlow.Render || role != Role.Multimedia) return;
                Task.Run(RefreshDeviceAndAudioSessions);
            }
        };
        _deviceEnumerator.RegisterEndpointNotificationCallback(_audioDeviceChangeCallback);
        RefreshDeviceAndAudioSessions();
    }

    public ObservableAudioSession? GetSessionById(VolumeApplicationId volumeApplicationId) =>
        AudioSessions.FirstOrDefault(session => Equals(session.AppId, volumeApplicationId));

    public ObservableAudioSession? GetSessionByProcessId(int processId) =>
        AudioSessions.FirstOrDefault(session => session.ProcessId == processId);

    public void UpdateAllSessions()
    {
        try
        {
            var currentSessions = GetAllAudioSessions();
            var currentSessionIds = currentSessions.Select(s => s.AppId).ToHashSet();

            mainThreadQueue.TryEnqueueImmediate(() =>
            {
                foreach (var session in currentSessions)
                {
                    AddOrUpdateSession(session);
                }

                var sessionsToRemove = AudioSessions.Where(s => !currentSessionIds.Contains(s.AppId)).ToList();
                foreach (var session in sessionsToRemove)
                {
                    _logger.Info($"Audio session ended for {session.ExecutableName} (PID: {session.ProcessId})");
                    RemoveSession(session);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to update audio sessions", ex);
        }
    }

    private IReadOnlyList<AudioSession> GetAllAudioSessions()
    {
        var audioDevice = _defaultDevice.Get();
        if (audioDevice == null) return ImmutableList<AudioSession>.Empty;

        var sessionControls = audioDevice.AudioSessionManager.FreshSessions();
        var audioSessions = new List<AudioSession>();

        try
        {
            audioSessions.AddRange(
                sessionControls.Select(sessionControl => CreateAudioSession(sessionControl))
                    .OfType<AudioSession>()
                    .Where(session => !string.IsNullOrWhiteSpace(session.ExecutableName))
            );
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to enumerate audio sessions", ex);
        }

        return audioSessions;
    }

    private void RefreshDevice()
    {
        try
        {
            var currentDefaultDevice = _defaultDevice.Get();
            var newDefaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

            if (currentDefaultDevice is { State: DeviceState.Active } && currentDefaultDevice.ID == newDefaultDevice.ID)
            {
                _logger.Debug("Default audio device has not changed, skipping device refresh");
                newDefaultDevice.Dispose();
                return;
            }
            if (currentDefaultDevice == null)
            {
                _logger.Debug($"Default audio device: '{newDefaultDevice.FriendlyName}'");
            }

            newDefaultDevice.AudioSessionManager.OnSessionCreated += (sender, session) =>
            {
                // The session object doesn't contain any useful info, so we need to fallback to UpdateAllSessions
                _logger.Debug("New audio session created, refreshing audio sessions");
                _ = Task.Run(UpdateAllSessions);
            };
            if (currentDefaultDevice != null && currentDefaultDevice.FriendlyName != newDefaultDevice.FriendlyName)
            {
                _logger.Info($"Default audio device changed: '{currentDefaultDevice.FriendlyName}' -> '{newDefaultDevice.FriendlyName}'");
            }
            _defaultDevice.GetAndSet(newDefaultDevice)?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get default audio device", ex);
        }
    }

    public void RefreshDeviceAndAudioSessions()
    {
        RefreshDevice();
        UpdateAllSessions();
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
            var processDisplayName = new[]
            {
                sessionControl.DisplayName,
                processInfo.DisplayName
            }.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))
                ?? string.Empty;

            var audioSession = new AudioSession
            {
                ProcessId = processId,
                ProcessDisplayName = processDisplayName,
                ExecutableName = processInfo.ExecutableName,
                ExecutablePath = processInfo.ExecutablePath,
                IconPath = sessionControl.IconPath ?? string.Empty,
                SessionControl = sessionControl
            };
            return audioSession;
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to create audio session", ex);
            return null;
        }
    }

    private void AddOrUpdateSession(AudioSession session)
    {
        if (!mainThreadQueue.HasThreadAccess)
            throw new InvalidOperationException("AddOrUpdateSession must be called on the UI thread.");

        var savedVolume = volumeSettingsManager.GetVolume(session.AppId);
        var observableSession = GetSessionByProcessId(session.ProcessId);
        var sessionAlreadyExisted = observableSession != null;
        var audioSessionChanged = observableSession?.SessionControl != null && !ReferenceEquals(observableSession.SessionControl, session.SessionControl);

#if DEBUG
        App.Logger.Debug($"{(!sessionAlreadyExisted ? "Adding" : "Updating")} audio session. PID={session.ProcessId}, ExecutableName={session.ExecutableName}, SavedVolume={(savedVolume.HasValue ? savedVolume.Value.ToString() : "null")}, AudioSessionChanged={audioSessionChanged}, SessionControlAddress={RuntimeHelpers.GetHashCode(observableSession?.SessionControl)}, NewSessionControlAddress={RuntimeHelpers.GetHashCode(session.SessionControl)}");
#endif

        if (observableSession?.AudioSession == null || audioSessionChanged)
        {
            var newSession = observableSession ?? new ObservableAudioSession
            {
                AudioSession = session,
            };
            newSession.PinnedVolume = savedVolume;

            var eventHandler = new ConfigurableAudioSessionEventsHandler
            {
                OnVolumeChangedHandler = (_, _) => RestoreSessionVolumeAndNotifyChanges(newSession),

                OnSessionDisconnectedHandler = _ =>
                {
                    _logger.Debug($"Audio session disconnected for {newSession.ExecutableName} (PID: {newSession.ProcessId})");
                    mainThreadQueue.TryEnqueueImmediate(() => RemoveSession(newSession));
                },

                OnStateChangedHandler = state =>
                {
                    if (state != AudioSessionState.AudioSessionStateExpired) return;

                    _logger.Debug($"Audio session expired for {newSession.ExecutableName} (PID: {newSession.ProcessId})");
                    mainThreadQueue.TryEnqueueImmediate(() => RemoveSession(newSession));
                }
            };
            session.SessionControl.RegisterEventClient(eventHandler);
            newSession.EventHandler = eventHandler;
            RestoreSessionVolumeAndNotifyChanges(newSession);
            observableSession = newSession;
            if (!sessionAlreadyExisted)
            {
                AudioSessions.Add(observableSession);
            }
        }

        observableSession.AudioSession = session;
        observableSession.PinnedVolume = savedVolume;
        LoadApplicationIconAsync(observableSession);
    }

    private void RemoveSession(ObservableAudioSession session)
    {
        if (!mainThreadQueue.HasThreadAccess)
            throw new InvalidOperationException("RemoveSession must be called on the UI thread.");

        try
        {
            if (session.EventHandler != null)
            {
                session.SessionControl.UnRegisterEventClient(session.EventHandler);
                session.EventHandler = null;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to unregister event client for {session.ExecutableName} (PID: {session.ProcessId})", ex);
        }

        AudioSessions.Remove(session);
    }

    private void RestoreSessionVolumeAndNotifyChanges(ObservableAudioSession newSession)
    {
        var autoRestoreEnabled = volumeSettingsManager.AutoRestoreEnabled;
        var wasSetFromProgram = newSession.WasAudioChangedFromWithinThisProgram;
        var pinnedVolume = newSession.PinnedVolume;

        mainThreadQueue.TryEnqueueImmediate(() =>
        {
            if (autoRestoreEnabled && !wasSetFromProgram && pinnedVolume != null && newSession.Volume != pinnedVolume)
            {
                _logger.Debug($"App volume {newSession.ExecutableName} (PID: {newSession.ProcessId}) was changed to {newSession.Volume}, reverting it to pinned volume {pinnedVolume}");
                newSession.SetVolume(pinnedVolume.Value, setLastSet: false);
            }
            else
            {
                newSession.NotifyVolumeOrMuteChanged();
            }
        });
    }

    private async void LoadApplicationIconAsync(ObservableAudioSession app)
    {
        try
        {
            var icon = await iconService.GetApplicationIconAsync(
                iconPath: app.IconPath,
                executablePath: app.ExecutablePath,
                executableName: app.ExecutableName
            ).ConfigureAwait(false);

            if (icon != null)
            {
                mainThreadQueue.TryEnqueueImmediate(() => { app.AudioSession = app.AudioSession.With(icon: icon); });
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to load icon for {app.ExecutableName} (PID: {app.ProcessId})", ex);
        }
    }

    public void Dispose()
    {
        if (!_isDisposed.CompareAndSet(false, true))
            return;

        try
        {
            if (_audioDeviceChangeCallback != null)
            {
                _deviceEnumerator.UnregisterEndpointNotificationCallback(_audioDeviceChangeCallback);
                _audioDeviceChangeCallback = null;
            }
            DisposeAll(_deviceEnumerator, _defaultDevice.Get(), _cacheLock);
        }
        catch
        {
            /* Ignore exceptions during dispose */
        }
    }
}
