#nullable enable

using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1 : IAudioContext
{
    bool IAudioContext._audioAvailable { get => _audioAvailable; set => _audioAvailable = value; }

    OpenGarrison.Core.SimulationConfig IAudioContext._config { get => _config; set => _config = value; }

    List<OpenGarrison.Client.Game1.ExplosionVisual> IAudioContext._explosions { get => _explosions; }

    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._faucetMusic => ref _faucetMusic;

    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._faucetMusicInstance => ref _faucetMusicInstance;

    ref bool IAudioContext._faucetMusicLoadAttempted => ref _faucetMusicLoadAttempted;

    float IAudioContext._gameplayPresentationDeltaSeconds { get => _gameplayPresentationDeltaSeconds; set => _gameplayPresentationDeltaSeconds = value; }

    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._ingameCombatMusic => ref _ingameCombatMusic;

    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._ingameCombatMusicInstance => ref _ingameCombatMusicInstance;

    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._ingameMusic => ref _ingameMusic;

    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._ingameMusicInstance => ref _ingameMusicInstance;

    ref bool IAudioContext._ingameMusicLoadAttempted => ref _ingameMusicLoadAttempted;

    int IAudioContext._ingameMusicVolumePercent { get => _ingameMusicVolumePercent; set => _ingameMusicVolumePercent = value; }

    bool IAudioContext._killCamEnabled { get => _killCamEnabled; set => _killCamEnabled = value; }

    Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._lastToDieGameOverSound { get => _lastToDieGameOverSound; set => _lastToDieGameOverSound = value; }

    Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._lastToDieGameOverSoundInstance { get => _lastToDieGameOverSoundInstance; set => _lastToDieGameOverSoundInstance = value; }

    bool IAudioContext._lastToDieGameOverSoundLoadAttempted { get => _lastToDieGameOverSoundLoadAttempted; set => _lastToDieGameOverSoundLoadAttempted = value; }

    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._lastToDieIngameMusic => ref _lastToDieIngameMusic;

    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._lastToDieIngameMusicInstance => ref _lastToDieIngameMusicInstance;

    ref bool IAudioContext._lastToDieIngameMusicLoadAttempted => ref _lastToDieIngameMusicLoadAttempted;

    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._lastToDieMenuMusic => ref _lastToDieMenuMusic;

    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._lastToDieMenuMusicInstance => ref _lastToDieMenuMusicInstance;

    ref bool IAudioContext._lastToDieMenuMusicLoadAttempted => ref _lastToDieMenuMusicLoadAttempted;

    float IAudioContext._localBuffBannerReadyCueEchoSuppressionSeconds { get => _localBuffBannerReadyCueEchoSuppressionSeconds; set => _localBuffBannerReadyCueEchoSuppressionSeconds = value; }

    OpenGarrison.Client.BuffBannerReadyCueTracker IAudioContext._localBuffBannerReadyCueTracker { get => _localBuffBannerReadyCueTracker; }

    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._localChaingunSoundInstance => ref _localChaingunSoundInstance;

    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._localFlamethrowerSoundInstance => ref _localFlamethrowerSoundInstance;

    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._localMedigunSoundInstance => ref _localMedigunSoundInstance;

    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._localUberIdleSoundInstance => ref _localUberIdleSoundInstance;

    bool IAudioContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }

    ref Microsoft.Xna.Framework.Audio.SoundEffect IAudioContext._menuMusic => ref _menuMusic;

    ref Microsoft.Xna.Framework.Audio.SoundEffectInstance IAudioContext._menuMusicInstance => ref _menuMusicInstance;

    ref bool IAudioContext._menuMusicLoadAttempted => ref _menuMusicLoadAttempted;

    OpenGarrison.Client.NetworkGameClient IAudioContext._networkClient { get => _networkClient; }

    List<OpenGarrison.Client.Game1.PendingBrowserSoundEvent> IAudioContext._pendingBrowserSoundEvents { get => _pendingBrowserSoundEvents; }

    Nullable<OpenGarrison.Core.PrimaryWeaponKind> IAudioContext._pendingImmediateRapidFireWeaponKind { get => _pendingImmediateRapidFireWeaponKind; set => _pendingImmediateRapidFireWeaponKind = value; }

    List<OpenGarrison.Core.WorldSoundEvent> IAudioContext._pendingNetworkSoundEvents { get => _pendingNetworkSoundEvents; }

    int IAudioContext._previousLocalDemoknightChargeTicks { get => _previousLocalDemoknightChargeTicks; set => _previousLocalDemoknightChargeTicks = value; }

    HashSet<ulong> IAudioContext._processedKillFeedEventIds { get => _processedKillFeedEventIds; }

    Queue<ulong> IAudioContext._processedKillFeedEventOrder { get => _processedKillFeedEventOrder; }

    HashSet<ulong> IAudioContext._processedNetworkSoundEventIds { get => _processedNetworkSoundEventIds; }

    Queue<ulong> IAudioContext._processedNetworkSoundEventOrder { get => _processedNetworkSoundEventOrder; }

    OpenGarrison.Client.GameMakerRuntimeAssetCache IAudioContext._runtimeAssets { get => _runtimeAssets; set => _runtimeAssets = value; }

    bool IAudioContext._wasDeathCamActive { get => _wasDeathCamActive; set => _wasDeathCamActive = value; }

    bool IAudioContext._wasMatchEnded { get => _wasMatchEnded; set => _wasMatchEnded = value; }

    OpenGarrison.Core.SimulationWorld IAudioContext._world { get => _world; set => _world = value; }

    bool IAudioContext.IsAnyLastToDieSessionActive { get => IsAnyLastToDieSessionActive; }

    bool IAudioContext.IsServerLauncherMode { get => IsServerLauncherMode; }

    void IAudioContext.AddConsoleLine(string line) { AddConsoleLine(line); }

    void IAudioContext.AdvanceLocalWeaponSoundFocus() { AdvanceLocalWeaponSoundFocus(); }

    void IAudioContext.AdvanceLowPriorityWorldSoundThrottle() { AdvanceLowPriorityWorldSoundThrottle(); }

    void IAudioContext.AdvanceRecentGibSoundEvents() { AdvanceRecentGibSoundEvents(); }

    void IAudioContext.AdvanceRecentProjectileSoundEvents() { AdvanceRecentProjectileSoundEvents(); }

    bool IAudioContext.AllowsIngameMusic() => AllowsIngameMusic();

    bool IAudioContext.AllowsMenuMusic() => AllowsMenuMusic();

    void IAudioContext.ApplyAudioVolumeState() { ApplyAudioVolumeState(); }

    void IAudioContext.BeginExplosionSoundDeduplicationFrame() { BeginExplosionSoundDeduplicationFrame(); }

    void IAudioContext.DisableAudio(string reason, System.Exception ex) { DisableAudio(reason, ex); }

    void IAudioContext.EnqueuePendingBrowserSoundEvent(string soundName, float x, float y) { EnqueuePendingBrowserSoundEvent(soundName, x, y); }

    void IAudioContext.ForgetPresentedExplosionVisualForSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent) { ForgetPresentedExplosionVisualForSoundEvent(soundEvent); }

    OpenGarrison.Core.PlayerEntity IAudioContext.GetImmediatePrimaryPresentationPlayer() => GetImmediatePrimaryPresentationPlayer();

    ValueTuple<float, float> IAudioContext.GetLoopedWorldSoundMix(string soundName, float worldX, float worldY, bool isLocalSource) => GetLoopedWorldSoundMix(soundName, worldX, worldY, isLocalSource);

    int IAudioContext.GetPlayerBuffBannerChargeDamage(OpenGarrison.Core.PlayerEntity player) => GetPlayerBuffBannerChargeDamage(player);

    int IAudioContext.GetPlayerBuffBannerMaxChargeDamage(OpenGarrison.Core.PlayerEntity player) => GetPlayerBuffBannerMaxChargeDamage(player);

    bool IAudioContext.GetPlayerIsHeavyEating(OpenGarrison.Core.PlayerEntity player) => GetPlayerIsHeavyEating(player);

    double IAudioContext.GetProjectileRenderTimeSeconds() => GetProjectileRenderTimeSeconds();

    int IAudioContext.GetResolvedLocalPlayerId() => GetResolvedLocalPlayerId();

    float IAudioContext.GetSoundEffectsVolumeScale() => GetSoundEffectsVolumeScale();

    Microsoft.Xna.Framework.Vector2 IAudioContext.GetWorldSoundListenerPosition() => GetWorldSoundListenerPosition();

    ValueTuple<float, float> IAudioContext.GetWorldSoundMix(OpenGarrison.Core.WorldSoundEvent soundEvent) => GetWorldSoundMix(soundEvent);

    ValueTuple<float, float> IAudioContext.GetWorldSoundMix(float worldX, float worldY) => GetWorldSoundMix(worldX, worldY);

    bool IAudioContext.HasPlayedExplosionSoundThisFrame(float x, float y) => HasPlayedExplosionSoundThisFrame(x, y);

    bool IAudioContext.HasPresentedExplosionVisualForSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent) => HasPresentedExplosionVisualForSoundEvent(soundEvent);

    bool IAudioContext.HasPresentedExplosionVisualThisFrame(float x, float y) => HasPresentedExplosionVisualThisFrame(x, y);

    bool IAudioContext.HasRecentPredictedExplosionVisual(float x, float y) => HasRecentPredictedExplosionVisual(x, y);

    bool IAudioContext.IsHostedLastToDieMenuMusicPhase() => IsHostedLastToDieMenuMusicPhase();

    bool IAudioContext.IsLastToDieDeathFocusPresentationActive() => IsLastToDieDeathFocusPresentationActive();

    bool IAudioContext.IsLastToDieFailurePresentationActive() => IsLastToDieFailurePresentationActive();

    bool IAudioContext.IsLastToDieMenuActive() => IsLastToDieMenuActive();

    bool IAudioContext.IsLocalPlayerSoundSource(int sourcePlayerId) => IsLocalPlayerSoundSource(sourcePlayerId);

    void IAudioContext.NotifyClientPluginsWorldSound(OpenGarrison.Core.WorldSoundEvent soundEvent) { NotifyClientPluginsWorldSound(soundEvent); }

    void IAudioContext.RecordPlayedExplosionSoundThisFrame(float x, float y) { RecordPlayedExplosionSoundThisFrame(x, y); }

    void IAudioContext.RememberPlayedGibSound(OpenGarrison.Core.WorldSoundEvent soundEvent) { RememberPlayedGibSound(soundEvent); }

    void IAudioContext.RememberPlayedLowPriorityWorldSound(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent) { RememberPlayedLowPriorityWorldSound(resolvedSoundName, soundEvent); }

    void IAudioContext.RememberPlayedProjectileSound(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent) { RememberPlayedProjectileSound(resolvedSoundName, soundEvent); }

    void IAudioContext.RememberPresentedExplosionVisualForSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent) { RememberPresentedExplosionVisualForSoundEvent(soundEvent); }

    bool IAudioContext.ShouldPresentAuthoritativeExplosionSound(OpenGarrison.Core.WorldSoundEvent soundEvent) => ShouldPresentAuthoritativeExplosionSound(soundEvent);

    bool IAudioContext.ShouldSuppressManagedRapidFireSound(OpenGarrison.Core.WorldSoundEvent soundEvent) => ShouldSuppressManagedRapidFireSound(soundEvent);

    bool IAudioContext.ShouldSuppressPredictedGibSoundEcho(OpenGarrison.Core.WorldSoundEvent soundEvent) => ShouldSuppressPredictedGibSoundEcho(soundEvent);

    bool IAudioContext.ShouldSuppressPredictedProjectileSoundEcho(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent) => ShouldSuppressPredictedProjectileSoundEcho(resolvedSoundName, soundEvent);

    bool IAudioContext.ShouldThrottleLowPriorityWorldSound(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent) => ShouldThrottleLowPriorityWorldSound(resolvedSoundName, soundEvent);

    void IAudioContext.StopFaucetMusic() { StopFaucetMusic(); }

    void IAudioContext.StopIngameMusic() { StopIngameMusic(); }

    void IAudioContext.StopLastToDieIngameMusic() { StopLastToDieIngameMusic(); }

    void IAudioContext.StopLastToDieMenuMusic() { StopLastToDieMenuMusic(); }

    void IAudioContext.StopMenuMusic() { StopMenuMusic(); }

    void IAudioContext.TriggerLocalConfirmedWeaponFireFeedback(string soundName, OpenGarrison.Core.WorldSoundEvent soundEvent) { TriggerLocalConfirmedWeaponFireFeedback(soundName, soundEvent); }

    bool IAudioContext.TryCreateExplosionVisual(OpenGarrison.Core.WorldSoundEvent soundEvent, out OpenGarrison.Client.Game1.ExplosionVisual explosion) => TryCreateExplosionVisual(soundEvent, out explosion);

    bool IAudioContext.TryPlaySound(Microsoft.Xna.Framework.Audio.SoundEffect sound, float volume, float pitch, float pan) => TryPlaySound(sound, volume, pitch, pan);

}
