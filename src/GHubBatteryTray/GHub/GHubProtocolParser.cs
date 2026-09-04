using System.Text.Json;
using GHubBatteryTray.Devices;

namespace GHubBatteryTray.GHub;

public static class GHubProtocolParser
{
    public static IReadOnlyList<GHubDevice> ParseDevices(JsonElement message)
    {
        if (!message.TryGetProperty("payload", out var payload) ||
            !payload.TryGetProperty("deviceInfos", out var deviceInfos) ||
            deviceInfos.ValueKind != JsonValueKind.Array)
        {
            throw new GHubProtocolException("G HUB returned an invalid device list.");
        }

        var devices = new List<GHubDevice>();
        foreach (var item in deviceInfos.EnumerateArray())
        {
            var deviceId = GetString(item, "id");
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                continue;
            }

            var capabilities = item.TryGetProperty("capabilities", out var value)
                ? value
                : default;
            var hasBattery = capabilities.ValueKind == JsonValueKind.Object &&
                GetBoolean(capabilities, "hasBatteryStatus");
            var unitId = GetString(item, "deviceUnitId") ?? GetString(item, "unitId");
            var signature = GetString(item, "deviceSignature");
            var model = GetString(item, "deviceModel") ?? "unknown";
            var name = GetString(item, "extendedDisplayName") ??
                GetString(item, "displayName") ??
                model;
            var key = CreateStableKey(signature, model, unitId, deviceId);

            devices.Add(new GHubDevice(
                key,
                deviceId,
                unitId,
                signature,
                name,
                GetString(item, "deviceType") ?? "UNKNOWN",
                GetString(item, "state") ?? "UNKNOWN",
                hasBattery));
        }

        return devices;
    }

    public static GHubBatteryReport ParseBattery(JsonElement message, string? fallbackDeviceId = null)
    {
        if (!message.TryGetProperty("payload", out var payload))
        {
            throw new GHubProtocolException("G HUB returned a battery message without a payload.");
        }

        var deviceId = GetString(payload, "deviceId") ?? fallbackDeviceId;
        if (string.IsNullOrWhiteSpace(deviceId) ||
            !payload.TryGetProperty("percentage", out var percentageElement) ||
            !percentageElement.TryGetInt32(out var percentage))
        {
            throw new GHubProtocolException("G HUB returned an invalid battery message.");
        }

        return new GHubBatteryReport(
            deviceId,
            Math.Clamp(percentage, 0, 100),
            GetBoolean(payload, "charging"),
            GetBoolean(payload, "fullyCharged"),
            GetDouble(payload, "mileage"),
            DateTimeOffset.UtcNow);
    }

    public static string CreateStableKey(
        string? signature,
        string model,
        string? unitId,
        string deviceId)
    {
        if (!string.IsNullOrWhiteSpace(signature))
        {
            return signature;
        }

        if (!string.IsNullOrWhiteSpace(unitId))
        {
            return $"{model}:{unitId}";
        }

        return $"volatile:{model}:{deviceId}";
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool GetBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        property.GetBoolean();

    private static double? GetDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.Number &&
        property.TryGetDouble(out var value)
            ? value
            : null;
}
