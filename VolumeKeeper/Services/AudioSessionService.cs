using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace VolumeKeeper.Services;

public class AudioSessionService : IDisposable
{
    private MMDeviceEnumerator? deviceEnumerator;
    private SessionCollection? sessionCollection;

    public AudioSessionService()
    {
        deviceEnumerator = new MMDeviceEnumerator();
    }

    public List<AudioSessionInfo> GetActiveAudioSessions()
    {
        var sessions = new List<AudioSessionInfo>();

        try
        {
            using var device = deviceEnumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (device?.AudioSessionManager?.Sessions == null)
                return sessions;

            sessionCollection = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessionCollection.Count; i++)
            {
                try
                {
                    var session = sessionCollection[i];
                    if (session.State == AudioSessionState.AudioSessionStateExpired)
                        continue;

                    var processId = session.GetProcessID;
                    string? applicationName = null;
                    
                    if (processId > 0)
                    {
                        try
                        {
                            using var process = Process.GetProcessById((int)processId);
                            applicationName = process.ProcessName;
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    if (string.IsNullOrEmpty(applicationName))
                        continue;

                    var volume = Math.Round(session.SimpleAudioVolume.Volume * 100);
                    var isMuted = session.SimpleAudioVolume.Mute;
                    var isActive = session.State == AudioSessionState.AudioSessionStateActive;

                    sessions.Add(new AudioSessionInfo
                    {
                        ApplicationName = FormatApplicationName(applicationName),
                        ProcessName = applicationName,
                        Volume = volume,
                        IsMuted = isMuted,
                        IsActive = isActive,
                        Session = session
                    });
                }
                catch
                {
                    continue;
                }
            }

            var uniqueSessions = sessions
                .GroupBy(s => s.ProcessName)
                .Select(g => g.First())
                .OrderBy(s => s.ApplicationName)
                .ToList();

            return uniqueSessions;
        }
        catch (Exception)
        {
            return sessions;
        }
    }

    public void SetApplicationVolume(string processName, double volumePercent)
    {
        try
        {
            using var device = deviceEnumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (device?.AudioSessionManager?.Sessions == null)
                return;

            var sessions = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];
                    var processId = session.GetProcessID;

                    if (processId > 0)
                    {
                        using var process = Process.GetProcessById((int)processId);
                        if (string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase))
                        {
                            session.SimpleAudioVolume.Volume = (float)(volumePercent / 100.0);
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
        }
        catch
        {
            // Handle silently
        }
    }

    private static string FormatApplicationName(string processName)
    {
        var knownApps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["spotify"] = "Spotify",
            ["discord"] = "Discord",
            ["msedge"] = "Microsoft Edge",
            ["chrome"] = "Google Chrome",
            ["firefox"] = "Mozilla Firefox",
            ["steam"] = "Steam",
            ["code"] = "Visual Studio Code",
            ["devenv"] = "Visual Studio",
            ["slack"] = "Slack",
            ["teams"] = "Microsoft Teams",
            ["zoom"] = "Zoom",
            ["vlc"] = "VLC Media Player",
            ["winamp"] = "Winamp",
            ["foobar2000"] = "foobar2000",
            ["itunes"] = "iTunes",
            ["wmplayer"] = "Windows Media Player",
            ["mpc-hc"] = "Media Player Classic",
            ["mpc-hc64"] = "Media Player Classic",
            ["potplayer"] = "PotPlayer",
            ["potplayer64"] = "PotPlayer"
        };

        return knownApps.TryGetValue(processName, out var displayName) 
            ? displayName 
            : processName.Replace("_", " ").Replace("-", " ");
    }

    public void Dispose()
    {
        deviceEnumerator?.Dispose();
    }
}

public class AudioSessionInfo
{
    public string ApplicationName { get; set; } = string.Empty;
    public string ProcessName { get; set; } = string.Empty;
    public double Volume { get; set; }
    public bool IsMuted { get; set; }
    public bool IsActive { get; set; }
    public AudioSessionControl? Session { get; set; }
}