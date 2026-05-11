using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core.BotBrain.Reference;

/// <summary>
/// Dormant reference copy of the useful combat decisions from the removed ModernGraphRoute practice bot.
/// This type is intentionally not used by runtime BotBrain code.
/// </summary>
internal static class ModernGraphRouteCombatReference
{
    private const float ModernEnemySeeDistance = 375f;
    private const float ModernSniperDangerDistance = 150f;
    private const int ModernHeavyIdleEatHealth = 100;
    private const int ModernHeavyCombatEatHealth = 30;
    private const float ModernCloseCombatDistance = 170f;
    private const float ModernSoldierShotgunDistance = 260f;
    private const float ModernMineThreatDistance = 400f;
    private const float ModernMineDetonationRadius = 50f;

    internal static ModernCombatTarget? FindBestCombatTarget(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers,
        IReadOnlyDictionary<int, int> spyVisibleTicksByPlayerId,
        int spyReactTicksThreshold)
    {
        ModernCombatTarget? bestTarget = null;
        var bestDistance = ModernEnemySeeDistance;

        for (var index = 0; index < world.Generators.Count; index += 1)
        {
            var candidate = world.Generators[index];
            if (candidate.Team == team || candidate.IsDestroyed)
            {
                continue;
            }

            var candidateX = candidate.Marker.CenterX;
            var candidateY = candidate.Marker.CenterY;
            if (!HasLineOfSight(world, player.X, player.Y, candidateX, candidateY, player.Team, player.IsCarryingIntel))
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidateX, candidateY);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = new ModernCombatTarget(ModernCombatTargetKind.Generator, candidate.Team, candidateX, candidateY, Generator: candidate);
        }

        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            var treatAsFriendlyFireTarget = SimulationWorld.ShouldTreatPlayerAsExperimentalFriendlyFireTarget(player, candidate);
            if (!candidate.IsAlive
                || (candidate.Team == team && !treatAsFriendlyFireTarget)
                || ShouldIgnoreSpyTarget(candidate, spyVisibleTicksByPlayerId, spyReactTicksThreshold))
            {
                continue;
            }

