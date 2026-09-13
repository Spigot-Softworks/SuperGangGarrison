#nullable enable

namespace OpenGarrison.Core;

public enum DeathCamPhraseCategory
{
    Generic,
    Bullet,
    Explosive,
    Backstab,
    Fire,
    Sniper,
    Sentry,
}

public static class DeathCamPhraseCatalog
{
    private static readonly string[] GenericPhrases =
    [
        "Sent to hell by",
        "Life ended by",
        "Heart stopped by",
        "Destroyed by",
        "Demolished by",
        "Retired by",
        "Fragged by",
        "Eliminated by",
    ];

    private static readonly string[] BulletPhrases =
    [
        "Gunned down by",
        "Blasted by",
        "Shot dead by",
    ];

    private static readonly string[] ExplosivePhrases =
    [
        "Exploded by",
        "Gibbed by",
        "Blown to bits by",
        "Atomized by",
    ];

    private static readonly string[] BackstabPhrases =
    [
        "Assassinated by",
        "Stabbed by",
        "Punctured by",
        "Shanked by",
    ];

    private static readonly string[] FirePhrases =
    [
        "Burned to death by",
        "Made crispy by",
        "Overcooked by",
        "Turned to ash by",
    ];

    private static readonly string[] SniperPhrases =
    [
        "Sniped by",
        "Noscoped by",
        "Hunted by",
        "Assassinated by",
    ];

    private static readonly string[] SentryPhrases = ["Autogunned by"];

    private static readonly HashSet<string> BulletWeapons = new(StringComparer.OrdinalIgnoreCase)
    {
        "ScatterKL",
        "ShotgunKL",
        "RevolverKL",
        "PistolKL",
        "MinigunKL",
        "NeedleKL",
        "NailgunKL",
        "SmgKL",
    };

    private static readonly HashSet<string> ExplosiveWeapons = new(StringComparer.OrdinalIgnoreCase)
    {
        "RocketKL",
        "RocketReflectKL",
        "GrenadeLauncherKL",
        "ReflectedGrenadeKL",
        "MineKL",
        "MineReflectKL",
        "ExplodeKL",
    };

    private static readonly HashSet<string> BackstabWeapons = new(StringComparer.OrdinalIgnoreCase)
    {
        "KnifeKL",
        "BackstabKL",
    };

    private static readonly HashSet<string> FireWeapons = new(StringComparer.OrdinalIgnoreCase)
    {
        "FlameKL",
        "FlareKL",
        "FlareReflectKL",
    };

    private static readonly HashSet<string> SniperWeapons = new(StringComparer.OrdinalIgnoreCase)
    {
        "RifleKL",
        "RifleChargedKL",
        "BowKL",
    };

    public static DeathCamPhraseCategory ResolveCategory(string? weaponSpriteName, bool isSentry = false)
    {
        if (isSentry || string.Equals(weaponSpriteName, "TurretKL", StringComparison.OrdinalIgnoreCase))
        {
            return DeathCamPhraseCategory.Sentry;
        }

        if (string.IsNullOrWhiteSpace(weaponSpriteName))
        {
            return DeathCamPhraseCategory.Generic;
        }

        if (BackstabWeapons.Contains(weaponSpriteName))
        {
            return DeathCamPhraseCategory.Backstab;
        }

        if (FireWeapons.Contains(weaponSpriteName))
        {
            return DeathCamPhraseCategory.Fire;
        }

        if (SniperWeapons.Contains(weaponSpriteName))
        {
            return DeathCamPhraseCategory.Sniper;
        }

        if (ExplosiveWeapons.Contains(weaponSpriteName))
        {
            return DeathCamPhraseCategory.Explosive;
        }

        return BulletWeapons.Contains(weaponSpriteName)
            ? DeathCamPhraseCategory.Bullet
            : DeathCamPhraseCategory.Generic;
    }

    public static IReadOnlyList<string> GetPhrases(DeathCamPhraseCategory category)
    {
        return category switch
        {
            DeathCamPhraseCategory.Bullet => BulletPhrases,
            DeathCamPhraseCategory.Explosive => ExplosivePhrases,
            DeathCamPhraseCategory.Backstab => BackstabPhrases,
            DeathCamPhraseCategory.Fire => FirePhrases,
            DeathCamPhraseCategory.Sniper => SniperPhrases,
            DeathCamPhraseCategory.Sentry => SentryPhrases,
            _ => GenericPhrases,
        };
    }

    public static string ChoosePhrase(Random random, string? weaponSpriteName, bool isSentry = false)
    {
        ArgumentNullException.ThrowIfNull(random);
        var phrases = GetPhrases(ResolveCategory(weaponSpriteName, isSentry));
        return phrases[random.Next(phrases.Count)];
    }
}
