using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services;

public class IconService(
    DispatcherQueue mainThreadQueue
) {
    private readonly ConcurrentDictionary<string, BitmapImage> _iconCache = new();

    public async Task<BitmapImage?> GetApplicationIconAsync(
        string iconPath,
        string executablePath,
        string executableName
    ) {
        var resolvedIconPath = string.IsNullOrWhiteSpace(iconPath) ? executablePath : iconPath;

        try
        {
            // Check cache first
            if (_iconCache.TryGetValue(resolvedIconPath, out var cachedIcon))
                return cachedIcon;

            // Extract icon from executable
            var bitmapImage = await ExtractIconAsync(resolvedIconPath, executablePath);
            if (bitmapImage != null)
            {
                _iconCache[resolvedIconPath] = bitmapImage;
            }

            return bitmapImage;
        }
        catch (Exception ex)
        {
            App.Logger.LogWarning($"Failed to get icon for {executableName}", ex, "IconService");
            return null;
        }
    }

    private Task<BitmapImage?> ExtractIconAsync(
        string iconPath,
        string executablePath
    ) {
        var isIconPathAnExecutable = string.Equals(iconPath, executablePath, StringComparison.OrdinalIgnoreCase);

        return Task.Run(async () =>
        {
            BitmapImage? bitmapImage = null;
            try
            {
                // Extract the icon from the executable
                if (isIconPathAnExecutable)
                {
                    using var icon = Icon.ExtractAssociatedIcon(iconPath);
                    if (icon != null)
                    {
                        bitmapImage = await ConvertIconToBitmapImageAsync(icon);
                    }
                }
                else
                {
                    bitmapImage = await LoadBitmapFromFileAsync(iconPath);
                }
            }
            catch (Exception ex)
            {
                App.Logger.LogWarning($"Failed to extract icon from {iconPath}", ex, "IconService");
            }

            return bitmapImage;
        });
    }

    private async Task<BitmapImage> LoadBitmapFromFileAsync(string filePath)
    {
        var bitmapImage = new BitmapImage();

        await using var stream = File.OpenRead(filePath);
        var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        await bitmapImage.SetSourceAsync(memoryStream.AsRandomAccessStream());
        return bitmapImage;
    }

    private async Task<BitmapImage> ConvertIconToBitmapImageAsync(Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        using var memoryStream = new MemoryStream();

        bitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        var bitmapImageTask = await mainThreadQueue.TryFetch(async () =>
        {
            var bitmapImage = new BitmapImage();
            // ReSharper disable once AccessToDisposedClosure
            await bitmapImage.SetSourceAsync(memoryStream.AsRandomAccessStream());
            return bitmapImage;
        }).ConfigureAwait(false);

        return await bitmapImageTask;
    }
}
