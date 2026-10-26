namespace OpenGarrison.Core;

public enum KingpinSpecial { ShoulderBash, Uppercut, DiveBodyslam, Beatdown }
public enum KingpinAction { None, Jab1, Jab2, Uppercut, ShoulderBash, DiveBodyslam, Beatdown }

/// <summary>Temporary melee prototype, isolated from the stock Heavy's loadout and rules.</summary>
public static class KingpinPrototype
{
    public const string GameplayClassId = "kingpin";
    public const string PrimaryBehaviorId = "builtin.weapon.kingpin";
    public const string SpecialBehaviorId = "builtin.ability.kingpin";
    public const string StateOwner = "kingpin.prototype";
    // The prototype art uses one source pixel for every two stock-art pixels.
    public const float BodySpriteScale = 2f;
    public const float MeterPerDamageDealt = 0.5f;
    public const float MeterPerDamageTaken = 0.25f;
    private static readonly string[] Keys = ["meter", "special", "action", "ticks", "duration", "cooldown", "combo", "guard", "guardAge", "guardCooldown", "parry", "pulse", "rate"];

    public static bool IsKingpin(PlayerEntity player) => player.GameplayClassId == GameplayClassId;
    public static int Read(PlayerEntity player, string key) => player.TryGetReplicatedStateInt(StateOwner, key, out var value) ? value : 0;
    private static void Write(PlayerEntity player, string key, int value) => player.SetReplicatedStateInt(StateOwner, key, value);
    public static float Meter(PlayerEntity player) => player.TryGetReplicatedStateFloat(StateOwner, "meter", out var value) ? Math.Clamp(value, 0f, 100f) : 0f;
    public static void SetMeter(PlayerEntity player, float value) => player.SetReplicatedStateFloat(StateOwner, "meter", float.IsFinite(value) ? Math.Clamp(value, 0f, 100f) : 0f);
    public static KingpinSpecial SelectedSpecial(PlayerEntity player) => (KingpinSpecial)Math.Clamp(Read(player, "special"), 0, 3);
    public static string SpecialName(KingpinSpecial special) => special switch
    {
        KingpinSpecial.Uppercut => "Uppercut (25%)",
        KingpinSpecial.DiveBodyslam => "Dive bodyslam (50%)",
        KingpinSpecial.Beatdown => "Beatdown (100%)",
        _ => "Shoulder bash (free)",
    };
    public static int SpecialCost(KingpinSpecial special) => special switch
    { KingpinSpecial.Uppercut => 25, KingpinSpecial.DiveBodyslam => 50, KingpinSpecial.Beatdown => 100, _ => 0 };

    internal static void Reset(PlayerEntity player)
    {
        player.KingpinBashHitPlayers.Clear();
        player.KingpinBashHitZones.Clear();
        foreach (var key in Keys) player.ClearReplicatedState(StateOwner, key);
    }

    internal static void Advance(SimulationWorld world, PlayerEntity player, PlayerInputSnapshot input, PlayerInputSnapshot previous, bool prediction = false)
    {
        if (!IsKingpin(player) || !player.IsAlive) return;
        var rate = world.Config.TicksPerSecond;
        Write(player, "rate", rate);
        foreach (var key in new[] { "ticks", "cooldown", "guardCooldown", "parry" })
            if (Read(player, key) > 0) Write(player, key, Read(player, key) - 1);
        if (input.UseAbility && !previous.UseAbility) Write(player, "special", (Read(player, "special") + 3) % 4);
        if (input.SwapWeapon && !previous.SwapWeapon) Write(player, "special", (Read(player, "special") + 1) % 4);

        var facing = Math.Sign(input.AimWorldX - player.X);
        if (facing == 0) facing = player.FacingDirectionX >= 0 ? 1 : -1;
        var movement = (input.Right ? 1 : 0) - (input.Left ? 1 : 0);
        var guarding = movement * facing < 0 && !input.FirePrimary && !input.FireSecondary
            && !player.IsTaunting && !player.IsExperimentalCryoFrozen && !player.IsUsingBinoculars
            && Read(player, "ticks") == 0;
        if (guarding && Read(player, "guard") == 0)
        {
            Write(player, "guardAge", Read(player, "guardCooldown") == 0 ? 0 : rate);
            if (Read(player, "guardCooldown") == 0) Write(player, "guardCooldown", Math.Max(1, rate));
        }
        else if (guarding) Write(player, "guardAge", Read(player, "guardAge") + 1);
        Write(player, "guard", guarding ? 1 : 0);

        var action = (KingpinAction)Read(player, "action");
        var elapsed = Read(player, "duration") - Read(player, "ticks");
        if (Read(player, "ticks") == 0 && action != KingpinAction.None)
        {
            Write(player, "action", (int)KingpinAction.None);
            action = KingpinAction.None;
        }
        if (action == KingpinAction.ShoulderBash && Read(player, "ticks") > 0)
            Hit(world, player, 35f, 42f, shove: 240f, bash: true, prediction: prediction);
        else if (action == KingpinAction.DiveBodyslam && Read(player, "ticks") > 0)
        {
            if (elapsed >= Math.Max(1, rate / 5))
            {
                if (player.IsGrounded)
                {
                    Hit(world, player, 70f, 75f, radial: true, lift: 220f, prediction: prediction);
                    SetAction(player, KingpinAction.None, 0);
                }
                else player.ApplyVelocityImpulse(player.HorizontalSpeed, Math.Max(player.VerticalSpeed, 450f));
            }
        }
        else if (action == KingpinAction.Beatdown && Read(player, "ticks") > 0)
        {
            var pulse = Math.Min(3, elapsed * 4 / Math.Max(1, Read(player, "duration")));
            if (pulse > Read(player, "pulse"))
            {
                Write(player, "pulse", pulse);
                Hit(world, player, 25f, 38f, prediction: prediction);
            }
        }
    }

