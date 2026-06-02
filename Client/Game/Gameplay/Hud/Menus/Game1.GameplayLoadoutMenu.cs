#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool CanOpenGameplayLoadoutMenu()
    {
        if (IsLocalSpectatorPresentationActive() || _world.LocalPlayerAwaitingJoin)
        {
            return false;
        }

        if (_networkClient.IsConnected && !IsLastToDieSessionActive)
        {
            return false;
        }

        return IsLastToDieSessionActive
            || GameplayLoadoutSelectionResolver.GetOrderedLoadouts(_world.LocalPlayer.ClassId).Count > 1;
    }

    private void LoadGameplayLoadoutMenuTextures()
    {
        _gameplayLoadoutClassStripTexture = TryLoadGameplayLoadoutMenuTexture("LoadoutStrip.png");
        _gameplayLoadoutClassSelectionTexture = TryLoadGameplayLoadoutMenuTexture("LoadoutSelectionStrip.png");
        _gameplayLoadoutBackgroundBarTexture = TryLoadGameplayLoadoutMenuTexture("LoadoutBackgroundBar.png");
        _gameplayLoadoutDescriptionBoardTexture = TryLoadGameplayLoadoutMenuTexture("DescriptionBoardS.png");
        LoadGameplayLoadoutSelectionAtlasTextures();
        _gameplayLoadoutSelectionTexture = TryLoadGameplayLoadoutMenuTexture("SelectionS2.png");
        _gameplayLoadoutScrollerTexture = TryLoadGameplayLoadoutMenuTexture("ScrollerS.png");
        _gameplayLoadoutPageTexture = TryLoadGameplayLoadoutMenuTexture("PageS.png");
        _gameplayLoadoutBackButtonTexture = TryLoadGameplayLoadoutMenuTexture("BackS.png");
        _gameplayLoadoutHelmetTexture = TryLoadGameplayLoadoutMenuTexture("HelmetS2.png");
        _gameplayLoadoutDogTagsTexture = TryLoadGameplayLoadoutMenuTexture("DogTagsS2.png");
    }

    private LoadedSpriteFrame? TryLoadGameplayLoadoutMenuTexture(string fileName)
    {
        try
        {
            return LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", fileName);
        }
        catch (Exception ex) when (OperatingSystem.IsBrowser())
        {
            AddConsoleLine($"browser skipped loadout texture {fileName}: {ex.Message}");
            return null;
        }
    }

    private void LoadGameplayLoadoutSelectionAtlasTextures()
    {
        _gameplayLoadoutSelectionAtlasTexture?.Dispose();
        _gameplayLoadoutSelectionAtlasTexture = null;

        foreach (var chunk in _gameplayLoadoutSelectionAtlasChunks)
        {
            chunk.Dispose();
        }

        _gameplayLoadoutSelectionAtlasChunks.Clear();

        if (!OperatingSystem.IsBrowser())
        {
            _gameplayLoadoutSelectionAtlasTexture = LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", "SelectionS.png");
            return;
        }

        for (var chunkIndex = 0; ; chunkIndex += 1)
        {
            var relativePath = $"Content/Sprites/Menu/RandomizerLoadout/SelectionS.chunk{chunkIndex}.png";
            if (!TryLoadBrowserFrameByRelativePath(relativePath, out var chunkTexture))
            {
                break;
            }

            _gameplayLoadoutSelectionAtlasChunks.Add(chunkTexture!);
        }

        if (_gameplayLoadoutSelectionAtlasChunks.Count == 0)
        {
            _gameplayLoadoutSelectionAtlasTexture = TryLoadGameplayLoadoutMenuTexture("SelectionS.png");
        }
    }

    private void UpdateGameplayLoadoutMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (!_gameplayLoadoutMenuOpen)
        {
            _gameplayLoadoutMenuHoverIndex = -1;
            ResetGameplayLoadoutPortraitAnimation();
            return;
        }

        if (_gameplayLoadoutMenuAwaitingEscapeRelease)
        {
            if (!keyboard.IsKeyDown(Keys.Escape))
            {
                _gameplayLoadoutMenuAwaitingEscapeRelease = false;
            }
        }
        else if (IsKeyPressed(keyboard, Keys.Escape))
        {
            CloseGameplayLoadoutMenu();
            OpenInGameMenu();
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Left))
        {
            ShiftGameplayLoadoutViewedClass(-1);
        }
        else if (IsKeyPressed(keyboard, Keys.Right))
        {
            ShiftGameplayLoadoutViewedClass(1);
        }

        var buttons = BuildGameplayLoadoutMenuButtons();
        _gameplayLoadoutMenuHoverIndex = GameplayLoadoutMenuState.GetHoveredButtonIndex(mouse, buttons);
        AdvanceGameplayLoadoutMenuPortraitAnimation(GetGameplayLoadoutMenuSafeViewedClass());

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _gameplayLoadoutMenuHoverIndex < 0 || _gameplayLoadoutMenuHoverIndex >= buttons.Count)
        {
            return;
        }

        buttons[_gameplayLoadoutMenuHoverIndex].Activate();
    }

    private void DrawGameplayLoadoutMenu()
    {
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, ViewportWidth, ViewportHeight), Color.Black * 0.82f);

        var viewedClass = GetGameplayLoadoutMenuSafeViewedClass();
        var loadouts = GameplayLoadoutMenuModel.BuildEntries(
            viewedClass,
            _world.LocalPlayer.ClassId,
            _world.LocalPlayer.GameplayLoadoutState.LoadoutId,
            _world.LocalPlayer.OwnsGameplayItem);
        if (loadouts.Length == 0)
        {
            return;
        }

        var selectedLoadout = GetGameplayLoadoutMenuViewedLoadoutEntry(viewedClass, loadouts);
        var buttons = BuildGameplayLoadoutMenuButtons();
        var layout = GameplayLoadoutMenuPresentation.CreateLayout(ViewportWidth, ViewportHeight);
        GameplayLoadoutMenuButton? hoveredButton = _gameplayLoadoutMenuHoverIndex >= 0 && _gameplayLoadoutMenuHoverIndex < buttons.Count
            ? buttons[_gameplayLoadoutMenuHoverIndex]
            : null;
        var highlightedClass = hoveredButton.HasValue && hoveredButton.Value.Kind == GameplayLoadoutMenuButtonKind.Class && hoveredButton.Value.ClassId.HasValue
            ? hoveredButton.Value.ClassId.Value
            : viewedClass;

        DrawGameplayLoadoutMenuBackdrop(layout);
        DrawGameplayLoadoutMenuClassStrip(layout, highlightedClass, buttons);
        DrawGameplayLoadoutMenuColumns(layout, viewedClass, selectedLoadout, buttons);
        DrawGameplayLoadoutMenuDetails(layout, viewedClass, selectedLoadout, hoveredButton);
        DrawGameplayLoadoutMenuFooter(layout, hoveredButton?.Kind == GameplayLoadoutMenuButtonKind.Back);
    }

    private List<GameplayLoadoutMenuButton> BuildGameplayLoadoutMenuButtons()
    {
        var viewedClass = GetGameplayLoadoutMenuSafeViewedClass();
        var loadouts = GameplayLoadoutMenuModel.BuildEntries(
            viewedClass,
            _world.LocalPlayer.ClassId,
            _world.LocalPlayer.GameplayLoadoutState.LoadoutId,
            _world.LocalPlayer.OwnsGameplayItem);
        var selectedLoadout = GetGameplayLoadoutMenuViewedLoadoutEntry(viewedClass, loadouts);
        var layout = GameplayLoadoutMenuPresentation.CreateLayout(ViewportWidth, ViewportHeight);
        var buttons = new List<GameplayLoadoutMenuButton>();

        for (var index = 0; index < GameplayLoadoutMenuPresentation.ClassStripOrder.Length; index += 1)
        {
            var classId = GameplayLoadoutMenuPresentation.ClassStripOrder[index];
            var bounds = GameplayLoadoutMenuPresentation.GetClassStripButtonBounds(layout, index);
            buttons.Add(new GameplayLoadoutMenuButton(
                bounds,
                () => _gameplayLoadoutMenuViewedClass = classId,
                GameplayLoadoutMenuButtonKind.Class,
                classId));
        }

        if (ShouldUseLastToDieAccessoryLoadoutColumn(viewedClass))
        {
            AddLastToDieAccessoryLoadoutButtons(layout, buttons);
        }
        else
        {
            var (leftOptions, rightOptions) = GameplayLoadoutMenuModel.GetVisualColumns(selectedLoadout, loadouts);
            for (var optionIndex = 0; optionIndex < leftOptions.Count; optionIndex += 1)
            {
                var option = leftOptions[optionIndex];
                var bounds = GameplayLoadoutMenuPresentation.GetColumnOptionBounds(layout.LeftColumnBounds, optionIndex);
                var capturedOption = option;
                buttons.Add(new GameplayLoadoutMenuButton(
                    bounds,
                    () => OnGameplayLoadoutMenuOptionSelected(viewedClass, capturedOption),
                    GameplayLoadoutMenuButtonKind.ItemOption,
                    viewedClass,
                    capturedOption.Slot,
                    capturedOption.Item.Id));
            }

            for (var optionIndex = 0; optionIndex < rightOptions.Count; optionIndex += 1)
            {
                var option = rightOptions[optionIndex];
                var bounds = GameplayLoadoutMenuPresentation.GetColumnOptionBounds(layout.RightColumnBounds, optionIndex);
                var capturedOption = option;
                buttons.Add(new GameplayLoadoutMenuButton(
                    bounds,
                    () => OnGameplayLoadoutMenuOptionSelected(viewedClass, capturedOption),
                    GameplayLoadoutMenuButtonKind.ItemOption,
                    viewedClass,
                    capturedOption.Slot,
                    capturedOption.Item.Id));
            }
        }

        buttons.Add(new GameplayLoadoutMenuButton(
            layout.BackButtonBounds,
            () =>
            {
                CloseGameplayLoadoutMenu();
                OpenInGameMenu();
            },
            GameplayLoadoutMenuButtonKind.Back));

        return buttons;
    }

    private void ShiftGameplayLoadoutViewedClass(int delta)
    {
        _gameplayLoadoutMenuViewedClass = GameplayLoadoutMenuState.GetShiftedViewedClass(
            _gameplayLoadoutMenuViewedClass,
            _world.LocalPlayer.ClassId,
            delta);
    }

    private void OnGameplayLoadoutMenuOptionSelected(PlayerClass classId, GameplayLoadoutMenuSlotOption option)
    {
        SetGameplayLoadoutMenuViewedLoadoutId(classId, option.Loadout.Loadout.Id);
        if (classId != _world.LocalPlayer.ClassId)
        {
            SetNetworkStatus($"Browsing {CharacterClassCatalog.GetDefinition(classId).DisplayName} loadouts. Change class to equip.");
            return;
        }

        ApplyGameplayLoadoutSelection(option.Loadout);
    }

    private void ApplyGameplayLoadoutSelection(GameplayLoadoutMenuEntry loadout)
    {
        SetGameplayLoadoutMenuViewedLoadoutId(_world.LocalPlayer.ClassId, loadout.Loadout.Id);

        if (!loadout.IsAvailable)
        {
            SetNetworkStatus("Loadout locked.");
            return;
        }

        if (_networkClient.IsConnected)
        {
            _networkClient.QueueGameplayLoadoutSelection(loadout.Loadout.Id);
            SetNetworkStatus($"Equipping {loadout.Loadout.DisplayName}...");
            return;
        }

        if (_world.TrySetNetworkPlayerGameplayLoadout(SimulationWorld.LocalPlayerSlot, loadout.Loadout.Id))
        {
            SetNetworkStatus($"{loadout.Loadout.DisplayName} equipped.");
        }
        else
        {
            SetNetworkStatus("Loadout change rejected.");
        }
    }

    private PlayerClass GetGameplayLoadoutMenuSafeViewedClass()
    {
        if (IsLastToDieSessionActive && _lastToDieRun?.SurvivorKind == LastToDieSurvivorKind.Soldier)
        {
            return PlayerClass.Soldier;
        }

        return GameplayLoadoutMenuState.GetSafeViewedClass(_gameplayLoadoutMenuViewedClass, _world.LocalPlayer.ClassId);
    }

    private GameplayLoadoutMenuEntry GetGameplayLoadoutMenuViewedLoadoutEntry(PlayerClass classId, GameplayLoadoutMenuEntry[]? entries = null)
    {
        entries ??= GameplayLoadoutMenuModel.BuildEntries(
            classId,
            _world.LocalPlayer.ClassId,
            _world.LocalPlayer.GameplayLoadoutState.LoadoutId,
            _world.LocalPlayer.OwnsGameplayItem);
        if (entries.Length == 0)
        {
            throw new InvalidOperationException("Viewed class must have at least one loadout.");
        }

        var viewedLoadoutId = GetGameplayLoadoutMenuViewedLoadoutId(classId, entries);
        return GameplayLoadoutMenuState.ResolveViewedLoadoutEntry(entries, viewedLoadoutId);
    }

    private string GetGameplayLoadoutMenuViewedLoadoutId(PlayerClass classId, GameplayLoadoutMenuEntry[]? entries = null)
    {
        entries ??= GameplayLoadoutMenuModel.BuildEntries(
            classId,
            _world.LocalPlayer.ClassId,
            _world.LocalPlayer.GameplayLoadoutState.LoadoutId,
            _world.LocalPlayer.OwnsGameplayItem);
        if (entries.Length == 0)
        {
            return string.Empty;
        }

        _gameplayLoadoutMenuViewedLoadoutIds.TryGetValue(classId, out var viewedLoadoutId);
        viewedLoadoutId = GameplayLoadoutMenuState.ResolveViewedLoadoutId(
            classId,
            _world.LocalPlayer.ClassId,
            _world.LocalPlayer.GameplayLoadoutState.LoadoutId,
            viewedLoadoutId,
            entries);
        _gameplayLoadoutMenuViewedLoadoutIds[classId] = viewedLoadoutId;
        return viewedLoadoutId;
    }

    private void SetGameplayLoadoutMenuViewedLoadoutId(PlayerClass classId, string loadoutId)
    {
        _gameplayLoadoutMenuViewedLoadoutIds[classId] = loadoutId;
    }

    private void DrawGameplayLoadoutMenuBackdrop(GameplayLoadoutMenuLayout layout)
    {
        _spriteBatch.Draw(_pixel, layout.PanelBounds, new Color(62, 60, 73));
        _spriteBatch.Draw(_pixel, new Rectangle(layout.PanelBounds.X, layout.PanelBounds.Y + (int)MathF.Round(86f * layout.Scale), layout.PanelBounds.Width, (int)MathF.Round(446f * layout.Scale)), new Color(211, 207, 202));
        _spriteBatch.Draw(_pixel, layout.HeaderBounds, new Color(180, 173, 155));
        _spriteBatch.Draw(_pixel, layout.FooterBounds, new Color(191, 176, 146));
        DrawMenuBitmapFontText(GameplayLoadoutMenuModel.GetRmClassName(GetGameplayLoadoutMenuSafeViewedClass()), new Vector2(layout.PanelBounds.X + 18f * layout.Scale, layout.PanelBounds.Y + 110f * layout.Scale), new Color(245, 238, 224), 1.18f * layout.Scale);
    }

    private void DrawGameplayLoadoutMenuClassStrip(
        GameplayLoadoutMenuLayout layout,
        PlayerClass viewedClass,
        List<GameplayLoadoutMenuButton> buttons)
    {
        if (_gameplayLoadoutClassSelectionTexture is not null)
        {
            DrawLoadedSpriteFrame(_gameplayLoadoutClassSelectionTexture, layout.ClassStripBounds, Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, layout.ClassStripBounds, new Color(84, 78, 71));
        }

        for (var index = 0; index < buttons.Count; index += 1)
        {
            var button = buttons[index];
            if (button.Kind != GameplayLoadoutMenuButtonKind.Class || !button.ClassId.HasValue)
            {
                continue;
            }

            var isViewed = button.ClassId.Value == viewedClass;
            if (isViewed)
            {
                var iconScale = layout.Scale;
                var teamOffset = _world.LocalPlayer.Team == PlayerTeam.Blue ? 10 : 0;
                var iconPosition = GameplayLoadoutMenuPresentation.GetClassStripIconPosition(layout, button.ClassId.Value);
                TryDrawScreenSprite("ClassSelectSpritesS", GameplayLoadoutMenuPresentation.GetClassSelectFrame(button.ClassId.Value) + teamOffset, iconPosition, Color.White, new Vector2(iconScale, iconScale));
            }
        }
    }

    private void DrawGameplayLoadoutMenuColumns(
        GameplayLoadoutMenuLayout layout,
        PlayerClass viewedClass,
        GameplayLoadoutMenuEntry selectedLoadout,
        List<GameplayLoadoutMenuButton> buttons)
    {
        var loadouts = GameplayLoadoutMenuModel.BuildEntries(
            viewedClass,
            _world.LocalPlayer.ClassId,
            _world.LocalPlayer.GameplayLoadoutState.LoadoutId,
            _world.LocalPlayer.OwnsGameplayItem);
        if (ShouldUseLastToDieAccessoryLoadoutColumn(viewedClass))
        {
            DrawLastToDieAccessoryLoadoutColumn(layout, layout.LeftColumnBounds, buttons);
            return;
        }

        var (leftOptions, rightOptions) = GameplayLoadoutMenuModel.GetVisualColumns(selectedLoadout, loadouts);
        DrawGameplayLoadoutMenuOptionColumn(layout, layout.LeftColumnBounds, leftOptions, buttons, viewedClass == _world.LocalPlayer.ClassId);
        DrawGameplayLoadoutMenuOptionColumn(layout, layout.RightColumnBounds, rightOptions, buttons, viewedClass == _world.LocalPlayer.ClassId);
    }

    private void DrawGameplayLoadoutMenuOptionColumn(
        GameplayLoadoutMenuLayout layout,
        Rectangle columnBounds,
        IReadOnlyList<GameplayLoadoutMenuSlotOption> options,
        List<GameplayLoadoutMenuButton> buttons,
        bool canEquipClass)
    {
        var selectedOptionIndex = 0;
        for (var optionIndex = 0; optionIndex < options.Count; optionIndex += 1)
        {
            if (!options[optionIndex].IsSelected)
            {
                continue;
            }

            selectedOptionIndex = optionIndex;
            break;
        }
        if (_gameplayLoadoutScrollerTexture is not null)
        {
            var frameWidth = _gameplayLoadoutScrollerTexture.Width / 5;
            var source = new Rectangle(frameWidth * selectedOptionIndex, 0, frameWidth, _gameplayLoadoutScrollerTexture.Height);
            DrawLoadedSpriteFrame(_gameplayLoadoutScrollerTexture, columnBounds.Location.ToVector2(), source, Color.White, 0f, Vector2.Zero, new Vector2(columnBounds.Width / (float)source.Width, columnBounds.Height / (float)source.Height), SpriteEffects.None, 0f);
        }
        else
        {
            _spriteBatch.Draw(_pixel, columnBounds, new Color(90, 82, 63));
        }

        for (var optionIndex = 0; optionIndex < 5; optionIndex += 1)
        {
            var bounds = GameplayLoadoutMenuPresentation.GetColumnOptionBounds(columnBounds, optionIndex);
            if (optionIndex >= options.Count)
            {
                continue;
            }

            var option = options[optionIndex];
            var hovered = false;
            if (_gameplayLoadoutMenuHoverIndex >= 0 && _gameplayLoadoutMenuHoverIndex < buttons.Count)
            {
                var hoveredButton = buttons[_gameplayLoadoutMenuHoverIndex];
                hovered = hoveredButton.Kind == GameplayLoadoutMenuButtonKind.ItemOption
                    && hoveredButton.Slot == option.Slot
                    && string.Equals(hoveredButton.ItemId, option.Item.Id, StringComparison.Ordinal)
                    && hoveredButton.Bounds == bounds;
            }

            DrawGameplayLoadoutMenuOption(bounds, option, hovered, canEquipClass);
        }
    }

    private void DrawGameplayLoadoutMenuDetails(
        GameplayLoadoutMenuLayout layout,
        PlayerClass viewedClass,
        GameplayLoadoutMenuEntry selectedLoadout,
        GameplayLoadoutMenuButton? hoveredButton)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var detailItemId = hoveredButton.HasValue && hoveredButton.Value.Kind == GameplayLoadoutMenuButtonKind.ItemOption
            ? hoveredButton.Value.ItemId
            : selectedLoadout.Loadout.PrimaryItemId;
        var detailItem = !string.IsNullOrWhiteSpace(detailItemId)
            ? runtimeRegistry.GetRequiredItem(detailItemId)
            : runtimeRegistry.GetRequiredItem(selectedLoadout.Loadout.PrimaryItemId);

        DrawGameplayLoadoutMenuPreview(layout, viewedClass, detailItem);

        if (_gameplayLoadoutDescriptionBoardTexture is not null)
        {
            DrawLoadedSpriteFrame(_gameplayLoadoutDescriptionBoardTexture, layout.DescriptionBounds, Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, layout.DescriptionBounds, new Color(143, 141, 145));
        }

        var cursorY = layout.DescriptionBounds.Y + 38f * layout.Scale;
        var textX = layout.DescriptionBounds.X + 22f * layout.Scale;
        var detailLines = ShouldUseLastToDieAccessoryLoadoutColumn(viewedClass)
            ? BuildLastToDieAccessoryLoadoutBoardLines(
                hoveredButton.HasValue && hoveredButton.Value.Kind == GameplayLoadoutMenuButtonKind.AccessoryOption
                    ? hoveredButton.Value.ItemId
                    : null)
            : GameplayLoadoutMenuTextBuilder.BuildBoardLines(viewedClass, detailItem, _config.TicksPerSecond);
        foreach (var line in detailLines)
        {
            foreach (var wrapped in WrapMenuBitmapText(line.Text, layout.DescriptionBounds.Width - (44f * layout.Scale), 0.54f * layout.Scale))
            {
                DrawMenuBitmapFontText(wrapped, new Vector2(textX, cursorY), line.Color, 0.54f * layout.Scale);
                cursorY += 15f * layout.Scale;
            }
        }
    }

    private void DrawGameplayLoadoutMenuFooter(GameplayLoadoutMenuLayout layout, bool backHovered)
    {
        if (_gameplayLoadoutBackButtonTexture is not null)
        {
            var source = new Rectangle(
                backHovered
                    ? _gameplayLoadoutBackButtonTexture.Width / 2
                    : 0,
                0,
                _gameplayLoadoutBackButtonTexture.Width / 2,
                _gameplayLoadoutBackButtonTexture.Height);
            DrawLoadedSpriteFrame(_gameplayLoadoutBackButtonTexture, layout.BackButtonBounds.Location.ToVector2(), source, Color.White, 0f, Vector2.Zero, new Vector2(layout.BackButtonBounds.Width / (float)source.Width, layout.BackButtonBounds.Height / (float)source.Height), SpriteEffects.None, 0f);
        }
        else
        {
            _spriteBatch.Draw(_pixel, layout.BackButtonBounds, new Color(213, 201, 180));
        }
    }

    private void DrawGameplayLoadoutMenuPreview(GameplayLoadoutMenuLayout layout, PlayerClass viewedClass, GameplayItemDefinition detailItem)
    {
        var portraitPosition = new Vector2(layout.PanelBounds.X + 300f * layout.Scale, layout.PanelBounds.Y + 242f * layout.Scale);
        TryDrawGameplayLoadoutMenuPortraitAnimation(viewedClass, portraitPosition, Color.White, 4f * layout.Scale);
    }

    private void DrawGameplayLoadoutMenuOption(Rectangle bounds, GameplayLoadoutMenuSlotOption option, bool hovered, bool canEquipClass)
    {
        var usedSelectionAtlas = false;
        if (GameplayLoadoutMenuPresentation.TryGetSelectionFrame(option.Item.Id, out var frameIndex))
        {
            var drawFrame = option.IsSelected ? frameIndex + 90 : frameIndex;
            var source = new Rectangle(drawFrame * 40, 0, 40, 24);
            if (TryDrawGameplayLoadoutSelectionAtlas(bounds, source))
            {
                usedSelectionAtlas = true;
            }
            else
            {
                _spriteBatch.Draw(_pixel, bounds, option.IsSelected ? new Color(232, 221, 177) : new Color(52, 47, 35));
            }
        }
        else
        {
            _spriteBatch.Draw(_pixel, bounds, option.IsSelected ? new Color(232, 221, 177) : new Color(52, 47, 35));
        }

        if (hovered)
        {
            DrawGameplayLoadoutMenuOutline(bounds, new Color(255, 239, 198), 2);
        }

        if (!usedSelectionAtlas)
        {
            var textColor = option.IsSelected ? new Color(51, 40, 31) : new Color(240, 233, 221);
            DrawCenteredMenuFontText(option.Item.DisplayName, bounds, textColor, 1f, 0.5f);
        }

        if (!option.Loadout.IsAvailable)
        {
            DrawGameplayLoadoutMenuOutline(bounds, new Color(154, 107, 100), 2);
        }
    }

    private bool TryDrawGameplayLoadoutSelectionAtlas(Rectangle destination, Rectangle source)
    {
        if (_gameplayLoadoutSelectionAtlasTexture is not null)
        {
            DrawLoadedSpriteFrame(_gameplayLoadoutSelectionAtlasTexture, destination.Location.ToVector2(), source, Color.White, 0f, Vector2.Zero, new Vector2(destination.Width / (float)source.Width, destination.Height / (float)source.Height), SpriteEffects.None, 0f);
            return true;
        }

        if (_gameplayLoadoutSelectionAtlasChunks.Count == 0)
        {
            return false;
        }

        var sourceOffset = source.X;
        foreach (var chunk in _gameplayLoadoutSelectionAtlasChunks)
        {
            if (sourceOffset < chunk.Width)
            {
                var chunkSource = new Rectangle(sourceOffset, source.Y, source.Width, source.Height);
                DrawLoadedSpriteFrame(chunk, destination.Location.ToVector2(), chunkSource, Color.White, 0f, Vector2.Zero, new Vector2(destination.Width / (float)chunkSource.Width, destination.Height / (float)chunkSource.Height), SpriteEffects.None, 0f);
                return true;
            }

            sourceOffset -= chunk.Width;
        }

        return false;
    }

    private void DrawGameplayLoadoutMenuOutline(Rectangle bounds, Color color, int thickness)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }

    private void AdvanceGameplayLoadoutMenuPortraitAnimation(PlayerClass viewedClass)
    {
        var hoverIndex = GameplayLoadoutMenuPresentation.GetClassSelectFrame(viewedClass);
        var previewTeam = _world.LocalPlayerTeam;
        if (_gameplayLoadoutPortraitAnimationHoverIndex != hoverIndex || _gameplayLoadoutPortraitAnimationTeam != previewTeam)
        {
            _gameplayLoadoutPortraitAnimationHoverIndex = hoverIndex;
            _gameplayLoadoutPortraitAnimationTeam = previewTeam;
            _gameplayLoadoutPortraitAnimationFrame = 0f;
            return;
        }

        var spriteName = GetClassSelectPortraitAnimationSpriteName(hoverIndex);
        if (spriteName is null)
        {
            return;
        }

        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        var perTeamFrames = Math.Max(1, sprite.Frames.Count / 2);
        var maxFrame = perTeamFrames - 1;
        if (maxFrame <= 0)
        {
            _gameplayLoadoutPortraitAnimationFrame = 0f;
            return;
        }

        _gameplayLoadoutPortraitAnimationFrame = MathF.Min(maxFrame, _gameplayLoadoutPortraitAnimationFrame + GetClassSelectPortraitAnimationAdvance(_clientUpdateElapsedSeconds));
    }

    private bool TryDrawGameplayLoadoutMenuPortraitAnimation(PlayerClass viewedClass, Vector2 position, Color tint, float scale)
    {
        var hoverIndex = GameplayLoadoutMenuPresentation.GetClassSelectFrame(viewedClass);
        var spriteName = GetClassSelectPortraitAnimationSpriteName(hoverIndex);
        if (spriteName is not null)
        {
            var sprite = GetResolvedSprite(spriteName);
            if (sprite is not null && sprite.Frames.Count > 0)
            {
                var perTeamFrames = Math.Max(1, sprite.Frames.Count / 2);
                var teamOffset = _world.LocalPlayerTeam == PlayerTeam.Blue ? perTeamFrames : 0;
                var frameIndex = teamOffset + Math.Clamp((int)MathF.Floor(_gameplayLoadoutPortraitAnimationFrame), 0, perTeamFrames - 1);
                if (TryDrawScreenSprite(spriteName, frameIndex, position, tint, new Vector2(scale, scale)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void ResetGameplayLoadoutPortraitAnimation()
    {
        _gameplayLoadoutPortraitAnimationHoverIndex = -1;
        _gameplayLoadoutPortraitAnimationTeam = null;
        _gameplayLoadoutPortraitAnimationFrame = 0f;
    }

    private IReadOnlyList<string> WrapMenuBitmapText(string text, float maxWidth, float scale)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var lines = new List<string>();
        var currentLine = string.Empty;
        for (var index = 0; index < words.Length; index += 1)
        {
            var candidate = string.IsNullOrEmpty(currentLine)
                ? words[index]
                : $"{currentLine} {words[index]}";
            if (!string.IsNullOrEmpty(currentLine) && MeasureMenuBitmapFontWidth(candidate, scale) > maxWidth)
            {
                lines.Add(currentLine);
                currentLine = words[index];
            }
            else
            {
                currentLine = candidate;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }

        return lines;
    }

}
