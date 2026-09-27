namespace OpenGarrison.PluginHost;

/// <summary>
/// The Lua host API surface (function lists are declared in the generated partial).
/// </summary>
public static partial class OpenGarrisonLuaHostApiSurface
{
    /// <summary>
    /// Gets the Lua function names for a host type.
    /// </summary>
    /// <param name="hostType">The host type.</param>
    /// <returns>The Lua function names, or an empty list for unknown host types.</returns>
    public static IReadOnlyList<string> GetFunctions(OpenGarrisonPluginType hostType)
    {
        return hostType switch
        {
            OpenGarrisonPluginType.Client => ClientFunctions,
            OpenGarrisonPluginType.Server => ServerFunctions,
            _ => Array.Empty<string>(),
        };
    }

    private static string[] CreateFunctionList(params string[] functions)
    {
        var orderedFunctions = functions
            .OrderBy(static function => function, StringComparer.Ordinal)
            .ToArray();
        if (orderedFunctions.Length != orderedFunctions.Distinct(StringComparer.Ordinal).Count())
        {
            throw new InvalidOperationException("Lua host API function names must be unique.");
        }

        return orderedFunctions;
    }
}
