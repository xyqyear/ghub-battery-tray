namespace GHubBatteryTray.Devices;

public sealed record BatterySnapshot(
    int Percentage,
    bool Charging,
    bool FullyCharged,
    double? MileageHours,
    DateTimeOffset UpdatedAt,
    bool IsLive);
