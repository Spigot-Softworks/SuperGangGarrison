namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public const float ExplosiveSplashRadiusMultiplier = 1.2f;
    public const float ExplosiveSplashMinimumDamage = 25f;

    public static float ResolveExplosiveSplashRadius(float baseRadius)
    {
        return float.IsFinite(baseRadius)
            ? MathF.Max(0f, baseRadius) * ExplosiveSplashRadiusMultiplier
            : 0f;
    }

    public static float ResolveExplosiveSplashDamage(float maximumDamage, float distanceFactor)
        => ResolveExplosiveSplashDamage(maximumDamage, distanceFactor, ExplosiveSplashMinimumDamage);

    public static float ResolveExplosiveSplashDamage(float maximumDamage, float distanceFactor, float minimumDamage)
    {
        if (!float.IsFinite(maximumDamage)
            || !float.IsFinite(distanceFactor)
            || maximumDamage <= 0f
            || distanceFactor <= 0f)
        {
            return 0f;
        }

        return MathF.Max(
            MathF.Max(0f, minimumDamage),
            maximumDamage * Math.Clamp(distanceFactor, 0f, 1f));
    }
}
