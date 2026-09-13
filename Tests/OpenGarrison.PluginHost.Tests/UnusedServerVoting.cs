using OpenGarrison.Server.Plugins;

namespace OpenGarrison.PluginHost.Tests;

internal static class UnusedServerVoting
{
    public static bool Register(string pluginId, OpenGarrisonServerVoteRegistration registration, out string error)
    {
        error = "Voting is not configured in this fixture.";
        return false;
    }
    public static bool Start(string pluginId, string kind, byte slot, string argument, out string error)
    {
        error = "Voting is not configured in this fixture.";
        return false;
    }
}
