using System.Windows;
using GHubBatteryTray.Devices;
using GHubBatteryTray.GHub;
using GHubBatteryTray.Infrastructure;
using GHubBatteryTray.Settings;
using GHubBatteryTray.Startup;
using GHubBatteryTray.Tray;
using GHubBatteryTray.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GHubBatteryTray;

public partial class App : System.Windows.Application, IDisposable
{
    private IHost? _host;
    private SingleInstanceCoordinator? _singleInstance;
    private TrayIconService? _trayIcons;
    private MainWindow? _mainWindow;
    private AppLog? _log;
    private bool _isExiting;

    public bool IsExiting => _isExiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _singleInstance = new SingleInstanceCoordinator();
            if (!_singleInstance.IsPrimary)
            {
                _singleInstance.SignalPrimary();
                Shutdown();
                return;
            }

            var builder = Host.CreateApplicationBuilder();
            builder.Services.AddSingleton<AppPaths>();
            builder.Services.AddSingleton<AppLog>();
            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddSingleton<DeviceStateStore>();
            builder.Services.AddSingleton<StartupRegistrationService>();
            builder.Services.AddSingleton<TrayIconRenderer>();
            builder.Services.AddSingleton<GHubMonitorService>();
            builder.Services.AddSingleton<IHostedService>(services =>
                services.GetRequiredService<GHubMonitorService>());
            builder.Services.AddSingleton<TrayIconService>();
            builder.Services.AddSingleton<SettingsViewModel>();
            builder.Services.AddSingleton<MainWindow>();
            _host = builder.Build();
            _log = _host.Services.GetRequiredService<AppLog>();
            _log.Info("Application services were created.");

            var settings = _host.Services.GetRequiredService<SettingsService>();
            await settings.InitializeAsync();
            var store = _host.Services.GetRequiredService<DeviceStateStore>();
            store.Seed(settings.Snapshot.LastKnownDevices.Values);

            _mainWindow = _host.Services.GetRequiredService<MainWindow>();
            _trayIcons = _host.Services.GetRequiredService<TrayIconService>();
            _trayIcons.Start(ShowSettings, RequestExit);

            _singleInstance.ActivationRequested += HandleActivationRequested;
            _singleInstance.StartListening();
            await _host.StartAsync();
            _log.Info("Background services started.");

            var backgroundLaunch = e.Args.Any(argument =>
                string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase));
            if (!backgroundLaunch || settings.Snapshot.SelectedDeviceKeys.Count == 0)
            {
                _log.Info("Opening the settings window.");
                ShowSettings();
            }
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                $"G HUB Battery Tray could not start.{Environment.NewLine}{Environment.NewLine}{exception.Message}",
                "G HUB Battery Tray",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            _isExiting = true;
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Dispose();
        base.OnExit(e);
    }

    public void Dispose()
    {
        _trayIcons?.Dispose();
        _singleInstance?.Dispose();
        _host?.Dispose();
        _trayIcons = null;
        _singleInstance = null;
        _host = null;
        GC.SuppressFinalize(this);
    }

    private void HandleActivationRequested(object? sender, EventArgs eventArgs) =>
        _ = Dispatcher.BeginInvoke(ShowSettings);

    private void ShowSettings()
    {
        _mainWindow?.ShowAndActivate();
    }

    private void RequestExit()
    {
        if (_isExiting)
        {
            return;
        }

        _isExiting = true;
        _trayIcons?.Dispose();
        _ = StopAndExitAsync();
    }

    private async Task StopAndExitAsync()
    {
        try
        {
            if (_host is not null)
            {
                await _host.StopAsync(TimeSpan.FromSeconds(3));
            }
        }
        finally
        {
            Shutdown();
        }
    }
}
