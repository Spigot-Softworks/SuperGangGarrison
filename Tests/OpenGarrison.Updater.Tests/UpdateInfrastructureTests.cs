using Xunit;

namespace OpenGarrison.Updater.Tests;

public sealed class UpdateInfrastructureTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "OpenGarrison.Updater.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void DeltaPreflightRequiresEveryUnchangedTargetFileToMatch()
    {
        var fixture = CreateDeltaFixture();
        var package = PreparedDeltaPackage.LoadAndValidate(
            fixture.DeltaRoot,
            "1.0.0",
            "1.1.0");

        Assert.True(package.CanApplyTo(fixture.InstallRoot, out var acceptedReason), acceptedReason);

        File.WriteAllText(Path.Combine(fixture.InstallRoot, "app", "unchanged.txt"), "locally modified");

        Assert.False(package.CanApplyTo(fixture.InstallRoot, out var rejectedReason));
        Assert.Contains("mismatch", rejectedReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeltaApplyChangesAddsDeletesAndPublishesTargetManifest()
    {
        var fixture = CreateDeltaFixture();
        var package = PreparedDeltaPackage.LoadAndValidate(
            fixture.DeltaRoot,
            "1.0.0",
            "1.1.0");

        TransactionalUpdateInstaller.ApplyDelta(package, fixture.InstallRoot);

        Assert.Equal("same", File.ReadAllText(Path.Combine(fixture.InstallRoot, "app", "unchanged.txt")));
        Assert.Equal("new changed", File.ReadAllText(Path.Combine(fixture.InstallRoot, "app", "changed.txt")));
        Assert.Equal("new file", File.ReadAllText(Path.Combine(fixture.InstallRoot, "app", "new.txt")));
        Assert.False(File.Exists(Path.Combine(fixture.InstallRoot, "app", "removed.txt")));
        Assert.Equal("1.1.0", File.ReadAllText(Path.Combine(fixture.InstallRoot, "version.txt")));
        var installedManifest = UpdateJson.ReadRequired<PackageFileManifest>(
            Path.Combine(fixture.InstallRoot, UpdateFileNames.PackageManifest));
        Assert.Equal("1.1.0", installedManifest.Version);
        Assert.False(Directory.Exists(Path.Combine(
            fixture.InstallRoot,
            UpdateFileNames.TransactionDirectory)));
    }

    [Fact]
    public void DeltaApplyRollsBackEveryTouchedFileWhenInstallFails()
    {
        var fixture = CreateDeltaFixture();
        var package = PreparedDeltaPackage.LoadAndValidate(
            fixture.DeltaRoot,
            "1.0.0",
            "1.1.0");

        Assert.Throws<InjectedUpdateFailure>(() => TransactionalUpdateInstaller.ApplyDelta(
            package,
            fixture.InstallRoot,
            beforeInstallForTesting: path =>
            {
                if (path.Equals("app/new.txt", StringComparison.Ordinal))
                {
                    throw new InjectedUpdateFailure();
                }
            }));

        Assert.Equal("old changed", File.ReadAllText(Path.Combine(fixture.InstallRoot, "app", "changed.txt")));
        Assert.Equal("remove me", File.ReadAllText(Path.Combine(fixture.InstallRoot, "app", "removed.txt")));
        Assert.False(File.Exists(Path.Combine(fixture.InstallRoot, "app", "new.txt")));
        Assert.Equal("1.0.0", File.ReadAllText(Path.Combine(fixture.InstallRoot, "version.txt")));
        Assert.False(Directory.Exists(Path.Combine(
            fixture.InstallRoot,
            UpdateFileNames.TransactionDirectory)));
    }

    [Fact]
    public void RecoveryRestoresBackupsLeftByInterruptedTransaction()
    {
        var installRoot = Path.Combine(_root, "recover-install");
        var transactionRoot = Path.Combine(installRoot, UpdateFileNames.TransactionDirectory);
        var backupPath = Path.Combine(transactionRoot, "backup", "app", "game.dll");
        var destinationPath = Path.Combine(installRoot, "app", "game.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.WriteAllText(backupPath, "old");
        File.WriteAllText(destinationPath, "partial new");
        File.WriteAllText(
            Path.Combine(transactionRoot, "journal.json"),
            """
            {
              "committed": false,
              "entries": [
                {
                  "path": "app/game.dll",
                  "install": true,
                  "executable": false,
                  "hadOriginal": true,
                  "backedUp": true,
                  "installed": true
                }
              ]
            }
            """);

        TransactionalUpdateInstaller.RecoverPendingTransaction(installRoot);

        Assert.Equal("old", File.ReadAllText(destinationPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public void RecoveryRemovesANewFileMovedBeforeItsJournalFlagWasPersisted()
    {
        var installRoot = Path.Combine(_root, "recover-new-file-install");
        var transactionRoot = Path.Combine(installRoot, UpdateFileNames.TransactionDirectory);
        var destinationPath = Path.Combine(installRoot, "app", "new.dll");
        Directory.CreateDirectory(transactionRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.WriteAllText(destinationPath, "partial new");
        File.WriteAllText(
            Path.Combine(transactionRoot, "journal.json"),
            """
            {
              "committed": false,
              "entries": [
                {
                  "path": "app/new.dll",
                  "install": true,
                  "executable": false,
                  "hadOriginal": false,
                  "backedUp": false,
                  "installed": false
                }
              ]
            }
            """);

        TransactionalUpdateInstaller.RecoverPendingTransaction(installRoot);

        Assert.False(File.Exists(destinationPath));
        Assert.False(Directory.Exists(transactionRoot));
    }

    [Fact]
    public void FullPackageApplyRemovesOnlyPreviouslyOwnedFiles()
    {
        var sourceRoot = Path.Combine(_root, "full-source");
        var installRoot = Path.Combine(_root, "full-install");
        WriteFile(sourceRoot, "app/game.dll", "target");
        WriteFile(sourceRoot, "version.txt", "2.0.0");
        WriteFile(installRoot, "app/game.dll", "old");
        WriteFile(installRoot, "app/removed.dll", "obsolete");
        WriteFile(installRoot, "app/custom-map.txt", "user content");

        var oldManifest = CreateManifest("1.0.0", installRoot, ["app/game.dll", "app/removed.dll"]);
        UpdateJson.WriteAtomic(Path.Combine(installRoot, UpdateFileNames.PackageManifest), oldManifest);
        var targetManifest = CreateManifest("2.0.0", sourceRoot, ["app/game.dll", "version.txt"]);
        UpdateJson.WriteAtomic(Path.Combine(sourceRoot, UpdateFileNames.PackageManifest), targetManifest);

        Assert.True(TransactionalUpdateInstaller.TryApplyFullPackage(sourceRoot, installRoot));

        Assert.Equal("target", File.ReadAllText(Path.Combine(installRoot, "app", "game.dll")));
        Assert.False(File.Exists(Path.Combine(installRoot, "app", "removed.dll")));
        Assert.Equal("user content", File.ReadAllText(Path.Combine(installRoot, "app", "custom-map.txt")));
        Assert.Equal("2.0.0", File.ReadAllText(Path.Combine(installRoot, "version.txt")));
    }

    [Fact]
    public void FullPackageApplyRejectsAManifestForAnUnexpectedVersionBeforeMutation()
    {
        var sourceRoot = Path.Combine(_root, "wrong-version-source");
        var installRoot = Path.Combine(_root, "wrong-version-install");
        WriteFile(sourceRoot, "app/game.dll", "target");
        WriteFile(installRoot, "app/game.dll", "old");
        var targetManifest = CreateManifest("2.0.0", sourceRoot, ["app/game.dll"]);
        UpdateJson.WriteAtomic(Path.Combine(sourceRoot, UpdateFileNames.PackageManifest), targetManifest);

        Assert.Throws<InvalidDataException>(() => TransactionalUpdateInstaller.TryApplyFullPackage(
            sourceRoot,
            installRoot,
            expectedVersion: "2.0.1"));

        Assert.Equal("old", File.ReadAllText(Path.Combine(installRoot, "app", "game.dll")));
        Assert.False(Directory.Exists(Path.Combine(
            installRoot,
            UpdateFileNames.TransactionDirectory)));
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/absolute.txt")]
    [InlineData("logs/updater.log")]
    [InlineData("config/preferences.ini")]
    [InlineData("replays/demo.json")]
    [InlineData(".opengarrison-update.lock")]
    public void UpdatePathsCannotEscapeOrOverwriteProtectedLocalData(string path)
    {
        Assert.Throws<InvalidDataException>(() => UpdatePath.NormalizeRelative(path));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private DeltaFixture CreateDeltaFixture()
    {
        var installRoot = Path.Combine(_root, "delta-install");
        var deltaRoot = Path.Combine(_root, "delta-package");
        WriteFile(installRoot, "app/unchanged.txt", "same");
        WriteFile(installRoot, "app/changed.txt", "old changed");
        WriteFile(installRoot, "app/removed.txt", "remove me");
        WriteFile(installRoot, "version.txt", "1.0.0");
        WriteFile(deltaRoot, "payload/app/changed.txt", "new changed");
        WriteFile(deltaRoot, "payload/app/new.txt", "new file");
        WriteFile(deltaRoot, "payload/version.txt", "1.1.0");

        var targetContentRoot = Path.Combine(_root, "delta-target-content");
        WriteFile(targetContentRoot, "app/unchanged.txt", "same");
        WriteFile(targetContentRoot, "app/changed.txt", "new changed");
        WriteFile(targetContentRoot, "app/new.txt", "new file");
        WriteFile(targetContentRoot, "version.txt", "1.1.0");
        var targetManifest = CreateManifest(
            "1.1.0",
            targetContentRoot,
            ["app/unchanged.txt", "app/changed.txt", "app/new.txt", "version.txt"]);
        var targetManifestPath = Path.Combine(deltaRoot, UpdateFileNames.DeltaTargetManifest);
        UpdateJson.WriteAtomic(targetManifestPath, targetManifest);

        var changedPaths = new[] { "app/changed.txt", "app/new.txt", "version.txt" };
        var plan = new DeltaUpdatePlan
        {
            FromVersion = "1.0.0",
            ToVersion = "1.1.0",
            TargetManifestSha256 = UpdateHash.ComputeSha256(targetManifestPath),
            Files = targetManifest.Files.Where(entry => changedPaths.Contains(entry.Path, StringComparer.Ordinal)).ToList(),
            DeletedFiles = ["app/removed.txt"],
        };
        UpdateJson.WriteAtomic(Path.Combine(deltaRoot, UpdateFileNames.DeltaPlan), plan);
        return new DeltaFixture(installRoot, deltaRoot);
    }

    private static PackageFileManifest CreateManifest(
        string version,
        string contentRoot,
        IReadOnlyList<string> paths)
    {
        return new PackageFileManifest
        {
            Version = version,
            Channel = "stable",
            Files = paths.Select(path =>
            {
                var filePath = Path.Combine(contentRoot, path.Replace('/', Path.DirectorySeparatorChar));
                return new PackageFileEntry
                {
                    Path = path,
                    Size = new FileInfo(filePath).Length,
                    Sha256 = UpdateHash.ComputeSha256(filePath),
                };
            }).ToList(),
        };
    }

    private static void WriteFile(string root, string relativePath, string contents)
    {
        var path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
    }

    private sealed record DeltaFixture(string InstallRoot, string DeltaRoot);

    private sealed class InjectedUpdateFailure : Exception
    {
    }
}
