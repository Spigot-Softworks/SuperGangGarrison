#nullable enable

using Microsoft.Xna.Framework;
using System;
using System.Linq;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

public partial class Game1
{
    private int _prePredictionFlameCount;
    private int _browserHostedLobbyDrawCount;
    public sealed record BrowserAutomationRect(int X, int Y, int Width, int Height)
    {
        public static BrowserAutomationRect FromRectangle(Rectangle rectangle)
        {
            return new BrowserAutomationRect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
        }
    }

    public sealed record BrowserAutomationAction(string Label, BrowserAutomationRect Bounds, bool Enabled = true);

    public sealed record BrowserRoomSnapshot(string RoomCode, string MenuPage, string Phase,
        int PlayerCount, int LocalSlot, bool IsOwner, long ServerTick, string[] MenuLabels, string[] OfferChoices);

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
        public BrowserRoomSnapshot? LastToDie { get; init; }
        public bool LoadingOverlayVisible { get; init; }
        public bool JoiningOverlayVisible { get; init; }
        public bool SurvivorBuffActive { get; init; }
        public bool AutomaticRespawnSuppressed { get; init; }
        public bool DroppedWeaponPickupsEnabled { get; init; }
        public string RoomPhase { get; init; } = "";
        public int RoomGeneration { get; init; }
        public string RoomSettings { get; init; } = "";
        public string[] InGameMenuLabels { get; init; } = [];
        public bool MusicPlaying { get; init; }
        public bool MusicPaused { get; init; }
        public string[] VoteMenuLabels { get; init; } = [];
        public string CurrentMap { get; init; } = "";
        public int VoteYesCount { get; init; }
        public bool VoteActive { get; init; }
        public string[] RunRoster { get; init; } = [];
        public string[] RoomRoster { get; init; } = [];
        public ulong RunRevision { get; init; }
        public string LastRunCommandResult { get; init; } = "";
        public int FirstPlayHintIndex { get; init; } = -1;
        public bool FirstPlayHintsFinished { get; init; }
        public System.Collections.Generic.Dictionary<string, BrowserAutomationRect> HudBounds { get; init; } = [];
        public string CustomBubbleBinding { get; init; } = "";
        public string BubbleMenu { get; init; } = "";
        public string[] PracticeBotNames { get; init; } = [];
        public int ParticleMode { get; init; }
        public int FlameRenderMode { get; init; }
        public int BloodRenderMode { get; init; }
        public bool ReducedBrowserEffects { get; init; }
        public string LoadingTitle { get; init; } = "";
        public int HostedLobbyDrawCount { get; init; }
        public int RocketSmokeCount { get; init; }
        public int FlameSmokeCount { get; init; }
        public int WorldFlameCount { get; init; }
        public int ProtocolFlameCount { get; init; }
        public int PrePredictionFlameCount { get; init; }
        public string[] ProtocolFlameStates { get; init; } = [];
        public int ShellCount { get; init; }
        public bool UmbrellaActive { get; init; }
        public int UmbrellaOpeningTicks { get; init; }
        public string WeaponAnimation { get; init; } = "";
        public string WeaponSprite { get; init; } = "";
        public int PrimaryAmmo { get; init; }
        public string EquippedItemId { get; init; } = "";
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
            StatusMessage: (_lastToDieMenuOpen ? GetLastToDieMenuStatusMessage() : _menuStatusMessage) ?? string.Empty,
            SelectedPracticeMap: GetSelectedPracticeMapEntry()?.LevelName ?? string.Empty,
            PracticeTickRate: _practiceTickRate,
            PracticeEnemyBotCount: _practiceEnemyBotCount,
            PracticeFriendlyBotCount: _practiceFriendlyBotCount,
            MenuButtons: GetBrowserMainMenuAutomationActions(),
            ManualConnectButtons: GetBrowserManualConnectAutomationActions(),
            PracticeButtons: GetBrowserPracticeAutomationActions(),
            TeamSelectButtons: GetBrowserTeamSelectAutomationActions(),
            ClassSelectButtons: GetBrowserClassSelectAutomationActions())
        {
            LastToDie = new BrowserRoomSnapshot(_peerRoomSession?.Connection.Grant.Code ?? _managedRoom?.RoomCode ?? "", _lastToDieMenuPage.ToString(),
                _networkClient.LastToDieState.Snapshot?.Phase.ToString() ?? _peerRoomSession?.State?.Phase ?? "",
                _networkClient.LastToDieState.Snapshot?.Players.Count ?? _peerRoomSession?.State?.Players?.Length ?? 0, _networkClient.LocalPlayerSlot,
                IsManagedRoomOwner || IsPeerRoomOwner || IsEmbeddedSessionOwner, _networkClient.LastToDieState.Snapshot?.ServerTick ?? 0,
                _lastToDieMenuOpen ? _lastToDieMenuPage == LastToDieMenuPage.PeerLobby ? GetPeerLobbyButtons() : GetLastToDieMenuButtonLabels() : [],
                _networkClient.LastToDieState.Snapshot?.Players.FirstOrDefault(p => p.Slot == _networkClient.LocalPlayerSlot)?.ActiveOfferChoices.ToArray() ?? []),
            LoadingOverlayVisible = _loadingOverlayVisible,
            RoomPhase = _peerRoomSession?.State?.Phase ?? "",
            RoomGeneration = _peerRoomSession?.State?.Generation ?? 0,
            RunRoster = _networkClient.LastToDieState.Snapshot?.Players.Select(p => $"{p.Slot}: connected={p.IsConnected}, ready={p.IsReady}, host={p.IsHost}, survivor={p.SurvivorId}").ToArray() ?? [],
            RoomRoster = _peerRoomSession?.State?.Players?.Select(p => $"{p.Slot}: connected={p.Connected}, ready={p.Ready}, team={p.Team}").ToArray() ?? [],
            RunRevision = _networkClient.LastToDieState.Snapshot?.StructuralRevision ?? 0,
            LastRunCommandResult = _networkClient.LastToDieState.LatestCommandResult?.ToString() ?? "",
            RoomSettings = _peerRoomSession?.State?.Settings?.ToString() ?? "",
            InGameMenuLabels = _inGameMenuOpen ? _inGameMenuController.GetInGameMenuActions().Select(a => a.Label).ToArray() : [],
            MusicPlaying = _voiceChat?.ServerState?.JukeboxPlaying == true,
            MusicPaused = _voiceChat?.ServerState?.JukeboxPaused == true,
            VoteMenuLabels = _voteMenuOpen ? BuildVoteMenuActions().Select(a => a.Label).ToArray() : [],
            CurrentMap = _world.Level.Name,
            FirstPlayHintIndex = _firstPlayHints is { Visible: true } hint && hint.ElapsedSeconds < hint.Marker.DurationSeconds ? hint.Index : -1,
            FirstPlayHintsFinished = _firstPlayHints?.Finished ?? false,
            HudBounds = _hudResolvedElements.ToDictionary(static entry => entry.Key,
                static entry => BrowserAutomationRect.FromRectangle(entry.Value.Bounds)),
            CustomBubbleBinding = InputBindingsSettings.FormatBinding(_inputBindings.CustomBubble),
            BubbleMenu = _bubbleMenuKind.ToString(),
            PracticeBotNames = _practiceBotSlots.Values.Select(slot => slot.DisplayName).ToArray(),
            ParticleMode = _particleMode,
            FlameRenderMode = _flameRenderMode,
            BloodRenderMode = _bloodRenderMode,
            ReducedBrowserEffects = UseReducedBrowserEffects,
            LoadingTitle = GetLoadingOverlayTitle(IsRestrictedBrowserEdition),
            HostedLobbyDrawCount = _browserHostedLobbyDrawCount,
            RocketSmokeCount = _rocketSmokeVisuals.Count,
            FlameSmokeCount = _flameSmokeVisuals.Count,
            WorldFlameCount = _world.Flames.Count,
            PrePredictionFlameCount = _prePredictionFlameCount,
            ProtocolFlameStates = _networkClient.Protocol64State.Projectiles
                .Where(projectile => projectile.EntityKind == OpenGarrison.Protocol.Protocol64ProjectileKind.Flame)
                .Take(4).Select(projectile => $"{projectile.StateTick}/{projectile.RemainingLifetimeTicks}@{projectile.X},{projectile.Y}").ToArray(),
            ProtocolFlameCount = _networkClient.Protocol64State.Projectiles.Count(
                projectile => projectile.EntityKind == OpenGarrison.Protocol.Protocol64ProjectileKind.Flame),
            ShellCount = _shellVisuals.Count,
            UmbrellaActive = GetPlayerIsCivvieUmbrellaActive(_world.LocalPlayer),
            UmbrellaOpeningTicks = GetPlayerPredictedPresentationState(_world.LocalPlayer).CivvieUmbrellaOpeningElapsedTicks,
            WeaponAnimation = GetPlayerWeaponAnimationMode(_world.LocalPlayer).ToString(),
            WeaponSprite = GetWeaponRenderDefinition(_world.LocalPlayer).NormalSpriteName ?? "",
            PrimaryAmmo = _world.LocalPlayer.CurrentShells,
            EquippedItemId = _world.LocalPlayer.GameplayLoadoutState.EquippedItemId,
            VoteYesCount = _votePresentationState?.YesVotes ?? 0,
            VoteActive = _votePresentationState is { IsComplete: false, RemainingTicks: > 0 },
            SurvivorBuffActive = _world.LocalPlayer.HasLastToDieSurvivorBuff,
            AutomaticRespawnSuppressed = _world.IsNetworkPlayerAutomaticRespawnSuppressed(_world.LocalPlayer),
            DroppedWeaponPickupsEnabled = _world.IsLastToDieGameplaySettingEnabled(settings => settings.EnableEnemyDroppedWeapons),
            JoiningOverlayVisible = _loadingOverlayVisible && _loadingOverlayIsJoining,
        };
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
            new BrowserAutomationAction("Special Abilities -", BrowserAutomationRect.FromRectangle(layout.SpecialAbilitiesLeftBounds)),
            new BrowserAutomationAction("Special Abilities +", BrowserAutomationRect.FromRectangle(layout.SpecialAbilitiesRightBounds)),
            new BrowserAutomationAction(_editingPeerPractice ? "Apply" : "Start Singleplayer", BrowserAutomationRect.FromRectangle(layout.StartBounds), canEnterGameplaySession),
            new BrowserAutomationAction(_editingPeerPractice ? "Cancel" : "Co-Op Lobby", BrowserAutomationRect.FromRectangle(layout.ClientPowersBounds)),
            new BrowserAutomationAction("Join Co-Op", BrowserAutomationRect.FromRectangle(layout.JoinCoOpBounds), !_editingPeerPractice),
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
            out var pasteBounds,
            out var connectBounds,
            out var backBounds,
            out _);
        if (_lastToDieRoomCodeJoinOpen)
        {
            return
            [
                new BrowserAutomationAction("Edit Room Code", BrowserAutomationRect.FromRectangle(hostBounds)),
                new BrowserAutomationAction("Paste Room Code", BrowserAutomationRect.FromRectangle(pasteBounds)),
                new BrowserAutomationAction("Join", BrowserAutomationRect.FromRectangle(connectBounds)),
                new BrowserAutomationAction("Back", BrowserAutomationRect.FromRectangle(backBounds)),
            ];
        }

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
            case "vote":
                if (!_voteMenuOpen) return false;
                var voteActions = BuildVoteMenuActions();
                var voteIndex = voteActions.FindIndex(a => a.Label == label);
                if (voteIndex < 0) return false;
                voteActions[voteIndex].Activate(); return true;
            case "ingame":
                if (!_networkClient.IsConnected && !IsPracticeSessionActive) return false;
                if (label == "Open") { _inGameMenuController.OpenInGameMenu(); return true; }
                if (!_inGameMenuOpen) return false;
                var menuActions = _inGameMenuController.GetInGameMenuActions();
                var menuIndex = menuActions.FindIndex(a => a.Label == label);
                if (menuIndex < 0) return false;
                menuActions[menuIndex].Activate(); return true;
            case "ltd":
                if (!_lastToDieMenuOpen) return false;
                if (_lastToDieMenuPage == LastToDieMenuPage.PeerLobby)
                {
                    var peerIndex = Array.IndexOf(GetPeerLobbyButtons(), label);
                    if (peerIndex < 0) return false;
                    ActivatePeerLobbyButton(peerIndex); return true;
                }
                var index = Array.IndexOf(GetLastToDieMenuButtonLabels(), label);
                if (index < 0) return false;
                ActivateLastToDieMenuButton(index);
                return true;
            case "ltdlobby":
                if (_networkClient.LastToDieState.Snapshot?.Phase != OpenGarrison.Protocol.LastToDieWirePhase.Lobby) return false;
                if (label == "Ready") _networkClient.SendLastToDieCommand(OpenGarrison.Protocol.LastToDieCommandKind.Ready);
                else if (label == "Start" && IsManagedRoomOwner) _networkClient.SendLastToDieCommand(OpenGarrison.Protocol.LastToDieCommandKind.RequestStart);
                else return false;
                return true;
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
            case "Special Abilities -":
                CyclePracticeSpecialAbilities(-1);
                return true;
            case "Special Abilities +":
                CyclePracticeSpecialAbilities(1);
                return true;
            case "Start Practice":
            case "Start Singleplayer":
            case "Apply":
                ApplyPeerPracticeSettings();
                return true;
            case "Co-Op Lobby":
                OpenPracticeCoOpMenu();
                return true;
            case "Join Co-Op":
                OpenPracticeCoOpJoin();
                return true;
            case "Cancel":
                ShowPeerLobby();
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
            case "Edit Room Code" when _lastToDieRoomCodeJoinOpen:
                _connectionFlowController.SetManualConnectEditingField(editHost: true);
                return true;
            case "Paste Room Code" when _lastToDieRoomCodeJoinOpen:
                _connectionFlowController.SetManualConnectEditingField(editHost: true);
                return PasteActiveClipboard();
            case "Join" when _lastToDieRoomCodeJoinOpen:
                TryConnectFromMenu();
                return true;
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
                CloseManualConnectMenuToOrigin(clearStatus: false);
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
            case "particle_mode":
                if (!int.TryParse(value, out var particleMode) || particleMode is < 0 or > 2) return false;
                _particleMode = particleMode;
                return true;
            case "flame_render_mode":
                if (!int.TryParse(value, out var flameMode) || flameMode is < 0 or > 1) return false;
                _flameRenderMode = flameMode;
                return true;
            case "blood_render_mode":
                if (!int.TryParse(value, out var bloodMode) || bloodMode is < 0 or > 1) return false;
                _bloodRenderMode = bloodMode;
                if (_bloodRenderMode != 0)
                {
                    _gameplayGoreEffectsController.ResetBloodSquibEffects();
                }

                return true;
            case "practice_map":
                return _practiceSetupOpen && SelectPracticeMapEntry(value);
            case "ltd_code":
                if (!_lastToDieMenuOpen || _lastToDieMenuPage != LastToDieMenuPage.RoomJoin) return false;
                _managedRoomCodeBuffer = new string((value ?? "").Where(char.IsAsciiLetterOrDigit).Take(24).ToArray()).ToUpperInvariant();
                return true;
            case "ltd_survivor":
                if (_networkClient.LastToDieState.Snapshot?.Phase != OpenGarrison.Protocol.LastToDieWirePhase.SurvivorChoice) return false;
                _networkClient.SendLastToDieCommand(OpenGarrison.Protocol.LastToDieCommandKind.ChooseSurvivor, selectedId: value);
                return true;
            case "ltd_reward":
                var run = _networkClient.LastToDieState.Snapshot;
                var participant = run?.Players.FirstOrDefault(p => p.Slot == _networkClient.LocalPlayerSlot);
                if (run?.Phase != OpenGarrison.Protocol.LastToDieWirePhase.RewardChoice || participant is null
                    || !participant.ActiveOfferChoices.Contains(value)) return false;
                _networkClient.SendLastToDieCommand(OpenGarrison.Protocol.LastToDieCommandKind.SelectReward, offerId: participant.ActiveOfferId, selectedId: value);
                return true;
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
        if (!OperatingSystem.IsBrowser() || IsRestrictedBrowserEdition)
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
            "Civilian" => PlayerClass.Quote,
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
