namespace GHubBatteryTray.Devices;

public sealed record GHubDevice(
    string Key,
    string DeviceId,
    string? UnitId,
    string? Signature,
    string Name,
    string DeviceType,
    string State,
    bool HasBatteryStatus);
