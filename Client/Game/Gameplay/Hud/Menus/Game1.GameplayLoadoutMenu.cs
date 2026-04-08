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
    private static readonly PlayerClass[] GameplayLoadoutMenuClassStripOrder =
    [
        PlayerClass.Scout,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Heavy,
        PlayerClass.Demoman,
        PlayerClass.Medic,
        PlayerClass.Engineer,
        PlayerClass.Spy,
        PlayerClass.Sniper,
    ];

    private static readonly int[] GameplayLoadoutMenuClassStripXOffsets = [24, 64, 104, 156, 196, 236, 288, 328, 368];

    private const int GameplayLoadoutClassIconSourceWidth = 52;
    private const int GameplayLoadoutClassIconSourceHeight = 52;

    private bool CanOpenGameplayLoadoutMenu()
    {
        if (_networkClient.IsSpectator || _world.LocalPlayerAwaitingJoin)
        {
            return false;
        }

        return GameplayLoadoutSelectionResolver.GetOrderedLoadouts(_world.LocalPlayer.ClassId).Count > 1;
    }

    private void LoadGameplayLoadoutMenuTextures()
    {
        _gameplayLoadoutClassStripTexture = LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", "LoadoutStrip.png");
        _gameplayLoadoutClassSelectionTexture = LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", "LoadoutSelectionStrip.png");
        _gameplayLoadoutBackgroundBarTexture = LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", "LoadoutBackgroundBar.png");
        _gameplayLoadoutDescriptionBoardTexture = LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", "DescriptionBoardS.png");
        _gameplayLoadoutSelectionAtlasTexture = LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", "SelectionS.png");
        _gameplayLoadoutSelectionTexture = LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", "SelectionS2.png");
        _gameplayLoadoutScrollerTexture = LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", "ScrollerS.png");
        _gameplayLoadoutPageTexture = LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", "PageS.png");
        _gameplayLoadoutBackButtonTexture = LoadMenuTexture("Sprites", "Menu", "RandomizerLoadout", "BackS.png");
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
        _gameplayLoadoutMenuHoverIndex = GetHoveredGameplayLoadoutMenuButtonIndex(mouse, buttons);
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
        var loadouts = GetGameplayLoadoutMenuEntries(viewedClass);
        if (loadouts.Count == 0)
        {
            return;
        }

        var selectedLoadout = GetGameplayLoadoutMenuViewedLoadoutEntry(viewedClass, loadouts);
        var buttons = BuildGameplayLoadoutMenuButtons();
        var layout = GetGameplayLoadoutMenuLayout(loadouts);
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
        var loadouts = GetGameplayLoadoutMenuEntries(viewedClass);
        var selectedLoadout = GetGameplayLoadoutMenuViewedLoadoutEntry(viewedClass, loadouts);
        var layout = GetGameplayLoadoutMenuLayout(loadouts);
        var buttons = new List<GameplayLoadoutMenuButton>();

        for (var index = 0; index < GameplayLoadoutMenuClassStripOrder.Length; index += 1)
        {
            var classId = GameplayLoadoutMenuClassStripOrder[index];
            var bounds = GetGameplayLoadoutMenuClassStripButtonBounds(layout, index);
            buttons.Add(new GameplayLoadoutMenuButton(
                bounds,
                () => _gameplayLoadoutMenuViewedClass = classId,
                GameplayLoadoutMenuButtonKind.Class,
                classId));
        }

        var (leftOptions, rightOptions) = GetGameplayLoadoutMenuVisualColumns(viewedClass, selectedLoadout, loadouts);
        for (var optionIndex = 0; optionIndex < leftOptions.Count; optionIndex += 1)
        {
            var option = leftOptions[optionIndex];
            var bounds = GetGameplayLoadoutMenuColumnOptionBounds(layout.LeftColumnBounds, optionIndex);
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
            var bounds = GetGameplayLoadoutMenuColumnOptionBounds(layout.RightColumnBounds, optionIndex);
            var capturedOption = option;
            buttons.Add(new GameplayLoadoutMenuButton(
                bounds,
                () => OnGameplayLoadoutMenuOptionSelected(viewedClass, capturedOption),
                GameplayLoadoutMenuButtonKind.ItemOption,
                viewedClass,
                capturedOption.Slot,
                capturedOption.Item.Id));
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

    private static int GetHoveredGameplayLoadoutMenuButtonIndex(MouseState mouse, IReadOnlyList<GameplayLoadoutMenuButton> buttons)
    {
        var mousePoint = new Point(mouse.X, mouse.Y);
        for (var index = 0; index < buttons.Count; index += 1)
        {
            if (buttons[index].Bounds.Contains(mousePoint))
            {
                return index;
            }
        }

        return -1;
    }

    private void ShiftGameplayLoadoutViewedClass(int delta)
    {
        var currentIndex = Array.IndexOf(GameplayLoadoutMenuClassStripOrder, GetGameplayLoadoutMenuSafeViewedClass());
        if (currentIndex < 0)
        {
            _gameplayLoadoutMenuViewedClass = _world.LocalPlayer.ClassId;
            return;
        }

        var nextIndex = (currentIndex + delta + GameplayLoadoutMenuClassStripOrder.Length) % GameplayLoadoutMenuClassStripOrder.Length;
        _gameplayLoadoutMenuViewedClass = GameplayLoadoutMenuClassStripOrder[nextIndex];
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

    private IReadOnlyList<GameplayLoadoutMenuEntry> GetGameplayLoadoutMenuEntries(PlayerClass classId)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        return GameplayLoadoutSelectionResolver.GetOrderedLoadouts(classId)
            .Select(loadout => new GameplayLoadoutMenuEntry(
                loadout,
                runtimeRegistry.LoadoutItemsAreOwned(loadout, _world.LocalPlayer.OwnsGameplayItem),
                classId == _world.LocalPlayer.ClassId
                    && string.Equals(loadout.Id, _world.LocalPlayer.GameplayLoadoutState.LoadoutId, StringComparison.Ordinal)))
            .ToArray();
    }

    private PlayerClass GetGameplayLoadoutMenuSafeViewedClass()
    {
        var viewedClass = _gameplayLoadoutMenuViewedClass;
        if (GameplayLoadoutSelectionResolver.GetOrderedLoadouts(viewedClass).Count > 0)
        {
            return viewedClass;
        }

        return _world.LocalPlayer.ClassId;
    }

    private GameplayLoadoutMenuEntry GetGameplayLoadoutMenuViewedLoadoutEntry(PlayerClass classId, IReadOnlyList<GameplayLoadoutMenuEntry>? entries = null)
    {
        entries ??= GetGameplayLoadoutMenuEntries(classId);
        if (entries.Count == 0)
        {
            throw new InvalidOperationException("Viewed class must have at least one loadout.");
        }

        var viewedLoadoutId = GetGameplayLoadoutMenuViewedLoadoutId(classId, entries);
        for (var index = 0; index < entries.Count; index += 1)
        {
            if (string.Equals(entries[index].Loadout.Id, viewedLoadoutId, StringComparison.Ordinal))
            {
                return entries[index];
            }
        }

        return entries[0];
    }

    private string GetGameplayLoadoutMenuViewedLoadoutId(PlayerClass classId, IReadOnlyList<GameplayLoadoutMenuEntry>? entries = null)
    {
        entries ??= GetGameplayLoadoutMenuEntries(classId);
        if (entries.Count == 0)
        {
            return string.Empty;
        }

        if (_gameplayLoadoutMenuViewedLoadoutIds.TryGetValue(classId, out var viewedLoadoutId)
            && entries.Any(entry => string.Equals(entry.Loadout.Id, viewedLoadoutId, StringComparison.Ordinal)))
        {
            return viewedLoadoutId;
        }

        if (classId == _world.LocalPlayer.ClassId
            && entries.Any(entry => string.Equals(entry.Loadout.Id, _world.LocalPlayer.GameplayLoadoutState.LoadoutId, StringComparison.Ordinal)))
        {
            viewedLoadoutId = _world.LocalPlayer.GameplayLoadoutState.LoadoutId;
        }
        else
        {
            viewedLoadoutId = entries[0].Loadout.Id;
        }

        _gameplayLoadoutMenuViewedLoadoutIds[classId] = viewedLoadoutId;
        return viewedLoadoutId;
    }

    private void SetGameplayLoadoutMenuViewedLoadoutId(PlayerClass classId, string loadoutId)
    {
        _gameplayLoadoutMenuViewedLoadoutIds[classId] = loadoutId;
    }

    private static List<GameplayLoadoutMenuSlotOption>[] BuildGameplayLoadoutMenuSlotOptions(
        PlayerClass classId,
        GameplayLoadoutMenuEntry selectedLoadout,
        IReadOnlyList<GameplayLoadoutMenuEntry> loadouts)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var slots = new[]
        {
            GameplayEquipmentSlot.Primary,
            GameplayEquipmentSlot.Secondary,
            GameplayEquipmentSlot.Utility,
        };
        var options = new List<GameplayLoadoutMenuSlotOption>[slots.Length];
        for (var slotIndex = 0; slotIndex < slots.Length; slotIndex += 1)
        {
            var slot = slots[slotIndex];
            var seenItems = new HashSet<string>(StringComparer.Ordinal);
            var slotOptions = new List<GameplayLoadoutMenuSlotOption>();
            for (var loadoutIndex = 0; loadoutIndex < loadouts.Count; loadoutIndex += 1)
            {
                var candidate = ResolveGameplayLoadoutItemId(loadouts[loadoutIndex].Loadout, slot);
                if (string.IsNullOrWhiteSpace(candidate) || !seenItems.Add(candidate))
                {
                    continue;
                }

                var resolvedLoadout = ResolveGameplayLoadoutForSlotSelection(slot, candidate, selectedLoadout.Loadout, loadouts);
                if (resolvedLoadout is null)
                {
                    continue;
                }

                var item = runtimeRegistry.GetRequiredItem(candidate);
                slotOptions.Add(new GameplayLoadoutMenuSlotOption(
                    slot,
                    item,
                    resolvedLoadout.Value,
                    string.Equals(ResolveGameplayLoadoutItemId(selectedLoadout.Loadout, slot), candidate, StringComparison.Ordinal)));
            }

            slotOptions.Sort((left, right) => CompareGameplayLoadoutMenuSlotOptions(left, right));
            options[slotIndex] = slotOptions;
        }

        return options;
    }

    private static GameplayLoadoutMenuEntry? ResolveGameplayLoadoutForSlotSelection(
        GameplayEquipmentSlot targetSlot,
        string itemId,
        GameplayClassLoadoutDefinition selectedLoadout,
        IReadOnlyList<GameplayLoadoutMenuEntry> loadouts)
    {
        GameplayLoadoutMenuEntry? best = null;
        var bestScore = int.MinValue;
        for (var index = 0; index < loadouts.Count; index += 1)
        {
            var candidate = loadouts[index];
            if (!string.Equals(ResolveGameplayLoadoutItemId(candidate.Loadout, targetSlot), itemId, StringComparison.Ordinal))
            {
                continue;
            }

            var score = 0;
            if (string.Equals(candidate.Loadout.PrimaryItemId, selectedLoadout.PrimaryItemId, StringComparison.Ordinal))
            {
                score += targetSlot == GameplayEquipmentSlot.Primary ? 3 : 1;
            }

            if (string.Equals(candidate.Loadout.SecondaryItemId ?? string.Empty, selectedLoadout.SecondaryItemId ?? string.Empty, StringComparison.Ordinal))
            {
                score += targetSlot == GameplayEquipmentSlot.Secondary ? 3 : 1;
            }

            if (string.Equals(candidate.Loadout.UtilityItemId ?? string.Empty, selectedLoadout.UtilityItemId ?? string.Empty, StringComparison.Ordinal))
            {
                score += targetSlot == GameplayEquipmentSlot.Utility ? 3 : 1;
            }

            if (candidate.IsAvailable)
            {
                score += 2;
            }

            if (best is null || score > bestScore)
            {
                best = candidate;
                bestScore = score;
            }
        }

        return best;
    }

    private static string? ResolveGameplayLoadoutItemId(GameplayClassLoadoutDefinition loadout, GameplayEquipmentSlot slot)
    {
        return slot switch
        {
            GameplayEquipmentSlot.Primary => loadout.PrimaryItemId,
            GameplayEquipmentSlot.Secondary => loadout.SecondaryItemId,
            GameplayEquipmentSlot.Utility => loadout.UtilityItemId,
            _ => null,
        };
    }

    private (IReadOnlyList<GameplayLoadoutMenuSlotOption> Left, IReadOnlyList<GameplayLoadoutMenuSlotOption> Right) GetGameplayLoadoutMenuVisualColumns(
        PlayerClass classId,
        GameplayLoadoutMenuEntry selectedLoadout,
        IReadOnlyList<GameplayLoadoutMenuEntry> loadouts)
    {
        var slotOptions = BuildGameplayLoadoutMenuSlotOptions(classId, selectedLoadout, loadouts);
        var rightOptions = new List<GameplayLoadoutMenuSlotOption>(slotOptions[1].Count + slotOptions[2].Count);
        rightOptions.AddRange(slotOptions[1]);
        rightOptions.AddRange(slotOptions[2]);
        return (slotOptions[0], rightOptions);
    }

    private GameplayLoadoutMenuLayout GetGameplayLoadoutMenuLayout(IReadOnlyList<GameplayLoadoutMenuEntry> loadouts)
    {
        const float baseWidth = 600f;
        const float baseHeight = 600f;
        var scale = MathF.Min((ViewportWidth - 24f) / baseWidth, (ViewportHeight - 24f) / baseHeight);
        scale = MathF.Max(1f, scale);
        var panelWidth = (int)MathF.Round(baseWidth * scale);
        var panelHeight = (int)MathF.Round(baseHeight * scale);
        var panel = new Rectangle((ViewportWidth - panelWidth) / 2, (ViewportHeight - panelHeight) / 2, panelWidth, panelHeight);

        Rectangle ScaleRect(float x, float y, float width, float height)
        {
            return new Rectangle(
                panel.X + (int)MathF.Round(x * scale),
                panel.Y + (int)MathF.Round(y * scale),
                (int)MathF.Round(width * scale),
                (int)MathF.Round(height * scale));
        }

        return new GameplayLoadoutMenuLayout(
            panel,
            ScaleRect(112f, 16f, 381f, 50f),
            ScaleRect(0f, 95f, 600f, 45f),
            ScaleRect(40f, 192f, 96f, 288f),
            ScaleRect(464f, 192f, 96f, 288f),
            ScaleRect(160f, 178f, 280f, 150f),
            ScaleRect(162f, 326f, 276f, 172f),
            ScaleRect(0f, 532f, 600f, 68f),
            ScaleRect(32f, 544f, 64f, 32f),
            scale);
    }

    private void DrawGameplayLoadoutMenuBackdrop(GameplayLoadoutMenuLayout layout)
    {
        _spriteBatch.Draw(_pixel, layout.PanelBounds, new Color(62, 60, 73));
        _spriteBatch.Draw(_pixel, new Rectangle(layout.PanelBounds.X, layout.PanelBounds.Y + (int)MathF.Round(86f * layout.Scale), layout.PanelBounds.Width, (int)MathF.Round(446f * layout.Scale)), new Color(211, 207, 202));
        _spriteBatch.Draw(_pixel, layout.HeaderBounds, new Color(180, 173, 155));
        _spriteBatch.Draw(_pixel, layout.FooterBounds, new Color(191, 176, 146));
        DrawMenuBitmapFontText(GetGameplayLoadoutMenuRmClassName(GetGameplayLoadoutMenuSafeViewedClass()), new Vector2(layout.PanelBounds.X + 18f * layout.Scale, layout.PanelBounds.Y + 110f * layout.Scale), new Color(245, 238, 224), 1.18f * layout.Scale);
    }

    private void DrawGameplayLoadoutMenuClassStrip(
        GameplayLoadoutMenuLayout layout,
        PlayerClass viewedClass,
        IReadOnlyList<GameplayLoadoutMenuButton> buttons)
    {
        if (_gameplayLoadoutClassSelectionTexture is not null)
        {
            _spriteBatch.Draw(_gameplayLoadoutClassSelectionTexture, layout.ClassStripBounds, Color.White);
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
                var iconPosition = new Vector2(
                    layout.PanelBounds.X + (88f + GameplayLoadoutMenuClassStripXOffsets[Array.IndexOf(GameplayLoadoutMenuClassStripOrder, button.ClassId.Value)]) * layout.Scale,
                    layout.PanelBounds.Y + 16f * layout.Scale);
                TryDrawScreenSprite("ClassSelectSpritesS", GetGameplayLoadoutMenuClassSelectFrame(button.ClassId.Value) + teamOffset, iconPosition, Color.White, new Vector2(iconScale, iconScale));
            }
        }
    }

    private void DrawGameplayLoadoutMenuColumns(
        GameplayLoadoutMenuLayout layout,
        PlayerClass viewedClass,
        GameplayLoadoutMenuEntry selectedLoadout,
        IReadOnlyList<GameplayLoadoutMenuButton> buttons)
    {
        var loadouts = GetGameplayLoadoutMenuEntries(viewedClass);
        var (leftOptions, rightOptions) = GetGameplayLoadoutMenuVisualColumns(viewedClass, selectedLoadout, loadouts);
        DrawGameplayLoadoutMenuOptionColumn(layout, layout.LeftColumnBounds, leftOptions, buttons, viewedClass == _world.LocalPlayer.ClassId);
        DrawGameplayLoadoutMenuOptionColumn(layout, layout.RightColumnBounds, rightOptions, buttons, viewedClass == _world.LocalPlayer.ClassId);
    }

    private void DrawGameplayLoadoutMenuOptionColumn(
        GameplayLoadoutMenuLayout layout,
        Rectangle columnBounds,
        IReadOnlyList<GameplayLoadoutMenuSlotOption> options,
        IReadOnlyList<GameplayLoadoutMenuButton> buttons,
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
            _spriteBatch.Draw(_gameplayLoadoutScrollerTexture, columnBounds, source, Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, columnBounds, new Color(90, 82, 63));
        }

        for (var optionIndex = 0; optionIndex < 5; optionIndex += 1)
        {
            var bounds = GetGameplayLoadoutMenuColumnOptionBounds(columnBounds, optionIndex);
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
            _spriteBatch.Draw(_gameplayLoadoutDescriptionBoardTexture, layout.DescriptionBounds, Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, layout.DescriptionBounds, new Color(143, 141, 145));
        }

        var cursorY = layout.DescriptionBounds.Y + 38f * layout.Scale;
        var textX = layout.DescriptionBounds.X + 22f * layout.Scale;
        foreach (var line in BuildGameplayLoadoutMenuBoardLines(viewedClass, detailItem, selectedLoadout))
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
            _spriteBatch.Draw(_gameplayLoadoutBackButtonTexture, layout.BackButtonBounds, source, Color.White);
        }
        else
        {
            _spriteBatch.Draw(_pixel, layout.BackButtonBounds, new Color(213, 201, 180));
        }
    }

    private void DrawGameplayLoadoutMenuPreview(GameplayLoadoutMenuLayout layout, PlayerClass viewedClass, GameplayItemDefinition detailItem)
    {
        var portraitPosition = new Vector2(layout.PanelBounds.X + 300f * layout.Scale, layout.PanelBounds.Y + 242f * layout.Scale);
        if (!TryDrawGameplayLoadoutMenuPortraitAnimation(viewedClass, portraitPosition, Color.White, 4f * layout.Scale))
        {
            var teamOffset = _world.LocalPlayerTeam == PlayerTeam.Blue ? 10 : 0;
            TryDrawScreenSprite(
                "ClassSelectPortraitS",
                GetGameplayLoadoutMenuClassSelectFrame(viewedClass) + teamOffset,
                portraitPosition,
                Color.White,
                new Vector2(4f * layout.Scale, 4f * layout.Scale));
        }
    }

    private void DrawGameplayLoadoutMenuOption(Rectangle bounds, GameplayLoadoutMenuSlotOption option, bool hovered, bool canEquipClass)
    {
        if (TryGetGameplayLoadoutMenuSelectionFrame(option.Item.Id, out var frameIndex))
        {
            if (_gameplayLoadoutSelectionAtlasTexture is not null)
            {
                var drawFrame = option.IsSelected ? frameIndex + 90 : frameIndex;
                var source = new Rectangle(drawFrame * 40, 0, 40, 24);
                _spriteBatch.Draw(_gameplayLoadoutSelectionAtlasTexture, bounds, source, Color.White);
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

        if (!TryGetGameplayLoadoutMenuSelectionFrame(option.Item.Id, out _))
        {
            var textColor = option.IsSelected ? new Color(51, 40, 31) : new Color(240, 233, 221);
            DrawCenteredMenuFontText(option.Item.DisplayName, bounds, textColor, 1f, 0.5f);
        }

        if (!option.Loadout.IsAvailable)
        {
            DrawGameplayLoadoutMenuOutline(bounds, new Color(154, 107, 100), 2);
        }
    }

    private void DrawGameplayLoadoutMenuOutline(Rectangle bounds, Color color, int thickness)
    {
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
    }

    private static bool TryGetGameplayLoadoutMenuSelectionFrame(string itemId, out int frameIndex)
    {
        frameIndex = itemId switch
        {
            "weapon.scattergun" => 0,
            "weapon.flamethrower" => 80,
            "weapon.rocketlauncher" => 10,
            "weapon.directhit" => 11,
            "weapon.blackbox" => 13,
            "weapon.minigun" => 60,
            "weapon.tomislav" => 61,
            "weapon.brassbeast" => 63,
            "ability.heavy-sandvich" => 65,
            "weapon.revolver" => 70,
            "weapon.diamondback" => 72,
            "weapon.rifle" => 20,
            "weapon.medigun" => 45,
            "ability.medic-needlegun" => 40,
            "weapon.mine_launcher" => 30,
            "ability.demoman-detonate" => 35,
            "ability.pyro-airblast" => 85,
            "ability.spy-cloak" => 75,
            "ability.sniper-scope" => 25,
            "ability.engineer-pda" => 55,
            _ => -1,
        };

        if (frameIndex < 0)
        {
            return false;
        }

        return true;
    }

    private static int GetGameplayLoadoutMenuClassSelectFrame(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Scout => 0,
            PlayerClass.Pyro => 1,
            PlayerClass.Soldier => 2,
            PlayerClass.Heavy => 3,
            PlayerClass.Demoman => 4,
            PlayerClass.Medic => 5,
            PlayerClass.Engineer => 6,
            PlayerClass.Spy => 7,
            PlayerClass.Sniper => 8,
            _ => 0,
        };
    }

    private void AdvanceGameplayLoadoutMenuPortraitAnimation(PlayerClass viewedClass)
    {
        var hoverIndex = GetGameplayLoadoutMenuClassSelectFrame(viewedClass);
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

        var sprite = _runtimeAssets.GetSprite(spriteName);
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

        _gameplayLoadoutPortraitAnimationFrame = MathF.Min(maxFrame, _gameplayLoadoutPortraitAnimationFrame + GetClassSelectPortraitAnimationAdvance());
    }

    private bool TryDrawGameplayLoadoutMenuPortraitAnimation(PlayerClass viewedClass, Vector2 position, Color tint, float scale)
    {
        var hoverIndex = GetGameplayLoadoutMenuClassSelectFrame(viewedClass);
        var spriteName = GetClassSelectPortraitAnimationSpriteName(hoverIndex);
        if (spriteName is not null)
        {
            var sprite = _runtimeAssets.GetSprite(spriteName);
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

        return TryDrawScreenSprite(
            "ClassSelectPortraitS",
            hoverIndex + (_world.LocalPlayerTeam == PlayerTeam.Blue ? 10 : 0),
            position,
            tint,
            new Vector2(scale, scale));
    }

    private void ResetGameplayLoadoutPortraitAnimation()
    {
        _gameplayLoadoutPortraitAnimationHoverIndex = -1;
        _gameplayLoadoutPortraitAnimationTeam = null;
        _gameplayLoadoutPortraitAnimationFrame = 0f;
    }

    private static float GetGameplayLoadoutMenuClassStripScale(GameplayLoadoutMenuLayout layout)
    {
        return layout.ClassStripBounds.Width / 520f;
    }

    private static Rectangle GetGameplayLoadoutMenuClassStripButtonBounds(GameplayLoadoutMenuLayout layout, int classIndex)
    {
        return new Rectangle(
            layout.PanelBounds.X + (int)MathF.Round((88f + GameplayLoadoutMenuClassStripXOffsets[classIndex]) * layout.Scale),
            layout.PanelBounds.Y,
            (int)MathF.Round(36f * layout.Scale),
            (int)MathF.Round(60f * layout.Scale));
    }

    private static Rectangle GetGameplayLoadoutMenuColumnOptionBounds(Rectangle columnBounds, int optionIndex)
    {
        var tileWidth = (int)MathF.Round(columnBounds.Width * (80f / 96f));
        var tileHeight = columnBounds.Height / 6;
        var offsetX = (columnBounds.Width - tileWidth) / 2;
        var y = columnBounds.Y + (int)MathF.Round(columnBounds.Height * (8f / 288f)) + (optionIndex * (columnBounds.Height / 5));
        return new Rectangle(columnBounds.X + offsetX, y, tileWidth, tileHeight);
    }

    private IEnumerable<GameplayLoadoutMenuBoardLine> BuildGameplayLoadoutMenuBoardLines(PlayerClass viewedClass, GameplayItemDefinition item, GameplayLoadoutMenuEntry selectedLoadout)
    {
        _ = selectedLoadout;
        var titleColor = new Color(245, 240, 232);
        var positiveColor = new Color(109, 214, 106);
        var negativeColor = new Color(214, 93, 86);
        var neutralColor = new Color(186, 186, 186);
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;

        if (item.Slot != GameplayEquipmentSlot.Primary || !runtimeRegistry.TryGetPrimaryWeaponBinding(item.BehaviorId, out _))
        {
            yield return new GameplayLoadoutMenuBoardLine(item.DisplayName, titleColor);

            foreach (var line in BuildGameplayLoadoutMenuAbilityLines(item))
            {
                yield return new GameplayLoadoutMenuBoardLine(line, neutralColor);
            }

            yield break;
        }

        var resolvedItem = runtimeRegistry.CreatePrimaryWeaponDefinition(item);

        yield return new GameplayLoadoutMenuBoardLine(item.DisplayName, titleColor);

        var stockItem = GetGameplayLoadoutMenuStockComparisonItem(viewedClass, item.Slot);
        if (stockItem is null || string.Equals(stockItem.Id, item.Id, StringComparison.Ordinal))
        {
            foreach (var line in BuildGameplayLoadoutMenuStockStatLines(resolvedItem))
            {
                yield return new GameplayLoadoutMenuBoardLine(line, titleColor);
            }
            yield break;
        }

        var resolvedStockItem = runtimeRegistry.CreatePrimaryWeaponDefinition(stockItem);

        foreach (var line in BuildGameplayLoadoutMenuDeltaLines(resolvedItem, resolvedStockItem, positive: true))
        {
            yield return new GameplayLoadoutMenuBoardLine(line, positiveColor);
        }

        foreach (var line in BuildGameplayLoadoutMenuDeltaLines(resolvedItem, resolvedStockItem, positive: false))
        {
            yield return new GameplayLoadoutMenuBoardLine(line, negativeColor);
        }

        foreach (var line in BuildGameplayLoadoutMenuNeutralLines(item, resolvedItem, resolvedStockItem))
        {
            yield return new GameplayLoadoutMenuBoardLine(line, neutralColor);
        }
    }

    private static GameplayItemDefinition? GetGameplayLoadoutMenuStockComparisonItem(PlayerClass viewedClass, GameplayEquipmentSlot slot)
    {
        var runtimeRegistry = CharacterClassCatalog.RuntimeRegistry;
        var stockLoadout = GameplayLoadoutSelectionResolver
            .GetOrderedLoadouts(viewedClass)
            .FirstOrDefault(loadout => loadout.Id.EndsWith(".stock", StringComparison.OrdinalIgnoreCase)
                || string.Equals(loadout.DisplayName, "Stock", StringComparison.OrdinalIgnoreCase));

        if (stockLoadout is null)
        {
            return null;
        }

        var stockItemId = ResolveGameplayLoadoutItemId(stockLoadout, slot);
        if (string.IsNullOrWhiteSpace(stockItemId))
        {
            return null;
        }

        return runtimeRegistry.GetRequiredItem(stockItemId);
    }

    private static IEnumerable<string> BuildGameplayLoadoutMenuAbilityLines(GameplayItemDefinition item)
    {
        if (item.Description?.Notes is { Count: > 0 } notes)
        {
            foreach (var note in notes)
            {
                yield return note;
            }

            yield break;
        }

        if (!string.IsNullOrWhiteSpace(item.Description?.Summary))
        {
            yield return item.Description.Summary!;
            yield break;
        }

        if (item.Slot == GameplayEquipmentSlot.Secondary)
        {
            yield return "Standard secondary ability";
            yield break;
        }

        if (item.Slot == GameplayEquipmentSlot.Utility)
        {
            yield return "Standard utility ability";
        }
    }

    private IEnumerable<string> BuildGameplayLoadoutMenuStockStatLines(PrimaryWeaponDefinition item)
    {
        foreach (var line in BuildGameplayLoadoutMenuStockDamageLines(item))
        {
            yield return line;
        }

        if (item.ProjectilesPerShot > 0)
        {
            yield return BuildGameplayLoadoutMenuValueLine("Projectiles", item.ProjectilesPerShot);
        }

        if (item.MaxAmmo > 0)
        {
            yield return BuildGameplayLoadoutMenuValueLine("Clip", item.MaxAmmo);
        }

        if (item.ReloadDelayTicks > 0)
        {
            var seconds = item.ReloadDelayTicks / (float)_config.TicksPerSecond;
            yield return BuildGameplayLoadoutMenuValueLine("Refire", seconds, " sec");
        }

        if (item.AmmoReloadTicks > 0)
        {
            var seconds = item.AmmoReloadTicks / (float)_config.TicksPerSecond;
            yield return BuildGameplayLoadoutMenuValueLine("Reload", seconds, " sec");
        }

        if (item.SpreadDegrees > 0f)
        {
            yield return BuildGameplayLoadoutMenuValueLine("Spread", item.SpreadDegrees, " deg");
        }

        var projectileSpeed = item.MinShotSpeed + item.AdditionalRandomShotSpeed;
        if (projectileSpeed > 0f)
        {
            yield return BuildGameplayLoadoutMenuValueLine("Proj speed", projectileSpeed, string.Empty);
        }

        if (item.DirectHitHealAmount is float directHitHealAmount && directHitHealAmount > 0f)
        {
            yield return BuildGameplayLoadoutMenuValueLine("Direct-hit heal", directHitHealAmount, string.Empty);
        }
    }

    private static IEnumerable<string> BuildGameplayLoadoutMenuStockDamageLines(PrimaryWeaponDefinition item)
    {
        if (item.RocketCombat is { } rocket)
        {
            yield return BuildGameplayLoadoutMenuValueLine("Direct damage", rocket.DirectHitDamage);
            yield return BuildGameplayLoadoutMenuValueLine("Splash damage", rocket.ExplosionDamage, string.Empty);
            yield return BuildGameplayLoadoutMenuValueLine("Blast radius", rocket.BlastRadius, string.Empty);
            yield break;
        }

        if (item.DirectHitDamage is { } directDamage)
        {
            yield return BuildGameplayLoadoutMenuValueLine("Damage", directDamage, string.Empty);
        }

        if (item.DamagePerTick is { } damagePerTick)
        {
            yield return BuildGameplayLoadoutMenuValueLine("Damage / tick", damagePerTick, string.Empty);
        }
    }

    private static IEnumerable<string> BuildGameplayLoadoutMenuDeltaLines(PrimaryWeaponDefinition item, PrimaryWeaponDefinition stockItem, bool positive)
    {
        foreach (var line in BuildGameplayLoadoutMenuDamageDeltaLines(item, stockItem, positive))
        {
            yield return line;
        }

        if (TryBuildGameplayLoadoutMenuDeltaLine("Projectiles", item.ProjectilesPerShot, stockItem.ProjectilesPerShot, false, positive, out var projectiles))
        {
            yield return projectiles;
        }

        if (TryBuildGameplayLoadoutMenuDeltaLine("Clip", item.MaxAmmo, stockItem.MaxAmmo, false, positive, out var clip))
        {
            yield return clip;
        }

        if (TryBuildGameplayLoadoutMenuDeltaLine("Refire", item.ReloadDelayTicks, stockItem.ReloadDelayTicks, true, positive, out var refire))
        {
            yield return refire;
        }

        if (TryBuildGameplayLoadoutMenuDeltaLine("Reload", item.AmmoReloadTicks, stockItem.AmmoReloadTicks, true, positive, out var reload))
        {
            yield return reload;
        }

        if (TryBuildGameplayLoadoutMenuDeltaLine("Spread", item.SpreadDegrees, stockItem.SpreadDegrees, true, positive, out var spread))
        {
            yield return spread;
        }

        var projectileSpeed = item.MinShotSpeed + item.AdditionalRandomShotSpeed;
        var stockProjectileSpeed = stockItem.MinShotSpeed + stockItem.AdditionalRandomShotSpeed;
        if (TryBuildGameplayLoadoutMenuDeltaLine("Projectile speed", projectileSpeed, stockProjectileSpeed, false, positive, out var projectileSpeedLine))
        {
            yield return projectileSpeedLine;
        }

        if (TryBuildGameplayLoadoutMenuEffectDeltaLine(
            "health on direct hit",
            item.DirectHitHealAmount,
            stockItem.DirectHitHealAmount,
            positive,
            out var directHitHealLine))
        {
            yield return directHitHealLine;
        }
    }

    private static IEnumerable<string> BuildGameplayLoadoutMenuDamageDeltaLines(PrimaryWeaponDefinition item, PrimaryWeaponDefinition stockItem, bool positive)
    {
        if (item.RocketCombat is { } rocket && stockItem.RocketCombat is { } stockRocket)
        {
            if (TryBuildGameplayLoadoutMenuDeltaLine("Direct damage", rocket.DirectHitDamage, stockRocket.DirectHitDamage, false, positive, out var direct))
            {
                yield return direct;
            }

            if (TryBuildGameplayLoadoutMenuDeltaLine("Splash damage", rocket.ExplosionDamage, stockRocket.ExplosionDamage, false, positive, out var splash))
            {
                yield return splash;
            }

            if (TryBuildGameplayLoadoutMenuDeltaLine("Blast radius", rocket.BlastRadius, stockRocket.BlastRadius, false, positive, out var radius))
            {
                yield return radius;
            }
            yield break;
        }

        if (item.DirectHitDamage is { } directDamage && stockItem.DirectHitDamage is { } stockDirectDamage)
        {
            if (TryBuildGameplayLoadoutMenuDeltaLine("Damage", directDamage, stockDirectDamage, false, positive, out var damage))
            {
                yield return damage;
            }
        }

        if (item.DamagePerTick is { } damagePerTick && stockItem.DamagePerTick is { } stockDamagePerTick)
        {
            if (TryBuildGameplayLoadoutMenuDeltaLine("Damage / tick", damagePerTick, stockDamagePerTick, false, positive, out var damageTick))
            {
                yield return damageTick;
            }
        }
    }

    private static IEnumerable<string> BuildGameplayLoadoutMenuNeutralLines(GameplayItemDefinition item, PrimaryWeaponDefinition itemStats, PrimaryWeaponDefinition stockItemStats)
    {
        if (itemStats.RefillsAllAtOnce != stockItemStats.RefillsAllAtOnce)
        {
            yield return itemStats.RefillsAllAtOnce ? "Reloads full clip at once" : "Shell-by-shell reload";
        }

        if (itemStats.AutoReloads != stockItemStats.AutoReloads)
        {
            yield return itemStats.AutoReloads ? "Auto reload enabled" : "Manual reload";
        }

        if (item.Description?.Notes is { Count: > 0 } notes)
        {
            foreach (var note in notes)
            {
                yield return note;
            }
        }
    }

    private static bool TryBuildGameplayLoadoutMenuDeltaLine(string label, int value, int stockValue, bool inverseIsBetter, bool positive, out string line)
    {
        return TryBuildGameplayLoadoutMenuDeltaLine(label, (float)value, stockValue, inverseIsBetter, positive, out line);
    }

    private static bool TryBuildGameplayLoadoutMenuDeltaLine(string label, float value, float stockValue, bool inverseIsBetter, bool positive, out string line)
    {
        line = string.Empty;
        if (stockValue <= 0f || NearlyEqual(value, stockValue))
        {
            return false;
        }

        var deltaPercent = ((value - stockValue) / stockValue) * 100f;
        var isPositive = inverseIsBetter ? deltaPercent < 0f : deltaPercent > 0f;
        if (isPositive != positive)
        {
            return false;
        }

        var sign = deltaPercent >= 0f ? "+" : "-";
        var deltaText = MathF.Abs(deltaPercent).ToString("0.#", CultureInfo.InvariantCulture);
        line = $"{sign}{deltaText}% {label}";
        return true;
    }

    private static bool TryBuildGameplayLoadoutMenuEffectDeltaLine(string label, float? value, float? stockValue, bool positive, out string line)
    {
        line = string.Empty;
        var resolvedValue = value ?? 0f;
        var resolvedStockValue = stockValue ?? 0f;
        if (NearlyEqual(resolvedValue, resolvedStockValue))
        {
            return false;
        }

        var isPositive = resolvedValue > resolvedStockValue;
        if (isPositive != positive)
        {
            return false;
        }

        var sign = isPositive ? "+" : "-";
        var magnitude = MathF.Abs(resolvedValue - resolvedStockValue);
        var magnitudeText = magnitude == MathF.Round(magnitude)
            ? ((int)MathF.Round(magnitude)).ToString(CultureInfo.InvariantCulture)
            : magnitude.ToString("0.#", CultureInfo.InvariantCulture);
        line = $"{sign}{magnitudeText} {label}";
        return true;
    }

    private static string BuildGameplayLoadoutMenuValueLine(string label, int value)
    {
        return BuildGameplayLoadoutMenuValueLine(label, (float)value, string.Empty);
    }

    private static string BuildGameplayLoadoutMenuValueLine(string label, float value, string suffix)
    {
        var valueText = value == MathF.Round(value)
            ? ((int)MathF.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);
        return $"{label}: {valueText}{suffix}";
    }

    private static bool NearlyEqual(float left, float right)
    {
        return MathF.Abs(left - right) < 0.005f;
    }

    private static int CompareGameplayLoadoutMenuSlotOptions(GameplayLoadoutMenuSlotOption left, GameplayLoadoutMenuSlotOption right)
    {
        var leftIsStock = IsGameplayLoadoutMenuStockOption(left);
        var rightIsStock = IsGameplayLoadoutMenuStockOption(right);
        if (leftIsStock != rightIsStock)
        {
            return leftIsStock ? -1 : 1;
        }

        return string.Compare(left.Item.DisplayName, right.Item.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGameplayLoadoutMenuStockOption(GameplayLoadoutMenuSlotOption option)
    {
        return option.Loadout.Loadout.Id.EndsWith(".stock", StringComparison.OrdinalIgnoreCase)
            || string.Equals(option.Loadout.Loadout.DisplayName, "Stock", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetGameplayLoadoutMenuRmClassName(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Scout => "RUNNER",
            PlayerClass.Pyro => "FIREBUG",
            PlayerClass.Soldier => "ROCKETMAN",
            PlayerClass.Heavy => "OVERWEIGHT",
            PlayerClass.Demoman => "DETONATOR",
            PlayerClass.Medic => "HEALER",
            PlayerClass.Engineer => "CONSTRUCTOR",
            PlayerClass.Spy => "INFILTRATOR",
            PlayerClass.Sniper => "RIFLEMAN",
            _ => CharacterClassCatalog.GetDefinition(playerClass).DisplayName.ToUpperInvariant(),
        };
    }

    private IEnumerable<string> BuildGameplayLoadoutMenuItemDetailLines(GameplayItemDefinition item)
    {
        if (item.Ammo.MaxAmmo > 0)
        {
            yield return $"Ammo: {item.Ammo.MaxAmmo.ToString(CultureInfo.InvariantCulture)}";
        }

        if (item.Ammo.ProjectilesPerUse > 0)
        {
            yield return $"Projectiles: {item.Ammo.ProjectilesPerUse.ToString(CultureInfo.InvariantCulture)}";
        }

        if (item.Ammo.UseDelaySourceTicks > 0)
        {
            var seconds = item.Ammo.UseDelaySourceTicks / (float)_config.TicksPerSecond;
            yield return $"Refire: {seconds.ToString("0.##", CultureInfo.InvariantCulture)} sec";
        }

        if (item.Ammo.ReloadSourceTicks > 0)
        {
            var seconds = item.Ammo.ReloadSourceTicks / (float)_config.TicksPerSecond;
            yield return $"Reload: {seconds.ToString("0.##", CultureInfo.InvariantCulture)} sec";
        }

        if (item.Ammo.MinProjectileSpeed > 0f || item.Ammo.AdditionalProjectileSpeed > 0f)
        {
            var speed = item.Ammo.MinProjectileSpeed + item.Ammo.AdditionalProjectileSpeed;
            yield return $"Projectile Speed: {speed.ToString("0.##", CultureInfo.InvariantCulture)}";
        }

        if (item.Combat?.Rocket is { } rocket)
        {
            yield return $"Direct Damage: {rocket.DirectHitDamage.ToString(CultureInfo.InvariantCulture)}";
            yield return $"Splash Damage: {rocket.ExplosionDamage.ToString("0.##", CultureInfo.InvariantCulture)}";
            yield return $"Blast Radius: {rocket.BlastRadius.ToString("0.##", CultureInfo.InvariantCulture)}";
        }

        if (item.Ammo.RefillsAllAtOnce)
        {
            yield return "Reloads the full clip at once";
        }
        else if (!item.Ammo.AutoReloads && item.Ammo.MaxAmmo > 0)
        {
            yield return "Manual reload";
        }

        if (item.Ownership?.DefaultGranted == false)
        {
            yield return "Tracked ownership required";
        }
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

    private readonly record struct GameplayLoadoutMenuEntry(
        GameplayClassLoadoutDefinition Loadout,
        bool IsAvailable,
        bool IsSelected);

    private readonly record struct GameplayLoadoutMenuSlotOption(
        GameplayEquipmentSlot Slot,
        GameplayItemDefinition Item,
        GameplayLoadoutMenuEntry Loadout,
        bool IsSelected);

    private readonly record struct GameplayLoadoutMenuLayout(
        Rectangle PanelBounds,
        Rectangle ClassStripBounds,
        Rectangle HeaderBounds,
        Rectangle LeftColumnBounds,
        Rectangle RightColumnBounds,
        Rectangle PreviewBounds,
        Rectangle DescriptionBounds,
        Rectangle FooterBounds,
        Rectangle BackButtonBounds,
        float Scale);

    private enum GameplayLoadoutMenuButtonKind
    {
        Class,
        ItemOption,
        Back,
    }

    private readonly record struct GameplayLoadoutMenuButton(
        Rectangle Bounds,
        Action Activate,
        GameplayLoadoutMenuButtonKind Kind,
        PlayerClass? ClassId = null,
        GameplayEquipmentSlot? Slot = null,
        string? ItemId = null);

    private readonly record struct GameplayLoadoutMenuBoardLine(
        string Text,
        Color Color);
}
