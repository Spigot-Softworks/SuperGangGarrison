#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;
using System;

namespace OpenGarrison.Client;

public partial class Game1
{
    private enum ClientPowerViability
    {
        High,
        Medium,
        Risky,
    }

    private readonly record struct ClientPowerInvestigationEntry(
        string Label,
        ClientPowerViability Viability,
        string HookSummary);

    private readonly record struct ClientPowersLayout(
        Rectangle Panel,
        Rectangle SoldierShotgunToggleBounds,
        Rectangle ListBounds,
        Rectangle BackBounds,
        int VisibleRowCount,
        bool CompactLayout);

    private static readonly ClientPowerInvestigationEntry[] ClientPowerEntries =
    [
        new("Soldier shotgun secondary (Space)", ClientPowerViability.High, "implemented via practice offhand seam"),
        new("Drop weapon on death", ClientPowerViability.Risky, "needs dropped item + pickup system"),
        new("Spinjump gravity multiplier", ClientPowerViability.High, "clean hook in player movement gravity"),
        new("Spinjump damage multiplier", ClientPowerViability.Risky, "current sim has no clear spinjump damage seam"),
        new("Heal on damage", ClientPowerViability.High, "damage pipeline already centralized"),
        new("Heal on kill", ClientPowerViability.High, "clean hook in kill resolution"),
        new("Speed on damage", ClientPowerViability.Medium, "needs temporary movement buff state"),
        new("Speed on kill", ClientPowerViability.Medium, "needs temporary movement buff state"),
        new("Reload on direct hit", ClientPowerViability.Medium, "multiple hit paths need one refill rule"),
        new("Reload on kill", ClientPowerViability.High, "kill hook can grant ammo/refill"),
        new("Extra jump on kill", ClientPowerViability.Medium, "needs temp air-jump grant beyond class max"),
        new("Distance damage multiplier", ClientPowerViability.Medium, "needs per-weapon travel/distance pass"),
        new("Rate of fire on damage", ClientPowerViability.Medium, "needs generic cooldown multiplier buff"),
        new("Rate of fire on kill", ClientPowerViability.Medium, "needs generic cooldown multiplier buff"),
        new("Passive health regeneration", ClientPowerViability.High, "clean per-tick hook in player state"),
        new("Invincibility on kill (1s)", ClientPowerViability.High, "can reuse uber-style immunity gate"),
        new("Damage resistance on kill", ClientPowerViability.Medium, "needs incoming damage scalar buff"),
        new("Self explosive resist on kill", ClientPowerViability.Medium, "explosion ownership is already known"),
        new("Damage multiplier on airshot", ClientPowerViability.Medium, "needs airborne hit check across weapons"),
        new("+10% projectile size", ClientPowerViability.Risky, "projectile radii are mostly static constants"),
        new("+10% projectile speed", ClientPowerViability.Medium, "localized to projectile spawn velocities"),
        new("+10% explosive damage radius", ClientPowerViability.Medium, "needs dynamic blast radius plumbing"),
        new("+10% explosive knockback power", ClientPowerViability.Medium, "localized to explosion impulse math"),
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
            TogglePracticeExperimentalSoldierShotgun();
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

        if (layout.SoldierShotgunToggleBounds.Contains(mouse.Position))
        {
            TogglePracticeExperimentalSoldierShotgun();
        }
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
            "Practice-only staging surface. The list below scores each requested feature against the current legacy simulation and keeps the UI seam ready for future hook-up work.",
            compactLayout ? 74 : 88);
        var summaryY = panel.Y + 56f;
        for (var index = 0; index < summaryLines.Length; index += 1)
        {
            DrawBitmapFontText(summaryLines[index], new Vector2(panel.X + padding, summaryY + (index * 18f)), new Color(210, 210, 210), 0.88f);
        }

        DrawBitmapFontText("Practice Toggle", new Vector2(panel.X + padding, layout.SoldierShotgunToggleBounds.Y + 6f), new Color(232, 232, 232), 0.92f);
        DrawMenuButtonScaled(
            layout.SoldierShotgunToggleBounds,
            _practiceExperimentalSoldierShotgunEnabled ? "Soldier Shotgun: Enabled" : "Soldier Shotgun: Disabled",
            false,
            compactLayout ? 0.9f : 1f);
        DrawBitmapFontText(
            "Soldier fires Engineer's shotgun with Space in practice. Mouse right stays unused for this experiment.",
            new Vector2(panel.X + padding, layout.SoldierShotgunToggleBounds.Bottom + 10f),
            new Color(210, 210, 210),
            0.84f);

