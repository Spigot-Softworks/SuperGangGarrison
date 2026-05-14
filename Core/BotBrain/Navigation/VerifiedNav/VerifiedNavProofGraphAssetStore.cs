using OpenGarrison.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.Core.BotBrain;

public static class VerifiedNavProofGraphAssetStore
{
    public const string EnableEnvironmentVariable = "BOTBRAIN_ENABLE_VERIFIED_PROOFGRAPH";
    public const string RequireEnvironmentVariable = "BOTBRAIN_REQUIRE_VERIFIED_PROOFGRAPH";
    public const string PathEnvironmentVariable = "BOTBRAIN_PROOFGRAPH_PATH";
    public const string DirectoryEnvironmentVariable = "BOTBRAIN_PROOFGRAPH_DIR";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    public static bool IsEnabled()
    {
        var value = Environment.GetEnvironmentVariable(EnableEnvironmentVariable);
        return IsTruthy(value) || IsRequired();
    }

    public static bool IsRequired()
        => IsTruthy(Environment.GetEnvironmentVariable(RequireEnvironmentVariable));

    private static bool IsTruthy(string? value)
        => string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    public static bool TryLoad(
        SimpleLevel level,
        PlayerTeam team,
        PlayerClass classId,
        out VerifiedNavProofGraphAsset asset)
    {
        ArgumentNullException.ThrowIfNull(level);
        asset = null!;

        var proofGraphEnabled = IsEnabled();
        var path = proofGraphEnabled
            ? Environment.GetEnvironmentVariable(PathEnvironmentVariable)
            : null;
        if (proofGraphEnabled && string.IsNullOrWhiteSpace(path))
        {
            var directory = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);
            path = !string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(directory, "verified-proof-graph.json")
                : null;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            path = ResolveShippedContentPath(level, team, classId);
        }

        if ((string.IsNullOrWhiteSpace(path) || !File.Exists(path)) && proofGraphEnabled)
        {
            path = ResolveDefaultArtifactPath(level, team, classId);
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        try
        {
            asset = JsonSerializer.Deserialize<VerifiedNavProofGraphAsset>(File.ReadAllText(path), SerializerOptions)!;
        }
        catch
        {
            asset = null!;
            return false;
        }

        if (asset is null
            || !string.Equals(asset.LevelName, level.Name, StringComparison.OrdinalIgnoreCase)
            || asset.MapAreaIndex != level.MapAreaIndex
            || asset.Team != team
            || asset.ClassId != classId
            || asset.Routes.Count == 0
            || asset.Edges.Count == 0)
        {
            asset = null!;
            return false;
        }

        return true;
    }

    private static string ResolveDefaultArtifactPath(SimpleLevel level, PlayerTeam team, PlayerClass classId)
    {
        var folder = $"{level.Name}-{team}-{classId}-proofgraph";
        var artifactPath = Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            "..",
            "artifacts",
            "verified-nav",
            folder,
            "verified-proof-graph.json");
        return artifactPath;
    }

    private static string ResolveShippedContentPath(SimpleLevel level, PlayerTeam team, PlayerClass classId)
    {
        var shippedFileName = $"{level.Name}.a{level.MapAreaIndex}.{team}.{classId}.verified-proof-graph.json";
        var contentRootPath = ContentRoot.GetPath("BotBrainProofGraphs", shippedFileName);
        if (File.Exists(contentRootPath))
        {
            return contentRootPath;
        }

        var sourceContentPath = ProjectSourceLocator.FindFile(
            Path.Combine("Core", "Content", "BotBrainProofGraphs", shippedFileName));
        if (!string.IsNullOrWhiteSpace(sourceContentPath))
        {
            return sourceContentPath;
        }

        var appContentPath = Path.Combine(
            AppContext.BaseDirectory,
            "Content",
            "BotBrainProofGraphs",
            shippedFileName);
        if (File.Exists(appContentPath))
        {
            return appContentPath;
        }

        return contentRootPath;
    }
}
