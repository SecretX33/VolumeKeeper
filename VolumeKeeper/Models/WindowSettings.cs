namespace VolumeKeeper.Models;

public record WindowSettings(
    double Width = 1200,
    double Height = 800,
    int? X = null,
    int? Y = null,
    bool IsMaximized = false
);
