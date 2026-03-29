using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class BuiltInServerCommand(
    string name,
    string description,
    string usage,
    Func<OpenGarrisonServerCommandContext, string, CancellationToken, Task<IReadOnlyList<string>>> executeAsync) : IOpenGarrisonServerCommand
{
    public string Name { get; } = name;

    public string Description { get; } = description;

    public string Usage { get; } = usage;

    public Task<IReadOnlyList<string>> ExecuteAsync(
        OpenGarrisonServerCommandContext context,
        string arguments,
        CancellationToken cancellationToken)
        => executeAsync(context, arguments, cancellationToken);
}
