#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.BotAI;
using OpenGarrison.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const string LastToDieExcludedRotationMapName = "Conflict";
    private const int LastToDieStartingEnemyBotCount = 3;
    private const int LastToDieFinalEnemyBotCount = 10;
    private const int LastToDieStartingStageMinutes = 3;
    private const int LastToDieStageMinuteIncrement = 1;
    private const int LastToDieFinalStageMinutes =
        LastToDieStartingStageMinutes + ((LastToDieFinalEnemyBotCount - LastToDieStartingEnemyBotCount) * LastToDieStageMinuteIncrement);
    private const int LastToDieStageCount = (LastToDieFinalEnemyBotCount - LastToDieStartingEnemyBotCount) + 1;
    private const int LastToDieMatchTimeLimitMinutes = 30;
    private const int LastToDieCapLimit = 5;
    private const int LastToDieRespawnSeconds = 5;
    private const int LastToDiePerkChoiceCount = 3;
    private const int LastToDieStageClearFadeTicks = 24;
    private const int LastToDieStageClearContinueDelayTicks = 18;
    private const int LastToDieFailureFadeTicks = 45;
    private const int LastToDieFailureContinueDelayTicks = 18;
    private const float LastToDieStageIntroDurationSeconds = 2f;

    private enum LastToDiePerkKind
    {
        SoldierShotgun,
        HealOnDamage,
        HealOnKill,
        RateOfFireOnDamage,
        SoldierInstantReload,
        SpeedOnDamage,
        SpeedOnKill,
        PassiveHealthRegeneration,
        InvincibilityOnKill,
        ProjectileSpeedMultiplier,
        AirshotDamageMultiplier,
    }

    private readonly record struct LastToDiePerkDefinition(
        LastToDiePerkKind Kind,
        string Label,
        string Description);

    private readonly record struct LastToDiePerkMenuLayout(
        Rectangle Panel,
        Rectangle[] CardBounds);

    private sealed class LastToDieRunState
    {
        public LastToDieRunState(string levelName)
        {
            CurrentLevelName = levelName;
        }

        public int StageNumber { get; set; } = 1;

        public int EnemyBotCount { get; set; } = LastToDieStartingEnemyBotCount;

        public int StageDurationMinutes { get; set; } = LastToDieStartingStageMinutes;

        public int StageRemainingTicks { get; set; }

        public int StageIntroTicksRemaining { get; set; }

        public string CurrentLevelName { get; set; }

        public bool AwaitingOpeningPerkSelection { get; set; }

        public HashSet<LastToDiePerkKind> ChosenPerks { get; } = [];

        public LastToDiePerkDefinition[] PendingPerkChoices { get; set; } = [];

        public int LevelsCompleted { get; set; }

        public int TotalKills { get; set; }

        public int TotalDamageDealt { get; set; }

        public int ObservedStageKills { get; set; }
    }

    private static readonly LastToDiePerkDefinition[] LastToDiePerkCatalog =
    [
        new(LastToDiePerkKind.SoldierShotgun, "Soldier shotgun", "Press Space to fire secondary shotgun."),
        new(LastToDiePerkKind.HealOnDamage, "Heal on damage", "Restore 35% of dealt damage."),
        new(LastToDiePerkKind.HealOnKill, "Heal on kill", "Restore 25 health on kill."),
        new(LastToDiePerkKind.RateOfFireOnDamage, "Rate of fire on hit", "Landing a hit instantly requeues primary fire."),
        new(LastToDiePerkKind.SoldierInstantReload, "Rocket reload on hit", "Landing a rocket hit refills 1 rocket."),
        new(LastToDiePerkKind.SpeedOnDamage, "Speed on damage", "Gain 20% move speed for 2.5s after dealing damage."),
        new(LastToDiePerkKind.SpeedOnKill, "Speed on kill", "Gain 20% move speed for 3s after kills."),
        new(LastToDiePerkKind.PassiveHealthRegeneration, "Passive regeneration", "Restore 3 health per second while alive."),
        new(LastToDiePerkKind.InvincibilityOnKill, "Invincibility on kill", "Gain 0.5s of invulnerability after kills."),
        new(LastToDiePerkKind.ProjectileSpeedMultiplier, "Projectile speed +20%", "All spawned projectile weapons fly 20% faster."),
        new(LastToDiePerkKind.AirshotDamageMultiplier, "Airshot damage +25%", "Direct projectile airshots deal bonus damage."),
    ];

    private LastToDieRunState? _lastToDieRun;
    private bool _lastToDiePerkMenuOpen;
    private int _lastToDiePerkHoverIndex = -1;
    private bool _lastToDieStageClearOverlayOpen;
    private int _lastToDieStageClearOverlayTicks;
    private bool _lastToDieFailureOverlayOpen;
    private int _lastToDieFailureOverlayTicks;

    private bool IsLastToDieSessionActive => _gameplaySessionKind == GameplaySessionKind.LastToDie;

    private bool IsOfflineBotSessionActive => _gameplaySessionKind is GameplaySessionKind.Practice or GameplaySessionKind.LastToDie;

    private int GetOfflineEnemyBotCount()
    {
        return IsLastToDieSessionActive
            ? _lastToDieRun?.EnemyBotCount ?? 0
            : _practiceEnemyBotCount;
    }

    private int GetOfflineFriendlyBotCount()
    {
        return IsLastToDieSessionActive ? 0 : _practiceFriendlyBotCount;
    }

    private bool ShouldSuspendOfflineGameplaySimulation()
    {
        return IsLastToDieSessionActive
            && (_lastToDiePerkMenuOpen || IsLastToDieStageClearOverlayActive() || IsLastToDieFailurePresentationActive());
    }

    private bool IsLastToDieStageClearOverlayActive()
    {
        return _lastToDieStageClearOverlayOpen;
    }

    private bool IsLastToDieFailureOverlayActive()
    {
        return _lastToDieFailureOverlayOpen;
    }

    private int GetLastToDieStageIntroDurationTicks()
    {
        return Math.Max(1, (int)MathF.Round(_config.TicksPerSecond * LastToDieStageIntroDurationSeconds));
    }

    private void ResetLastToDieState()
    {
        StopLastToDieGameOverSound();
        _lastToDieRun = null;
        _lastToDiePerkMenuOpen = false;
        _lastToDiePerkHoverIndex = -1;
        _lastToDieStageClearOverlayOpen = false;
        _lastToDieStageClearOverlayTicks = 0;
        ClearLastToDieDeathFocusPresentation();
        ResetLastToDieCombatFeedbackPresentation();
        _lastToDieFailureOverlayOpen = false;
        _lastToDieFailureOverlayTicks = 0;
    }

    private void TryStartLastToDieRun()
    {
        _practiceMapEntries = BuildPracticeMapEntries();
        var initialMap = SelectInitialLastToDieMap();
        if (initialMap is null)
        {
            _menuStatusMessage = "No local maps are available for Last To Die.";
            return;
        }

        ResetLastToDieState();
        _lastToDieRun = new LastToDieRunState(initialMap.LevelName)
        {
            AwaitingOpeningPerkSelection = true,
        };
        if (!BeginLastToDieStage(initialMap.LevelName))
        {
            ResetLastToDieState();
            return;
        }

        OpenLastToDiePerkMenu();
    }

    private bool BeginLastToDieStage(string levelName)
    {
        if (_lastToDieRun is null)
        {
            return false;
        }

        var settings = BuildLastToDieExperimentalGameplaySettings(_lastToDieRun);
        if (!TryBeginOfflineBotSession(
                levelName,
                GameplaySessionKind.LastToDie,
                _practiceTickRate,
                settings,
                LastToDieMatchTimeLimitMinutes,
                LastToDieCapLimit,
                LastToDieRespawnSeconds,
                openJoinMenus: false,
                consoleSessionName: "last to die"))
        {
            return false;
        }

        _lastToDiePerkMenuOpen = false;
        _lastToDiePerkHoverIndex = -1;
        _lastToDieStageClearOverlayOpen = false;
        _lastToDieStageClearOverlayTicks = 0;
        ClearLastToDieDeathFocusPresentation();
        ResetLastToDieCombatFeedbackPresentation();
        _lastToDieFailureOverlayOpen = false;
        _lastToDieFailureOverlayTicks = 0;

        _world.PrepareLocalPlayerJoin();
        _world.SetLocalPlayerTeam(PlayerTeam.Red);
        _world.CompleteLocalPlayerJoin(PlayerClass.Soldier);
        _world.TryMoveLocalPlayerToControlPointSpawn();
        SyncPracticeBotRoster(PlayerTeam.Red);
        _world.DespawnEnemyDummy();
        _world.DespawnFriendlyDummy();
        ObserveLastToDieCombatFeedbackState();
        _lastToDieRun.ObservedStageKills = 0;

        _lastToDieRun.CurrentLevelName = levelName;
        _lastToDieRun.StageRemainingTicks = _lastToDieRun.StageDurationMinutes * 60 * _config.TicksPerSecond;
        _lastToDieRun.StageIntroTicksRemaining = GetLastToDieStageIntroDurationTicks();

        AddConsoleLine(
            $"last to die stage={_lastToDieRun.StageNumber} map={levelName} enemies={_lastToDieRun.EnemyBotCount} minutes={_lastToDieRun.StageDurationMinutes}");
        return true;
    }

    private bool TryBeginOfflineBotSession(
        string levelName,
        GameplaySessionKind sessionKind,
        int tickRate,
        ExperimentalGameplaySettings experimentalSettings,
        int timeLimitMinutes,
        int capLimit,
        int respawnSeconds,
        bool openJoinMenus,
        string consoleSessionName)
    {
        if (sessionKind != GameplaySessionKind.LastToDie)
        {
            ResetLastToDieState();
        }

        ResetPracticeBotManagerState(releaseWorldSlots: true);
        ResetPracticeNavigationState();
        _botDiagnosticLatestSnapshot = BotControllerDiagnosticsSnapshot.Empty;
        ResetBotDiagnosticSample();
        _networkClient.Disconnect();
        _networkClient.ClearPendingTeamSelection();
        _networkClient.ClearPendingClassSelection();
        StopHostedServer();
        StopLocalRapidFireWeaponAudio();
        StopMenuMusic();
        StopLastToDieMenuMusic();
        StopFaucetMusic();
        StopIngameMusic();
        StopLastToDieIngameMusic();
        StopLastToDieGameOverSound();
        ResetTransientPresentationEffects();
        ResetProcessedNetworkEventHistory();
        ResetClientTimingState();
        ResetSnapshotStateHistory();
        ResetBackstabVisuals();
        _lastAppliedSnapshotFrame = 0;
        _lastBufferedSnapshotFrame = 0;
        _hasReceivedSnapshot = false;
        _localPlayerSnapshotEntityId = null;
        _hasPredictedLocalPlayerPosition = false;
        _hasSmoothedLocalPlayerRenderPosition = false;
        _predictedLocalPlayerRenderCorrectionOffset = Vector2.Zero;
        _lastPredictedRenderSmoothingTimeSeconds = -1d;
        _pendingPredictedInputs.Clear();
        _pendingNetworkVisualEvents.Clear();
        _pendingNetworkDamageEvents.Clear();

        ReinitializeSimulationForTickRate(tickRate);
        _world.ConfigureExperimentalGameplaySettings(experimentalSettings);
        _world.ConfigureMatchDefaults(
            timeLimitMinutes: timeLimitMinutes,
            capLimit: capLimit,
            respawnSeconds: respawnSeconds);
        if (!_world.TryLoadLevel(levelName))
        {
            _menuStatusMessage = $"Failed to load local map: {levelName}";
            return false;
        }

        _world.Level.ForcedBlockingTeamGates = sessionKind == GameplaySessionKind.LastToDie
            ? TeamGateLockMask.Red
            : TeamGateLockMask.None;

        _world.AutoRestartOnMapChange = sessionKind != GameplaySessionKind.LastToDie;
        _gameplaySessionKind = sessionKind;
        LoadPracticeNavigationAssetsForCurrentLevel();
        _pendingHostedConnectTicks = -1;
        _pendingHostedConnectPort = 8190;
        _practiceSetupOpen = false;
        _lastToDieMenuOpen = false;
        _lastToDiePerkMenuOpen = false;
        _clientPowersOpen = false;
        _clientPowersOpenedFromGameplay = false;
        _mainMenuOpen = false;
        _manualConnectOpen = false;
        _hostSetupOpen = false;
        _hostSetupEditField = HostSetupEditField.None;
        _creditsOpen = false;
        _optionsMenuOpen = false;
        _optionsMenuOpenedFromGameplay = false;
        _pluginOptionsMenuOpen = false;
        _pluginOptionsMenuOpenedFromGameplay = false;
        _controlsMenuOpen = false;
        _controlsMenuOpenedFromGameplay = false;
        _inGameMenuOpen = false;
        _quitPromptOpen = false;
        _quitPromptHoverIndex = -1;
        _consoleOpen = false;
        _passwordPromptOpen = false;
        _passwordEditBuffer = string.Empty;
        _passwordPromptMessage = string.Empty;
        _teamSelectOpen = openJoinMenus;
        _classSelectOpen = false;
        _pendingClassSelectTeam = null;
        _menuStatusMessage = string.Empty;
        InitializePracticeBotNamePoolForMatch();

        if (openJoinMenus)
        {
            _world.PrepareLocalPlayerJoin();
            ApplyPracticeDummyPreferencesBeforeJoin();
        }

        AddConsoleLine($"{consoleSessionName} started on {levelName} tickrate={tickRate}");
        return true;
    }

    private void AdvanceLastToDieSimulationTick()
    {
        if (!IsLastToDieSessionActive
            || _lastToDieRun is null
            || _lastToDiePerkMenuOpen
            || IsLastToDieStageClearOverlayActive()
            || IsLastToDieFailurePresentationActive())
        {
            return;
        }

        if (_lastToDieRun.StageRemainingTicks > 0)
        {
            _lastToDieRun.StageRemainingTicks -= 1;
        }

        if (_lastToDieRun.StageIntroTicksRemaining > 0)
        {
            _lastToDieRun.StageIntroTicksRemaining -= 1;
        }
    }

    private void UpdateLastToDieSession(int clientTicks)
    {
        _ = clientTicks;
        UpdateLastToDieRunStats();

        if (!IsLastToDieSessionActive
            || _lastToDieRun is null
            || _lastToDiePerkMenuOpen
            || IsLastToDieStageClearOverlayActive()
            || IsLastToDieFailurePresentationActive())
        {
            return;
        }

        if (!_world.LocalPlayerAwaitingJoin && !_world.LocalPlayer.IsAlive)
        {
            TriggerLastToDieDeathFocusFailure();
            return;
        }

        if (_world.MatchState.IsEnded)
        {
            if (_world.MatchState.WinnerTeam == PlayerTeam.Blue)
            {
                TriggerLastToDieFailure();
                return;
            }

            if (_world.MatchState.WinnerTeam == PlayerTeam.Red)
            {
                HandleLastToDieStageClear();
                return;
            }
        }

        if (_lastToDieRun.StageRemainingTicks <= 0)
        {
            HandleLastToDieStageClear();
        }
    }

    private void HandleLastToDieStageClear()
    {
        if (_lastToDieRun is null
            || _lastToDiePerkMenuOpen
            || IsLastToDieStageClearOverlayActive()
            || IsLastToDieFailurePresentationActive())
        {
            return;
        }

        _lastToDieRun.LevelsCompleted += 1;

        if (IsLastToDieFinalStage(_lastToDieRun))
        {
            ReturnToLastToDieMenu("Last To Die cleared.");
            return;
        }

        OpenLastToDieStageClearOverlay();
    }

    private void OpenLastToDieStageClearOverlay()
    {
        StopIngameMusic();
        StopLastToDieIngameMusic();
        CloseInGameMenu();
        if (_lastToDieStageClearOverlayOpen)
        {
            return;
        }

        _lastToDieStageClearOverlayOpen = true;
        _lastToDieStageClearOverlayTicks = 0;
    }

    private void ContinueFromLastToDieStageClearOverlay()
    {
        _lastToDieStageClearOverlayOpen = false;
        _lastToDieStageClearOverlayTicks = 0;
        SuppressPrimaryFireUntilMouseRelease();
        OpenLastToDiePerkMenu();
    }

    private static bool IsLastToDieFinalStage(LastToDieRunState run)
    {
        return run.EnemyBotCount >= LastToDieFinalEnemyBotCount
            && run.StageDurationMinutes >= LastToDieFinalStageMinutes;
    }

    private void OpenLastToDiePerkMenu()
    {
        if (_lastToDieRun is null)
        {
            return;
        }

        var choices = BuildLastToDiePerkChoices(_lastToDieRun);
        if (choices.Length == 0)
        {
            if (_lastToDieRun.AwaitingOpeningPerkSelection)
            {
                _lastToDieRun.AwaitingOpeningPerkSelection = false;
                _lastToDieRun.PendingPerkChoices = [];
                return;
            }

            AdvanceLastToDieStage();
            return;
        }

        StopIngameMusic();
        StopLastToDieIngameMusic();
        _lastToDieRun.PendingPerkChoices = choices;
        _lastToDiePerkMenuOpen = true;
        _lastToDiePerkHoverIndex = -1;
    }

    private static LastToDiePerkDefinition[] BuildLastToDiePerkChoices(LastToDieRunState run)
    {
        var available = LastToDiePerkCatalog
            .Where(definition => !run.ChosenPerks.Contains(definition.Kind))
            .ToList();
        ShuffleLastToDiePerks(available);
        return available
            .Take(Math.Min(LastToDiePerkChoiceCount, available.Count))
            .ToArray();
    }

    private void AdvanceLastToDieStage()
    {
        if (_lastToDieRun is null)
        {
            return;
        }

        _lastToDieRun.StageNumber = Math.Min(LastToDieStageCount, _lastToDieRun.StageNumber + 1);
        _lastToDieRun.EnemyBotCount = Math.Min(LastToDieFinalEnemyBotCount, _lastToDieRun.EnemyBotCount + 1);
        _lastToDieRun.StageDurationMinutes = Math.Min(
            LastToDieFinalStageMinutes,
            _lastToDieRun.StageDurationMinutes + LastToDieStageMinuteIncrement);
        _lastToDieRun.PendingPerkChoices = [];

        var nextMap = SelectRandomLastToDieMap(_lastToDieRun.CurrentLevelName);
        if (nextMap is null || !BeginLastToDieStage(nextMap.LevelName))
        {
            ReturnToLastToDieMenu("Failed to start the next Last To Die stage.");
        }
    }

    private PracticeMapEntry? SelectRandomLastToDieMap(string? excludedLevelName)
    {
        if (_practiceMapEntries.Count == 0)
        {
            _practiceMapEntries = BuildPracticeMapEntries();
        }

        if (_practiceMapEntries.Count == 0)
        {
            return null;
        }

        var rotationEntries = _practiceMapEntries
            .Where(IsEligibleLastToDieRotationMap)
            .ToList();
        if (rotationEntries.Count == 0)
        {
            return null;
        }

        var candidates = string.IsNullOrWhiteSpace(excludedLevelName)
            ? rotationEntries
            : rotationEntries
                .Where(entry => !string.Equals(entry.LevelName, excludedLevelName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        if (candidates.Count == 0)
        {
            candidates = rotationEntries;
        }

        return candidates[RandomNumberGenerator.GetInt32(candidates.Count)];
    }

    private PracticeMapEntry? SelectInitialLastToDieMap()
    {
        if (_practiceMapEntries.Count == 0)
        {
            _practiceMapEntries = BuildPracticeMapEntries();
        }

        return SelectRandomLastToDieMap(excludedLevelName: null);
    }

    private static bool IsEligibleLastToDieRotationMap(PracticeMapEntry entry)
    {
        return entry.Mode == GameModeKind.KingOfTheHill
            && !string.Equals(entry.LevelName, LastToDieExcludedRotationMapName, StringComparison.OrdinalIgnoreCase);
    }

    private static void ShuffleLastToDiePerks(List<LastToDiePerkDefinition> definitions)
    {
        for (var index = definitions.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (definitions[index], definitions[swapIndex]) = (definitions[swapIndex], definitions[index]);
        }
    }

    private static ExperimentalGameplaySettings BuildLastToDieExperimentalGameplaySettings(LastToDieRunState run)
    {
        var settings = new ExperimentalGameplaySettings(
            EnableComboTracking: true,
            EnableKillStreakTracking: true,
            EnableEnemyHealthPackDrops: true,
            EnableEnemyDroppedWeapons: true,
            EnemyHealthPackDropChance: 1f);

        foreach (var perk in run.ChosenPerks)
        {
            settings = perk switch
            {
                LastToDiePerkKind.SoldierShotgun => settings with { EnableSoldierShotgunSecondaryWeapon = true },
                LastToDiePerkKind.HealOnDamage => settings with { EnableHealOnDamage = true },
                LastToDiePerkKind.HealOnKill => settings with { EnableHealOnKill = true },
                LastToDiePerkKind.RateOfFireOnDamage => settings with { EnableRateOfFireMultiplierOnDamage = true },
                LastToDiePerkKind.SoldierInstantReload => settings with { EnableSoldierInstantReload = true },
                LastToDiePerkKind.SpeedOnDamage => settings with { EnableSpeedOnDamage = true },
                LastToDiePerkKind.SpeedOnKill => settings with { EnableSpeedOnKill = true },
                LastToDiePerkKind.PassiveHealthRegeneration => settings with { EnablePassiveHealthRegeneration = true },
                LastToDiePerkKind.InvincibilityOnKill => settings with { EnableInvincibilityOnKill = true },
                LastToDiePerkKind.ProjectileSpeedMultiplier => settings with { EnableProjectileSpeedMultiplier = true },
                LastToDiePerkKind.AirshotDamageMultiplier => settings with { EnableAirshotDamageMultiplier = true },
                _ => settings,
            };
        }

        return settings;
    }

    private void UpdateLastToDiePerkMenu(KeyboardState keyboard, MouseState mouse)
    {
        if (!_lastToDiePerkMenuOpen || _lastToDieRun is null)
        {
            return;
        }

        var layout = GetLastToDiePerkMenuLayout(_lastToDieRun.PendingPerkChoices.Length);
        _lastToDiePerkHoverIndex = GetLastToDiePerkHoverIndex(mouse.Position, layout);

        if (TryGetLastToDiePerkHotkeySelection(keyboard, _lastToDieRun.PendingPerkChoices.Length, out var selectedIndex))
        {
            ChooseLastToDiePerk(selectedIndex);
            return;
        }

        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!clickPressed || _lastToDiePerkHoverIndex < 0)
        {
            return;
        }

        ChooseLastToDiePerk(_lastToDiePerkHoverIndex);
    }

    private void UpdateLastToDieStageClearOverlay(KeyboardState keyboard, MouseState mouse)
    {
        if (!_lastToDieStageClearOverlayOpen)
        {
            return;
        }

        _lastToDieStageClearOverlayTicks += 1;
        var readyForContinue = _lastToDieStageClearOverlayTicks >= LastToDieStageClearContinueDelayTicks;
        var clickPressed = mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton != ButtonState.Pressed;
        if (!readyForContinue)
        {
            return;
        }

        if (clickPressed || IsKeyPressed(keyboard, Keys.Enter) || IsKeyPressed(keyboard, Keys.Space))
        {
            ContinueFromLastToDieStageClearOverlay();
        }
    }

    private bool TryGetLastToDiePerkHotkeySelection(KeyboardState keyboard, int choiceCount, out int selectedIndex)
    {
        selectedIndex = -1;
        Keys[] digitKeys = [Keys.D1, Keys.D2, Keys.D3];
        Keys[] numpadKeys = [Keys.NumPad1, Keys.NumPad2, Keys.NumPad3];
        for (var index = 0; index < Math.Min(choiceCount, digitKeys.Length); index += 1)
        {
            if (IsKeyPressed(keyboard, digitKeys[index]) || IsKeyPressed(keyboard, numpadKeys[index]))
            {
                selectedIndex = index;
                return true;
            }
        }

        return false;
    }

    private void ChooseLastToDiePerk(int selectedIndex)
    {
        if (_lastToDieRun is null
            || selectedIndex < 0
            || selectedIndex >= _lastToDieRun.PendingPerkChoices.Length)
        {
            return;
        }

        var selected = _lastToDieRun.PendingPerkChoices[selectedIndex];
        _lastToDieRun.ChosenPerks.Add(selected.Kind);
        _lastToDieRun.PendingPerkChoices = [];
        _lastToDiePerkMenuOpen = false;
        _lastToDiePerkHoverIndex = -1;
        SuppressPrimaryFireUntilMouseRelease();

        if (_lastToDieRun.AwaitingOpeningPerkSelection)
        {
            _lastToDieRun.AwaitingOpeningPerkSelection = false;
            if (!BeginLastToDieStage(_lastToDieRun.CurrentLevelName))
            {
                ReturnToLastToDieMenu("Failed to start Last To Die.");
            }
            return;
        }

        AdvanceLastToDieStage();
    }

    private LastToDiePerkMenuLayout GetLastToDiePerkMenuLayout(int choiceCount)
    {
        var panelWidth = Math.Min(ViewportWidth - 48, 980);
        var panelHeight = Math.Min(ViewportHeight - 40, 440);
        var panel = new Rectangle(
            (ViewportWidth - panelWidth) / 2,
            (ViewportHeight - panelHeight) / 2,
            panelWidth,
            panelHeight);
        var availableWidth = panel.Width - 64;
        var gap = 18;
        var cardWidth = (availableWidth - (gap * Math.Max(0, choiceCount - 1))) / Math.Max(1, choiceCount);
        var cardHeight = 224;
        var cardTop = panel.Y + 136;
        var cards = new Rectangle[choiceCount];
        var left = panel.X + 32;
        for (var index = 0; index < choiceCount; index += 1)
        {
            cards[index] = new Rectangle(left, cardTop, cardWidth, cardHeight);
            left += cardWidth + gap;
        }

        return new LastToDiePerkMenuLayout(panel, cards);
    }

    private static int GetLastToDiePerkHoverIndex(Point mousePosition, LastToDiePerkMenuLayout layout)
    {
        for (var index = 0; index < layout.CardBounds.Length; index += 1)
        {
            if (layout.CardBounds[index].Contains(mousePosition))
            {
                return index;
            }
        }

        return -1;
    }

    private void DrawLastToDiePerkMenu()
    {
        if (!_lastToDiePerkMenuOpen || _lastToDieRun is null)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * 0.86f);

        var layout = GetLastToDiePerkMenuLayout(_lastToDieRun.PendingPerkChoices.Length);
        _spriteBatch.Draw(_pixel, layout.Panel, new Color(22, 24, 29, 242));
        _spriteBatch.Draw(_pixel, new Rectangle(layout.Panel.X, layout.Panel.Y, layout.Panel.Width, 3), new Color(210, 210, 210));
        _spriteBatch.Draw(_pixel, new Rectangle(layout.Panel.X, layout.Panel.Bottom - 3, layout.Panel.Width, 3), new Color(76, 76, 76));

        DrawBitmapFontText("Perks", new Vector2(layout.Panel.X + 28f, layout.Panel.Y + 24f), Color.White, 1.22f);
        var subtitle = _lastToDieRun.AwaitingOpeningPerkSelection
            ? "Choose 1 perk."
            : "Choose 1 reward for the next stage.";
        DrawBitmapFontText(subtitle, new Vector2(layout.Panel.X + 28f, layout.Panel.Y + 58f), new Color(212, 212, 212), 0.94f);

        for (var index = 0; index < _lastToDieRun.PendingPerkChoices.Length; index += 1)
        {
            var choice = _lastToDieRun.PendingPerkChoices[index];
            var bounds = layout.CardBounds[index];
            var isHovered = index == _lastToDiePerkHoverIndex;
            var backColor = isHovered ? new Color(70, 38, 38, 240) : new Color(34, 37, 43, 232);
            var accentColor = isHovered ? new Color(210, 78, 78) : new Color(118, 126, 140);
            _spriteBatch.Draw(_pixel, bounds, backColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 3), accentColor);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 3, bounds.Width, 3), new Color(14, 16, 19));

            DrawBitmapFontText($"{index + 1}", new Vector2(bounds.X + 14f, bounds.Y + 12f), new Color(236, 224, 198), 1f);
            DrawBitmapFontText(choice.Label, new Vector2(bounds.X + 14f, bounds.Y + 44f), Color.White, 0.98f);

            var descriptionLines = WrapMenuParagraph(choice.Description, 28);
            var lineY = bounds.Y + 84f;
            for (var lineIndex = 0; lineIndex < descriptionLines.Length; lineIndex += 1)
            {
                DrawBitmapFontText(descriptionLines[lineIndex], new Vector2(bounds.X + 14f, lineY), new Color(214, 214, 214), 0.88f);
                lineY += 20f;
            }
        }
    }

    private void DrawLastToDieStageClearOverlay()
    {
        if (!_lastToDieStageClearOverlayOpen || _lastToDieRun is null)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var alpha = Math.Clamp(_lastToDieStageClearOverlayTicks / (float)LastToDieStageClearFadeTicks, 0f, 1f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * (0.8f * alpha));

        DrawHudTextCentered(
            "YOU SURVIVED",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.34f),
            new Color(240, 232, 208) * alpha,
            2.7f);

        var clearedText = $"Stage {_lastToDieRun.StageNumber} cleared.";
        DrawHudTextCentered(
            clearedText,
            new Vector2(viewportWidth / 2f, viewportHeight * 0.44f),
            new Color(216, 206, 176) * alpha,
            1.3f);

        if (_lastToDieStageClearOverlayTicks < LastToDieStageClearContinueDelayTicks)
        {
            return;
        }

        DrawHudTextCentered(
            "Press Enter or click to choose your next perk.",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.55f),
            new Color(214, 214, 214) * alpha,
            1f);
    }

    private void TriggerLastToDieFailure()
    {
        OpenLastToDieFailureOverlay();
    }

    private void UpdateLastToDieFailureOverlay(KeyboardState keyboard, MouseState mouse)
    {
        _ = mouse;
        if (!_lastToDieFailureOverlayOpen)
        {
            return;
        }

        _lastToDieFailureOverlayTicks += 1;
        var readyForContinue = _lastToDieFailureOverlayTicks >= LastToDieFailureContinueDelayTicks;
        if (!readyForContinue)
        {
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Enter))
        {
            ReturnToLastToDieMenu();
        }
    }

    private void DrawLastToDieFailureOverlay()
    {
        if (!_lastToDieFailureOverlayOpen)
        {
            return;
        }

        var viewportWidth = ViewportWidth;
        var viewportHeight = ViewportHeight;
        var alpha = Math.Clamp(_lastToDieFailureOverlayTicks / (float)LastToDieFailureFadeTicks, 0f, 1f);
        var centerFadeAlpha = MathF.Min(0.9f, alpha * 1.1f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, viewportHeight), Color.Black * centerFadeAlpha);

        var edgeAlpha = MathF.Min(0.56f, alpha * 0.7f);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewportWidth, 96), new Color(120, 0, 0) * edgeAlpha);
        _spriteBatch.Draw(_pixel, new Rectangle(0, viewportHeight - 96, viewportWidth, 96), new Color(120, 0, 0) * edgeAlpha);
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, 96, viewportHeight), new Color(120, 0, 0) * edgeAlpha);
        _spriteBatch.Draw(_pixel, new Rectangle(viewportWidth - 96, 0, 96, viewportHeight), new Color(120, 0, 0) * edgeAlpha);

        if (_lastToDieDeathFocus is not null && alpha >= 0.25f)
        {
            DrawLastToDieFailureCorpse(viewportWidth, viewportHeight, alpha);
        }

        if (_lastToDieRun is null || alpha < 0.2f)
        {
            return;
        }

        DrawHudTextCentered(
            "YOU FAILED YOUR TEAM",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.18f),
            new Color(230, 214, 214) * alpha,
            3f);

        DrawHudTextCentered(
            $"Kills: {_lastToDieRun.TotalKills}",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.74f),
            new Color(220, 214, 214) * alpha,
            1.15f);
        DrawHudTextCentered(
            $"Damage: {_lastToDieRun.TotalDamageDealt}",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.80f),
            new Color(220, 214, 214) * alpha,
            1.15f);
        DrawHudTextCentered(
            $"Matches clutched: {_lastToDieRun.LevelsCompleted}",
            new Vector2(viewportWidth / 2f, viewportHeight * 0.86f),
            new Color(220, 214, 214) * alpha,
            1.15f);

        if (_lastToDieFailureOverlayTicks >= LastToDieFailureContinueDelayTicks)
        {
            DrawHudTextCentered(
                "Press Enter to continue.",
                new Vector2(viewportWidth / 2f, viewportHeight * 0.93f),
                new Color(214, 214, 214) * alpha,
                1f);
        }
    }

    private void UpdateLastToDieRunStats()
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null || _world.LocalPlayerAwaitingJoin)
        {
            return;
        }

        var currentKills = Math.Max(0, _world.LocalPlayer.Kills);
        if (currentKills < _lastToDieRun.ObservedStageKills)
        {
            _lastToDieRun.ObservedStageKills = currentKills;
            return;
        }

        if (currentKills > _lastToDieRun.ObservedStageKills)
        {
            _lastToDieRun.TotalKills += currentKills - _lastToDieRun.ObservedStageKills;
            _lastToDieRun.ObservedStageKills = currentKills;
        }
    }

    private void RegisterLastToDieLocalDamageDealt(int amount)
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null || amount <= 0)
        {
            return;
        }

        _lastToDieRun.TotalDamageDealt += amount;
    }

    private void DrawLastToDieHud()
    {
        if (!IsLastToDieSessionActive || _lastToDieRun is null)
        {
            return;
        }

        var timerText = FormatHudTimerText(_lastToDieRun.StageRemainingTicks);
        DrawTimerFontTextRightAligned(timerText, new Vector2(ViewportWidth - 18f, 18f), Color.White, 1f);

        var stageLabel = $"Stage {_lastToDieRun.StageNumber}/{LastToDieStageCount}";
        var enemiesLabel = $"{_lastToDieRun.EnemyBotCount} Enemies";
        var stageX = ViewportWidth - MeasureBitmapFontWidth(stageLabel, 0.92f) - 18f;
        var enemiesX = ViewportWidth - MeasureBitmapFontWidth(enemiesLabel, 0.92f) - 18f;
        DrawBitmapFontText(stageLabel, new Vector2(stageX, 44f), new Color(232, 232, 232), 0.92f);
        DrawBitmapFontText(enemiesLabel, new Vector2(enemiesX, 64f), new Color(210, 196, 160), 0.92f);

        if (_lastToDieRun.StageIntroTicksRemaining > 0)
        {
            var introDurationTicks = GetLastToDieStageIntroDurationTicks();
            var introProgress = 1f - (_lastToDieRun.StageIntroTicksRemaining / (float)introDurationTicks);
            var fadeAlpha = introProgress < 0.32f
                ? Math.Clamp(introProgress / 0.32f, 0f, 1f)
                : Math.Clamp(1f - ((introProgress - 0.32f) / 0.68f), 0f, 1f);
            var introColor = new Color(241, 232, 203) * (fadeAlpha * 0.96f);
            DrawHudTextCentered("SURVIVE!", new Vector2(ViewportWidth / 2f, ViewportHeight * 0.2f), introColor, 2.4f);
        }
    }
}
