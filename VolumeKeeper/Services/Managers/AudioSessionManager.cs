using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
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
    private static readonly TimeSpan RefreshAudioSessionDelay = TimeSpan.FromMilliseconds(200);
    private readonly MMDeviceEnumerator _deviceEnumerator = new();
    private readonly AtomicReference<IReadOnlyList<MMDevice>> _allAudioDevices = new(ImmutableList<MMDevice>.Empty);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly AtomicReference<bool> _isDisposed = new(false);
    private readonly AtomicReference<CancellationTokenSource?> _refreshAudioDevicesAndSessionDebounceTokenSource = new(null);
    private readonly AtomicReference<CancellationTokenSource?> _updateAllAudioSessionsDebounceTokenSource = new(null);
    private readonly SemaphoreSlim _refreshAudioDevicesLock = new(1, 1);
    private readonly SemaphoreSlim _updateAllSessionsLock = new(1, 1);

    private IMMNotificationClient? _audioDeviceChangeCallback;
    public ObservableCollection<ObservableAudioSession> AudioSessions { get; } = [];

    public async Task Initialize()
    {
        _audioDeviceChangeCallback = new ConfigurableIMMNotificationClient
        {
            OnDeviceStateChangedHandler = (_, _) => ScheduleRefreshDeviceAndAudioSessions(),
            OnDeviceAddedHandler = _ => ScheduleRefreshDeviceAndAudioSessions(),
            OnDeviceRemovedHandler = _ => ScheduleRefreshDeviceAndAudioSessions(),
            OnDefaultDeviceChangedHandler = (dataFlow, role, _) =>
            {
                if (dataFlow != DataFlow.Render || role != Role.Multimedia) return;
                ScheduleRefreshDeviceAndAudioSessions();
            }
        };
        _deviceEnumerator.RegisterEndpointNotificationCallback(_audioDeviceChangeCallback);
        await RefreshDeviceAndAudioSessions(CancellationToken.None).ConfigureAwait(false);
    }

    public ObservableAudioSession? GetSessionById(VolumeApplicationId volumeApplicationId) =>
        AudioSessions.FirstOrDefault(session => Equals(session.AppId, volumeApplicationId));

    public ObservableAudioSession? GetSessionByProcessId(uint processId) =>
        AudioSessions.FirstOrDefault(session => session.ProcessId == processId);

    public Task ScheduleUpdateAllAudioSessions(bool immediately = false)
    {
        _logger.Debug("Scheduling update all audio sessions with debounce");

        var cancellationTokenSource = new CancellationTokenSource();
        var oldCancellationTokenSource = _updateAllAudioSessionsDebounceTokenSource.GetAndSet(cancellationTokenSource);
        var cancellationToken = cancellationTokenSource.Token;

        return Task.Run(async () =>
        {
            try
            {
                if (oldCancellationTokenSource != null)
                {
                    _logger.Debug("Cancelling previously scheduled audio session refresh");
                    await oldCancellationTokenSource.CancelAsync().ConfigureAwait(false);
                }

                if (!immediately)
                {
                    await Task.Delay(RefreshAudioSessionDelay, cancellationToken).ConfigureAwait(false);
                }
                if (cancellationToken.IsCancellationRequested) return;

                await UpdateAllSessions(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when debounce is cancelled
            }
            finally
            {
                _updateAllAudioSessionsDebounceTokenSource.CompareAndSet(cancellationTokenSource, null);
                cancellationTokenSource.Dispose();
            }
        }, cancellationToken);
    }

    public async Task UpdateAllSessions(CancellationToken cancellationToken)
    {
        await _updateAllSessionsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        // Lock has been acquired, do NOT cancel from this point on
        try
        {
            var currentSessions = GetAllAudioSessions();
            var currentSessionIds = currentSessions.Select(s => s.AppId).ToHashSet();

            await mainThreadQueue.TryFetchImmediate(() =>
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

                return true;
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to update audio sessions", ex);
        }
        finally
        {
            _updateAllSessionsLock.Release();
        }
    }

    private IReadOnlyList<AudioSession> GetAllAudioSessions()
    {
        var audioDevices = _allAudioDevices.Get();
        if (audioDevices.Count == 0) return ImmutableList<AudioSession>.Empty;

        var audioSessions = new List<AudioSession>();
        var sessionControls = audioDevices.SelectMany(audioDevice => audioDevice.AudioSessionManager.FreshSessions())
            .GroupBy(session => session.GetProcessID)
            .ToList();

        try
        {
            audioSessions.AddRange(
                sessionControls.Select(sessionControl => CreateAudioSession(sessionControl.ToList()))
                    .OfType<AudioSession>()
            );
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to enumerate audio sessions", ex);
        }

        return audioSessions;
    }

    private async Task RefreshDevices(CancellationToken cancellationToken)
    {
        await _refreshAudioDevicesLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        // Lock has been acquired, do NOT cancel from this point on
        try
        {
            var allDevices = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToList();
            if (allDevices.Count == 0)
            {
                _logger.Warn("No active audio output devices found");
                DisposeAllDevices(_allAudioDevices.GetAndSet(ImmutableList<MMDevice>.Empty));
                return;
            }

            var defaultDevice = _deviceEnumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                ? _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : null;

            // Ensure the default device is always first in the list
            if (defaultDevice != null)
            {
                var index = allDevices.FindIndex(item => item.ID == defaultDevice.ID);
                if (index >= 0)
                {
                    allDevices[index].Dispose();
                    allDevices.RemoveAt(index);
                }
                allDevices.Insert(0, defaultDevice);
            }

            // If the audio devices have not changed, skip the refresh
            var currentAudioDevices = _allAudioDevices.Get();
            if (currentAudioDevices.Select(d => d.ID).SequenceEqual(allDevices.Select(d => d.ID)))
            {
                _logger.Debug("Default audio devices have not changed, skipping device refresh");
                DisposeAllDevices(allDevices);
                return;
            }

            foreach (var device in allDevices)
            {
                device.AudioSessionManager.OnSessionCreated += (sender, session) =>
                {
                    // The session object doesn't contain any useful info, so we need to fallback to UpdateAllSessions
                    _logger.Debug("New audio session created, refreshing audio sessions");
                    _ = ScheduleUpdateAllAudioSessions();
                };
            }

            var previousDefaultDevice = currentAudioDevices.FirstOrDefault();
            var isChangingDefaultDevice = previousDefaultDevice?.FriendlyName != defaultDevice?.FriendlyName;

            switch (isChangingDefaultDevice)
            {
                case true when previousDefaultDevice != null && defaultDevice != null:
                    _logger.Debug($"Default audio device changed: '{previousDefaultDevice.FriendlyName}' -> '{defaultDevice.FriendlyName}'");
                    break;

                case true when defaultDevice != null:
                    _logger.Debug($"Default audio device: '{defaultDevice.FriendlyName}'");
                    break;
            }

            _logger.Debug($"Active audio devices ({allDevices.Count}): {string.Join(", ", allDevices.Select(d => d.FriendlyName))}");

            DisposeAllDevices(_allAudioDevices.GetAndSet(allDevices));
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to get default audio device", ex);
        } finally
        {
            _refreshAudioDevicesLock.Release();
        }
    }

    public Task ScheduleRefreshDeviceAndAudioSessions(bool immediately = false)
    {
        _logger.Debug("Scheduling refresh of audio devices and sessions with debounce");

        var cancellationTokenSource = new CancellationTokenSource();
        var oldCancellationTokenSource = _refreshAudioDevicesAndSessionDebounceTokenSource.GetAndSet(cancellationTokenSource);
        var cancellationToken = cancellationTokenSource.Token;

        return Task.Run(async () =>
        {
            try
            {
                if (oldCancellationTokenSource != null)
                {
                    _logger.Debug("Cancelling previously scheduled audio devices and session refresh");
                    await oldCancellationTokenSource.CancelAsync().ConfigureAwait(false);
                }

                if (!immediately)
                {
                    await Task.Delay(RefreshAudioSessionDelay, cancellationToken).ConfigureAwait(false);
                }
                if (cancellationToken.IsCancellationRequested) return;

                await RefreshDeviceAndAudioSessions(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected when debounce is cancelled
            }
            finally
            {
                _refreshAudioDevicesAndSessionDebounceTokenSource.CompareAndSet(cancellationTokenSource, null);
                cancellationTokenSource.Dispose();
            }
        }, cancellationToken);
    }

    private async Task RefreshDeviceAndAudioSessions(CancellationToken cancellationToken)
    {
        await RefreshDevices(cancellationToken).ConfigureAwait(false);
        await UpdateAllSessions(cancellationToken).ConfigureAwait(false);
    }

    private AudioSession? CreateAudioSession(
        IReadOnlyList<AudioSessionControl> sessionControls,
        ProcessInfo? fetchedProcessInfo = null
    ) {
        if (sessionControls.Count == 0) return null;

        try
        {
            var sessionControl = sessionControls[0];
            var processId = sessionControl.GetProcessID;
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
                SessionControls = sessionControls
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
        var existingSession = GetSessionByProcessId(session.ProcessId);
        var sessionAlreadyExisted = existingSession != null;

#if DEBUG
        App.Logger.Debug($"{(!sessionAlreadyExisted ? "Adding" : "Updating")} audio session. PID={session.ProcessId}, ExecutableName={session.ExecutableName}, SavedVolume={(savedVolume.HasValue ? savedVolume.Value.ToString() : "null")}");
#endif

        var observableSession = existingSession ?? new ObservableAudioSession
        {
            AudioSession = session,
        };
        observableSession.PinnedVolume = savedVolume;
        observableSession.EventHandler ??= new ConfigurableAudioSessionEventsHandler
        {
            OnVolumeChangedHandler = (_, _) => RestoreSessionVolumeAndNotifyChanges(observableSession),

            OnSessionDisconnectedHandler = _ =>
            {
                _logger.Debug($"Audio session disconnected for {observableSession.ExecutableName} (PID: {observableSession.ProcessId})");
                mainThreadQueue.TryEnqueueImmediate(() => RemoveSession(observableSession));
            },

            OnStateChangedHandler = state =>
            {
                if (state != AudioSessionState.AudioSessionStateExpired) return;

                _logger.Debug($"Audio session expired for {observableSession.ExecutableName} (PID: {observableSession.ProcessId})");
                mainThreadQueue.TryEnqueueImmediate(() => RemoveSession(observableSession));
            }
        };

        if (!sessionAlreadyExisted || session != observableSession.AudioSession)
        {
            if (sessionAlreadyExisted)
            {
                observableSession.AudioSession.Dispose();
                observableSession.AudioSession = session;
            }

            foreach (var sessionControl in session.SessionControls)
            {
                sessionControl.RegisterEventClient(observableSession.EventHandler);
            }
        }

        if (!sessionAlreadyExisted)
        {
            AudioSessions.Add(observableSession);
        }

        RestoreSessionVolumeAndNotifyChanges(observableSession);
        LoadApplicationIconAsync(observableSession);
    }

    private void RemoveSession(ObservableAudioSession session)
    {
        if (!mainThreadQueue.HasThreadAccess)
            throw new InvalidOperationException("RemoveSession must be called on the UI thread.");

        AudioSessions.Remove(session);
        session.Dispose();
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

    private void DisposeAllDevices(IEnumerable<MMDevice>? devices)
    {
        if (devices == null) return;
        foreach (var mmDevice in devices)
        {
            try
            {
                mmDevice.Dispose();
            }
            catch
            {
                /* Ignore exceptions during dispose */
            }
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

            DisposeAllDevices(_allAudioDevices.Get());
            _allAudioDevices.Set(ImmutableList<MMDevice>.Empty);
            DisposeAll(_deviceEnumerator, _cacheLock);
        }
        catch
        {
            /* Ignore exceptions during dispose */
        }
    }
}
