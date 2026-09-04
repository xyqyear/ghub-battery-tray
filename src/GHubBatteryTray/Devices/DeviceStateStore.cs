using GHubBatteryTray.Settings;

namespace GHubBatteryTray.Devices;

public sealed class DeviceStateStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, DeviceStatus> _devices = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _deviceKeysById = new(StringComparer.Ordinal);
    private MonitorConnection _connection = new(
        MonitorConnectionState.Connecting,
        "Connecting to G HUB");

    public event EventHandler? Changed;

    public MonitorConnection Connection
    {
        get
        {
            lock (_sync)
            {
                return _connection;
            }
        }
    }

    public IReadOnlyList<DeviceStatus> Snapshot
    {
        get
        {
            lock (_sync)
            {
                return _devices.Values
                    .OrderBy(status => status.Device.Name, StringComparer.CurrentCultureIgnoreCase)
                    .ToArray();
            }
        }
    }

    public void Seed(IEnumerable<CachedDeviceState> cachedDevices)
    {
        lock (_sync)
        {
            foreach (var cached in cachedDevices)
            {
                var device = new GHubDevice(
                    cached.Key,
                    string.Empty,
                    null,
                    null,
                    cached.Name,
                    cached.DeviceType,
                    "CACHED",
                    true);
                var battery = new BatterySnapshot(
                    cached.Percentage,
                    cached.Charging,
                    cached.FullyCharged,
                    cached.MileageHours,
                    cached.UpdatedAt,
                    false);
                _devices[cached.Key] = new DeviceStatus(device, battery, false);
            }
        }

        RaiseChanged();
    }

    public void SetConnection(MonitorConnectionState state, string message)
    {
        lock (_sync)
        {
            _connection = new MonitorConnection(state, message);
        }

        RaiseChanged();
    }

    public void ReplaceDevices(IReadOnlyCollection<GHubDevice> devices)
    {
        lock (_sync)
        {
            _deviceKeysById.Clear();
            var presentKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var device in devices)
            {
                presentKeys.Add(device.Key);
                _deviceKeysById[device.DeviceId] = device.Key;
                _devices.TryGetValue(device.Key, out var existing);
                _devices[device.Key] = new DeviceStatus(device, existing?.Battery, true);
            }

            foreach (var key in _devices.Keys.ToArray())
            {
                if (presentKeys.Contains(key))
                {
                    continue;
                }

                var existing = _devices[key];
                _devices[key] = existing with
                {
                    Battery = existing.Battery is null
                        ? null
                        : existing.Battery with { IsLive = false },
                    IsPresent = false,
                };
            }
        }

        RaiseChanged();
    }

    public DeviceStatus? UpdateBattery(
        string deviceId,
        int percentage,
        bool charging,
        bool fullyCharged,
        double? mileageHours,
        DateTimeOffset updatedAt)
    {
        DeviceStatus? updated = null;

        lock (_sync)
        {
            if (!_deviceKeysById.TryGetValue(deviceId, out var deviceKey) ||
                !_devices.TryGetValue(deviceKey, out var existing))
            {
                return null;
            }

            var battery = new BatterySnapshot(
                Math.Clamp(percentage, 0, 100),
                charging,
                fullyCharged,
                mileageHours,
                updatedAt,
                true);
            updated = existing with { Battery = battery, IsPresent = true };
            _devices[deviceKey] = updated;
        }

        RaiseChanged();
        return updated;
    }

    public void MarkBatteryStale(string deviceId)
    {
        lock (_sync)
        {
            if (!_deviceKeysById.TryGetValue(deviceId, out var deviceKey) ||
                !_devices.TryGetValue(deviceKey, out var existing) ||
                existing.Battery is null)
            {
                return;
            }

            _devices[deviceKey] = existing with
            {
                Battery = existing.Battery with { IsLive = false },
            };
        }

        RaiseChanged();
    }

    public void MarkDisconnected()
    {
        lock (_sync)
        {
            foreach (var key in _devices.Keys.ToArray())
            {
                var existing = _devices[key];
                _devices[key] = existing with
                {
                    Battery = existing.Battery is null
                        ? null
                        : existing.Battery with { IsLive = false },
                    IsPresent = false,
                };
            }
        }

        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
