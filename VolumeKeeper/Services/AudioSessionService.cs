using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Threading.Tasks;
using System.Drawing;
using System.IO;

namespace VolumeKeeper.Services;

public partial class AudioSessionService : IDisposable
{
    private readonly MMDeviceEnumerator? _deviceEnumerator;
    private SessionCollection? _sessionCollection;

    public AudioSessionService()
    {
        try
        {
            _deviceEnumerator = new MMDeviceEnumerator();
            App.Logger.LogInfo("AudioSessionService initialized", "AudioSessionService");
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to initialize MMDeviceEnumerator", ex, "AudioSessionService");
            throw;
        }
    }

    public List<AudioSessionInfo> GetActiveAudioSessions()
    {
        var sessions = new List<AudioSessionInfo>();

        try
        {
            using var device = _deviceEnumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (device?.AudioSessionManager?.Sessions == null)
                return sessions;

            _sessionCollection = device.AudioSessionManager.Sessions;

            for (int i = 0; i < _sessionCollection.Count; i++)
            {
                try
                {
                    var session = _sessionCollection[i];
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
                        catch (Exception ex)
                        {
                            App.Logger.LogWarning($"Failed to get process name for PID {processId}: {ex.Message}", "AudioSessionService");
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
                        Session = session,
                        IconPath = GetApplicationIconPath(processId)
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.LogWarning($"Failed to process audio session: {ex.Message}", "AudioSessionService");
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
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to get active audio sessions", ex, "AudioSessionService");
            return sessions;
        }
    }

    public void SetApplicationVolume(string processName, double volumePercent)
    {
        try
        {
            using var device = _deviceEnumerator?.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            if (device?.AudioSessionManager?.Sessions == null)
                return;

            var sessions = device.AudioSessionManager.Sessions;

            for (int i = 0; i < sessions.Count; i++)
            {
                try
                {
                    var session = sessions[i];
                    var processId = session.GetProcessID;
                    if (processId <= 0) continue;

                    using var process = Process.GetProcessById((int)processId);
                    if (!string.Equals(process.ProcessName, processName, StringComparison.OrdinalIgnoreCase)) continue;

                    var newVolume = (float)(volumePercent / 100.0);
                    session.SimpleAudioVolume.Volume = newVolume;
                    App.Logger.LogInfo($"Set {processName} volume to {volumePercent}%", "AudioSessionService");
                }
                catch (Exception ex)
                {
                    App.Logger.LogWarning($"Failed to set volume for process: {ex.Message}", "AudioSessionService");
                    continue;
                }
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError($"Failed to set application volume for {processName}", ex, "AudioSessionService");
        }
    }

    public async Task<bool> SetApplicationVolumeAsync(string processName, double volumePercent)
    {
        return await Task.Run(() =>
        {
            try
            {
                SetApplicationVolume(processName, volumePercent);
                return true;
            }
            catch
            {
                return false;
            }
        });
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

    private string? GetApplicationIconPath(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.MainModule?.FileName;
        }
        catch (Exception ex)
        {
            App.Logger.LogWarning($"Failed to get executable path for PID {processId}: {ex.Message}", "AudioSessionService");
            return null;
        }
    }

    public void Dispose()
    {
        App.Logger.LogInfo("AudioSessionService disposing", "AudioSessionService");
        _deviceEnumerator?.Dispose();
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
    public string? IconPath { get; set; }
}
