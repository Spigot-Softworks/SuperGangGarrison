namespace OpenGarrison.PluginHost;

public static partial class OpenGarrisonLuaHostApiSurface
{
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
