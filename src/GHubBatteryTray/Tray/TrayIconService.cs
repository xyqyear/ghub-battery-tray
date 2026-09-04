using System.Drawing;
using System.Globalization;
using System.Windows;
using GHubBatteryTray.Devices;
using GHubBatteryTray.GHub;
using GHubBatteryTray.Infrastructure;
using GHubBatteryTray.Settings;
using Forms = System.Windows.Forms;

namespace GHubBatteryTray.Tray;

public sealed class TrayIconService : IDisposable
{
    private const string PlaceholderKey = "__application";

    private readonly DeviceStateStore _store;
    private readonly SettingsService _settings;
    private readonly GHubMonitorService _monitor;
    private readonly TrayIconRenderer _renderer;
    private readonly AppLog _log;
    private readonly Dictionary<string, TrayEntry> _entries = new(StringComparer.Ordinal);
    private Action? _showSettings;
    private Action? _exit;
    private bool _started;

    public TrayIconService(
        DeviceStateStore store,
        SettingsService settings,
        GHubMonitorService monitor,
        TrayIconRenderer renderer,
        AppLog log)
    {
        _store = store;
        _settings = settings;
        _monitor = monitor;
        _renderer = renderer;
        _log = log;
    }

    public void Start(Action showSettings, Action exit)
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _showSettings = showSettings;
        _exit = exit;
        _store.Changed += HandleStateChanged;
        _settings.Changed += HandleStateChanged;
        Render();
    }

    public void Dispose()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        _store.Changed -= HandleStateChanged;
        _settings.Changed -= HandleStateChanged;

        foreach (var entry in _entries.Values)
        {
            entry.Dispose();
        }

        _entries.Clear();
    }

    private void HandleStateChanged(object? sender, EventArgs eventArgs)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            Render();
        }
        else
        {
            _ = dispatcher.BeginInvoke(Render);
        }
    }

    private void Render()
    {
        if (!_started)
        {
            return;
        }

        try
        {
            var settings = _settings.Snapshot;
            var states = _store.Snapshot.ToDictionary(
                status => status.Device.Key,
                StringComparer.Ordinal);
            var desired = new Dictionary<string, DeviceStatus?>(StringComparer.Ordinal);

            foreach (var key in settings.SelectedDeviceKeys)
            {
                desired[key] = states.GetValueOrDefault(key) ?? CreateMissingStatus(key, settings);
            }

            if (desired.Count == 0)
            {
                desired[PlaceholderKey] = null;
            }

            foreach (var obsoleteKey in _entries.Keys.Except(desired.Keys).ToArray())
            {
                _entries[obsoleteKey].Dispose();
                _entries.Remove(obsoleteKey);
            }

            foreach (var item in desired)
            {
                if (!_entries.TryGetValue(item.Key, out var entry))
                {
                    entry = new TrayEntry(item.Key);
                    entry.NotifyIcon.MouseClick += (_, args) =>
                    {
                        if (args.Button == Forms.MouseButtons.Left)
                        {
                            _showSettings?.Invoke();
                        }
                    };
                    _entries[item.Key] = entry;
                }

                UpdateEntry(entry, item.Value);
            }
        }
        catch (Exception exception)
        {
            _log.Error("Tray icons could not be updated.", exception);
        }
    }

    private void UpdateEntry(TrayEntry entry, DeviceStatus? status)
    {
        var icon = _renderer.CreateIcon(status);
        var menu = CreateMenu(entry.Key, status);
        var tooltip = CreateTooltip(status);

        entry.Update(icon, menu, tooltip);
    }

    private Forms.ContextMenuStrip CreateMenu(string key, DeviceStatus? status)
    {
        var menu = new Forms.ContextMenuStrip();
        var nameItem = new Forms.ToolStripMenuItem(
            status?.Device.Name ?? "G HUB Battery Tray")
        {
            Enabled = false,
        };
        var batteryItem = new Forms.ToolStripMenuItem(CreateBatteryText(status))
        {
            Enabled = false,
        };
        var refreshItem = new Forms.ToolStripMenuItem("Refresh now");
        refreshItem.Click += (_, _) => _monitor.RequestRefresh();
        var settingsItem = new Forms.ToolStripMenuItem("Open settings");
        settingsItem.Click += (_, _) => _showSettings?.Invoke();

        menu.Items.Add(nameItem);
        menu.Items.Add(batteryItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(refreshItem);
        menu.Items.Add(settingsItem);

        if (!string.Equals(key, PlaceholderKey, StringComparison.Ordinal))
        {
            var hideItem = new Forms.ToolStripMenuItem("Hide this device");
            hideItem.Click += (_, _) => _ = HideDeviceSafelyAsync(key);
            menu.Items.Add(hideItem);
        }

        menu.Items.Add(new Forms.ToolStripSeparator());
        var exitItem = new Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => _exit?.Invoke();
        menu.Items.Add(exitItem);
        return menu;
    }

    private static string CreateTooltip(DeviceStatus? status)
    {
        var tooltip = status is null
            ? "G HUB Battery Tray — no device selected"
            : $"{status.Device.Name} — {CreateBatteryText(status)}";

        return tooltip.Length <= 120 ? tooltip : tooltip[..120];
    }

    private static string CreateBatteryText(DeviceStatus? status)
    {
        if (status?.Battery is null)
        {
            return "Battery unavailable";
        }

        var battery = status.Battery;
        var charging = battery.Charging ? " · charging" : string.Empty;
        var freshness = battery.IsLive
            ? string.Empty
            : $" · last seen {battery.UpdatedAt.LocalDateTime:g}";
        var mileage = battery.MileageHours is > 0
            ? string.Create(
                CultureInfo.CurrentCulture,
                $" · {battery.MileageHours.Value:0.#} h left")
            : string.Empty;

        return $"{battery.Percentage}%{charging}{mileage}{freshness}";
    }

    private static DeviceStatus CreateMissingStatus(
        string key,
        SettingsSnapshot settings)
    {
        if (settings.LastKnownDevices.TryGetValue(key, out var cached))
        {
            return new DeviceStatus(
                new GHubDevice(
                    key,
                    string.Empty,
                    null,
                    null,
                    cached.Name,
                    cached.DeviceType,
                    "CACHED",
                    true),
                new BatterySnapshot(
                    cached.Percentage,
                    cached.Charging,
                    cached.FullyCharged,
                    cached.MileageHours,
                    cached.UpdatedAt,
                    false),
                false);
        }

        return new DeviceStatus(
            new GHubDevice(
                key,
                string.Empty,
                null,
                null,
                "Unavailable device",
                "UNKNOWN",
                "UNKNOWN",
                true),
            null,
            false);
    }

    private async Task HideDeviceSafelyAsync(string key)
    {
        try
        {
            await _settings.SetDeviceSelectedAsync(key, false);
        }
        catch (Exception exception)
        {
            _log.Error("The device selection could not be saved.", exception);
        }
    }

    private sealed class TrayEntry : IDisposable
    {
        private Icon? _icon;
        private Forms.ContextMenuStrip? _menu;

        public TrayEntry(string key)
        {
            Key = key;
            NotifyIcon = new Forms.NotifyIcon
            {
                Visible = false,
            };
        }

        public string Key { get; }

        public Forms.NotifyIcon NotifyIcon { get; }

        public void Update(Icon icon, Forms.ContextMenuStrip menu, string tooltip)
        {
            NotifyIcon.Icon = icon;
            NotifyIcon.Text = tooltip;
            NotifyIcon.ContextMenuStrip = menu;
            if (!NotifyIcon.Visible)
            {
                NotifyIcon.Visible = true;
            }

            _icon?.Dispose();
            _menu?.Dispose();
            _icon = icon;
            _menu = menu;
        }

        public void Dispose()
        {
            NotifyIcon.Visible = false;
            NotifyIcon.Dispose();
            _menu?.Dispose();
            _icon?.Dispose();
        }
    }
}
