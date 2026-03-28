#nullable enable

using Microsoft.Xna.Framework;
using OpenGarrison.BotAI;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const double BotDiagnosticSummaryIntervalSeconds = 1d;
    private const int BotDiagnosticHistoryLimit = 120;
    private const int BotDiagnosticOverlayMaxEntries = 8;
    private bool _botDiagnosticsEnabled;
    private readonly List<string> _botDiagnosticSummaryHistory = new();
    private readonly List<string> _botDiagnosticOverlayLines = new();
    private double _botDiagnosticSummaryElapsedSeconds;
    private int _botDiagnosticSummaryFrames;
    private double _botDiagnosticUpdateTotalMilliseconds;
    private double _botDiagnosticUpdateMaxMilliseconds;
    private int _botDiagnosticUpdateHitches2;
    private int _botDiagnosticUpdateHitches4;
    private int _botDiagnosticTotalBots;
    private int _botDiagnosticAliveBots;
    private int _botDiagnosticVisibleEnemyBots;
    private int _botDiagnosticHealFocusBots;
    private int _botDiagnosticCabinetSeekBots;
    private int _botDiagnosticUnstickBots;
    private int _botDiagnosticObservedMaxBots;
    private BotControllerDiagnosticsSnapshot _botDiagnosticLatestSnapshot = BotControllerDiagnosticsSnapshot.Empty;
    private string _botDiagnosticLastConsoleSummary = "botdiag disabled";

    private void BeginBotDiagnosticsFrame(GameTime gameTime)
    {
        if (!_botDiagnosticsEnabled || !IsPracticeSessionActive)
        {
            return;
        }

        _botDiagnosticSummaryElapsedSeconds += Math.Max(0d, gameTime.ElapsedGameTime.TotalSeconds);
        _botDiagnosticSummaryFrames += 1;
    }

    private void RecordPracticeBotDiagnosticsUpdate(double elapsedMilliseconds, BotControllerDiagnosticsSnapshot snapshot)
    {
        if (!_botDiagnosticsEnabled || !IsPracticeSessionActive)
        {
            return;
        }

        _botDiagnosticLatestSnapshot = snapshot;
        _botDiagnosticUpdateTotalMilliseconds += Math.Max(0d, elapsedMilliseconds);
        _botDiagnosticUpdateMaxMilliseconds = Math.Max(_botDiagnosticUpdateMaxMilliseconds, elapsedMilliseconds);
        if (elapsedMilliseconds >= 2d)
        {
            _botDiagnosticUpdateHitches2 += 1;
        }

        if (elapsedMilliseconds >= 4d)
        {
            _botDiagnosticUpdateHitches4 += 1;
        }

        _botDiagnosticTotalBots += snapshot.ControlledBotCount;
        _botDiagnosticAliveBots += snapshot.AliveBotCount;
        _botDiagnosticVisibleEnemyBots += snapshot.VisibleEnemyCount;
        _botDiagnosticHealFocusBots += snapshot.HealFocusCount;
        _botDiagnosticCabinetSeekBots += snapshot.CabinetSeekCount;
        _botDiagnosticUnstickBots += snapshot.UnstickCount;
        _botDiagnosticObservedMaxBots = Math.Max(_botDiagnosticObservedMaxBots, snapshot.ControlledBotCount);
    }

    private void FinalizeBotDiagnosticsFrame()
    {
        if (!_botDiagnosticsEnabled || !IsPracticeSessionActive)
        {
            return;
        }

        if (_botDiagnosticSummaryElapsedSeconds < BotDiagnosticSummaryIntervalSeconds)
        {
            return;
        }

        PublishBotDiagnosticSummary();
    }

    private void EnableBotDiagnostics()
    {
        _botDiagnosticsEnabled = true;
        _practiceBotController.CollectDiagnostics = true;
        _botDiagnosticLatestSnapshot = BotControllerDiagnosticsSnapshot.Empty;
        _botDiagnosticSummaryHistory.Clear();
        _botDiagnosticOverlayLines.Clear();
        ResetBotDiagnosticSample();
        AddConsoleLine("bot diagnostics enabled");
    }

    private void DisableBotDiagnostics()
    {
        _botDiagnosticsEnabled = false;
        _practiceBotController.CollectDiagnostics = false;
        _botDiagnosticLatestSnapshot = BotControllerDiagnosticsSnapshot.Empty;
        _botDiagnosticOverlayLines.Clear();
        AddConsoleLine("bot diagnostics disabled");
    }

    private void ClearBotDiagnosticsHistory()
    {
        _botDiagnosticLatestSnapshot = BotControllerDiagnosticsSnapshot.Empty;
        _botDiagnosticSummaryHistory.Clear();
        _botDiagnosticOverlayLines.Clear();
        _botDiagnosticLastConsoleSummary = "botdiag history cleared";
        ResetBotDiagnosticSample();
        AddConsoleLine("bot diagnostics history cleared");
    }

    private void PrintBotDiagnosticsStatus()
    {
        AddConsoleLine(_botDiagnosticsEnabled ? "bot diagnostics: enabled" : "bot diagnostics: disabled");
        AddConsoleLine(_botDiagnosticLastConsoleSummary);
        AddConsoleLine(GetPracticeNavigationDiagnosticsSummary());
    }

    private void DrawBotDiagnosticsOverlay()
    {
        if (!_botDiagnosticsEnabled)
        {
            return;
        }

        BuildBotDiagnosticOverlayLines();
        if (_botDiagnosticOverlayLines.Count == 0)
        {
            return;
        }

        var width = 720;
        var lineHeight = 18;
        var padding = 10;
        var x = 18;
        var y = _consoleOpen ? 210 : 18;
        var height = (_botDiagnosticOverlayLines.Count * lineHeight) + (padding * 2);
        var rectangle = new Rectangle(x, y, width, height);
        _spriteBatch.Draw(_pixel, rectangle, new Color(18, 20, 24, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, 2), new Color(255, 186, 92));

        var position = new Vector2(rectangle.X + padding, rectangle.Y + padding);
        for (var index = 0; index < _botDiagnosticOverlayLines.Count; index += 1)
        {
            _spriteBatch.DrawString(_consoleFont, _botDiagnosticOverlayLines[index], position, new Color(236, 238, 242));
            position.Y += lineHeight;
        }
    }

    private void BuildBotDiagnosticOverlayLines()
    {
        _botDiagnosticOverlayLines.Clear();
        if (!IsPracticeSessionActive)
        {
            _botDiagnosticOverlayLines.Add("BOT DIAG enabled");
            _botDiagnosticOverlayLines.Add("practice session inactive");
            return;
        }

        var latest = _botDiagnosticLatestSnapshot;
        _botDiagnosticOverlayLines.Add(
            $"BOT DIAG bots={latest.ControlledBotCount} alive={latest.AliveBotCount} vis={latest.VisibleEnemyCount} heal={latest.HealFocusCount} cabinet={latest.CabinetSeekCount} unstick={latest.UnstickCount}");

        if (_botDiagnosticSummaryFrames == 0 && _botDiagnosticSummaryHistory.Count == 0)
        {
            _botDiagnosticOverlayLines.Add("summary waiting for first 1s sample...");
        }
        else
        {
            _botDiagnosticOverlayLines.Add(_botDiagnosticLastConsoleSummary);
        }

        _botDiagnosticOverlayLines.Add(GetPracticeNavigationDiagnosticsSummary());

        if (latest.ControlledBotCount == 0)
        {
            _botDiagnosticOverlayLines.Add("no practice bots active");
            return;
        }

        var shownEntries = Math.Min(latest.Entries.Count, BotDiagnosticOverlayMaxEntries);
        for (var index = 0; index < shownEntries; index += 1)
        {
            var entry = latest.Entries[index];
            _botDiagnosticOverlayLines.Add(FormatBotDiagnosticEntry(entry));
        }

        if (latest.Entries.Count > shownEntries)
        {
            _botDiagnosticOverlayLines.Add($"... {latest.Entries.Count - shownEntries} more bots");
        }
    }

    private void PublishBotDiagnosticSummary()
    {
        var intervalSeconds = Math.Max(_botDiagnosticSummaryElapsedSeconds, 0.0001d);
        var averageUpdateMilliseconds = _botDiagnosticSummaryFrames == 0
            ? 0d
            : _botDiagnosticUpdateTotalMilliseconds / _botDiagnosticSummaryFrames;
        var averageBots = _botDiagnosticSummaryFrames == 0
            ? 0d
            : _botDiagnosticTotalBots / (double)_botDiagnosticSummaryFrames;
        var averageAliveBots = _botDiagnosticSummaryFrames == 0
            ? 0d
            : _botDiagnosticAliveBots / (double)_botDiagnosticSummaryFrames;
        var averageVisibleEnemies = _botDiagnosticSummaryFrames == 0
            ? 0d
            : _botDiagnosticVisibleEnemyBots / (double)_botDiagnosticSummaryFrames;
        var averageHealFocusBots = _botDiagnosticSummaryFrames == 0
            ? 0d
            : _botDiagnosticHealFocusBots / (double)_botDiagnosticSummaryFrames;
        var averageCabinetSeekers = _botDiagnosticSummaryFrames == 0
            ? 0d
            : _botDiagnosticCabinetSeekBots / (double)_botDiagnosticSummaryFrames;
        var averageUnstickBots = _botDiagnosticSummaryFrames == 0
            ? 0d
            : _botDiagnosticUnstickBots / (double)_botDiagnosticSummaryFrames;

        _botDiagnosticLastConsoleSummary = string.Create(
            CultureInfo.InvariantCulture,
            $"botdiag cost={averageUpdateMilliseconds:F3}/{_botDiagnosticUpdateMaxMilliseconds:F3}ms avgBots={averageBots:F1}/{averageAliveBots:F1}alive maxBots={_botDiagnosticObservedMaxBots} avgVis={averageVisibleEnemies:F1} avgHeal={averageHealFocusBots:F1} avgCab={averageCabinetSeekers:F1} avgUnstick={averageUnstickBots:F1} hitch2={_botDiagnosticUpdateHitches2} hitch4={_botDiagnosticUpdateHitches4}");
        _botDiagnosticSummaryHistory.Add($"[{DateTime.Now:HH:mm:ss}] {_botDiagnosticLastConsoleSummary}");
        while (_botDiagnosticSummaryHistory.Count > BotDiagnosticHistoryLimit)
        {
            _botDiagnosticSummaryHistory.RemoveAt(0);
        }

        ResetBotDiagnosticSample();
    }

    private void ResetBotDiagnosticSample()
    {
        _botDiagnosticSummaryElapsedSeconds = 0d;
        _botDiagnosticSummaryFrames = 0;
        _botDiagnosticUpdateTotalMilliseconds = 0d;
        _botDiagnosticUpdateMaxMilliseconds = 0d;
        _botDiagnosticUpdateHitches2 = 0;
        _botDiagnosticUpdateHitches4 = 0;
        _botDiagnosticTotalBots = 0;
        _botDiagnosticAliveBots = 0;
        _botDiagnosticVisibleEnemyBots = 0;
        _botDiagnosticHealFocusBots = 0;
        _botDiagnosticCabinetSeekBots = 0;
        _botDiagnosticUnstickBots = 0;
        _botDiagnosticObservedMaxBots = 0;
    }

    private static string FormatBotDiagnosticEntry(BotControllerDiagnosticsEntry entry)
    {
        var focusLabel = TruncateBotDiagnosticLabel(entry.FocusLabel, 16);
        var routeLabel = TruncateBotDiagnosticLabel(entry.RouteLabel, 10);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"[{entry.Slot:00}] {GetBotTeamLabel(entry.Team)} {entry.ClassId,-7} {GetBotRoleLabel(entry.Role),-7} {GetBotStateLabel(entry.State),-8} {GetBotFocusLabel(entry.FocusKind),-4}={focusLabel,-16} nav={routeLabel,-10} hp={entry.Health,3}/{entry.MaxHealth,-3} vis={(entry.HasVisibleEnemy ? "Y" : "N")} stuck={entry.StuckTicks,2}");
    }

    private static string GetBotTeamLabel(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? "BLU" : "RED";
    }

    private static string GetBotRoleLabel(BotRole role)
    {
        return role switch
        {
            BotRole.DefendObjective => "defend",
            BotRole.EscortCarrier => "escort",
            BotRole.HuntCarrier => "hunt",
            BotRole.ReturnWithIntel => "return",
            BotRole.ContestArena => "arena",
            _ => "attack",
        };
    }

    private static string GetBotStateLabel(BotStateKind state)
    {
        return state switch
        {
            BotStateKind.Respawning => "respawn",
            BotStateKind.SeekHealingCabinet => "cabinet",
            BotStateKind.HealAlly => "heal",
            BotStateKind.CombatAdvance => "advance",
            BotStateKind.CombatRetreat => "retreat",
            BotStateKind.CombatStrafe => "combat",
            BotStateKind.Patrol => "patrol",
            BotStateKind.Unstick => "unstick",
            _ => "travel",
        };
    }

    private static string GetBotFocusLabel(BotFocusKind focusKind)
    {
        return focusKind switch
        {
            BotFocusKind.Enemy => "tgt",
            BotFocusKind.HealTarget => "ally",
            BotFocusKind.HealingCabinet => "goal",
            _ => "goal",
        };
    }

    private static string TruncateBotDiagnosticLabel(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..Math.Max(0, maxLength - 1)] + "~";
    }
}
