#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Globalization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public void DrawLocalHealthHud()
    {
        _hudManager.LocalStatus.DrawLocalHealthHud();
    }

    public void DrawDamageVignette()
    {
        _hudManager.LocalStatus.DrawDamageVignette();
    }

    public SentryEntity? GetLocalOwnedSentry()
    {
        return _hudManager.Engineer.GetLocalOwnedSentry();
    }

    public int GetLocalOwnedSentryCount()
    {
        return _hudManager.Engineer.GetLocalOwnedSentryCount();
    }

    public static int GetCharacterHudFrameIndex(PlayerEntity player)
    {
        return GameplayLocalStatusHudController.GetCharacterHudFrameIndex(player);
    }

    public void DrawDemoknightHud()
    {
        _hudManager.LocalStatus.DrawDemoknightHud();
    }

    private void DrawPyroAmmoHud()
    {
        _hudManager.LocalStatus.DrawPyroAmmoHud();
    }

    private void DrawHeavyAmmoHud()
    {
        _hudManager.LocalStatus.DrawHeavyAmmoHud();
    }

    private void DrawQuoteAmmoHud()
    {
        _hudManager.LocalStatus.DrawQuoteAmmoHud();
    }

    private void DrawDemomanStickyHud()
    {
        _hudManager.LocalStatus.DrawDemomanStickyHud();
    }

    private void DrawExperimentalOffhandHud()
    {
        _hudManager.LocalStatus.DrawExperimentalOffhandHud();
    }

    private void DrawAcquiredMedigunPrompt()
    {
        _hudManager.LocalStatus.DrawAcquiredMedigunPrompt();
    }

    private void DrawAcquiredWeaponHud()
    {
        _hudManager.LocalStatus.DrawAcquiredWeaponHud();
    }

    private void DrawPyroFlareHud(int frameIndex)
    {
        _hudManager.LocalStatus.DrawPyroFlareHud(frameIndex);
    }

    private bool TryDrawSourceAmmoHudSprite(string spriteName, int frameIndex)
    {
        return _hudManager.LocalStatus.TryDrawSourceAmmoHudSprite(spriteName, frameIndex);
    }

    private void DrawSourceAmmoHudBar(float left, float width, float value, float maxValue, Color fillColor)
    {
        _hudManager.LocalStatus.DrawSourceAmmoHudBar(left, width, value, maxValue, fillColor);
    }

    private Rectangle GetReloadAmmoHudBarRectangle()
    {
        return _hudManager.LocalStatus.GetReloadAmmoHudBarRectangle();
    }

    private Vector2 GetSourceHudPoint(float sourceX, float sourceY)
    {
        return _hudManager.LocalStatus.GetSourceHudPoint(sourceX, sourceY);
    }

    private Rectangle GetSourceHudRectangle(float sourceX, float sourceY, float width, float height)
    {
        return _hudManager.LocalStatus.GetSourceHudRectangle(sourceX, sourceY, width, height);
    }

    private void DrawMedicHud()
    {
        _hudManager.Medic.DrawMedicHud();
    }

    private void DrawEngineerHud()
    {
        _hudManager.Engineer.DrawEngineerHud();
    }

    private PlayerEntity? FindMedicHealingPlayer(int playerId)
    {
        return _hudManager.Medic.FindMedicHealingPlayer(playerId);
    }

    private void DrawHealerRadarHud(Vector2 cameraPosition, MouseState mouse)
    {
        _hudManager.Medic.DrawHealerRadarHud(cameraPosition, mouse);
    }

    private void DrawSniperHud(Vector2 screenAimPosition)
    {
        _hudManager.Aim.DrawSniperHud(screenAimPosition);
    }

    private void DrawPersistentSelfNameHud(Vector2 cameraPosition)
    {
        _hudManager.PlayerName.DrawPersistentSelfNameHud(cameraPosition);
    }

    private void DrawForcedPlayerNameHuds(Vector2 cameraPosition)
    {
        _hudManager.PlayerName.DrawForcedPlayerNameHuds(cameraPosition);
    }

    private void DrawHoveredPlayerNameHud(MouseState mouse, Vector2 cameraPosition)
    {
        _hudManager.PlayerName.DrawHoveredPlayerNameHud(mouse, cameraPosition);
    }

    private void DrawPlayerNameHud(PlayerEntity player, Vector2 cameraPosition)
    {
        _hudManager.PlayerName.DrawPlayerNameHud(player, cameraPosition);
    }

    private PlayerEntity? GetHoveredPlayerForNameHud(MouseState mouse, Vector2 cameraPosition)
    {
        return _hudManager.PlayerName.GetHoveredPlayerForNameHud(mouse, cameraPosition);
    }

    public static bool ShouldForceMapBotNameplate(PlayerEntity player) =>
        player.TryGetReplicatedStateBool(
            BotSpawnMetadata.VisualReplicatedStateOwnerId,
            BotSpawnMetadata.ForceNameplateReplicatedStateKey,
            out var forced)
        && forced;

    private static bool ShouldForceMapBotHealthBar(PlayerEntity player) =>
        player.TryGetReplicatedStateBool(
            BotSpawnMetadata.VisualReplicatedStateOwnerId,
            BotSpawnMetadata.ForceHealthBarReplicatedStateKey,
            out var forced)
        && forced;

    private void DrawCrosshair(Vector2 screenPosition)
    {
        _hudManager.Aim.DrawCrosshair(screenPosition);
    }

    private void DrawControllerAimLine(Vector2 cameraPosition, Vector2 screenAimPosition)
    {
        _hudManager.Aim.DrawControllerAimLine(cameraPosition, screenAimPosition);
    }

    private int CountLocalOwnedStickyMines()
    {
        return _hudManager.LocalStatus.CountLocalOwnedStickyMines();
    }

    private string? GetAmmoHudSpriteName()
    {
        return _hudManager.LocalStatus.GetAmmoHudSpriteName();
    }

    private int GetAmmoHudFrameIndex()
    {
        return _hudManager.LocalStatus.GetAmmoHudFrameIndex();
    }

    private void DrawAmmoReloadBar(Rectangle barRectangle)
    {
        _hudManager.LocalStatus.DrawAmmoReloadBar(barRectangle);
    }

    private float GetAmmoReloadBarProgress(PlayerEntity player)
    {
        return _hudManager.LocalStatus.GetAmmoReloadBarProgress(player);
    }

    private bool IsLocalDisplayedMainWeaponAcquired()
    {
        return _hudManager.LocalStatus.IsLocalDisplayedMainWeaponAcquired();
    }

    private string GetLocalDisplayedMainWeaponPresentationItemId()
    {
        return _hudManager.LocalStatus.GetLocalDisplayedMainWeaponPresentationItemId();
    }

    public PrimaryWeaponDefinition GetLocalDisplayedMainWeaponStats()
    {
        return _hudManager.LocalStatus.GetLocalDisplayedMainWeaponStats();
    }

    public int GetLocalDisplayedMainWeaponCurrentShells()
    {
        return _hudManager.LocalStatus.GetLocalDisplayedMainWeaponCurrentShells();
    }

    public int GetLocalDisplayedMainWeaponMaxShells()
    {
        return _hudManager.LocalStatus.GetLocalDisplayedMainWeaponMaxShells();
    }

    public int GetLocalDisplayedMainWeaponCooldownTicks()
    {
        return _hudManager.LocalStatus.GetLocalDisplayedMainWeaponCooldownTicks();
    }

    public int GetLocalDisplayedMainWeaponReloadTicks()
    {
        return _hudManager.LocalStatus.GetLocalDisplayedMainWeaponReloadTicks();
    }

    private string GetLocalAlternatePrimaryWeaponPresentationItemId()
    {
        return _hudManager.LocalStatus.GetLocalAlternatePrimaryWeaponPresentationItemId();
    }

    private PrimaryWeaponDefinition GetLocalAlternatePrimaryWeaponStats()
    {
        return _hudManager.LocalStatus.GetLocalAlternatePrimaryWeaponStats();
    }

    private int GetLocalAlternatePrimaryWeaponCurrentShells()
    {
        return _hudManager.LocalStatus.GetLocalAlternatePrimaryWeaponCurrentShells();
    }

    private int GetLocalAlternatePrimaryWeaponMaxShells()
    {
        return _hudManager.LocalStatus.GetLocalAlternatePrimaryWeaponMaxShells();
    }

    private float GetLocalAlternatePrimaryWeaponReloadProgress()
    {
        return _hudManager.LocalStatus.GetLocalAlternatePrimaryWeaponReloadProgress();
    }

    private static float GetMedicNeedleReloadProgress(int currentShells, int maxShells, int refillTicks)
    {
        return GameplayLocalStatusHudController.GetMedicNeedleReloadProgressProxy(currentShells, maxShells, refillTicks);
    }
}
