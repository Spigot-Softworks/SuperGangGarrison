namespace OpenGarrison.Core;

/// <summary>Presentation and inherited-motion defaults for one authored player-gib part.</summary>
public readonly record struct AuthoredPlayerGibPart(
    string SpriteName,
    int CatalogPartIndex,
    float VelocityRangeX,
    float VelocityRangeY,
    float RotationRange,
    float HorizontalFriction,
    float RotationFriction,
    float BloodChance,
    bool InheritPlayerVelocity,
    float SpawnOffsetX,
    float SpawnOffsetY);

/// <summary>
/// Hand-drawn, independently moving parts for the stock classes that have a complete authored set.
/// The lookup is intentionally keyed by gameplay class ID so runtime Quote/legacy Quote does not
/// accidentally receive the stock Civilian/Employer parts.
/// </summary>
public static class AuthoredPlayerGibCatalog
{
    private readonly record struct PartTemplate(
        string PartName,
        bool TeamVariant,
        float VelocityRangeX,
        float VelocityRangeY,
        float RotationRange,
        float HorizontalFriction,
        float RotationFriction,
        float BloodChance,
        bool InheritPlayerVelocity);

    private static readonly PartTemplate[] LegParts =
    [
        Leg("LegTopL"),
        Leg("LegBottomL"),
        Leg("LegTopR"),
        Leg("LegBottomR"),
    ];

    private static readonly Dictionary<string, PartTemplate[]> ClassParts = new(StringComparer.Ordinal)
    {
        ["civilian"] =
        [
            .. LegParts,
            Chest("ChestA"),
            Chest("ChestB"),
            Head("Head"),
            Accessory("Gun"),
        ],
        ["scout"] =
        [
            .. LegParts,
            Chest("Chest"),
            Head("Head", teamVariant: false),
            Accessory("Hat", teamVariant: false),
            Accessory("Gun", teamVariant: false),
        ],
        ["soldier"] =
        [
            .. LegParts,
            Chest("ChestA"),
            Chest("ChestB"),
            Head("Head", teamVariant: false),
            Accessory("Hat"),
            Accessory("Gun", teamVariant: false),
        ],
        ["pyro"] =
        [
            .. LegParts,
            Chest("ChestA"),
            Chest("ChestB"),
            Head("Head"),
            Accessory("Gun"),
        ],
        ["demoman"] =
        [
            .. LegParts,
            Chest("ChestA"),
            Chest("ChestB"),
            Head("Head"),
            Accessory("Hat"),
            Accessory("Gun", teamVariant: false),
        ],
        ["heavy"] =
        [
            .. LegParts,
            Chest("ChestA"),
            Chest("ChestB"),
            Head("Head", teamVariant: false),
            Accessory("Gun", teamVariant: false),
        ],
        ["engineer"] =
        [
            .. LegParts,
            Chest("ChestA"),
            Chest("ChestB"),
            Head("Head", teamVariant: false),
            Accessory("Hat", teamVariant: false),
            Accessory("Gun", teamVariant: false),
        ],
        ["medic"] =
        [
            .. LegParts,
            Chest("ChestA"),
            Chest("ChestB"),
            Head("Head"),
            Accessory("Gun"),
        ],
        ["sniper"] =
        [
            .. LegParts,
            Chest("ChestA"),
            Chest("ChestB"),
            Head("Head"),
            Accessory("Hat"),
            Accessory("Gun", teamVariant: false),
        ],
        ["spy"] =
        [
            Leg("LegTop"),
            Leg("LegBottom"),
            Chest("ChestA"),
            Chest("ChestB"),
            Head("Head"),
            Accessory("Gun", teamVariant: false),
        ],
    };

    private static readonly IReadOnlyDictionary<string, int> CatalogPartIndices = CreateCatalogPartIndices();

    public static bool TryGetParts(
        string? gameplayClassId,
        PlayerTeam team,
        out IReadOnlyList<AuthoredPlayerGibPart> parts)
    {
        parts = Array.Empty<AuthoredPlayerGibPart>();
        if (gameplayClassId is null
            || !ClassParts.TryGetValue(gameplayClassId, out var templates)
            || !TryGetTeamName(team, out var teamName))
        {
            return false;
        }

        var className = ToClassName(gameplayClassId);
        var resolved = new AuthoredPlayerGibPart[templates.Length];
        for (var index = 0; index < templates.Length; index += 1)
        {
            var template = templates[index];
            var teamSuffix = template.TeamVariant ? teamName : string.Empty;
            var placement = AuthoredPlayerGibPlacementCatalog.Get(gameplayClassId, template.PartName);
            resolved[index] = new AuthoredPlayerGibPart(
                $"PlayerGib{className}{teamSuffix}{template.PartName}S",
                index,
                template.VelocityRangeX,
                template.VelocityRangeY,
                template.RotationRange,
                template.HorizontalFriction,
                template.RotationFriction,
                template.BloodChance,
                template.InheritPlayerVelocity,
                placement.X,
                placement.Y);
        }

        parts = resolved;
        return true;
    }

    /// <summary>Returns the stable authored-part index used by the Medium gib-detail filter.</summary>
    public static int GetCatalogPartIndex(string? spriteName)
        => spriteName is not null && CatalogPartIndices.TryGetValue(spriteName, out var index)
            ? index
            : -1;

    public static bool IsAuthoredSprite(string? spriteName)
        => GetCatalogPartIndex(spriteName) >= 0;

    private static IReadOnlyDictionary<string, int> CreateCatalogPartIndices()
    {
        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var classId in ClassParts.Keys)
        {
            foreach (var team in new[] { PlayerTeam.Red, PlayerTeam.Blue })
            {
                if (!TryGetParts(classId, team, out var parts))
                {
                    continue;
                }

                foreach (var part in parts)
                {
                    indices.TryAdd(part.SpriteName, part.CatalogPartIndex);
                }
            }
        }

        return indices;
    }

    private static string ToClassName(string gameplayClassId)
        => gameplayClassId switch
        {
            "civilian" => "Civilian",
            "scout" => "Scout",
            "soldier" => "Soldier",
            "pyro" => "Pyro",
            "demoman" => "Demoman",
            "heavy" => "Heavy",
            "engineer" => "Engineer",
            "medic" => "Medic",
            "sniper" => "Sniper",
            "spy" => "Spy",
            _ => throw new ArgumentOutOfRangeException(nameof(gameplayClassId)),
        };

    private static bool TryGetTeamName(PlayerTeam team, out string teamName)
    {
        switch (team)
        {
            case PlayerTeam.Red:
                teamName = "Red";
                return true;
            case PlayerTeam.Blue:
                teamName = "Blue";
                return true;
            default:
                teamName = string.Empty;
                return false;
        }
    }

    private static PartTemplate Leg(string partName)
        => new(partName, true, 2f, 0f, 6f, 0.3f, 0.4f, 7f, InheritPlayerVelocity: false);

    private static PartTemplate Chest(string partName)
        => new(partName, true, 8f, 9f, 72f, 0.4f, 0.6f, 1.8f, InheritPlayerVelocity: true);

    private static PartTemplate Head(string partName, bool teamVariant = true)
        => new(partName, teamVariant, 8f, 9f, 52f, 0.5f, 0.5f, 1.4f, InheritPlayerVelocity: false);

    private static PartTemplate Accessory(string partName, bool teamVariant = true)
        => new(partName, teamVariant, 8f, 9f, 52f, 0.4f, 0.2f, 0f, InheritPlayerVelocity: true);
}
