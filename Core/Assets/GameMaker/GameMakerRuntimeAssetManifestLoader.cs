using System.Text.Json;

namespace OpenGarrison.Core;

public static class GameMakerRuntimeAssetManifestLoader
{
    public const string ManifestRelativePath = "_gamemaker-asset-manifest.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static GameMakerAssetManifest LoadPackagedOrProjectAssets()
    {
        var attemptedPaths = GetManifestCandidatePaths();
        foreach (var manifestPath in attemptedPaths)
        {
            if (TryLoadFromFile(manifestPath, out var manifest))
            {
                return manifest;
            }
        }

        throw new FileNotFoundException(
            $"Runtime GameMaker asset manifest was not found or could not be loaded. Checked: {string.Join(", ", attemptedPaths)}. Run the asset bake before starting the game.",
            attemptedPaths[0]);
    }

    public static bool TryLoadFromContentRoot(out GameMakerAssetManifest manifest)
    {
        foreach (var manifestPath in GetManifestCandidatePaths())
        {
            if (TryLoadFromFile(manifestPath, out manifest))
            {
                return true;
            }
        }

        manifest = null!;
        return false;
    }

    public static bool TryLoadFromFile(string manifestPath, out GameMakerAssetManifest manifest)
    {
        manifest = null!;
        if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
        {
            return false;
        }

        try
        {
            var document = JsonSerializer.Deserialize<BrowserGameMakerAssetManifestDocument>(
                File.ReadAllText(manifestPath),
                JsonOptions);
            if (document is null)
            {
                return false;
            }

            manifest = document.ToManifest();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return false;
        }
    }

    private static string[] GetManifestCandidatePaths()
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var paths = new List<string>(capacity: 2);

        AddCandidate(ContentRoot.GetPath(ManifestRelativePath));
        AddCandidate(Path.Combine(AppContext.BaseDirectory, "Content", ManifestRelativePath));
        return paths.ToArray();

        void AddCandidate(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var fullPath = Path.GetFullPath(path);
            if (!paths.Contains(fullPath, comparer))
            {
                paths.Add(fullPath);
            }
        }
    }
}
