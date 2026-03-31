using OpenGarrison.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.BotAI;

public static class BotNavigationAssetStore
{
    public const int CurrentFormatVersion = 2;
    private const string ShippedRelativeDirectory = "Core/Content/BotNav";
    private const string RuntimeCacheDirectoryName = "bot-nav";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    static BotNavigationAssetStore()
    {
        SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static BotNavigationLoadResult LoadForLevel(SimpleLevel level, IReadOnlyList<PlayerClass>? classes = null)
    {
        ArgumentNullException.ThrowIfNull(level);

        var requestedClasses = classes ?? BotNavigationClasses.All;
        var fingerprint = BotNavigationLevelFingerprint.Compute(level);
        var assets = new Dictionary<PlayerClass, BotNavigationAsset>();
        var statuses = new List<BotNavigationAssetStatus>(requestedClasses.Count);

        foreach (var classId in requestedClasses.Distinct())
        {
            if (TryLoadAsset(level, classId, fingerprint, out var asset, out var status))
            {
                assets[classId] = asset!;
            }

            statuses.Add(status);
        }

        return new BotNavigationLoadResult(level.Name, level.MapAreaIndex, fingerprint, assets, statuses);
    }

    public static void SaveShipped(BotNavigationAsset asset, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(asset);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        var outputPath = Path.Combine(outputDirectory, GetAssetFileName(asset));
        WriteAsset(outputPath, asset);
    }

    public static void SaveRuntimeCache(BotNavigationAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var cachePath = GetRuntimeCachePath(asset);
        WriteAsset(cachePath, asset);
    }

    public static string GetAssetFileName(string levelName, int mapAreaIndex, PlayerClass classId)
    {
        return $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.{BotNavigationClasses.GetFileToken(classId)}.botnav.json";
    }

    public static string GetRuntimeCachePath(string levelName, int mapAreaIndex, PlayerClass classId, string levelFingerprint)
    {
        var cacheFileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.{BotNavigationClasses.GetFileToken(classId)}.{TrimFingerprint(levelFingerprint)}.botnav.json";
        return RuntimePaths.GetConfigPath(Path.Combine(RuntimeCacheDirectoryName, cacheFileName));
    }

    public static string? ResolveShippedPath(string levelName, int mapAreaIndex, PlayerClass classId)
    {
        return ResolvePath(GetAssetFileName(levelName, mapAreaIndex, classId));
    }

    public static string GetLegacyAssetFileName(string levelName, int mapAreaIndex, BotNavigationProfile profile)
    {
        return $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.{BotNavigationProfiles.GetFileToken(profile)}.botnav.json";
    }

    public static string GetLegacyRuntimeCachePath(string levelName, int mapAreaIndex, BotNavigationProfile profile, string levelFingerprint)
    {
        var cacheFileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.{BotNavigationProfiles.GetFileToken(profile)}.{TrimFingerprint(levelFingerprint)}.botnav.json";
        return RuntimePaths.GetConfigPath(Path.Combine(RuntimeCacheDirectoryName, cacheFileName));
    }

    public static string? ResolveLegacyProfileShippedPath(string levelName, int mapAreaIndex, BotNavigationProfile profile)
    {
        return ResolvePath(GetLegacyAssetFileName(levelName, mapAreaIndex, profile));
    }

    private static bool TryLoadAsset(
        SimpleLevel level,
        PlayerClass classId,
        string fingerprint,
        out BotNavigationAsset? asset,
        out BotNavigationAssetStatus status)
    {
        var profile = BotNavigationProfiles.GetProfileForClass(classId);
        var classShippedPath = ResolveShippedPath(level.Name, level.MapAreaIndex, classId);
        var classRuntimeCachePath = GetRuntimeCachePath(level.Name, level.MapAreaIndex, classId, fingerprint);
        var legacyShippedPath = ResolveLegacyProfileShippedPath(level.Name, level.MapAreaIndex, profile);
        var legacyRuntimeCachePath = GetLegacyRuntimeCachePath(level.Name, level.MapAreaIndex, profile, fingerprint);
        var candidates = new List<LoadedAssetCandidate>(4);
        var failureMessages = new List<string>();
        var candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        TryLoadCandidate(
            level,
            classId,
            profile,
            fingerprint,
            classShippedPath,
            BotNavigationAssetSource.ShippedContent,
            allowLegacyProfileFallback: false,
            candidates,
            failureMessages,
            candidatePaths);
        TryLoadCandidate(
            level,
            classId,
            profile,
            fingerprint,
            File.Exists(classRuntimeCachePath) ? classRuntimeCachePath : null,
            BotNavigationAssetSource.RuntimeCache,
            allowLegacyProfileFallback: false,
            candidates,
            failureMessages,
            candidatePaths);
        TryLoadCandidate(
            level,
            classId,
            profile,
            fingerprint,
            legacyShippedPath,
            BotNavigationAssetSource.ShippedContent,
            allowLegacyProfileFallback: true,
            candidates,
            failureMessages,
            candidatePaths);
        TryLoadCandidate(
            level,
            classId,
            profile,
            fingerprint,
            File.Exists(legacyRuntimeCachePath) ? legacyRuntimeCachePath : null,
            BotNavigationAssetSource.RuntimeCache,
            allowLegacyProfileFallback: true,
            candidates,
            failureMessages,
            candidatePaths);

        var selectedCandidate = SelectBestCandidate(candidates);
        if (selectedCandidate is not null)
        {
            asset = selectedCandidate.Asset;
            status = new BotNavigationAssetStatus(
                classId,
                profile,
                IsLoaded: true,
                selectedCandidate.Source,
                selectedCandidate.Path,
                selectedCandidate.Message,
                asset!.Nodes.Count,
                asset.Edges.Count,
                selectedCandidate.Validation.IsStructurallyValid,
                selectedCandidate.Validation.BuildSummary());
            return true;
        }

        asset = null;
        status = new BotNavigationAssetStatus(
            classId,
            profile,
            IsLoaded: false,
            BotNavigationAssetSource.None,
            classShippedPath ?? classRuntimeCachePath,
            BuildLoadFailureMessage(failureMessages, classShippedPath, classRuntimeCachePath, legacyShippedPath, legacyRuntimeCachePath),
            NodeCount: 0,
            EdgeCount: 0);
        return false;
    }

    private static void TryLoadCandidate(
        SimpleLevel level,
        PlayerClass classId,
        BotNavigationProfile profile,
        string fingerprint,
        string? path,
        BotNavigationAssetSource source,
        bool allowLegacyProfileFallback,
        List<LoadedAssetCandidate> candidates,
        List<string> failureMessages,
        ISet<string> candidatePaths)
    {
        if (string.IsNullOrWhiteSpace(path) || !candidatePaths.Add(path))
        {
            return;
        }

        if (TryReadAndValidate(path, level, classId, profile, fingerprint, allowLegacyProfileFallback, out var asset, out var message, out var validation))
        {
            candidates.Add(new LoadedAssetCandidate(asset!, source, path, message, validation));
            return;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            failureMessages.Add(message);
        }
    }

    private static bool TryReadAndValidate(
        string path,
        SimpleLevel level,
        PlayerClass classId,
        BotNavigationProfile profile,
        string fingerprint,
        bool allowLegacyProfileFallback,
        out BotNavigationAsset? asset,
        out string message,
        out BotNavigationValidationResult validation)
    {
        asset = null;
        message = string.Empty;
        validation = BotNavigationValidationResult.Valid;

        try
        {
            var json = File.ReadAllText(path);
            asset = JsonSerializer.Deserialize<BotNavigationAsset>(json, SerializerOptions);
            if (asset is null)
            {
                message = "asset could not be deserialized";
                return false;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            message = ex.Message;
            return false;
        }

        if (asset.FormatVersion != CurrentFormatVersion)
        {
            message = $"format mismatch {asset.FormatVersion}";
            asset = null;
            return false;
        }

        if (!string.Equals(asset.LevelName, level.Name, StringComparison.OrdinalIgnoreCase)
            || asset.MapAreaIndex != level.MapAreaIndex
            || asset.Profile != profile)
        {
            message = "asset metadata mismatch";
            asset = null;
            return false;
        }

        if (asset.ClassId.HasValue)
        {
            if (asset.ClassId.Value != classId)
            {
                message = "asset class mismatch";
                asset = null;
                return false;
            }
        }
        else if (!allowLegacyProfileFallback)
        {
            message = "asset class metadata missing";
            asset = null;
            return false;
        }

        if (!string.Equals(asset.LevelFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase))
        {
            message = "level fingerprint mismatch";
            asset = null;
            return false;
        }

        validation = BotNavigationAssetValidator.Validate(level, asset);
        message = BuildSummary(asset, allowLegacyProfileFallback && !asset.ClassId.HasValue);
        return true;
    }

    private static void WriteAsset(string path, BotNavigationAsset asset)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(asset, SerializerOptions);
        File.WriteAllText(path, json);
    }

    private static string GetAssetFileName(BotNavigationAsset asset)
    {
        return asset.ClassId.HasValue
            ? GetAssetFileName(asset.LevelName, asset.MapAreaIndex, asset.ClassId.Value)
            : GetLegacyAssetFileName(asset.LevelName, asset.MapAreaIndex, asset.Profile);
    }

    private static string GetRuntimeCachePath(BotNavigationAsset asset)
    {
        return asset.ClassId.HasValue
            ? GetRuntimeCachePath(asset.LevelName, asset.MapAreaIndex, asset.ClassId.Value, asset.LevelFingerprint)
            : GetLegacyRuntimeCachePath(asset.LevelName, asset.MapAreaIndex, asset.Profile, asset.LevelFingerprint);
    }

    private static string? ResolvePath(string fileName)
    {
        var runtimePath = ContentRoot.GetPath("BotNav", fileName);
        if (File.Exists(runtimePath))
        {
            return runtimePath;
        }

        var projectPath = ProjectSourceLocator.FindFile($"{ShippedRelativeDirectory}/{fileName}");
        if (!string.IsNullOrWhiteSpace(projectPath) && File.Exists(projectPath))
        {
            return projectPath;
        }

        return null;
    }

    private static string BuildSummary(BotNavigationAsset asset, bool usedLegacyProfileFallback)
    {
        var summary = $"asset nodes={asset.Nodes.Count} edges={asset.Edges.Count} strategy={asset.BuildStrategy}";
        return usedLegacyProfileFallback ? $"{summary} legacy-fallback" : summary;
    }

    private static LoadedAssetCandidate? SelectBestCandidate(IReadOnlyList<LoadedAssetCandidate> candidates)
    {
        LoadedAssetCandidate? bestCandidate = null;
        for (var index = 0; index < candidates.Count; index += 1)
        {
            var candidate = candidates[index];
            if (bestCandidate is null
                || candidate.Validation.IsStructurallyValid && !bestCandidate.Validation.IsStructurallyValid
                || candidate.Validation.IsStructurallyValid == bestCandidate.Validation.IsStructurallyValid
                    && candidate.Source < bestCandidate.Source)
            {
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    private static string BuildLoadFailureMessage(
        IReadOnlyList<string> failureMessages,
        string? classShippedPath,
        string classRuntimeCachePath,
        string? legacyShippedPath,
        string legacyRuntimeCachePath)
    {
        if (failureMessages.Count > 0)
        {
            return failureMessages[0];
        }

        if (classShippedPath is null
            && !File.Exists(classRuntimeCachePath)
            && legacyShippedPath is null
            && !File.Exists(legacyRuntimeCachePath))
        {
            return "no shipped asset found";
        }

        return "no compatible nav asset found";
    }

    private static string SanitizeFileToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var sanitized = value.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            sanitized = sanitized.Replace(invalid, '_');
        }

        return sanitized.Replace(' ', '-');
    }

    private static string TrimFingerprint(string fingerprint)
    {
        return string.IsNullOrWhiteSpace(fingerprint)
            ? "unknown"
            : fingerprint[..Math.Min(12, fingerprint.Length)].ToLowerInvariant();
    }

    private sealed record LoadedAssetCandidate(
        BotNavigationAsset Asset,
        BotNavigationAssetSource Source,
        string Path,
        string Message,
        BotNavigationValidationResult Validation);
}
