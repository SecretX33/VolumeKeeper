using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using VolumeKeeper.Services.Log;
using VolumeKeeper.Util;

namespace VolumeKeeper.Services;

public sealed class IconService(
    DispatcherQueue mainThreadQueue
) {
    private readonly Logger _logger = App.Logger.Named();
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
            _logger.Warn($"Failed to get icon for {executableName}", ex);
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
                        bitmapImage = await ConvertToBitmapImageAsync(icon);
                    }
                }
                else
                {
                    bitmapImage = await LoadBitmapFromFileAsync(iconPath);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to extract icon from {iconPath}", ex);
            }

            return bitmapImage;
        });
    }

    private async Task<BitmapImage> LoadBitmapFromFileAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        using var memoryStream = new MemoryStream();

        await stream.CopyToAsync(memoryStream);
        memoryStream.Position = 0;

        return await ConvertIntoBitmapImageAsync(memoryStream).ConfigureAwait(false);
    }

    private async Task<BitmapImage> ConvertToBitmapImageAsync(Icon icon)
    {
        using var bitmap = icon.ToBitmap();
        using var memoryStream = new MemoryStream();

        bitmap.Save(memoryStream, ImageFormat.Png);
        memoryStream.Position = 0;

        return await ConvertIntoBitmapImageAsync(memoryStream).ConfigureAwait(false);
    }

    private Task<BitmapImage> ConvertIntoBitmapImageAsync(Stream stream) =>
        mainThreadQueue.TryFetchImmediate(async () =>
        {
            var bitmapImage = new BitmapImage();
            // ReSharper disable once AccessToDisposedClosure
            await bitmapImage.SetSourceAsync(stream.AsRandomAccessStream());
            return bitmapImage;
        });
}
