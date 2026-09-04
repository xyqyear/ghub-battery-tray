using System.IO;
using System.Text.Json;
using GHubBatteryTray.Devices;
using GHubBatteryTray.Infrastructure;

namespace GHubBatteryTray.Settings;

public sealed class SettingsService : IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly AppPaths _paths;
    private readonly AppLog _log;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private readonly object _sync = new();
    private AppSettings _settings = new();

    public SettingsService(AppPaths paths, AppLog log)
    {
        _paths = paths;
        _log = log;
    }

    public event EventHandler? Changed;

    public void Dispose() => _writeGate.Dispose();

    public SettingsSnapshot Snapshot
    {
        get
        {
            lock (_sync)
            {
                return CreateSnapshot(_settings);
            }
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        AppSettings loaded;

        if (!File.Exists(_paths.SettingsFile))
        {
            loaded = new AppSettings();
        }
        else
        {
            try
            {
                await using var stream = File.OpenRead(_paths.SettingsFile);
                loaded = await JsonSerializer.DeserializeAsync<AppSettings>(
                    stream,
                    SerializerOptions,
                    cancellationToken) ?? new AppSettings();
            }
            catch (JsonException exception)
            {
                BackupInvalidSettings();
                _log.Error("Settings were invalid and have been reset.", exception);
                loaded = new AppSettings();
            }
            catch (IOException exception)
            {
                _log.Error("Settings could not be read and defaults will be used.", exception);
                loaded = new AppSettings();
            }
        }

        loaded = Normalize(loaded);
        lock (_sync)
        {
            _settings = loaded;
        }
    }

    public Task SetDeviceSelectedAsync(
        string deviceKey,
        bool selected,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            settings =>
            {
                if (settings.SelectedDeviceKeys.Contains(deviceKey) == selected)
                {
                    return settings;
                }

                var selectedKeys = new HashSet<string>(
                    settings.SelectedDeviceKeys,
                    StringComparer.Ordinal);

                if (selected)
                {
                    selectedKeys.Add(deviceKey);
                }
                else
                {
                    selectedKeys.Remove(deviceKey);
                }

                return settings with { SelectedDeviceKeys = selectedKeys };
            },
            cancellationToken);

    public Task SetStartWithWindowsAsync(
        bool enabled,
        CancellationToken cancellationToken = default) =>
        UpdateAsync(
            settings => settings.StartWithWindows == enabled
                ? settings
                : settings with { StartWithWindows = enabled },
            cancellationToken);

    public Task UpdateCachedDeviceAsync(
        DeviceStatus status,
        CancellationToken cancellationToken = default)
    {
        if (status.Battery is null)
        {
            return Task.CompletedTask;
        }

        return UpdateAsync(
            settings =>
            {
                if (settings.LastKnownDevices.TryGetValue(status.Device.Key, out var existing) &&
                    existing.Name == status.Device.Name &&
                    existing.DeviceType == status.Device.DeviceType &&
                    existing.Percentage == status.Battery.Percentage &&
                    existing.Charging == status.Battery.Charging &&
                    existing.FullyCharged == status.Battery.FullyCharged &&
                    existing.MileageHours == status.Battery.MileageHours &&
                    status.Battery.UpdatedAt - existing.UpdatedAt < TimeSpan.FromMinutes(10))
                {
                    return settings;
                }

                var devices = new Dictionary<string, CachedDeviceState>(
                    settings.LastKnownDevices,
                    StringComparer.Ordinal)
                {
                    [status.Device.Key] = new CachedDeviceState
                    {
                        Key = status.Device.Key,
                        Name = status.Device.Name,
                        DeviceType = status.Device.DeviceType,
                        Percentage = status.Battery.Percentage,
                        Charging = status.Battery.Charging,
                        FullyCharged = status.Battery.FullyCharged,
                        MileageHours = status.Battery.MileageHours,
                        UpdatedAt = status.Battery.UpdatedAt,
                    },
                };

                return settings with { LastKnownDevices = devices };
            },
            cancellationToken);
    }

    private async Task UpdateAsync(
        Func<AppSettings, AppSettings> update,
        CancellationToken cancellationToken)
    {
        await _writeGate.WaitAsync(cancellationToken);
        try
        {
            AppSettings updated;
            lock (_sync)
            {
                var candidate = update(_settings);
                if (ReferenceEquals(candidate, _settings))
                {
                    return;
                }

                updated = Normalize(candidate);
                _settings = updated;
            }

            await SaveAsync(updated, cancellationToken);
        }
        finally
        {
            _writeGate.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        var temporaryPath = $"{_paths.SettingsFile}.tmp";

        await using (var stream = new FileStream(
            temporaryPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                settings,
                SerializerOptions,
                cancellationToken);
        }

        File.Move(temporaryPath, _paths.SettingsFile, true);
    }

    private void BackupInvalidSettings()
    {
        try
        {
            var backupPath = Path.Combine(
                _paths.RootDirectory,
                $"settings.invalid-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            File.Copy(_paths.SettingsFile, backupPath, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static AppSettings Normalize(AppSettings settings) => settings with
    {
        SchemaVersion = 1,
        SelectedDeviceKeys = new HashSet<string>(
            settings.SelectedDeviceKeys ?? [],
            StringComparer.Ordinal),
        LastKnownDevices = new Dictionary<string, CachedDeviceState>(
            settings.LastKnownDevices ?? [],
            StringComparer.Ordinal),
    };

    private static SettingsSnapshot CreateSnapshot(AppSettings settings) => new(
        new HashSet<string>(settings.SelectedDeviceKeys, StringComparer.Ordinal),
        settings.StartWithWindows,
        new Dictionary<string, CachedDeviceState>(
            settings.LastKnownDevices,
            StringComparer.Ordinal));
}
