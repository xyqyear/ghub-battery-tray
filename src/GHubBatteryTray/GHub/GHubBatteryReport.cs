namespace GHubBatteryTray.GHub;

public sealed record GHubBatteryReport(
    string DeviceId,
    int Percentage,
    bool Charging,
    bool FullyCharged,
    double? MileageHours,
    DateTimeOffset UpdatedAt);