    private static void SetAction(PlayerEntity player, KingpinAction action, int ticks)
    {
        Write(player, "action", (int)action);
        Write(player, "duration", ticks);
        Write(player, "ticks", ticks);
        Write(player, "guard", 0);
        Write(player, "parry", 0);
    }

    internal static GameplayPrimaryWeaponResult Punch(GameplayPrimaryWeaponContext context)
        => Punch(context.World, context.Player);

    internal static GameplayPrimaryWeaponResult Punch(SimulationWorld world, PlayerEntity player, bool prediction = false)
    {
        if (!IsKingpin(player)) return GameplayPrimaryWeaponResult.Ignored;
        if (!player.IsAlive || player.IsTaunting || player.IsExperimentalCryoFrozen || Read(player, "ticks") > 0)
            return GameplayPrimaryWeaponResult.HandledResult;
        var combo = 1 - Read(player, "combo");
        Write(player, "combo", combo);
        SetAction(player, combo == 1 ? KingpinAction.Jab1 : KingpinAction.Jab2,
            Math.Max(2, world.Config.TicksPerSecond / 4));
        Hit(world, player, 30f, 34f, prediction: prediction);
        return GameplayPrimaryWeaponResult.HandledResult;
    }

    internal static GameplayAbilityResult UseSpecial(GameplayAbilityContext context)
        => UseSpecial(context.World, context.Player);

    internal static GameplayAbilityResult UseSpecial(SimulationWorld world, PlayerEntity player, bool prediction = false)
    {
        if (!IsKingpin(player)) return GameplayAbilityResult.Ignored;
        if (!player.IsAlive || player.IsTaunting || player.IsExperimentalCryoFrozen
            || Read(player, "ticks") > 0 || Read(player, "cooldown") > 0)
            return GameplayAbilityResult.HandledAndConsumed;
        var special = SelectedSpecial(player);
        var cost = SpecialCost(special);
        if (Meter(player) < cost) return GameplayAbilityResult.HandledAndConsumed;
        SetMeter(player, Meter(player) - cost);
        var rate = world.Config.TicksPerSecond;
        var facing = player.AimWorldX >= player.X ? 1f : -1f;
        Write(player, "cooldown", rate);
        switch (special)
        {
            case KingpinSpecial.ShoulderBash:
                player.KingpinBashHitPlayers.Clear();
                player.KingpinBashHitZones.Clear();
                SetAction(player, KingpinAction.ShoulderBash, Math.Max(2, rate / 3));
                player.ApplyVelocityImpulse(facing * 300f, Math.Min(player.VerticalSpeed, -40f));
                Hit(world, player, 35f, 42f, shove: 240f, bash: true, prediction: prediction);
                break;
            case KingpinSpecial.Uppercut:
                SetAction(player, KingpinAction.Uppercut, Math.Max(2, rate / 2));
                player.ApplyVelocityImpulse(player.HorizontalSpeed, -280f);
                Hit(world, player, 45f, 38f, lift: 360f, prediction: prediction);
                break;
            case KingpinSpecial.DiveBodyslam:
                SetAction(player, KingpinAction.DiveBodyslam, rate * 2);
                player.ApplyVelocityImpulse(player.HorizontalSpeed, player.IsGrounded ? -260f : 450f);
                break;
            case KingpinSpecial.Beatdown:
                SetAction(player, KingpinAction.Beatdown, rate);
                Write(player, "pulse", 0);
                Hit(world, player, 25f, 38f, prediction: prediction);
                break;
        }
        return GameplayAbilityResult.HandledAndConsumed;
    }

