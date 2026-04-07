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
    private void DrawLocalHealthHud()
    {
        _gameplayLocalStatusHudController.DrawLocalHealthHud();
    }

    private SentryEntity? GetLocalOwnedSentry()
    {
        return _gameplayEngineerHudController.GetLocalOwnedSentry();
    }

    private static int GetCharacterHudFrameIndex(PlayerEntity player)
    {
        return GameplayLocalStatusHudController.GetCharacterHudFrameIndex(player);
    }

    private void DrawAmmoHud()
    {
        _gameplayLocalStatusHudController.DrawAmmoHud();
    }

    private void DrawDemoknightHud()
    {
        _gameplayLocalStatusHudController.DrawDemoknightHud();
    }

    private void DrawPyroAmmoHud()
    {
        _gameplayLocalStatusHudController.DrawPyroAmmoHud();
    }

    private void DrawHeavyAmmoHud()
    {
        _gameplayLocalStatusHudController.DrawHeavyAmmoHud();
    }

    private void DrawHeavySandwichHud()
    {
        _gameplayLocalStatusHudController.DrawHeavySandwichHud();
    }

    private void DrawQuoteAmmoHud()
    {
        _gameplayLocalStatusHudController.DrawQuoteAmmoHud();
    }

    private void DrawDemomanStickyHud()
    {
        _gameplayLocalStatusHudController.DrawDemomanStickyHud();
    }

    private void DrawExperimentalOffhandHud()
    {
        _gameplayLocalStatusHudController.DrawExperimentalOffhandHud();
    }

    private void DrawAcquiredMedigunPrompt()
    {
        _gameplayLocalStatusHudController.DrawAcquiredMedigunPrompt();
    }

    private void DrawAcquiredWeaponHud()
    {
        _gameplayLocalStatusHudController.DrawAcquiredWeaponHud();
    }

    private void DrawPyroFlareHud(int frameIndex)
    {
        _gameplayLocalStatusHudController.DrawPyroFlareHud(frameIndex);
    }

    private bool TryDrawSourceAmmoHudSprite(string spriteName, int frameIndex)
    {
        return _gameplayLocalStatusHudController.TryDrawSourceAmmoHudSprite(spriteName, frameIndex);
    }

    private void DrawSourceAmmoHudBar(float left, float width, float value, float maxValue, Color fillColor)
    {
        _gameplayLocalStatusHudController.DrawSourceAmmoHudBar(left, width, value, maxValue, fillColor);
    }

    private Rectangle GetReloadAmmoHudBarRectangle()
    {
        return _gameplayLocalStatusHudController.GetReloadAmmoHudBarRectangle();
    }

    private Vector2 GetSourceHudPoint(float sourceX, float sourceY)
    {
        return _gameplayLocalStatusHudController.GetSourceHudPoint(sourceX, sourceY);
    }

    private Rectangle GetSourceHudRectangle(float sourceX, float sourceY, float width, float height)
    {
        return _gameplayLocalStatusHudController.GetSourceHudRectangle(sourceX, sourceY, width, height);
    }

    private void DrawMedicHud()
    {
        _gameplayMedicHudController.DrawMedicHud();
    }

    private void DrawMedicAssistHud()
    {
        _gameplayMedicHudController.DrawMedicAssistHud();
    }

    private void DrawEngineerHud()
    {
        _gameplayEngineerHudController.DrawEngineerHud();
    }

    private void DrawCenterStatusHud(string label, float value, float maxValue, float viewportYRatio, float textAlpha)
    {
        _gameplayMedicHudController.DrawCenterStatusHud(label, value, maxValue, viewportYRatio, textAlpha);
    }

    private PlayerEntity? FindMedicHealingPlayer(int playerId)
    {
        return _gameplayMedicHudController.FindMedicHealingPlayer(playerId);
    }

    private void DrawHealerRadarHud(Vector2 cameraPosition, MouseState mouse)
    {
        _gameplayMedicHudController.DrawHealerRadarHud(cameraPosition, mouse);
    }

    private void DrawSniperHud(MouseState mouse)
    {
        _gameplayAimHudController.DrawSniperHud(mouse);
    }

    private void DrawPersistentSelfNameHud(Vector2 cameraPosition)
    {
        _gameplayPlayerNameHudController.DrawPersistentSelfNameHud(cameraPosition);
    }

    private void DrawHoveredPlayerNameHud(MouseState mouse, Vector2 cameraPosition)
    {
        _gameplayPlayerNameHudController.DrawHoveredPlayerNameHud(mouse, cameraPosition);
    }

    private void DrawPlayerNameHud(PlayerEntity player, Vector2 cameraPosition)
    {
        _gameplayPlayerNameHudController.DrawPlayerNameHud(player, cameraPosition);
    }

    private PlayerEntity? GetHoveredPlayerForNameHud(MouseState mouse, Vector2 cameraPosition)
    {
        return _gameplayPlayerNameHudController.GetHoveredPlayerForNameHud(mouse, cameraPosition);
    }

    private void DrawCrosshair(MouseState mouse)
    {
        _gameplayAimHudController.DrawCrosshair(mouse);
    }

    private int CountLocalOwnedStickyMines()
    {
        return _gameplayLocalStatusHudController.CountLocalOwnedStickyMines();
    }

    private string? GetAmmoHudSpriteName()
    {
        return _gameplayLocalStatusHudController.GetAmmoHudSpriteName();
    }

    private int GetAmmoHudFrameIndex()
    {
        return _gameplayLocalStatusHudController.GetAmmoHudFrameIndex();
    }

    private void DrawAmmoReloadBar(Rectangle barRectangle)
    {
        _gameplayLocalStatusHudController.DrawAmmoReloadBar(barRectangle);
    }

    private float GetAmmoReloadBarProgress(PlayerEntity player)
    {
        return _gameplayLocalStatusHudController.GetAmmoReloadBarProgress(player);
    }

    private bool IsLocalDisplayedMainWeaponAcquired()
    {
        return _gameplayLocalStatusHudController.IsLocalDisplayedMainWeaponAcquired();
    }

    private string GetLocalDisplayedMainWeaponPresentationItemId()
    {
        return _gameplayLocalStatusHudController.GetLocalDisplayedMainWeaponPresentationItemId();
    }

    private PrimaryWeaponDefinition GetLocalDisplayedMainWeaponStats()
    {
        return _gameplayLocalStatusHudController.GetLocalDisplayedMainWeaponStats();
    }

    private int GetLocalDisplayedMainWeaponCurrentShells()
    {
        return _gameplayLocalStatusHudController.GetLocalDisplayedMainWeaponCurrentShells();
    }

    private int GetLocalDisplayedMainWeaponMaxShells()
    {
        return _gameplayLocalStatusHudController.GetLocalDisplayedMainWeaponMaxShells();
    }

    private int GetLocalDisplayedMainWeaponCooldownTicks()
    {
        return _gameplayLocalStatusHudController.GetLocalDisplayedMainWeaponCooldownTicks();
    }

    private int GetLocalDisplayedMainWeaponReloadTicks()
    {
        return _gameplayLocalStatusHudController.GetLocalDisplayedMainWeaponReloadTicks();
    }

    private string GetLocalAlternatePrimaryWeaponPresentationItemId()
    {
        return _gameplayLocalStatusHudController.GetLocalAlternatePrimaryWeaponPresentationItemId();
    }

    private PrimaryWeaponDefinition GetLocalAlternatePrimaryWeaponStats()
    {
        return _gameplayLocalStatusHudController.GetLocalAlternatePrimaryWeaponStats();
    }

    private int GetLocalAlternatePrimaryWeaponCurrentShells()
    {
        return _gameplayLocalStatusHudController.GetLocalAlternatePrimaryWeaponCurrentShells();
    }

    private int GetLocalAlternatePrimaryWeaponMaxShells()
    {
        return _gameplayLocalStatusHudController.GetLocalAlternatePrimaryWeaponMaxShells();
    }

    private float GetLocalAlternatePrimaryWeaponReloadProgress()
    {
        return _gameplayLocalStatusHudController.GetLocalAlternatePrimaryWeaponReloadProgress();
    }

    private static float GetMedicNeedleReloadProgress(int currentShells, int maxShells, int refillTicks)
    {
        return GameplayLocalStatusHudController.GetMedicNeedleReloadProgressProxy(currentShells, maxShells, refillTicks);
    }
}
