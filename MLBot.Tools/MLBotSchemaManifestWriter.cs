using OpenGarrison.MLBot;
using OpenGarrison.MLBot.Contracts;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.MLBot.Tools;

internal static class MLBotSchemaManifestWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static int Run(string? outputPath)
    {
        var manifest = new MLBotSchemaManifest
        {
            CurrentSchema = MLBotObservationVectorSchema.V7,
            CurrentFeatureCount = MLBotFeatureVectorizer.FeatureCount,
            DemoSchemaVersion = new MLBotDemonstrationMetadata().SchemaVersion,
            FeatureCounts = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["V1"] = MLBotFeatureVectorizer.FeatureCountV1,
                ["V2"] = MLBotFeatureVectorizer.FeatureCountV2,
                ["V3"] = MLBotFeatureVectorizer.FeatureCountV3,
                ["V4"] = MLBotFeatureVectorizer.FeatureCountV4,
                ["V5"] = MLBotFeatureVectorizer.FeatureCountV5,
                ["V6"] = MLBotFeatureVectorizer.FeatureCountV6,
                ["V7"] = MLBotFeatureVectorizer.FeatureCountV7,
            },
            DeprecatedScaffoldSchemas = ["V3", "V4"],
            Notes =
            [
                "V7 is the default world-truth schema.",
                "V5 excludes legacy nav graph nodes, score-route state, and traversal metadata.",
                "V6 adds team/mode perspective, objective-side affordances, progress-toward-objective features, and capture-state perspective features.",
                "V7 adds direct collision-derived landing affordances for local vertical traversal.",
                "Promotion/evaluation claims should pass with policy overrides disabled.",
            ],
        };

        var json = JsonSerializer.Serialize(manifest, JsonOptions);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.WriteLine(json);
            return 0;
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(outputPath, json);
        Console.WriteLine($"saved schema_manifest={outputPath}");
        return 0;
    }
}

internal sealed class MLBotSchemaManifest
{
    public MLBotObservationVectorSchema CurrentSchema { get; set; }

    public int CurrentFeatureCount { get; set; }

    public string DemoSchemaVersion { get; set; } = string.Empty;

    public Dictionary<string, int> FeatureCounts { get; set; } = new(StringComparer.Ordinal);

    public string[] DeprecatedScaffoldSchemas { get; set; } = [];

    public string[] Notes { get; set; } = [];
}
