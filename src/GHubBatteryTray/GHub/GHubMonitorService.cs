using System.IO;
using System.Threading.Channels;
using GHubBatteryTray.Devices;
using GHubBatteryTray.Infrastructure;
using GHubBatteryTray.Settings;
using Microsoft.Extensions.Hosting;

namespace GHubBatteryTray.GHub;

public sealed class GHubMonitorService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
    ];

    private readonly DeviceStateStore _store;
    private readonly SettingsService _settings;
    private readonly AppLog _log;
    private readonly Channel<bool> _refreshRequests = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

    public GHubMonitorService(
        DeviceStateStore store,
        SettingsService settings,
        AppLog log)
    {
        _store = store;
        _settings = settings;
        _log = log;
    }

    public void RequestRefresh() => _refreshRequests.Writer.TryWrite(true);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var retryIndex = 0;

        while (!stoppingToken.IsCancellationRequested)
        {
            await using var client = new GHubClient(_log);
            client.BatteryChanged += HandleBatteryChanged;
            client.DeviceStateChanged += HandleDeviceStateChanged;

            try
            {
                _store.SetConnection(
                    MonitorConnectionState.Connecting,
                    "Connecting to G HUB");
                await client.ConnectAsync(stoppingToken);
                _store.SetConnection(
                    MonitorConnectionState.Connected,
                    "Connected to G HUB");
                await client.SubscribeToDeviceStateChangesAsync(stoppingToken);
                await client.SubscribeToBatteryChangesAsync(stoppingToken);
                await RefreshAsync(client, stoppingToken);
                retryIndex = 0;
                await RunConnectedAsync(client, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _log.Error("The G HUB monitor disconnected.", exception);
                _store.MarkDisconnected();
                _store.SetConnection(
                    MonitorConnectionState.Disconnected,
                    "Waiting for G HUB");

                var delay = RetryDelays[Math.Min(retryIndex, RetryDelays.Length - 1)];
                retryIndex++;
                await WaitForRefreshAsync(delay, stoppingToken);
            }
            finally
            {
                client.BatteryChanged -= HandleBatteryChanged;
                client.DeviceStateChanged -= HandleDeviceStateChanged;
            }
        }
    }

    private async Task RunConnectedAsync(GHubClient client, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var cycleCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            var wakeTask = WaitForRefreshAsync(PollInterval, cycleCancellation.Token);
            var completedTask = await Task.WhenAny(wakeTask, client.Completion);

            if (completedTask == client.Completion)
            {
                await cycleCancellation.CancelAsync();
                try
                {
                    await wakeTask;
                }
                catch (OperationCanceledException)
                {
                }

                await client.Completion;
                throw new IOException("The G HUB connection ended.");
            }

            await wakeTask;
            await RefreshAsync(client, cancellationToken);
        }
    }

    private async Task RefreshAsync(GHubClient client, CancellationToken cancellationToken)
    {
        var devices = await client.GetDevicesAsync(cancellationToken);
        _store.ReplaceDevices(devices);

        foreach (var device in devices)
        {
            try
            {
                var report = await client.GetBatteryAsync(device.DeviceId, cancellationToken);
                var status = ApplyReport(report);
                if (status is not null)
                {
                    await _settings.UpdateCachedDeviceAsync(status, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _store.MarkBatteryStale(device.DeviceId);
                _log.Warning($"Battery query failed for {device.Name}: {exception.Message}");
            }
        }
    }

    private void HandleBatteryChanged(GHubBatteryReport report)
    {
        var status = ApplyReport(report);
        if (status is not null)
        {
            _ = PersistSafelyAsync(status);
        }
    }

    private void HandleDeviceStateChanged() => RequestRefresh();

    private DeviceStatus? ApplyReport(GHubBatteryReport report) => _store.UpdateBattery(
        report.DeviceId,
        report.Percentage,
        report.Charging,
        report.FullyCharged,
        report.MileageHours,
        report.UpdatedAt);

    private async Task PersistSafelyAsync(DeviceStatus status)
    {
        try
        {
            await _settings.UpdateCachedDeviceAsync(status);
        }
        catch (Exception exception)
        {
            _log.Error("A battery update could not be cached.", exception);
        }
    }

    private async Task WaitForRefreshAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await _refreshRequests.Reader.ReadAsync(cancellationToken)
                .AsTask()
                .WaitAsync(delay, cancellationToken);
        }
        catch (TimeoutException)
        {
        }

        while (_refreshRequests.Reader.TryRead(out _))
        {
        }
    }
}
