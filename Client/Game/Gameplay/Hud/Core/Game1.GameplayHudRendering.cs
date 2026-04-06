#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Globalization;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string GameplayRenderTraceFileName = "client-gameplay-render-trace.log";

    private void DrawGameplayHudLayers(MouseState mouse, Vector2 cameraPosition)
    {
        if (IsLastToDieDeathFocusPresentationActive())
        {
            return;
        }

        var deathCamActive = !_world.LocalPlayer.IsAlive && IsGameplayDeathCamActive();
        var localPlayerAlive = _world.LocalPlayer.IsAlive;
        WriteGameplayRenderTrace("hud begin");
        DrawKillFeedHud();
        WriteGameplayRenderTrace("hud after killfeed");
        DrawChatHud();
        WriteGameplayRenderTrace("hud after chat");
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
    }

    private void DrawGameplayModalOverlays(MouseState mouse)
    {
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
    }

    private void WriteGameplayRenderTrace(string message)
    {
        try
        {
            var timestamp = DateTimeOffset.Now.ToString("O", CultureInfo.InvariantCulture);
            File.AppendAllText(RuntimePaths.GetLogPath(GameplayRenderTraceFileName), $"[{timestamp}] {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }
}
