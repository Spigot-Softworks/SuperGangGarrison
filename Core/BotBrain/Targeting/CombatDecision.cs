using System.Runtime.CompilerServices;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core.BotBrain;

public sealed class CombatDecisionMemory
{
    public int BeenHealingTicks { get; set; }

    public int BeenHealingSwitchTicks { get; set; } = 20;

    public int ReloadCounterTicks { get; set; }

    public int ZoomToShootTicks { get; set; } = 50;

    public int LastPyroReflectProjectileId { get; set; } = -1;

    public int LastHeavyDashThreatProjectileId { get; set; } = -1;

    public int DemomanGrenadePreferenceTicks { get; set; }

    public int DemomanGrenadeDecisionCooldownTicks { get; set; }

    public int DemomanGrenadeDecisionSerial { get; set; }
}

public readonly record struct CombatFireDecision(
    bool FirePrimary,
    bool FireSecondary,
    bool UseAbility);

public enum MedicHealTargetSelectionKind
{
    None,
    HumanMedicCall,
    Critical,
    Pocket,
}

public readonly record struct MedicHealTargetSelection(
    PlayerEntity? Target,
    MedicHealTargetSelectionKind Kind);

public readonly record struct SpyBackstabPlan(
    PlayerEntity? Target,
    bool ShouldAttempt,
    bool ReadyToStab,
    float ApproachX,
    float ApproachY);

public readonly record struct IncomingReflectProjectile(
    int Id,
    float TimeToClosestPointTicks);

public enum BotBrainCombatTargetKind
{
    Player,
    Sentry,
    Generator,
}

public readonly record struct BotBrainCombatTarget(
    BotBrainCombatTargetKind Kind,
    PlayerTeam Team,
    float X,
    float Y,
    PlayerEntity? Player = null,
    SentryEntity? Sentry = null,
    GeneratorState? Generator = null);

public static class CombatDecisionResolver
{
    private static readonly ConditionalWeakTable<SimpleLevel, LineOfSightLevelCache> LineOfSightLevelCaches = new();
    private static readonly ConditionalWeakTable<SimulationWorld, LineOfSightFrameCache> LineOfSightFrameCaches = new();

    private const int HeavyIdleEatHealth = 100;
    private const int HeavyCombatEatHealth = 30;
    private const float CloseCombatDistance = 170f;
    private const float SniperDangerDistance = 300f;
    private const float SoldierShotgunDistance = 260f;
    private const float MineThreatDistance = 400f;
    private const float MineThreatDistanceSquared = MineThreatDistance * MineThreatDistance;
    private const float MineDetonationRadius = 50f;
    private const float MineDetonationRadiusSquared = MineDetonationRadius * MineDetonationRadius;
    private const float MedicHealTargetMaxDistance = 300f;
    private const float MedicHealTargetMaxDistanceSquared = MedicHealTargetMaxDistance * MedicHealTargetMaxDistance;
    private const float SpyLowHealthFraction = 0.25f;
    private const float SpyBackstabMaxPlanDistance = 260f;
    private const float SpyBackstabAllyIsolationDistance = 340f;
    private const float SpyBackstabStableTargetSpeed = 45f;
    private const float SpyBackstabApproachOffset = 26f;
    private const float SpyBackstabReadyHorizontalError = 18f;
    private const float SpyBackstabReadyVerticalError = 22f;
    private const float SpyRevolverAttackDistance = 420f;
    private const float SpyIntelDecloakDistance = 48f;
    private const float PyroReflectDetectionDistance = 300f;
    private const float PyroReflectLaneRadius = 46f;
    private const float PyroReflectAccurateWindowTicks = 8f;
    private const float PyroReflectMistimeWindowTicks = 16f;
    private const float PyroReflectLatePanicWindowTicks = 1.5f;
    private const float HeavyDashThreatDetectionDistance = 340f;
    private const float HeavyDashThreatLaneRadius = 64f;
    private const float HeavyDashThreatWindowTicks = 14f;
    private const float DemomanGrenadeMinDistance = 90f;
    private const float DemomanGrenadeMaxDistance = 520f;
    private const int DemomanGrenadeLoadedPreferenceTicks = 12;
    private const int DemomanGrenadeReloadPreferenceTicks = 52;

    public static PlayerEntity? FindBestMedicHealTarget(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        IReadOnlyDictionary<byte, PlayerTeam>? controlledTeamsBySlot = null)
    {
        return FindBestMedicHealTargetSelection(world, self, team, controlledTeamsBySlot).Target;
    }

    public static MedicHealTargetSelection FindBestMedicHealTargetSelection(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team,
        IReadOnlyDictionary<byte, PlayerTeam>? controlledTeamsBySlot = null)
    {
        if (self.ClassId != PlayerClass.Medic)
        {
            return default;
        }

        PlayerEntity? bestHumanMedicCallTarget = null;
        var bestHumanMedicCallScore = float.PositiveInfinity;
        PlayerEntity? bestCriticalTarget = null;
        var bestCriticalScore = float.PositiveInfinity;
        PlayerEntity? bestPocketTarget = null;
        var bestPocketMaxHealth = int.MinValue;
        var bestPocketDistance = float.PositiveInfinity;
        foreach (var candidate in EnumeratePlayers(world))
        {
            if (!candidate.IsAlive
                || candidate.Team != team
                || candidate.Id == self.Id
                || IsCloakedSpy(candidate))
            {
                continue;
            }

            var distanceSquared = DistanceSquared(self.X, self.Y, candidate.X, candidate.Y);
            if (distanceSquared > MedicHealTargetMaxDistanceSquared)
            {
                continue;
            }

            if (!HasLineOfSight(world, self.X, self.Y, candidate.X, candidate.Y, self.Team, self.IsCarryingIntel))
            {
                continue;
            }

            var distance = MathF.Sqrt(distanceSquared);
            var healthFraction = GetHealthFraction(candidate);
            if (IsNonControlledMedicCall(world, candidate, controlledTeamsBySlot))
            {
                var humanCallScore = (distance / 1000f) + healthFraction;
                if (humanCallScore < bestHumanMedicCallScore)
                {
                    bestHumanMedicCallScore = humanCallScore;
                    bestHumanMedicCallTarget = candidate;
                }
            }

            if (healthFraction < 0.5f)
            {
                var criticalScore = (distance / 1000f) + healthFraction * 2f;
                if (criticalScore >= bestCriticalScore)
                {
                    continue;
                }

                bestCriticalScore = criticalScore;
                bestCriticalTarget = candidate;
                continue;
            }

            if (candidate.MaxHealth < bestPocketMaxHealth
                || candidate.MaxHealth == bestPocketMaxHealth && distance >= bestPocketDistance)
            {
                continue;
            }

            bestPocketMaxHealth = candidate.MaxHealth;
            bestPocketDistance = distance;
            bestPocketTarget = candidate;
        }

        if (bestHumanMedicCallTarget is not null)
        {
            return new MedicHealTargetSelection(bestHumanMedicCallTarget, MedicHealTargetSelectionKind.HumanMedicCall);
        }

        if (bestCriticalTarget is not null)
        {
            return new MedicHealTargetSelection(bestCriticalTarget, MedicHealTargetSelectionKind.Critical);
        }

        return bestPocketTarget is not null
            ? new MedicHealTargetSelection(bestPocketTarget, MedicHealTargetSelectionKind.Pocket)
            : default;
    }

