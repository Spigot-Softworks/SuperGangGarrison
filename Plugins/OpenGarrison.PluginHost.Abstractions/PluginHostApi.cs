using System.Text.Json.Serialization;

namespace OpenGarrison.PluginHost;

[JsonConverter(typeof(JsonStringEnumConverter<OpenGarrisonPluginType>))]
public enum OpenGarrisonPluginType
{
    Client,
    Server,
    Gameplay,
}

[JsonConverter(typeof(JsonStringEnumConverter<OpenGarrisonPluginRuntimeKind>))]
public enum OpenGarrisonPluginRuntimeKind
{
    Clr,
    Lua,
}

public sealed record OpenGarrisonPluginHostCapabilities(
    bool ReadOnlyState,
    bool SemanticEvents,
    bool PluginMessaging,
    bool ReplicatedState,
    bool AssetRegistration,
    bool UiRegistration,
    bool Hotkeys,
    bool ScoreboardPanels,
    bool AdminOperations,
    bool LoadoutSelection);

public sealed record OpenGarrisonPluginRuntimeSurface(
    OpenGarrisonPluginRuntimeKind Runtime,
    string ApiVersion,
    IReadOnlyList<string> Functions);

public sealed record OpenGarrisonPluginHostApi(
    string ApiVersion,
    OpenGarrisonPluginType HostType,
    OpenGarrisonPluginHostCapabilities Capabilities,
    IReadOnlyList<OpenGarrisonPluginRuntimeSurface> RuntimeSurfaces)
{
    public static OpenGarrisonPluginHostApi CreateClientDefault()
    {
        return new OpenGarrisonPluginHostApi(
            "1.0",
            OpenGarrisonPluginType.Client,
            new OpenGarrisonPluginHostCapabilities(
                ReadOnlyState: true,
                SemanticEvents: true,
                PluginMessaging: true,
                ReplicatedState: true,
                AssetRegistration: true,
                UiRegistration: true,
                Hotkeys: true,
                ScoreboardPanels: true,
                AdminOperations: false,
                LoadoutSelection: false),
            [
                new OpenGarrisonPluginRuntimeSurface(
                    OpenGarrisonPluginRuntimeKind.Lua,
                    OpenGarrisonLuaHostApiSurface.ApiVersion,
                    OpenGarrisonLuaHostApiSurface.ClientFunctions),
            ]);
    }

    public static OpenGarrisonPluginHostApi CreateServerDefault()
    {
        return new OpenGarrisonPluginHostApi(
            "1.0",
            OpenGarrisonPluginType.Server,
            new OpenGarrisonPluginHostCapabilities(
                ReadOnlyState: true,
                SemanticEvents: true,
                PluginMessaging: true,
                ReplicatedState: true,
                AssetRegistration: false,
                UiRegistration: false,
                Hotkeys: false,
                ScoreboardPanels: false,
                AdminOperations: true,
                LoadoutSelection: true),
            [
                new OpenGarrisonPluginRuntimeSurface(
                    OpenGarrisonPluginRuntimeKind.Lua,
                    OpenGarrisonLuaHostApiSurface.ApiVersion,
                    OpenGarrisonLuaHostApiSurface.ServerFunctions),
            ]);
    }
}
