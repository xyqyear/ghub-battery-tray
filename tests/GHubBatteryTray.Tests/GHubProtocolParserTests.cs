using System.Text.Json;
using GHubBatteryTray.GHub;

namespace GHubBatteryTray.Tests;

[TestClass]
public sealed class GHubProtocolParserTests
{
    [TestMethod]
    public void ParseDevicesUsesSignatureAsStableKey()
    {
        const string json = """
            {
              "payload": {
                "deviceInfos": [
                  {
                    "id": "dev00000007",
                    "deviceUnitId": "123456",
                    "deviceSignature": "MOUSE.pro_wireless.0.123456",
                    "deviceModel": "pro_wireless",
                    "extendedDisplayName": "Pro Wireless Mouse",
                    "deviceType": "MOUSE",
                    "state": "ACTIVE",
                    "capabilities": { "hasBatteryStatus": true }
                  }
                ]
              }
            }
            """;
        using var document = JsonDocument.Parse(json);

        var devices = GHubProtocolParser.ParseDevices(document.RootElement);

        Assert.HasCount(1, devices);
        Assert.AreEqual("MOUSE.pro_wireless.0.123456", devices[0].Key);
        Assert.AreEqual("dev00000007", devices[0].DeviceId);
        Assert.AreEqual("Pro Wireless Mouse", devices[0].Name);
        Assert.IsTrue(devices[0].HasBatteryStatus);
    }

    [TestMethod]
    public void ParseBatteryAcceptsOptionalFields()
    {
        const string json = """
            {
              "payload": {
                "percentage": 86,
                "charging": false
              }
            }
            """;
        using var document = JsonDocument.Parse(json);

        var report = GHubProtocolParser.ParseBattery(document.RootElement, "dev00000007");

        Assert.AreEqual("dev00000007", report.DeviceId);
        Assert.AreEqual(86, report.Percentage);
        Assert.IsFalse(report.Charging);
        Assert.IsFalse(report.FullyCharged);
        Assert.IsNull(report.MileageHours);
    }

    [TestMethod]
    public void StableKeyFallsBackToModelAndUnitId()
    {
        var key = GHubProtocolParser.CreateStableKey(
            null,
            "g502_x",
            "unit-42",
            "dev00000002");

        Assert.AreEqual("g502_x:unit-42", key);
    }
}