    private static void Hit(SimulationWorld world, PlayerEntity attacker, float damage, float reach, bool radial = false, float lift = 0f, float shove = 80f, bool bash = false, bool prediction = false)
    {
        // Predict poses and movement, but only the server may apply hits or rewards.
        if (prediction || world.ClientPredictionMode) return;
        var facing = attacker.AimWorldX >= attacker.X ? 1f : -1f;
        var originY = (attacker.Top + attacker.Bottom) * 0.5f;
        var radius = reach * attacker.PlayerScale;
        bool InRange(float left, float top, float right, float bottom)
        {
            if (!radial && (right - attacker.X) * facing < 0f && (left - attacker.X) * facing < 0f) return false;
            var dx = Math.Clamp(attacker.X, left, right) - attacker.X;
            var dy = Math.Clamp(originY, top, bottom) - originY;
            return dx * dx + dy * dy <= radius * radius;
        }
        foreach (var target in ((ISimulationPlayerDirectory)world).EnumerateSimulatedPlayers().ToArray())
        {
            if (!target.IsAlive || target.Id == attacker.Id || !world.DamageRules.CanTeamDamagePlayer(attacker.Team, attacker.Id, target)
                || !InRange(target.Left, target.Top, target.Right, target.Bottom)
                || !world.GeometryResolver.HasDirectLineOfSight(attacker.X, originY, target.X, (target.Top + target.Bottom) * 0.5f, target.Team)) continue;
            if (bash && !attacker.KingpinBashHitPlayers.Add(target.Id)) continue;
            var health = target.Health;
            var fatal = world.Combat.ApplyPlayerContinuousDamageWithContext(target, damage, attacker,
                PlayerEntity.SpyDamageRevealAlpha, additionalTraits: PlayerDamageTraits.Melee);
            if (target.Health < health && !target.IsUbered)
                target.ApplyVelocityImpulse(target.HorizontalSpeed + facing * shove, Math.Min(target.VerticalSpeed, -lift));
            if (fatal) world.PlayerDeaths.KillPlayer(target, killer: attacker, weaponSpriteName: "WhipKL");
        }
        foreach (var index in world.Level.GetRoomObjectIndices(RoomObjectType.DamageableZone))
        {
            var zone = world.Level.GetRoomObject(index);
            if (InRange(zone.Left, zone.Top, zone.Right, zone.Bottom)
                && world.GeometryResolver.HasObstacleLineOfSight(attacker.X, originY, zone.CenterX, zone.CenterY))
            {
                if (bash && !attacker.KingpinBashHitZones.Add(index)) continue;
                world.MapLogic.TryApplyDamageableZoneDamage(index, damage, attacker.Team);
            }
        }
    }

    internal static float ApplyProjectileGuard(PlayerEntity target, float damage, in PlayerDamageRequest request)
    {
        if (!IsKingpin(target) || !target.IsAlive || target.IsUbered || Read(target, "guard") == 0 || damage <= 0f
            || request.Traits.HasFlag(PlayerDamageTraits.Melee) || request.Traits.HasFlag(PlayerDamageTraits.Periodic)
            || (request.Traits & (PlayerDamageTraits.DirectProjectile | PlayerDamageTraits.Bullet | PlayerDamageTraits.Explosive)) == 0)
            return damage;
        var sourceX = request.Umbrella.ThreatSourceX ?? request.Attacker?.X;
        var facing = target.AimWorldX >= target.X ? 1f : -1f;
        // Guard is driven by retreat input. Velocity can still point forward while
        // braking, or be zero when retreating against a wall.
        if (!sourceX.HasValue || (sourceX.Value - target.X) * facing <= 0f) return damage;
        var age = Read(target, "guardAge");
        var rate = Math.Max(1, Read(target, "rate"));
        var reduction = age < Math.Max(1, rate / 8) ? 0.9f : age < Math.Max(2, rate / 3) ? 0.5f : 1f / 3f;
        var prevented = damage * reduction;
        SetMeter(target, Meter(target) + prevented * 0.5f);
        if (reduction >= 0.9f) Write(target, "parry", Math.Max(1, rate / 5));
        return damage - prevented;
    }

    internal static void RegisterCombatDamage(PlayerEntity attacker, PlayerEntity target, int damage)
    {
        if (damage <= 0 || attacker.Id == target.Id || attacker.Team == target.Team) return;
        if (IsKingpin(attacker)) SetMeter(attacker, Meter(attacker) + damage * MeterPerDamageDealt);
        if (IsKingpin(target)) SetMeter(target, Meter(target) + damage * MeterPerDamageTaken);
    }

    public static int GetSpriteFrame(PlayerEntity player)
    {
        if (Read(player, "parry") > 0) return 12;
        var ticks = Read(player, "ticks");
        if (ticks > 0)
        {
            var second = ticks <= Read(player, "duration") / 2 ? 1 : 0;
            return (KingpinAction)Read(player, "action") switch
            {
                KingpinAction.Jab1 => 2 + second, KingpinAction.Jab2 => 4 + second,
                KingpinAction.Uppercut => 6 + second, KingpinAction.ShoulderBash => 8 + second,
                KingpinAction.DiveBodyslam => 10,
                KingpinAction.Beatdown => 2 + (Read(player, "duration") - ticks) * 4 / Math.Max(1, Read(player, "duration")) % 4,
                _ => 0,
            };
        }
        if (Read(player, "guard") > 0) return 11;
        // Frames 0 and 1 are duplicate idle art, not a locomotion strip.
        return 0;
    }
}
