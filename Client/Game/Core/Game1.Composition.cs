#nullable enable

namespace OpenGarrison.Client;

public partial class Game1
{
    private static (
        ClientSettings ClientSettings,
        InputBindingsSettings InputBindings,
        LastToDieStatsDocument LastToDieStats,
        HostedServerRuntimeController HostedServerRuntime,
        Microsoft.Xna.Framework.GraphicsDeviceManager GraphicsDeviceManager)
        CreateRuntimeServices(Game1 game, HostedServerConsoleState hostedServerConsole)
    {
        return (
            ClientSettings.Load(),
            InputBindingsSettings.Load(),
            LastToDieStatsDocument.Load(),
            new HostedServerRuntimeController(hostedServerConsole),
            new Microsoft.Xna.Framework.GraphicsDeviceManager(game));
    }
}
