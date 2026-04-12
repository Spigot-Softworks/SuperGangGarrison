namespace OpenGarrison.Core;

public static class ContentRoot
{
    private static string? _path;

    public static string Path => string.IsNullOrWhiteSpace(_path) ? "Content" : _path;

    public static void Initialize(string rootDirectory)
    {
        _path = rootDirectory;
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
