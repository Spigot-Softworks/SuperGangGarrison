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
        else if (CanDrawGameplayCrosshair())
        {
            DrawCrosshair(mouse);
        }

        if (_consoleOpen)
        {
            DrawConsoleOverlay();
        }

        DrawNetworkDiagnosticsOverlay();
        DrawBotDiagnosticsOverlay();

        switch (GetActiveGameplayOverlay())
        {
            case GameplayOverlayKind.InGameMenu:
                DrawInGameMenu();
                break;
            case GameplayOverlayKind.ClientPowers:
                DrawClientPowersMenu();
                break;
            case GameplayOverlayKind.LastToDieStageClear:
                DrawLastToDieStageClearOverlay();
                break;
            case GameplayOverlayKind.LastToDieSurvivorMenu:
                DrawLastToDieSurvivorMenu();
                break;
            case GameplayOverlayKind.LastToDiePerkMenu:
                DrawLastToDiePerkMenu();
                break;
            case GameplayOverlayKind.PracticeSetup:
                DrawPracticeSetupMenu();
                break;
            case GameplayOverlayKind.PluginOptionsMenu:
                DrawPluginOptionsMenu();
                break;
            case GameplayOverlayKind.OptionsMenu:
                DrawOptionsMenu();
                break;
            case GameplayOverlayKind.ControlsMenu:
                DrawControlsMenu();
                break;
        }

        DrawQuitPrompt();
        DrawLastToDieFailureOverlay();

        if (ShouldDrawSoftwareMenuCursor())
        {
            DrawSoftwareMenuCursor(mouse);
        }
    }
}
