#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawGameplayHudLayers(MouseState mouse, Vector2 cameraPosition)
    {
        if (IsLastToDieDeathFocusPresentationActive())
        {
            return;
        }

        var deathCamActive = _killCamEnabled && !_world.LocalPlayer.IsAlive && _world.LocalDeathCam is not null;
        var localPlayerAlive = _world.LocalPlayer.IsAlive;
        DrawKillFeedHud();
        DrawChatHud();
        DrawScorePanelHud();
        DrawAutoBalanceNotice();
        DrawRespawnHud();
        DrawDeathCamHud();
        DrawWinBannerHud();
        DrawLastToDieHud();
        DrawLastToDieCombatFeedbackHud();
        if (!_networkClient.IsSpectator && localPlayerAlive && !deathCamActive)
        {
            DrawLocalHealthHud();
            DrawExperimentalHealingHudIndicators();
            DrawAmmoHud();
            DrawSniperHud(mouse);
            DrawMedicHud();
            DrawMedicAssistHud();
            DrawHealerRadarHud(cameraPosition, mouse);
            DrawEngineerHud();
        }

        if (!deathCamActive)
        {
            DrawPersistentSelfNameHud(cameraPosition);
            DrawHoveredPlayerNameHud(mouse, cameraPosition);
        }

        if (!deathCamActive)
        {
            DrawBuildMenuHud();
        }

        DrawNoticeHud();
        DrawScoreboardHud();
        if (!_networkClient.IsSpectator && localPlayerAlive && !deathCamActive)
        {
            DrawBubbleMenuHud();
        }

        DrawClientPluginHud(cameraPosition);
        DrawNavEditorOverlay(mouse, cameraPosition);
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
        }

        if (_teamSelectOpen || _teamSelectAlpha > 0.02f)
        {
            DrawTeamSelectHud();
        }

        if (_classSelectOpen || _classSelectAlpha > 0.02f)
        {
            DrawClassSelectHud();
        }
        else if (!_teamSelectOpen
            && _teamSelectAlpha <= 0.02f
            && !_networkClient.IsSpectator
            && _world.LocalPlayer.IsAlive
            && (!_killCamEnabled || _world.LocalDeathCam is null)
            && !ShouldBlockGameplayForNavEditor()
            && !_consoleOpen
            && !_clientPowersOpen
            && !_lastToDiePerkMenuOpen
            && !IsLastToDieDeathFocusPresentationActive()
            && !IsLastToDieFailureOverlayActive()
            && !_practiceSetupOpen
            && !_inGameMenuOpen
            && !_pluginOptionsMenuOpen
            && !_optionsMenuOpen
            && !_controlsMenuOpen)
        {
            DrawCrosshair(mouse);
        }

        if (_consoleOpen)
        {
            DrawConsoleOverlay();
        }

        DrawNetworkDiagnosticsOverlay();
        DrawBotDiagnosticsOverlay();

        if (_inGameMenuOpen)
        {
            DrawInGameMenu();
        }
        else if (_clientPowersOpen)
        {
            DrawClientPowersMenu();
        }
        else if (IsLastToDieStageClearOverlayActive())
        {
            DrawLastToDieStageClearOverlay();
        }
        else if (_lastToDiePerkMenuOpen)
        {
            DrawLastToDiePerkMenu();
        }
        else if (_practiceSetupOpen)
        {
            DrawPracticeSetupMenu();
        }
        else if (_pluginOptionsMenuOpen)
        {
            DrawPluginOptionsMenu();
        }
        else if (_optionsMenuOpen)
        {
            DrawOptionsMenu();
        }
        else if (_controlsMenuOpen)
        {
            DrawControlsMenu();
        }

        DrawQuitPrompt();
        DrawLastToDieFailureOverlay();

        if (ShouldDrawSoftwareMenuCursor())
        {
            DrawSoftwareMenuCursor(mouse);
        }
    }
}
