using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using VolumeKeeper.Models;
using VolumeKeeper.Util;
using static VolumeKeeper.Util.Util;

namespace VolumeKeeper.Services.Managers;

public partial class AudioSessionManager : IDisposable
{
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private readonly AtomicReference<MMDevice?> _defaultDevice = new(null);
    private readonly TimeSpan _cacheTtl = TimeSpan.FromMilliseconds(500);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly AtomicReference<bool> _isDisposed = new(false);
    private SessionCollection? _sessions;
    private List<AudioSession>? _cachedSessions;
    private DateTime _cacheExpiry = DateTime.MinValue;

    public AudioSessionManager()
    {
        _deviceEnumerator = new MMDeviceEnumerator();
        RefreshDevice();
    }

    public async Task<List<AudioSession>> GetAllSessionsAsync()
    {
        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_cachedSessions != null && DateTime.UtcNow < _cacheExpiry)
            {
                return new List<AudioSession>(_cachedSessions);
            }

            var sessions = GetAllSessionsInternal();
            _cachedSessions = sessions;
            _cacheExpiry = DateTime.UtcNow.Add(_cacheTtl);
            return new List<AudioSession>(sessions);
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public async Task<List<AudioSession>> GetSessionsById(VolumeApplicationId volumeApplicationId)
    {
        var allSessions = await GetAllSessionsAsync();
        return allSessions.Where(session => session.AppId == volumeApplicationId).ToList();
    }

    public async Task<List<AudioSession>> GetSessionsByExecutableAsync(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
            return [];

        var sessions = await GetAllSessionsAsync();
        return sessions.Where(s =>
            string.Equals(s.ExecutableName, executableName, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public List<AudioSession> GetSessionsByExecutable(string executableName)
    {
        return GetSessionsByExecutableAsync(executableName).GetAwaiter().GetResult();
    }

    public async Task<AudioSession?> GetSessionByProcessIdAsync(int processId)
    {
        var sessions = await GetAllSessionsAsync();
        return sessions.FirstOrDefault(s => s.ProcessId == processId);
    }

    public void RefreshSessions()
    {
        _cacheLock.Wait();
        try
        {
            _cachedSessions = null;
            _cacheExpiry = DateTime.MinValue;
            RefreshDevice();
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void InvalidateCache()
    {
        _cacheLock.Wait();
        try
        {
            _cachedSessions = null;
            _cacheExpiry = DateTime.MinValue;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

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

    private List<AudioSession> GetAllSessionsInternal()
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

    private AudioSession? CreateAudioSession(AudioSessionControl sessionControl, SimpleAudioVolume simpleVolume)
    {
        try
        {
            var processId = (int)sessionControl.GetProcessID;
            if (processId == 0)
                return null;

            using var process = Process.GetProcessById(processId);
            var fullPath = process.MainModule?.FileName;
            var executableName = Path.GetFileName(fullPath ?? process.ProcessName);

            App.Logger.LogDebug($"Found audio session: PID={processId}, Name={executableName}, Path={fullPath}");

            if (!executableName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                executableName += ".exe";
            }

            return new AudioSession
            {
                ProcessId = processId,
                ProcessName = process.ProcessName,
                ExecutableName = executableName,
                ExecutablePath = fullPath,
                Volume = simpleVolume.Volume * 100,
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
