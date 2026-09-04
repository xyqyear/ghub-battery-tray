using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using GHubBatteryTray.Tray;
using GHubBatteryTray.ViewModels;

namespace GHubBatteryTray;

public partial class MainWindow : Window
{
    public MainWindow(SettingsViewModel viewModel, TrayIconRenderer iconRenderer)
    {
        InitializeComponent();
        DataContext = viewModel;

        using var icon = iconRenderer.CreateIcon(null);
        var image = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(32, 32));
        image.Freeze();
        Icon = image;
    }

    public void ShowAndActivate()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = true;
        Topmost = false;
        Focus();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (System.Windows.Application.Current is App { IsExiting: false })
        {
            e.Cancel = true;
            Hide();
        }

        base.OnClosing(e);
    }
}
