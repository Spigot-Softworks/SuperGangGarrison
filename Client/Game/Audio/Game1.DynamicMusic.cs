#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using System;
using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int DynamicMusicCombatLingerTicks = SimulationConfig.DefaultTicksPerSecond * 10;
    private const int DynamicMusicCombatParticipantLingerTicks = SimulationConfig.DefaultTicksPerSecond * 5;
    private const float DynamicMusicFadeInPerSecond = 1.45f;
    private const float DynamicMusicFadeOutPerSecond = 0.95f;
    private const float DynamicMusicCombatRiserDelaySeconds = 0.83f;
    private const float DynamicMusicCombatLowHealthThreshold = 0.35f;
    private const float DynamicMusicCombatPresenceDistance = 560f;
    private const float DynamicMusicCombatPresenceDistanceSquared = DynamicMusicCombatPresenceDistance * DynamicMusicCombatPresenceDistance;
    private const float DynamicMusicIntelVolumeScale = 0.70f;
    private const float DynamicMusicUberVolumeScale = 0.70f;
    private const float DynamicMusicNearbyUberDistance = 420f;
    private const float DynamicMusicNearbyUberDistanceSquared = DynamicMusicNearbyUberDistance * DynamicMusicNearbyUberDistance;

    private enum DynamicMusicEventState
    {
        Normal,
        Combat,
        Intel,
        Uber,
    }

    private enum DynamicCombatMusicStage
    {
        None,
        Light,
        Medium,
        Hard,
    }

    private enum DynamicCombatMusicLeadStem
    {
        None,
        Drum,
        Body,
        Lead,
    }

    private bool _dynamicMusicEnabled = true;
    private bool _dynamicMusicLoadAttempted;
    private SoundEffect? _dynamicCombatDrumMusic;
    private SoundEffectInstance? _dynamicCombatDrumMusicInstance;
    private SoundEffect? _dynamicCombatBodyMusic;
    private SoundEffectInstance? _dynamicCombatBodyMusicInstance;
    private SoundEffect? _dynamicCombatBassMusic;
    private SoundEffectInstance? _dynamicCombatBassMusicInstance; 
	private SoundEffect? _dynamicCombatLeadMusic;
    private SoundEffectInstance? _dynamicCombatLeadMusicInstance;
    private SoundEffect? _dynamicCombatRiser;
    private SoundEffectInstance? _dynamicCombatRiserInstance;
    private SoundEffect? _dynamicIntelMusic;
    private SoundEffectInstance? _dynamicIntelMusicInstance;
    private SoundEffect? _dynamicUberMusic;
    private SoundEffectInstance? _dynamicUberMusicInstance;
    private readonly Dictionary<int, int> _dynamicCombatParticipantTicks = new();
    private int _dynamicMusicCombatTicksRemaining;
    private DynamicCombatMusicStage _dynamicCombatMusicStage;
    private DynamicCombatMusicStage _dynamicCombatRiserHoldStage;
    private DynamicCombatMusicLeadStem _dynamicCombatLeadStem;
    private bool _dynamicCombatRiserPending;
    private bool _dynamicCombatDrumsLocked;
    private float _dynamicCombatRiserDelaySecondsRemaining;
    private DynamicMusicEventState _dynamicMusicTargetState = DynamicMusicEventState.Normal;
    private float _dynamicNormalMusicFade = 1f;
    private float _dynamicCombatMusicFade;
    private float _dynamicCombatDrumFade;
    private float _dynamicCombatBodyFade;
    private float _dynamicCombatBassFade;
    private float _dynamicCombatLeadFade;
    private float _dynamicIntelMusicFade;
    private float _dynamicUberMusicFade;

    private void ObserveDynamicMusicDamageEvent(WorldDamageEvent damageEvent)
    {
        if (ShouldTriggerDynamicCombatMusic(damageEvent.Amount, damageEvent.AttackerPlayerId, damageEvent.TargetKind, damageEvent.TargetEntityId))
        {
            StartOrExtendDynamicCombatMusic(damageEvent.AttackerPlayerId, damageEvent.TargetEntityId);
        }
    }

    private void ObserveDynamicMusicDamageEvent(SnapshotDamageEvent damageEvent)
    {
        if (ShouldTriggerDynamicCombatMusic(damageEvent.Amount, damageEvent.AttackerPlayerId, (DamageTargetKind)damageEvent.TargetKind, damageEvent.TargetEntityId))
        {
            StartOrExtendDynamicCombatMusic(damageEvent.AttackerPlayerId, damageEvent.TargetEntityId);
        }
    }

    private void StartOrExtendDynamicCombatMusic(int attackerPlayerId, int targetEntityId)
    {
        if (_dynamicMusicCombatTicksRemaining <= 0)
        {
            _dynamicCombatLeadStem = Random.Shared.Next(2) == 0
                ? DynamicCombatMusicLeadStem.Drum
                : DynamicCombatMusicLeadStem.Body;
            _dynamicCombatDrumsLocked = false;
        }

        _dynamicMusicCombatTicksRemaining = DynamicMusicCombatLingerTicks;
        TrackDynamicCombatParticipant(GetResolvedLocalPlayerId());
        TrackDynamicCombatParticipant(attackerPlayerId);
        TrackDynamicCombatParticipant(targetEntityId);
    }

    private bool ShouldTriggerDynamicCombatMusic(int amount, int attackerPlayerId, DamageTargetKind targetKind, int targetEntityId)
    {
        if (!_dynamicMusicEnabled
            || amount <= 0
            || targetKind != DamageTargetKind.Player
            || attackerPlayerId <= 0
            || targetEntityId <= 0
            || attackerPlayerId == targetEntityId
            || IsLocalSpectatorPresentationActive())
        {
            return false;
        }

        var localPlayerId = GetResolvedLocalPlayerId();
        var localPlayerInvolved = attackerPlayerId == localPlayerId || targetEntityId == localPlayerId;
        if (!localPlayerInvolved)
        {
            return false;
        }

        var attacker = FindPlayerById(attackerPlayerId);
        var target = FindPlayerById(targetEntityId);
        if (attacker is not null && target is not null && attacker.Team == target.Team)
        {
            return false;
        }

        var localTeam = _world.LocalPlayer.Team;
        if (attackerPlayerId == localPlayerId && target is not null && target.Team == localTeam)
        {
            return false;
        }

        if (targetEntityId == localPlayerId && attacker is not null && attacker.Team == localTeam)
        {
            return false;
        }

        return true;
    }

    private void AdvanceDynamicCombatMusicTimers(int clientTicks)
    {
        if (clientTicks <= 0)
        {
            return;
        }

        if (_dynamicMusicCombatTicksRemaining > 0)
        {
            _dynamicMusicCombatTicksRemaining = Math.Max(0, _dynamicMusicCombatTicksRemaining - clientTicks);
            if (_dynamicMusicCombatTicksRemaining == 0)
            {
                EndDynamicCombatMusicSession();
            }
        }

        if (_dynamicCombatParticipantTicks.Count == 0)
        {
            return;
        }

        var playerIds = new List<int>(_dynamicCombatParticipantTicks.Count);
        foreach (var entry in _dynamicCombatParticipantTicks)
        {
            playerIds.Add(entry.Key);
        }

        for (var index = 0; index < playerIds.Count; index += 1)
        {
            var playerId = playerIds[index];
            if (!_dynamicCombatParticipantTicks.TryGetValue(playerId, out var currentTicks))
            {
                continue;
            }

            var remainingTicks = Math.Max(0, currentTicks - clientTicks);
            if (remainingTicks <= 0)
            {
                _dynamicCombatParticipantTicks.Remove(playerId);
                continue;
            }

            _dynamicCombatParticipantTicks[playerId] = remainingTicks;
        }
    }

    private void RefreshDynamicCombatMusicPresence()
    {
        if (_dynamicMusicCombatTicksRemaining <= 0
            || IsLocalSpectatorPresentationActive()
            || _world.LocalPlayerAwaitingJoin
            || !_world.LocalPlayer.IsAlive)
        {
            return;
        }

        var localPlayer = _world.LocalPlayer;
        var localPlayerId = GetResolvedLocalPlayerId();
        TrackDynamicCombatParticipant(localPlayerId);

        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || ReferenceEquals(player, localPlayer))
            {
                continue;
            }

            if (player.Team != localPlayer.Team && IsDynamicCombatPlayerNearLocalPlayer(player))
            {
                TrackDynamicCombatParticipant(player.Id);
            }

            TrackDynamicCombatMedicContribution(player);
        }
    }

    private bool IsDynamicCombatPlayerNearLocalPlayer(PlayerEntity player)
    {
        var localPlayer = _world.LocalPlayer;
        var deltaX = player.X - localPlayer.X;
        var deltaY = player.Y - localPlayer.Y;
        return (deltaX * deltaX) + (deltaY * deltaY) <= DynamicMusicCombatPresenceDistanceSquared;
    }

    private void TrackDynamicCombatMedicContribution(PlayerEntity player)
    {
        if (!player.IsMedicHealing || !player.MedicHealTargetId.HasValue)
        {
            return;
        }

        var healingTargetId = player.MedicHealTargetId.Value;
        var localPlayerId = GetResolvedLocalPlayerId();
        if (player.Id == localPlayerId
            || healingTargetId == localPlayerId
            || _dynamicCombatParticipantTicks.ContainsKey(player.Id)
            || _dynamicCombatParticipantTicks.ContainsKey(healingTargetId))
        {
            TrackDynamicCombatParticipant(player.Id);
            TrackDynamicCombatParticipant(healingTargetId);
        }
    }

    private void TrackDynamicCombatParticipant(int playerId)
    {
        if (playerId <= 0)
        {
            return;
        }

        _dynamicCombatParticipantTicks[playerId] = DynamicMusicCombatParticipantLingerTicks;
    }

    private DynamicCombatMusicStage ResolveDynamicCombatMusicStage()
    {
        if (_dynamicMusicCombatTicksRemaining <= 0
            || IsLocalSpectatorPresentationActive()
            || _world.LocalPlayerAwaitingJoin
            || !_world.LocalPlayer.IsAlive)
        {
            return DynamicCombatMusicStage.None;
        }

        var participantCount = CountActiveDynamicCombatParticipants();
        if (participantCount >= 4)
        {
            return DynamicCombatMusicStage.Hard;
        }

        if (IsLocalPlayerLowHealthForDynamicCombatMusic() || participantCount >= 3)
        {
            return DynamicCombatMusicStage.Medium;
        }

        return DynamicCombatMusicStage.Light;
    }

    private int CountActiveDynamicCombatParticipants()
    {
        var count = 0;
        foreach (var entry in _dynamicCombatParticipantTicks)
        {
            if (entry.Value <= 0)
            {
                continue;
            }

            var player = FindPlayerById(entry.Key);
            if (player is { IsAlive: true })
            {
                count += 1;
            }
        }

        return count;
    }

    private bool IsLocalPlayerLowHealthForDynamicCombatMusic()
    {
        var localPlayer = _world.LocalPlayer;
        return localPlayer.MaxHealth > 0
            && localPlayer.Health / (float)localPlayer.MaxHealth <= DynamicMusicCombatLowHealthThreshold;
    }

    private void EndDynamicCombatMusicSession()
    {
        _dynamicCombatParticipantTicks.Clear();
        _dynamicCombatMusicStage = DynamicCombatMusicStage.None;
        _dynamicCombatRiserHoldStage = DynamicCombatMusicStage.None;
        _dynamicCombatLeadStem = DynamicCombatMusicLeadStem.None;
        _dynamicCombatRiserPending = false;
        _dynamicCombatDrumsLocked = false;
        _dynamicCombatRiserDelaySecondsRemaining = 0f;
        StopDynamicMusicInstance(_dynamicCombatRiserInstance);
    }

    private void UpdateDynamicMusic(GameTime gameTime, int clientTicks)
    {
        AdvanceDynamicCombatMusicTimers(clientTicks);
        RefreshDynamicCombatMusicPresence();

        if (!CanUseDynamicMusic())
        {
            ResetDynamicMusicPlayback();
            return;
        }

        EnsureDynamicMusicLoaded();
        var requestedState = GetDynamicMusicTargetState();
        var requestedCombatStage = requestedState == DynamicMusicEventState.Combat
            ? ResolveDynamicCombatMusicStage()
            : DynamicCombatMusicStage.None;
        var targetState = ResolveAvailableDynamicMusicState(requestedState, requestedCombatStage);
        var previousCombatStage = _dynamicCombatMusicStage;
        var targetCombatStage = targetState == DynamicMusicEventState.Combat
            ? requestedCombatStage
            : DynamicCombatMusicStage.None;
        _dynamicMusicTargetState = targetState;
        UpdateDynamicCombatRiserForStage(previousCombatStage, targetCombatStage);
        _dynamicCombatMusicStage = targetCombatStage;

        if (_gameplayAudioMusicController.CanStartMusicPlayback())
        {
            EnsureDynamicMusicPlaybackStarted(targetState);
        }

        AdvanceDynamicMusicFades(targetState, gameTime);
        StopSilentDynamicMusicTracks(targetState);
        ApplyAudioVolumeState();
    }

    private bool CanUseDynamicMusic()
    {
        return _dynamicMusicEnabled
            && _audioAvailable
            && AllowsIngameMusic()
            && !_world.MatchState.IsEnded
            && !IsLastToDieSessionActive
            && !IsLastToDieDeathFocusPresentationActive();
    }

    private DynamicMusicEventState GetDynamicMusicTargetState()
    {
        if (IsNearbyUberMusicEventActive())
        {
            return DynamicMusicEventState.Uber;
        }

        if (_world.RedIntel.IsCarried || _world.BlueIntel.IsCarried)
        if ((_world.RedIntel.IsCarried || _world.BlueIntel.IsCarried || _world.RedIntel.IsDropped || _world.BlueIntel.IsDropped) 
			&& 
			(_world.RedCaps == 0 && _world.BlueCaps == 0))
        {
            return DynamicMusicEventState.Intel;
        }

        if (_dynamicMusicCombatTicksRemaining > 0)
        {
            return DynamicMusicEventState.Combat;
        }

        return DynamicMusicEventState.Normal;
    }

    private DynamicMusicEventState ResolveAvailableDynamicMusicState(DynamicMusicEventState state, DynamicCombatMusicStage combatStage)
    {
        return state switch
        {
            DynamicMusicEventState.Combat when combatStage == DynamicCombatMusicStage.None || !HasDynamicCombatMusicInstances() => DynamicMusicEventState.Normal,
            DynamicMusicEventState.Intel when _dynamicIntelMusicInstance is null => DynamicMusicEventState.Normal,
            DynamicMusicEventState.Uber when _dynamicUberMusicInstance is null => DynamicMusicEventState.Normal,
            _ => state,
        };
    }

    private void UpdateDynamicCombatRiserForStage(DynamicCombatMusicStage previousStage, DynamicCombatMusicStage targetStage)
    {
        if (targetStage != DynamicCombatMusicStage.Hard)
        {
            if (_dynamicCombatRiserPending || _dynamicCombatRiserDelaySecondsRemaining > 0f)
            {
                StopDynamicMusicInstance(_dynamicCombatRiserInstance);
            }

            _dynamicCombatRiserPending = false;
            _dynamicCombatRiserDelaySecondsRemaining = 0f;
            _dynamicCombatRiserHoldStage = DynamicCombatMusicStage.None;
            return;
        }

        if (previousStage == DynamicCombatMusicStage.Hard
            || previousStage == DynamicCombatMusicStage.None
            || _dynamicCombatRiserPending
            || _dynamicCombatRiserDelaySecondsRemaining > 0f)
        {
            return;
        }

        _dynamicCombatRiserPending = true;
        _dynamicCombatRiserDelaySecondsRemaining = 0f;
        _dynamicCombatRiserHoldStage = previousStage;
    }

    private bool HasDynamicCombatMusicInstances()
    {
        return _dynamicCombatDrumMusicInstance is not null
            && _dynamicCombatBodyMusicInstance is not null
            && _dynamicCombatBassMusicInstance is not null;
    }

    private bool IsNearbyUberMusicEventActive()
    {
        var listenerPosition = GetWorldSoundListenerPosition();
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || !player.IsUbered)
            {
                continue;
            }

            var deltaX = player.X - listenerPosition.X;
            var deltaY = player.Y - listenerPosition.Y;
            if ((deltaX * deltaX) + (deltaY * deltaY) <= DynamicMusicNearbyUberDistanceSquared)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureDynamicMusicLoaded()
    {
        if (_dynamicMusicLoadAttempted || !_audioAvailable)
        {
            return;
        }

        _dynamicMusicLoadAttempted = true;
        _gameplayAudioMusicController.TryLoadOptionalLoopedMusic(
            Path.Combine("Music", "action_redo_drum.ogg"),
            out _dynamicCombatDrumMusic,
            out _dynamicCombatDrumMusicInstance);
        _gameplayAudioMusicController.TryLoadOptionalLoopedMusic(
            Path.Combine("Music", "action_redo_body.ogg"),
            out _dynamicCombatBodyMusic,
            out _dynamicCombatBodyMusicInstance);
        _gameplayAudioMusicController.TryLoadOptionalLoopedMusic(
            Path.Combine("Music", "action_redo_bass.ogg"),
            out _dynamicCombatBassMusic,
            out _dynamicCombatBassMusicInstance);
			_gameplayAudioMusicController.TryLoadOptionalLoopedMusic(
            Path.Combine("Music", "action_redo_lead.ogg"),
            out _dynamicCombatLeadMusic,
            out _dynamicCombatLeadMusicInstance);
        _gameplayAudioMusicController.TryLoadOptionalMusicSound(
            Path.Combine("Music", "transition_riser.ogg"),
            out _dynamicCombatRiser,
            out _dynamicCombatRiserInstance,
            isLooped: false);
        _gameplayAudioMusicController.TryLoadOptionalLoopedMusic(
            Path.Combine("Music", "menumusic4.wav"),
            out _dynamicIntelMusic,
            out _dynamicIntelMusicInstance);
        _gameplayAudioMusicController.TryLoadOptionalLoopedMusic(
            Path.Combine("Music", "uber_common.wav"),
            out _dynamicUberMusic,
            out _dynamicUberMusicInstance);
    }

    private void EnsureDynamicMusicPlaybackStarted(DynamicMusicEventState targetState)
    {
        if (targetState == DynamicMusicEventState.Combat)
        {
            EnsureDynamicCombatMusicPlaybackStarted();
        }
        else if (HasDynamicCombatStemFade())
        {
            TryStartDynamicCombatLoopInstances("crossfading combat music event");
        }

        if (targetState == DynamicMusicEventState.Intel)
        {
            TryStartDynamicMusicInstance(_dynamicIntelMusicInstance, "starting intelligence music event");
        }
        else if (_dynamicIntelMusicFade > 0f)
        {
            TryStartDynamicMusicInstance(_dynamicIntelMusicInstance, "crossfading intelligence music event");
        }

        if (targetState == DynamicMusicEventState.Uber)
        {
            TryStartDynamicMusicInstance(_dynamicUberMusicInstance, "starting uber music event");
        }
        else if (_dynamicUberMusicFade > 0f)
        {
            TryStartDynamicMusicInstance(_dynamicUberMusicInstance, "crossfading uber music event");
        }
    }

    private void EnsureDynamicCombatMusicPlaybackStarted()
    {
        EnsureDynamicCombatLeadStem();

        if (_dynamicCombatRiserPending)
        {
            _dynamicCombatRiserPending = false;
            if (_dynamicCombatRiserInstance is not null)
            {
                SetSoundEffectInstanceVolume(_dynamicCombatRiserInstance, GetCombatMusicVolumeScale(GetNonLinearVolumeScale(_ingameMusicVolumePercent)));
                TryRestartDynamicMusicInstance(_dynamicCombatRiserInstance, "starting combat music riser");
                _dynamicCombatRiserDelaySecondsRemaining = DynamicMusicCombatRiserDelaySeconds;
            }
            else
            {
                _dynamicCombatRiserHoldStage = DynamicCombatMusicStage.None;
            }
        }

        if (_dynamicCombatRiserDelaySecondsRemaining > 0f)
        {
            return;
        }

        TryStartDynamicCombatLoopInstances("starting combat music event");
    }

    private bool HasDynamicCombatStemFade()
    {
        return _dynamicCombatDrumFade > 0f
            || _dynamicCombatBodyFade > 0f
            || _dynamicCombatBassFade > 0f
            || _dynamicCombatLeadFade > 0f;
    }

    private void TryStartDynamicCombatLoopInstances(string operation)
    {
        TryStartDynamicMusicInstance(_dynamicCombatDrumMusicInstance, operation);
        TryStartDynamicMusicInstance(_dynamicCombatBodyMusicInstance, operation);
        TryStartDynamicMusicInstance(_dynamicCombatBassMusicInstance, operation);
        TryStartDynamicMusicInstance(_dynamicCombatLeadMusicInstance, operation);
    }

    private void TryStartDynamicMusicInstance(SoundEffectInstance? instance, string operation)
    {
        if (instance is null)
        {
            return;
        }

        try
        {
            if (instance.State != SoundState.Playing)
            {
                instance.Play();
            }
        }
        catch (Exception ex)
        {
            DisableAudio(operation, ex);
        }
    }

    private void TryRestartDynamicMusicInstance(SoundEffectInstance? instance, string operation)
    {
        if (instance is null)
        {
            return;
        }

        try
        {
            instance.Stop();
            instance.Play();
        }
        catch (Exception ex)
        {
            DisableAudio(operation, ex);
        }
    }

    private void AdvanceDynamicMusicFades(DynamicMusicEventState targetState, GameTime gameTime)
    {
        var elapsedSeconds = Math.Clamp((float)gameTime.ElapsedGameTime.TotalSeconds, 0f, 0.1f);
        if (elapsedSeconds <= 0f)
        {
            elapsedSeconds = 1f / SimulationConfig.DefaultTicksPerSecond;
        }

        var fadeInStep = DynamicMusicFadeInPerSecond * elapsedSeconds;
        var fadeOutStep = DynamicMusicFadeOutPerSecond * elapsedSeconds;
        if (targetState == DynamicMusicEventState.Combat && _dynamicCombatRiserDelaySecondsRemaining > 0f)
        {
            _dynamicCombatRiserDelaySecondsRemaining = Math.Max(0f, _dynamicCombatRiserDelaySecondsRemaining - elapsedSeconds);
            if (_dynamicCombatRiserDelaySecondsRemaining <= 0f)
            {
                _dynamicCombatRiserHoldStage = DynamicCombatMusicStage.None;
            }
        }

        _dynamicCombatMusicFade = MoveDynamicMusicFadeToward(_dynamicCombatMusicFade, targetState == DynamicMusicEventState.Combat ? 1f : 0f, targetState == DynamicMusicEventState.Combat ? fadeInStep : fadeOutStep);
        var combatStemStage = _dynamicCombatRiserDelaySecondsRemaining > 0f
            ? _dynamicCombatRiserHoldStage
            : _dynamicCombatMusicStage;
        var combatStemTargets = targetState == DynamicMusicEventState.Combat
            ? GetDynamicCombatStemTargetVolumes(combatStemStage)
            : default;
        if (targetState == DynamicMusicEventState.Combat && combatStemTargets.Drum > 0f)
        {
            _dynamicCombatDrumsLocked = true;
        }

        if (targetState == DynamicMusicEventState.Combat && _dynamicCombatDrumsLocked)
        {
            combatStemTargets.Drum = 1f;
        }

        _dynamicCombatDrumFade = MoveDynamicMusicFadeToward(_dynamicCombatDrumFade, combatStemTargets.Drum, combatStemTargets.Drum > _dynamicCombatDrumFade ? fadeInStep : fadeOutStep);
        _dynamicCombatBodyFade = MoveDynamicMusicFadeToward(_dynamicCombatBodyFade, combatStemTargets.Body, combatStemTargets.Body > _dynamicCombatBodyFade ? fadeInStep : fadeOutStep);
        _dynamicCombatBassFade = MoveDynamicMusicFadeToward(_dynamicCombatBassFade, combatStemTargets.Bass, combatStemTargets.Bass > _dynamicCombatBassFade ? fadeInStep : fadeOutStep);
        _dynamicCombatLeadFade = MoveDynamicMusicFadeToward(_dynamicCombatLeadFade, combatStemTargets.Lead, combatStemTargets.Lead > _dynamicCombatLeadFade ? fadeInStep : fadeOutStep);
        _dynamicIntelMusicFade = MoveDynamicMusicFadeToward(_dynamicIntelMusicFade, targetState == DynamicMusicEventState.Intel ? 1f : 0f, targetState == DynamicMusicEventState.Intel ? fadeInStep : fadeOutStep);
        _dynamicUberMusicFade = MoveDynamicMusicFadeToward(_dynamicUberMusicFade, targetState == DynamicMusicEventState.Uber ? 1f : 0f, targetState == DynamicMusicEventState.Uber ? fadeInStep : fadeOutStep);
        var strongestEventFade = Math.Max(_dynamicCombatMusicFade, Math.Max(_dynamicIntelMusicFade, _dynamicUberMusicFade));
        _dynamicNormalMusicFade = 1f - strongestEventFade;
    }

    private (float Drum, float Body, float Bass, float Lead) GetDynamicCombatStemTargetVolumes(DynamicCombatMusicStage stage)
    {
        var leadStem = EnsureDynamicCombatLeadStem();
        return stage switch
        {
            DynamicCombatMusicStage.Light when leadStem == DynamicCombatMusicLeadStem.Drum => (1f, 0f, 0f, 0f),
            DynamicCombatMusicStage.Light when leadStem == DynamicCombatMusicLeadStem.Body => (0f, 1f, 0f, 0f),
            DynamicCombatMusicStage.Medium when leadStem == DynamicCombatMusicLeadStem.Drum => (1f, 0f, 1f, 0f),
            DynamicCombatMusicStage.Medium when leadStem == DynamicCombatMusicLeadStem.Body => (0f, 1f, 1f, 0f),
            //DynamicCombatMusicStage.Hard => (1f, 1f, 1f),
            DynamicCombatMusicStage.Hard => (1f, 1f, 1f, 0f),
            _ => default,
        };
    }

    private DynamicCombatMusicLeadStem EnsureDynamicCombatLeadStem()
    {
        if (_dynamicCombatLeadStem == DynamicCombatMusicLeadStem.None)
        {
            _dynamicCombatLeadStem = Random.Shared.Next(2) == 0
                ? DynamicCombatMusicLeadStem.Drum
                : DynamicCombatMusicLeadStem.Body;
        }

        return _dynamicCombatLeadStem;
    }

    private static float MoveDynamicMusicFadeToward(float current, float target, float maxDelta)
    {
        if (current < target)
        {
            return Math.Min(target, current + maxDelta);
        }

        if (current > target)
        {
            return Math.Max(target, current - maxDelta);
        }

        return current;
    }

    private void StopSilentDynamicMusicTracks(DynamicMusicEventState targetState)
    {
        if (targetState != DynamicMusicEventState.Combat)
        {
            StopDynamicMusicInstance(_dynamicCombatRiserInstance);
            if (!HasDynamicCombatStemFade())
            {
                StopDynamicMusicInstance(_dynamicCombatDrumMusicInstance);
                StopDynamicMusicInstance(_dynamicCombatBodyMusicInstance);
                StopDynamicMusicInstance(_dynamicCombatBassMusicInstance);
                StopDynamicMusicInstance(_dynamicCombatLeadMusicInstance);
            }
        }

        if (targetState != DynamicMusicEventState.Intel && _dynamicIntelMusicFade <= 0f)
        {
            StopDynamicMusicInstance(_dynamicIntelMusicInstance);
        }

        if (targetState != DynamicMusicEventState.Uber && _dynamicUberMusicFade <= 0f)
        {
            StopDynamicMusicInstance(_dynamicUberMusicInstance);
        }
    }

    private void ResetDynamicMusicPlayback()
    {
        if (!HasDynamicMusicPlaybackState())
        {
            return;
        }

        _dynamicMusicTargetState = DynamicMusicEventState.Normal;
        _dynamicMusicCombatTicksRemaining = 0;
        _dynamicCombatParticipantTicks.Clear();
        _dynamicCombatMusicStage = DynamicCombatMusicStage.None;
        _dynamicCombatRiserHoldStage = DynamicCombatMusicStage.None;
        _dynamicCombatLeadStem = DynamicCombatMusicLeadStem.None;
        _dynamicCombatRiserPending = false;
        _dynamicCombatDrumsLocked = false;
        _dynamicCombatRiserDelaySecondsRemaining = 0f;
        _dynamicNormalMusicFade = 1f;
        _dynamicCombatMusicFade = 0f;
        _dynamicCombatDrumFade = 0f;
        _dynamicCombatBodyFade = 0f;
        _dynamicCombatBassFade = 0f;
        _dynamicCombatLeadFade = 0f;
        _dynamicIntelMusicFade = 0f;
        _dynamicUberMusicFade = 0f;
        StopDynamicMusic();
        ApplyAudioVolumeState();
    }

    private bool HasDynamicMusicPlaybackState()
    {
        return _dynamicMusicTargetState != DynamicMusicEventState.Normal
            || _dynamicMusicCombatTicksRemaining > 0
            || _dynamicCombatParticipantTicks.Count > 0
            || _dynamicCombatRiserPending
            || _dynamicCombatRiserDelaySecondsRemaining > 0f
            || _dynamicNormalMusicFade < 1f
            || _dynamicCombatMusicFade > 0f
            || HasDynamicCombatStemFade()
            || _dynamicIntelMusicFade > 0f
            || _dynamicUberMusicFade > 0f
            || IsDynamicMusicInstancePlaying(_dynamicCombatDrumMusicInstance)
            || IsDynamicMusicInstancePlaying(_dynamicCombatBodyMusicInstance)
            || IsDynamicMusicInstancePlaying(_dynamicCombatBassMusicInstance)
            || IsDynamicMusicInstancePlaying(_dynamicCombatLeadMusicInstance)
            || IsDynamicMusicInstancePlaying(_dynamicCombatRiserInstance)
            || IsDynamicMusicInstancePlaying(_dynamicIntelMusicInstance)
            || IsDynamicMusicInstancePlaying(_dynamicUberMusicInstance);
    }

    private static bool IsDynamicMusicInstancePlaying(SoundEffectInstance? instance)
    {
        try
        {
            return instance?.State == SoundState.Playing;
        }
        catch
        {
            return false;
        }
    }

    private void StopDynamicMusic()
    {
        StopDynamicMusicInstance(_dynamicCombatDrumMusicInstance);
        StopDynamicMusicInstance(_dynamicCombatBodyMusicInstance);
        StopDynamicMusicInstance(_dynamicCombatBassMusicInstance);
        StopDynamicMusicInstance(_dynamicCombatLeadMusicInstance);
        StopDynamicMusicInstance(_dynamicCombatRiserInstance);
        StopDynamicMusicInstance(_dynamicIntelMusicInstance);
        StopDynamicMusicInstance(_dynamicUberMusicInstance);
    }

    private static void StopDynamicMusicInstance(SoundEffectInstance? instance)
    {
        try
        {
            if (instance?.State == SoundState.Playing)
            {
                instance.Stop();
            }
        }
        catch
        {
        }
    }

    private void DisposeDynamicMusic()
    {
        StopDynamicMusic();
        DisposeDynamicMusicTrack(ref _dynamicCombatDrumMusic, ref _dynamicCombatDrumMusicInstance);
        DisposeDynamicMusicTrack(ref _dynamicCombatBodyMusic, ref _dynamicCombatBodyMusicInstance);
        DisposeDynamicMusicTrack(ref _dynamicCombatBassMusic, ref _dynamicCombatBassMusicInstance);
        DisposeDynamicMusicTrack(ref _dynamicCombatLeadMusic, ref _dynamicCombatLeadMusicInstance);
        DisposeDynamicMusicTrack(ref _dynamicCombatRiser, ref _dynamicCombatRiserInstance);
        DisposeDynamicMusicTrack(ref _dynamicIntelMusic, ref _dynamicIntelMusicInstance);
        DisposeDynamicMusicTrack(ref _dynamicUberMusic, ref _dynamicUberMusicInstance);
        _dynamicMusicLoadAttempted = false;
        _dynamicCombatParticipantTicks.Clear();
        _dynamicCombatMusicStage = DynamicCombatMusicStage.None;
        _dynamicCombatRiserHoldStage = DynamicCombatMusicStage.None;
        _dynamicCombatLeadStem = DynamicCombatMusicLeadStem.None;
        _dynamicCombatRiserPending = false;
        _dynamicCombatDrumsLocked = false;
        _dynamicCombatRiserDelaySecondsRemaining = 0f;
        _dynamicNormalMusicFade = 1f;
        _dynamicCombatMusicFade = 0f;
        _dynamicCombatDrumFade = 0f;
        _dynamicCombatBodyFade = 0f;
        _dynamicCombatBassFade = 0f;
        _dynamicCombatLeadFade = 0f;
        _dynamicIntelMusicFade = 0f;
        _dynamicUberMusicFade = 0f;
    }

    private static void DisposeDynamicMusicTrack(ref SoundEffect? music, ref SoundEffectInstance? instance)
    {
        try { instance?.Dispose(); } catch { }
        instance = null;
        try { music?.Dispose(); } catch { }
        music = null;
    }

    private void UpdateDynamicMusicInstanceVolumes(float ingameMusicVolume)
    {
        var combatMusicVolume = GetCombatMusicVolumeScale(ingameMusicVolume);
        SetSoundEffectInstanceVolume(_dynamicCombatDrumMusicInstance, combatMusicVolume * _dynamicCombatDrumFade);
        SetSoundEffectInstanceVolume(_dynamicCombatBodyMusicInstance, combatMusicVolume * _dynamicCombatBodyFade);
        SetSoundEffectInstanceVolume(_dynamicCombatBassMusicInstance, combatMusicVolume * _dynamicCombatBassFade);
        SetSoundEffectInstanceVolume(_dynamicCombatLeadMusicInstance, combatMusicVolume * _dynamicCombatLeadFade);
        SetSoundEffectInstanceVolume(_dynamicCombatRiserInstance, combatMusicVolume);
        SetSoundEffectInstanceVolume(_dynamicIntelMusicInstance, ingameMusicVolume * DynamicMusicIntelVolumeScale * _dynamicIntelMusicFade);
        SetSoundEffectInstanceVolume(_dynamicUberMusicInstance, ingameMusicVolume * DynamicMusicUberVolumeScale * _dynamicUberMusicFade);
    }

    private float GetCombatMusicVolumeScale(float ingameMusicVolume)
    {
        return ingameMusicVolume * Math.Clamp(
            _combatMusicVolumePercent / 100f,
            0f,
            OpenGarrisonPreferencesDocument.MaxCombatMusicVolumePercent / 100f);
    }
}
