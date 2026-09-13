namespace OpenGarrison.Core;

public static class BuilderDocumentComparer
{
    public static bool ContentEquals(CustomMapBuilderDocument a, CustomMapBuilderDocument b)
    {
        if (ReferenceEquals(a, b)) return true;
        return a.Name == b.Name && a.BackgroundImagePath == b.BackgroundImagePath && a.WalkmaskImagePath == b.WalkmaskImagePath
            && a.Scale == b.Scale && a.VisualScale == b.VisualScale && a.EmbeddedWalkmaskSection == b.EmbeddedWalkmaskSection
            && DictionaryEquals(a.Metadata, b.Metadata) && a.ParallaxLayers.SequenceEqual(b.ParallaxLayers)
            && a.Entities.Count == b.Entities.Count && a.Entities.Zip(b.Entities).All(pair => EntityEquals(pair.First, pair.Second))
            && a.Resources.Count == b.Resources.Count && a.Resources.All(pair => b.Resources.TryGetValue(pair.Key, out var other)
                && pair.Value.Name == other.Name && pair.Value.Kind == other.Kind && pair.Value.SourcePath == other.SourcePath
                && (ReferenceEquals(pair.Value.EmbeddedBytes, other.EmbeddedBytes)
                    || (pair.Value.EmbeddedBytes ?? []).AsSpan().SequenceEqual(other.EmbeddedBytes ?? [])));
    }
    private static bool EntityEquals(CustomMapBuilderEntity a, CustomMapBuilderEntity b) => ReferenceEquals(a,b)
        || (a.Type == b.Type && a.X == b.X && a.Y == b.Y && a.XScale == b.XScale && a.YScale == b.YScale && DictionaryEquals(a.Properties,b.Properties));
    private static bool DictionaryEquals(IReadOnlyDictionary<string,string> a, IReadOnlyDictionary<string,string> b) =>
        ReferenceEquals(a,b) || (a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out var value) && value == pair.Value));
}
