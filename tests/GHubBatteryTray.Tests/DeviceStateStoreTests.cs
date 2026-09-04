using GHubBatteryTray.Devices;

namespace GHubBatteryTray.Tests;

[TestClass]
public sealed class DeviceStateStoreTests
{
    [TestMethod]
    public void DisconnectRetainsLastKnownBatteryAsStale()
    {
        var store = new DeviceStateStore();
        var device = new GHubDevice(
            "mouse-key",
            "dev00000001",
            "unit-1",
            "mouse-key",
            "Test Mouse",
            "MOUSE",
            "ACTIVE",
            true);
        store.ReplaceDevices([device]);
        store.UpdateBattery(
            device.DeviceId,
            73,
            false,
            false,
            20.5,
            DateTimeOffset.UtcNow);

        store.MarkDisconnected();

        var snapshot = store.Snapshot;
        Assert.HasCount(1, snapshot);
        var status = snapshot[0];
        Assert.IsNotNull(status.Battery);
        Assert.AreEqual(73, status.Battery.Percentage);
        Assert.IsFalse(status.Battery.IsLive);
        Assert.IsFalse(status.IsPresent);
    }

    [TestMethod]
    public void NewEphemeralIdUpdatesExistingStableDevice()
    {
        var store = new DeviceStateStore();
        var first = CreateDevice("dev00000001");
        store.ReplaceDevices([first]);
        store.UpdateBattery(first.DeviceId, 70, false, false, null, DateTimeOffset.UtcNow);

        var reconnected = CreateDevice("dev00000009");
        store.ReplaceDevices([reconnected]);
        store.UpdateBattery(reconnected.DeviceId, 69, false, false, null, DateTimeOffset.UtcNow);

        var snapshot = store.Snapshot;
        Assert.HasCount(1, snapshot);
        var status = snapshot[0];
        Assert.AreEqual("dev00000009", status.Device.DeviceId);
        Assert.AreEqual(69, status.Battery?.Percentage);
    }

    private static GHubDevice CreateDevice(string deviceId) => new(
        "stable-signature",
        deviceId,
        "unit-1",
        "stable-signature",
        "Test Mouse",
        "MOUSE",
        "ACTIVE",
        true);
}
