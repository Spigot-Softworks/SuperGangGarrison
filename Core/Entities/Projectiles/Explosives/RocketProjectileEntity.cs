namespace OpenGarrison.Core;

public sealed class RocketProjectileEntity : SimulationEntity
{
    public const int LifetimeTicks = 200;
    public const int DirectHitDamage = 25;
    public const float ExplosionDamage = 30f;
    public const float BlastRadius = 65f;
    public const float MaxDistanceToTravel = 800f;
    public const float FriendlyPassThroughDistancePenalty = 100f;
    public const float FadeLifetimeSourceTicks = 8f;
    public const float InitialKnockback = 8f;
    public const float ReducedKnockback = 5f;
    public const float NormalReducedKnockbackDelaySourceTicks = 20f;
    public const float NormalZeroKnockbackDelaySourceTicks = 30f;
    public const float ReflectedReducedKnockbackDelaySourceTicks = 40f;
    public const float ReflectedZeroKnockbackDelaySourceTicks = 80f;
    public const float SplashThresholdFactor = 0.25f;

    private readonly HashSet<int> _passedFriendlyPlayerIds = [];

    public RocketProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float speed,
        float directionRadians,
        float reducedKnockbackSourceTicksRemaining = NormalReducedKnockbackDelaySourceTicks,
        float zeroKnockbackSourceTicksRemaining = NormalZeroKnockbackDelaySourceTicks,
        int? rangeAnchorOwnerId = null,
        float? lastKnownRangeOriginX = null,
        float? lastKnownRangeOriginY = null,
        float distanceToTravel = MaxDistanceToTravel,
        bool isFading = false,
        float fadeSourceTicksRemaining = 0f,
        IReadOnlyList<int>? passedFriendlyPlayerIds = null) : base(id)
    {
        Team = team;
        OwnerId = ownerId;
        X = x;
        Y = y;
        DirectionRadians = directionRadians;
        Speed = speed;
        TicksRemaining = LifetimeTicks;
        ReducedKnockbackSourceTicksRemaining = MathF.Max(0f, reducedKnockbackSourceTicksRemaining);
        ZeroKnockbackSourceTicksRemaining = MathF.Max(0f, zeroKnockbackSourceTicksRemaining);
        RangeAnchorOwnerId = rangeAnchorOwnerId ?? ownerId;
        LastKnownRangeOriginX = lastKnownRangeOriginX ?? x;
        LastKnownRangeOriginY = lastKnownRangeOriginY ?? y;
        DistanceToTravel = MathF.Max(0f, distanceToTravel);
        IsFading = isFading;
        FadeSourceTicksRemaining = isFading ? MathF.Max(0f, fadeSourceTicksRemaining) : 0f;
        SetPassedFriendlyPlayerIds(passedFriendlyPlayerIds);
    }

    public PlayerTeam Team { get; private set; }

    public int OwnerId { get; private set; }

    public float X { get; private set; }

    public float Y { get; private set; }

    public float PreviousX { get; private set; }

    public float PreviousY { get; private set; }

    public float DirectionRadians { get; private set; }

    public float Speed { get; private set; }

    public int TicksRemaining { get; private set; }

    public float ReducedKnockbackSourceTicksRemaining { get; private set; }

    public float ZeroKnockbackSourceTicksRemaining { get; private set; }

    public int RangeAnchorOwnerId { get; private set; }

    public float LastKnownRangeOriginX { get; private set; }

    public float LastKnownRangeOriginY { get; private set; }

    public float DistanceToTravel { get; private set; }

    public bool IsFading { get; private set; }

    public float FadeSourceTicksRemaining { get; private set; }

    public IReadOnlyCollection<int> PassedFriendlyPlayerIds => _passedFriendlyPlayerIds;

    public float CurrentKnockback => ZeroKnockbackSourceTicksRemaining <= 0f
        ? 0f
        : ReducedKnockbackSourceTicksRemaining <= 0f
            ? ReducedKnockback
            : InitialKnockback;

    public bool ExplodeImmediately { get; private set; }

    public bool IsExpired => TicksRemaining <= 0;

    public void AdvanceOneTick(float deltaSeconds)
    {
        PreviousX = X;
        PreviousY = Y;
        X += MathF.Cos(DirectionRadians) * Speed;
        Y += MathF.Sin(DirectionRadians) * Speed;
        Speed += 1f;
        Speed *= 0.92f;
        TicksRemaining -= 1;
        AdvanceKnockbackDecay(deltaSeconds);
    }

    public void RefreshRangeOrigin(float rangeOriginX, float rangeOriginY)
    {
        LastKnownRangeOriginX = rangeOriginX;
        LastKnownRangeOriginY = rangeOriginY;
    }

    public bool TryBeginFadeFromSourceRange()
    {
        if (IsFading || GetCurrentSourceRange() < DistanceToTravel)
        {
            return false;
        }

        IsFading = true;
        FadeSourceTicksRemaining = FadeLifetimeSourceTicks;
        return true;
    }

    public void AdvanceFade(float deltaSeconds)
    {
        if (!IsFading || FadeSourceTicksRemaining <= 0f)
        {
            return;
        }

        var elapsedSourceTicks = MathF.Max(0f, deltaSeconds) * LegacyMovementModel.SourceTicksPerSecond;
        FadeSourceTicksRemaining = MathF.Max(0f, FadeSourceTicksRemaining - elapsedSourceTicks);
        if (FadeSourceTicksRemaining <= 0f)
        {
            Destroy();
        }
    }

    public bool TryRegisterFriendlyPassThrough(int playerId)
    {
        if (playerId == OwnerId || !_passedFriendlyPlayerIds.Add(playerId))
        {
            return false;
        }

        DistanceToTravel = MathF.Max(0f, DistanceToTravel - FriendlyPassThroughDistancePenalty);
        return true;
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

    public void Reflect(int ownerId, PlayerTeam team, float directionRadians)
    {
        OwnerId = ownerId;
        Team = team;
        DirectionRadians = directionRadians;
        TicksRemaining = LifetimeTicks;
        PreviousX = X;
        PreviousY = Y;
        ExplodeImmediately = false;
        ReducedKnockbackSourceTicksRemaining = ReflectedReducedKnockbackDelaySourceTicks;
        ZeroKnockbackSourceTicksRemaining = ReflectedZeroKnockbackDelaySourceTicks;
    }

    public void ApplyImpulse(float velocityX, float velocityY)
    {
        var nextVelocityX = MathF.Cos(DirectionRadians) * Speed + velocityX;
        var nextVelocityY = MathF.Sin(DirectionRadians) * Speed + velocityY;
        var nextSpeed = MathF.Sqrt((nextVelocityX * nextVelocityX) + (nextVelocityY * nextVelocityY));
        if (nextSpeed <= 0.0001f)
        {
            Speed = 0f;
            return;
        }

        DirectionRadians = MathF.Atan2(nextVelocityY, nextVelocityX);
        Speed = nextSpeed;
    }

    public void DelayExplosionUntilNextTick()
    {
        ExplodeImmediately = true;
    }

    public void ClearDelayedExplosion()
    {
        ExplodeImmediately = false;
    }

    public void ApplyNetworkState(
        float x,
        float y,
        float directionRadians,
        float speed,
        int ticksRemaining,
        float reducedKnockbackSourceTicksRemaining = NormalReducedKnockbackDelaySourceTicks,
        float zeroKnockbackSourceTicksRemaining = NormalZeroKnockbackDelaySourceTicks,
        int? rangeAnchorOwnerId = null,
        float? lastKnownRangeOriginX = null,
        float? lastKnownRangeOriginY = null,
        float distanceToTravel = MaxDistanceToTravel,
        bool isFading = false,
        float fadeSourceTicksRemaining = 0f,
        IReadOnlyList<int>? passedFriendlyPlayerIds = null)
    {
        PreviousX = X;
        PreviousY = Y;
        X = x;
        Y = y;
        DirectionRadians = directionRadians;
        Speed = speed;
        TicksRemaining = ticksRemaining;
        ReducedKnockbackSourceTicksRemaining = MathF.Max(0f, reducedKnockbackSourceTicksRemaining);
        ZeroKnockbackSourceTicksRemaining = MathF.Max(0f, zeroKnockbackSourceTicksRemaining);
        RangeAnchorOwnerId = rangeAnchorOwnerId ?? OwnerId;
        LastKnownRangeOriginX = lastKnownRangeOriginX ?? x;
        LastKnownRangeOriginY = lastKnownRangeOriginY ?? y;
        DistanceToTravel = MathF.Max(0f, distanceToTravel);
        IsFading = isFading;
        FadeSourceTicksRemaining = isFading ? MathF.Max(0f, fadeSourceTicksRemaining) : 0f;
        SetPassedFriendlyPlayerIds(passedFriendlyPlayerIds);
        ExplodeImmediately = false;
    }

    public void ApplyNetworkState(
        float x,
        float y,
        float previousX,
        float previousY,
        float directionRadians,
        float speed,
        int ticksRemaining,
        float reducedKnockbackSourceTicksRemaining = NormalReducedKnockbackDelaySourceTicks,
        float zeroKnockbackSourceTicksRemaining = NormalZeroKnockbackDelaySourceTicks,
        int? rangeAnchorOwnerId = null,
        float? lastKnownRangeOriginX = null,
        float? lastKnownRangeOriginY = null,
        float distanceToTravel = MaxDistanceToTravel,
        bool isFading = false,
        float fadeSourceTicksRemaining = 0f,
        IReadOnlyList<int>? passedFriendlyPlayerIds = null)
    {
        PreviousX = previousX;
        PreviousY = previousY;
        X = x;
        Y = y;
        DirectionRadians = directionRadians;
        Speed = speed;
        TicksRemaining = ticksRemaining;
        ReducedKnockbackSourceTicksRemaining = MathF.Max(0f, reducedKnockbackSourceTicksRemaining);
        ZeroKnockbackSourceTicksRemaining = MathF.Max(0f, zeroKnockbackSourceTicksRemaining);
        RangeAnchorOwnerId = rangeAnchorOwnerId ?? OwnerId;
        LastKnownRangeOriginX = lastKnownRangeOriginX ?? x;
        LastKnownRangeOriginY = lastKnownRangeOriginY ?? y;
        DistanceToTravel = MathF.Max(0f, distanceToTravel);
        IsFading = isFading;
        FadeSourceTicksRemaining = isFading ? MathF.Max(0f, fadeSourceTicksRemaining) : 0f;
        SetPassedFriendlyPlayerIds(passedFriendlyPlayerIds);
        ExplodeImmediately = false;
    }

    private void AdvanceKnockbackDecay(float deltaSeconds)
    {
        var elapsedSourceTicks = MathF.Max(0f, deltaSeconds) * LegacyMovementModel.SourceTicksPerSecond;
        ReducedKnockbackSourceTicksRemaining = MathF.Max(0f, ReducedKnockbackSourceTicksRemaining - elapsedSourceTicks);
        ZeroKnockbackSourceTicksRemaining = MathF.Max(0f, ZeroKnockbackSourceTicksRemaining - elapsedSourceTicks);
    }

    private float GetCurrentSourceRange()
    {
        var deltaX = X - LastKnownRangeOriginX;
        var deltaY = Y - LastKnownRangeOriginY;
        return MathF.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }

    private void SetPassedFriendlyPlayerIds(IReadOnlyList<int>? passedFriendlyPlayerIds)
    {
        _passedFriendlyPlayerIds.Clear();
        if (passedFriendlyPlayerIds is null)
        {
            return;
        }

        for (var index = 0; index < passedFriendlyPlayerIds.Count; index += 1)
        {
            _passedFriendlyPlayerIds.Add(passedFriendlyPlayerIds[index]);
        }
    }
}

