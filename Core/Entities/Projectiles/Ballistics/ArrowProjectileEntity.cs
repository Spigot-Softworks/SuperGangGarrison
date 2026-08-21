namespace OpenGarrison.Core;

public sealed class ArrowProjectileEntity : NeedleProjectileEntity
{
    public new const int DamagePerHit = PlayerEntity.SniperBowMinDamage;
    public new const int LifetimeTicks = 270;
    public new const float GravityPerTick = 0.48f;

    private const float SpriteLeft = -4f;
    private const float SpriteRight = 36f;
    private const float SpriteTop = -2f;
    private const float SpriteBottom = 8f;
    private const float MaximumPlatformSlope = 0.5f;

    private readonly int _damage;
    private readonly HashSet<int> _piercedPlayerIds = [];

    public override int Damage => _damage;

    public float FakeSpeedMultiplier { get; private set; }

    public bool IsLanded { get; private set; }

    /// <summary>
    /// Spawn-time Last to Die payload. Keeping this on the projectile makes a
    /// released arrow independent from later perk swaps and network hydration.
    /// </summary>
    public bool AppliesLastToDieGuardian { get; private set; }

    public bool PiercesPlayers { get; private set; }

    public bool AppliesLastToDieTranqDarts { get; private set; }

    public float LastToDiePoisonDamagePerSecond { get; private set; }

    public float LastToDieGhostDamageMultiplier { get; private set; }

    public bool AppliesLastToDieDecapitator { get; private set; }

    public bool IsLastToDieDecapitatorFullyCharged { get; private set; }

    public bool AppliesLastToDieExplosiveTip { get; private set; }

    public bool IsLastToDieExplosiveTipArmed =>
        AppliesLastToDieExplosiveTip && !LastToDieExplosiveTipConsumed;

    private bool LastToDieExplosiveTipConsumed { get; set; }

    public PlayerClass? LastToDieAttachedHeadClassId { get; private set; }

    public PlayerTeam? LastToDieAttachedHeadTeam { get; private set; }

    public bool HasLastToDieAttachedHead =>
        LastToDieAttachedHeadClassId.HasValue && LastToDieAttachedHeadTeam.HasValue;

