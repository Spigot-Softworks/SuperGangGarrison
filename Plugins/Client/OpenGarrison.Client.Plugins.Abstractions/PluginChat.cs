using System.Collections.Generic;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// The chat text submitted by the player, before plugin interception.
/// </summary>
/// <param name="Text">The submitted text.</param>
/// <param name="TeamOnly">Whether the message is team-only.</param>
public sealed record ClientChatSubmitContext(
    string Text,
    bool TeamOnly);

/// <summary>
/// The result of plugin chat-submit interception.
/// </summary>
/// <param name="Text">The (possibly rewritten) text.</param>
/// <param name="TeamOnly">Whether the message is team-only.</param>
/// <param name="IsCancelled">Whether the message should be dropped.</param>
/// <param name="IsHandled">Whether the plugin handled the message itself.</param>
public sealed record ClientChatSubmitResult(
    string Text,
    bool TeamOnly,
    bool IsCancelled = false,
    bool IsHandled = false);

/// <summary>
/// Hooks a client plugin can implement to intercept outgoing chat.
/// </summary>
public interface IOpenGarrisonClientChatHooks
{
    /// <summary>
    /// Called before a chat message is submitted, allowing rewrite, cancellation, or handling.
    /// </summary>
    /// <param name="context">The submitted chat.</param>
    /// <returns>The interception result.</returns>
    ClientChatSubmitResult BeforeChatSubmit(ClientChatSubmitContext context)
        => new(context.Text, context.TeamOnly);
}

/// <summary>
/// Hooks a client plugin can implement to handle chat commands locally.
/// </summary>
public interface IOpenGarrisonClientChatCommandHooks
{
    /// <summary>
    /// Tries to handle submitted chat as a plugin command.
    /// </summary>
    /// <param name="context">The submitted chat.</param>
    /// <returns>True when the plugin handled the chat.</returns>
    bool TryHandleChatCommand(ClientChatSubmitContext context);
}
