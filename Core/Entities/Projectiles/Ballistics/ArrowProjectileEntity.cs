namespace OpenGarrison.Core;

public sealed class ArrowProjectileEntity : NeedleProjectileEntity
{
    public new const int DamagePerHit = PlayerEntity.SniperBowMinDamage;
    public new const int LifetimeTicks = 270;
    public new const float GravityPerTick = 0.48f;

    private readonly int _damage;

    public override int Damage => _damage;

    public float FakeSpeedMultiplier { get; private set; }

    protected override float ProjectileGravityPerTick =>
        GravityPerTick * FakeSpeedMultiplier * FakeSpeedMultiplier;

    // ArrowS origin is near the tail (4, 2); the tip is on the right edge of the 40px-wide sprite.
    public override float HitProbeForwardOffset => 35f;

    public ArrowProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int damage = DamagePerHit,
        float fakeSpeedMultiplier = PlayerEntity.SniperBowMinFakeSpeedMultiplier) : base(id, team, ownerId, x, y, velocityX, velocityY, LifetimeTicks)
    {
        _damage = Math.Max(0, damage);
        SetFakeSpeedMultiplier(fakeSpeedMultiplier);
    }

    public void SetFakeSpeedMultiplier(float fakeSpeedMultiplier)
    {
        FakeSpeedMultiplier = MathF.Max(0.0001f, fakeSpeedMultiplier);
    }
}
