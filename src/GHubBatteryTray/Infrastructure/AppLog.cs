using System.Globalization;
using System.IO;

namespace GHubBatteryTray.Infrastructure;

public sealed class AppLog
{
    private readonly AppPaths _paths;
    private readonly object _sync = new();

    public AppLog(AppPaths paths)
    {
        _paths = paths;
        PrepareDirectory();
    }

    public void Info(string message) => Write("INF", message);

    public void Warning(string message) => Write("WRN", message);

    public void Error(string message, Exception exception) =>
        Write("ERR", $"{message} {exception}");

    private void Write(string level, string message)
    {
        try
        {
            lock (_sync)
            {
                Directory.CreateDirectory(_paths.LogsDirectory);
                var path = Path.Combine(
                    _paths.LogsDirectory,
                    $"app-{DateTime.Now:yyyyMMdd}.log");
                var line = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{DateTimeOffset.Now:O} [{level}] {message}{Environment.NewLine}");
                File.AppendAllText(path, line);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void PrepareDirectory()
    {
        try
        {
            Directory.CreateDirectory(_paths.LogsDirectory);
            var cutoff = DateTime.UtcNow.AddDays(-7);
            foreach (var path in Directory.EnumerateFiles(_paths.LogsDirectory, "app-*.log"))
            {
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                {
                    File.Delete(path);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
