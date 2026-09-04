namespace GHubBatteryTray.Settings;

public sealed record AppSettings
{
    public int SchemaVersion { get; init; } = 1;

    public HashSet<string> SelectedDeviceKeys { get; init; } = new(StringComparer.Ordinal);

    public bool StartWithWindows { get; init; }

    public Dictionary<string, CachedDeviceState> LastKnownDevices { get; init; } =
        new(StringComparer.Ordinal);
}

public sealed record SettingsSnapshot(
    IReadOnlySet<string> SelectedDeviceKeys,
    bool StartWithWindows,
    IReadOnlyDictionary<string, CachedDeviceState> LastKnownDevices);
