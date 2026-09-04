using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GHubBatteryTray.Devices;
using GHubBatteryTray.GHub;
using GHubBatteryTray.Infrastructure;
using GHubBatteryTray.Settings;
using GHubBatteryTray.Startup;

namespace GHubBatteryTray.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly DeviceStateStore _store;
    private readonly SettingsService _settings;
    private readonly StartupRegistrationService _startup;
    private readonly GHubMonitorService _monitor;
    private readonly AppLog _log;
    private readonly Dictionary<string, DeviceItemViewModel> _items = new(StringComparer.Ordinal);
    private bool _synchronizingStartup;
    private bool _disposed;

    public SettingsViewModel(
        DeviceStateStore store,
        SettingsService settings,
        StartupRegistrationService startup,
        GHubMonitorService monitor,
        AppLog log)
    {
        _store = store;
        _settings = settings;
        _startup = startup;
        _monitor = monitor;
        _log = log;
        _store.Changed += HandleChanged;
        _settings.Changed += HandleChanged;

        _synchronizingStartup = true;
        StartWithWindows = _startup.IsEnabled;
        _synchronizingStartup = false;
        Synchronize();
    }

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = [];

    [ObservableProperty]
    public partial string ConnectionText { get; set; } = "Connecting to G HUB";

    [ObservableProperty]
    public partial MonitorConnectionState ConnectionState { get; set; } =
        MonitorConnectionState.Connecting;

    [ObservableProperty]
    public partial bool IsEmpty { get; set; } = true;

    [ObservableProperty]
    public partial bool StartWithWindows { get; set; }

    [ObservableProperty]
    public partial string ErrorMessage { get; set; } = string.Empty;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _store.Changed -= HandleChanged;
        _settings.Changed -= HandleChanged;
    }

    [RelayCommand]
    private void Refresh()
    {
        ErrorMessage = string.Empty;
        _monitor.RequestRefresh();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_synchronizingStartup)
        {
            return;
        }

        try
        {
            _startup.SetEnabled(value);
            _ = SaveStartupSettingSafelyAsync(value);
            ErrorMessage = string.Empty;
        }
        catch (Exception exception)
        {
            _log.Error("Startup registration could not be changed.", exception);
            ErrorMessage = "Could not change startup setting";
            _synchronizingStartup = true;
            StartWithWindows = !value;
            _synchronizingStartup = false;
        }
    }

    private void HandleChanged(object? sender, EventArgs eventArgs)
    {
        var dispatcher = System.Windows.Application.Current.Dispatcher;
        if (dispatcher.CheckAccess())
        {
            Synchronize();
        }
        else
        {
            _ = dispatcher.BeginInvoke(Synchronize);
        }
    }

    private void Synchronize()
    {
        var connection = _store.Connection;
        ConnectionText = connection.Message;
        ConnectionState = connection.State;
        var settings = _settings.Snapshot;
        var states = _store.Snapshot;

        foreach (var status in states)
        {
            var selected = settings.SelectedDeviceKeys.Contains(status.Device.Key);
            if (_items.TryGetValue(status.Device.Key, out var existing))
            {
                existing.Update(status, selected);
            }
            else
            {
                var item = new DeviceItemViewModel(status, selected, ChangeSelectionAsync);
                _items[status.Device.Key] = item;
                Devices.Add(item);
            }
        }

        var stateKeys = states.Select(status => status.Device.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var obsoleteKey in _items.Keys.Except(stateKeys).ToArray())
        {
            Devices.Remove(_items[obsoleteKey]);
            _items.Remove(obsoleteKey);
        }

        IsEmpty = Devices.Count == 0;
    }

    private async Task ChangeSelectionAsync(string key, bool selected)
    {
        try
        {
            await _settings.SetDeviceSelectedAsync(key, selected);
            ErrorMessage = string.Empty;
        }
        catch (Exception exception)
        {
            _log.Error("The device selection could not be saved.", exception);
            ErrorMessage = "Could not save device selection";
            Synchronize();
        }
    }

    private async Task SaveStartupSettingSafelyAsync(bool value)
    {
        try
        {
            await _settings.SetStartWithWindowsAsync(value);
        }
        catch (Exception exception)
        {
            _log.Error("The startup preference could not be saved.", exception);
            ErrorMessage = "Could not save startup preference";
        }
    }
}
