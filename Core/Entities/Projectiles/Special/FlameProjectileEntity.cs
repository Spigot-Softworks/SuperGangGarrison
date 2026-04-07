namespace OpenGarrison.Core;

public sealed class FlameProjectileEntity : SimulationEntity
{
    public const int AirLifetimeTicks = 15;
    public const int AttachedLifetimeTicks = 150;
    public const float DirectHitDamage = 3.14f;
    public const float BurnIntensityIncrease = 1.35f;
    public const float BurnDurationIncreaseSourceTicks = 30f;
    public const bool AfterburnFalloff = true;
    public const float BurnDamagePerTick = 0.06f;
    public const float GravityPerTick = 0.15f;
    public const int PenetrationCap = 1;

    private float _burnDamageAccumulator;
    private readonly HashSet<int> _hitPlayerIds = [];

    public FlameProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        int ticksRemaining = AirLifetimeTicks,
        bool isPerseverant = false,
        float directHitDamage = DirectHitDamage,
        float burnDamagePerTick = BurnDamagePerTick) : base(id)
    {
        Team = team;
        OwnerId = ownerId;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = ticksRemaining;
        IsPerseverant = isPerseverant;
        DirectHitDamageValue = directHitDamage;
        BurnDamagePerTickValue = burnDamagePerTick;
    }

    public PlayerTeam Team { get; }

    public int OwnerId { get; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float PreviousX { get; private set; }

    public float PreviousY { get; private set; }

    public float VelocityX { get; private set; }

    public float VelocityY { get; private set; }

    public int TicksRemaining { get; private set; }

    public float DirectHitDamageValue { get; private set; }

    public float BurnDamagePerTickValue { get; private set; }

    public int? AttachedPlayerId { get; private set; }

    public float AttachedOffsetX { get; private set; }

    public float AttachedOffsetY { get; private set; }

    public bool IsAttached => AttachedPlayerId.HasValue;

    public bool IsExpired => TicksRemaining <= 0;

    public bool IsPerseverant { get; }

    public int HitPlayerCount => _hitPlayerIds.Count;

    public void AdvanceOneTick(float deltaSeconds)
    {
        PreviousX = X;
        PreviousY = Y;
        if (!IsAttached)
        {
            var sourceDelta = MathF.Max(0f, deltaSeconds) * LegacyMovementModel.SourceTicksPerSecond;
            X += VelocityX * sourceDelta;
            Y += VelocityY * sourceDelta;
            VelocityY += GravityPerTick * sourceDelta;
        }

        TicksRemaining -= 1;
    }

    public float GetAfterburnFalloffAmount(int airLifetimeSimulationTicks)
    {
        if (airLifetimeSimulationTicks <= 0)
        {
            return 0f;
        }

        return 1f - float.Clamp(TicksRemaining / (float)airLifetimeSimulationTicks, 0f, 1f);
    }

    public void AttachToPlayer(PlayerEntity player, int attachedLifetimeTicks)
    {
        AttachedPlayerId = player.Id;
        AttachedOffsetX = X - player.X;
        AttachedOffsetY = Y - player.Y;
        VelocityX = 0f;
        VelocityY = 0f;
        TicksRemaining = attachedLifetimeTicks;
        _burnDamageAccumulator = 0f;
    }

    public bool ApplyAttachedBurn(PlayerEntity player)
    {
        if (!IsAttached || AttachedPlayerId != player.Id || !player.IsAlive)
        {
            return false;
        }

        X = player.X + AttachedOffsetX;
        Y = player.Y + AttachedOffsetY;
        _burnDamageAccumulator += BurnDamagePerTickValue;
        var wholeDamage = (int)_burnDamageAccumulator;
        if (wholeDamage <= 0)
        {
            return false;
        }

        _burnDamageAccumulator -= wholeDamage;
        return player.ApplyDamage(wholeDamage);
    }

    public void MoveTo(float x, float y)
    {
        X = x;
        Y = y;
    }

    public void Destroy()
    {
        TicksRemaining = 0;
    }

    public bool HasHitPlayer(int playerId)
    {
        return _hitPlayerIds.Contains(playerId);
    }

    public void RegisterHitPlayer(int playerId)
    {
        _hitPlayerIds.Add(playerId);
    }

    public void ApplyNetworkState(
        float x,
        float y,
        float velocityX,
        float velocityY,
        int ticksRemaining,
        int? attachedPlayerId,
        float attachedOffsetX,
        float attachedOffsetY)
    {
        PreviousX = X;
        PreviousY = Y;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = ticksRemaining;
        AttachedPlayerId = attachedPlayerId;
        AttachedOffsetX = attachedOffsetX;
        AttachedOffsetY = attachedOffsetY;
        DirectHitDamageValue = DirectHitDamage;
        BurnDamagePerTickValue = BurnDamagePerTick;
    }

    public void ApplyNetworkState(
        float x,
        float y,
        float previousX,
        float previousY,
        float velocityX,
        float velocityY,
        int ticksRemaining,
        int? attachedPlayerId,
        float attachedOffsetX,
        float attachedOffsetY)
    {
        PreviousX = previousX;
        PreviousY = previousY;
        X = x;
        Y = y;
        VelocityX = velocityX;
        VelocityY = velocityY;
        TicksRemaining = ticksRemaining;
        AttachedPlayerId = attachedPlayerId;
        AttachedOffsetX = attachedOffsetX;
        AttachedOffsetY = attachedOffsetY;
        DirectHitDamageValue = DirectHitDamage;
        BurnDamagePerTickValue = BurnDamagePerTick;
    }
}
