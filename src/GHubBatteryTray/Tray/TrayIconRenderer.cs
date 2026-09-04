using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using GHubBatteryTray.Devices;

namespace GHubBatteryTray.Tray;

public sealed partial class TrayIconRenderer
{
    private const int IconSize = 32;
    private readonly int _iconSize = IconSize;

    public Icon CreateIcon(DeviceStatus? status)
    {
        using var bitmap = new Bitmap(
            _iconSize,
            _iconSize,
            System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var battery = status?.Battery;
        var accent = GetAccentColor(battery);
        DrawMouse(graphics);
        DrawBattery(graphics, battery, accent);

        var iconHandle = bitmap.GetHicon();
        try
        {
            using var temporaryIcon = Icon.FromHandle(iconHandle);
            return (Icon)temporaryIcon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private static void DrawMouse(Graphics graphics)
    {
        using var bodyPath = RoundedRectangle(new RectangleF(0, 5, 16, 24), 7.5f);
        using var bodyBrush = new SolidBrush(Color.FromArgb(52, 58, 64));
        using var haloPen = new Pen(Color.FromArgb(20, 20, 20), 3.5f);
        using var edgePen = new Pen(Color.FromArgb(244, 246, 248), 1.4f);
        graphics.FillPath(bodyBrush, bodyPath);
        graphics.DrawPath(haloPen, bodyPath);
        graphics.DrawPath(edgePen, bodyPath);
        graphics.DrawLine(haloPen, 8, 6, 8, 14);
        graphics.DrawLine(edgePen, 8, 6, 8, 14);

        using var wheelBrush = new SolidBrush(Color.FromArgb(71, 184, 255));
        using var wheelPath = RoundedRectangle(new RectangleF(6.5f, 8, 3, 6), 1.5f);
        graphics.FillPath(wheelBrush, wheelPath);
    }

    private static void DrawBattery(
        Graphics graphics,
        BatterySnapshot? battery,
        Color accent)
    {
        using var terminalBrush = new SolidBrush(Color.FromArgb(245, 247, 249));
        using var terminalPath = RoundedRectangle(new RectangleF(17, 1, 7, 6), 2);
        graphics.FillPath(terminalBrush, terminalPath);

        using var batteryPath = RoundedRectangle(new RectangleF(10.5f, 5.5f, 20, 25), 3.5f);
        using var backgroundBrush = new SolidBrush(Color.FromArgb(45, 49, 54));
        using var haloPen = new Pen(Color.FromArgb(18, 18, 18), 3.5f);
        using var edgePen = new Pen(Color.FromArgb(245, 247, 249), 1.5f);
        graphics.FillPath(backgroundBrush, batteryPath);

        if (battery is not null)
        {
            var fillHeight = (int)Math.Round(20 * battery.Percentage / 100d);
            if (battery.Percentage > 0)
            {
                fillHeight = Math.Max(fillHeight, 1);
            }

            if (fillHeight > 0)
            {
                using var fillBrush = new SolidBrush(accent);
                graphics.FillRectangle(fillBrush, 13, 28 - fillHeight, 15, fillHeight);
            }

            if (battery.Charging)
            {
                using var boltBrush = new SolidBrush(Color.FromArgb(255, 220, 75));
                graphics.FillPolygon(
                    boltBrush,
                    [
                        new PointF(21, 8),
                        new PointF(16, 18),
                        new PointF(20, 18),
                        new PointF(18, 27),
                        new PointF(26, 15),
                        new PointF(22, 15),
                    ]);
            }
        }
        else
        {
            using var unavailablePen = new Pen(Color.FromArgb(210, 98, 98), 2.5f);
            graphics.DrawLine(unavailablePen, 14, 9, 27, 28);
        }

        graphics.DrawPath(haloPen, batteryPath);
        graphics.DrawPath(edgePen, batteryPath);
    }

    private static Color GetAccentColor(BatterySnapshot? battery)
    {
        if (battery is null)
        {
            return Color.FromArgb(125, 132, 140);
        }

        if (battery.Charging)
        {
            return Color.FromArgb(54, 168, 255);
        }

        return battery.Percentage switch
        {
            < 20 => Color.FromArgb(239, 83, 80),
            < 50 => Color.FromArgb(255, 183, 77),
            _ => Color.FromArgb(76, 201, 120),
        };
    }

    private static GraphicsPath RoundedRectangle(RectangleF bounds, float radius)
    {
        var diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint iconHandle);
}
