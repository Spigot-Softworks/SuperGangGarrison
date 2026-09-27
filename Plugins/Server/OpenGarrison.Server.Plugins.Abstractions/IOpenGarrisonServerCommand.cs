namespace OpenGarrison.Server.Plugins;

/// <summary>
/// A server command a plugin can register for admin and console use.
/// </summary>
public interface IOpenGarrisonServerCommand
{
    /// <summary>
    /// Gets the command name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the command description.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the command usage text.
    /// </summary>
    string Usage { get; }

    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="arguments">The raw command arguments.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The output lines produced by the command.</returns>
    Task<IReadOnlyList<string>> ExecuteAsync(
        OpenGarrisonServerCommandContext context,
        string arguments,
        CancellationToken cancellationToken);
}
