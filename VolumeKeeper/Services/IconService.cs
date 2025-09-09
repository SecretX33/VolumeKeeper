using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services;

public class IconService
{
    private readonly Dictionary<string, BitmapImage> _iconCache = new();
    private readonly string _iconCacheDirectory;

    public IconService()
    {
        _iconCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VolumeKeeper",
            "icons");

        Directory.CreateDirectory(_iconCacheDirectory);
    }

    public async Task<BitmapImage?> GetApplicationIconAsync(string? executablePath, string processName)
    {
        if (string.IsNullOrEmpty(executablePath))
            return null;

        try
        {
            // Check cache first
            if (_iconCache.TryGetValue(executablePath, out var cachedIcon))
                return cachedIcon;

            // Check file cache
            var cacheFileName = $"{processName.Replace(".", "_")}.png";
            var cacheFilePath = Path.Combine(_iconCacheDirectory, cacheFileName);

            BitmapImage? bitmapImage = null;

            if (File.Exists(cacheFilePath))
            {
                // Load from file cache
                bitmapImage = await LoadBitmapFromFileAsync(cacheFilePath);
            }
            else
            {
                // Extract icon from executable
                using var icon = await ExtractIconAsync(executablePath);
                if (icon != null)
                {
                    // Save to file cache
                    await SaveIconToCacheAsync(icon, cacheFilePath);
                    bitmapImage = await ConvertIconToBitmapImageAsync(icon);
                }
            }

            if (bitmapImage != null)
            {
                _iconCache[executablePath] = bitmapImage;
            }

            return bitmapImage;
        }
        catch (Exception ex)
        {
            App.Logger.LogWarning($"Failed to get icon for {processName}: {ex.Message}", "IconService");
            return null;
        }
    }

    private async Task<Icon?> ExtractIconAsync(string filePath)
    {
        return await Task.Run(() =>
        {
            try
            {
                // Extract the icon from the executable
                var icon = NativeMethods.ExtractIconFromFile(filePath);
                return icon;
            }
            catch (Exception ex)
            {
                App.Logger.LogWarning($"Failed to extract icon from {filePath}: {ex.Message}", "IconService");
            }

            return null;
        });
    }

    private async Task SaveIconToCacheAsync(Icon icon, string filePath)
    {
        await Task.Run(() =>
        {
            try
            {
                using var bitmap = icon.ToBitmap();
                bitmap.Save(filePath, ImageFormat.Png);
            }
            catch (Exception ex)
            {
                App.Logger.LogWarning($"Failed to save icon to cache: {ex.Message}", "IconService");
            }
        });
    }

    private async Task<BitmapImage> LoadBitmapFromFileAsync(string filePath)
    {
        var bitmapImage = new BitmapImage();

        using var stream = File.OpenRead(filePath);
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        await bitmapImage.SetSourceAsync(memoryStream.AsRandomAccessStream());
        return bitmapImage;
    }

    private async Task<BitmapImage> ConvertIconToBitmapImageAsync(Icon icon)
    {
        return await Task.Run(async () =>
        {
            using var bitmap = icon.ToBitmap();
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Png);
            memoryStream.Position = 0;

            var bitmapImage = new BitmapImage();
            await bitmapImage.SetSourceAsync(memoryStream.AsRandomAccessStream());
            return bitmapImage;
        });
    }

    public void ClearCache()
    {
        _iconCache.Clear();

        try
        {
            if (Directory.Exists(_iconCacheDirectory))
            {
                foreach (var file in Directory.GetFiles(_iconCacheDirectory, "*.png"))
                {
                    File.Delete(file);
                }
            }
        }
        catch (Exception ex)
        {
            App.Logger.LogError("Failed to clear icon cache", ex, "IconService");
        }
    }
}
