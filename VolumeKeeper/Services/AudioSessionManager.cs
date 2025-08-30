using System;
using System.Collections.Generic;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VolumeKeeper.Services;

public class AudioSession
{
    public int ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string ExecutableName { get; set; } = string.Empty;
    public float Volume { get; set; }
    public bool IsMuted { get; set; }
    public string IconPath { get; set; } = string.Empty;
    public AudioSessionControl SessionControl { get; set; } = null!;
}

public class AudioSessionManager : IDisposable
{
    private readonly MMDeviceEnumerator _deviceEnumerator;
    private MMDevice? _defaultDevice;
    private SessionCollection? _sessions;

    public AudioSessionManager()
    {
        _deviceEnumerator = new MMDeviceEnumerator();
        RefreshDevice();
    }

    private void RefreshDevice()
    {
        try
        {
            _defaultDevice = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _sessions = _defaultDevice?.AudioSessionManager?.Sessions;
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to get default audio device", ex, "AudioSessionManager");
        }
    }

    public List<AudioSession> GetAllSessions()
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
                if (session != null && !string.IsNullOrEmpty(session.ExecutableName))
                {
                    sessions.Add(session);
                }
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to enumerate audio sessions", ex, "AudioSessionManager");
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
            var executableName = Path.GetFileName(process.MainModule?.FileName ?? process.ProcessName);

            return new AudioSession
            {
                ProcessId = processId,
                ProcessName = process.ProcessName,
                ExecutableName = executableName,
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

    public bool SetSessionVolume(string executableName, float volumePercentage)
    {
        var sessions = GetAllSessions();
        var matchingSessions = sessions.Where(s =>
            string.Equals(s.ExecutableName, executableName, StringComparison.OrdinalIgnoreCase));

        bool anySet = false;
        foreach (var session in matchingSessions)
        {
            try
            {
                session.SessionControl.SimpleAudioVolume.Volume = volumePercentage / 100f;
                anySet = true;
                App.Logger.LogInfo($"Set volume for {executableName} (PID: {session.ProcessId}) to {volumePercentage}%", "AudioSessionManager");
            }
            catch (Exception ex)
            {
                App.Logger.LogError($"Failed to set volume for {executableName} (PID: {session.ProcessId})", ex, "AudioSessionManager");
            }
        }

        return anySet;
    }

    public float? GetSessionVolume(string executableName)
    {
        var sessions = GetAllSessions();
        var session = sessions.FirstOrDefault(s =>
            string.Equals(s.ExecutableName, executableName, StringComparison.OrdinalIgnoreCase));

        return session?.Volume;
    }

    public void Dispose()
    {
        _deviceEnumerator?.Dispose();
    }
}