    private static bool IsNonControlledMedicCall(
        SimulationWorld world,
        PlayerEntity candidate,
        IReadOnlyDictionary<byte, PlayerTeam>? controlledTeamsBySlot)
    {
        if (!candidate.IsChatBubbleVisible
            || candidate.ChatBubbleFrameIndex != ChatBubbleFrameCatalog.Medic
            || !world.TryGetPlayerNetworkSlot(candidate, out var slot))
        {
            return false;
        }

        return controlledTeamsBySlot is null || !controlledTeamsBySlot.ContainsKey(slot);
    }

    private static bool IsCloakedSpy(PlayerEntity candidate) =>
        candidate.ClassId == PlayerClass.Spy && candidate.IsSpyCloaked;

    public static bool IsPlayerVisibleToBot(PlayerEntity observer, PlayerEntity candidate)
    {
        if (candidate.ClassId != PlayerClass.Spy || !candidate.IsSpyCloaked)
        {
            return true;
        }

        if (!candidate.IsSpyVisibleToEnemies)
        {
            return false;
        }

        return candidate.IsCarryingIntel || !IsCloakedSpyBehindObserver(observer, candidate);
    }

    private static bool IsCloakedSpyBehindObserver(PlayerEntity observer, PlayerEntity spy)
    {
        var facingDirection = MathF.Sign(observer.FacingDirectionX);
        if (facingDirection == 0f)
        {
            return false;
        }

        var spyDirection = MathF.Sign(spy.X - observer.X);
        return spyDirection != 0f && spyDirection == -facingDirection;
    }

    public static CombatFireDecision Resolve(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        CombatDecisionMemory memory)
    {
        UpdateReloadMemory(self, memory);
        var isBeingHealed = IsPlayerBeingHealed(world, self);
        var firePrimary = ResolvePrimaryFire(world, self, combatTarget, healTarget, memory, isBeingHealed);
        var fireSecondary = ResolveSecondaryFire(world, self, combatTarget, healTarget, memory, isBeingHealed);
        var useAbility = ResolveAbilityInputFromLoadout(world, self, combatTarget, healTarget, firePrimary, fireSecondary, memory);
        ApplyReloadDiscipline(self, memory, ref firePrimary, ref fireSecondary, ref useAbility);
        return new CombatFireDecision(firePrimary, fireSecondary, useAbility);
    }

    private static bool ResolvePrimaryFire(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        CombatDecisionMemory memory,
        bool isBeingHealed)
    {
        if (self.ClassId == PlayerClass.Medic)
        {
            memory.BeenHealingTicks += 1;
            var canKeepHealing = true;
            if (healTarget is not null
                && self.MedicHealTargetId.HasValue
                && self.MedicHealTargetId.Value != healTarget.Id
                && memory.BeenHealingTicks > memory.BeenHealingSwitchTicks)
            {
                memory.BeenHealingTicks = 0;
                canKeepHealing = false;
            }

            return healTarget is not null || combatTarget is null && canKeepHealing;
        }

        memory.BeenHealingTicks = 0;
        if (combatTarget is null)
        {
            return false;
        }

        if (self.ClassId == PlayerClass.Spy)
        {
            return ResolveSpyPrimaryFire(world, self, combatTarget);
        }

        if (self.ClassId == PlayerClass.Heavy && ShouldHeavyEat(self, isBeingHealed, HeavyCombatEatHealth))
        {
            return false;
        }

        if (self.ClassId == PlayerClass.Sniper && self.IsSniperScoped)
        {
            if (self.SniperChargeTicks >= memory.ZoomToShootTicks)
            {
                memory.ZoomToShootTicks = Math.Max(1, memory.ZoomToShootTicks);
                return true;
            }

            return false;
        }

        return ShouldPrimaryWeaponFire(self, DistanceBetween(self.X, self.Y, combatTarget.Value.X, combatTarget.Value.Y));
    }

    private static bool ResolveSecondaryFire(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        CombatDecisionMemory memory,
        bool isBeingHealed)
    {
        if (self.ClassId == PlayerClass.Medic)
        {
            if (healTarget is not null)
            {
                return self.IsMedicUberReady && (healTarget.Health < 50 || self.Health < 40);
            }

            return combatTarget is not null;
        }

        if (self.ClassId == PlayerClass.Heavy)
        {
            return ShouldHeavyEat(self, isBeingHealed, combatTarget is null ? HeavyIdleEatHealth : HeavyCombatEatHealth);
        }

        if (self.ClassId == PlayerClass.Pyro && ShouldPyroAirblastIncomingProjectile(world, self, memory))
        {
            return true;
        }

        if (self.ClassId == PlayerClass.Sniper)
        {
            if (combatTarget is null)
            {
                return self.IsSniperScoped;
            }

            var distance = DistanceBetween(self.X, self.Y, combatTarget.Value.X, combatTarget.Value.Y);
            if (distance >= SniperDangerDistance)
            {
                return !self.IsSniperScoped;
            }

            if (self.IsSniperScoped)
            {
                memory.ZoomToShootTicks = Math.Max(1, memory.ZoomToShootTicks);
                return true;
            }

            return false;
        }

        if (self.ClassId == PlayerClass.Spy)
        {
            return ResolveSpySecondaryFire(world, self, combatTarget);
        }

        return self.ClassId == PlayerClass.Demoman && ShouldDetonateMines(world, self);
    }

