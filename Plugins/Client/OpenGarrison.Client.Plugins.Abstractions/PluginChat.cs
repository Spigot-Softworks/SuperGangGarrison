using System.Collections.Generic;

namespace OpenGarrison.Client.Plugins;

public sealed record ClientChatSubmitContext(
    string Text,
    bool TeamOnly);

public sealed record ClientChatSubmitResult(
    string Text,
    bool TeamOnly,
    bool IsCancelled = false,
    bool IsHandled = false);

public interface IOpenGarrisonClientChatHooks
{
    ClientChatSubmitResult BeforeChatSubmit(ClientChatSubmitContext context)
        => new(context.Text, context.TeamOnly);
}

public interface IOpenGarrisonClientChatCommandHooks
{
    bool TryHandleChatCommand(ClientChatSubmitContext context);
}
