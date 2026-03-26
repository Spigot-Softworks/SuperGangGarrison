using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.Core.Tests;

public sealed class PlayerEntityPredictionStateTests
{
    [Fact]
    public void PredictionState_RoundTripsThroughPlayerEntity()
    {
        var player = new PlayerEntity(17, CharacterClassCatalog.Scout, "Predictor");
        var state = new PlayerEntity.PredictionState(
            Team: PlayerTeam.Blue,
            ClassDefinition: CharacterClassCatalog.Spy,
            IsAlive: true,
            X: 384.5f,
            Y: 192.25f,
            HorizontalSpeed: -6.5f,
            VerticalSpeed: 3.25f,
            LegacyStateTickAccumulator: 0.75f,
            MovementState: LegacyMovementState.RocketJuggle,
            IsGrounded: false,
            Health: 72,
            Metal: 66f,
            IsCarryingIntel: true,
            IntelPickupCooldownTicks: 11,
            IntelRechargeTicks: 333f,
            IsInSpawnRoom: true,
            RemainingAirJumps: 1,
            FacingDirectionX: -1f,
            AimDirectionDegrees: 137.5f,
            SourceFacingDirectionX: -1f,
            PreviousSourceFacingDirectionX: 1f,
            CurrentShells: 4,
            PrimaryCooldownTicks: 9,
            ReloadTicksUntilNextShell: 7,
            ContinuousDamageAccumulator: 0.6f,
            IsHeavyEating: false,
            HeavyEatTicksRemaining: 0,
            HeavyEatCooldownTicksRemaining: 0,
            HeavyHealingAccumulator: 0.2f,
            IsTaunting: false,
            TauntFrameIndex: 3.5f,
            IsSniperScoped: false,
            SniperChargeTicks: 0,
            UberTicksRemaining: 2,
            MedicHealTargetId: 44,
            IsMedicHealing: true,
            MedicUberCharge: 1337f,
            IsMedicUberReady: true,
            IsMedicUbering: false,
            MedicNeedleCooldownTicks: 5,
            MedicNeedleRefillTicks: 4,
            ContinuousHealingAccumulator: 0.75f,
            QuoteBubbleCount: 2,
            QuoteBladesOut: 1,
            PyroAirblastCooldownTicks: 6,
            IsSpyCloaked: true,
            SpyCloakAlpha: 0.4f,
            SpyBackstabWindupTicksRemaining: 8,
            SpyBackstabRecoveryTicksRemaining: 3,
            SpyBackstabVisualTicksRemaining: 17,
            SpyBackstabDirectionDegrees: 215f,
            SpyBackstabHitboxPending: true,
            IsSpyVisibleToEnemies: true,
            BurnIntensity: 6.75f,
            BurnDurationSourceTicks: 42f,
            BurnDecayDelaySourceTicksRemaining: 18f,
            BurnIntensityDecayPerSourceTick: 0.075f,
            BurnedByPlayerId: 91,
            Kills: 9,
            Deaths: 4,
            Caps: 1,
            HealPoints: 30,
            ActiveDominationCount: 0,
            IsDominatingLocalViewer: false,
            IsDominatedByLocalViewer: false,
            IsChatBubbleVisible: true,
            ChatBubbleFrameIndex: 2,
            ChatBubbleAlpha: 0.85f,
            IsChatBubbleFading: true,
            ChatBubbleTicksRemaining: 18,
            PyroPrimaryFuelScaled: 40);

        player.RestorePredictionState(state);

        Assert.Equal(state, player.CapturePredictionState());
        Assert.Equal(CharacterClassCatalog.Spy, player.ClassDefinition);
        Assert.Equal(PlayerTeam.Blue, player.Team);
        Assert.True(player.IsSpyCloaked);
        Assert.Equal(1, player.RemainingAirJumps);
        Assert.Equal(44, player.MedicHealTargetId);
        Assert.Equal(6.75f, player.BurnIntensity);
        Assert.Equal(42f, player.BurnDurationSourceTicks);
        Assert.Equal(91, player.BurnedByPlayerId);
    }
}
