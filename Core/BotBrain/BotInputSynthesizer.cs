using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core.BotBrain;

/// <summary>
/// Assembles a PlayerInputSnapshot from steering output, aim position, and combat decisions.
/// This is the final step: all the bot's thinking collapses into the same input struct
/// that a human player's keyboard/mouse would produce.
/// </summary>
public static class BotInputSynthesizer
{
    /// <summary>
    /// Maximum engagement range for firing weapons.
    /// </summary>
    private const float MaxFireRange = 500f;

    /// <summary>
    /// Minimum engagement range (don't fire point-blank with explosives).
    /// </summary>
    private const float MinExplosiveFireRange = 60f;

    public static PlayerInputSnapshot Synthesize(
        PlayerEntity self,
        SteeringOutput steering,
        float aimX,
        float aimY,
        CombatFireDecision combat,
        PlayerInputSnapshot previousInput)
    {
        var left = steering.MoveDirection < 0f;
        var right = steering.MoveDirection > 0f;
        var up = steering.Jump && !previousInput.Up;
        var down = steering.DropDown;

        var input = new PlayerInputSnapshot(
            Left: left,
            Right: right,
            Up: up,
            Down: down,
            BuildSentry: false,
            DestroySentry: false,
            Taunt: false,
            FirePrimary: combat.FirePrimary,
            FireSecondary: combat.FireSecondary,
            AimWorldX: aimX,
            AimWorldY: aimY,
            DebugKill: false);
        return ApplyCombat(self, input, combat, previousInput);
    }

    public static PlayerInputSnapshot ApplyCombat(
        PlayerEntity self,
        PlayerInputSnapshot input,
        CombatFireDecision combat,
        PlayerInputSnapshot previousInput)
    {
        var useAbility = combat.UseAbility;
        var swapWeapon = input.SwapWeapon;
        if (self.HasUtilityBehavior(BuiltInGameplayBehaviorIds.SoldierSecondaryWeapon))
        {
            if (self.HasExperimentalOffhandWeapon)
            {
                var wantsOffhand = combat.UseAbility;
                if (wantsOffhand != self.IsExperimentalOffhandSelected && !previousInput.SwapWeapon)
                {
                    swapWeapon = true;
                }
            }

            useAbility = false;
        }

        return input with
        {
            FirePrimary = combat.FirePrimary,
            FireSecondary = combat.FireSecondary,
            UseAbility = useAbility,
            SwapWeapon = swapWeapon,
        };
    }
}
