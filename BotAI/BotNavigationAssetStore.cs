using OpenGarrison.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.BotAI;

public static class BotNavigationAssetStore
{
    public const int CurrentFormatVersion = 3;
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

    public static BotNavigationLoadResult LoadForLevel(
        SimpleLevel level,
        IReadOnlyList<PlayerClass>? classes = null,
        bool useModernRuntimeGeneration = true,
        bool allowSynchronousGeneration = true,
        bool preferFreshModernGeneration = false)
    {
        ArgumentNullException.ThrowIfNull(level);

        var requestedClasses = (classes ?? BotNavigationClasses.All)
            .Distinct()
            .ToArray();
        var fingerprint = BotNavigationLevelFingerprint.Compute(level);
        if (useModernRuntimeGeneration)
        {
            return LoadModernAssets(level, requestedClasses, fingerprint, allowSynchronousGeneration, preferFreshModernGeneration);
        }

        var assets = new Dictionary<PlayerClass, BotNavigationAsset>();
        var statuses = new List<BotNavigationAssetStatus>(requestedClasses.Length);

        foreach (var classId in requestedClasses)
        {
            if (TryLoadAsset(level, classId, fingerprint, preferRuntimeCache: false, out var asset, out var status))
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
        foreach (var candidateLevelName in EnumerateShippedLevelNameCandidates(levelName))
        {
            var path = ResolvePath(GetAssetFileName(candidateLevelName, mapAreaIndex, classId));
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }

        return null;
    }

    public static string GetLegacyAssetFileName(string levelName, int mapAreaIndex, BotNavigationProfile profile)
    {
        return $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.{BotNavigationProfiles.GetFileToken(profile)}.botnav.json";
    }

    public static string GetModernAssetFileName(string levelName, int mapAreaIndex)
    {
        return $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.modern.botnav.json";
    }

    public static string GetLegacyRuntimeCachePath(string levelName, int mapAreaIndex, BotNavigationProfile profile, string levelFingerprint)
    {
        var cacheFileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.{BotNavigationProfiles.GetFileToken(profile)}.{TrimFingerprint(levelFingerprint)}.botnav.json";
        return RuntimePaths.GetConfigPath(Path.Combine(RuntimeCacheDirectoryName, cacheFileName));
    }

    public static string GetModernRuntimeCachePath(string levelName, int mapAreaIndex, string levelFingerprint)
    {
        var cacheFileName = $"{SanitizeFileToken(levelName)}.a{Math.Max(1, mapAreaIndex)}.modern.{TrimFingerprint(levelFingerprint)}.botnav.json";
        return RuntimePaths.GetConfigPath(Path.Combine(RuntimeCacheDirectoryName, cacheFileName));
    }

    public static string? ResolveLegacyProfileShippedPath(string levelName, int mapAreaIndex, BotNavigationProfile profile)
    {
        foreach (var candidateLevelName in EnumerateShippedLevelNameCandidates(levelName))
        {
            var path = ResolvePath(GetLegacyAssetFileName(candidateLevelName, mapAreaIndex, profile));
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }

        return null;
    }

    public static string? ResolveModernShippedPath(string levelName, int mapAreaIndex)
    {
        foreach (var candidateLevelName in EnumerateShippedLevelNameCandidates(levelName))
        {
            var path = ResolvePath(GetModernAssetFileName(candidateLevelName, mapAreaIndex));
            if (!string.IsNullOrWhiteSpace(path))
            {
                return path;
            }
        }

        return null;
    }

    private static BotNavigationLoadResult LoadModernAssets(
        SimpleLevel level,
        PlayerClass[] requestedClasses,
        string fingerprint,
        bool allowSynchronousGeneration,
        bool preferFreshModernGeneration)
    {
        var generatedAsset = LoadOrBuildModernAsset(level, fingerprint, allowSynchronousGeneration, preferFreshModernGeneration);
        var assets = new Dictionary<PlayerClass, BotNavigationAsset>();
        var statuses = new List<BotNavigationAssetStatus>(requestedClasses.Length);

        foreach (var classId in requestedClasses)
        {
            var profile = BotNavigationProfiles.GetProfileForClass(classId);
            if (generatedAsset.Candidate is not null)
            {
                var candidate = generatedAsset.Candidate;
                assets[classId] = candidate.Asset;
                statuses.Add(new BotNavigationAssetStatus(
                    classId,
                    profile,
                    IsLoaded: true,
                    candidate.Source,
                    candidate.Path,
                    candidate.Message,
                    candidate.Asset.Nodes.Count,
                    candidate.Asset.Edges.Count,
                    candidate.Validation.IsStructurallyValid,
                    candidate.Validation.BuildSummary()));
                continue;
            }

            statuses.Add(new BotNavigationAssetStatus(
                classId,
                profile,
                IsLoaded: false,
                BotNavigationAssetSource.None,
                GetModernRuntimeCachePath(level.Name, level.MapAreaIndex, fingerprint),
                string.IsNullOrWhiteSpace(generatedAsset.FailureMessage)
                    ? "modern nav generation failed"
                    : generatedAsset.FailureMessage!,
                NodeCount: 0,
                EdgeCount: 0));
        }

        return new BotNavigationLoadResult(level.Name, level.MapAreaIndex, fingerprint, assets, statuses);
    }

    private static bool TryLoadAsset(
        SimpleLevel level,
        PlayerClass classId,
        string fingerprint,
        bool preferRuntimeCache,
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

        if (preferRuntimeCache)
        {
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
                File.Exists(legacyRuntimeCachePath) ? legacyRuntimeCachePath : null,
                BotNavigationAssetSource.RuntimeCache,
                allowLegacyProfileFallback: true,
                candidates,
                failureMessages,
                candidatePaths);
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
                legacyShippedPath,
                BotNavigationAssetSource.ShippedContent,
                allowLegacyProfileFallback: true,
                candidates,
                failureMessages,
                candidatePaths);
        }
        else
        {
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
        }

