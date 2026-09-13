#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;
using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const float JumpPadBuildNoticeCost = 50f;
    private const float DispenserBuildNoticeCost = 100f;
    private bool _buildMenuNumericInputConsumed;
    private bool _buildMenuJumpPadSelectionPressed;
    private bool _buildMenuAbilityWasDown;
    private bool _buildMenuSecondaryWasDown;
    private int _buildWheelSelectedSlot;
    private bool _buildMenuSentrySelectionPressed;
    private bool _buildMenuDispenserSelectionPressed;
    private bool _buildMenuDestroySentrySelectionPressed;
    private bool _buildMenuDestroyDispenserSelectionPressed;
    private bool _hasBuildMenuGameplayAimOffset;
    private float _buildMenuGameplayAimOffsetX;
    private float _buildMenuGameplayAimOffsetY;

    private void ResetBuildMenuInputSelection()
    {
        _buildMenuNumericInputConsumed = false;
        _buildMenuJumpPadSelectionPressed = false;
        _buildMenuSentrySelectionPressed = false;
        _buildMenuDispenserSelectionPressed = false;
        _buildMenuDestroySentrySelectionPressed = false;
        _buildMenuDestroyDispenserSelectionPressed = false;
    }

    private PlayerInputSnapshot ApplyBuildMenuInputSelection(PlayerInputSnapshot input)
    {
        // Constructor's utility binding opens the wheel. Only an explicit jump-pad
        // selection may send UseAbility to the simulation, even after the wheel closes.
        if (_world.LocalPlayer.ClassId == PlayerClass.Engineer)
            input = input with { UseAbility = false };

        if (_buildMenuOpen || _buildMenuNumericInputConsumed)
        {
            input = input with { FirePrimary = false, FireSecondary = false, UseAbility = false,
                SwapWeapon = false, ToggleSecondaryWeapon = false, InteractWeapon = false,
                BuildSentry = false, DestroySentry = false, BuildDispenser = false, DestroyDispenser = false };
        }
        if (!_buildMenuNumericInputConsumed) return input;
        var selectedInput = input with
        {
            BuildSentry = _buildMenuSentrySelectionPressed,
            BuildDispenser = _buildMenuDispenserSelectionPressed,
            DestroySentry = _buildMenuDestroySentrySelectionPressed,
            DestroyDispenser = _buildMenuDestroyDispenserSelectionPressed,
            UseAbility = _buildMenuJumpPadSelectionPressed,
        };
        return ApplyBuildWheelSentryAim(
            selectedInput,
            _world.LocalPlayer.X,
            _world.LocalPlayer.Y,
            _hasBuildMenuGameplayAimOffset,
            _buildMenuGameplayAimOffsetX,
            _buildMenuGameplayAimOffsetY);
    }

    private void DrawBuildMenuHud()
    {
        if ((!_buildMenuOpen && !_hudEditorOpen) || _world.LocalPlayer.ClassId != PlayerClass.Engineer
            || !CanDrawGameplayBuildHud()
            || !TryResolveHudElement(HudElementId.ClassEngineerBuildMenu, out var resolved)) return;
        var sprite = GetResolvedSprite("BuildWheelS");
        if (sprite is null || sprite.Frames.Count < 42) return;
        var center = resolved.Origin;
        var scale = resolved.Layout.Scale;
        void DrawFrame(int index)
        {
            var frame = sprite.Frames[index];
            _spriteBatch.Draw(frame.Texture, center, frame.SourceRectangle, Color.White,
                0f, new Vector2(BuildWheelPresentation.SpriteCenter), scale, SpriteEffects.None, 0f);
        }
        var selected = _buildMenuOpen ? _buildWheelSelectedSlot : 0;
        for (var slot = 0; slot <= 4; slot++)
        {
            var frame = slot switch { 1 => 3, 2 => 4, 3 => 5, 4 => 2, _ => 1 };
            DrawFrame(frame + (selected == slot ? 5 : 0));
        }
        DrawFrame(0);
        DrawFrame(13);
        var blue = _world.LocalPlayer.Team == PlayerTeam.Blue;
        for (var slot = 1; slot <= 4; slot++)
        {
            var exists = HasBuildWheelStructure(slot);
            var canBuild = !_world.LocalPlayer.IsInSpawnRoom
                && GetPlayerMetal(_world.LocalPlayer) >= GetBuildWheelCost(slot);
            var iconFrame = BuildWheelPresentation.GetIconFrame(slot, blue, exists, canBuild);
            var icon = sprite.Frames[iconFrame];
            _spriteBatch.Draw(icon.Texture, center + BuildWheelPresentation.GetIconOffset(slot) * scale,
                icon.SourceRectangle, Color.White, 0f, BuildWheelPresentation.GetIconOrigin(iconFrame), scale, SpriteEffects.None, 0f);
        }
        var label = selected switch { 1 => "Sentry", 3 => "Dispenser", 2 or 4 => "Jump pad", _ => "Cancel" };
        if (selected != 0) label = (HasBuildWheelStructure(selected) ? "Destroy " : "Build ") + label;
        DrawBitmapFontText(label, center + new Vector2(-MeasureBitmapFontWidth(label, 1f) / 2f, 110f * scale), Color.White, 1f);
        UpdateHudElementBounds(HudElementId.ClassEngineerBuildMenu,
            new Rectangle((int)(center.X - 100f * scale), (int)(center.Y - 100f * scale), (int)(201f * scale), (int)(201f * scale)));
    }

    private bool HasBuildWheelStructure(int slot) => slot switch
    {
        1 => GetLocalOwnedSentry() is not null,
        3 => _gameplayEngineerHudController.GetLocalOwnedDispenser() is not null,
        2 or 4 => HasLocalOwnedJumpPad(),
        _ => false,
    };

    private float GetBuildWheelCost(int slot) => slot switch
    {
        1 => _world.LocalPlayer.MaxMetal,
        3 => DispenserBuildNoticeCost,
        _ => JumpPadBuildNoticeCost,
    };

    private int GetBuildWheelSelectedSlot(MouseState mouse)
    {
        var center = GetBubbleMenuScreenCenter();
        var scale = 1f;
        if (TryResolveHudElement(HudElementId.ClassEngineerBuildMenu, out var resolved))
        {
            center = resolved.Origin;
            scale = MathF.Max(0.01f, resolved.Layout.Scale);
        }
        var delta = (new Vector2(mouse.X, mouse.Y) - center) / scale;
        return RadialWheelSelection.GetSlot(MathF.Atan2(delta.Y, delta.X) * (180f / MathF.PI) + 90f,
            delta.Length(), 4, 45f);
    }

    private void CommitBuildWheelSelection(int slot)
    {
        _buildMenuNumericInputConsumed = true;
        var exists = HasBuildWheelStructure(slot);
        if (slot == 1)
        {
            _buildMenuSentrySelectionPressed = !exists;
            _buildMenuDestroySentrySelectionPressed = exists;
        }
        else if (slot == 3)
        {
            _buildMenuDispenserSelectionPressed = !exists;
            _buildMenuDestroyDispenserSelectionPressed = exists;
        }
        else if (slot is 2 or 4) _buildMenuJumpPadSelectionPressed = true;
        if (slot != 0 && !exists) TryShowEngineerBuildResourceNotice(_world.LocalPlayer, GetBuildWheelCost(slot));
        BeginClosingBuildMenu();
    }

    private void UpdateBuildMenuState(KeyboardState keyboard, MouseState mouse, PlayerInputSnapshot input)
    {
        ResetBuildMenuInputSelection();
        var abilityPressed = input.UseAbility && !_buildMenuAbilityWasDown;
        _buildMenuAbilityWasDown = input.UseAbility;
        var secondaryPressed = input.FireSecondary && !_buildMenuSecondaryWasDown;
        _buildMenuSecondaryWasDown = input.FireSecondary;
        if (ShouldCloseBuildMenuForGameplayState() || _scoreboardOpen || _scoreboardAlpha > 0.02f
            || _bubbleMenuKind != BubbleMenuKind.None)
        {
            BeginClosingBuildMenu();
            return;
        }
        var leftPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
        if (secondaryPressed)
        {
            // M2 keeps its direct sentry build/destroy command, including while the wheel is open.
            BeginClosingBuildMenu();
            return;
        }
        if (abilityPressed)
        {
            _buildMenuNumericInputConsumed = true;
            ToggleBuildMenu(input);
            return;
        }
        var onePressed = IsKeyPressed(keyboard, Keys.D1) || IsKeyPressed(keyboard, Keys.NumPad1);
        if (onePressed && !_buildMenuOpen)
        {
            _buildMenuNumericInputConsumed = true;
            ToggleBuildMenu(input);
            return;
        }
        if (!_buildMenuOpen) return;
        _buildWheelSelectedSlot = GetBuildWheelSelectedSlot(mouse);
        var digit = GetPressedDigit(keyboard);
        if (digit is >= 0 and <= 4)
        {
            _buildMenuNumericInputConsumed = true;
            _buildMenuSentrySelectionPressed = digit == 1;
            _buildMenuDestroySentrySelectionPressed = digit == 2;
            _buildMenuDispenserSelectionPressed = digit == 3;
            _buildMenuDestroyDispenserSelectionPressed = digit == 4;
            BeginClosingBuildMenu();
        }
        else if (leftPressed)
        {
            SuppressPrimaryFireUntilMouseRelease();
            CommitBuildWheelSelection(_buildWheelSelectedSlot);
        }
    }

    private bool TryShowEngineerBuildResourceNotice(PlayerEntity player, float requiredMetal)
    {
        if (!IsEngineerBuildResourceInsufficient(GetPlayerMetal(player), requiredMetal))
        {
            return false;
        }

        ShowNotice(NoticeKind.NutsNBolts);
        return true;
    }

    internal static bool IsEngineerBuildResourceInsufficient(float metal, float requiredMetal)
    {
        return metal < requiredMetal;
    }

    private bool HasLocalOwnedJumpPad()
    {
        var localPlayerId = GetPlayerStateKey(_world.LocalPlayer);
        foreach (var jumpPad in _world.JumpPads)
        {
            if (jumpPad.OwnerPlayerId == localPlayerId)
            {
                return true;
            }
        }

        return false;
    }

    private void TryShowEngineerJumpPadBuildNoticeOnUtilityPress(PlayerInputSnapshot input)
    {
        if (!input.UseAbility || _latestPredictedLocalInput.UseAbility)
        {
            return;
        }

        var player = _world.LocalPlayer;
        if (IsLocalSpectatorPresentationActive()
            || player.ClassId != PlayerClass.Engineer
            || !player.IsAlive
            || player.IsInSpawnRoom
            || _world.LocalPlayerAwaitingJoin
            || _world.IsPlayerHumiliated(player)
            || HasLocalOwnedJumpPad())
        {
            return;
        }

        if (GetPlayerMetal(player) < JumpPadBuildNoticeCost)
        {
            ShowNotice(NoticeKind.NutsNBolts);
        }
    }

    private void ToggleBuildMenu(PlayerInputSnapshot input)
    {
        if (_buildMenuOpen) { BeginClosingBuildMenu(); return; }
        CaptureBuildMenuGameplayAim(input);
        _buildMenuOpen = true;
        _buildMenuClosing = false;
        _buildMenuAlpha = 1f;
        _buildWheelSelectedSlot = 0;
    }

    private void CaptureBuildMenuGameplayAim(PlayerInputSnapshot input)
    {
        var player = _world.LocalPlayer;
        var offsetX = input.AimWorldX - player.X;
        var offsetY = input.AimWorldY - player.Y;
        if (!float.IsFinite(offsetX)
            || !float.IsFinite(offsetY)
            || (offsetX * offsetX) + (offsetY * offsetY) < 0.0001f)
        {
            var aimRadians = player.AimDirectionDegrees * (MathF.PI / 180f);
            offsetX = MathF.Cos(aimRadians) * 100f;
            offsetY = MathF.Sin(aimRadians) * 100f;
        }

        _buildMenuGameplayAimOffsetX = offsetX;
        _buildMenuGameplayAimOffsetY = offsetY;
        _hasBuildMenuGameplayAimOffset = true;
    }

    internal static PlayerInputSnapshot ApplyBuildWheelSentryAim(
        PlayerInputSnapshot input,
        float playerX,
        float playerY,
        bool hasCapturedAimOffset,
        float capturedAimOffsetX,
        float capturedAimOffsetY)
    {
        if (!input.BuildSentry || !hasCapturedAimOffset)
        {
            return input;
        }

        return input with
        {
            AimWorldX = playerX + capturedAimOffsetX,
            AimWorldY = playerY + capturedAimOffsetY,
        };
    }

    private void BeginClosingBuildMenu()
    {
        _buildMenuOpen = false;
        _buildMenuClosing = false;
    }
}
