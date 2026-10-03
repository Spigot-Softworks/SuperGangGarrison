#nullable enable

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenGarrison.Client;

public interface IAudioContext
{
    AudioRuntimeSettings AudioRuntimeSettings { get; }
    GameplayRuntimeSettings GameplayRuntimeSettings { get; }
    bool _audioAvailable { get; set; }
    OpenGarrison.Core.SimulationConfig _config { get; set; }
    List<OpenGarrison.Client.Game1.ExplosionVisual> _explosions { get; }
    float _gameplayPresentationDeltaSeconds { get; set; }
    MusicResources MusicResources { get; }
    bool _mainMenuOpen { get; set; }
    OpenGarrison.Client.NetworkGameClient _networkClient { get; }
    List<OpenGarrison.Client.Game1.PendingBrowserSoundEvent> _pendingBrowserSoundEvents { get; }
    Nullable<OpenGarrison.Core.PrimaryWeaponKind> _pendingImmediateRapidFireWeaponKind { get; set; }
    OpenGarrison.Client.GameMakerRuntimeAssetCache _runtimeAssets { get; set; }
    bool _wasDeathCamActive { get; set; }
    bool _wasMatchEnded { get; set; }
    OpenGarrison.Core.SimulationWorld _world { get; set; }
    bool IsAnyLastToDieSessionActive { get; }
    bool IsServerLauncherMode { get; }
    void AddConsoleLine(string line);
    void AdvanceLocalWeaponSoundFocus();
    void AdvanceLowPriorityWorldSoundThrottle();
    void AdvanceRecentGibSoundEvents();
    void AdvanceRecentProjectileSoundEvents();
    bool AllowsIngameMusic();
    bool AllowsMenuMusic();
    void ApplyAudioVolumeState();
    void BeginExplosionSoundDeduplicationFrame();
    void DisableAudio(string reason, System.Exception ex);
    void EnqueuePendingBrowserSoundEvent(string soundName, float x, float y);
    void EnqueuePendingBrowserSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent);
    void ForgetPresentedExplosionVisualForSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent);
    OpenGarrison.Core.PlayerEntity GetImmediatePrimaryPresentationPlayer();
    (float Volume, float Pan) GetLoopedWorldSoundMix(string soundName, float worldX, float worldY, bool isLocalSource);
    int GetPlayerBuffBannerChargeDamage(OpenGarrison.Core.PlayerEntity player);
    int GetPlayerBuffBannerMaxChargeDamage(OpenGarrison.Core.PlayerEntity player);
    bool GetPlayerIsHeavyEating(OpenGarrison.Core.PlayerEntity player);
    double GetProjectileRenderTimeSeconds();
    int GetResolvedLocalPlayerId();
    float GetSoundEffectsVolumeScale();
    Microsoft.Xna.Framework.Vector2 GetWorldSoundListenerPosition();
    (float Volume, float Pan) GetWorldSoundMix(OpenGarrison.Core.WorldSoundEvent soundEvent);
    (float Volume, float Pan) GetWorldSoundMix(float worldX, float worldY);
    bool HasPlayedExplosionSoundThisFrame(float x, float y);
    bool HasPresentedExplosionVisualForSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent);
    bool HasPresentedExplosionVisualThisFrame(float x, float y);
    bool HasRecentPredictedExplosionVisual(float x, float y);
    bool IsHostedLastToDieMenuMusicPhase();
    bool IsLastToDieDeathFocusPresentationActive();
    bool IsLastToDieFailurePresentationActive();
    bool IsLastToDieMenuActive();
    bool IsLocalPlayerSoundSource(int sourcePlayerId);
    void NotifyClientPluginsWorldSound(OpenGarrison.Core.WorldSoundEvent soundEvent);
    void RecordPlayedExplosionSoundThisFrame(float x, float y);
    void RememberPlayedGibSound(OpenGarrison.Core.WorldSoundEvent soundEvent);
    void RememberPlayedLowPriorityWorldSound(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent);
    void RememberPlayedProjectileSound(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent);
    void RememberPresentedExplosionVisualForSoundEvent(OpenGarrison.Core.WorldSoundEvent soundEvent);
    bool ShouldPresentAuthoritativeExplosionSound(OpenGarrison.Core.WorldSoundEvent soundEvent);
    bool ShouldSuppressManagedRapidFireSound(OpenGarrison.Core.WorldSoundEvent soundEvent);
    bool ShouldSuppressPredictedGibSoundEcho(OpenGarrison.Core.WorldSoundEvent soundEvent);
    bool ShouldSuppressPredictedProjectileSoundEcho(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent);
    bool ShouldThrottleLowPriorityWorldSound(string resolvedSoundName, OpenGarrison.Core.WorldSoundEvent soundEvent);
    void StopFaucetMusic();
    void StopIngameMusic();
    void StopLastToDieIngameMusic();
    void StopLastToDieMenuMusic();
    void StopMenuMusic();
    void TriggerLocalConfirmedWeaponFireFeedback(string soundName, OpenGarrison.Core.WorldSoundEvent soundEvent);
    bool TryCreateExplosionVisual(OpenGarrison.Core.WorldSoundEvent soundEvent, out OpenGarrison.Client.Game1.ExplosionVisual explosion);
    bool TryPlaySound(Microsoft.Xna.Framework.Audio.SoundEffect sound, float volume, float pitch, float pan);
}
