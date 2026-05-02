#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    public sealed record BrowserAutomationRect(int X, int Y, int Width, int Height)
    {
        public static BrowserAutomationRect FromRectangle(Rectangle rectangle)
        {
            return new BrowserAutomationRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }
    }

    public sealed record BrowserAutomationAction(string Label, BrowserAutomationRect Bounds, bool Enabled = true);

    public sealed record BrowserAutomationSnapshot(
        string Shell,
        bool StartupSplashOpen,
        bool MainMenuOpen,
        string MainMenuPage,
        string MainMenuOverlay,
        string GameplayOverlay,
        bool PracticeSetupOpen,
        bool TeamSelectOpen,
        bool ClassSelectOpen,
        bool AwaitingJoin,
        bool PracticeSessionActive,
        bool CanEnterGameplaySession,
        string GameplaySessionEntryReason,
        bool MenuBootstrapComplete,
        bool ContentBootstrapComplete,
        bool BootstrapInitialized,
        bool BootstrapContentLoaded,
        int BootstrapInitializeCalls,
        int BootstrapLoadContentCalls,
        int BrowserHostLifecycleEnsureCalls,
        string DeferredContentBootstrapStage,
        bool BrowserBootstrapAssetsApplied,
        int StartupSplashTicks,
        bool BrowserInputFocused,
        string[] BrowserPressedKeys,
        bool AudioAvailable,
        bool LocalPlayerAlive,
        float LocalPlayerX,
        float LocalPlayerY,
        int LooseSheetVisualCount,
        string StatusMessage,
        string SelectedPracticeMap,
        int PracticeTickRate,
        int PracticeEnemyBotCount,
        int PracticeFriendlyBotCount,
        BrowserAutomationAction[] MenuButtons,
        BrowserAutomationAction[] PracticeButtons,
        BrowserAutomationAction[] TeamSelectButtons,
        BrowserAutomationAction[] ClassSelectButtons)
    {
        public static BrowserAutomationSnapshot Empty { get; } = new(
            Shell: "Unknown",
            StartupSplashOpen: false,
            MainMenuOpen: false,
            MainMenuPage: string.Empty,
            MainMenuOverlay: string.Empty,
            GameplayOverlay: string.Empty,
            PracticeSetupOpen: false,
            TeamSelectOpen: false,
            ClassSelectOpen: false,
            AwaitingJoin: false,
            PracticeSessionActive: false,
            CanEnterGameplaySession: false,
            GameplaySessionEntryReason: string.Empty,
            MenuBootstrapComplete: false,
            ContentBootstrapComplete: false,
            BootstrapInitialized: false,
            BootstrapContentLoaded: false,
            BootstrapInitializeCalls: 0,
            BootstrapLoadContentCalls: 0,
            BrowserHostLifecycleEnsureCalls: 0,
            DeferredContentBootstrapStage: string.Empty,
            BrowserBootstrapAssetsApplied: false,
            StartupSplashTicks: 0,
            BrowserInputFocused: false,
            BrowserPressedKeys: [],
            AudioAvailable: false,
            LocalPlayerAlive: false,
            LocalPlayerX: 0f,
            LocalPlayerY: 0f,
            LooseSheetVisualCount: 0,
            StatusMessage: string.Empty,
            SelectedPracticeMap: string.Empty,
            PracticeTickRate: 0,
            PracticeEnemyBotCount: 0,
            PracticeFriendlyBotCount: 0,
            MenuButtons: [],
            PracticeButtons: [],
            TeamSelectButtons: [],
            ClassSelectButtons: []);
    }

    public BrowserAutomationSnapshot GetBrowserAutomationSnapshot()
    {
        var canEnterGameplaySession = _bootstrapController.CanEnterGameplaySession(out var gameplaySessionEntryReason);
        return new BrowserAutomationSnapshot(
            Shell: GetBrowserAutomationShell(),
            StartupSplashOpen: _startupSplashOpen,
            MainMenuOpen: _mainMenuOpen,
            MainMenuPage: _mainMenuPage.ToString(),
            MainMenuOverlay: GetActiveMainMenuOverlay().ToString(),
            GameplayOverlay: GetActiveGameplayOverlay().ToString(),
            PracticeSetupOpen: _practiceSetupOpen,
            TeamSelectOpen: _teamSelectOpen,
            ClassSelectOpen: _classSelectOpen,
            AwaitingJoin: _world.LocalPlayerAwaitingJoin,
            PracticeSessionActive: IsPracticeSessionActive,
            CanEnterGameplaySession: canEnterGameplaySession,
            GameplaySessionEntryReason: gameplaySessionEntryReason ?? string.Empty,
            MenuBootstrapComplete: _bootstrapController.IsMenuBootstrapComplete,
            ContentBootstrapComplete: _bootstrapController.IsContentBootstrapComplete,
            BootstrapInitialized: _bootstrapController.IsInitialized,
            BootstrapContentLoaded: _bootstrapController.IsContentLoaded,
            BootstrapInitializeCalls: _bootstrapController.InitializeCallCount,
            BootstrapLoadContentCalls: _bootstrapController.LoadContentCallCount,
            BrowserHostLifecycleEnsureCalls: _browserHostLifecycleEnsureCallCount,
            DeferredContentBootstrapStage: _bootstrapController.DeferredContentBootstrapStageName,
            BrowserBootstrapAssetsApplied: _browserBootstrapAssetsApplied,
            StartupSplashTicks: _startupSplashTicks,
            BrowserInputFocused: BrowserInputBridge.IsFocused,
            BrowserPressedKeys: BrowserInputBridge.GetPressedKeyNamesSnapshot(),
            AudioAvailable: _audioAvailable,
            LocalPlayerAlive: _world.LocalPlayer.IsAlive,
            LocalPlayerX: _world.LocalPlayer.X,
            LocalPlayerY: _world.LocalPlayer.Y,
            LooseSheetVisualCount: _looseSheetVisuals.Count,
            StatusMessage: _menuStatusMessage ?? string.Empty,
            SelectedPracticeMap: GetSelectedPracticeMapEntry()?.LevelName ?? string.Empty,
            PracticeTickRate: _practiceTickRate,
            PracticeEnemyBotCount: _practiceEnemyBotCount,
            PracticeFriendlyBotCount: _practiceFriendlyBotCount,
            MenuButtons: GetBrowserMainMenuAutomationActions(),
            PracticeButtons: GetBrowserPracticeAutomationActions(),
            TeamSelectButtons: GetBrowserTeamSelectAutomationActions(),
            ClassSelectButtons: GetBrowserClassSelectAutomationActions());
    }

    private string GetBrowserAutomationShell()
    {
        if (_startupSplashOpen)
        {
            return "StartupSplash";
        }

        if (_mainMenuOpen)
        {
            return "MainMenu";
        }

        return "Gameplay";
    }

    private BrowserAutomationAction[] GetBrowserMainMenuAutomationActions()
    {
        if (!_mainMenuOpen || GetActiveMainMenuOverlay() != MainMenuOverlayKind.None)
        {
            return [];
        }

        return BuildMainMenuButtons()
            .Select(button => new BrowserAutomationAction(button.Label, BrowserAutomationRect.FromRectangle(button.Bounds)))
            .ToArray();
    }

    private BrowserAutomationAction[] GetBrowserPracticeAutomationActions()
    {
        if (!_practiceSetupOpen)
        {
            return [];
        }

        var layout = GetPracticeSetupLayout();
        var canEnterGameplaySession = _bootstrapController.CanEnterGameplaySession(out _);
        return
        [
            new BrowserAutomationAction("Enemy Bots -", BrowserAutomationRect.FromRectangle(layout.EnemyBotsLeftBounds)),
            new BrowserAutomationAction("Enemy Bots +", BrowserAutomationRect.FromRectangle(layout.EnemyBotsRightBounds)),
            new BrowserAutomationAction("Friendly Bots -", BrowserAutomationRect.FromRectangle(layout.FriendlyBotsLeftBounds)),
            new BrowserAutomationAction("Friendly Bots +", BrowserAutomationRect.FromRectangle(layout.FriendlyBotsRightBounds)),
            new BrowserAutomationAction("Special Abilities", BrowserAutomationRect.FromRectangle(layout.SpecialAbilitiesBounds)),
            new BrowserAutomationAction("Start Practice", BrowserAutomationRect.FromRectangle(layout.StartBounds), canEnterGameplaySession),
            new BrowserAutomationAction("Experimental", BrowserAutomationRect.FromRectangle(layout.ClientPowersBounds)),
            new BrowserAutomationAction("Back", BrowserAutomationRect.FromRectangle(layout.BackBounds)),
        ];
    }

    private BrowserAutomationAction[] GetBrowserTeamSelectAutomationActions()
    {
        if (!_teamSelectOpen)
        {
            return [];
        }

        var panelLeft = (ViewportWidth / 2f) - 400f;
        return
        [
            new BrowserAutomationAction("Auto Select", BrowserAutomationRect.FromRectangle(CreateBrowserTeamSelectButtonBounds(panelLeft, 40f, 127f, 48f, 223f))),
            new BrowserAutomationAction("Spectate", BrowserAutomationRect.FromRectangle(CreateBrowserTeamSelectButtonBounds(panelLeft, 156f, 193f, 118f, 151f))),
            new BrowserAutomationAction("RED", BrowserAutomationRect.FromRectangle(CreateBrowserTeamSelectButtonBounds(panelLeft, 228f, 315f, 48f, 223f))),
            new BrowserAutomationAction("BLU", BrowserAutomationRect.FromRectangle(CreateBrowserTeamSelectButtonBounds(panelLeft, 340f, 427f, 48f, 223f))),
        ];
    }

    private BrowserAutomationAction[] GetBrowserClassSelectAutomationActions()
    {
        if (!_classSelectOpen)
        {
            return [];
        }

        var panelLeft = (ViewportWidth / 2f) - 400f;
        int[] leftEdges = [24, 64, 104, 156, 196, 236, 288, 328, 368, 420];
        string[] labels = ["Scout", "Pyro", "Soldier", "Heavy", "Demoman", "Medic", "Engineer", "Spy", "Sniper", "Random"];
        return labels
            .Select((label, index) => new BrowserAutomationAction(
                label,
                BrowserAutomationRect.FromRectangle(new Rectangle(
                    (int)MathF.Round(panelLeft + leftEdges[index]),
                    0,
                    36,
                    50))))
            .ToArray();
    }

    private static Rectangle CreateBrowserTeamSelectButtonBounds(float panelLeft, float left, float right, float top, float bottom)
    {
        return new Rectangle(
            (int)MathF.Round(panelLeft + left),
            (int)MathF.Round(top),
            Math.Max(1, (int)MathF.Round(right - left)),
            Math.Max(1, (int)MathF.Round(bottom - top)));
    }

    public bool TryInvokeBrowserAutomationAction(string actionSet, string label)
    {
        if (!OperatingSystem.IsBrowser()
            || string.IsNullOrWhiteSpace(actionSet)
            || string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        switch (actionSet.Trim().ToLowerInvariant())
        {
            case "menu":
                return TryInvokeBrowserMainMenuAction(label);
            case "practice":
                return TryInvokeBrowserPracticeAction(label);
            case "teamselect":
                return TryInvokeBrowserTeamSelectAction(label);
            case "classselect":
                return TryInvokeBrowserClassSelectAction(label);
            default:
                return false;
        }
    }

    private bool TryInvokeBrowserMainMenuAction(string label)
    {
        if (!_mainMenuOpen || GetActiveMainMenuOverlay() != MainMenuOverlayKind.None)
        {
            return false;
        }

        var button = BuildMainMenuButtons()
            .FirstOrDefault(candidate => string.Equals(candidate.Label, label, StringComparison.Ordinal));
        if (button.Activate is null)
        {
            return false;
        }

        button.Activate();
        return true;
    }

    private bool TryInvokeBrowserPracticeAction(string label)
    {
        if (!_practiceSetupOpen)
        {
            return false;
        }

        switch (label)
        {
            case "Enemy Bots -":
                CyclePracticeEnemyBots(-1);
                return true;
            case "Enemy Bots +":
                CyclePracticeEnemyBots(1);
                return true;
            case "Friendly Bots -":
                CyclePracticeFriendlyBots(-1);
                return true;
            case "Friendly Bots +":
                CyclePracticeFriendlyBots(1);
                return true;
            case "Start Practice":
                TryStartPracticeFromSetup();
                return true;
            case "Experimental":
                OpenClientPowersMenu(fromGameplay: false);
                return true;
            case "Back":
                _practiceSetupOpen = false;
                return true;
            default:
                return false;
        }
    }

    private bool TryInvokeBrowserTeamSelectAction(string label)
    {
        if (!_teamSelectOpen)
        {
            return false;
        }

        switch (label)
        {
            case "Auto Select":
                ApplyTeamSelection(0);
                return true;
            case "Spectate":
                ApplyTeamSelection(1);
                return true;
            case "RED":
                ApplyTeamSelection(2);
                return true;
            case "BLU":
                ApplyTeamSelection(3);
                return true;
            default:
                return false;
        }
    }

    private bool TryInvokeBrowserClassSelectAction(string label)
    {
        if (!_classSelectOpen)
        {
            return false;
        }

        var selectedClass = label switch
        {
            "Scout" => PlayerClass.Scout,
            "Pyro" => PlayerClass.Pyro,
            "Soldier" => PlayerClass.Soldier,
            "Heavy" => PlayerClass.Heavy,
            "Demoman" => PlayerClass.Demoman,
            "Medic" => PlayerClass.Medic,
            "Engineer" => PlayerClass.Engineer,
            "Spy" => PlayerClass.Spy,
            "Sniper" => PlayerClass.Sniper,
            "Random" => GetRandomPlayableClass(),
            _ => default,
        };
        if (selectedClass == default && !string.Equals(label, "Scout", StringComparison.Ordinal))
        {
            return false;
        }

        ApplyDirectClassSelection(selectedClass);
        CloseGameplaySelectionMenus();
        return true;
    }
}