    private static bool ResolveSpyPrimaryFire(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget)
    {
        if (combatTarget is not { Kind: BotBrainCombatTargetKind.Player, Player: { } target })
        {
            return false;
        }

        if (self.IsSpyCloaked)
        {
            return ResolveSpyBackstabPlan(world, self, combatTarget).ReadyToStab;
        }

        return ShouldSpyUseRevolver(self, target)
            && ShouldPrimaryWeaponFire(self, DistanceBetween(self.X, self.Y, target.X, target.Y));
    }

    private static bool ResolveSpySecondaryFire(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget)
    {
        if (self.IsSpyBackstabAnimating || self.IsCarryingIntel)
        {
            return false;
        }

        if (self.IsSpyCloaked)
        {
            if (ShouldSpyDecloakForIntelPickup(world, self))
            {
                return true;
            }

            if (ShouldSpyDecloakForObjectiveCapture(world, self))
            {
                return true;
            }

            if (combatTarget is not { Kind: BotBrainCombatTargetKind.Player, Player: { } target })
            {
                return false;
            }

            var shouldBreakCloakToShoot = GetHealthFraction(self) < SpyLowHealthFraction
                || !ResolveSpyBackstabPlan(world, self, combatTarget).ShouldAttempt;
            if (shouldBreakCloakToShoot
                && ShouldSpyUseRevolver(self, target)
                && self.SpyCloakAlpha <= PlayerEntity.SpyCloakToggleThreshold)
            {
                return true;
            }

            return false;
        }

        if (combatTarget is not { Kind: BotBrainCombatTargetKind.Player })
        {
            return !ShouldSpyStayUncloakedForObjectiveCapture(world, self);
        }

        return ResolveSpyBackstabPlan(world, self, combatTarget).ShouldAttempt;
    }

    private static bool ShouldSpyDecloakForObjectiveCapture(SimulationWorld world, PlayerEntity self)
    {
        if (world.MatchRules.Mode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
            || self.ClassId != PlayerClass.Spy
            || !self.IsSpyCloaked)
        {
            return false;
        }

        return IsSpyOnRelevantUnownedControlPoint(world, self);
    }

    private static bool ShouldSpyStayUncloakedForObjectiveCapture(SimulationWorld world, PlayerEntity self)
    {
        if (world.MatchRules.Mode is not (GameModeKind.ControlPoint or GameModeKind.KingOfTheHill or GameModeKind.DoubleKingOfTheHill)
            || self.ClassId != PlayerClass.Spy)
        {
            return false;
        }

        return IsSpyOnRelevantUnownedControlPoint(world, self);
    }

    private static bool IsSpyOnRelevantUnownedControlPoint(SimulationWorld world, PlayerEntity self)
    {
        foreach (var point in world.ControlPoints)
        {
            if (point.IsLocked
                || point.Team == self.Team
                || !world.IsPlayerInControlPointCaptureZone(self, point.Index))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool ShouldSpyDecloakForIntelPickup(SimulationWorld world, PlayerEntity self)
    {
        if (world.MatchRules.Mode != GameModeKind.CaptureTheFlag
            || self.ClassId != PlayerClass.Spy
            || !self.IsSpyCloaked
            || self.IsCarryingIntel
            || self.IntelPickupCooldownTicks > 0
            || self.IsInsideBlockingTeamGate(world.Level, self.Team))
        {
            return false;
        }

        var opposingTeam = self.Team == PlayerTeam.Red ? PlayerTeam.Blue : PlayerTeam.Red;
        var enemyIntelBase = world.Level.GetIntelBase(opposingTeam);
        if (!enemyIntelBase.HasValue)
        {
            return false;
        }

        var dx = enemyIntelBase.Value.X - self.X;
        var dy = enemyIntelBase.Value.Y - self.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy)) <= SpyIntelDecloakDistance;
    }

    public static SpyBackstabPlan ResolveSpyBackstabPlan(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget)
    {
        if (self.ClassId != PlayerClass.Spy
            || self.IsCarryingIntel
            || self.Health <= 0
            || GetHealthFraction(self) < SpyLowHealthFraction
            || IsSpyCompromised(self)
            || combatTarget is not { Kind: BotBrainCombatTargetKind.Player, Player: { } target }
            || !target.IsAlive
            || !IsValidSpyBackstabTarget(world, self, target))
        {
            return default;
        }

        var predictedTargetX = target.X + target.HorizontalSpeed * (float)world.Config.FixedDeltaSeconds * PlayerEntity.SpyBackstabWindupTicksDefault;
        var behindDirection = ResolveBehindDirection(target);
        var approachX = predictedTargetX + behindDirection * SpyBackstabApproachOffset;
        var approachY = target.Y;
        var horizontalError = MathF.Abs(self.X - approachX);
        var verticalError = MathF.Abs(self.Y - approachY);
        var isBehindTarget = behindDirection < 0f
            ? self.X <= target.X + 2f
            : self.X >= target.X - 2f;
        var readyToStab = self.IsSpyCloaked
            && self.IsSpyBackstabReady
            && isBehindTarget
            && horizontalError <= SpyBackstabReadyHorizontalError
            && verticalError <= SpyBackstabReadyVerticalError
            && HasLineOfSight(world, self.X, self.Y, target.X, target.Y, self.Team, self.IsCarryingIntel);

        return new SpyBackstabPlan(target, ShouldAttempt: true, readyToStab, approachX, approachY);
    }

    public static bool IsSpyCompromised(PlayerEntity self)
    {
        return self.ClassId == PlayerClass.Spy
            && self.IsSpyCloaked
            && self.IsSpyVisibleToEnemies
            && self.SpyCloakAlpha >= PlayerEntity.SpyDamageRevealAlpha;
    }

    private static bool ResolveAbilityInputFromLoadout(
        SimulationWorld world,
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        bool firePrimary,
        bool fireSecondary,
        CombatDecisionMemory memory)
    {
        if (self.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber))
        {
            return self.IsMedicUberReady && (self.Health < 40 || healTarget is { Health: < 50 });
        }

        if (self.HasUtilityBehavior(BuiltInGameplayBehaviorIds.HeavyUtility)
            && ShouldHeavyDashIncomingFire(world, self, memory))
        {
            return true;
        }

        if (combatTarget is not { } target)
        {
            return false;
        }

        if (self.HasUtilityBehavior(BuiltInGameplayBehaviorIds.PyroUtility))
        {
            return false;
        }

