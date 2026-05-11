using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core.BotBrain;

public sealed class CombatDecisionMemory
{
    public int BeenHealingTicks { get; set; }

    public int BeenHealingSwitchTicks { get; set; } = 20;

    public int ReloadCounterTicks { get; set; }

    public int ZoomToShootTicks { get; set; } = 50;

    public int LastPyroReflectProjectileId { get; set; } = -1;
}

public readonly record struct CombatFireDecision(
    bool FirePrimary,
    bool FireSecondary,
    bool UseAbility);

public enum MedicHealTargetSelectionKind
{
    None,
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
    private const int HeavyIdleEatHealth = 100;
    private const int HeavyCombatEatHealth = 30;
    private const float CloseCombatDistance = 170f;
    private const float SniperDangerDistance = 300f;
    private const float SoldierShotgunDistance = 260f;
    private const float MineThreatDistance = 400f;
    private const float MineDetonationRadius = 50f;
    private const float MedicHealTargetMaxDistance = 300f;
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

    public static PlayerEntity? FindBestMedicHealTarget(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team)
    {
        return FindBestMedicHealTargetSelection(world, self, team).Target;
    }

    public static MedicHealTargetSelection FindBestMedicHealTargetSelection(
        SimulationWorld world,
        PlayerEntity self,
        PlayerTeam team)
    {
        if (self.ClassId != PlayerClass.Medic)
        {
            return default;
        }

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
                || !HasLineOfSight(world, self.X, self.Y, candidate.X, candidate.Y, self.Team, self.IsCarryingIntel))
            {
                continue;
            }

            var distance = DistanceBetween(self.X, self.Y, candidate.X, candidate.Y);
            if (distance > MedicHealTargetMaxDistance)
            {
                continue;
            }

            var healthFraction = GetHealthFraction(candidate);
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

        if (bestCriticalTarget is not null)
        {
            return new MedicHealTargetSelection(bestCriticalTarget, MedicHealTargetSelectionKind.Critical);
        }

        return bestPocketTarget is not null
            ? new MedicHealTargetSelection(bestPocketTarget, MedicHealTargetSelectionKind.Pocket)
            : default;
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
        var useAbility = ResolveAbilityInputFromLoadout(self, combatTarget, healTarget, firePrimary, fireSecondary);
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
        PlayerEntity self,
        BotBrainCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        bool firePrimary,
        bool fireSecondary)
    {
        if (self.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber))
        {
            return self.IsMedicUberReady && (self.Health < 40 || healTarget is { Health: < 50 });
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

        return false;
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

    private static bool ShouldPyroReflectAccurately(PlayerEntity self, int projectileId)
    {
        return PositiveModulo(HashCode.Combine(self.Id, projectileId, 0x51A7), 100) < 50;
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
                || DistanceBetween(self.X, self.Y, mine.X, mine.Y) >= MineThreatDistance)
            {
                continue;
            }

            foreach (var candidate in EnumeratePlayers(world))
            {
                if (candidate.IsAlive
                    && candidate.Id != self.Id
                    && DistanceBetween(candidate.X, candidate.Y, mine.X, mine.Y) <= MineDetonationRadius)
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

        var directionX = (targetX - originX) / distance;
        var directionY = (targetY - originY) / distance;
        var lineLeft = MathF.Min(originX, targetX);
        var lineTop = MathF.Min(originY, targetY);
        var lineRight = MathF.Max(originX, targetX);
        var lineBottom = MathF.Max(originY, targetY);

        foreach (var solid in world.Level.Solids)
        {
            if (RectangleBlocksLine(solid.Left, solid.Top, solid.Right, solid.Bottom))
            {
                return false;
            }
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(team, carryingIntel))
        {
            if (RectangleBlocksLine(gate.Left, gate.Top, gate.Right, gate.Bottom))
            {
                return false;
            }
        }

        foreach (var wall in world.Level.RoomObjects)
        {
            if ((wall.Type == RoomObjectType.PlayerWall || wall.Type == RoomObjectType.BulletWall)
                && RectangleBlocksLine(wall.Left, wall.Top, wall.Right, wall.Bottom))
            {
                return false;
            }
        }

        return true;

        bool RectangleBlocksLine(float left, float top, float right, float bottom)
        {
            return RectanglesOverlap(lineLeft, lineTop, lineRight, lineBottom, left, top, right, bottom)
                && GetRayIntersectionDistanceWithRectangle(originX, originY, directionX, directionY, left, top, right, bottom, distance).HasValue;
        }
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
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    private static int PositiveModulo(int value, int modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }
}
