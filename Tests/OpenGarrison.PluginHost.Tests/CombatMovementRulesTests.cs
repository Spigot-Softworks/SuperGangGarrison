using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class CombatMovementRulesTests
{
    [Fact]
    public void AirborneVelocityReachBeginsAboveRunJumpBaselineAndCaps()
    {
        var definition = new AirborneVelocityReachDefinition(
            BonusPerExcessBaseline: 0.5f,
            MaxReachMultiplier: 1.5f);
        const float runSpeed = 180f;
        const float jumpSpeed = 240f;
        const float baseline = 300f;

        Assert.Equal(1f, AirborneVelocityReachRules.ResolveMultiplier(
            isGrounded: true,
            horizontalSpeed: baseline * 2f,
            verticalSpeed: 0f,
            runSpeed,
            jumpSpeed,
            definition));
        Assert.Equal(1f, AirborneVelocityReachRules.ResolveMultiplier(
            isGrounded: false,
            horizontalSpeed: runSpeed,
            verticalSpeed: -jumpSpeed,
            runSpeed,
            jumpSpeed,
            definition));
        Assert.Equal(1.25f, AirborneVelocityReachRules.ResolveMultiplier(
            isGrounded: false,
            horizontalSpeed: baseline * 1.5f,
            verticalSpeed: 0f,
            runSpeed,
            jumpSpeed,
            definition), precision: 4);
        Assert.Equal(1.5f, AirborneVelocityReachRules.ResolveMultiplier(
            isGrounded: false,
            horizontalSpeed: baseline * 3f,
            verticalSpeed: 0f,
            runSpeed,
            jumpSpeed,
            definition), precision: 4);
    }

    [Fact]
    public void PelletKnockbackIsAConstantPerUseBudgetAcrossProjectileMultipliers()
    {
        var weapon = CharacterClassCatalog.Scattergun;

        var stockPayload = BulletKnockbackRules.ResolvePayload(
            weapon,
            weapon.ProjectilesPerShot);
        var doubledPayload = BulletKnockbackRules.ResolvePayload(
            weapon,
            weapon.ProjectilesPerShot * 2);

        Assert.Equal(4f, stockPayload.Impulse * weapon.ProjectilesPerShot, precision: 4);
        Assert.Equal(4f, doubledPayload.Impulse * weapon.ProjectilesPerShot * 2, precision: 4);
        Assert.Equal(0.5f, stockPayload.AirborneVerticalScale);
        Assert.Equal(0.5f, stockPayload.GroundedVerticalScale);
    }

    [Fact]
    public void LegacyWeaponsWithoutAuthoredKnockbackKeepTheirOldPerProjectileImpulse()
    {
        var legacyWeapon = CharacterClassCatalog.Scattergun with
        {
            PlayerKnockback = null,
            PlayerKnockbackScale = 1.2f,
        };

        var payload = BulletKnockbackRules.ResolvePayload(legacyWeapon, actualProjectileCount: 20);

        Assert.Equal(0.6f, payload.Impulse, precision: 4);
        Assert.Equal(1f, payload.AirborneVerticalScale);
        Assert.Equal(1f, payload.GroundedVerticalScale);
    }

    [Fact]
    public void BulletKnockbackNormalizesDirectionAndDoesNotMoveUberedPlayers()
    {
        var target = new PlayerEntity(1, CharacterClassCatalog.Scout, "Target");
        target.Spawn(PlayerTeam.Blue, 0f, 0f);
        var payload = new BulletKnockbackPayload(
            Impulse: 4f,
            AirborneVerticalScale: 0.5f,
            GroundedVerticalScale: 0.5f);

        BulletKnockbackRules.Apply(target, 3f, -4f, payload);

        Assert.Equal(72f, target.HorizontalSpeed, precision: 3);
        Assert.Equal(-48f, target.VerticalSpeed, precision: 3);

        target.ApplyVelocityImpulse(0f, 0f);
        target.RefreshUber();
        BulletKnockbackRules.Apply(target, 1f, -1f, payload);

        Assert.Equal(0f, target.HorizontalSpeed);
        Assert.Equal(0f, target.VerticalSpeed);
    }
}
