namespace GHubBatteryTray.Devices;

public enum MonitorConnectionState
{
    Connecting,
    Connected,
    Disconnected,
}

public sealed record MonitorConnection(
    MonitorConnectionState State,
    string Message);
