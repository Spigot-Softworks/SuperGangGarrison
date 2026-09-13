using System;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server.Plugins.ChatVoting;

/// <summary>
/// CLR reference plugin showing how to extend the native vote coordinator without
/// owning ballot, timeout, cooldown, networking, replay, or menu state.
/// </summary>
public sealed class ChatVotingPlugin : IOpenGarrisonServerPlugin
{
    public string Id => "chat.voting";

    public string DisplayName => "Native Vote Extension Example";

    public Version Version => new(2, 0, 0);

    public void Initialize(IOpenGarrisonServerPluginContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var registration = new OpenGarrisonServerVoteRegistration(
            "restart-map",
            "Restart Current Map",
            "Reload the current map and area.",
            OpenGarrisonServerVoteTargetKind.None,
            _ => context.AdminOperations.TryChangeMap(
                context.ServerState.LevelName,
                context.ServerState.MapAreaIndex,
                preservePlayerStats: false),
            _ => string.IsNullOrWhiteSpace(context.ServerState.LevelName)
                ? OpenGarrisonServerVoteValidationResult.Reject("No active map can be restarted.")
                : OpenGarrisonServerVoteValidationResult.Accept($"restart {context.ServerState.LevelName}"));

        if (!context.TryRegisterVoteKind(registration, out var errorMessage))
        {
            throw new InvalidOperationException($"Native restart vote registration failed: {errorMessage}");
        }

        context.Log("registered native restart-map vote kind");
    }

    public void Shutdown()
    {
    }
}
