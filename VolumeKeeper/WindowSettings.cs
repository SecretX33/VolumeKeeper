using System;
using System.IO;
using System.Text.Json;

namespace VolumeKeeper;

public class WindowSettings
{
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 800;
    public double X { get; set; } = double.NaN;
    public double Y { get; set; } = double.NaN;
    public bool IsMaximized { get; set; } = false;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VolumeKeeper",
        "window-settings.json"
    );

    public static WindowSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<WindowSettings>(json) ?? new WindowSettings();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load window settings: {ex.Message}");
        }

        return new WindowSettings();
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save window settings: {ex.Message}");
        }
    }
}
