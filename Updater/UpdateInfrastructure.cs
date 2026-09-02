using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

internal static class UpdateFileNames
{
    public const string PackageManifest = "package-manifest.json";
    public const string DeltaPlan = "delta-plan.json";
    public const string DeltaTargetManifest = "target-package-manifest.json";
    public const string DeltaPayloadDirectory = "payload";
    public const string TransactionDirectory = ".opengarrison-update-transaction";
    public const string InstallationLock = ".opengarrison-update.lock";
}

internal class UpdatePackageDescriptor
{
    public string Url { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long Size { get; set; }
}

internal sealed class UpdateDeltaDescriptor : UpdatePackageDescriptor
{
    public string FromVersion { get; set; } = string.Empty;

    public string ToVersion { get; set; } = string.Empty;

    public string PlanSha256 { get; set; } = string.Empty;

    public string TargetManifestSha256 { get; set; } = string.Empty;

    public string MinLauncherVersion { get; set; } = string.Empty;
}

internal sealed class PackageFileManifest
{
    public int SchemaVersion { get; set; } = 1;

    public string Version { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public List<PackageFileEntry> Files { get; set; } = [];
}

internal sealed class PackageFileEntry
{
    public string Path { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long Size { get; set; }

    public bool Executable { get; set; }
}

internal sealed class DeltaUpdatePlan
{
    public int SchemaVersion { get; set; } = 1;

    public string FromVersion { get; set; } = string.Empty;

    public string ToVersion { get; set; } = string.Empty;

    public string TargetManifestSha256 { get; set; } = string.Empty;

    public List<PackageFileEntry> Files { get; set; } = [];

    public List<string> DeletedFiles { get; set; } = [];
}

internal static class UpdateJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static T ReadRequired<T>(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var value = JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        return value ?? throw new InvalidDataException($"Update metadata '{path}' is empty or invalid.");
    }

    public static void WriteAtomic<T>(string path, T value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, Options));
        File.Move(temporaryPath, path, overwrite: true);
    }
}

internal static class UpdateHash
{
    public static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static void VerifyFile(string path, long expectedSize, string expectedSha256, string description)
    {
        if (!File.Exists(path))
        {
            throw new InvalidDataException($"{description} is missing: '{path}'.");
        }

        var actualSize = new FileInfo(path).Length;
        if (expectedSize >= 0 && actualSize != expectedSize)
        {
            throw new InvalidDataException(
                $"{description} size mismatch for '{path}': expected {expectedSize}, got {actualSize}.");
        }

        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            throw new InvalidDataException($"{description} is missing a SHA-256 digest for '{path}'.");
        }

        var actualSha256 = ComputeSha256(path);
        if (!string.Equals(actualSha256, expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"{description} hash mismatch for '{path}': expected {expectedSha256}, got {actualSha256}.");
        }
    }
}

internal static class UpdatePath
{
    public static string NormalizeRelative(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new InvalidDataException("Update metadata contains an empty path.");
        }

        var normalized = relativePath.Trim().Replace('\\', '/');
        if (normalized[0] == '/'
            || Path.IsPathRooted(normalized)
            || normalized.Contains('\0'))
        {
            throw new InvalidDataException($"Update path must be relative: '{relativePath}'.");
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0
            || segments.Any(static segment => segment is "." or ".."))
        {
            throw new InvalidDataException($"Update path escapes the package root: '{relativePath}'.");
        }

        normalized = string.Join('/', segments);
        if (IsPreservedLocalPath(normalized)
            || normalized.Equals(UpdateFileNames.TransactionDirectory, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(UpdateFileNames.TransactionDirectory + "/", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals(UpdateFileNames.InstallationLock, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"Update path targets protected local data: '{relativePath}'.");
        }

        return normalized;
    }

    public static string ResolveUnderRoot(string rootDirectory, string relativePath)
    {
        var normalized = NormalizeRelative(relativePath);
        var root = Path.GetFullPath(rootDirectory);
        var candidate = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, comparison))
        {
            throw new InvalidDataException($"Update path escapes the package root: '{relativePath}'.");
        }

