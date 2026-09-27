using System.Text.Json.Serialization;

namespace OpenGarrison.PluginHost;

/// <summary>
/// The type of plugin a host supports.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OpenGarrisonPluginType>))]
public enum OpenGarrisonPluginType
{
    /// <summary>A client plugin.</summary>
    Client,
    /// <summary>A server plugin.</summary>
    Server,
    /// <summary>A gameplay plugin.</summary>
    Gameplay,
}

/// <summary>
/// The runtime kind a plugin runs in.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<OpenGarrisonPluginRuntimeKind>))]
public enum OpenGarrisonPluginRuntimeKind
{
    /// <summary>A CLR (managed assembly) plugin.</summary>
    Clr,
    /// <summary>A Lua script plugin.</summary>
    Lua,
}

/// <summary>
/// The capabilities a plugin host exposes.
/// </summary>
/// <param name="ReadOnlyState">Whether read-only state is available.</param>
/// <param name="SemanticEvents">Whether semantic events are available.</param>
/// <param name="PluginMessaging">Whether plugin messaging is available.</param>
/// <param name="ReplicatedState">Whether replicated state is available.</param>
/// <param name="AssetRegistration">Whether asset registration is available.</param>
/// <param name="UiRegistration">Whether UI registration is available.</param>
/// <param name="Hotkeys">Whether hotkeys are available.</param>
/// <param name="ScoreboardPanels">Whether scoreboard panels are available.</param>
/// <param name="AdminOperations">Whether admin operations are available.</param>
/// <param name="LoadoutSelection">Whether loadout selection is available.</param>
/// <param name="Voting">Whether voting is available.</param>
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
    bool LoadoutSelection,
    bool Voting = false);

/// <summary>
/// A runtime surface exposed by the host.
/// </summary>
/// <param name="Runtime">The runtime kind.</param>
/// <param name="ApiVersion">The API version.</param>
/// <param name="Functions">The available function names.</param>
public sealed record OpenGarrisonPluginRuntimeSurface(
    OpenGarrisonPluginRuntimeKind Runtime,
    string ApiVersion,
    IReadOnlyList<string> Functions);

/// <summary>
/// Describes the API a plugin host exposes.
/// </summary>
/// <param name="ApiVersion">The API version.</param>
/// <param name="HostType">The host type.</param>
/// <param name="Capabilities">The host capabilities.</param>
/// <param name="RuntimeSurfaces">The runtime surfaces.</param>
public sealed record OpenGarrisonPluginHostApi(
    string ApiVersion,
    OpenGarrisonPluginType HostType,
    OpenGarrisonPluginHostCapabilities Capabilities,
    IReadOnlyList<OpenGarrisonPluginRuntimeSurface> RuntimeSurfaces)
{
    /// <summary>
    /// Creates the default host API description for a client host.
    /// </summary>
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
                LoadoutSelection: false,
                Voting: false),
            [
                new OpenGarrisonPluginRuntimeSurface(
                    OpenGarrisonPluginRuntimeKind.Lua,
                    OpenGarrisonLuaHostApiSurface.ApiVersion,
                    OpenGarrisonLuaHostApiSurface.ClientFunctions),
            ]);
    }

    /// <summary>
    /// Creates the default host API description for a server host.
    /// </summary>
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
                LoadoutSelection: true,
                Voting: true),
            [
                new OpenGarrisonPluginRuntimeSurface(
                    OpenGarrisonPluginRuntimeKind.Lua,
                    OpenGarrisonLuaHostApiSurface.ApiVersion,
                    OpenGarrisonLuaHostApiSurface.ServerFunctions),
            ]);
    }
}
