#nullable enable

using static OpenGarrison.Client.Game1;

namespace OpenGarrison.Client;

public sealed class PluginManager
{
    private readonly IPluginContext _context;

    public PluginManager(IPluginContext context)
    {
        _context = context;
        Runtime = new ClientPluginRuntimeController(context);
        Events = new ClientPluginEventController(context);
        UiBridge = new ClientPluginUiBridgeController(context);
        Marker = new ClientPluginMarkerController(context);
    }

    public ClientPluginRuntimeController Runtime { get; }

    public ClientPluginEventController Events { get; }

    public ClientPluginUiBridgeController UiBridge { get; }

    public ClientPluginMarkerController Marker { get; }
}
