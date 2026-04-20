#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static string _browserLastGameplayRenderTrace = "not-started";
    private static readonly Queue<string> _browserRecentGameplayRenderTraces = new();

    public static string GetBrowserLastGameplayRenderTrace()
    {
        return _browserLastGameplayRenderTrace;
    }

    public static string GetBrowserRecentGameplayRenderTraces()
    {
        return _browserRecentGameplayRenderTraces.Count == 0
            ? "none"
            : string.Join(" -> ", _browserRecentGameplayRenderTraces);
    }

    private const string GameplayRenderTraceFileName = "client-gameplay-render-trace.log";
    private static readonly bool GameplayRenderTraceEnabled = GetGameplayRenderTraceEnabled();

    private void DrawGameplayHudLayers(MouseState mouse, Vector2 cameraPosition)
    {
        var browserHudDrawStartTimestamp = OperatingSystem.IsBrowser() ? Stopwatch.GetTimestamp() : 0L;
        if (IsLastToDieDeathFocusPresentationActive())
        {
            RecordBrowserHudDrawDuration(browserHudDrawStartTimestamp);
            return;
        }

        var deathCamActive = !_world.LocalPlayer.IsAlive && IsGameplayDeathCamActive();
        var localPlayerAlive = _world.LocalPlayer.IsAlive;
        WriteGameplayRenderTrace("hud begin");
        DrawKillFeedHud();
        WriteGameplayRenderTrace("hud after killfeed");
        DrawChatHud();
        WriteGameplayRenderTrace("hud after chat");
        DrawClientPluginOverlayMenuHud();
        WriteGameplayRenderTrace("hud after clientpluginoverlaymenu");
        DrawScorePanelHud();
        WriteGameplayRenderTrace("hud after scorepanel");
        DrawAutoBalanceNotice();
        WriteGameplayRenderTrace("hud after autobalance");
        DrawRespawnHud();
        WriteGameplayRenderTrace("hud after respawn");
        DrawDeathCamHud();
        WriteGameplayRenderTrace("hud after deathcam");
        DrawWinBannerHud();
        WriteGameplayRenderTrace("hud after winbanner");
        DrawLastToDieHud();
        WriteGameplayRenderTrace("hud after lasttodie");
        DrawLastToDieCombatFeedbackHud();
        WriteGameplayRenderTrace("hud after lasttodie-combat");
        if (!_networkClient.IsSpectator && localPlayerAlive && !deathCamActive)
        {
            DrawLocalHealthHud();
            WriteGameplayRenderTrace("hud after localhealth");
            DrawExperimentalHealingHudIndicators();
            WriteGameplayRenderTrace("hud after experimentalhealing");
            DrawAmmoHud();
            WriteGameplayRenderTrace("hud after ammo");
            DrawSniperHud(mouse);
            WriteGameplayRenderTrace("hud after sniper");
            DrawMedicHud();
            WriteGameplayRenderTrace("hud after medic");
            DrawMedicAssistHud();
            WriteGameplayRenderTrace("hud after medicassist");
            DrawHealerRadarHud(cameraPosition, mouse);
            WriteGameplayRenderTrace("hud after healerradar");
            DrawEngineerHud();
            WriteGameplayRenderTrace("hud after engineer");
        }

        if (!deathCamActive)
        {
            DrawPersistentSelfNameHud(cameraPosition);
            WriteGameplayRenderTrace("hud after persistentselfname");
            DrawHoveredPlayerNameHud(mouse, cameraPosition);
            WriteGameplayRenderTrace("hud after hoveredplayername");
            DrawDroppedWeaponInteractionHud(cameraPosition);
            WriteGameplayRenderTrace("hud after droppedweaponhud");
        }

        if (CanDrawGameplayBuildHud())
        {
            DrawBuildMenuHud();
            WriteGameplayRenderTrace("hud after buildmenu");
        }

        DrawNoticeHud();
        WriteGameplayRenderTrace("hud after notice");
        DrawScoreboardHud();
        WriteGameplayRenderTrace("hud after scoreboard");
        if (CanDrawGameplayBubbleHud())
        {
            DrawBubbleMenuHud();
            WriteGameplayRenderTrace("hud after bubblemenu");
        }

        DrawClientPluginHud(cameraPosition);
        WriteGameplayRenderTrace("hud after clientpluginhud");
        DrawNavEditorOverlay(mouse, cameraPosition);
        WriteGameplayRenderTrace("hud after naveditor");
        RecordBrowserHudDrawDuration(browserHudDrawStartTimestamp);
    }

    private void DrawGameplayModalOverlays(MouseState mouse)
    {
        var browserModalDrawStartTimestamp = OperatingSystem.IsBrowser() ? Stopwatch.GetTimestamp() : 0L;
        if (IsLastToDieDeathFocusPresentationActive())
        {
            if (IsLastToDieFailureOverlayActive())
            {
                DrawLastToDieFailureOverlay();
                if (ShouldDrawSoftwareMenuCursor())
                {
                    DrawSoftwareMenuCursor(mouse);
                }
            }

            RecordBrowserModalDrawDuration(browserModalDrawStartTimestamp);
            return;
        }

        if (_passwordPromptOpen)
        {
            DrawPasswordPrompt();
            WriteGameplayRenderTrace("modal after passwordprompt");
        }

        if (_teamSelectOpen || _teamSelectAlpha > 0.02f)
        {
            DrawTeamSelectHud();
            WriteGameplayRenderTrace("modal after teamselect");
        }

        if (_classSelectOpen || _classSelectAlpha > 0.02f)
        {
            DrawClassSelectHud();
            WriteGameplayRenderTrace("modal after classselect");
        }
        else if (CanDrawGameplayCrosshair())
        {
            DrawCrosshair(mouse);
            WriteGameplayRenderTrace("modal after crosshair");
        }

        if (_consoleOpen)
        {
            DrawConsoleOverlay();
            WriteGameplayRenderTrace("modal after console");
        }

        DrawNetworkDiagnosticsOverlay();
        WriteGameplayRenderTrace("modal after networkdiagnostics");
        DrawBotDiagnosticsOverlay();
        WriteGameplayRenderTrace("modal after botdiagnostics");

        switch (GetActiveGameplayOverlay())
        {
            case GameplayOverlayKind.InGameMenu:
                DrawInGameMenu();
                WriteGameplayRenderTrace("modal after ingamemenu");
                break;
            case GameplayOverlayKind.DebugMenu:
                DrawDebugMenu();
                WriteGameplayRenderTrace("modal after debugmenu");
                break;
            case GameplayOverlayKind.LoadoutMenu:
                DrawGameplayLoadoutMenu();
                WriteGameplayRenderTrace("modal after loadoutmenu");
                break;
            case GameplayOverlayKind.ClientPowers:
                DrawClientPowersMenu();
                WriteGameplayRenderTrace("modal after clientpowers");
                break;
            case GameplayOverlayKind.LastToDieStageClear:
                DrawLastToDieStageClearOverlay();
                WriteGameplayRenderTrace("modal after stageclear");
                break;
            case GameplayOverlayKind.LastToDieSurvivorMenu:
                DrawLastToDieSurvivorMenu();
                WriteGameplayRenderTrace("modal after survivormenu");
                break;
            case GameplayOverlayKind.LastToDiePerkMenu:
                DrawLastToDiePerkMenu();
                WriteGameplayRenderTrace("modal after perkmenu");
                break;
            case GameplayOverlayKind.PracticeSetup:
                DrawPracticeSetupMenu();
                WriteGameplayRenderTrace("modal after practicesetup");
                break;
            case GameplayOverlayKind.PluginOptionsMenu:
                DrawPluginOptionsMenu();
                WriteGameplayRenderTrace("modal after pluginoptions");
                break;
            case GameplayOverlayKind.OptionsMenu:
                DrawOptionsMenu();
                WriteGameplayRenderTrace("modal after options");
                break;
            case GameplayOverlayKind.ControlsMenu:
                DrawControlsMenu();
                WriteGameplayRenderTrace("modal after controls");
                break;
        }

        DrawQuitPrompt();
        WriteGameplayRenderTrace("modal after quitprompt");
        DrawLastToDieFailureOverlay();
        WriteGameplayRenderTrace("modal after failureoverlay");

        if (ShouldDrawSoftwareMenuCursor())
        {
            DrawSoftwareMenuCursor(mouse);
            WriteGameplayRenderTrace("modal after softwarecursor");
        }

        RecordBrowserModalDrawDuration(browserModalDrawStartTimestamp);
    }

    private static void WriteGameplayRenderTrace(string message)
    {
        if (OperatingSystem.IsBrowser())
        {
            _browserLastGameplayRenderTrace = message;
            _browserRecentGameplayRenderTraces.Enqueue(message);
            while (_browserRecentGameplayRenderTraces.Count > 16)
            {
                _browserRecentGameplayRenderTraces.Dequeue();
            }
        }

        if (!GameplayRenderTraceEnabled)
        {
            return;
        }

        try
        {
            var timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            File.AppendAllText(RuntimePaths.GetLogPath(GameplayRenderTraceFileName), $"[{timestamp}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private static bool GetGameplayRenderTraceEnabled()
    {
        if (OperatingSystem.IsBrowser())
        {
            return false;
        }

        if (AppContext.TryGetSwitch("OpenGarrison.EnableGameplayRenderTrace", out var enabledBySwitch))
        {
            return enabledBySwitch;
        }

        var envValue = Environment.GetEnvironmentVariable("OPENGARRISON_TRACE_GAMEPLAY_RENDER");
        return string.Equals(envValue, "1", StringComparison.Ordinal)
            || string.Equals(envValue, "true", StringComparison.OrdinalIgnoreCase);
    }
}