        return candidate;
    }

    private static bool IsPreservedLocalPath(string normalizedRelativePath)
    {
        var separatorIndex = normalizedRelativePath.IndexOf('/');
        var firstSegment = separatorIndex < 0
            ? normalizedRelativePath
            : normalizedRelativePath[..separatorIndex];
        return firstSegment.Equals("config", StringComparison.OrdinalIgnoreCase)
            || firstSegment.Equals("logs", StringComparison.OrdinalIgnoreCase)
            || firstSegment.Equals("replays", StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class PreparedDeltaPackage
{
    private PreparedDeltaPackage(
        string packageRoot,
        DeltaUpdatePlan plan,
        PackageFileManifest targetManifest,
        string targetManifestPath)
    {
        PackageRoot = packageRoot;
        Plan = plan;
        TargetManifest = targetManifest;
        TargetManifestPath = targetManifestPath;
    }

    public string PackageRoot { get; }

    public DeltaUpdatePlan Plan { get; }

    public PackageFileManifest TargetManifest { get; }

    public string TargetManifestPath { get; }

    public static PreparedDeltaPackage LoadAndValidate(
        string packageRoot,
        string expectedFromVersion,
        string expectedToVersion,
        string expectedPlanSha256 = "",
        string expectedTargetManifestSha256 = "")
    {
        var planPath = Path.Combine(packageRoot, UpdateFileNames.DeltaPlan);
        var targetManifestPath = Path.Combine(packageRoot, UpdateFileNames.DeltaTargetManifest);
        if (!string.IsNullOrWhiteSpace(expectedPlanSha256))
        {
            UpdateHash.VerifyFile(
                planPath,
                expectedSize: -1,
                expectedSha256: expectedPlanSha256,
                description: "Delta plan");
        }

        var plan = UpdateJson.ReadRequired<DeltaUpdatePlan>(planPath);
        var targetManifest = UpdateJson.ReadRequired<PackageFileManifest>(targetManifestPath);
        ValidateVersion("delta base", plan.FromVersion, expectedFromVersion);
        ValidateVersion("delta target", plan.ToVersion, expectedToVersion);
        ValidateVersion("target package", targetManifest.Version, expectedToVersion);
        if (plan.SchemaVersion != 1 || targetManifest.SchemaVersion != 1)
        {
            throw new InvalidDataException("Unsupported delta or package-manifest schema version.");
        }

        var targetManifestSha256 = UpdateHash.ComputeSha256(targetManifestPath);
        var declaredTargetHash = string.IsNullOrWhiteSpace(expectedTargetManifestSha256)
            ? plan.TargetManifestSha256
            : expectedTargetManifestSha256;
        if (string.IsNullOrWhiteSpace(declaredTargetHash)
            || !string.Equals(targetManifestSha256, declaredTargetHash, StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(plan.TargetManifestSha256)
                && !string.Equals(targetManifestSha256, plan.TargetManifestSha256, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidDataException("Delta target package manifest failed SHA-256 verification.");
        }

        ValidateEntries(plan.Files, "delta file");
        ValidateEntries(targetManifest.Files, "target package file");
        var changedPaths = new HashSet<string>(
            plan.Files.Select(static entry => UpdatePath.NormalizeRelative(entry.Path)),
            GetPathComparer());
        var targetPaths = new HashSet<string>(
            targetManifest.Files.Select(static entry => UpdatePath.NormalizeRelative(entry.Path)),
            GetPathComparer());
        foreach (var deletedPath in plan.DeletedFiles)
        {
            var normalized = UpdatePath.NormalizeRelative(deletedPath);
            if (changedPaths.Contains(normalized) || targetPaths.Contains(normalized))
            {
                throw new InvalidDataException($"Delta path is both installed and deleted: '{normalized}'.");
            }
        }

        foreach (var entry in plan.Files)
        {
            var normalized = UpdatePath.NormalizeRelative(entry.Path);
            var targetEntry = targetManifest.Files.FirstOrDefault(candidate =>
                GetPathComparer().Equals(UpdatePath.NormalizeRelative(candidate.Path), normalized));
            if (targetEntry is null
                || targetEntry.Size != entry.Size
                || !string.Equals(targetEntry.Sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase)
                || targetEntry.Executable != entry.Executable)
            {
                throw new InvalidDataException($"Delta file does not match the target package manifest: '{normalized}'.");
            }

            var payloadPath = UpdatePath.ResolveUnderRoot(
                Path.Combine(packageRoot, UpdateFileNames.DeltaPayloadDirectory),
                normalized);
            UpdateHash.VerifyFile(payloadPath, entry.Size, entry.Sha256, "Delta payload");
        }

        return new PreparedDeltaPackage(packageRoot, plan, targetManifest, targetManifestPath);
    }

    public bool CanApplyTo(string installationRoot, out string reason)
    {
        var changedPaths = new HashSet<string>(
            Plan.Files.Select(static entry => UpdatePath.NormalizeRelative(entry.Path)),
            GetPathComparer());
        foreach (var entry in TargetManifest.Files)
        {
            var normalized = UpdatePath.NormalizeRelative(entry.Path);
            if (changedPaths.Contains(normalized))
            {
                continue;
            }

            var installedPath = UpdatePath.ResolveUnderRoot(installationRoot, normalized);
            try
            {
                UpdateHash.VerifyFile(installedPath, entry.Size, entry.Sha256, "Installed base file");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
            {
                reason = ex.Message;
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static void ValidateEntries(IReadOnlyList<PackageFileEntry> entries, string description)
    {
        var paths = new HashSet<string>(GetPathComparer());
        foreach (var entry in entries)
        {
            var normalized = UpdatePath.NormalizeRelative(entry.Path);
            if (!paths.Add(normalized))
            {
                throw new InvalidDataException($"Duplicate {description} path: '{normalized}'.");
            }

            if (entry.Size < 0 || string.IsNullOrWhiteSpace(entry.Sha256))
            {
                throw new InvalidDataException($"Invalid {description} metadata: '{normalized}'.");
            }
        }
    }

    private static void ValidateVersion(string description, string actual, string expected)
    {
        if (string.IsNullOrWhiteSpace(actual)
            || !string.Equals(actual.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Unexpected {description} version: expected '{expected}', got '{actual}'.");
        }
    }

    internal static StringComparer GetPathComparer()
        => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
}

internal static class TransactionalUpdateInstaller
{
    public static void RecoverPendingTransaction(string destinationDirectory)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationRoot);
        using var installationLock = AcquireInstallationLock(destinationRoot);
        RecoverPendingTransactionCore(destinationRoot);
    }

    public static bool HasUncommittedTransaction(string destinationDirectory)
    {
        var transactionRoot = Path.Combine(destinationDirectory, UpdateFileNames.TransactionDirectory);
        if (!Directory.Exists(transactionRoot))
        {
            return false;
        }

        var journalPath = Path.Combine(transactionRoot, "journal.json");
        if (!File.Exists(journalPath))
        {
            return false;
        }

        try
        {
            return !UpdateJson.ReadRequired<UpdateTransactionJournal>(journalPath).Committed;
        }
        catch
        {
            return true;
        }
    }

    private static void RecoverPendingTransactionCore(string destinationDirectory)
    {
        var transactionRoot = Path.Combine(destinationDirectory, UpdateFileNames.TransactionDirectory);
        if (!Directory.Exists(transactionRoot))
        {
            return;
        }

        var journalPath = Path.Combine(transactionRoot, "journal.json");
        if (!File.Exists(journalPath))
        {
            Directory.Delete(transactionRoot, recursive: true);
            return;
        }

        var journal = UpdateJson.ReadRequired<UpdateTransactionJournal>(journalPath);
        if (journal.Committed)
        {
            Directory.Delete(transactionRoot, recursive: true);
            return;
        }

        RollBack(destinationDirectory, transactionRoot, journal);
        Directory.Delete(transactionRoot, recursive: true);
    }

    public static void ApplyDelta(
        PreparedDeltaPackage package,
        string destinationDirectory,
        Action<double>? reportProgress = null,
        Action<string>? beforeInstallForTesting = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        Apply(
            destinationDirectory,
            package.Plan.Files.Select(entry => new UpdateInstallFile(
                entry,
                UpdatePath.ResolveUnderRoot(
                    Path.Combine(package.PackageRoot, UpdateFileNames.DeltaPayloadDirectory),
                    entry.Path))).Append(new UpdateInstallFile(
                        new PackageFileEntry
                        {
                            Path = UpdateFileNames.PackageManifest,
                            Size = new FileInfo(package.TargetManifestPath).Length,
                            Sha256 = UpdateHash.ComputeSha256(package.TargetManifestPath),
                            Executable = false,
                        },
                        package.TargetManifestPath)),
            package.Plan.DeletedFiles,
            reportProgress,
            beforeInstallForTesting);
    }

    public static bool TryApplyFullPackage(
        string sourceDirectory,
        string destinationDirectory,
        Action<double>? reportProgress = null,
        Action<string>? beforeInstallForTesting = null,
        string? expectedVersion = null)
    {
        var packageManifestPath = Path.Combine(sourceDirectory, UpdateFileNames.PackageManifest);
        if (!File.Exists(packageManifestPath))
        {
            return false;
        }

        var targetManifest = UpdateJson.ReadRequired<PackageFileManifest>(packageManifestPath);
        if (targetManifest.SchemaVersion != 1)
        {
            throw new InvalidDataException(
                $"Unsupported package-manifest schema version {targetManifest.SchemaVersion}.");
        }

        if (!string.IsNullOrWhiteSpace(expectedVersion)
            && !string.Equals(
                targetManifest.Version.Trim(),
                expectedVersion.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Full package version mismatch: expected '{expectedVersion}', got '{targetManifest.Version}'.");
        }

        var installFiles = new List<UpdateInstallFile>(targetManifest.Files.Count + 1);
        foreach (var entry in targetManifest.Files)
        {
            var sourcePath = UpdatePath.ResolveUnderRoot(sourceDirectory, entry.Path);
            UpdateHash.VerifyFile(sourcePath, entry.Size, entry.Sha256, "Full update payload");
            installFiles.Add(new UpdateInstallFile(entry, sourcePath));
        }

        installFiles.Add(new UpdateInstallFile(
            new PackageFileEntry
            {
                Path = UpdateFileNames.PackageManifest,
                Size = new FileInfo(packageManifestPath).Length,
                Sha256 = UpdateHash.ComputeSha256(packageManifestPath),
                Executable = false,
            },
            packageManifestPath));

        var deletedFiles = ResolveRemovedPackageFiles(destinationDirectory, targetManifest);
        Apply(
            destinationDirectory,
            installFiles,
            deletedFiles,
            reportProgress,
            beforeInstallForTesting);
        return true;
    }

    public static void ApplyLegacyFullPackage(string destinationDirectory, Action apply)
    {
        ArgumentNullException.ThrowIfNull(apply);
        var destinationRoot = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationRoot);
        using var installationLock = AcquireInstallationLock(destinationRoot);
        RecoverPendingTransactionCore(destinationRoot);
        apply();
    }

    private static string[] ResolveRemovedPackageFiles(
        string destinationDirectory,
        PackageFileManifest targetManifest)
    {
        var installedManifestPath = Path.Combine(destinationDirectory, UpdateFileNames.PackageManifest);
        if (!File.Exists(installedManifestPath))
        {
            return [];
        }

        try
        {
            var installedManifest = UpdateJson.ReadRequired<PackageFileManifest>(installedManifestPath);
            var targetPaths = new HashSet<string>(
                targetManifest.Files.Select(static entry => UpdatePath.NormalizeRelative(entry.Path)),
                PreparedDeltaPackage.GetPathComparer());
            return installedManifest.Files
                .Select(static entry => UpdatePath.NormalizeRelative(entry.Path))
                .Where(path => !targetPaths.Contains(path))
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return [];
        }
    }

    private static void Apply(
        string destinationDirectory,
        IEnumerable<UpdateInstallFile> installFiles,
        IEnumerable<string> deletedFiles,
        Action<double>? reportProgress,
        Action<string>? beforeInstallForTesting)
    {
        var destinationRoot = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(destinationRoot);
        using var installationLock = AcquireInstallationLock(destinationRoot);
        RecoverPendingTransactionCore(destinationRoot);

        var pathComparer = PreparedDeltaPackage.GetPathComparer();
        var files = installFiles
            .Select(file => new UpdateInstallFile(
                new PackageFileEntry
                {
                    Path = UpdatePath.NormalizeRelative(file.Entry.Path),
                    Sha256 = file.Entry.Sha256,
                    Size = file.Entry.Size,
                    Executable = file.Entry.Executable,
                },
                file.SourcePath))
            .OrderBy(static file => GetInstallOrder(file.Entry.Path))
            .ThenBy(static file => file.Entry.Path, StringComparer.Ordinal)
            .ToArray();
        var installsByPath = new HashSet<string>(files.Select(static file => file.Entry.Path), pathComparer);
        if (installsByPath.Count != files.Length)
        {
            throw new InvalidDataException("Update contains duplicate install paths.");
        }

        var deletes = deletedFiles
            .Select(UpdatePath.NormalizeRelative)
            .Distinct(pathComparer)
            .Where(path => !installsByPath.Contains(path))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        var transactionRoot = Path.Combine(destinationRoot, UpdateFileNames.TransactionDirectory);
        var stagingRoot = Path.Combine(transactionRoot, "staging");
        var backupRoot = Path.Combine(transactionRoot, "backup");
        Directory.CreateDirectory(stagingRoot);
        Directory.CreateDirectory(backupRoot);

        var journal = new UpdateTransactionJournal();
        foreach (var file in files)
        {
            UpdateHash.VerifyFile(file.SourcePath, file.Entry.Size, file.Entry.Sha256, "Update source");
            var stagedPath = UpdatePath.ResolveUnderRoot(stagingRoot, file.Entry.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(stagedPath) ?? stagingRoot);
            File.Copy(file.SourcePath, stagedPath, overwrite: true);
            UpdateHash.VerifyFile(stagedPath, file.Entry.Size, file.Entry.Sha256, "Staged update file");
        }

        foreach (var path in deletes)
        {
            journal.Entries.Add(new UpdateTransactionEntry
            {
                Path = path,
                Install = false,
                HadOriginal = File.Exists(UpdatePath.ResolveUnderRoot(destinationRoot, path)),
            });
        }

        foreach (var file in files)
        {
            journal.Entries.Add(new UpdateTransactionEntry
            {
                Path = file.Entry.Path,
                Install = true,
                Executable = file.Entry.Executable,
                HadOriginal = File.Exists(UpdatePath.ResolveUnderRoot(destinationRoot, file.Entry.Path)),
            });
        }

        var journalPath = Path.Combine(transactionRoot, "journal.json");
        UpdateJson.WriteAtomic(journalPath, journal);

        try
        {
            for (var index = 0; index < journal.Entries.Count; index += 1)
            {
                var entry = journal.Entries[index];
                var destinationPath = UpdatePath.ResolveUnderRoot(destinationRoot, entry.Path);
                var backupPath = UpdatePath.ResolveUnderRoot(backupRoot, entry.Path);
                beforeInstallForTesting?.Invoke(entry.Path);

                if (File.Exists(destinationPath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(backupPath) ?? backupRoot);
                    File.Move(destinationPath, backupPath, overwrite: false);
                    entry.BackedUp = true;
                    UpdateJson.WriteAtomic(journalPath, journal);
                }

                if (entry.Install)
                {
                    var stagedPath = UpdatePath.ResolveUnderRoot(stagingRoot, entry.Path);
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationRoot);
                    File.Move(stagedPath, destinationPath, overwrite: false);
                    if (entry.Executable)
                    {
                        EnsureExecutable(destinationPath);
                    }

                    entry.Installed = true;
                    UpdateJson.WriteAtomic(journalPath, journal);
                }

                reportProgress?.Invoke((index + 1) / (double)Math.Max(1, journal.Entries.Count));
            }

            journal.Committed = true;
            UpdateJson.WriteAtomic(journalPath, journal);
        }
        catch
        {
            try
            {
                RollBack(destinationRoot, transactionRoot, journal);
                Directory.Delete(transactionRoot, recursive: true);
            }
            catch
            {
                // Preserve the journal and backups for recovery on the next updater launch.
            }

            throw;
        }

        try
        {
            Directory.Delete(transactionRoot, recursive: true);
        }
        catch
        {
            // The committed journal makes leftover transaction data safe to clean up
            // on the next launch. A transient cleanup failure must not undo an update.
        }

        RemoveEmptyDeletedDirectories(destinationRoot, deletes);
    }

    private static FileStream AcquireInstallationLock(string destinationRoot)
    {
        var lockPath = Path.Combine(destinationRoot, UpdateFileNames.InstallationLock);
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            try
            {
                return new FileStream(
                    lockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.None);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(100);
            }
        }
    }

    private static void RollBack(
        string destinationRoot,
        string transactionRoot,
        UpdateTransactionJournal journal)
    {
        var backupRoot = Path.Combine(transactionRoot, "backup");
        for (var index = journal.Entries.Count - 1; index >= 0; index -= 1)
        {
            var entry = journal.Entries[index];
            var destinationPath = UpdatePath.ResolveUnderRoot(destinationRoot, entry.Path);
            var backupPath = UpdatePath.ResolveUnderRoot(backupRoot, entry.Path);
            if (File.Exists(destinationPath)
                && (entry.Installed || !entry.HadOriginal || File.Exists(backupPath)))
            {
                File.Delete(destinationPath);
            }

            if (File.Exists(backupPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? destinationRoot);
                File.Move(backupPath, destinationPath, overwrite: false);
            }
        }
    }

    private static int GetInstallOrder(string relativePath)
    {
        if (relativePath.Equals(UpdateFileNames.PackageManifest, StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        if (relativePath.Equals("version.txt", StringComparison.OrdinalIgnoreCase))
        {
            return 3;
        }

        return 1;
    }

    private static void EnsureExecutable(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = File.GetUnixFileMode(path);
        File.SetUnixFileMode(
            path,
            mode
            | UnixFileMode.UserExecute
            | UnixFileMode.GroupExecute
            | UnixFileMode.OtherExecute);
    }

    private static void RemoveEmptyDeletedDirectories(string destinationRoot, IReadOnlyList<string> deletedFiles)
    {
        foreach (var directory in deletedFiles
                     .Select(path => Path.GetDirectoryName(UpdatePath.ResolveUnderRoot(destinationRoot, path)))
                     .Where(static path => !string.IsNullOrWhiteSpace(path))
                     .Distinct(PreparedDeltaPackage.GetPathComparer())
                     .OrderByDescending(static path => path!.Length))
        {
            try
            {
                if (directory is not null
                    && Directory.Exists(directory)
                    && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                }
            }
            catch
            {
                // Empty directory cleanup is best-effort only.
            }
        }
    }

    private sealed class UpdateTransactionJournal
    {
        public bool Committed { get; set; }

        public List<UpdateTransactionEntry> Entries { get; set; } = [];
    }

    private sealed class UpdateTransactionEntry
    {
        public string Path { get; set; } = string.Empty;

        public bool Install { get; set; }

        public bool Executable { get; set; }

        public bool HadOriginal { get; set; }

        public bool BackedUp { get; set; }

        public bool Installed { get; set; }
    }

    private sealed record UpdateInstallFile(PackageFileEntry Entry, string SourcePath);
}
