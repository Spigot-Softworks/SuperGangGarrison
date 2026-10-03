using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using Xunit;

namespace OpenGarrison.Core.Tests.Simulation.Replay;

public sealed class ReplayHarnessTests
{
    private static readonly object RuntimeAssetManifestLock = new();

    [Fact]
    public void MovementReplayMatchesGolden()
    {
        AssertReplayGolden(ReplayScenario.Movement());
    }

    [Fact]
    public void CombatReplayMatchesGolden()
    {
        AssertReplayGolden(ReplayScenario.Combat());
    }

    [Fact]
    public void StructuresReplayMatchesGolden()
    {
        AssertReplayGolden(ReplayScenario.Structures());
    }

    [Fact]
    public void MovementReplayProtocolRoundTripIsByteStable()
    {
        var replay = RunReplay(ReplayScenario.Movement());
        foreach (var message in replay.Messages.Take(16))
        {
            var serialized = ProtocolCodec.Serialize(message);
            Assert.True(
                ProtocolCodec.TryDeserialize(serialized, out var deserialized),
                "ProtocolCodec could not deserialize a serialized replay snapshot.");
            Assert.NotNull(deserialized);

            var reserialized = ProtocolCodec.Serialize((IProtocolMessage)deserialized!);
            Assert.Equal(serialized, reserialized);
        }
    }

    private static void AssertReplayGolden(ReplayScenario scenario)
    {
        var firstRun = RunReplay(scenario);
        var secondRun = RunReplay(scenario);
        AssertHashSequencesEqual(scenario.Name, firstRun.Hashes, secondRun.Hashes);

        var goldenPath = GetGoldenPath(scenario.Name);
        if (string.Equals(Environment.GetEnvironmentVariable("REPLAY_RECORD"), "1", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            File.WriteAllLines(goldenPath, firstRun.Hashes);
            return;
        }

        Assert.True(
            File.Exists(goldenPath),
            $"Replay golden file '{goldenPath}' is missing. Record it with REPLAY_RECORD=1 dotnet test --filter \"FullyQualifiedName~Replay\".");
        var expected = File.ReadAllLines(goldenPath);
        AssertHashSequencesEqual(scenario.Name, expected, firstRun.Hashes);
    }

    private static ReplayResult RunReplay(ReplayScenario scenario)
    {
        EnsureRuntimeAssetManifest();
        using var deterministicScope = new DeterministicSimulationScope();
        var world = scenario.Setup();
        var capture = new SnapshotCapture();
        var messages = new List<SnapshotMessage>(scenario.TickCount);
        var hashes = new List<string>(scenario.TickCount);

        for (var tick = 0; tick < scenario.TickCount; tick += 1)
        {
            foreach (var (slot, input) in scenario.Inputs(tick))
            {
                if (!world.NetworkPlayerRules.TrySetNetworkPlayerInput(slot, input))
                {
                    throw new InvalidOperationException($"Replay input could not be applied to slot {slot} at tick {tick}.");
                }
            }

            world.AdvanceOneTick();
            var message = capture.Capture(world);
            messages.Add(message);
            hashes.Add(Convert.ToHexString(SHA256.HashData(ProtocolCodec.Serialize(message))));
        }

        return new ReplayResult(messages, hashes);
    }

    private static void AssertHashSequencesEqual(
        string scenarioName,
        IReadOnlyList<string> expected,
        IReadOnlyList<string> actual)
    {
        var sharedLength = Math.Min(expected.Count, actual.Count);
        for (var tick = 0; tick < sharedLength; tick += 1)
        {
            Assert.True(
                expected[tick] == actual[tick],
                $"Replay '{scenarioName}' first differs at tick {tick}: expected {expected[tick]}, actual {actual[tick]}.");
        }

        Assert.True(
            expected.Count == actual.Count,
            $"Replay '{scenarioName}' has a different tick count; first possible mismatch is tick {sharedLength} (expected {expected.Count}, actual {actual.Count}).");
    }

    private static string GetGoldenPath(string scenarioName)
    {
        return Path.Combine(
            Path.GetDirectoryName(ResolveSourceFilePath())!,
            "Goldens",
            $"{scenarioName}.sha256");
    }

    private static string GetSourceFilePath([CallerFilePath] string sourceFilePath = "")
    {
        return sourceFilePath;
    }

    private static string ResolveSourceFilePath()
    {
        var sourceFilePath = GetSourceFilePath();
        const string mappedSourceRoot = "/_/OpenGarrison/";
        if (!sourceFilePath.StartsWith(mappedSourceRoot, StringComparison.Ordinal))
        {
            return sourceFilePath;
        }

        var repositoryRoot = new DirectoryInfo(AppContext.BaseDirectory);
        while (repositoryRoot is not null)
        {
            if (File.Exists(Path.Combine(repositoryRoot.FullName, "OpenGarrison.sln")))
            {
                return Path.Combine(repositoryRoot.FullName, sourceFilePath[mappedSourceRoot.Length..]);
            }

            repositoryRoot = repositoryRoot.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), sourceFilePath[mappedSourceRoot.Length..]);
    }

    private static void EnsureRuntimeAssetManifest()
    {
        lock (RuntimeAssetManifestLock)
        {
            var outputManifest = Path.Combine(
                AppContext.BaseDirectory,
                "Content",
                GameMakerRuntimeAssetManifestLoader.ManifestRelativePath);
            if (File.Exists(outputManifest))
            {
                return;
            }

            var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
            for (var directory = outputDirectory; directory is not null; directory = directory.Parent)
            {
                var serverBinDirectory = Path.Combine(directory.FullName, "Server", "bin");
                if (!Directory.Exists(serverBinDirectory))
                {
                    continue;
                }

                foreach (var configurationDirectory in Directory.EnumerateDirectories(serverBinDirectory))
                {
                    var candidate = Path.Combine(
                        configurationDirectory,
                        "net10.0",
                        "Content",
                        GameMakerRuntimeAssetManifestLoader.ManifestRelativePath);
                    if (!File.Exists(candidate))
                    {
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(outputManifest)!);
                    File.Copy(candidate, outputManifest, overwrite: true);
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Replay tests could not find the built runtime asset manifest for the Server project. Expected {outputManifest} or a Server/bin/*/net10.0 copy.");
        }
    }

    private sealed record ReplayResult(
        IReadOnlyList<SnapshotMessage> Messages,
        IReadOnlyList<string> Hashes);
}
