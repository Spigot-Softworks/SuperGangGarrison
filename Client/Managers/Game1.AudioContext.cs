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
    AudioRuntimeSettings IAudioContext.AudioRuntimeSettings { get => _audioManager.RuntimeSettings; }
    GameplayRuntimeSettings IAudioContext.GameplayRuntimeSettings { get => _gameplayManager.RuntimeSettings; }
    MusicResources IAudioContext.MusicResources => _audioManager.MusicResources;

    bool IAudioContext._audioAvailable { get => _audioAvailable; set => _audioAvailable = value; }

    OpenGarrison.Core.SimulationConfig IAudioContext._config { get => _config; set => _config = value; }

    List<OpenGarrison.Client.Game1.ExplosionVisual> IAudioContext._explosions { get => _explosions; }


    float IAudioContext._gameplayPresentationDeltaSeconds { get => _gameplayPresentationDeltaSeconds; set => _gameplayPresentationDeltaSeconds = value; }











    bool IAudioContext._mainMenuOpen { get => _mainMenuOpen; set => _mainMenuOpen = value; }


    OpenGarrison.Client.NetworkGameClient IAudioContext._networkClient { get => _networkClient; }

    List<OpenGarrison.Client.Game1.PendingBrowserSoundEvent> IAudioContext._pendingBrowserSoundEvents { get => _pendingBrowserSoundEvents; }

    Nullable<OpenGarrison.Core.PrimaryWeaponKind> IAudioContext._pendingImmediateRapidFireWeaponKind { get => _pendingImmediateRapidFireWeaponKind; set => _pendingImmediateRapidFireWeaponKind = value; }







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

    void IAudioContext.EnqueuePendingBrowserSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent) { EnqueuePendingBrowserSoundEvent(soundEvent); }

    void IAudioContext.ForgetPresentedExplosionVisualForSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent) { ForgetPresentedExplosionVisualForSoundEvent(soundEvent); }

    OpenGarrison.Core.PlayerEntity IAudioContext.GetImmediatePrimaryPresentationPlayer() => GetImmediatePrimaryPresentationPlayer();

    ValueTuple<float, float> IAudioContext.GetLoopedWorldSoundMix(string soundName, float worldX, float worldY, bool isLocalSource, int sourcePlayerId) => GetLoopedWorldSoundMix(soundName, worldX, worldY, isLocalSource, sourcePlayerId);

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

    string IAudioContext.ResolvePlayerDeathVoiceSoundName(OpenGarrison.Core.WorldSoundEvent soundEvent) => ResolvePlayerDeathVoiceSoundName(soundEvent);

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
