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
    private const float SentryBuildNoticeCost = 100f;
    private const float BuildMenuListDefaultX = 37f;
    private const float BuildMenuListSpriteOriginY = 68f;
    private const float BuildMenuListWidth = 62f;
    private const float BuildMenuListHeight = 137f;
    // Hole top-lefts in build_menu.png (icon tiles are 32x36).
    private static readonly Vector2 BuildMenuSentryIconOffset = new(26f, 12f - BuildMenuListSpriteOriginY);
    private static readonly Vector2 BuildMenuDispenserIconOffset = new(26f, 50f - BuildMenuListSpriteOriginY);
    private static readonly Vector2 BuildMenuJumpPadIconOffset = new(26f, 88f - BuildMenuListSpriteOriginY);

    private bool _buildMenuNumericInputConsumed;
    private bool _buildMenuJumpPadSelectionPressed;
    private bool _buildMenuDestroyJumpPadSelectionPressed;
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

    private bool IsBuildMenuWheelEnabled
        => OpenGarrisonPreferencesDocument.NormalizeBuildMenuStyle(_clientSettings.BuildMenuStyle) == BuildMenuStyle.Wheel;

    private void ResetBuildMenuInputSelection()
    {
        _buildMenuNumericInputConsumed = false;
        _buildMenuJumpPadSelectionPressed = false;
        _buildMenuDestroyJumpPadSelectionPressed = false;
        _buildMenuSentrySelectionPressed = false;
        _buildMenuDispenserSelectionPressed = false;
        _buildMenuDestroySentrySelectionPressed = false;
        _buildMenuDestroyDispenserSelectionPressed = false;
    }

    private PlayerInputSnapshot ApplyBuildMenuInputSelection(PlayerInputSnapshot input)
    {
        if (!IsBuildMenuWheelEnabled)
        {
            // Constructor M2/skill open the side build menu; only menu selections
            // may reach the simulation as build/destroy commands.
            if (_world.LocalPlayer.ClassId == PlayerClass.Engineer)
            {
                input = input with { FireSecondary = false, UseAbility = false };
            }

            if (_buildMenuOpen || _buildMenuNumericInputConsumed)
            {
                input = input with
                {
                    FirePrimary = false,
                    FireSecondary = false,
                    UseAbility = false,
                    SwapWeapon = false,
                    ToggleSecondaryWeapon = false,
                    InteractWeapon = false,
                    BuildSentry = false,
                    DestroySentry = false,
                    BuildDispenser = false,
                    DestroyDispenser = false,
                    BuildJumpPad = false,
                    DestroyJumpPad = false,
                };
            }

            if (!_buildMenuNumericInputConsumed)
            {
                return input;
            }

            return input with
            {
                BuildSentry = _buildMenuSentrySelectionPressed,
                BuildDispenser = _buildMenuDispenserSelectionPressed,
                DestroySentry = _buildMenuDestroySentrySelectionPressed,
                DestroyDispenser = _buildMenuDestroyDispenserSelectionPressed,
                BuildJumpPad = _buildMenuJumpPadSelectionPressed,
                DestroyJumpPad = _buildMenuDestroyJumpPadSelectionPressed,
            };
        }

        // Constructor's utility binding opens the wheel. Building commands come
        // only from explicit menu selections — not raw UseAbility / M2.
        if (_world.LocalPlayer.ClassId == PlayerClass.Engineer)
            input = input with { UseAbility = false, FireSecondary = false };

        if (_buildMenuOpen || _buildMenuNumericInputConsumed)
        {
            input = input with { FirePrimary = false, FireSecondary = false, UseAbility = false,
                SwapWeapon = false, ToggleSecondaryWeapon = false, InteractWeapon = false,
                BuildSentry = false, DestroySentry = false, BuildDispenser = false, DestroyDispenser = false,
                BuildJumpPad = false, DestroyJumpPad = false };
        }
        if (!_buildMenuNumericInputConsumed) return input;
        var selectedInput = input with
        {
            BuildSentry = _buildMenuSentrySelectionPressed,
            BuildDispenser = _buildMenuDispenserSelectionPressed,
            DestroySentry = _buildMenuDestroySentrySelectionPressed,
            DestroyDispenser = _buildMenuDestroyDispenserSelectionPressed,
            BuildJumpPad = _buildMenuJumpPadSelectionPressed,
            DestroyJumpPad = _buildMenuDestroyJumpPadSelectionPressed,
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
        if (!IsBuildMenuWheelEnabled)
        {
            DrawBuildMenuListHud(resolved);
            return;
        }

        var sprite = GetResolvedSprite("BuildWheelS");
        if (sprite is null || sprite.Frames.Count < 14) return;
        var center = resolved.Origin;
        var scale = resolved.Layout.Scale;
        void DrawFrame(int index)
        {
            var frame = sprite.Frames[index];
            _spriteBatch.Draw(frame.Texture, center, frame.SourceRectangle, Color.White,
                0f, new Vector2(BuildWheelPresentation.SpriteCenter), scale, SpriteEffects.None, 0f);
        }

        // Chrome first: sectors, highlight, outline, then cancel.
        var selected = _buildMenuOpen ? _buildWheelSelectedSlot : 0;
        for (var slot = 0; slot <= BuildWheelPresentation.SlotCount; slot++)
        {
            DrawFrame(BuildWheelPresentation.GetChromeFrame(slot, selected == slot));
        }
        DrawFrame(0);
        DrawFrame(13);

        // Building icons last, in an opaque pass so translucent sector fills cannot show through.
        var blue = _world.LocalPlayer.Team == PlayerTeam.Blue;
        _spriteBatch.End();
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.Opaque,
            rasterizerState: RasterizerState.CullNone);
        for (var slot = 1; slot <= BuildWheelPresentation.SlotCount; slot++)
        {
            var exists = HasBuildWheelStructure(slot);
            var canBuild = CanAffordBuildMenuSlot(slot);
            var iconPos = center
                + BuildWheelPresentation.GetIconOffset(slot) * scale
                - BuildWheelPresentation.IconTopLeftOrigin * scale;
            DrawBuildMenuWheelBuildingIconPlate(
                BuildWheelPresentation.GetIconSpriteName(slot),
                iconPos,
                GetBuildMenuListIconFrame(blue, exists, canBuild),
                scale);
        }

        _spriteBatch.End();
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            blendState: BlendState.AlphaBlend,
            rasterizerState: RasterizerState.CullNone);
        for (var slot = 1; slot <= BuildWheelPresentation.SlotCount; slot++)
        {
            if (!HasBuildWheelStructure(slot))
            {
                continue;
            }

            var iconPos = center
                + BuildWheelPresentation.GetIconOffset(slot) * scale
                - BuildWheelPresentation.IconTopLeftOrigin * scale;
            TryDrawScreenSprite("BuildMenuDestroyOverlayS", 0, iconPos, Color.White, new Vector2(scale));
        }

        var label = BuildWheelPresentation.GetSlotLabel(selected);
        if (selected != 0) label = (HasBuildWheelStructure(selected) ? "Destroy " : "Build ") + label;
        DrawBitmapFontText(label, center + new Vector2(-MeasureBitmapFontWidth(label, 1f) / 2f, 110f * scale), Color.White, 1f);
        UpdateHudElementBounds(HudElementId.ClassEngineerBuildMenu,
            new Rectangle((int)(center.X - 100f * scale), (int)(center.Y - 100f * scale), (int)(201f * scale), (int)(201f * scale)));
    }

    private void DrawBuildMenuWheelBuildingIconPlate(string spriteName, Vector2 position, int frameIndex, float scale)
    {
        var iconSprite = GetResolvedSprite(spriteName);
        if (iconSprite is null || iconSprite.Frames.Count == 0)
        {
            return;
        }

        var frame = iconSprite.Frames[Math.Clamp(frameIndex, 0, iconSprite.Frames.Count - 1)];
        _spriteBatch.Draw(
            frame.Texture,
            position,
            frame.SourceRectangle,
            Color.White,
            0f,
            iconSprite.Origin.ToVector2(),
            new Vector2(scale),
            SpriteEffects.None,
            0f);
    }

    private void DrawBuildMenuListHud(HudResolvedElement resolved)
    {
        var scale = MathF.Max(0.01f, resolved.Layout.Scale);
        var origin = resolved.Origin;
        if (!_hudLayoutProfile.Overrides.ContainsKey(HudElementId.ClassEngineerBuildMenu))
        {
            origin = HudLayoutResolver.ResolveOrigin(
                HudAnchor.CenterLeft,
                new Vector2(BuildMenuListDefaultX, 0f),
                ViewportWidth,
                ViewportHeight);
        }

        var animatedOrigin = _hudEditorOpen && !_buildMenuOpen
            ? origin
            : origin + new Vector2(_buildMenuX - BuildMenuListDefaultX, 0f);
        var alpha = _hudEditorOpen && !_buildMenuOpen ? 1f : _buildMenuAlpha;
        var tint = Color.White * alpha;
        TryDrawScreenSprite("BuildMenuS", 0, animatedOrigin, tint, new Vector2(scale));

        var player = _world.LocalPlayer;
        var blue = player.Team == PlayerTeam.Blue;
        DrawBuildMenuBuildingIcon(
            "BuildMenuSentryIconS",
            animatedOrigin + BuildMenuSentryIconOffset * scale,
            blue,
            GetLocalOwnedSentry() is not null,
            CanAffordBuildMenuSlot(1),
            tint,
            scale);
        DrawBuildMenuBuildingIcon(
            "BuildMenuDispenserIconS",
            animatedOrigin + BuildMenuDispenserIconOffset * scale,
            blue,
            _gameplayEngineerHudController.GetLocalOwnedDispenser() is not null,
            CanAffordBuildMenuSlot(2),
            tint,
            scale);
        DrawBuildMenuBuildingIcon(
            "BuildMenuJumpPadIconS",
            animatedOrigin + BuildMenuJumpPadIconOffset * scale,
            blue,
            HasLocalOwnedJumpPad(),
            CanAffordBuildMenuSlot(3),
            tint,
            scale);

        UpdateHudElementBounds(
            HudElementId.ClassEngineerBuildMenu,
            new Rectangle(
                (int)MathF.Round(animatedOrigin.X),
                (int)MathF.Round(animatedOrigin.Y - (BuildMenuListSpriteOriginY * scale)),
                Math.Max(1, (int)MathF.Round(BuildMenuListWidth * scale)),
                Math.Max(1, (int)MathF.Round(BuildMenuListHeight * scale))));
    }

    private void DrawBuildMenuBuildingIcon(
        string spriteName,
        Vector2 position,
        bool blue,
        bool exists,
        bool canBuild,
        Color tint,
        float scale)
    {
        var frameIndex = GetBuildMenuListIconFrame(blue, exists, canBuild);
        TryDrawScreenSprite(spriteName, frameIndex, position, tint, new Vector2(scale));
        if (exists)
        {
            TryDrawScreenSprite("BuildMenuDestroyOverlayS", 0, position, tint, new Vector2(scale));
        }
    }

    private static int GetBuildMenuListIconFrame(bool blue, bool exists, bool canBuild)
    {
        // Per team: ready, nonuts. Destroy uses BuildMenuDestroyOverlayS.
        var teamBase = blue ? 2 : 0;
        if (exists || canBuild)
        {
            return teamBase;
        }

        return teamBase + 1;
    }

    private bool CanAffordBuildMenuSlot(int slot)
    {
        var player = _world.LocalPlayer;
        if (player.IsInSpawnRoom)
        {
            return false;
        }

        return GetPlayerMetal(player) >= GetBuildMenuListCost(slot);
    }

    private float GetBuildMenuListCost(int slot) => slot switch
    {
        1 => SentryBuildNoticeCost,
        2 => DispenserBuildNoticeCost,
        _ => JumpPadBuildNoticeCost,
    };

    private bool HasBuildWheelStructure(int slot) => slot switch
    {
        1 => GetLocalOwnedSentry() is not null,
        2 => _gameplayEngineerHudController.GetLocalOwnedDispenser() is not null,
        3 => HasLocalOwnedJumpPad(),
        _ => false,
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
        return BuildWheelPresentation.GetSlotFromPointer(
            MathF.Atan2(delta.Y, delta.X) * (180f / MathF.PI) + 90f,
            delta.Length());
    }

    private void CommitBuildWheelSelection(int slot)
    {
        if (slot is < 1 or > BuildWheelPresentation.SlotCount)
        {
            _buildMenuNumericInputConsumed = true;
            BeginClosingBuildMenu();
            return;
        }

        // Match list-menu rules: destroy if owned, otherwise require metal / not in spawn.
        CommitBuildMenuListSlot(slot);
    }

    private void CommitBuildMenuListSlot(int slot)
    {
        var exists = slot switch
        {
            1 => GetLocalOwnedSentry() is not null,
            2 => _gameplayEngineerHudController.GetLocalOwnedDispenser() is not null,
            3 => HasLocalOwnedJumpPad(),
            _ => false,
        };

        if (exists)
        {
            _buildMenuNumericInputConsumed = true;
            if (slot == 1) _buildMenuDestroySentrySelectionPressed = true;
            else if (slot == 2) _buildMenuDestroyDispenserSelectionPressed = true;
            else if (slot == 3) _buildMenuDestroyJumpPadSelectionPressed = true;
            BeginClosingBuildMenu();
            return;
        }

        var cost = GetBuildMenuListCost(slot);
        if (IsEngineerBuildResourceInsufficient(GetPlayerMetal(_world.LocalPlayer), cost))
        {
            ShowNotice(NoticeKind.NutsNBolts);
            return;
        }

        if (!CanAffordBuildMenuSlot(slot))
        {
            // Enough metal but otherwise blocked (e.g. spawn room) — keep the menu open.
            return;
        }

        _buildMenuNumericInputConsumed = true;
        if (slot == 1) _buildMenuSentrySelectionPressed = true;
        else if (slot == 2) _buildMenuDispenserSelectionPressed = true;
        else if (slot == 3) _buildMenuJumpPadSelectionPressed = true;
        BeginClosingBuildMenu();
    }

    private void UpdateBuildMenuState(KeyboardState keyboard, MouseState mouse, PlayerInputSnapshot input)
    {
        ResetBuildMenuInputSelection();
        if (!IsBuildMenuWheelEnabled)
        {
            UpdateBuildMenuListState(keyboard, mouse, input);
            return;
        }

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
        if (abilityPressed || secondaryPressed)
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
        if (digit is >= 1 and <= BuildWheelPresentation.SlotCount)
        {
            CommitBuildWheelSelection(digit.Value);
        }
        else if (digit is 0 or 4)
        {
            _buildMenuNumericInputConsumed = true;
            BeginClosingBuildMenu();
        }
        else if (leftPressed)
        {
            SuppressPrimaryFireUntilMouseRelease();
            CommitBuildWheelSelection(_buildWheelSelectedSlot);
        }
    }

    private void UpdateBuildMenuListState(KeyboardState keyboard, MouseState mouse, PlayerInputSnapshot input)
    {
        var abilityPressed = input.UseAbility && !_buildMenuAbilityWasDown;
        _buildMenuAbilityWasDown = input.UseAbility;
        var secondaryPressed = input.FireSecondary && !_buildMenuSecondaryWasDown;
        _buildMenuSecondaryWasDown = input.FireSecondary;

        if (ShouldCloseBuildMenuForGameplayState())
        {
            BeginClosingBuildMenu();
            AdvanceBuildMenuAnimation();
            return;
        }

        if (_scoreboardOpen || _bubbleMenuKind != BubbleMenuKind.None)
        {
            BeginClosingBuildMenu();
            AdvanceBuildMenuAnimation();
            return;
        }

        var player = _world.LocalPlayer;
        if (player.ClassId == PlayerClass.Engineer)
        {
            if (abilityPressed || secondaryPressed)
            {
                _buildMenuNumericInputConsumed = true;
                ToggleBuildMenuList();
            }
            else if (_buildMenuOpen && !_buildMenuClosing)
            {
                var onePressed = IsKeyPressed(keyboard, Keys.D1) || IsKeyPressed(keyboard, Keys.NumPad1);
                var twoPressed = IsKeyPressed(keyboard, Keys.D2) || IsKeyPressed(keyboard, Keys.NumPad2);
                var threePressed = IsKeyPressed(keyboard, Keys.D3) || IsKeyPressed(keyboard, Keys.NumPad3);
                var fourPressed = IsKeyPressed(keyboard, Keys.D4) || IsKeyPressed(keyboard, Keys.NumPad4);

                if (onePressed) CommitBuildMenuListSlot(1);
                else if (twoPressed) CommitBuildMenuListSlot(2);
                else if (threePressed) CommitBuildMenuListSlot(3);
                else if (fourPressed) BeginClosingBuildMenu();
            }
        }

        AdvanceBuildMenuAnimation();
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
        // Match SimulationWorld jump-pad ownership (player.Id), not the client
        // snapshot presentation key which can differ in multiplayer.
        var localPlayerId = _world.LocalPlayer.Id;
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
        if (!IsBuildMenuWheelEnabled)
        {
            ToggleBuildMenuList();
            return;
        }

        if (_buildMenuOpen) { BeginClosingBuildMenu(); return; }
        CaptureBuildMenuGameplayAim(input);
        _buildMenuOpen = true;
        _buildMenuClosing = false;
        _buildMenuAlpha = 1f;
        _buildWheelSelectedSlot = 0;
    }

    private void ToggleBuildMenuList()
    {
        if (_buildMenuOpen && !_buildMenuClosing)
        {
            BeginClosingBuildMenu();
            return;
        }

        if (_buildMenuOpen && _buildMenuClosing)
        {
            // Cancel close at full opacity so the menu never "breathes" translucent.
            _buildMenuClosing = false;
            _buildMenuAlpha = 0.99f;
            return;
        }

        _buildMenuOpen = true;
        _buildMenuClosing = false;
        _buildMenuAlpha = 0.99f;
        _buildMenuX = -BuildMenuListDefaultX;
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
        if (!IsBuildMenuWheelEnabled)
        {
            if (_buildMenuOpen)
            {
                _buildMenuClosing = true;
            }

            return;
        }

        _buildMenuOpen = false;
        _buildMenuClosing = false;
    }

    private void AdvanceBuildMenuAnimation()
    {
        if (!_buildMenuOpen)
        {
            return;
        }

        if (!_buildMenuClosing)
        {
            if (_buildMenuAlpha < 0.99f)
            {
                _buildMenuAlpha = AdvanceOpeningAlpha(_buildMenuAlpha, 0.01f, 0.99f);
            }

            if (_buildMenuX < BuildMenuListDefaultX)
            {
                _buildMenuX = MathF.Min(BuildMenuListDefaultX, _buildMenuX + ScaleLegacyUiDistance(15f));
            }

            return;
        }

        if (_buildMenuAlpha > 0.01f)
        {
            _buildMenuAlpha = AdvanceClosingAlpha(_buildMenuAlpha, 0.01f);
        }

        _buildMenuX -= ScaleLegacyUiDistance(15f);
        if (_buildMenuX < -BuildMenuListDefaultX)
        {
            _buildMenuOpen = false;
            _buildMenuClosing = false;
        }
    }
}
