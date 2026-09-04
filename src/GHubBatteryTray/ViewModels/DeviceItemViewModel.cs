using CommunityToolkit.Mvvm.ComponentModel;
using GHubBatteryTray.Devices;

namespace GHubBatteryTray.ViewModels;

public sealed partial class DeviceItemViewModel : ObservableObject
{
    private readonly Func<string, bool, Task> _selectionChanged;
    private bool _synchronizing;

    public DeviceItemViewModel(
        DeviceStatus status,
        bool isSelected,
        Func<string, bool, Task> selectionChanged)
    {
        Key = status.Device.Key;
        _selectionChanged = selectionChanged;
        _synchronizing = true;
        IsSelected = isSelected;
        _synchronizing = false;
        Update(status, isSelected);
    }

    public string Key { get; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DetailText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BatteryText { get; set; } = "—";

    [ObservableProperty]
    public partial double BatteryPercentage { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public void Update(DeviceStatus status, bool isSelected)
    {
        Name = status.Device.Name;
        _synchronizing = true;
        IsSelected = isSelected;
        _synchronizing = false;

        if (status.Battery is null)
        {
            BatteryText = "—";
            BatteryPercentage = 0;
            DetailText = status.IsPresent
                ? "Battery unavailable"
                : "Waiting for device";
            return;
        }

        BatteryText = $"{status.Battery.Percentage}%";
        BatteryPercentage = status.Battery.Percentage;

        if (!status.Battery.IsLive)
        {
            DetailText = $"Last seen {status.Battery.UpdatedAt.LocalDateTime:g}";
            return;
        }

        var charging = status.Battery.Charging ? "Charging" : "Live";
        DetailText = status.Battery.MileageHours is > 0
            ? $"{charging} · {status.Battery.MileageHours.Value:0.#} h remaining"
            : charging;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_synchronizing)
        {
            _ = _selectionChanged(Key, value);
        }
    }
}
