using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public static class BrowserGameplayModCatalog
{
    private static readonly Lock SyncRoot = new();
    private static GameplayModPackDefinition[] _definitions = [];

    public static bool HasDefinitions
    {
        get
        {
            lock (SyncRoot)
            {
                return _definitions.Length > 0;
            }
        }
    }

    public static IReadOnlyList<GameplayModPackDefinition> GetDefinitions()
    {
        lock (SyncRoot)
        {
            return _definitions;
        }
    }

    public static void Register(IEnumerable<GameplayModPackDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var snapshot = definitions.ToArray();
        lock (SyncRoot)
        {
            _definitions = snapshot;
        }
    }

    public static bool TryGetDefinition(string packId, out GameplayModPackDefinition definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);

        lock (SyncRoot)
        {
            definition = _definitions.FirstOrDefault(definition =>
                string.Equals(definition.Id, packId, StringComparison.OrdinalIgnoreCase))!;
            return definition is not null;
        }
    }
}
