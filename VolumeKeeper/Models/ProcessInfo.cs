namespace VolumeKeeper.Models;

public sealed record ProcessInfo(
    int Id,
    string DisplayName,
    string ExecutableName,
    string ExecutablePath
)
{
    public VolumeApplicationId AppId => new(ExecutablePath);
}
