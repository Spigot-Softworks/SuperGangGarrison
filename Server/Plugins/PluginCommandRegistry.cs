using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class PluginCommandRegistry
{
    private readonly Dictionary<string, CommandRegistration> _commandsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<CommandRegistration> _primaryCommands = new();

    public void RegisterBuiltIn(
        string name,
        string description,
        string usage,
        Func<OpenGarrisonServerCommandContext, string, CancellationToken, Task<IReadOnlyList<string>>> executeAsync,
        params string[] aliases)
    {
        RegisterBuiltIn(name, description, usage, executeAsync, OpenGarrisonServerAdminPermissions.None, aliases);
    }

    public void RegisterBuiltIn(
        string name,
        string description,
        string usage,
        Func<OpenGarrisonServerCommandContext, string, CancellationToken, Task<IReadOnlyList<string>>> executeAsync,
        OpenGarrisonServerAdminPermissions requiredPermissions,
        params string[] aliases)
    {
        var command = new BuiltInServerCommand(name, description, usage, executeAsync);
        Register(command, ownerId: "builtin", requiredPermissions, aliases);
    }

    public void RegisterPluginCommand(
        IOpenGarrisonServerCommand command,
        string pluginId,
        OpenGarrisonServerAdminPermissions requiredPermissions = OpenGarrisonServerAdminPermissions.None)
    {
        Register(command, pluginId, requiredPermissions, []);
    }

    public IReadOnlyList<(string Name, string Description, string Usage, string OwnerId, bool IsBuiltIn, OpenGarrisonServerAdminPermissions RequiredPermissions)> GetPrimaryCommands()
    {
        return _primaryCommands
            .OrderBy(entry => entry.Command.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => (
                entry.Command.Name,
                entry.Command.Description,
                entry.Command.Usage,
                entry.OwnerId,
                string.Equals(entry.OwnerId, "builtin", StringComparison.OrdinalIgnoreCase),
                entry.RequiredPermissions))
            .ToArray();
    }

    public bool TryExecute(
        string commandLine,
        OpenGarrisonServerCommandContext context,
        CancellationToken cancellationToken,
        out IReadOnlyList<string> responseLines)
    {
        responseLines = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return false;
        }

        var trimmed = commandLine.Trim();
        var separatorIndex = trimmed.IndexOf(' ');
        var commandName = separatorIndex < 0 ? trimmed : trimmed[..separatorIndex];
        var arguments = separatorIndex < 0 ? string.Empty : trimmed[(separatorIndex + 1)..].Trim();
        if (!_commandsByName.TryGetValue(commandName, out var registration))
        {
            return false;
        }

        if (!context.HasPermission(registration.RequiredPermissions))
        {
            responseLines =
            [
                $"[server] command \"{registration.Command.Name}\" requires {registration.RequiredPermissions}."
            ];
            return true;
        }

        try
        {
            responseLines = registration.Command.ExecuteAsync(context, arguments, cancellationToken).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            responseLines =
            [
                $"[server] command \"{registration.Command.Name}\" was canceled."
            ];
        }
        catch (Exception ex)
        {
            responseLines =
            [
                $"[server] command \"{registration.Command.Name}\" failed: {ex.Message}"
            ];
        }

        return true;
    }

    private void Register(
        IOpenGarrisonServerCommand command,
        string ownerId,
        OpenGarrisonServerAdminPermissions requiredPermissions,
        IReadOnlyList<string> aliases)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);
        var registration = new CommandRegistration(command, ownerId, requiredPermissions);
        RegisterName(command.Name, registration, isPrimary: true);
        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                continue;
            }

            RegisterName(alias.Trim(), registration, isPrimary: false);
        }
    }

    private void RegisterName(string name, CommandRegistration registration, bool isPrimary)
    {
        if (_commandsByName.TryGetValue(name, out var existing))
        {
            throw new InvalidOperationException(
                $"Command \"{name}\" is already registered by \"{existing.OwnerId}\".");
        }

        _commandsByName[name] = registration;
        if (isPrimary)
        {
            _primaryCommands.Add(registration);
        }
    }

    private sealed record CommandRegistration(
        IOpenGarrisonServerCommand Command,
        string OwnerId,
        OpenGarrisonServerAdminPermissions RequiredPermissions);
}
