using MoonSharp.Interpreter;

namespace OpenGarrison.Core;

public static class MapScriptMetadata
{
    public const string SourceKey = "mapScript";
    public const int MaximumSourceLength = 65536;
    public static string Read(IReadOnlyDictionary<string, string> metadata)
        => metadata.TryGetValue(SourceKey, out var source) ? source : string.Empty;
    public static string? Validate(string source)
    {
        if (source.Length > MaximumSourceLength) return "Map script exceeds 64 KB.";
        try { new Script(CoreModules.None).LoadString(source, codeFriendlyName: "map.lua"); return null; }
        catch (InterpreterException error)
        {
            var message = error.DecoratedMessage ?? error.Message;
            return message.Length > 1000 ? message[..1000] : message;
        }
    }
}