        if (self.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SoldierSecondaryWeapon))
        {
            return !fireSecondary
                && (!firePrimary || self.CurrentShells <= 0)
                && self.ExperimentalOffhandCurrentShells > 0
                && DistanceBetween(self.X, self.Y, target.X, target.Y) <= SoldierShotgunDistance;
        }

        if (ShouldDemomanUseGrenadeLauncher(self, target, fireSecondary, memory))
        {
            return true;
        }

        return false;
    }

    private static bool ShouldDemomanUseGrenadeLauncher(
        PlayerEntity self,
        BotBrainCombatTarget target,
        bool fireSecondary,
        CombatDecisionMemory memory)
    {
        if (self.ClassId != PlayerClass.Demoman
            || fireSecondary
            || self.ExperimentalOffhandWeapon?.Kind != PrimaryWeaponKind.GrenadeLauncher)
        {
            memory.DemomanGrenadePreferenceTicks = 0;
            return false;
        }

        var distance = DistanceBetween(self.X, self.Y, target.X, target.Y);
        if (distance is < DemomanGrenadeMinDistance or > DemomanGrenadeMaxDistance)
        {
            memory.DemomanGrenadePreferenceTicks = 0;
            return false;
        }

        if (self.IsExperimentalOffhandSelected && memory.DemomanGrenadePreferenceTicks > 0)
        {
            memory.DemomanGrenadePreferenceTicks -= 1;
            return true;
        }

        if (memory.DemomanGrenadeDecisionCooldownTicks > 0)
        {
            memory.DemomanGrenadeDecisionCooldownTicks -= 1;
            return false;
        }

        memory.DemomanGrenadeDecisionSerial += 1;
        var targetId = target.Player?.Id
            ?? target.Sentry?.Id
            ?? (target.Generator is null ? 0 : (int)target.Team);
        memory.DemomanGrenadeDecisionCooldownTicks = 36 + PositiveModulo(
            (self.Id * 7) + (targetId * 11) + (memory.DemomanGrenadeDecisionSerial * 5),
            24);

        if (PositiveModulo(memory.DemomanGrenadeDecisionSerial + self.Id + targetId, 3) != 0)
        {
            return false;
        }

        memory.DemomanGrenadePreferenceTicks = self.ExperimentalOffhandCurrentShells > 0
            ? DemomanGrenadeLoadedPreferenceTicks
            : DemomanGrenadeReloadPreferenceTicks;
        return true;
    }

    private static bool ShouldHeavyDashIncomingFire(
        SimulationWorld world,
        PlayerEntity self,
        CombatDecisionMemory memory)
    {
        if (self.ClassId != PlayerClass.Heavy
            || self.IsHeavyEating
            || self.IsExperimentalGhostDashing
            || self.ExperimentalGhostDashCooldownTicksRemaining > 0
            || !TryFindIncomingHeavyDashThreat(world, self, out var projectile)
            || memory.LastHeavyDashThreatProjectileId == projectile.Id
            || projectile.TimeToClosestPointTicks > HeavyDashThreatWindowTicks
            || !ShouldHeavyDashIncomingProjectile(self, projectile.Id))
        {
            return false;
        }

        memory.LastHeavyDashThreatProjectileId = projectile.Id;
        return true;
    }

    private static void UpdateReloadMemory(PlayerEntity self, CombatDecisionMemory memory)
    {
        if (memory.ReloadCounterTicks > 0)
        {
            memory.ReloadCounterTicks -= 1;
            if (self.CurrentShells >= self.PrimaryWeapon.MaxAmmo)
            {
                memory.ReloadCounterTicks = 0;
            }

            return;
        }

        if (self.PrimaryWeapon.Kind == PrimaryWeaponKind.Rifle)
        {
            return;
        }

        if (self.CurrentShells <= 0)
        {
            memory.ReloadCounterTicks = (3 * self.PrimaryWeapon.ReloadDelayTicks) + self.PrimaryWeapon.AmmoReloadTicks;
            return;
        }

        if (self.PrimaryWeapon.Kind == PrimaryWeaponKind.FlameThrower && self.CurrentShells < 3)
        {
            memory.ReloadCounterTicks = 4 * self.PrimaryWeapon.AmmoReloadTicks;
            return;
        }

        if (self.PrimaryWeapon.Kind == PrimaryWeaponKind.Minigun && self.CurrentShells < 3)
        {
            memory.ReloadCounterTicks = 6 * self.PrimaryWeapon.AmmoReloadTicks;
        }
    }

    private static void ApplyReloadDiscipline(
        PlayerEntity self,
        CombatDecisionMemory memory,
        ref bool firePrimary,
        ref bool fireSecondary,
        ref bool useAbility)
    {
        if (memory.ReloadCounterTicks <= 0)
        {
            return;
        }

        if (self.ClassId == PlayerClass.Medic)
        {
            fireSecondary = false;
            useAbility = false;
            return;
        }

        firePrimary = false;
    }

    private static bool ShouldPyroAirblastIncomingProjectile(
        SimulationWorld world,
        PlayerEntity self,
        CombatDecisionMemory memory)
    {
        if (!self.CanFirePyroAirblast())
        {
            return false;
        }

        if (!TryFindIncomingReflectProjectile(world, self, out var projectile))
        {
            return false;
        }

        if (memory.LastPyroReflectProjectileId == projectile.Id)
        {
            return false;
        }

        var accurateReflect = ShouldPyroReflectAccurately(self, projectile.Id);
        var shouldFire = accurateReflect
            ? projectile.TimeToClosestPointTicks <= PyroReflectAccurateWindowTicks
            : projectile.TimeToClosestPointTicks is > PyroReflectAccurateWindowTicks and <= PyroReflectMistimeWindowTicks
                || projectile.TimeToClosestPointTicks <= PyroReflectLatePanicWindowTicks;
        if (!shouldFire)
        {
            return false;
        }

        memory.LastPyroReflectProjectileId = projectile.Id;
        return true;
    }

    private static bool TryFindIncomingReflectProjectile(
        SimulationWorld world,
        PlayerEntity self,
        out IncomingReflectProjectile projectile)
    {
        projectile = default;
        var bestScore = float.PositiveInfinity;

        foreach (var rocket in world.Rockets)
        {
            var velocityX = MathF.Cos(rocket.DirectionRadians) * rocket.Speed;
            var velocityY = MathF.Sin(rocket.DirectionRadians) * rocket.Speed;
            TryConsiderReflectProjectile(self, rocket.Team, rocket.OwnerId, rocket.Id, rocket.X, rocket.Y, velocityX, velocityY, ref bestScore, ref projectile);
        }

        foreach (var flare in world.Flares)
        {
            TryConsiderReflectProjectile(self, flare.Team, flare.OwnerId, flare.Id, flare.X, flare.Y, flare.VelocityX, flare.VelocityY, ref bestScore, ref projectile);
        }

        foreach (var mine in world.Mines)
        {
            if (mine.IsDestroyed || mine.IsStickied)
            {
                continue;
            }

            TryConsiderReflectProjectile(self, mine.Team, mine.OwnerId, mine.Id, mine.X, mine.Y, mine.VelocityX, mine.VelocityY, ref bestScore, ref projectile);
        }

        return projectile.Id != 0;
    }

    private static void TryConsiderReflectProjectile(
        PlayerEntity self,
        PlayerTeam projectileTeam,
        int ownerId,
        int projectileId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        ref float bestScore,
        ref IncomingReflectProjectile projectile)
    {
        if (projectileTeam == self.Team || ownerId == self.Id)
        {
            return;
        }

        var speedSq = (velocityX * velocityX) + (velocityY * velocityY);
        if (speedSq <= 0.001f)
        {
            return;
        }

        var toPlayerX = self.X - x;
        var toPlayerY = self.Y - y;
        var closingDistance = ((toPlayerX * velocityX) + (toPlayerY * velocityY)) / MathF.Sqrt(speedSq);
        if (closingDistance <= 0f || closingDistance > PyroReflectDetectionDistance)
        {
            return;
        }

        var distanceToProjectile = DistanceBetween(self.X, self.Y, x, y);
        var perpendicularSq = MathF.Max(0f, (distanceToProjectile * distanceToProjectile) - (closingDistance * closingDistance));
        var perpendicularDistance = MathF.Sqrt(perpendicularSq);
        if (perpendicularDistance > PyroReflectLaneRadius)
        {
            return;
        }

        var timeToClosestPointTicks = closingDistance / MathF.Sqrt(speedSq);
        var score = timeToClosestPointTicks + (perpendicularDistance / PyroReflectLaneRadius);
        if (score >= bestScore)
        {
            return;
        }

        bestScore = score;
        projectile = new IncomingReflectProjectile(projectileId, timeToClosestPointTicks);
    }

    private static bool TryFindIncomingHeavyDashThreat(
        SimulationWorld world,
        PlayerEntity self,
        out IncomingReflectProjectile projectile)
    {
        projectile = default;
        var bestScore = float.PositiveInfinity;

        foreach (var rocket in world.Rockets)
        {
            var velocityX = MathF.Cos(rocket.DirectionRadians) * rocket.Speed;
            var velocityY = MathF.Sin(rocket.DirectionRadians) * rocket.Speed;
            TryConsiderHeavyDashProjectile(self, rocket.Team, rocket.OwnerId, rocket.Id, rocket.X, rocket.Y, velocityX, velocityY, ref bestScore, ref projectile);
        }

        foreach (var flare in world.Flares)
        {
            TryConsiderHeavyDashProjectile(self, flare.Team, flare.OwnerId, flare.Id, flare.X, flare.Y, flare.VelocityX, flare.VelocityY, ref bestScore, ref projectile);
        }

        foreach (var grenade in world.Grenades)
        {
            if (grenade.IsDestroyed)
            {
                continue;
            }

            TryConsiderHeavyDashProjectile(self, grenade.Team, grenade.OwnerId, grenade.Id, grenade.X, grenade.Y, grenade.VelocityX, grenade.VelocityY, ref bestScore, ref projectile);
        }

        foreach (var shot in world.Shots)
        {
            if (shot.IsExpired)
            {
                continue;
            }

            TryConsiderHeavyDashProjectile(self, shot.Team, shot.OwnerId, shot.Id, shot.X, shot.Y, shot.VelocityX, shot.VelocityY, ref bestScore, ref projectile);
        }

        foreach (var needle in world.Needles)
        {
            if (needle.IsExpired)
            {
                continue;
            }

            TryConsiderHeavyDashProjectile(self, needle.Team, needle.OwnerId, needle.Id, needle.X, needle.Y, needle.VelocityX, needle.VelocityY, ref bestScore, ref projectile);
        }

        foreach (var revolverShot in world.RevolverShots)
        {
            if (revolverShot.IsExpired)
            {
                continue;
            }

            TryConsiderHeavyDashProjectile(self, revolverShot.Team, revolverShot.OwnerId, revolverShot.Id, revolverShot.X, revolverShot.Y, revolverShot.VelocityX, revolverShot.VelocityY, ref bestScore, ref projectile);
        }

        return projectile.Id != 0;
    }

    private static void TryConsiderHeavyDashProjectile(
        PlayerEntity self,
        PlayerTeam projectileTeam,
        int ownerId,
        int projectileId,
        float x,
        float y,
        float velocityX,
        float velocityY,
        ref float bestScore,
        ref IncomingReflectProjectile projectile)
    {
        if (projectileTeam == self.Team || ownerId == self.Id)
        {
            return;
        }

        var speedSq = (velocityX * velocityX) + (velocityY * velocityY);
        if (speedSq <= 0.001f)
        {
            return;
        }

        var speed = MathF.Sqrt(speedSq);
        var toPlayerX = self.X - x;
        var toPlayerY = self.Y - y;
        var closingDistance = ((toPlayerX * velocityX) + (toPlayerY * velocityY)) / speed;
        if (closingDistance <= 0f || closingDistance > HeavyDashThreatDetectionDistance)
        {
            return;
        }

        var distanceToProjectile = DistanceBetween(self.X, self.Y, x, y);
        var perpendicularSq = MathF.Max(0f, (distanceToProjectile * distanceToProjectile) - (closingDistance * closingDistance));
        var perpendicularDistance = MathF.Sqrt(perpendicularSq);
        if (perpendicularDistance > HeavyDashThreatLaneRadius)
        {
            return;
        }

        var timeToClosestPointTicks = closingDistance / speed;
        var score = timeToClosestPointTicks + (perpendicularDistance / HeavyDashThreatLaneRadius);
        if (score >= bestScore)
        {
            return;
        }

        bestScore = score;
        projectile = new IncomingReflectProjectile(projectileId, timeToClosestPointTicks);
    }

    private static bool ShouldPyroReflectAccurately(PlayerEntity self, int projectileId)
    {
        return PositiveModulo(HashCode.Combine(self.Id, projectileId, 0x51A7), 100) < 50;
    }

    private static bool ShouldHeavyDashIncomingProjectile(PlayerEntity self, int projectileId)
    {
        return PositiveModulo((self.Id * 31) ^ (projectileId * 131) ^ 0x6D, 100) < 35;
    }

    private static bool ShouldPrimaryWeaponFire(PlayerEntity self, float distanceToTarget)
    {
        if (self.PrimaryCooldownTicks > 0)
        {
            return false;
        }

        if (self.CurrentShells <= 0 && self.ReloadTicksUntilNextShell > 0)
        {
            return false;
        }

        if (self.IsExperimentalDemoknightEnabled)
        {
            return distanceToTarget <= self.GetExperimentalDemoknightSwordRange();
        }

        return self.ClassId is not (PlayerClass.Soldier or PlayerClass.Demoman) || distanceToTarget >= 60f;
    }

    private static bool ShouldSpyUseRevolver(PlayerEntity self, PlayerEntity target)
    {
        return target.IsAlive
            && DistanceBetween(self.X, self.Y, target.X, target.Y) <= SpyRevolverAttackDistance;
    }

    private static bool IsValidSpyBackstabTarget(SimulationWorld world, PlayerEntity self, PlayerEntity target)
    {
        if (DistanceBetween(self.X, self.Y, target.X, target.Y) > SpyBackstabMaxPlanDistance
            || MathF.Abs(self.Y - target.Y) > 90f
            || MathF.Abs(target.HorizontalSpeed) > SpyBackstabStableTargetSpeed
            || !HasLineOfSight(world, self.X, self.Y, target.X, target.Y, self.Team, self.IsCarryingIntel))
        {
            return false;
        }

        foreach (var candidate in EnumeratePlayers(world))
        {
            if (!candidate.IsAlive
                || candidate.Id == target.Id
                || candidate.Team != target.Team)
            {
                continue;
            }

            if (DistanceBetween(candidate.X, candidate.Y, target.X, target.Y) <= SpyBackstabAllyIsolationDistance)
            {
                return false;
            }
        }

        return true;
    }

    private static float ResolveBehindDirection(PlayerEntity target)
    {
        return target.FacingDirectionX >= 0f ? -1f : 1f;
    }

    private static bool ShouldHeavyEat(PlayerEntity self, bool isBeingHealed, int healthThreshold)
    {
        return self.ClassId == PlayerClass.Heavy
            && !isBeingHealed
            && !self.IsHeavyEating
            && self.HeavyEatCooldownTicksRemaining <= 0
            && self.Health <= healthThreshold;
    }

    private static bool ShouldDetonateMines(SimulationWorld world, PlayerEntity self)
    {
        foreach (var mine in world.Mines)
        {
            if (mine.OwnerId != self.Id
                || mine.IsDestroyed
                || DistanceSquared(self.X, self.Y, mine.X, mine.Y) >= MineThreatDistanceSquared)
            {
                continue;
            }

            foreach (var candidate in EnumeratePlayers(world))
            {
                if (candidate.IsAlive
                    && candidate.Id != self.Id
                    && DistanceSquared(candidate.X, candidate.Y, mine.X, mine.Y) <= MineDetonationRadiusSquared)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsPlayerBeingHealed(SimulationWorld world, PlayerEntity self)
    {
        foreach (var candidate in EnumeratePlayers(world))
        {
            if (candidate.IsAlive
                && candidate.Team == self.Team
                && candidate.ClassId == PlayerClass.Medic
                && candidate.IsMedicHealing
                && candidate.MedicHealTargetId == self.Id)
            {
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<PlayerEntity> EnumeratePlayers(SimulationWorld world)
    {
        yield return world.LocalPlayer;
        if (world.EnemyPlayerEnabled)
        {
            yield return world.EnemyPlayer;
        }

        foreach (var slot in SimulationWorld.NetworkPlayerSlots)
        {
            if (world.TryGetNetworkPlayer(slot, out var player))
            {
                yield return player;
            }
        }
    }

    public static bool HasLineOfSight(
        SimulationWorld world,
        float originX,
        float originY,
        float targetX,
        float targetY,
        PlayerTeam team,
        bool carryingIntel)
    {
        var distance = DistanceBetween(originX, originY, targetX, targetY);
        if (distance <= 0.0001f)
        {
            return true;
        }

        var frameCache = LineOfSightFrameCaches.GetValue(world, static _ => new LineOfSightFrameCache());
        var cacheKey = new LineOfSightCacheKey(
            originX,
            originY,
            targetX,
            targetY,
            team,
            carryingIntel,
            world.Level.ControlPointSetupGatesActive,
            world.Level.ForcedBlockingTeamGates);
        if (frameCache.TryGet(world.Frame, world.Level, cacheKey, out var cachedResult))
        {
            return cachedResult;
        }

        var directionX = (targetX - originX) / distance;
        var directionY = (targetY - originY) / distance;
        var lineLeft = MathF.Min(originX, targetX);
        var lineTop = MathF.Min(originY, targetY);
        var lineRight = MathF.Max(originX, targetX);
        var lineBottom = MathF.Max(originY, targetY);
        var levelCache = LineOfSightLevelCaches.GetValue(world.Level, static level => new LineOfSightLevelCache(level));

        var solidCandidates = frameCache.GetSolidCandidates(levelCache, lineLeft, lineTop, lineRight, lineBottom);
        foreach (var solid in solidCandidates)
        {
            if (RectangleBlocksLine(solid.Left, solid.Top, solid.Right, solid.Bottom))
            {
                frameCache.Store(cacheKey, false);
                return false;
            }
        }

        foreach (var gate in levelCache.GetBlockingTeamGates(
            team,
            carryingIntel,
            world.Level.ControlPointSetupGatesActive,
            world.Level.ForcedBlockingTeamGates))
        {
            if (RectangleBlocksLine(gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                frameCache.Store(cacheKey, false);
                return false;
            }
        }

        var staticBlockerCandidates = frameCache.GetStaticRoomObjectBlockerCandidates(levelCache, lineLeft, lineTop, lineRight, lineBottom);
        foreach (var wall in staticBlockerCandidates)
        {
            if (RectangleBlocksLine(wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                frameCache.Store(cacheKey, false);
                return false;
            }
        }

        frameCache.Store(cacheKey, true);
        return true;

        bool RectangleBlocksLine(float left, float top, float right, float bottom)
        {
            return RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, left, top, right, bottom)
                && GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, left, top, right, bottom, distance).HasValue;
        }
    }

    private readonly record struct LineOfSightCacheKey(
        float OriginX,
        float OriginY,
        float TargetX,
        float TargetY,
        PlayerTeam Team,
        bool CarryingIntel,
        bool ControlPointSetupGatesActive,
        TeamGateLockMask ForcedBlockingTeamGates);

    private sealed class LineOfSightFrameCache
    {
        private readonly Dictionary<LineOfSightCacheKey, bool> _results = new();
        private readonly HashSet<int> _solidCandidateIndices = [];
        private readonly List<LevelSolid> _solidCandidates = [];
        private readonly HashSet<int> _staticRoomObjectBlockerCandidateIndices = [];
        private readonly List<RoomObjectMarker> _staticRoomObjectBlockerCandidates = [];
        private long _frame = long.MinValue;
        private SimpleLevel? _level;

        public bool TryGet(long frame, SimpleLevel level, LineOfSightCacheKey key, out bool result)
        {
            Prepare(frame, level);
            return _results.TryGetValue(key, out result);
        }

        public void Store(LineOfSightCacheKey key, bool result)
        {
            _results[key] = result;
        }

        public List<LevelSolid> GetSolidCandidates(
            LineOfSightLevelCache levelCache,
            float left,
            float top,
            float right,
            float bottom)
        {
            _solidCandidateIndices.Clear();
            _solidCandidates.Clear();
            levelCache.AddSolidCandidates(left, top, right, bottom, _solidCandidateIndices, _solidCandidates);
            return _solidCandidates;
        }

        public List<RoomObjectMarker> GetStaticRoomObjectBlockerCandidates(
            LineOfSightLevelCache levelCache,
            float left,
            float top,
            float right,
            float bottom)
        {
            _staticRoomObjectBlockerCandidateIndices.Clear();
            _staticRoomObjectBlockerCandidates.Clear();
            levelCache.AddStaticRoomObjectBlockerCandidates(
                left,
                top,
                right,
                bottom,
                _staticRoomObjectBlockerCandidateIndices,
                _staticRoomObjectBlockerCandidates);
            return _staticRoomObjectBlockerCandidates;
        }

        private void Prepare(long frame, SimpleLevel level)
        {
            if (_frame == frame && ReferenceEquals(_level, level))
            {
                return;
            }

            _frame = frame;
            _level = level;
            _results.Clear();
        }
    }

    private sealed class LineOfSightLevelCache
    {
        private readonly LineOfSightSpatialIndex<LevelSolid> _solidIndex;
        private readonly LineOfSightSpatialIndex<RoomObjectMarker> _staticRoomObjectBlockerIndex;
        private readonly RoomObjectMarker[] _gateCandidates;
        private readonly Dictionary<LineOfSightGateCacheKey, IReadOnlyList<RoomObjectMarker>> _blockingGatesByKey = [];

        public LineOfSightLevelCache(SimpleLevel level)
        {
            _solidIndex = LineOfSightSpatialIndex<LevelSolid>.Build(
                level.Solids,
                static solid => solid.Left,
                static solid => solid.Top,
                static solid => solid.Right,
                static solid => solid.Bottom);
            _staticRoomObjectBlockerIndex = LineOfSightSpatialIndex<RoomObjectMarker>.Build(
                level.RoomObjects
                    .Where(static roomObject => roomObject.Type is RoomObjectType.PlayerWall or RoomObjectType.BulletWall)
                    .ToArray(),
                static roomObject => roomObject.Left,
                static roomObject => roomObject.Top,
                static roomObject => roomObject.Right,
                static roomObject => roomObject.Bottom);
            _gateCandidates = level.RoomObjects
                .Where(static roomObject => roomObject.Type is RoomObjectType.ControlPointSetupGate or RoomObjectType.TeamGate or RoomObjectType.IntelGate)
                .ToArray();
        }

        public void AddSolidCandidates(
            float left,
            float top,
            float right,
            float bottom,
            HashSet<int> seenIndices,
            List<LevelSolid> candidates)
        {
            _solidIndex.AddCandidates(left, top, right, bottom, seenIndices, candidates);
        }

        public void AddStaticRoomObjectBlockerCandidates(
            float left,
            float top,
            float right,
            float bottom,
            HashSet<int> seenIndices,
            List<RoomObjectMarker> candidates)
        {
            _staticRoomObjectBlockerIndex.AddCandidates(left, top, right, bottom, seenIndices, candidates);
        }

        public IReadOnlyList<RoomObjectMarker> GetBlockingTeamGates(
            PlayerTeam team,
            bool carryingIntel,
            bool controlPointSetupGatesActive,
            TeamGateLockMask forcedBlockingTeamGates)
        {
            var key = new LineOfSightGateCacheKey(team, carryingIntel, controlPointSetupGatesActive, forcedBlockingTeamGates);
            if (_blockingGatesByKey.TryGetValue(key, out var cachedGates))
            {
                return cachedGates;
            }

            var blockingGates = new List<RoomObjectMarker>();
            foreach (var roomObject in _gateCandidates)
            {
                switch (roomObject.Type)
                {
                    case RoomObjectType.ControlPointSetupGate:
                        if (controlPointSetupGatesActive)
                        {
                            blockingGates.Add(roomObject);
                        }
                        break;
                    case RoomObjectType.TeamGate:
                        if (roomObject.Team.HasValue && IsForcedBlockingTeamGate(roomObject.Team.Value, forcedBlockingTeamGates))
                        {
                            blockingGates.Add(roomObject);
                            break;
                        }

                        if (carryingIntel || (roomObject.Team.HasValue && roomObject.Team.Value != team))
                        {
                            blockingGates.Add(roomObject);
                        }
                        break;
                    case RoomObjectType.IntelGate:
                        if (IsIntelGateBlocking(roomObject, team, carryingIntel))
                        {
                            blockingGates.Add(roomObject);
                        }
                        break;
                }
            }

            var result = blockingGates.Count == 0
                ? Array.Empty<RoomObjectMarker>()
                : blockingGates.ToArray();
            _blockingGatesByKey[key] = result;
            return result;
        }

        private static bool IsForcedBlockingTeamGate(PlayerTeam team, TeamGateLockMask forcedBlockingTeamGates)
        {
            return team switch
            {
                PlayerTeam.Red => (forcedBlockingTeamGates & TeamGateLockMask.Red) != 0,
                PlayerTeam.Blue => (forcedBlockingTeamGates & TeamGateLockMask.Blue) != 0,
                _ => false,
            };
        }

        private static bool IsIntelGateBlocking(RoomObjectMarker roomObject, PlayerTeam team, bool carryingIntel)
        {
            if (carryingIntel)
            {
                return false;
            }

            if (roomObject.Team.HasValue)
            {
                return roomObject.Team.Value != team;
            }

            return true;
        }
    }

    private readonly record struct LineOfSightGateCacheKey(
        PlayerTeam Team,
        bool CarryingIntel,
        bool ControlPointSetupGatesActive,
        TeamGateLockMask ForcedBlockingTeamGates);

    private sealed class LineOfSightSpatialIndex<T>
    {
        private const float CellSize = 128f;
        private readonly IReadOnlyList<T> _items;
        private readonly Dictionary<CellKey, List<int>> _itemIndicesByCell;

        private LineOfSightSpatialIndex(IReadOnlyList<T> items, Dictionary<CellKey, List<int>> itemIndicesByCell)
        {
            _items = items;
            _itemIndicesByCell = itemIndicesByCell;
        }

        public static LineOfSightSpatialIndex<T> Build(
            IReadOnlyList<T> items,
            Func<T, float> getLeft,
            Func<T, float> getTop,
            Func<T, float> getRight,
            Func<T, float> getBottom)
        {
            var itemIndicesByCell = new Dictionary<CellKey, List<int>>();
            for (var itemIndex = 0; itemIndex < items.Count; itemIndex += 1)
            {
                var item = items[itemIndex];
                var minCellX = GetCellCoordinate(getLeft(item));
                var maxCellX = GetCellCoordinate(getRight(item));
                var minCellY = GetCellCoordinate(getTop(item));
                var maxCellY = GetCellCoordinate(getBottom(item));
                for (var cellY = minCellY; cellY <= maxCellY; cellY += 1)
                {
                    for (var cellX = minCellX; cellX <= maxCellX; cellX += 1)
                    {
                        var key = new CellKey(cellX, cellY);
                        if (!itemIndicesByCell.TryGetValue(key, out var indices))
                        {
                            indices = [];
                            itemIndicesByCell[key] = indices;
                        }

                        indices.Add(itemIndex);
                    }
                }
            }

            return new LineOfSightSpatialIndex<T>(items, itemIndicesByCell);
        }

        public void AddCandidates(
            float left,
            float top,
            float right,
            float bottom,
            HashSet<int> seenIndices,
            List<T> candidates)
        {
            if (_items.Count == 0)
            {
                return;
            }

            var minCellX = GetCellCoordinate(left);
            var maxCellX = GetCellCoordinate(right);
            var minCellY = GetCellCoordinate(top);
            var maxCellY = GetCellCoordinate(bottom);
            for (var cellY = minCellY; cellY <= maxCellY; cellY += 1)
            {
                for (var cellX = minCellX; cellX <= maxCellX; cellX += 1)
                {
                    if (!_itemIndicesByCell.TryGetValue(new CellKey(cellX, cellY), out var itemIndices))
                    {
                        continue;
                    }

                    for (var index = 0; index < itemIndices.Count; index += 1)
                    {
                        var itemIndex = itemIndices[index];
                        if (seenIndices.Add(itemIndex))
                        {
                            candidates.Add(_items[itemIndex]);
                        }
                    }
                }
            }
        }

        private static int GetCellCoordinate(float value)
        {
            return (int)MathF.Floor(value / CellSize);
        }

        private readonly record struct CellKey(int X, int Y);
    }

    private static float? GetRayIntersectionDistanceWithRectangle(
        float originX,
        float originY,
        float directionX,
        float directionY,
        float left,
        float top,
        float right,
        float bottom,
        float maxDistance)
    {
        const float epsilon = 0.0001f;
        float tMin;
        float tMax;
        if (MathF.Abs(directionX) < epsilon)
        {
            if (originX < left || originX > right)
            {
                return null;
            }

            tMin = float.NegativeInfinity;
            tMax = float.PositiveInfinity;
        }
        else
        {
            var invDirectionX = 1f / directionX;
            var tx1 = (left - originX) * invDirectionX;
            var tx2 = (right - originX) * invDirectionX;
            tMin = MathF.Min(tx1, tx2);
            tMax = MathF.Max(tx1, tx2);
        }

        float tyMin;
        float tyMax;
        if (MathF.Abs(directionY) < epsilon)
        {
            if (originY < top || originY > bottom)
            {
                return null;
            }

            tyMin = float.NegativeInfinity;
            tyMax = float.PositiveInfinity;
        }
        else
        {
            var invDirectionY = 1f / directionY;
            var ty1 = (top - originY) * invDirectionY;
            var ty2 = (bottom - originY) * invDirectionY;
            tyMin = MathF.Min(ty1, ty2);
            tyMax = MathF.Max(ty1, ty2);
        }

        var entryDistance = MathF.Max(tMin, tyMin);
        var exitDistance = MathF.Min(tMax, tyMax);
        if (exitDistance < 0f || entryDistance > exitDistance || entryDistance > maxDistance)
        {
            return null;
        }

        return entryDistance < 0f ? 0f : entryDistance;
    }

    private static bool RectanglesOverlap(float leftA, float topA, float rightA, float bottomA, float leftB, float topB, float rightB, float bottomB)
    {
        return leftA <= rightB
            && rightA >= leftB
            && topA <= bottomB
            && bottomA >= topB;
    }

    private static float GetHealthFraction(PlayerEntity player)
    {
        return player.MaxHealth <= 0 ? 0f : Math.Clamp(player.Health / (float)player.MaxHealth, 0f, 1f);
    }

    private static float DistanceBetween(float ax, float ay, float bx, float by)
    {
        return MathF.Sqrt(DistanceSquared(ax, ay, bx, by));
    }

    private static float DistanceSquared(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return (dx * dx) + (dy * dy);
    }

    private static int PositiveModulo(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }
}
