#nullable enable

using OpenGarrison.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void HandleGameplayMapTransitionIfNeeded()
    {
        var currentLevelName = _world.Level.Name;
        var currentMapAreaIndex = _world.Level.MapAreaIndex;
        if (_observedGameplayMapAreaIndex < 0 || string.IsNullOrWhiteSpace(_observedGameplayLevelName))
        {
            _observedGameplayLevelName = currentLevelName;
            _observedGameplayMapAreaIndex = currentMapAreaIndex;
            return;
        }

        if (string.Equals(_observedGameplayLevelName, currentLevelName, StringComparison.OrdinalIgnoreCase)
            && _observedGameplayMapAreaIndex == currentMapAreaIndex)
        {
            return;
        }

        StopLocalRapidFireWeaponAudio();
        StopIngameMusic();
        ResetTransientPresentationEffects();
        ResetProcessedNetworkEventHistory();
        _wasDeathCamActive = false;
        _wasMatchEnded = false;
        if (_navEditorEnabled)
        {
            DisableNavEditor("nav editor closed after map change");
        }
        _observedGameplayLevelName = currentLevelName;
        _observedGameplayMapAreaIndex = currentMapAreaIndex;
    }

    private bool IsGameplayBindingKey(Keys key)
    {
        return _inputBindings.MoveUp == key
            || _inputBindings.MoveDown == key
            || _inputBindings.MoveLeft == key
            || _inputBindings.MoveRight == key
            || _inputBindings.Taunt == key
            || _inputBindings.CallMedic == key
            || _inputBindings.FireSecondaryWeapon == key
            || _inputBindings.InteractWeapon == key
            || _inputBindings.ChangeTeam == key
            || _inputBindings.ChangeClass == key
            || _inputBindings.ShowScoreboard == key
            || _inputBindings.ToggleConsole == key
            || _inputBindings.OpenBubbleMenuZ == key
            || _inputBindings.OpenBubbleMenuX == key
            || _inputBindings.OpenBubbleMenuC == key;
    }

    private static bool IsChatShortcutHeld(KeyboardState keyboard)
    {
        return keyboard.IsKeyDown(Keys.Y)
            || keyboard.IsKeyDown(Keys.U);
    }

    private bool IsChatShortcutPressed(KeyboardState keyboard, Keys key)
    {
        return IsKeyPressed(keyboard, key);
    }

    private void UpdateGameplayScreenState(KeyboardState keyboard)
    {
        var escapePressed = keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape);
        var changeTeamPressed = IsKeyPressed(keyboard, _inputBindings.ChangeTeam);
        var changeClassPressed = IsKeyPressed(keyboard, _inputBindings.ChangeClass);
        if (_chatSubmitAwaitingOpenKeyRelease
            && !IsChatShortcutHeld(keyboard))
        {
            _chatSubmitAwaitingOpenKeyRelease = false;
        }

        var canOpenChat = !_chatSubmitAwaitingOpenKeyRelease
            && !IsGameplayMenuOpen();
        var openPublicChatPressed = canOpenChat && IsChatShortcutPressed(keyboard, Keys.Y);
        var openTeamChatPressed = canOpenChat && IsChatShortcutPressed(keyboard, Keys.U);
        var pausePressed = GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || escapePressed;

        if (!_passwordPromptOpen && !_consoleOpen && !_teamSelectOpen && !_classSelectOpen && !_chatOpen
            && (openPublicChatPressed || openTeamChatPressed))
        {
            OpenChat(teamOnly: openTeamChatPressed);
            return;
        }

        if (_chatOpen && escapePressed)
        {
            ResetChatInputState();
            return;
        }

        UpdateSpectatorTrackingHotkeys(keyboard);

        if (!_passwordPromptOpen && !_optionsMenuOpen && !_pluginOptionsMenuOpen && !_controlsMenuOpen && !_clientPowersOpen && !_inGameMenuOpen)
        {
            var canToggleSelectionMenu = !_consoleOpen
                && !_chatOpen
                && !IsLastToDieSessionActive
                && !_world.MatchState.IsEnded
                && (!_killCamEnabled || _world.LocalDeathCam is null);
            if (canToggleSelectionMenu && changeTeamPressed)
            {
                _teamSelectOpen = !_teamSelectOpen;
                if (_teamSelectOpen)
                {
                    _classSelectOpen = false;
                }
            }
            else if (canToggleSelectionMenu && !_world.LocalPlayerAwaitingJoin && changeClassPressed)
            {
                _classSelectOpen = !_classSelectOpen;
                if (_classSelectOpen)
                {
                    _teamSelectOpen = false;
                }
            }
        }

        if (_consoleOpen && escapePressed)
        {
            _consoleOpen = false;
        }
        else if (_chatOpen && escapePressed)
        {
            ResetChatInputState();
        }
        else if (_teamSelectOpen && escapePressed && !_world.LocalPlayerAwaitingJoin)
        {
            _teamSelectOpen = false;
        }
        else if (_classSelectOpen && escapePressed)
        {
            _classSelectOpen = false;
        }
        else if (!_consoleOpen && !_teamSelectOpen && !_classSelectOpen && !_optionsMenuOpen && !_pluginOptionsMenuOpen && !_controlsMenuOpen && !_clientPowersOpen && !_inGameMenuOpen && pausePressed)
        {
            OpenInGameMenu();
        }

        if (_world.MatchState.IsEnded || (_killCamEnabled && _world.LocalDeathCam is not null))
        {
            _teamSelectOpen = false;
            _classSelectOpen = false;
        }

        if (_passwordPromptOpen)
        {
            _teamSelectOpen = false;
            _classSelectOpen = false;
        }
    }

    private (PlayerInputSnapshot GameplayInput, PlayerInputSnapshot NetworkInput) BuildGameplayInputs(KeyboardState keyboard, MouseState mouse, Vector2 cameraPosition)
    {
        if (_suppressPrimaryFireUntilMouseRelease
            && mouse.LeftButton != ButtonState.Pressed)
        {
            _suppressPrimaryFireUntilMouseRelease = false;
        }

        var fullInput = KeyboardInputMapper.BuildGameplaySnapshot(_inputBindings, keyboard, mouse, cameraPosition.X, cameraPosition.Y);
        if (_bubbleMenuKind != BubbleMenuKind.None && !_bubbleMenuClosing)
        {
            fullInput = fullInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                FireSecondaryWeapon = false,
                InteractWeapon = false,
            };
        }

        var blockedInput = ShouldPreserveAimWhileBlocked()
            ? BuildAimOnlyGameplaySnapshot(fullInput)
            : default;
        var gameplayInput = _networkClient.IsConnected
            ? default
            : IsGameplayInputBlocked()
                ? blockedInput
                : fullInput;
        var networkInput = IsGameplayInputBlocked()
            ? blockedInput
            : fullInput;

        if (_suppressPrimaryFireUntilMouseRelease)
        {
            gameplayInput = gameplayInput with
            {
                FirePrimary = false,
                FireSecondaryWeapon = false,
            };
            networkInput = networkInput with
            {
                FirePrimary = false,
                FireSecondaryWeapon = false,
            };
        }

        if (_world.IsPlayerHumiliated(_world.LocalPlayer))
        {
            gameplayInput = gameplayInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                FireSecondaryWeapon = false,
                InteractWeapon = false,
                BuildSentry = false,
                DestroySentry = false,
            };
            networkInput = networkInput with
            {
                FirePrimary = false,
                FireSecondary = false,
                FireSecondaryWeapon = false,
                InteractWeapon = false,
                BuildSentry = false,
                DestroySentry = false,
            };
        }

        UpdateBuildMenuState(keyboard, mouse);

        return (gameplayInput, networkInput);
    }

    private void SuppressPrimaryFireUntilMouseRelease()
    {
        _suppressPrimaryFireUntilMouseRelease = true;
    }

    private void UpdateSpectatorTrackingHotkeys(KeyboardState keyboard)
    {
        if (!_networkClient.IsSpectator
            || _consoleOpen
            || _chatOpen
            || _passwordPromptOpen
            || _teamSelectOpen
            || _classSelectOpen
            || IsGameplayMenuOpen())
        {
            return;
        }

        if (IsKeyPressed(keyboard, Keys.Add) || IsKeyPressed(keyboard, Keys.OemPlus))
        {
            CycleSpectatorTracking(forward: true);
        }
        else if (IsKeyPressed(keyboard, Keys.Subtract) || IsKeyPressed(keyboard, Keys.OemMinus))
        {
            CycleSpectatorTracking(forward: false);
        }
    }

    private bool ShouldPreserveAimWhileBlocked()
    {
        return _chatOpen
            && !_consoleOpen
            && !_passwordPromptOpen
            && !_teamSelectOpen
            && !_classSelectOpen
            && !IsGameplayMenuOpen();
    }

    private static PlayerInputSnapshot BuildAimOnlyGameplaySnapshot(PlayerInputSnapshot input)
    {
        return input with
        {
            Left = false,
            Right = false,
            Up = false,
            Down = false,
            BuildSentry = false,
            DestroySentry = false,
            Taunt = false,
            FirePrimary = false,
            FireSecondary = false,
            FireSecondaryWeapon = false,
            InteractWeapon = false,
            DebugKill = false,
            DropIntel = false,
        };
    }

    private void AdvanceGameplaySimulation(GameTime gameTime, PlayerInputSnapshot networkInput)
    {
        if (_networkClient.IsConnected)
        {
            AdvanceNetworkInputLane(networkInput);
        }
        else
        {
            if (ShouldSuspendOfflineGameplaySimulation())
            {
                return;
            }

            BeginBotDiagnosticsFrame(gameTime);
            _simulator.Step(
                gameTime.ElapsedGameTime.TotalSeconds,
                OnPracticeSimulationBeforeTick,
                OnPracticeSimulationAfterTick);
            FinalizeBotDiagnosticsFrame();
        }
    }

    private void OnPracticeSimulationBeforeTick()
    {
        UpdatePracticeBots();
        OnNavEditorTraversalCaptureBeforeTick();
    }

    private void OnPracticeSimulationAfterTick()
    {
        OnNavEditorTraversalCaptureAfterTick();
        AdvanceLastToDieSimulationTick();
    }

    private void UpdateGameplayPresentation(GameTime gameTime, KeyboardState keyboard, MouseState mouse, int clientTicks)
    {
        var interpolationStartTimestamp = _networkDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L;
        UpdateInterpolatedWorldState();
        if (_networkDiagnosticsEnabled)
        {
            RecordInterpolationDuration(GetDiagnosticsElapsedMilliseconds(interpolationStartTimestamp));
        }

        HandleGameplayMapTransitionIfNeeded();
        UpdateLocalSentryNotice();
        UpdateIntelNotice();
        UpdateLocalPredictedRenderPosition();
        foreach (var player in EnumerateRenderablePlayers())
        {
            UpdatePlayerRenderState(player);
        }

        RemoveStalePlayerRenderState();
        AdvanceGameplayClientTicks(clientTicks);
        PlayPendingVisualEvents();
        PlayPendingSoundEvents();
        DispatchPendingDamageEventsToPlugins();
        QueuePendingExperimentalHealingHudIndicators();
        UpdateLocalRapidFireWeaponAudio();
        PlayDeathCamSoundIfNeeded();
        PlayRoundEndSoundIfNeeded();
        PlayKillFeedAnnouncementSounds();
        EnsureIngameMusicPlaying();
        UpdateLastToDieSession(clientTicks);
        UpdateLastToDieCombatFeedbackPresentation();
        UpdateTeamSelect(keyboard, mouse);
        UpdateClassSelect(mouse);
    }

    private void UpdateGameplayWindowState()
    {
        var wantsMouseVisible = _passwordPromptOpen
            || _quitPromptOpen
            || _teamSelectOpen
            || _teamSelectAlpha > 0.02f
            || _classSelectOpen
            || _classSelectAlpha > 0.02f
            || ShouldBlockGameplayForNavEditor()
            || _practiceSetupOpen
            || IsLastToDieStageClearOverlayActive()
            || _lastToDiePerkMenuOpen
            || IsLastToDieFailureOverlayActive()
            || _inGameMenuOpen
            || _optionsMenuOpen
            || _pluginOptionsMenuOpen
            || _controlsMenuOpen;
        IsMouseVisible = wantsMouseVisible && !ShouldUseSoftwareMenuCursor();

        var sourceTag = _world.Level.ImportedFromSource ? "src" : "fallback";
        var lifeTag = _world.LocalPlayerAwaitingJoin ? "joining" : _world.LocalPlayer.IsAlive ? "alive" : $"respawn:{_world.LocalPlayerRespawnTicks}";
        var remoteLifeTag = _networkClient.IsConnected
            ? $"remotes:{_world.RemoteSnapshotPlayers.Count}"
            : _world.EnemyPlayerEnabled
                ? "offline:npc:on"
                : "offline:npc:off";
        var carryingIntelTag = _world.LocalPlayer.IsCarryingIntel ? "yes" : "no";
        var heavyTag = GetPlayerIsHeavyEating(_world.LocalPlayer)
            ? $" eat:{GetPlayerHeavyEatTicksRemaining(_world.LocalPlayer)}"
            : string.Empty;
        var sniperTag = GetPlayerIsSniperScoped(_world.LocalPlayer)
            ? $" scope:{GetPlayerSniperRifleDamage(_world.LocalPlayer)}"
            : string.Empty;
        var consoleTag = _consoleOpen ? " console:open" : string.Empty;
        var netTag = _networkClient.IsConnected ? $" net:{_networkClient.ServerDescription}" : string.Empty;
        Window.Title = $"OpenGarrison.Client - {_world.LocalPlayer.DisplayName} ({_world.LocalPlayer.ClassName}) - {_world.Level.Name} [{sourceTag}] - {lifeTag} - HP {_world.LocalPlayer.Health}/{_world.LocalPlayer.MaxHealth} - Ammo {_world.LocalPlayer.CurrentShells}/{_world.LocalPlayer.MaxShells} - {remoteLifeTag} - Caps {_world.RedCaps}:{_world.BlueCaps} - Carrying {carryingIntelTag} - BlueIntel {(GetIntelStateLabel(_world.BlueIntel))} - Frame {_world.Frame} - Pos ({_world.LocalPlayer.X:F1}, {_world.LocalPlayer.Y:F1}) - AirJumps {_world.LocalPlayer.RemainingAirJumps}{heavyTag}{sniperTag}{consoleTag}{netTag}";
    }

    private void FinalizeGameplayFrame(KeyboardState keyboard, MouseState mouse)
    {
        _previousKeyboard = keyboard;
        _previousMouse = mouse;
        _wasLocalPlayerAlive = _world.LocalPlayer.IsAlive;
        _wasDeathCamActive = _killCamEnabled
            && !_world.LocalPlayer.IsAlive
            && _world.LocalDeathCam is not null
            && GetDeathCamElapsedTicks(_world.LocalDeathCam) >= DeathCamFocusDelayTicks;
        _wasMatchEnded = _world.MatchState.IsEnded;
    }
}
