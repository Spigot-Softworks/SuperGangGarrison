#nullable enable

using System;
using Microsoft.Xna.Framework;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1
{
    public readonly Lazy<PlayerSkinCatalog> _playerSkins = new(PlayerSkinCatalog.Load);

    public PlayerSkinDefinition? GetPlayerSkin(PlayerEntity player) =>
        _networkClient.IsLegacyGg2Connection
            ? null
            : _playerSkins.Value.Find(player.GameplayClassId, player.Team);

    private BrowserPlayerSkinSnapshot? GetBrowserPlayerSkinSnapshot()
    {
        var player = _world.LocalPlayer;
        if (!player.IsAlive || !TryGetPlayerSkinBody(player, out var body)) return null;
        var weapon = GetWeaponRenderDefinition(player);
        _playerRenderStates.TryGetValue(GetPlayerStateKey(player), out var state);
        return new BrowserPlayerSkinSnapshot(body.SpriteName ?? "", (int)body.AnimationImage,
            state?.SkinAnimation.ClipName ?? "idle", GetResolvedSprite(body.SpriteName!)?.Frames.Count ?? 0,
            weapon.NormalSpriteName ?? "", weapon.NormalSpriteName is { } name ? GetResolvedSprite(name)?.Frames.Count ?? 0 : 0,
            state?.WeaponAnimationMode.ToString() ?? "Idle", weapon.PoseFrameIndex);
    }

    private int GetPlayerSkinPose(PlayerEntity player, PlayerSkinDefinition skin) =>
        _playerRenderStates.TryGetValue(GetPlayerStateKey(player), out var state) && state.SkinAnimation.TryGetPose(skin, out var pose)
            ? pose
            : skin.Clips["idle"].Frames[0];

    private bool TryGetPlayerSkinBody(PlayerEntity player, out PlayerBodySpriteSelection selection)
    {
        var skin = GetPlayerSkin(player);
        if (skin is null || player.IsTaunting)
        {
            selection = default;
            return false;
        }
        if (_playerRenderStates.TryGetValue(GetPlayerStateKey(player), out var state)
            && !state.SkinAnimation.TryGetPose(skin, out _))
        {
            selection = default;
            return false;
        }
        var pose = GetPlayerSkinPose(player, skin);
        string bodySprite;
        string? legsSprite = null;
        string? torsoSprite = null;
        if (skin.CloakedBodySprite is { } cloakedSprite && GetPlayerIsSpyCloaked(player))
        {
            bodySprite = cloakedSprite;
            legsSprite = skin.CloakedLegsBodySprite;
            torsoSprite = skin.CloakedTorsoBodySprite;
        }
        else if (ShouldDrawLegsOnlyBody(player, skin))
        {
            bodySprite = skin.LegsBodySprite!;
        }
        else
        {
            bodySprite = skin.BodySprite;
            legsSprite = skin.LegsBodySprite;
            torsoSprite = skin.TorsoBodySprite;
        }

        var equipmentPose = pose;
        var weaponBobMode = OpenGarrisonPreferencesDocument.NormalizeWeaponBobMode(_weaponBobMode);
        float equipmentOffset;
        if (weaponBobMode == WeaponBobMode.Disabled)
        {
            equipmentOffset = (skin.EquipmentOffset + skin.Poses[pose].EquipmentOffset) * skin.PixelScale;
        }
        else
        {
            equipmentPose = GetDelayedRunSkinPose(skin, pose);
            equipmentOffset = (skin.EquipmentOffset + skin.Poses[equipmentPose].EquipmentOffset) * skin.PixelScale;
        }

        selection = new PlayerBodySpriteSelection(skin.SpriteForTeam(bodySprite, player.Team),
            pose, 0, equipmentOffset,
            player.IsCarryingIntel, false,
            legsSprite is null ? null : skin.SpriteForTeam(legsSprite, player.Team),
            torsoSprite is null ? null : skin.SpriteForTeam(torsoSprite, player.Team));
        return true;
    }

    private bool ShouldDrawLegsOnlyBody(PlayerEntity player, PlayerSkinDefinition skin)
        => skin.LegsBodySprite is not null
            && _gameplayWeaponRenderController is not null
            && GetWeaponRenderDefinition(player).UseTorsoReplacement;

    /// <summary>
    /// Idle-height torso replacements bob up 1 source pixel on the up-bob run/jump poses
    /// (1st, 2nd, 5th, and 6th frames of the run cycle).
    /// </summary>
    private static bool IsTorsoReplacementUpBobPose(PlayerSkinDefinition skin, int pose)
    {
        if (!skin.Clips.TryGetValue("run", out var run))
        {
            return false;
        }

        for (var i = 0; i < run.Frames.Length; i++)
        {
            if (run.Frames[i] != pose)
            {
                continue;
            }

            var cycleIndex = GameplayPlayerSpriteRenderController.GetRunEquipmentBobFrame(i, delayByOneFrame: false);
            if (cycleIndex is 0 or 1 or 4 or 5)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns the pose one run-cycle frame behind <paramref name="pose"/> when that pose
    /// appears in the run clip; otherwise returns <paramref name="pose"/> unchanged.
    /// </summary>
    private static int GetDelayedRunSkinPose(PlayerSkinDefinition skin, int pose)
    {
        if (!skin.Clips.TryGetValue("run", out var run) || run.Frames.Length == 0)
        {
            return pose;
        }

        for (var i = 0; i < run.Frames.Length; i++)
        {
            if (run.Frames[i] != pose)
            {
                continue;
            }

            var delayedIndex = ((i - 1) % run.Frames.Length + run.Frames.Length) % run.Frames.Length;
            return run.Frames[delayedIndex];
        }

        return pose;
    }

    /// <summary>
    /// Idle-height torso replacements share the body/legs origin; bob up 1 source
    /// pixel on the up-bob run/jump poses.
    /// </summary>
    private float GetTorsoReplacementYOffset(PlayerEntity player)
    {
        var mode = OpenGarrisonPreferencesDocument.NormalizeWeaponBobMode(_weaponBobMode);
        if (mode == WeaponBobMode.Disabled)
        {
            return 0f;
        }

        if (_playerRenderStates.TryGetValue(GetPlayerStateKey(player), out var renderState))
        {
            return renderState.SmoothedTorsoBobOffset;
        }

        var skin = GetPlayerSkin(player);
        if (skin is null)
        {
            return 0f;
        }

        var pose = GetPlayerSkinPose(player, skin);
        var bobPose = GetDelayedRunSkinPose(skin, pose);
        if (IsTorsoReplacementUpBobPose(skin, bobPose))
        {
            return -1f * skin.PixelScale;
        }

        return 0f;
    }

    private float GetTorsoReplacementBobOffset(PlayerEntity player) => GetTorsoReplacementYOffset(player);

    private bool PlayerSkinIncludesCloakedWeapon(PlayerEntity player, PlayerBodySpriteSelection body) =>
        GetPlayerIsSpyCloaked(player) && GetPlayerSkin(player) is { CloakedBodySprite: { } sprite } skin
        && body.SpriteName == skin.SpriteForTeam(sprite, player.Team);

    private WeaponRenderDefinition ApplyPlayerSkinWeapon(PlayerEntity player,
        GameplayItemPresentationDefinition presentation, WeaponRenderDefinition definition, bool standing = false)
    {
        if (definition.UseTorsoReplacement)
        {
            // Torso replacements keep their own idle/attack strips; do not remap to skin weapons.
            // Blue team uses FooBlueS / FooBlueFS when those sprites exist (Detonator sleeve palette).
            var xOffset = definition.XOffset;
            var yOffset = definition.YOffset;
            // Strong Drink art is authored against stock SniperRedS origin (26,40). Elkondo
            // legs use (32,40); nudge so the bottle torso sits on the skin legs.
            if (IsStrongDrinkWeaponPresentation(definition)
                && GetPlayerSkin(player) is { } strongDrinkSkin)
            {
                const float stockBodyOriginX = 26f;
                const float stockBodyOriginY = 40f;
                xOffset += stockBodyOriginX - (strongDrinkSkin.Origin[0] * strongDrinkSkin.PixelScale);
                yOffset += stockBodyOriginY - (strongDrinkSkin.Origin[1] * strongDrinkSkin.PixelScale);
            }

            return definition with
            {
                NormalSpriteName = ResolveTorsoReplacementSpriteForTeam(definition.NormalSpriteName, player.Team),
                RecoilSpriteName = ResolveTorsoReplacementSpriteForTeam(definition.RecoilSpriteName, player.Team),
                ReloadSpriteName = ResolveTorsoReplacementSpriteForTeam(definition.ReloadSpriteName, player.Team),
                TorsoSpriteName = ResolveTorsoReplacementSpriteForTeam(definition.TorsoSpriteName, player.Team),
                TorsoRecoilSpriteName = ResolveTorsoReplacementSpriteForTeam(definition.TorsoRecoilSpriteName, player.Team),
                XOffset = xOffset,
                YOffset = yOffset,
                SingleTeamFrames = true,
            };
        }

        var skin = GetPlayerSkin(player);
        if (skin is null || (!standing && (player.IsTaunting || _world.IsPlayerHumiliated(player)
            || IsBackstabReplacementRenderActive(player) || GetPlayerIsBuffBannerDeploying(player))))
            return definition;
        var pose = standing ? skin.Clips["idle"].Frames[0] : GetPlayerSkinPose(player, skin);
        if (skin.Poses[pose].WeaponSkin is { } weaponSkinId
            && _playerSkins.Value.Skins.TryGetValue(weaponSkinId, out var weaponSkin))
        {
            pose = skin.Poses[pose].WeaponSkinPose;
            skin = weaponSkin;
        }
        var weapon = skin.Weapon;
        if (weapon is null) return definition;
        // Match the actual equipped item's presentation, so offhands, acquired weapons,
        // and custom gameplay items retain their own art and attachment points.
        if (!CharacterClassCatalog.RuntimeRegistry.TryGetItem(weapon.ItemId, out var item)
            || !ReferenceEquals(presentation, item.Presentation) || presentation.WorldSpriteName != weapon.MatchSprite)
            return definition;
        var offset = skin.Poses[pose].WeaponOffset;
        return definition with
        {
            NormalSpriteName = skin.SpriteForTeam(weapon.Sprite, player.Team),
            RecoilSpriteName = weapon.FireSprite is null ? null : skin.SpriteForTeam(weapon.FireSprite, player.Team),
            RecoilDurationSeconds = definition.RecoilDurationSeconds / weapon.FirePlaybackRate,
            ReloadSpriteName = weapon.ReloadSprite is null ? null : skin.SpriteForTeam(weapon.ReloadSprite, player.Team),
            RecoilOverlay = default,
            ReloadOverlay = default,
            XOffset = (offset[0] + weapon.AttachmentOffset[0] - skin.Origin[0]) * skin.PixelScale,
            YOffset = (offset[1] + weapon.AttachmentOffset[1] - skin.Origin[1]) * skin.PixelScale,
            ReloadSpriteXOffset = 0,
            ReloadSpriteYOffset = 0,
            SingleTeamFrames = true,
            PoseFrameIndex = pose,
            MuzzleOffset = weapon.Muzzle.Length == 2
                ? new Vector2(weapon.Muzzle[0] - weapon.Pivot[0], weapon.Muzzle[1] - weapon.Pivot[1]) * skin.PixelScale
                : null,
        };
    }

    private string? ResolveTorsoReplacementSpriteForTeam(string? spriteName, PlayerTeam team)
    {
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            return spriteName;
        }

        var teamSpriteName = ExperimentalDemoknightCatalog.ResolveTorsoReplacementTeamSpriteName(spriteName, team);
        if (string.Equals(teamSpriteName, spriteName, StringComparison.Ordinal))
        {
            return spriteName;
        }

        return GetResolvedSprite(teamSpriteName) is not null ? teamSpriteName : spriteName;
    }

    private static bool IsStrongDrinkWeaponPresentation(WeaponRenderDefinition definition)
    {
        return definition.NormalSpriteName is not null
            && definition.NormalSpriteName.StartsWith("BottleArm", StringComparison.Ordinal);
    }
}
