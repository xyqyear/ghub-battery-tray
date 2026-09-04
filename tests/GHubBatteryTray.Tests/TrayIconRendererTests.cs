using GHubBatteryTray.Devices;
using GHubBatteryTray.Tray;

namespace GHubBatteryTray.Tests;

[TestClass]
public sealed class TrayIconRendererTests
{
    [TestMethod]
    public void RendererCreatesUsableIconsForEveryState()
    {
        var renderer = new TrayIconRenderer();

        using var unavailable = renderer.CreateIcon(null);
        using var normal = renderer.CreateIcon(CreateStatus(82, false, true));
        using var charging = renderer.CreateIcon(CreateStatus(32, true, true));
        using var stale = renderer.CreateIcon(CreateStatus(15, false, false));

        Assert.AreNotEqual(IntPtr.Zero, unavailable.Handle);
        Assert.AreNotEqual(IntPtr.Zero, normal.Handle);
        Assert.AreNotEqual(IntPtr.Zero, charging.Handle);
        Assert.AreNotEqual(IntPtr.Zero, stale.Handle);
    }

    [TestMethod]
    public void BatteryFillRisesFromBottom()
    {
        var renderer = new TrayIconRenderer();
        using var icon = renderer.CreateIcon(CreateStatus(50, false, true));
        using var bitmap = icon.ToBitmap();

        var topPixel = bitmap.GetPixel(20, 10);
        var bottomPixel = bitmap.GetPixel(20, 25);

        Assert.IsGreaterThan(topPixel.G + 50, bottomPixel.G);
    }

    [TestMethod]
    public void StaleReadingKeepsTheLastLiveIcon()
    {
        var renderer = new TrayIconRenderer();
        using var liveIcon = renderer.CreateIcon(CreateStatus(64, false, true));
        using var staleIcon = renderer.CreateIcon(CreateStatus(64, false, false));
        using var liveBitmap = liveIcon.ToBitmap();
        using var staleBitmap = staleIcon.ToBitmap();

        for (var x = 0; x < liveBitmap.Width; x++)
        {
            for (var y = 0; y < liveBitmap.Height; y++)
            {
                Assert.AreEqual(liveBitmap.GetPixel(x, y), staleBitmap.GetPixel(x, y));
            }
        }
    }

    private static DeviceStatus CreateStatus(int percentage, bool charging, bool live)
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
            percentage,
            charging,
            false,
            10,
            DateTimeOffset.UtcNow,
            live);
        return new DeviceStatus(device, battery, live);
    }
}
