using OpenGarrison.Core;
using OpenGarrison.MLBot.Contracts;
using System.Text.Json;

namespace OpenGarrison.MLBot.Tools;

internal static class MLBotDemoInspector
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { WriteIndented = true };

    public static int RunSummary(string rootPath)
    {
        var files = EnumerateDemoFiles(rootPath).ToArray();
        if (files.Length == 0)
        {
            Console.WriteLine($"no demos found under {rootPath}");
            return 0;
        }

        var totalSamples = 0;
        var totalSuccessful = 0;
        var byPhase = new Dictionary<MLBotTaskPhase, (int Files, int Samples, int Successes)>();

        for (var index = 0; index < files.Length; index += 1)
        {
            var document = Load(files[index]);
            totalSamples += document.Samples.Length;
            if (document.Metadata.Success)
            {
                totalSuccessful += 1;
            }

            var phase = document.Metadata.RequestedPhase;
            var aggregate = byPhase.GetValueOrDefault(phase);
            byPhase[phase] = (
                aggregate.Files + 1,
                aggregate.Samples + document.Samples.Length,
                aggregate.Successes + (document.Metadata.Success ? 1 : 0));
        }

        Console.WriteLine($"root={rootPath}");
        Console.WriteLine($"files={files.Length} samples={totalSamples} successes={totalSuccessful}");
        foreach (var entry in byPhase.OrderBy(static entry => entry.Key))
        {
            Console.WriteLine(
                $"phase={entry.Key} files={entry.Value.Files} samples={entry.Value.Samples} successes={entry.Value.Successes}");
        }

        return 0;
    }

    public static int RunCoverage(string rootPath, string? levelName)
    {
        var files = EnumerateDemoFiles(rootPath).ToArray();
        if (files.Length == 0)
        {
            Console.WriteLine($"no demos found under {rootPath}");
            return 0;
        }

        var coverage = new Dictionary<CoverageKey, CoverageAggregate>();
        for (var index = 0; index < files.Length; index += 1)
        {
            var document = Load(files[index]);
            var metadata = document.Metadata;
            if (!string.IsNullOrWhiteSpace(levelName)
                && !string.Equals(metadata.LevelName, levelName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var key = new CoverageKey(
                metadata.LevelName,
                metadata.Team,
                metadata.ClassId,
                metadata.RequestedPhase,
                metadata.CaptureKind);
            if (!coverage.TryGetValue(key, out var aggregate))
            {
                aggregate = new CoverageAggregate();
                coverage[key] = aggregate;
            }

            aggregate.Files += 1;
            aggregate.Samples += document.Samples.Length;
            aggregate.Successes += metadata.Success ? 1 : 0;
            aggregate.ShortCaptures += metadata.ShortCapture ? 1 : 0;
        }

        Console.WriteLine($"root={rootPath}");
        if (!string.IsNullOrWhiteSpace(levelName))
        {
            Console.WriteLine($"map_filter={levelName}");
        }

        if (coverage.Count == 0)
        {
            Console.WriteLine("no matching demos found");
            return 0;
        }

        foreach (var entry in coverage
            .OrderBy(static entry => entry.Key.LevelName)
            .ThenBy(static entry => entry.Key.Team)
            .ThenBy(static entry => entry.Key.ClassId)
            .ThenBy(static entry => entry.Key.RequestedPhase)
            .ThenBy(static entry => entry.Key.CaptureKind))
        {
            Console.WriteLine(
                $"level={entry.Key.LevelName} team={entry.Key.Team} class={entry.Key.ClassId} phase={entry.Key.RequestedPhase} kind={entry.Key.CaptureKind} files={entry.Value.Files} successes={entry.Value.Successes} samples={entry.Value.Samples} short={entry.Value.ShortCaptures}");
        }

        return 0;
    }

    public static int RunManifest(string rootPath, string outputPath)
    {
        var files = EnumerateDemoFiles(rootPath).ToArray();
        var entries = new List<ManifestEntry>(files.Length);
        for (var index = 0; index < files.Length; index += 1)
        {
            var document = Load(files[index]);
            entries.Add(new ManifestEntry(
                files[index],
                document.Metadata.LevelName,
                document.Metadata.Team,
                document.Metadata.ClassId,
                document.Metadata.RequestedPhase,
                document.Metadata.Success,
                document.Samples.Length));
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(entries, ManifestJsonOptions);
        File.WriteAllText(outputPath, json);
        Console.WriteLine($"wrote manifest entries={entries.Count} path={outputPath}");
        return 0;
    }

    private static IEnumerable<string> EnumerateDemoFiles(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(rootPath, "*.json", SearchOption.AllDirectories);
    }

    private static MLBotDemonstrationDocument Load(string path)
    {
        return JsonConfigurationFile.LoadOrCreate<MLBotDemonstrationDocument>(path, static () => new MLBotDemonstrationDocument());
    }

    private sealed record ManifestEntry(
        string Path,
        string LevelName,
        PlayerTeam Team,
        PlayerClass ClassId,
        MLBotTaskPhase RequestedPhase,
        bool Success,
        int SampleCount);

    private sealed record CoverageKey(
        string LevelName,
        PlayerTeam Team,
        PlayerClass ClassId,
        MLBotTaskPhase RequestedPhase,
        MLBotDemonstrationCaptureKind CaptureKind);

    private sealed class CoverageAggregate
    {
        public int Files { get; set; }

        public int Successes { get; set; }

        public int Samples { get; set; }

        public int ShortCaptures { get; set; }
    }
}
