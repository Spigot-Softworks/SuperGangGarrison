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
        bool ManualConnectOpen,
        bool PracticeSetupOpen,
        bool TeamSelectOpen,
        bool ClassSelectOpen,
        bool ChatOpen,
        string ChatInput,
        string ManualConnectHost,
        string ManualConnectPort,
        bool EditingConnectHost,
        bool EditingConnectPort,
        bool AwaitingJoin,
        bool PracticeSessionActive,
        bool NetworkConnected,
        string NetworkServerDescription,
        int EstimatedPingMilliseconds,
        ulong LastAppliedSnapshotFrame,
        int QueuedAuthoritativeSnapshotCount,
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
        string IngameResolution,
        int ViewportWidth,
        int ViewportHeight,
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
        BrowserAutomationAction[] ManualConnectButtons,
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
            ManualConnectOpen: false,
            PracticeSetupOpen: false,
            TeamSelectOpen: false,
            ClassSelectOpen: false,
            ChatOpen: false,
            ChatInput: string.Empty,
            ManualConnectHost: string.Empty,
            ManualConnectPort: string.Empty,
            EditingConnectHost: false,
            EditingConnectPort: false,
            AwaitingJoin: false,
            PracticeSessionActive: false,
            NetworkConnected: false,
            NetworkServerDescription: string.Empty,
            EstimatedPingMilliseconds: -1,
            LastAppliedSnapshotFrame: 0UL,
            QueuedAuthoritativeSnapshotCount: 0,
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
            IngameResolution: string.Empty,
            ViewportWidth: 0,
            ViewportHeight: 0,
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
            ManualConnectButtons: [],
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
            ManualConnectOpen: _manualConnectOpen,
            PracticeSetupOpen: _practiceSetupOpen,
            TeamSelectOpen: _teamSelectOpen,
            ClassSelectOpen: _classSelectOpen,
            ChatOpen: _chatOpen,
            ChatInput: _chatInput,
            ManualConnectHost: _connectHostBuffer,
            ManualConnectPort: _connectPortBuffer,
            EditingConnectHost: _editingConnectHost,
            EditingConnectPort: _editingConnectPort,
            AwaitingJoin: _world.LocalPlayerAwaitingJoin,
            PracticeSessionActive: IsPracticeSessionActive,
            NetworkConnected: _networkClient.IsConnected,
            NetworkServerDescription: _networkClient.ServerDescription ?? string.Empty,
            EstimatedPingMilliseconds: _networkClient.EstimatedPingMilliseconds,
            LastAppliedSnapshotFrame: _lastAppliedSnapshotFrame,
            QueuedAuthoritativeSnapshotCount: _queuedAuthoritativeSnapshots.Count,
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
            IngameResolution: GetIngameResolutionLabel(_ingameResolution),
            ViewportWidth: ViewportWidth,
            ViewportHeight: ViewportHeight,
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
            ManualConnectButtons: GetBrowserManualConnectAutomationActions(),
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
            new BrowserAutomationAction("VIP Rules", BrowserAutomationRect.FromRectangle(layout.VipRulesBounds)),
            new BrowserAutomationAction("Start Practice", BrowserAutomationRect.FromRectangle(layout.StartBounds), canEnterGameplaySession),
            new BrowserAutomationAction("Experimental", BrowserAutomationRect.FromRectangle(layout.ClientPowersBounds)),
            new BrowserAutomationAction("Back", BrowserAutomationRect.FromRectangle(layout.BackBounds)),
        ];
    }

    private BrowserAutomationAction[] GetBrowserManualConnectAutomationActions()
    {
        if (!_manualConnectOpen)
        {
            return [];
        }

        GetManualConnectLayout(
            out _,
            out var hostBounds,
            out var portBounds,
            out var connectBounds,
            out var backBounds,
            out _);
        return
        [
            new BrowserAutomationAction("Edit Host", BrowserAutomationRect.FromRectangle(hostBounds)),
            new BrowserAutomationAction("Edit Port", BrowserAutomationRect.FromRectangle(portBounds)),
            new BrowserAutomationAction("Connect", BrowserAutomationRect.FromRectangle(connectBounds)),
            new BrowserAutomationAction("Back", BrowserAutomationRect.FromRectangle(backBounds)),
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
            case "manualconnect":
                return TryInvokeBrowserManualConnectAction(label);
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
            case "Special Abilities":
                TogglePracticeSpecialAbilities();
                return true;
            case "VIP Rules":
                TogglePracticeVipRules();
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

    private bool TryInvokeBrowserManualConnectAction(string label)
    {
        if (!_manualConnectOpen)
        {
            return false;
        }

        switch (label)
        {
            case "Edit Host":
                _connectionFlowController.SetManualConnectEditingField(editHost: true);
                return true;
            case "Edit Port":
                _connectionFlowController.SetManualConnectEditingField(editHost: false);
                return true;
            case "Connect":
                TryConnectFromMenu();
                return true;
            case "Back":
                CloseManualConnectMenu(clearStatus: false);
                return true;
            default:
                return false;
        }
    }

    public bool TrySetBrowserAutomationValue(string fieldName, string value)
    {
        if (!OperatingSystem.IsBrowser() || string.IsNullOrWhiteSpace(fieldName))
        {
            return false;
        }

        switch (fieldName.Trim().ToLowerInvariant())
        {
            case "manualconnect_host":
                if (!_manualConnectOpen)
                {
                    return false;
                }

                _connectHostBuffer = (value ?? string.Empty).Trim();
                InitializeConnectHostCursor();
                _connectionFlowController.SetManualConnectEditingField(editHost: true);
                return true;
            case "manualconnect_port":
                if (!_manualConnectOpen)
                {
                    return false;
                }

                var digitsOnly = new string((value ?? string.Empty).Where(char.IsDigit).Take(5).ToArray());
                _connectPortBuffer = digitsOnly;
                InitializeConnectPortCursor();
                _connectionFlowController.SetManualConnectEditingField(editHost: false);
                return true;
            case "practice_enemy_bots":
                if (!_practiceSetupOpen || !int.TryParse((value ?? string.Empty).Trim(), out var practiceEnemyBots))
                {
                    return false;
                }

                _practiceEnemyBotCount = Math.Clamp(practiceEnemyBots, 0, 9);
                return true;
            case "practice_friendly_bots":
                if (!_practiceSetupOpen || !int.TryParse((value ?? string.Empty).Trim(), out var practiceFriendlyBots))
                {
                    return false;
                }

                _practiceFriendlyBotCount = Math.Clamp(practiceFriendlyBots, 0, 9);
                return true;
            default:
                return false;
        }
    }

    public bool TryBeginBrowserAutomationConnect(string host, string portText)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return false;
        }

        _connectHostBuffer = (host ?? string.Empty).Trim();
        _connectPortBuffer = new string((portText ?? string.Empty).Where(char.IsDigit).Take(5).ToArray());
        InitializeConnectHostCursor();
        InitializeConnectPortCursor();
        _manualConnectOpen = true;
        _connectionFlowController.SetManualConnectEditingField(editHost: false);
        _menuStatusMessage = string.Empty;
        return _connectionFlowController.TryParseManualConnectTarget(out var endpoint)
            && TryConnectToServer(endpoint, addConsoleFeedback: false);
    }

    public bool TryBeginBrowserAutomationPractice(int enemyBotCount, int friendlyBotCount)
    {
        if (!OperatingSystem.IsBrowser() || !_practiceSetupOpen)
        {
            return false;
        }

        _practiceEnemyBotCount = Math.Clamp(enemyBotCount, 0, 9);
        _practiceFriendlyBotCount = Math.Clamp(friendlyBotCount, 0, 9);
        TryStartPracticeFromSetup();
        return true;
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
