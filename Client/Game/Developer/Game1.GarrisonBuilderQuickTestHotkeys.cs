#nullable enable

using Microsoft.Xna.Framework.Input;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Builder quick test shortcut: Left Ctrl fills the match with bots (5 on RED, 6 on BLU);
/// pressed again once that is already the case, it restarts the quick test match.
/// </summary>
public partial class Game1
{
    private const int GarrisonBuilderQuickTestRedBots = 5;
    private const int GarrisonBuilderQuickTestBlueBots = 6;

    /// <summary>The exported map the current quick test runs on (for restarting it).</summary>
    private string _garrisonBuilderQuickTestLevelName = string.Empty;

    /// <summary>Returns true when the key press was used (the rest of this frame's screen input is skipped).</summary>
    private bool UpdateGarrisonBuilderQuickTestHotkeys(KeyboardState keyboard)
    {
        if (!_garrisonBuilderQuickTestActive
            || !IsPracticeSessionActive
            || _chatOpen
            || _consoleOpen
            || _inGameMenuOpen
            || !IsKeyPressed(keyboard, Keys.LeftControl))
        {
            return false;
        }

        // Practice counts bots as friendly / enemy to the local player's team.
        var localTeam = _world.LocalPlayerTeam == PlayerTeam.Blue ? PlayerTeam.Blue : PlayerTeam.Red;
        var friendly = localTeam == PlayerTeam.Red ? GarrisonBuilderQuickTestRedBots : GarrisonBuilderQuickTestBlueBots;
        var enemy = localTeam == PlayerTeam.Red ? GarrisonBuilderQuickTestBlueBots : GarrisonBuilderQuickTestRedBots;
        if (_practiceFriendlyBotCount != friendly || _practiceEnemyBotCount != enemy)
        {
            _practiceFriendlyBotCount = friendly;
            _practiceEnemyBotCount = enemy;
            SyncPracticeBotRoster(localTeam);
            var message = $"Bots: {GarrisonBuilderQuickTestRedBots} RED, {GarrisonBuilderQuickTestBlueBots} BLU. Left Ctrl again restarts.";
            ShowNoticeText(message, 200, playSound: false);
            AddConsoleLine("builder quick test: " + message);
            return true;
        }

        RestartGarrisonBuilderQuickTest();
        return true;
    }

    /// <summary>Starts the quick test match over on the same exported map, with the player back on as before.</summary>
    private void RestartGarrisonBuilderQuickTest()
    {
        var levelName = string.IsNullOrWhiteSpace(_garrisonBuilderQuickTestLevelName)
            ? _world.Level.Name
            : _garrisonBuilderQuickTestLevelName;
        if (!TryBeginGarrisonBuilderQuickTestSession(levelName))
        {
            AddConsoleLine($"builder quick test: could not restart {levelName}");
            ShowNoticeText("Could not restart the quick test.", 200, playSound: false);
            return;
        }

        _garrisonBuilderQuickTestActive = true;
        PrepareGarrisonBuilderQuickTestPlayer();
        LogPracticeMapBotSpawnDiagnostics("quick test restarted");
        AddConsoleLine("builder quick test: restarted");
        ShowNoticeText("Quick test restarted.", 150, playSound: false);
    }
}