            if (!HasLineOfSight(world, player.X, player.Y, candidate.X, candidate.Y, player.Team, player.IsCarryingIntel))
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = new ModernCombatTarget(ModernCombatTargetKind.Player, candidate.Team, candidate.X, candidate.Y, Player: candidate);
        }

        for (var index = 0; index < world.Sentries.Count; index += 1)
        {
            var candidate = world.Sentries[index];
            if (candidate.Team == team)
            {
                continue;
            }

            if (!HasLineOfSight(world, player.X, player.Y, candidate.X, candidate.Y, player.Team, player.IsCarryingIntel))
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, candidate.Y);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = new ModernCombatTarget(ModernCombatTargetKind.Sentry, candidate.Team, candidate.X, candidate.Y, Sentry: candidate);
        }

        return bestTarget;
    }

    internal static PlayerEntity? FindBestMedicHealTarget(
        SimulationWorld world,
        PlayerEntity player,
        PlayerTeam team,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        if (player.ClassId != PlayerClass.Medic)
        {
            return null;
        }

        PlayerEntity? bestTarget = null;
        var bestScore = float.PositiveInfinity;
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (!candidate.IsAlive
                || candidate.Team != team
                || ReferenceEquals(candidate, player)
                || !HasLineOfSight(world, player.X, player.Y, candidate.X, candidate.Y, player.Team, player.IsCarryingIntel))
            {
                continue;
            }

            var distance = DistanceBetween(player.X, player.Y, candidate.X, candidate.Y);
            var score = (distance / 1000f) + (GetHealthFraction(candidate) * 2f);
            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestTarget = candidate;
        }

        return bestTarget;
    }

    internal static ModernFireDecision ResolveFireDecision(
        SimulationWorld world,
        PlayerEntity player,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        IReadOnlyList<PlayerEntity> allPlayers,
        ModernCombatMemory memory,
        bool isBeingHealed)
    {
        var firePrimary = ResolvePrimaryFire(player, combatTarget, healTarget, memory, isBeingHealed);
        var fireSecondary = ResolveSecondaryFire(world, player, combatTarget, healTarget, allPlayers, memory, isBeingHealed);
        var fireAbility = ResolveAbilityInputFromLoadout(
            player,
            combatTarget?.X,
            combatTarget?.Y,
            healTarget,
            firePrimary,
            fireSecondary);

        ApplyReloadDiscipline(player, memory, ref firePrimary, ref fireSecondary, ref fireAbility);
        return new ModernFireDecision(firePrimary, fireSecondary, fireAbility);
    }

    internal static bool ResolveAbilityInputFromLoadout(
        PlayerEntity player,
        float? combatTargetX,
        float? combatTargetY,
        PlayerEntity? healTarget,
        bool firePrimary,
        bool fireSecondary)
    {
        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.MedicUber))
        {
            return player.IsMedicUberReady
                && (player.Health < 40 || healTarget is { Health: < 50 });
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.PyroUtility))
        {
            return combatTargetX.HasValue
                && combatTargetY.HasValue
                && player.CanFirePyroAirblast()
                && DistanceBetween(player.X, player.Y, combatTargetX.Value, combatTargetY.Value) <= ModernCloseCombatDistance;
        }

        if (player.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SoldierSecondaryWeapon))
        {
            return combatTargetX.HasValue
                && combatTargetY.HasValue
                && !fireSecondary
                && (!firePrimary || player.CurrentShells <= 0)
                && player.ExperimentalOffhandCurrentShells > 0
                && DistanceBetween(player.X, player.Y, combatTargetX.Value, combatTargetY.Value) <= ModernSoldierShotgunDistance;
        }

        return false;
    }

    internal static bool IsPlayerBeingHealed(IReadOnlyList<PlayerEntity> allPlayers, PlayerEntity player)
    {
        for (var index = 0; index < allPlayers.Count; index += 1)
        {
            var candidate = allPlayers[index];
            if (candidate.IsAlive
                && candidate.Team == player.Team
                && candidate.ClassId == PlayerClass.Medic
                && candidate.IsMedicHealing
                && candidate.MedicHealTargetId == player.Id)
            {
                return true;
            }
        }

        return false;
    }

    internal static void UpdateReloadMemory(PlayerEntity player, ModernCombatMemory memory)
    {
        if (memory.ReloadCounterTicks > 0)
        {
            memory.ReloadCounterTicks -= 1;
            if (player.CurrentShells >= player.PrimaryWeapon.MaxAmmo)
            {
                memory.ReloadCounterTicks = 0;
            }

            return;
        }

        if (player.PrimaryWeapon.Kind == PrimaryWeaponKind.Rifle)
        {
            return;
        }

        if (player.CurrentShells <= 0)
        {
            memory.ReloadCounterTicks = (3 * player.PrimaryWeapon.ReloadDelayTicks) + player.PrimaryWeapon.AmmoReloadTicks;
            return;
        }

        if (player.PrimaryWeapon.Kind == PrimaryWeaponKind.FlameThrower && player.CurrentShells < 3)
        {
            memory.ReloadCounterTicks = 4 * player.PrimaryWeapon.AmmoReloadTicks;
            return;
        }

        if (player.PrimaryWeapon.Kind == PrimaryWeaponKind.Minigun && player.CurrentShells < 3)
        {
            memory.ReloadCounterTicks = 6 * player.PrimaryWeapon.AmmoReloadTicks;
        }
    }

    private static bool ResolvePrimaryFire(
        PlayerEntity player,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        ModernCombatMemory memory,
        bool isBeingHealed)
    {
        if (player.ClassId == PlayerClass.Medic)
        {
            memory.BeenHealingTicks += 1;
            var firePrimary = true;
            if (healTarget is not null
                && player.MedicHealTargetId.HasValue
                && player.MedicHealTargetId.Value != healTarget.Id
                && memory.BeenHealingTicks > memory.BeenHealingSwitchTicks)
            {
                memory.BeenHealingTicks = 0;
                firePrimary = false;
            }

            return healTarget is not null || combatTarget is null && firePrimary;
        }

        memory.BeenHealingTicks = 0;
        if (combatTarget is null)
        {
            return false;
        }

        if (player.ClassId == PlayerClass.Heavy && ShouldHeavyEat(player, isBeingHealed, ModernHeavyCombatEatHealth))
        {
            return false;
        }

        if (player.ClassId == PlayerClass.Sniper && player.IsSniperScoped)
        {
            if (player.SniperChargeTicks >= memory.ZoomToShootTicks)
            {
                memory.ZoomToShootTicks = Math.Max(1, memory.ZoomToShootTicks);
                return true;
            }

            return false;
        }

        return true;
    }

    private static bool ResolveSecondaryFire(
        SimulationWorld world,
        PlayerEntity player,
        ModernCombatTarget? combatTarget,
        PlayerEntity? healTarget,
        IReadOnlyList<PlayerEntity> allPlayers,
        ModernCombatMemory memory,
        bool isBeingHealed)
    {
        if (player.ClassId == PlayerClass.Medic)
        {
            if (healTarget is not null)
            {
                return player.IsMedicUberReady && (healTarget.Health < 50 || player.Health < 40);
            }

            return combatTarget is not null;
        }

        if (player.ClassId == PlayerClass.Heavy)
        {
            return ShouldHeavyEat(
                player,
                isBeingHealed,
                combatTarget is null ? ModernHeavyIdleEatHealth : ModernHeavyCombatEatHealth);
        }

        if (player.ClassId == PlayerClass.Sniper)
        {
            if (combatTarget is null)
            {
                return player.IsSniperScoped;
            }

            var distance = DistanceBetween(player.X, player.Y, combatTarget.Value.X, combatTarget.Value.Y);
            if (distance >= ModernSniperDangerDistance)
            {
                return !player.IsSniperScoped;
            }

            if (player.IsSniperScoped)
            {
                memory.ZoomToShootTicks = Math.Max(1, memory.ZoomToShootTicks);
                return true;
            }

            return false;
        }

        return player.ClassId == PlayerClass.Demoman
            && ShouldDetonateMines(world, player, allPlayers);
    }

    private static bool ShouldHeavyEat(PlayerEntity player, bool isBeingHealed, int healthThreshold)
    {
        return player.ClassId == PlayerClass.Heavy
            && !isBeingHealed
            && !player.IsHeavyEating
            && player.HeavyEatCooldownTicksRemaining <= 0
            && player.Health <= healthThreshold;
    }

    private static bool ShouldDetonateMines(
        SimulationWorld world,
        PlayerEntity player,
        IReadOnlyList<PlayerEntity> allPlayers)
    {
        for (var mineIndex = 0; mineIndex < world.Mines.Count; mineIndex += 1)
        {
            var mine = world.Mines[mineIndex];
            if (mine.OwnerId != player.Id
                || mine.IsDestroyed
                || DistanceBetween(player.X, player.Y, mine.X, mine.Y) >= ModernMineThreatDistance)
            {
                continue;
            }

            for (var playerIndex = 0; playerIndex < allPlayers.Count; playerIndex += 1)
            {
                var candidate = allPlayers[playerIndex];
                if (candidate.IsAlive
                    && candidate.Id != player.Id
                    && DistanceBetween(candidate.X, candidate.Y, mine.X, mine.Y) <= ModernMineDetonationRadius)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ApplyReloadDiscipline(
        PlayerEntity player,
        ModernCombatMemory memory,
        ref bool firePrimary,
        ref bool fireSecondary,
        ref bool fireAbility)
    {
        if (memory.ReloadCounterTicks <= 0)
        {
            return;
        }

        if (player.ClassId == PlayerClass.Medic)
        {
            fireSecondary = false;
            fireAbility = false;
            return;
        }

        firePrimary = false;
    }

    private static bool ShouldIgnoreSpyTarget(
        PlayerEntity player,
        IReadOnlyDictionary<int, int> spyVisibleTicksByPlayerId,
        int spyReactTicksThreshold)
    {
        return player.ClassId == PlayerClass.Spy
            && (!spyVisibleTicksByPlayerId.TryGetValue(player.Id, out var visibleTicks)
                || visibleTicks < spyReactTicksThreshold);
    }

    private static bool HasLineOfSight(
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
        return player.MaxHealth <= 0
            ? 0f
            : Math.Clamp(player.Health / (float)player.MaxHealth, 0f, 1f);
    }

    private static float DistanceBetween(float ax, float ay, float bx, float by)
    {
        var dx = bx - ax;
        var dy = by - ay;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    internal sealed class ModernCombatMemory
    {
        public int BeenHealingTicks { get; set; }
        public int BeenHealingSwitchTicks { get; set; } = 20;
        public int ReloadCounterTicks { get; set; }
        public int ZoomToShootTicks { get; set; } = 50;
    }

    internal readonly record struct ModernFireDecision(bool FirePrimary, bool FireSecondary, bool UseAbility);

    internal enum ModernCombatTargetKind
    {
        Player,
        Sentry,
        Generator,
    }

    internal readonly record struct ModernCombatTarget(
        ModernCombatTargetKind Kind,
        PlayerTeam Team,
        float X,
        float Y,
        PlayerEntity? Player = null,
        SentryEntity? Sentry = null,
        GeneratorState? Generator = null);
}
