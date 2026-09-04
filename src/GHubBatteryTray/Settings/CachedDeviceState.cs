namespace GHubBatteryTray.Settings;

public sealed record CachedDeviceState
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required string DeviceType { get; init; }

    public int Percentage { get; init; }

    public bool Charging { get; init; }

    public bool FullyCharged { get; init; }

    public double? MileageHours { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }
}