        _spriteBatch.Draw(_pixel, listBounds, new Color(21, 23, 28, 232));
        _spriteBatch.Draw(_pixel, new Rectangle(listBounds.X, listBounds.Y, listBounds.Width, 2), new Color(102, 108, 118));
        _spriteBatch.Draw(_pixel, new Rectangle(listBounds.X, listBounds.Bottom - 2, listBounds.Width, 2), new Color(12, 14, 18));

        DrawBitmapFontText("Feature", new Vector2(labelX, listBounds.Y + 8f), new Color(230, 230, 230), 0.92f);
        DrawBitmapFontText("Status", new Vector2(statusX, listBounds.Y + 8f), new Color(230, 230, 230), 0.92f);
        DrawBitmapFontText("Primary Hook", new Vector2(noteX, listBounds.Y + 8f), new Color(230, 230, 230), 0.92f);

        var endIndex = Math.Min(ClientPowerEntries.Length, _clientPowersScrollOffset + layout.VisibleRowCount);
        var rowY = listBounds.Y + headerHeight + 6;
        for (var index = _clientPowersScrollOffset; index < endIndex; index += 1)
        {
            var rowBounds = new Rectangle(listBounds.X + 6, rowY - 3, listBounds.Width - 22, rowHeight - 2);
            var alternate = ((index - _clientPowersScrollOffset) & 1) == 0;
            _spriteBatch.Draw(_pixel, rowBounds, alternate ? new Color(34, 37, 43, 150) : new Color(27, 29, 35, 150));

            var entry = ClientPowerEntries[index];
            var viabilityLabel = GetClientPowerViabilityLabel(entry.Viability);
            var viabilityColor = GetClientPowerViabilityColor(entry.Viability);
            DrawBitmapFontText(TrimBitmapMenuText(entry.Label, labelColumnWidth, 0.9f), new Vector2(labelX, rowY + 2f), Color.White, 0.9f);
            DrawBitmapFontText(TrimBitmapMenuText(viabilityLabel, statusColumnWidth, 0.9f), new Vector2(statusX, rowY + 2f), viabilityColor, 0.9f);
            DrawBitmapFontText(TrimBitmapMenuText(entry.HookSummary, noteColumnWidth, 0.86f), new Vector2(noteX, rowY + 3f), new Color(205, 205, 205), 0.86f);
            rowY += rowHeight;
        }

        DrawClientPowersScrollBar(listBounds, headerHeight, rowHeight, layout.VisibleRowCount);

        var footerText = $"Showing {_clientPowersScrollOffset + 1}-{Math.Max(_clientPowersScrollOffset + 1, endIndex)} of {ClientPowerEntries.Length}. Use mouse wheel or Up/Down to review.";
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
        var toggleWidth = compactLayout ? 290 : 330;
        var toggleBounds = new Rectangle(
            panel.Right - padding - toggleWidth,
            panel.Y + (compactLayout ? 102 : 114),
            toggleWidth,
            buttonHeight);
        var listTop = toggleBounds.Bottom + (compactLayout ? 34 : 42);
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
            toggleBounds,
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

    private static string GetClientPowerViabilityLabel(ClientPowerViability viability)
    {
        return viability switch
        {
            ClientPowerViability.High => "High",
            ClientPowerViability.Medium => "Medium",
            _ => "Risky",
        };
    }

    private static Color GetClientPowerViabilityColor(ClientPowerViability viability)
    {
        return viability switch
        {
            ClientPowerViability.High => new Color(118, 222, 160),
            ClientPowerViability.Medium => new Color(236, 198, 102),
            _ => new Color(242, 126, 126),
        };
    }

    private ExperimentalGameplaySettings GetPracticeExperimentalGameplaySettings()
    {
        return new ExperimentalGameplaySettings(
            EnableSoldierShotgunSecondaryWeapon: _practiceExperimentalSoldierShotgunEnabled);
    }

    private void TogglePracticeExperimentalSoldierShotgun()
    {
        _practiceExperimentalSoldierShotgunEnabled = !_practiceExperimentalSoldierShotgunEnabled;
        ApplyPracticeExperimentalGameplaySettings();
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
