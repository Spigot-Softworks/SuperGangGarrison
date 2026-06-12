#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        BeginHudElementFrame();
        var browserHudDrawStartTimestamp = ShouldMeasureClientPerformanceDurations() ? Stopwatch.GetTimestamp() : 0L;
        if (IsLastToDieDeathFocusPresentationActive())
        {
            RecordBrowserHudDrawDuration(browserHudDrawStartTimestamp);
            return;
        }

        if (_gameplayHudHidden)
        {
            WriteGameplayRenderTrace("hud hidden");
            RecordBrowserHudDrawDuration(browserHudDrawStartTimestamp);
            return;
        }

        var deathCamActive = !_world.LocalPlayer.IsAlive && IsGameplayDeathCamActive();
        var localPlayerAlive = _world.LocalPlayer.IsAlive;
        WriteGameplayRenderTrace("hud begin");
        DrawDamageVignette();
        WriteGameplayRenderTrace("hud after damagevignette");
        DrawKillFeedHud();
        WriteGameplayRenderTrace("hud after killfeed");
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
        DrawVipPresentationOverlay();
        WriteGameplayRenderTrace("hud after vippresentation");
        DrawJumpHud();
        WriteGameplayRenderTrace("hud after jump");
        DrawLastToDieCombatFeedbackHud();
        WriteGameplayRenderTrace("hud after lasttodie-combat");
        if (!IsLocalSpectatorPresentationActive() && localPlayerAlive && !deathCamActive)
        {
            CollectGameplayHudElements();
            DrawGameplayHudElements(0, 10);
            WriteGameplayRenderTrace("hud after localhealth");
            _lastGameplayHudMouse = mouse;
            DrawGameplayHudElements(11, 11);
            WriteGameplayRenderTrace("hud after ltdbufficon");
            DrawExperimentalHealingHudIndicators();
            WriteGameplayRenderTrace("hud after experimentalhealing");
            DrawGameplayHudElements(12, 29);
            WriteGameplayRenderTrace("hud after ammo");
            var aimScreenPosition = GetEffectiveAimScreenPosition(mouse, cameraPosition);
            DrawSniperHud(aimScreenPosition);
            WriteGameplayRenderTrace("hud after sniper");
            DrawGameplayHudElements(30, 39);
            WriteGameplayRenderTrace("hud after medic");
            DrawHealerRadarHud(cameraPosition, mouse);
            WriteGameplayRenderTrace("hud after healerradar");
            DrawGameplayHudElements(40, int.MaxValue);
            WriteGameplayRenderTrace("hud after engineer");
        }

        if (!deathCamActive)
        {
            DrawPersistentSelfNameHud(cameraPosition);
            WriteGameplayRenderTrace("hud after persistentselfname");
            DrawHoveredPlayerNameHud(mouse, cameraPosition);
            WriteGameplayRenderTrace("hud after hoveredplayername");
            DrawSpectatorTrackedPlayerCrosshair(cameraPosition);
            WriteGameplayRenderTrace("hud after spectatortrackedcrosshair");
            if (IsLocalSpectatorPresentationActive())
            {
                DrawSpectatorBaselineHud(cameraPosition);
                WriteGameplayRenderTrace("hud after spectatorbaseline");
                var trackedPlayer = GetSpectatorFocusPlayer();
                if (trackedPlayer is not null && GetPlayerIsSniperScoped(trackedPlayer))
                {
                    var aimWorldPosition = GetRenderAimWorldPosition(trackedPlayer);
                    var screenAimPosition = new Vector2(aimWorldPosition.X - cameraPosition.X, aimWorldPosition.Y - cameraPosition.Y);
                    _gameplayAimHudController.DrawSpectatorSniperHud(trackedPlayer, screenAimPosition);
                }
            }
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
        DrawGarrisonBuilderEditorOverlay(mouse);
        WriteGameplayRenderTrace("hud after garrisonbuilder");
        DrawNavEditorOverlay(mouse, cameraPosition);
        WriteGameplayRenderTrace("hud after naveditor");
        // Draw binocular overlay before chat so chat renders on top
        if (!IsLocalSpectatorPresentationActive() && localPlayerAlive && !deathCamActive)
        {
            DrawBinocularOverlay();
            WriteGameplayRenderTrace("hud after binocular");
        }

        DrawChatHud();
        WriteGameplayRenderTrace("hud after chat");

        DrawPostGameMvpWinScreenHud();
        WriteGameplayRenderTrace("hud after postgamemvp");
        
        RecordBrowserHudDrawDuration(browserHudDrawStartTimestamp);
    }

    private void DrawSpectatorTrackedPlayerCrosshair(Vector2 cameraPosition)
    {
        if (!IsLocalSpectatorPresentationActive())
        {
            return;
        }

        var trackedPlayer = GetSpectatorFocusPlayer();
        if (trackedPlayer is null || !trackedPlayer.IsAlive)
        {
            return;
        }

        var crosshairSprite = GetResolvedSprite("SpectatorCrosshairS");
        if (crosshairSprite is null || crosshairSprite.Frames.Count < 2)
        {
            return;
        }

        var aimWorldPosition = GetRenderAimWorldPosition(trackedPlayer);
        var screenPosition = new Vector2(aimWorldPosition.X - cameraPosition.X, aimWorldPosition.Y - cameraPosition.Y);
        var frameIndex = trackedPlayer.Team == PlayerTeam.Blue ? 1 : 0;
        frameIndex = Math.Clamp(frameIndex, 0, crosshairSprite.Frames.Count - 1);
        DrawLoadedSpriteFrame(crosshairSprite.Frames[frameIndex], screenPosition, null, Color.White, 0f, crosshairSprite.Origin.ToVector2(), Vector2.One, SpriteEffects.None, 0f);
    }

    private void DrawGameplayModalOverlays(MouseState mouse, Vector2 cameraPosition)
    {
        var browserModalDrawStartTimestamp = ShouldMeasureClientPerformanceDurations() ? Stopwatch.GetTimestamp() : 0L;
        if (_gameplayHudHidden)
        {
            if (_consoleOpen)
            {
                DrawConsoleOverlay();
                WriteGameplayRenderTrace("modal after console");
            }

            RecordBrowserModalDrawDuration(browserModalDrawStartTimestamp);
            return;
        }

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
            var aimScreenPosition = GetEffectiveAimScreenPosition(mouse, cameraPosition);
            if (ShouldDrawControllerAimLine())
            {
                DrawControllerAimLine(cameraPosition, aimScreenPosition);
            }
            else
            {
                DrawCrosshair(aimScreenPosition);
            }
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
                if (_friendsMenuOpen)
                {
                    DrawFriendsMenu();
                    WriteGameplayRenderTrace("modal after ingamemenu-socialmenu");
                }
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
            case GameplayOverlayKind.CustomBubbleEditor:
                DrawCustomBubbleEditor();
                WriteGameplayRenderTrace("modal after custombubbleeditor");
                break;
            case GameplayOverlayKind.SocialMenu:
                DrawFriendsMenu();
                WriteGameplayRenderTrace("modal after socialmenu");
                break;
            case GameplayOverlayKind.HudEditor:
                DrawHudEditor();
                WriteGameplayRenderTrace("modal after hudeditor");
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
