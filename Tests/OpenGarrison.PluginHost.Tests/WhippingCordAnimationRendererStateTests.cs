using OpenGarrison.Client;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using OpenGarrison.Server;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

[Collection(ContentRootTestGroup.Name)]
public sealed class WhippingCordAnimationRendererStateTests
{
    [Fact]
    public void UntickedRemoteBackswingCannotReviveAfterReleaseOrACompletedLaterSwing()
    {
        var source = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        source.NetworkPlayers.PrepareLocalPlayerJoin();
        source.NetworkPlayers.SetLocalPlayerTeam(PlayerTeam.Red);
        source.NetworkPlayers.CompleteLocalPlayerJoin(PlayerClass.Engineer);
        Assert.True(source.LocalPlayer.TrySelectGameplayPrimaryItem(WhippingCordCatalog.ItemId));
        source.LocalPlayer.LatchWhippingCord(480f, 320f, 75f);
        var stringCache = new SnapshotStringCache();
        var remotePlayerState = source.Snapshots.ToSnapshotPlayerState(
            2,
            source.LocalPlayer,
            source.LocalPlayer,
            value => stringCache.GetOrAddCacheId(value)) with
        {
            Slot = 2,
            PlayerId = 202,
            Name = "Remote Engineer",
        };

        var receiver = new SimulationWorld(new SimulationConfig { EnableLocalDummies = false });
        var localPlayerState = SimulationWorldSnapshotPresentationTests.CreatePlayerState(
            1,
            101,
            "Local",
            PlayerTeam.Red,
            PlayerClass.Scout,
            isAlive: true,
            gibDeaths: 0);
        Assert.True(receiver.SnapshotApply.ApplySnapshot(
            SimulationWorldSnapshotPresentationTests.CreateSnapshot(
                receiver,
                frame: 1,
                localPlayerState,
                remotePlayerState),
            localPlayerSlot: 1));
        var player = Assert.Single(receiver.RemoteSnapshotPlayers);
        var weapon = CreateWhippingCordWeaponDefinition();
        var renderState = new Game1.PlayerRenderState();

        player.LatchWhippingCord(player.X + 48f, player.Y - 36f, 60f);
        Update(player, renderState, weapon, currentCooldownTicks: 0, swingStarted: false, elapsedSeconds: 0f);
        Assert.Equal(Game1.WeaponAnimationMode.Recoil, renderState.WeaponAnimationMode);

        player.HydrateWhippingCordLatch(false, 0f, 0f, 0f);
        Assert.True(player.IsWhippingCordBackswingActive);
        for (var frame = 0; frame < 20; frame++)
        {
            Update(player, renderState, weapon, currentCooldownTicks: 0, swingStarted: false, elapsedSeconds: 1f / 60f);
        }

        Assert.True(player.IsWhippingCordBackswingActive);
        Assert.Equal(Game1.WeaponAnimationMode.Idle, renderState.WeaponAnimationMode);

        Update(player, renderState, weapon, currentCooldownTicks: 18, swingStarted: true, elapsedSeconds: 0f);
        Assert.Equal(Game1.WeaponAnimationMode.Recoil, renderState.WeaponAnimationMode);
        for (var frame = 0; frame < 20; frame++)
        {
            Update(player, renderState, weapon, currentCooldownTicks: 18, swingStarted: false, elapsedSeconds: 1f / 60f);
        }

        Assert.True(player.IsWhippingCordBackswingActive);
        Assert.Equal(Game1.WeaponAnimationMode.Idle, renderState.WeaponAnimationMode);

        // A newly visible/recreated render state may first see the player after
        // the latch transition. Remote gameplay timers remain stale, so it must
        // start idle instead of adopting that unticked backswing counter.
        var recreatedRenderState = new Game1.PlayerRenderState();
        Update(player, recreatedRenderState, weapon, currentCooldownTicks: 0, swingStarted: false, elapsedSeconds: 0f);
        Assert.Equal(Game1.WeaponAnimationMode.Idle, recreatedRenderState.WeaponAnimationMode);
    }

    private static void Update(
        PlayerEntity player,
        Game1.PlayerRenderState renderState,
        Game1.WeaponRenderDefinition weapon,
        int currentCooldownTicks,
        bool swingStarted,
        float elapsedSeconds)
    {
        Game1.UpdateWhippingCordWeaponAnimationState(
            player,
            renderState,
            weapon,
            currentCooldownTicks,
            immediatePress: false,
            swingStarted,
            elapsedSeconds,
            allowGameplayBackswingClock: false);
    }

    private static Game1.WeaponRenderDefinition CreateWhippingCordWeaponDefinition()
    {
        return new Game1.WeaponRenderDefinition(
            NormalSpriteName: WhippingCordCatalog.WhipSpriteName,
            RecoilSpriteName: WhippingCordCatalog.WhipRecoilSpriteName,
            ReloadSpriteName: null,
            RecoilOverlay: new Game1.WeaponAnimationOverlayDefinition(null, null),
            ReloadOverlay: new Game1.WeaponAnimationOverlayDefinition(null, null),
            XOffset: 0f,
            YOffset: 0f,
            ReloadSpriteXOffset: 0f,
            ReloadSpriteYOffset: 0f,
            RecoilDurationSeconds: 0.3f,
            ReloadDurationSeconds: 0f);
    }
}
