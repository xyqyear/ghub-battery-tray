namespace GHubBatteryTray.Devices;

public sealed record DeviceStatus(
    GHubDevice Device,
    BatterySnapshot? Battery,
    bool IsPresent);
