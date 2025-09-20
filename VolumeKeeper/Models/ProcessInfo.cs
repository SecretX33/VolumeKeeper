namespace VolumeKeeper.Models;

public record ProcessInfo(
    int Id,
    string DisplayName,
    string ExecutableName,
    string? ExecutablePath
)
{
    public VolumeApplicationId AppId => VolumeApplicationId.Create(ExecutablePath, ExecutableName);
}
