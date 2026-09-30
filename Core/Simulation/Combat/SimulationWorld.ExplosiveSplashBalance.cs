namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    public const float ExplosiveSplashRadiusMultiplier = CombatSystem.ExplosiveSplashRadiusMultiplier;
    public const float ExplosiveSplashMinimumDamage = CombatSystem.ExplosiveSplashMinimumDamage;

    public static float ResolveExplosiveSplashRadius(float baseRadius)
    {
        return CombatSystem.ResolveExplosiveSplashRadius(baseRadius);
    }

    public static float ResolveExplosiveSplashDamage(float maximumDamage, float distanceFactor)
        => CombatSystem.ResolveExplosiveSplashDamage(maximumDamage, distanceFactor, ExplosiveSplashMinimumDamage);

    public static float ResolveExplosiveSplashDamage(float maximumDamage, float distanceFactor, float minimumDamage)
    {
        return CombatSystem.ResolveExplosiveSplashDamage(maximumDamage, distanceFactor, minimumDamage);
    }
}
