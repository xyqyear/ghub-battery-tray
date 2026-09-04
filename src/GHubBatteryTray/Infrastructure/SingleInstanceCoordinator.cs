namespace GHubBatteryTray.Infrastructure;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private const string InstanceName = @"Local\GHubBatteryTray.Instance";
    private const string ActivationName = @"Local\GHubBatteryTray.Activate";

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private readonly CancellationTokenSource _cancellation = new();
    private Task? _listener;
    private bool _disposed;

    public SingleInstanceCoordinator()
    {
        _activationEvent = new EventWaitHandle(
            false,
            EventResetMode.AutoReset,
            ActivationName);
        _mutex = new Mutex(true, InstanceName, out var createdNew);
        IsPrimary = createdNew;
    }

    public bool IsPrimary { get; }

    public event EventHandler? ActivationRequested;

    public void StartListening()
    {
        if (!IsPrimary || _listener is not null)
        {
            return;
        }

        _listener = Task.Run(() => Listen(_cancellation.Token));
    }

    public void SignalPrimary() => _activationEvent.Set();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancellation.Cancel();
        _activationEvent.Set();

        if (IsPrimary)
        {
            _mutex.ReleaseMutex();
        }

        _mutex.Dispose();
        _activationEvent.Dispose();
        _cancellation.Dispose();
    }

    private void Listen(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            _activationEvent.WaitOne();
            if (!cancellationToken.IsCancellationRequested)
            {
                ActivationRequested?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
