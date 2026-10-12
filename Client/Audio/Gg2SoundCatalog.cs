#nullable enable

using System;
using System.Collections.Generic;

namespace OpenGarrison.Client;

/// <summary>
/// GG2 mode plays GG2 Modern's own in-match sounds. Nine of GG2's sounds were replaced in
/// SGG's content; GG2's originals ship beside them as "Gg2" + name (copied from
/// Modern/GML-GG2-Modern/Sounds). SGG sounds that stand in for a GG2 sound map to it, and
/// cues GG2 never plays (landing, pain, reload-ready clicks) are silent.
/// Sounds of SGG-only weapons and map features are left alone.
/// </summary>
public static class Gg2SoundCatalog
{
    /// <summary>GG2 sounds whose SGG file differs from GG2 Modern's.</summary>
    private static readonly HashSet<string> ReplacedSounds = new(StringComparer.OrdinalIgnoreCase)
    {
        "ChaingunSnd",
        "DeathSnd1",
        "DeathSnd2",
        "ExplosionSnd",
        "RevolverSnd",
        "RocketSnd",
        "SentryFloorSnd",
        "ShotgunSnd",
        "SniperSnd",
    };

    /// <summary>SGG sounds used where GG2 plays one of its own.</summary>
    private static readonly Dictionary<string, string> StandInSounds = new(StringComparer.OrdinalIgnoreCase)
    {
        // GG2's scattergun fires ShotgunSnd (Scattergun User Event 3).
        ["ScattergunSnd"] = "ShotgunSnd",
    };

    /// <summary>SGG cues with no GG2 counterpart.</summary>
    private static readonly HashSet<string> SilentSounds = new(StringComparer.OrdinalIgnoreCase)
    {
        "ChaingunShotSnd",
        "LandingSnd",
        "ShotgunShellReloadSnd",
        "ShotgunReadySnd",
        "SMGReadySnd",
        "FlareReadySnd",
        "PistolReadySnd",
        "PistolHalfReloadSnd",
        "NailReloadSnd",
        "PlayerHeavyPainSnd",
        "PlayerVeryHeavyPainSnd",
        "PlayerFunnyDieSnd",
    };

    public const string OriginalPrefix = "Gg2";

    /// <summary>
    /// The sound asset to play for a requested name: unchanged outside GG2 mode; in GG2 mode
    /// GG2's original, the GG2 sound an SGG stand-in replaces, or null for silence.
    /// </summary>
    public static string? Resolve(string soundName, bool gg2Mode)
    {
        if (!gg2Mode || string.IsNullOrEmpty(soundName))
        {
            return soundName;
        }

        if (SilentSounds.Contains(soundName))
        {
            return null;
        }

        if (StandInSounds.TryGetValue(soundName, out var standIn))
        {
            soundName = standIn;
        }

        return ReplacedSounds.Contains(soundName) ? OriginalPrefix + soundName : soundName;
    }
}
