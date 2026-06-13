namespace OpenGarrison.Core;

public sealed class MedicHealNeedleProjectileEntity : NeedleProjectileEntity
{
    public const int DefaultHealPerHit = 30;
    public const int DefaultEnemyDamagePerHit = 22;
    public const float HealthyTargetUberChargePerHit = 1.75f;
    public const float DamagedTargetUberChargePerHealedHealth = 2.5f;

    private readonly int _healPerHit;
    private readonly int _enemyDamagePerHit;

    public MedicHealNeedleProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int healPerHit = DefaultHealPerHit,
        int enemyDamagePerHit = DefaultEnemyDamagePerHit) : base(id, team, ownerId, x, y, velocityX, velocityY)
    {
        _healPerHit = Math.Max(0, healPerHit);
        _enemyDamagePerHit = Math.Max(0, enemyDamagePerHit);
    }

    public int HealPerHit => _healPerHit;

    public override int Damage => _enemyDamagePerHit;

    protected override float ProjectileGravityPerTick => RevolverProjectileEntity.GravityPerTick;
}
