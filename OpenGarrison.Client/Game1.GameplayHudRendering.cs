#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void DrawGameplayHudLayers(MouseState mouse, Vector2 cameraPosition)
    {
        var deathCamActive = _killCamEnabled && !_world.LocalPlayer.IsAlive && _world.LocalDeathCam is not null;
        var localPlayerAlive = _world.LocalPlayer.IsAlive;
        DrawKillFeedHud();
        DrawChatHud();
        DrawScorePanelHud();
        DrawAutoBalanceNotice();
        DrawRespawnHud();
        DrawDeathCamHud();
        DrawWinBannerHud();
        if (!_networkClient.IsSpectator && localPlayerAlive && !deathCamActive)
        {
            DrawLocalHealthHud();
            DrawAmmoHud();
            DrawSniperHud(mouse);
            DrawMedicHud();
            DrawMedicAssistHud();
            DrawHealerRadarHud(cameraPosition, mouse);
            DrawEngineerHud();
        }

        if (!deathCamActive)
        {
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
    }

    private void DrawGameplayModalOverlays(MouseState mouse)
    {
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
            && !_consoleOpen
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

        if (ShouldDrawSoftwareMenuCursor())
        {
            DrawSoftwareMenuCursor(mouse);
        }
    }
}
