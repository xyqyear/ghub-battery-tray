using System.IO;
using GHubBatteryTray.Devices;
using GHubBatteryTray.Infrastructure;
using GHubBatteryTray.Settings;

namespace GHubBatteryTray.Tests;

[TestClass]
public sealed class SettingsServiceTests
{
    [TestMethod]
    public async Task SettingsRoundTripPreservesSelectionAndBatteryCache()
    {
        var root = Path.Combine(Path.GetTempPath(), $"GHubBatteryTray.Tests-{Guid.NewGuid():N}");

        try
        {
            var paths = new AppPaths(root);
            var log = new AppLog(paths);
            using (var settings = new SettingsService(paths, log))
            {
                await settings.InitializeAsync();
                await settings.SetDeviceSelectedAsync("stable-key", true);
                await settings.SetStartWithWindowsAsync(true);
                await settings.UpdateCachedDeviceAsync(CreateStatus());
            }

            using var reloaded = new SettingsService(paths, log);
            await reloaded.InitializeAsync();

            Assert.Contains("stable-key", reloaded.Snapshot.SelectedDeviceKeys);
            Assert.IsTrue(reloaded.Snapshot.StartWithWindows);
            Assert.AreEqual(61, reloaded.Snapshot.LastKnownDevices["stable-key"].Percentage);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static DeviceStatus CreateStatus()
    {
        var device = new GHubDevice(
            "stable-key",
            "dev00000001",
            "unit-1",
            "stable-key",
            "Test Mouse",
            "MOUSE",
            "ACTIVE",
            true);
        var battery = new BatterySnapshot(
            61,
            false,
            false,
            12.5,
            DateTimeOffset.UtcNow,
            true);
        return new DeviceStatus(device, battery, true);
    }
}
