using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using GHubBatteryTray.Devices;
using GHubBatteryTray.Infrastructure;

namespace GHubBatteryTray.GHub;

public sealed class GHubClient : IAsyncDisposable
{
    private static readonly Uri Endpoint = new("ws://127.0.0.1:9010");
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(5);

    private readonly AppLog _log;
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending =
        new(StringComparer.Ordinal);
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _connectionCancellation;
    private Task? _receiveTask;

    public GHubClient(AppLog log)
    {
        _log = log;
    }

    public event Action<GHubBatteryReport>? BatteryChanged;

    public event Action? DeviceStateChanged;

    public Task Completion => _receiveTask ?? Task.CompletedTask;

    public async Task ConnectAsync(CancellationToken cancellationToken)
    {
        if (_socket is not null)
        {
            throw new InvalidOperationException("The G HUB client is already connected.");
        }

        _socket = new ClientWebSocket();
        _socket.Options.AddSubProtocol("json");
        _socket.Options.SetRequestHeader("Origin", "file://");
        _socket.Options.SetRequestHeader("Pragma", "no-cache");
        _socket.Options.SetRequestHeader("Cache-Control", "no-cache");
        await _socket.ConnectAsync(Endpoint, cancellationToken);

        _connectionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _receiveTask = ReceiveLoopAsync(_connectionCancellation.Token);
        _log.Info("Connected to the G HUB WebSocket.");
    }

    public async Task<IReadOnlyList<GHubDevice>> GetDevicesAsync(CancellationToken cancellationToken)
    {
        var response = await RequestAsync("GET", "/devices/list", cancellationToken);
        return GHubProtocolParser.ParseDevices(response)
            .Where(device => device.HasBatteryStatus)
            .ToArray();
    }

    public async Task<GHubBatteryReport> GetBatteryAsync(
        string deviceId,
        CancellationToken cancellationToken)
    {
        var response = await RequestAsync(
            "GET",
            $"/battery/{deviceId}/state",
            cancellationToken);
        return GHubProtocolParser.ParseBattery(response, deviceId);
    }

    public Task SubscribeToBatteryChangesAsync(CancellationToken cancellationToken) =>
        SendAsync(
            new GHubRequest(string.Empty, "SUBSCRIBE", "/battery/state/changed"),
            cancellationToken);

    public Task SubscribeToDeviceStateChangesAsync(CancellationToken cancellationToken) =>
        SendAsync(
            new GHubRequest(string.Empty, "SUBSCRIBE", "/devices/state/changed"),
            cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (_connectionCancellation is not null)
        {
            await _connectionCancellation.CancelAsync();
        }

        _socket?.Abort();
        _socket?.Dispose();

        if (_receiveTask is not null)
        {
            try
            {
                await _receiveTask;
            }
            catch (Exception exception) when (
                exception is OperationCanceledException or WebSocketException)
            {
            }
        }

        _connectionCancellation?.Dispose();
        _sendGate.Dispose();
    }

    private async Task<JsonElement> RequestAsync(
        string verb,
        string path,
        CancellationToken cancellationToken)
    {
        var messageId = Guid.NewGuid().ToString();
        var completion = new TaskCompletionSource<JsonElement>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pending.TryAdd(messageId, completion))
        {
            throw new InvalidOperationException("Could not register a G HUB request.");
        }

        try
        {
            await SendAsync(new GHubRequest(messageId, verb, path), cancellationToken);
            var response = await completion.Task.WaitAsync(RequestTimeout, cancellationToken);
            ValidateResponse(response, path);
            return response;
        }
        finally
        {
            _pending.TryRemove(messageId, out _);
        }
    }

    private async Task SendAsync(GHubRequest request, CancellationToken cancellationToken)
    {
        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
        {
            throw new WebSocketException("The G HUB WebSocket is not open.");
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(request);
        await _sendGate.WaitAsync(cancellationToken);
        try
        {
            await socket.SendAsync(
                bytes,
                WebSocketMessageType.Text,
                true,
                cancellationToken);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        Exception? failure = null;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var text = await ReceiveTextAsync(cancellationToken);
                using var document = JsonDocument.Parse(text);
                var message = document.RootElement;
                var path = message.TryGetProperty("path", out var pathElement)
                    ? pathElement.GetString()
                    : null;
                var messageId = message.TryGetProperty("msgId", out var idElement)
                    ? idElement.GetString()
                    : null;
                var verb = message.TryGetProperty("verb", out var verbElement)
                    ? verbElement.GetString()
                    : null;

                if (!string.IsNullOrEmpty(messageId) &&
                    _pending.TryRemove(messageId, out var completion))
                {
                    completion.TrySetResult(message.Clone());
                    continue;
                }

                if (string.Equals(verb, "BROADCAST", StringComparison.Ordinal) &&
                    string.Equals(path, "/battery/state/changed", StringComparison.Ordinal) &&
                    message.TryGetProperty("payload", out _))
                {
                    var report = GHubProtocolParser.ParseBattery(message);
                    try
                    {
                        BatteryChanged?.Invoke(report);
                    }
                    catch (Exception exception)
                    {
                        _log.Error("A battery-change handler failed.", exception);
                    }

                    await SubscribeToBatteryChangesAsync(cancellationToken);
                }

                if (string.Equals(verb, "BROADCAST", StringComparison.Ordinal) &&
                    string.Equals(path, "/devices/state/changed", StringComparison.Ordinal))
                {
                    try
                    {
                        DeviceStateChanged?.Invoke();
                    }
                    catch (Exception exception)
                    {
                        _log.Error("A device-state handler failed.", exception);
                    }

                    await SubscribeToDeviceStateChangesAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            failure = exception;
            throw;
        }
        finally
        {
            var exception = failure ?? new WebSocketException("The G HUB WebSocket closed.");
            foreach (var pending in _pending.Values)
            {
                pending.TrySetException(exception);
            }

            _pending.Clear();
        }
    }

    private async Task<string> ReceiveTextAsync(CancellationToken cancellationToken)
    {
        var socket = _socket ?? throw new WebSocketException("The G HUB WebSocket is not open.");
        var buffer = ArrayPool<byte>.Shared.Rent(8192);

        try
        {
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new WebSocketException("G HUB closed the WebSocket connection.");
                }

                stream.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            return Encoding.UTF8.GetString(stream.GetBuffer(), 0, checked((int)stream.Length));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void ValidateResponse(JsonElement response, string path)
    {
        if (response.TryGetProperty("result", out var result) &&
            result.TryGetProperty("code", out var codeElement) &&
            !string.Equals(codeElement.GetString(), "SUCCESS", StringComparison.Ordinal))
        {
            throw new GHubProtocolException(
                $"G HUB rejected {path}: {codeElement.GetString() ?? "UNKNOWN"}.");
        }
    }

    private sealed record GHubRequest(
        [property: System.Text.Json.Serialization.JsonPropertyName("msgId")] string MessageId,
        [property: System.Text.Json.Serialization.JsonPropertyName("verb")] string Verb,
        [property: System.Text.Json.Serialization.JsonPropertyName("path")] string Path);
}
