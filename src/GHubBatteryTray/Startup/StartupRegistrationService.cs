using Microsoft.Win32;

namespace GHubBatteryTray.Startup;

public sealed class StartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GHubBatteryTray";
    private readonly string _valueName = ValueName;

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
            return key?.GetValue(_valueName) is string value &&
                !string.IsNullOrWhiteSpace(value);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, true);
        if (enabled)
        {
            var executablePath = Environment.ProcessPath ??
                throw new InvalidOperationException("The application path is unavailable.");
            key.SetValue(_valueName, $"\"{executablePath}\" --background", RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(_valueName, false);
        }
    }
}
