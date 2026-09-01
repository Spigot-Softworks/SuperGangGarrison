using OpenGarrison.Core.LastToDie;

namespace OpenGarrison.Core;

public sealed class MedicHealNeedleProjectileEntity : NeedleProjectileEntity
{
    public const int DefaultHealPerHit = 30;
    public const int DefaultEnemyDamagePerHit = 22;
    public const float DefaultProjectileSpeed = 20f;
    public const float DefaultSpreadDegrees = 1f;
    public const float HealthyTargetUberChargePerHit = 1.75f * 2f / 3f;
    public const float DamagedTargetUberChargePerHealedHealth = 2.5f * 2f / 3f;

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
        int enemyDamagePerHit = DefaultEnemyDamagePerHit,
        LastToDieMedicKritzM2Payload lastToDiePayload = default,
        int lastToDieJavelinFuseTicksRemaining = 0,
        bool isLastToDieJavelinAnchored = false,
        bool hasLastToDieJavelinExploded = false) : base(
            id,
            team,
            ownerId,
            x,
            y,
            velocityX,
            velocityY,
            lifetimeTicks: lastToDiePayload.AppliesJavelin
                ? Math.Max(LifetimeTicks, Math.Max(1, lastToDieJavelinFuseTicksRemaining))
                : LifetimeTicks)
    {
        _healPerHit = Math.Max(0, healPerHit);
        _enemyDamagePerHit = Math.Max(0, enemyDamagePerHit);
        LastToDiePayload = lastToDiePayload;
        HydrateLastToDieJavelinState(
            isLastToDieJavelinAnchored,
            lastToDieJavelinFuseTicksRemaining,
            hasLastToDieJavelinExploded);
    }

    public int HealPerHit => _healPerHit;

    public override int Damage => _enemyDamagePerHit;

    public LastToDieMedicKritzM2Payload LastToDiePayload { get; }

    public bool AppliesLastToDieHailMary => LastToDiePayload.AppliesHailMary;

    public bool AppliesLastToDieNeurotoxin => LastToDiePayload.AppliesNeurotoxin;

    public bool AppliesLastToDieJavelin => LastToDiePayload.AppliesJavelin;

    public bool IsLastToDieJavelinAnchored { get; private set; }

    public int LastToDieJavelinFuseTicksRemaining { get; private set; }

    public bool HasLastToDieJavelinExploded { get; private set; }

    public bool IsLastToDieJavelinFuseExpired =>
        AppliesLastToDieJavelin && LastToDieJavelinFuseTicksRemaining <= 0;

    protected override float ProjectileGravityPerTick => RevolverProjectileEntity.GravityPerTick;

    public override void AdvanceOneTick(float gravityScale = 1f)
    {
        if (!AppliesLastToDieJavelin)
        {
            base.AdvanceOneTick(gravityScale);
            return;
        }

        if (IsLastToDieJavelinAnchored)
        {
            SetPreviousPositionToCurrent();
            SetVelocity(0f, 0f);
            AdvanceLifetimeOneTick();
        }
        else
        {
            base.AdvanceOneTick(gravityScale);
        }

        LastToDieJavelinFuseTicksRemaining = Math.Max(
            0,
            LastToDieJavelinFuseTicksRemaining - 1);
    }

    public bool TryAnchorLastToDieJavelin(float x, float y)
    {
        if (!AppliesLastToDieJavelin
            || IsLastToDieJavelinAnchored
            || HasLastToDieJavelinExploded)
        {
            return false;
        }

        MoveTo(x, y);
        SetVelocity(0f, 0f);
        IsLastToDieJavelinAnchored = true;
        return true;
    }

    public bool TryMarkLastToDieJavelinExploded()
    {
        if (!AppliesLastToDieJavelin || HasLastToDieJavelinExploded)
        {
            return false;
        }

        HasLastToDieJavelinExploded = true;
        LastToDieJavelinFuseTicksRemaining = 0;
        Destroy();
        return true;
    }

    public void HydrateLastToDieJavelinState(
        bool isAnchored,
        int fuseTicksRemaining,
        bool hasExploded)
    {
        if (!AppliesLastToDieJavelin)
        {
            IsLastToDieJavelinAnchored = false;
            LastToDieJavelinFuseTicksRemaining = 0;
            HasLastToDieJavelinExploded = false;
            return;
        }

        IsLastToDieJavelinAnchored = isAnchored;
        LastToDieJavelinFuseTicksRemaining = Math.Max(0, fuseTicksRemaining);
        HasLastToDieJavelinExploded = hasExploded;
        if (isAnchored)
        {
            SetVelocity(0f, 0f);
        }

        if (hasExploded)
        {
            Destroy();
        }
    }
}