    public string? LastToDieAttachedHeadSpriteName =>
        LastToDieAttachedHeadClassId is { } classId
        && LastToDieAttachedHeadTeam is { } team
            ? ExperimentalDemoknightCatalog.GetDecapitatedHeadSpriteName(classId, team)
            : null;

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
        float fakeSpeedMultiplier = PlayerEntity.SniperBowMinFakeSpeedMultiplier,
        bool appliesLastToDieGuardian = false,
        bool piercesPlayers = false,
        bool appliesLastToDieTranqDarts = false,
        float lastToDiePoisonDamagePerSecond = 0f,
        float lastToDieGhostDamageMultiplier = 1f,
        bool appliesLastToDieDecapitator = false,
        bool isLastToDieDecapitatorFullyCharged = false,
        bool appliesLastToDieExplosiveTip = false,
        PlayerClass? lastToDieAttachedHeadClassId = null,
        PlayerTeam? lastToDieAttachedHeadTeam = null) : base(id, team, ownerId, x, y, velocityX, velocityY, LifetimeTicks)
    {
        _damage = Math.Max(0, damage);
        SetFakeSpeedMultiplier(fakeSpeedMultiplier);
        ConfigureLastToDiePayload(
            appliesLastToDieGuardian,
            piercesPlayers,
            appliesLastToDieTranqDarts,
            lastToDiePoisonDamagePerSecond,
            lastToDieGhostDamageMultiplier,
            appliesLastToDieDecapitator,
            isLastToDieDecapitatorFullyCharged,
            appliesLastToDieExplosiveTip,
            lastToDieAttachedHeadClassId,
            lastToDieAttachedHeadTeam);
    }

    public void SetFakeSpeedMultiplier(float fakeSpeedMultiplier)
    {
        FakeSpeedMultiplier = MathF.Max(0.0001f, fakeSpeedMultiplier);
    }

    public void ConfigureLastToDiePayload(
        bool appliesGuardian,
        bool piercesPlayers,
        bool appliesTranqDarts = false,
        float poisonDamagePerSecond = 0f,
        float ghostDamageMultiplier = 1f,
        bool appliesDecapitator = false,
        bool isDecapitatorFullyCharged = false,
        bool appliesExplosiveTip = false,
        PlayerClass? attachedHeadClassId = null,
        PlayerTeam? attachedHeadTeam = null)
    {
        AppliesLastToDieGuardian = appliesGuardian;
        PiercesPlayers = piercesPlayers;
        AppliesLastToDieTranqDarts = appliesTranqDarts;
        LastToDiePoisonDamagePerSecond = MathF.Max(0f, poisonDamagePerSecond);
        LastToDieGhostDamageMultiplier = MathF.Max(1f, ghostDamageMultiplier);
        AppliesLastToDieDecapitator = appliesDecapitator;
        IsLastToDieDecapitatorFullyCharged = appliesDecapitator && isDecapitatorFullyCharged;
        AppliesLastToDieExplosiveTip = appliesExplosiveTip;
        LastToDieExplosiveTipConsumed = false;
        if (attachedHeadClassId.HasValue
            && attachedHeadTeam.HasValue
            && Enum.IsDefined(attachedHeadClassId.Value)
            && Enum.IsDefined(attachedHeadTeam.Value))
        {
            LastToDieAttachedHeadClassId = attachedHeadClassId;
            LastToDieAttachedHeadTeam = attachedHeadTeam;
        }
        else
        {
            LastToDieAttachedHeadClassId = null;
            LastToDieAttachedHeadTeam = null;
        }
        if (!piercesPlayers)
        {
            _piercedPlayerIds.Clear();
        }
    }

    public bool TryAttachLastToDieDecapitatedHead(PlayerClass classId, PlayerTeam team)
    {
        if (HasLastToDieAttachedHead
            || !AppliesLastToDieDecapitator
            || !IsLastToDieDecapitatorFullyCharged
            || !Enum.IsDefined(classId)
            || !Enum.IsDefined(team))
        {
            return false;
        }

        LastToDieAttachedHeadClassId = classId;
        LastToDieAttachedHeadTeam = team;
        return true;
    }

    public override void Destroy()
    {
        ClearLastToDieDecapitatorPayload();
        base.Destroy();
    }

    public bool TryConsumeLastToDieExplosiveTip()
    {
        if (!IsLastToDieExplosiveTipArmed)
        {
            return false;
        }

        LastToDieExplosiveTipConsumed = true;
        return true;
    }

    public bool HasPiercedPlayer(int playerId) => _piercedPlayerIds.Contains(playerId);

    public void MarkPlayerPierced(int playerId)
    {
        if ((PiercesPlayers || HasLastToDieAttachedHead) && playerId > 0)
        {
            _piercedPlayerIds.Add(playerId);
        }
    }

    public override void AdvanceOneTick(float gravityScale = 1f)
    {
        if (!IsLanded)
        {
            base.AdvanceOneTick(gravityScale);
            return;
        }

        SetPreviousPositionToCurrent();
        AdvanceLifetimeOneTick();
    }

    public void Land(float x, float y, float directionX, float directionY)
    {
        MoveTo(x, y);
        SetPreviousPositionToCurrent();

        var directionLength = MathF.Sqrt((directionX * directionX) + (directionY * directionY));
        if (directionLength > 0.0001f)
        {
            SetVelocity(directionX / directionLength, directionY / directionLength);
        }

        IsLanded = true;
    }

    public void SetLanded(bool isLanded)
    {
        IsLanded = isLanded;
        if (isLanded)
        {
            SetPreviousPositionToCurrent();
        }
    }

    /// <summary>
    /// Gets the axis-aligned one-way support surface for a landed arrow.
    /// Flying arrows deliberately have no platform bounds. Steeply embedded
    /// arrows are also excluded because their footprint is not a usable
    /// horizontal platform.
    /// </summary>
    public bool TryGetOneWayPlatformBounds(out float left, out float top, out float right)
    {
        left = 0f;
        top = 0f;
        right = 0f;
        if (!IsLanded || IsExpired)
        {
            return false;
        }

        var directionLength = MathF.Sqrt((VelocityX * VelocityX) + (VelocityY * VelocityY));
        if (directionLength <= 0.0001f)
        {
            return false;
        }

        var directionX = VelocityX / directionLength;
        var directionY = VelocityY / directionLength;
        if (MathF.Abs(directionY) > MathF.Abs(directionX) * MaximumPlatformSlope)
        {
            return false;
        }

        // The legacy presentation flips the sprite vertically when the arrow
        // travels left. Mirror the local sprite bounds here so the support
        // surface follows the rendered stuck arrow in either direction.
        var localTop = directionX < 0f ? -SpriteBottom : SpriteTop;
        var localBottom = directionX < 0f ? -SpriteTop : SpriteBottom;
        var minX = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var minY = float.PositiveInfinity;
        var cosine = directionX;
        var sine = directionY;

        ConsiderRotatedSpriteCorner(SpriteLeft, localTop, cosine, sine, ref minX, ref maxX, ref minY);
        ConsiderRotatedSpriteCorner(SpriteRight, localTop, cosine, sine, ref minX, ref maxX, ref minY);
        ConsiderRotatedSpriteCorner(SpriteLeft, localBottom, cosine, sine, ref minX, ref maxX, ref minY);
        ConsiderRotatedSpriteCorner(SpriteRight, localBottom, cosine, sine, ref minX, ref maxX, ref minY);

        left = X + minX;
        top = Y + minY;
        right = X + maxX;
        return right - left > 1f;
    }

    private static void ConsiderRotatedSpriteCorner(
        float localX,
        float localY,
        float cosine,
        float sine,
        ref float minX,
        ref float maxX,
        ref float minY)
    {
        var worldX = (localX * cosine) - (localY * sine);
        var worldY = (localX * sine) + (localY * cosine);
        minX = MathF.Min(minX, worldX);
        maxX = MathF.Max(maxX, worldX);
        minY = MathF.Min(minY, worldY);
    }

    public override void Reflect(int ownerId, PlayerTeam team, float directionRadians)
    {
        base.Reflect(ownerId, team, directionRadians);
        IsLanded = false;
        AppliesLastToDieGuardian = false;
        PiercesPlayers = false;
        AppliesLastToDieTranqDarts = false;
        LastToDiePoisonDamagePerSecond = 0f;
        LastToDieGhostDamageMultiplier = 1f;
        AppliesLastToDieExplosiveTip = false;
        LastToDieExplosiveTipConsumed = false;
        ClearLastToDieDecapitatorPayload();
        _piercedPlayerIds.Clear();
    }

    private void ClearLastToDieDecapitatorPayload()
    {
        AppliesLastToDieDecapitator = false;
        IsLastToDieDecapitatorFullyCharged = false;
        LastToDieAttachedHeadClassId = null;
        LastToDieAttachedHeadTeam = null;
    }

}
