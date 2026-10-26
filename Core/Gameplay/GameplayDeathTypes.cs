namespace OpenGarrison.Core;

// How a kill dealt by a gameplay plugin (its damage or its projectiles) leaves the body: the
// usual corpse, gibs, or one of the corpse animations other weapons use.
public static class GameplayDeathTypes
{
    public const string Default = "default";
    public const string Gib = "gib";
    public const string Rifle = "rifle";
    public const string Severe = "severe";
    public const string Decapitated = "decapitated";
    public const string Bisected = "bisected";

    // Unknown or missing names give the usual corpse.
    public static void Resolve(string? deathType, out bool gibbed, out DeadBodyAnimationKind animationKind)
    {
        gibbed = false;
        animationKind = DeadBodyAnimationKind.Default;
        switch (deathType?.Trim().ToLowerInvariant())
        {
            case Gib:
                gibbed = true;
                break;
            case Rifle:
                animationKind = DeadBodyAnimationKind.Rifle;
                break;
            case Severe:
                animationKind = DeadBodyAnimationKind.Severe;
                break;
            case Decapitated:
                animationKind = DeadBodyAnimationKind.Decapitated;
                break;
            case Bisected:
                animationKind = DeadBodyAnimationKind.Bisected;
                break;
        }
    }
}
