#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum ClientPowerToggleKind
    {
        SoldierShotgunSecondaryWeapon,
        StickyGibBlood,
        HealOnDamage,
        HealOnKill,
        RateOfFireOnDamage,
        SoldierInstantReload,
        SpeedOnDamage,
        SpeedOnKill,
        PassiveHealthRegeneration,
        InvincibilityOnKill,
        ProjectileSpeedMultiplier,
        AirshotDamageMultiplier,
        EnemyHealthPackDrops,
        EnemyDroppedWeapons,
    }

    private readonly record struct ClientPowerToggleEntry(
        ClientPowerToggleKind Kind,
        string Label,
        string Description);

    private readonly record struct ClientPowersLayout(
        Rectangle Panel,
        Rectangle ListBounds,
        Rectangle BackBounds,
        int VisibleRowCount,
        bool CompactLayout);

    private static readonly ClientPowerToggleEntry[] ClientPowerEntries =
    [
        new(ClientPowerToggleKind.SoldierShotgunSecondaryWeapon, "Soldier shotgun", "Press Space to fire secondary shotgun in practice."),
        new(ClientPowerToggleKind.StickyGibBlood, "Sticky gib blood", "Gib blood coats nearby players for 10s, then fades out."),
        new(ClientPowerToggleKind.HealOnDamage, "Heal on damage", "Restore 35% of dealt damage."),
        new(ClientPowerToggleKind.HealOnKill, "Heal on kill", "Restore 25 health on kill."),
        new(ClientPowerToggleKind.RateOfFireOnDamage, "Rate of fire on hit", "Landing a hit instantly requeues primary fire if it is still cooling down."),
        new(ClientPowerToggleKind.SoldierInstantReload, "Soldier instant reload on hit", "Landing a hit instantly refills 1 Soldier rocket."),
        new(ClientPowerToggleKind.SpeedOnDamage, "Speed on damage", "20% movement boost for 2.5s after dealing damage."),
        new(ClientPowerToggleKind.SpeedOnKill, "Speed on kill", "20% movement boost for 3s after kills."),
        new(ClientPowerToggleKind.PassiveHealthRegeneration, "Passive regeneration", "Restore 3 health per second while alive."),
        new(ClientPowerToggleKind.InvincibilityOnKill, "Invincibility on kill", "Gain 0.5s of superburst invulnerability on kills."),
        new(ClientPowerToggleKind.ProjectileSpeedMultiplier, "Projectile speed +20%", "Boost spawned projectile launch speed by 20%."),
        new(ClientPowerToggleKind.AirshotDamageMultiplier, "Airshot damage +25%", "Direct projectile airshots deal bonus damage and show yellow hit numbers."),
        new(ClientPowerToggleKind.EnemyHealthPackDrops, "Enemies drop health packs", "10% chance for enemy kills to drop a temporary small or large health pack."),
        new(ClientPowerToggleKind.EnemyDroppedWeapons, "Enemies drop weapons", "50% chance for enemy bot kills to drop a temporary primary weapon for Soldier to pick up with Q."),
    ];

    private void OpenClientPowersMenu(bool fromGameplay)
    {
        if (!IsPracticeSessionActive && !_practiceSetupOpen)
        {
            return;
        }

        _clientPowersOpen = true;
        _clientPowersOpenedFromGameplay = fromGameplay;
        _clientPowersScrollOffset = 0;
        if (fromGameplay)
        {
            CloseInGameMenu();
        }
    }

    private void CloseClientPowersMenu(bool reopenPreviousMenu = true)
    {
        var reopenInGameMenu = reopenPreviousMenu
            && _clientPowersOpenedFromGameplay
            && !_mainMenuOpen;
        _clientPowersOpen = false;
        _clientPowersOpenedFromGameplay = false;
        _clientPowersScrollOffset = 0;

        if (reopenInGameMenu)
        {
            OpenInGameMenu();
        }
    }

    private void UpdateClientPowersMenu(KeyboardState keyboard, MouseState mouse)
    {
        var layout = GetClientPowersLayout();
        ClampClientPowersScrollOffset(layout.VisibleRowCount);

        if (IsKeyPressed(keyboard, Keys.Escape))
        {
            CloseClientPowersMenu();
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            TogglePracticeClientPower(ClientPowerToggleKind.SoldierShotgunSecondaryWeapon);
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Up))
        {
            AdjustClientPowersScrollOffset(-1, layout.VisibleRowCount);
        }
        else if (IsKeyPressed(keyboard, Keys.Down))
        {
            AdjustClientPowersScrollOffset(1, layout.VisibleRowCount);
        }
        else if (IsKeyPressed(keyboard, Keys.PageUp))
        {
            AdjustClientPowersScrollOffset(-layout.VisibleRowCount, layout.VisibleRowCount);
        }
        else if (IsKeyPressed(keyboard, Keys.PageDown))
        {
            AdjustClientPowersScrollOffset(layout.VisibleRowCount, layout.VisibleRowCount);
        }
        else if (IsKeyPressed(keyboard, Keys.Home))
        {
            _clientPowersScrollOffset = 0;
        }
        else if (IsKeyPressed(keyboard, Keys.End))
        {
            _clientPowersScrollOffset = GetMaxClientPowersScrollOffset(layout.VisibleRowCount);
        }

        var wheelDelta = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta != 0)
        {
            var stepCount = Math.Max(1, Math.Abs(wheelDelta) / 120);
            AdjustClientPowersScrollOffset(wheelDelta > 0 ? -stepCount : stepCount, layout.VisibleRowCount);
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed)
        {
            return;
        }

        if (layout.BackBounds.Contains(mouse.Position))
        {
            CloseClientPowersMenu();
            return;
        }

        if (!layout.ListBounds.Contains(mouse.Position))
        {
            return;
        }

        const int headerHeight = 30;
        const int rowHeight = 30;
        var clickedRow = (mouse.Y - (layout.ListBounds.Y + headerHeight + 6)) / rowHeight;
        if (clickedRow < 0 || clickedRow >= layout.VisibleRowCount)
        {
            return;
        }

        var entryIndex = _clientPowersScrollOffset + clickedRow;
        if (entryIndex < 0 || entryIndex >= ClientPowerEntries.Length)
        {
            return;
        }

        TogglePracticeClientPower(ClientPowerEntries[entryIndex].Kind);
    }

    private void DrawClientPowersMenu()
    {
        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.82f);

        var layout = GetClientPowersLayout();
        ClampClientPowersScrollOffset(layout.VisibleRowCount);

        var panel = layout.Panel;
        var compactLayout = layout.CompactLayout;
        var padding = compactLayout ? 20f : 28f;
        var listBounds = layout.ListBounds;
        const int headerHeight = 30;
        const int rowHeight = 30;
        var columnWidth = listBounds.Width - 44f;
        var labelColumnWidth = MathF.Max(180f, columnWidth * 0.42f);
        var statusColumnWidth = MathF.Max(82f, columnWidth * 0.14f);
        var noteColumnWidth = MathF.Max(80f, columnWidth - labelColumnWidth - statusColumnWidth);
        var labelX = listBounds.X + 12f;
        var statusX = labelX + labelColumnWidth + 18f;
        var noteX = statusX + statusColumnWidth + 14f;

        _spriteBatch.Draw(_pixel, panel, new Color(31, 33, 38, 242));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 3, panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Client Powers", new Vector2(panel.X + padding, panel.Y + 22f), Color.White, compactLayout ? 1.08f : 1.2f);

        var summaryLines = WrapMenuParagraph(
            "Practice-only experimental toggles. These rows drive clean local-practice rules or client-side presentation hooks without bleeding into online flow.",
            compactLayout ? 74 : 88);
        var summaryY = panel.Y + 56f;
        for (var index = 0; index < summaryLines.Length; index += 1)
        {
            DrawBitmapFontText(summaryLines[index], new Vector2(panel.X + padding, summaryY + (index * 18f)), new Color(210, 210, 210), 0.88f);
        }

        _spriteBatch.Draw(_pixel, listBounds, new Color(21, 23, 28, 232));
        _spriteBatch.Draw(_pixel, new Rectangle(listBounds.X, listBounds.Y, listBounds.Width, 2), new Color(102, 108, 118));
        _spriteBatch.Draw(_pixel, new Rectangle(listBounds.X, listBounds.Bottom - 2, listBounds.Width, 2), new Color(12, 14, 18));

        DrawBitmapFontText("Setting", new Vector2(labelX, listBounds.Y + 8f), new Color(230, 230, 230), 0.92f);
        DrawBitmapFontText("Status", new Vector2(statusX, listBounds.Y + 8f), new Color(230, 230, 230), 0.92f);
        DrawBitmapFontText("Effect", new Vector2(noteX, listBounds.Y + 8f), new Color(230, 230, 230), 0.92f);

        var endIndex = Math.Min(ClientPowerEntries.Length, _clientPowersScrollOffset + layout.VisibleRowCount);
        var rowY = listBounds.Y + headerHeight + 6;
        for (var index = _clientPowersScrollOffset; index < endIndex; index += 1)
        {
            var rowBounds = new Rectangle(listBounds.X + 6, rowY - 3, listBounds.Width - 22, rowHeight - 2);
            var alternate = ((index - _clientPowersScrollOffset) & 1) == 0;
            _spriteBatch.Draw(_pixel, rowBounds, alternate ? new Color(34, 37, 43, 150) : new Color(27, 29, 35, 150));

            var entry = ClientPowerEntries[index];
            var enabled = GetPracticeClientPowerEnabled(entry.Kind);
            var stateLabel = enabled ? "Enabled" : "Disabled";
            var stateColor = enabled ? new Color(118, 222, 160) : new Color(172, 172, 172);
            DrawBitmapFontText(TrimBitmapMenuText(entry.Label, labelColumnWidth, 0.9f), new Vector2(labelX, rowY + 2f), Color.White, 0.9f);
            DrawBitmapFontText(TrimBitmapMenuText(stateLabel, statusColumnWidth, 0.9f), new Vector2(statusX, rowY + 2f), stateColor, 0.9f);
            DrawBitmapFontText(TrimBitmapMenuText(entry.Description, noteColumnWidth, 0.86f), new Vector2(noteX, rowY + 3f), new Color(205, 205, 205), 0.86f);
            rowY += rowHeight;
        }

        DrawClientPowersScrollBar(listBounds, headerHeight, rowHeight, layout.VisibleRowCount);

        var footerText = $"Showing {_clientPowersScrollOffset + 1}-{Math.Max(_clientPowersScrollOffset + 1, endIndex)} of {ClientPowerEntries.Length}. Click a row to toggle it.";
        DrawBitmapFontText(footerText, new Vector2(panel.X + padding, panel.Bottom - (compactLayout ? 62f : 68f)), new Color(210, 210, 210), 0.88f);
        DrawMenuButtonScaled(layout.BackBounds, _clientPowersOpenedFromGameplay ? "Back to Pause Menu" : "Back", false, 1f);
    }

    private ClientPowersLayout GetClientPowersLayout()
    {
        var panelWidth = Math.Min(ViewportWidth - 32, 980);
        var panelHeight = Math.Min(ViewportHeight - 24, ViewportHeight < 720 ? 600 : 660);
        var panel = new Rectangle(
            (ViewportWidth - panelWidth) / 2,
            (ViewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);

        var compactLayout = panel.Width < 860 || panel.Height < 620;
        var padding = compactLayout ? 20 : 28;
        var buttonHeight = compactLayout ? 36 : 42;
        var listTop = panel.Y + (compactLayout ? 126 : 138);
        var listBottomMargin = compactLayout ? 86 : 94;
        var listBounds = new Rectangle(
            panel.X + padding,
            listTop,
            panel.Width - (padding * 2),
            panel.Height - (listTop - panel.Y) - listBottomMargin);
        var backWidth = compactLayout ? 190 : 220;
        var backBounds = new Rectangle(
            panel.Right - padding - backWidth,
            panel.Bottom - padding - buttonHeight,
            backWidth,
            buttonHeight);
        var visibleRowCount = Math.Max(1, (listBounds.Height - 36) / 30);

        return new ClientPowersLayout(
            panel,
            listBounds,
            backBounds,
            visibleRowCount,
            compactLayout);
    }

    private void AdjustClientPowersScrollOffset(int deltaRows, int visibleRowCount)
    {
        _clientPowersScrollOffset = Math.Clamp(
            _clientPowersScrollOffset + deltaRows,
            0,
            GetMaxClientPowersScrollOffset(visibleRowCount));
    }

    private void ClampClientPowersScrollOffset(int visibleRowCount)
    {
        _clientPowersScrollOffset = Math.Clamp(
            _clientPowersScrollOffset,
            0,
            GetMaxClientPowersScrollOffset(visibleRowCount));
    }

    private static int GetMaxClientPowersScrollOffset(int visibleRowCount)
    {
        return Math.Max(0, ClientPowerEntries.Length - Math.Max(1, visibleRowCount));
    }

    private void DrawClientPowersScrollBar(Rectangle listBounds, int headerHeight, int rowHeight, int visibleRowCount)
    {
        if (ClientPowerEntries.Length <= visibleRowCount)
        {
            return;
        }

        var trackBounds = new Rectangle(listBounds.Right - 10, listBounds.Y + headerHeight + 6, 4, (visibleRowCount * rowHeight) - 4);
        _spriteBatch.Draw(_pixel, trackBounds, new Color(62, 66, 74));

        var maxOffset = GetMaxClientPowersScrollOffset(visibleRowCount);
        var thumbHeight = Math.Max(18, (int)MathF.Round(trackBounds.Height * (visibleRowCount / (float)ClientPowerEntries.Length)));
        var thumbTravel = Math.Max(0, trackBounds.Height - thumbHeight);
        var thumbY = trackBounds.Y + (maxOffset == 0
            ? 0
            : (int)MathF.Round((_clientPowersScrollOffset / (float)maxOffset) * thumbTravel));
        var thumbBounds = new Rectangle(trackBounds.X, thumbY, trackBounds.Width, thumbHeight);
        _spriteBatch.Draw(_pixel, thumbBounds, new Color(188, 190, 196));
    }

    private bool GetPracticeClientPowerEnabled(ClientPowerToggleKind kind)
    {
        return kind switch
        {
            ClientPowerToggleKind.SoldierShotgunSecondaryWeapon => _practiceExperimentalGameplaySettings.EnableSoldierShotgunSecondaryWeapon,
            ClientPowerToggleKind.StickyGibBlood => _practiceStickyGibBloodEnabled,
            ClientPowerToggleKind.HealOnDamage => _practiceExperimentalGameplaySettings.EnableHealOnDamage,
            ClientPowerToggleKind.HealOnKill => _practiceExperimentalGameplaySettings.EnableHealOnKill,
            ClientPowerToggleKind.RateOfFireOnDamage => _practiceExperimentalGameplaySettings.EnableRateOfFireMultiplierOnDamage,
            ClientPowerToggleKind.SoldierInstantReload => _practiceExperimentalGameplaySettings.EnableSoldierInstantReload,
            ClientPowerToggleKind.SpeedOnDamage => _practiceExperimentalGameplaySettings.EnableSpeedOnDamage,
            ClientPowerToggleKind.SpeedOnKill => _practiceExperimentalGameplaySettings.EnableSpeedOnKill,
            ClientPowerToggleKind.PassiveHealthRegeneration => _practiceExperimentalGameplaySettings.EnablePassiveHealthRegeneration,
            ClientPowerToggleKind.InvincibilityOnKill => _practiceExperimentalGameplaySettings.EnableInvincibilityOnKill,
            ClientPowerToggleKind.ProjectileSpeedMultiplier => _practiceExperimentalGameplaySettings.EnableProjectileSpeedMultiplier,
            ClientPowerToggleKind.AirshotDamageMultiplier => _practiceExperimentalGameplaySettings.EnableAirshotDamageMultiplier,
            ClientPowerToggleKind.EnemyHealthPackDrops => _practiceExperimentalGameplaySettings.EnableEnemyHealthPackDrops,
            ClientPowerToggleKind.EnemyDroppedWeapons => _practiceExperimentalGameplaySettings.EnableEnemyDroppedWeapons,
            _ => false,
        };
    }

    private void TogglePracticeClientPower(ClientPowerToggleKind kind)
    {
        switch (kind)
        {
            case ClientPowerToggleKind.SoldierShotgunSecondaryWeapon:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableSoldierShotgunSecondaryWeapon = !_practiceExperimentalGameplaySettings.EnableSoldierShotgunSecondaryWeapon,
                };
                break;
            case ClientPowerToggleKind.StickyGibBlood:
                _practiceStickyGibBloodEnabled = !_practiceStickyGibBloodEnabled;
                break;
            case ClientPowerToggleKind.HealOnDamage:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableHealOnDamage = !_practiceExperimentalGameplaySettings.EnableHealOnDamage,
                };
                break;
            case ClientPowerToggleKind.HealOnKill:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableHealOnKill = !_practiceExperimentalGameplaySettings.EnableHealOnKill,
                };
                break;
            case ClientPowerToggleKind.RateOfFireOnDamage:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableRateOfFireMultiplierOnDamage = !_practiceExperimentalGameplaySettings.EnableRateOfFireMultiplierOnDamage,
                };
                break;
            case ClientPowerToggleKind.SoldierInstantReload:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableSoldierInstantReload = !_practiceExperimentalGameplaySettings.EnableSoldierInstantReload,
                };
                break;
            case ClientPowerToggleKind.SpeedOnDamage:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableSpeedOnDamage = !_practiceExperimentalGameplaySettings.EnableSpeedOnDamage,
                };
                break;
            case ClientPowerToggleKind.SpeedOnKill:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableSpeedOnKill = !_practiceExperimentalGameplaySettings.EnableSpeedOnKill,
                };
                break;
            case ClientPowerToggleKind.PassiveHealthRegeneration:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnablePassiveHealthRegeneration = !_practiceExperimentalGameplaySettings.EnablePassiveHealthRegeneration,
                };
                break;
            case ClientPowerToggleKind.InvincibilityOnKill:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableInvincibilityOnKill = !_practiceExperimentalGameplaySettings.EnableInvincibilityOnKill,
                };
                break;
            case ClientPowerToggleKind.ProjectileSpeedMultiplier:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableProjectileSpeedMultiplier = !_practiceExperimentalGameplaySettings.EnableProjectileSpeedMultiplier,
                };
                break;
            case ClientPowerToggleKind.AirshotDamageMultiplier:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableAirshotDamageMultiplier = !_practiceExperimentalGameplaySettings.EnableAirshotDamageMultiplier,
                };
                break;
            case ClientPowerToggleKind.EnemyHealthPackDrops:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableEnemyHealthPackDrops = !_practiceExperimentalGameplaySettings.EnableEnemyHealthPackDrops,
                };
                break;
            case ClientPowerToggleKind.EnemyDroppedWeapons:
                _practiceExperimentalGameplaySettings = _practiceExperimentalGameplaySettings with
                {
                    EnableEnemyDroppedWeapons = !_practiceExperimentalGameplaySettings.EnableEnemyDroppedWeapons,
                };
                break;
        }

        ApplyPracticeExperimentalGameplaySettings();
    }

    private ExperimentalGameplaySettings GetPracticeExperimentalGameplaySettings()
    {
        return _practiceExperimentalGameplaySettings;
    }

    private void ApplyPracticeExperimentalGameplaySettings()
    {
        if (!IsPracticeSessionActive)
        {
            return;
        }

        _world.ConfigureExperimentalGameplaySettings(GetPracticeExperimentalGameplaySettings());
    }
}
