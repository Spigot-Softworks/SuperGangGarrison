using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

/// <summary>
/// Plugin-registered gameplay content a server sends to connecting clients, so classes,
/// items and loadouts that only exist in the server process can be selected and simulated.
/// Pack art is not embedded: <see cref="AssetPacks"/> carries sprite definitions plus a
/// manifest URL the client downloads the frame images from.
/// </summary>
public sealed record GameplayReplicatedContent(
    IReadOnlyList<GameplayReplicatedItem> Items,
    IReadOnlyList<GameplayReplicatedClass> Classes,
    IReadOnlyList<GameplayReplicatedLoadout> Loadouts,
    IReadOnlyList<GameplayReplicatedPrimaryWeaponBehavior> PrimaryWeaponBehaviors,
    IReadOnlyList<GameplayReplicatedAssetPack>? AssetPacks = null)
{
    public const int SchemaVersion = 2;

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public static GameplayReplicatedContent Empty { get; } = new([], [], [], []);

    [JsonIgnore]
    public bool IsEmpty => Items.Count == 0
        && Classes.Count == 0
        && Loadouts.Count == 0
        && PrimaryWeaponBehaviors.Count == 0
        && (AssetPacks?.Count ?? 0) == 0;

    public byte[] SerializeToUtf8Json()
    {
        return JsonSerializer.SerializeToUtf8Bytes(new Document(SchemaVersion, this), JsonOptions);
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> utf8Json, out GameplayReplicatedContent content, out string errorMessage)
    {
        try
        {
            var document = JsonSerializer.Deserialize<Document>(utf8Json, JsonOptions);
            if (document?.Content is null)
            {
                content = Empty;
                errorMessage = "Replicated gameplay content is empty.";
                return false;
            }

            if (document.SchemaVersion != SchemaVersion)
            {
                content = Empty;
                errorMessage = $"Replicated gameplay content schema {document.SchemaVersion} is not supported (expected {SchemaVersion}).";
                return false;
            }

            content = document.Content;
            errorMessage = string.Empty;
            return true;
        }
        catch (JsonException ex)
        {
            content = Empty;
            errorMessage = $"Replicated gameplay content is invalid: {ex.Message}";
            return false;
        }
    }

    internal static string SerializeForComparison<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions);
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        // Matches GameplayModPackDirectoryLoader so definitions round-trip like pack JSON.
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record Document(int SchemaVersion, GameplayReplicatedContent Content);
}

public sealed record GameplayReplicatedItem(string ModPackId, GameplayItemDefinition Item);

public sealed record GameplayReplicatedClass(string ModPackId, GameplayClassDefinition Class, GameplayClassRuntimeBinding Binding);

/// <summary>A loadout a plugin added to a class it does not own, such as a stock class.</summary>
public sealed record GameplayReplicatedLoadout(string ClassId, GameplayClassLoadoutDefinition Loadout);

public sealed record GameplayReplicatedPrimaryWeaponBehavior(string BehaviorId, string? FireSoundName);

/// <summary>
/// A pack's sprite definitions (origins and hit masks), sound definitions, and where to
/// download their files: <paramref name="ManifestUrl"/> is relative to the server's HTTP endpoint.
/// </summary>
public sealed record GameplayReplicatedAssetPack(
    string PackId,
    string ContentHash,
    string ManifestUrl,
    IReadOnlyDictionary<string, GameplaySpriteAssetDefinition> Sprites,
    IReadOnlyDictionary<string, GameplaySoundAssetDefinition>? Sounds = null);
