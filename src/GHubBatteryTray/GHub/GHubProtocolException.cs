namespace GHubBatteryTray.GHub;

public sealed class GHubProtocolException : Exception
{
    public GHubProtocolException(string message)
        : base(message)
    {
    }
}