        var selectedCandidate = SelectBestCandidate(candidates, preferRuntimeCache);
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
        HashSet<string> candidatePaths)
    {
        var candidateKey = $"{path}|legacy={allowLegacyProfileFallback}";
        if (string.IsNullOrWhiteSpace(path) || !candidatePaths.Add(candidateKey))
        {
            return;
        }

        if (TryReadAndValidate(
                path,
                level,
                classId,
                profile,
                fingerprint,
                allowLegacyProfileFallback,
                allowFingerprintMismatch: false,
                out var asset,
                out var message,
                out var validation))
        {
            candidates.Add(new LoadedAssetCandidate(asset!, source, path, message, validation));
            return;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            failureMessages.Add(message);
        }
    }

    private static ModernAssetLoadResult LoadOrBuildModernAsset(
        SimpleLevel level,
        string fingerprint,
        bool allowSynchronousGeneration,
        bool preferFreshModernGeneration)
    {
        var cachePath = GetModernRuntimeCachePath(level.Name, level.MapAreaIndex, fingerprint);
        var cacheFailure = string.Empty;
        var shippedFailure = string.Empty;
        if (!preferFreshModernGeneration
            && TryLoadModernCandidate(cachePath, level, fingerprint, BotNavigationAssetSource.RuntimeCache, out var cachedCandidate, out cacheFailure))
        {
            return new ModernAssetLoadResult(cachedCandidate, string.Empty);
        }

        var shippedPath = ResolveModernShippedPath(level.Name, level.MapAreaIndex);
        if (!preferFreshModernGeneration
            && TryLoadModernCandidate(shippedPath, level, fingerprint, BotNavigationAssetSource.ShippedContent, out var shippedCandidate, out shippedFailure))
        {
            return new ModernAssetLoadResult(shippedCandidate, string.Empty);
        }

        var buildFailure = string.Empty;
        if (allowSynchronousGeneration)
        {
            try
            {
                var asset = BotNavigationModernPointGraphBuilder.Build(level, fingerprint);
                var validation = BotNavigationAssetValidator.Validate(level, asset);
                TryWriteRuntimeCache(asset);
                return new ModernAssetLoadResult(
                    new LoadedAssetCandidate(
                        asset,
                        BotNavigationAssetSource.GeneratedAtRuntime,
                        cachePath,
                        $"{BuildSummary(asset, usedLegacyProfileFallback: false)} generated",
                        validation),
                    string.Empty);
            }
            catch (Exception ex)
            {
                buildFailure = $"modern nav build failed: {ex.Message}";
                if (!string.IsNullOrWhiteSpace(cacheFailure))
                {
                    buildFailure = $"{buildFailure} ({cacheFailure})";
                }
            }
        }

        if (preferFreshModernGeneration
            && TryLoadModernCandidate(cachePath, level, fingerprint, BotNavigationAssetSource.RuntimeCache, out var fallbackCachedCandidate, out cacheFailure))
        {
            return new ModernAssetLoadResult(fallbackCachedCandidate, string.Empty);
        }

        if (TryLoadModernCandidate(shippedPath, level, fingerprint, BotNavigationAssetSource.ShippedContent, out var fallbackShippedCandidate, out shippedFailure))
        {
            return new ModernAssetLoadResult(fallbackShippedCandidate, string.Empty);
        }

        if (!allowSynchronousGeneration)
        {
            var failure = !string.IsNullOrWhiteSpace(shippedFailure)
                ? shippedFailure
                : cacheFailure;
            return new ModernAssetLoadResult(
                null,
                string.IsNullOrWhiteSpace(failure)
                    ? "modern nav unavailable without synchronous generation"
                    : failure);
        }

        var finalFailure = !string.IsNullOrWhiteSpace(buildFailure)
            ? buildFailure
            : !string.IsNullOrWhiteSpace(shippedFailure)
                ? shippedFailure
                : cacheFailure;
        return new ModernAssetLoadResult(null, finalFailure);
    }

    private static bool TryLoadModernCandidate(
        string? path,
        SimpleLevel level,
        string fingerprint,
        BotNavigationAssetSource source,
        out LoadedAssetCandidate? candidate,
        out string failureMessage)
    {
        candidate = null;
        failureMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        if (!TryReadAndValidateModernAsset(
                path,
                level,
                fingerprint,
                allowFingerprintMismatch: false,
                out var asset,
                out var message,
                out var validation))
        {
            if (source == BotNavigationAssetSource.ShippedContent
                && message == "modern nav asset fingerprint mismatch"
                && TryReadAndValidateModernAsset(
                    path,
                    level,
                    fingerprint,
                    allowFingerprintMismatch: true,
                    out asset,
                    out message,
                    out validation))
            {
                candidate = new LoadedAssetCandidate(
                    asset!,
                    source,
                    path,
                    $"{BuildSummary(asset!, usedLegacyProfileFallback: false)} shipped compat-fingerprint",
                    validation);
                return true;
            }

            failureMessage = message;
            return false;
        }

        candidate = new LoadedAssetCandidate(
            asset!,
            source,
            path,
            source == BotNavigationAssetSource.ShippedContent
                ? $"{BuildSummary(asset!, usedLegacyProfileFallback: false)} shipped"
                : $"{BuildSummary(asset!, usedLegacyProfileFallback: false)} cached",
            validation);
        return true;
    }

    private static bool TryReadAndValidateModernAsset(
        string path,
        SimpleLevel level,
        string fingerprint,
        bool allowFingerprintMismatch,
        out BotNavigationAsset? asset,
        out string message,
        out BotNavigationValidationResult validation)
    {
        asset = null;
        message = string.Empty;
        validation = BotNavigationValidationResult.Valid;

        if (!TryReadAsset(path, out asset))
        {
            message = "modern nav asset could not be deserialized";
            return false;
        }

        if (asset is null)
        {
            message = "modern nav asset could not be deserialized";
            return false;
        }

        if (asset.FormatVersion != CurrentFormatVersion)
        {
            message = $"modern nav asset format mismatch {asset.FormatVersion}";
            asset = null;
            return false;
        }

        if (!string.Equals(asset.LevelName, level.Name, StringComparison.OrdinalIgnoreCase)
            || asset.MapAreaIndex != level.MapAreaIndex)
        {
            message = "modern nav asset metadata mismatch";
            asset = null;
            return false;
        }

        if (asset.ClassId.HasValue)
        {
            message = "modern nav asset must be profile-scoped";
            asset = null;
            return false;
        }

        if (!string.Equals(asset.LevelFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) && !allowFingerprintMismatch)
        {
            message = "modern nav asset fingerprint mismatch";
            asset = null;
            return false;
        }

        if (asset.BuildStrategy != BotNavigationBuildStrategy.ModernClientBotPointGraph)
        {
            message = $"modern nav asset strategy mismatch {asset.BuildStrategy}";
            asset = null;
            return false;
        }

        validation = BotNavigationAssetValidator.Validate(level, asset);
        if (!validation.IsStructurallyValid)
        {
            message = $"modern nav asset invalid: {validation.BuildSummary()}";
            asset = null;
            return false;
        }

        return true;
    }

    private static void TryWriteRuntimeCache(BotNavigationAsset asset)
    {
        try
        {
            SaveRuntimeCache(asset);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool TryReadAndValidate(
        string path,
        SimpleLevel level,
        PlayerClass classId,
        BotNavigationProfile profile,
        string fingerprint,
        bool allowLegacyProfileFallback,
        bool allowFingerprintMismatch,
        out BotNavigationAsset? asset,
        out string message,
        out BotNavigationValidationResult validation)
    {
        asset = null;
        message = string.Empty;
        validation = BotNavigationValidationResult.Valid;

        if (!TryReadAsset(path, out asset))
        {
            message = "asset could not be deserialized";
            return false;
        }

        if (asset is null)
        {
            message = "asset could not be deserialized";
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

        if (!string.Equals(asset.LevelFingerprint, fingerprint, StringComparison.OrdinalIgnoreCase) && !allowFingerprintMismatch)
        {
            message = "level fingerprint mismatch";
            asset = null;
            return false;
        }

        validation = BotNavigationAssetValidator.Validate(level, asset);
        message = BuildSummary(asset, allowLegacyProfileFallback && !asset.ClassId.HasValue);
        return true;
    }

    private static bool TryReadAsset(string path, out BotNavigationAsset? asset)
    {
        asset = null;
        try
        {
            var json = File.ReadAllText(path);
            asset = JsonSerializer.Deserialize<BotNavigationAsset>(json, SerializerOptions);
            return asset is not null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return false;
        }
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
            : asset.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph
                ? GetModernAssetFileName(asset.LevelName, asset.MapAreaIndex)
                : GetLegacyAssetFileName(asset.LevelName, asset.MapAreaIndex, asset.Profile);
    }

    private static string GetRuntimeCachePath(BotNavigationAsset asset)
    {
        return asset.ClassId.HasValue
            ? GetRuntimeCachePath(asset.LevelName, asset.MapAreaIndex, asset.ClassId.Value, asset.LevelFingerprint)
            : asset.BuildStrategy == BotNavigationBuildStrategy.ModernClientBotPointGraph
                ? GetModernRuntimeCachePath(asset.LevelName, asset.MapAreaIndex, asset.LevelFingerprint)
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

    private static IEnumerable<string> EnumerateShippedLevelNameCandidates(string levelName)
    {
        var yielded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(levelName) && yielded.Add(levelName))
        {
            yield return levelName;
        }

        if (!OpenGarrisonStockMapCatalog.TryGetDefinition(levelName, out var definition))
        {
            yield break;
        }

        if (yielded.Add(definition.LevelName))
        {
            yield return definition.LevelName;
        }

        for (var aliasIndex = 0; aliasIndex < definition.Aliases.Length; aliasIndex += 1)
        {
            var alias = definition.Aliases[aliasIndex];
            if (!string.IsNullOrWhiteSpace(alias) && yielded.Add(alias))
            {
                yield return alias;
            }
        }
    }

    private static string BuildSummary(BotNavigationAsset asset, bool usedLegacyProfileFallback)
    {
        var summary = $"asset nodes={asset.Nodes.Count} edges={asset.Edges.Count} strategy={asset.BuildStrategy}";
        return usedLegacyProfileFallback ? $"{summary} legacy-fallback" : summary;
    }

    private static LoadedAssetCandidate? SelectBestCandidate(IReadOnlyList<LoadedAssetCandidate> candidates, bool preferRuntimeCache)
    {
        LoadedAssetCandidate? bestCandidate = null;
        for (var index = 0; index < candidates.Count; index += 1)
        {
            var candidate = candidates[index];
            if (bestCandidate is null
                || candidate.Validation.IsStructurallyValid && !bestCandidate.Validation.IsStructurallyValid
                || candidate.Validation.IsStructurallyValid == bestCandidate.Validation.IsStructurallyValid
                    && GetSourcePreferenceRank(candidate.Source, preferRuntimeCache) < GetSourcePreferenceRank(bestCandidate.Source, preferRuntimeCache))
            {
                bestCandidate = candidate;
            }
        }

        return bestCandidate;
    }

    private static int GetSourcePreferenceRank(BotNavigationAssetSource source, bool preferRuntimeCache)
    {
        return preferRuntimeCache
            ? source switch
            {
                BotNavigationAssetSource.GeneratedAtRuntime => 0,
                BotNavigationAssetSource.RuntimeCache => 1,
                BotNavigationAssetSource.ShippedContent => 2,
                _ => 3,
            }
            : source switch
            {
                BotNavigationAssetSource.RuntimeCache => 0,
                BotNavigationAssetSource.GeneratedAtRuntime => 1,
                BotNavigationAssetSource.ShippedContent => 2,
                _ => 3,
            };
    }

    private static string BuildLoadFailureMessage(
        List<string> failureMessages,
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

    private sealed record ModernAssetLoadResult(
        LoadedAssetCandidate? Candidate,
        string FailureMessage);
}
