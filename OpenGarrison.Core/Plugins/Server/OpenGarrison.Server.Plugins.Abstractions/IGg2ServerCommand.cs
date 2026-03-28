namespace OpenGarrison.Server.Plugins;

public interface IOpenGarrisonServerCommand
{
    string Name { get; }

    string Description { get; }

    string Usage { get; }

    Task<IReadOnlyList<string>> ExecuteAsync(
        OpenGarrisonServerCommandContext context,
        string arguments,
        CancellationToken cancellationToken);
}
