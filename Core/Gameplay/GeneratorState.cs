namespace OpenGarrison.Core;

public sealed class GeneratorState
{
    public GeneratorState(PlayerTeam team, RoomObjectMarker marker, int maxHealth)
    {
        Team = team;
        Marker = marker;
        MaxHealth = maxHealth;
        Health = maxHealth;
    }

    public PlayerTeam Team { get; }

    public RoomObjectMarker Marker { get; }

    public int Health { get; private set; }

    public int MaxHealth { get; }

    public bool IsDestroyed => Health <= 0;

    public float HealthFraction => MaxHealth <= 0 ? 0f : Math.Clamp(Health / (float)MaxHealth, 0f, 1f);

    public int DamageStage
    {
        get
        {
            if (HealthFraction >= 0.66f)
            {
                return 0;
            }

            return HealthFraction >= 0.33f ? 1 : 2;
        }
    }

    public bool ApplyDamage(float damage)
    {
        if (damage <= 0f || IsDestroyed)
        {
            return false;
        }

        Health = Math.Max(0, Health - (int)MathF.Ceiling(damage));
        return IsDestroyed;
    }

    public void SetHealth(int health)
    {
        Health = Math.Clamp(health, 0, MaxHealth);
    }

    public void Reset()
    {
        Health = MaxHealth;
    }
}
