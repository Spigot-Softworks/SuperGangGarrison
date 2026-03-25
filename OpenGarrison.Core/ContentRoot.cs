namespace OpenGarrison.Core;

public static class ContentRoot
{
    public static string Path { get; private set; } = "Content";

    public static void Initialize(string rootDirectory)
    {
        Path = rootDirectory;
        SimpleLevelFactory.ClearCachedCatalog();
    }

    public static string GetPath(params string[] parts)
    {
        var allParts = new string[parts.Length + 1];
        allParts[0] = Path;
        parts.CopyTo(allParts, 1);
        return System.IO.Path.Combine(allParts);
    }
}
