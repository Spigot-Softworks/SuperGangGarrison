#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1
{
    public const int BrowserPendingSoundEventLifetimeTicks = 30;
    public const int BrowserPendingSoundEventLimit = 96;
    public const int RecentGibSoundEchoLifetimeTicks = 18;
    public const int RecentGibSoundEchoLimit = 16;
    public const float RecentGibSoundEchoDistanceSquared = 64f * 64f;
    public const int RecentProjectileSoundEchoLifetimeTicks = 18;
    public const int RecentProjectileSoundEchoLimit = 32;
    public const float RecentProjectileExplosionSoundEchoDistanceSquared = 64f * 64f;
    public const float RecentProjectileFireSoundEchoDistanceSquared = 24f * 24f;
    public const int LowPriorityWorldSoundThrottleLifetimeTicks = 8;
    public const int LowPriorityWorldSoundThrottleLimit = 24;
    public const int LowPriorityWorldSoundFrameLimit = 4;
    public const float JumpSoundThrottleDistanceSquared = 96f * 96f;
    public const float LocalWeaponSoundVolumeMultiplier = 2.1f;
    public const float LocalWeaponSoundMinimumVolume = 0.9f;
    public const float RemoteWeaponSoundVolumeMultiplier = 0.52f;
    public const float LocalWeaponSoundPanMultiplier = 0.35f;
    public const float LocalWeaponSoundFocusDurationSeconds = 0.16f;
    public const float FocusedRemoteWeaponSoundVolumeMultiplier = 0.58f;
    public const float FocusedOtherWorldSoundVolumeMultiplier = 0.76f;
    public const float RemoteHealingCabinetSoundVolumeMultiplier = 0.62f;

    public sealed class PendingBrowserSoundEvent
    {
        public PendingBrowserSoundEvent(string soundName, float x, float y, int ticksRemaining)
        {
            SoundName = soundName;
            X = x;
            Y = y;
            TicksRemaining = ticksRemaining;
        }

        public string SoundName { get; }

        public float X { get; }

        public float Y { get; }

        public int TicksRemaining { get; set; }
    }

    public sealed class RecentGibSoundEvent
    {
        public RecentGibSoundEvent(float x, float y, bool isNetworkEvent, int ticksRemaining)
        {
            X = x;
            Y = y;
            IsNetworkEvent = isNetworkEvent;
            TicksRemaining = ticksRemaining;
        }

        public float X { get; }

        public float Y { get; }

        public bool IsNetworkEvent { get; }

        public int TicksRemaining { get; set; }
    }

    public sealed class RecentProjectileSoundEvent
    {
        public RecentProjectileSoundEvent(
            string soundName,
            float x,
            float y,
            bool isNetworkEvent,
            int sourcePlayerId,
            int ticksRemaining)
        {
            SoundName = soundName;
            X = x;
            Y = y;
            IsNetworkEvent = isNetworkEvent;
            SourcePlayerId = sourcePlayerId;
            TicksRemaining = ticksRemaining;
        }

        public string SoundName { get; }

        public float X { get; }

        public float Y { get; }

        public bool IsNetworkEvent { get; }

        public int SourcePlayerId { get; }

        public int TicksRemaining { get; set; }
    }

    public sealed class RecentLowPriorityWorldSoundEvent
    {
        public RecentLowPriorityWorldSoundEvent(string soundName, float x, float y, int ticksRemaining)
        {
            SoundName = soundName;
            X = x;
            Y = y;
            TicksRemaining = ticksRemaining;
        }

        public string SoundName { get; }

        public float X { get; }

        public float Y { get; }

        public int TicksRemaining { get; set; }
    }

    public SoundEffect? _menuMusic;
    public SoundEffectInstance? _menuMusicInstance;
    public SoundEffect? _lastToDieMenuMusic;
    public SoundEffectInstance? _lastToDieMenuMusicInstance;
    public SoundEffect? _faucetMusic;
    public SoundEffectInstance? _faucetMusicInstance;
    public SoundEffect? _ingameMusic;
    public SoundEffectInstance? _ingameMusicInstance;
    public SoundEffect? _ingameCombatMusic;
    public SoundEffectInstance? _ingameCombatMusicInstance;
    public SoundEffect? _lastToDieIngameMusic;
    public SoundEffectInstance? _lastToDieIngameMusicInstance;
    public SoundEffect? _lastToDieGameOverSound;
    public SoundEffectInstance? _lastToDieGameOverSoundInstance;
    public SoundEffectInstance? _localChaingunSoundInstance;
    public SoundEffectInstance? _localFlamethrowerSoundInstance;
    public SoundEffectInstance? _localMedigunSoundInstance;
    public SoundEffectInstance? _localUberIdleSoundInstance;
    public bool _audioAvailable = true;
    public bool _audioMuted;
    public int _masterVolumePercent = 100;
    public int _menuMusicVolumePercent = 70;
    public int _ingameMusicVolumePercent = 70;
    public int _combatMusicVolumePercent = OpenGarrisonPreferencesDocument.DefaultCombatMusicVolumePercent;
    public int _soundEffectsVolumePercent = 70;
    public MusicMode _musicMode = MusicMode.MenuAndInGame;
    public bool _menuMusicLoadAttempted;
    public bool _lastToDieMenuMusicLoadAttempted;
    public bool _faucetMusicLoadAttempted;
    public bool _ingameMusicLoadAttempted;
    public bool _lastToDieIngameMusicLoadAttempted;
    public bool _lastToDieGameOverSoundLoadAttempted;
    public readonly HashSet<ulong> _processedNetworkSoundEventIds = new();
    public readonly Queue<ulong> _processedNetworkSoundEventOrder = new();
    public readonly HashSet<ulong> _processedKillFeedEventIds = new();
    public readonly Queue<ulong> _processedKillFeedEventOrder = new();
    public readonly List<PendingBrowserSoundEvent> _pendingBrowserSoundEvents = new();
    public readonly List<WorldSoundEvent> _pendingNetworkSoundEvents = new();
    public readonly List<RecentGibSoundEvent> _recentGibSoundEvents = new();
    public readonly List<RecentProjectileSoundEvent> _recentProjectileSoundEvents = new();
    public readonly List<RecentLowPriorityWorldSoundEvent> _recentLowPriorityWorldSoundEvents = new();
    public int _lowPriorityWorldSoundsPlayedThisFrame;
    public float _localWeaponSoundFocusRemainingSeconds;
    private readonly List<PlayedExplosionSoundThisFrame> _playedExplosionSoundsThisFrame = new();
    private string _lastOneShotSoundFailureMessage = string.Empty;

    private readonly record struct PlayedExplosionSoundThisFrame(float X, float Y);

    public void LoadMenuMusic()
    {
        _audioManager.Music.LoadMenuMusic();
    }

    public void LoadFaucetMusic()
    {
        _audioManager.Music.LoadFaucetMusic();
    }

    public void LoadIngameMusic()
    {
        _audioManager.Music.LoadIngameMusic();
    }

    public void LoadLastToDieMenuMusic()
    {
        _audioManager.Music.LoadLastToDieMenuMusic();
    }

    public void LoadLastToDieIngameMusic()
    {
        _audioManager.Music.LoadLastToDieIngameMusic();
    }

    private void TryLoadLoopedMusic(
        string relativePath,
        out SoundEffect? music,
        out SoundEffectInstance? musicInstance,
        float volume = 1f,
        bool disableAudioOnFailure = true)
    {
        music = null;
        musicInstance = null;

        var musicPath = FindLoopedMusicPath(relativePath);
        if (musicPath is null || !File.Exists(musicPath))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(musicPath);
            music = SoundEffect.FromStream(stream);
            musicInstance = music.CreateInstance();
            musicInstance.IsLooped = true;
            musicInstance.Volume = volume;
        }
        catch (Exception ex)
        {
            try
            {
                musicInstance?.Dispose();
            }
            catch
            {
            }

            try
            {
                music?.Dispose();
            }
            catch
            {
            }

            musicInstance = null;
            music = null;

            if (disableAudioOnFailure)
            {
                DisableAudio($"initializing {Path.GetFileName(relativePath)}", ex);
                return;
            }

            AddConsoleLine($"optional music unavailable: {Path.GetFileName(relativePath)} ({ex.GetType().Name}: {ex.Message})");
        }
    }

    public void EnsureMenuMusicPlaying()
    {
        _audioManager.Music.EnsureMenuMusicPlaying();
    }

    private void EnsureFaucetMusicPlaying()
    {
        _audioManager.Music.EnsureFaucetMusicPlaying();
    }

    public void StopMenuMusic()
    {
        _audioManager.Music.StopMenuMusic();
    }

    public void StopLastToDieMenuMusic()
    {
        _audioManager.Music.StopLastToDieMenuMusic();
    }

    public void StopFaucetMusic()
    {
        _audioManager.Music.StopFaucetMusic();
    }

    private void EnsureIngameMusicPlaying()
    {
        _audioManager.Music.EnsureIngameMusicPlaying();
    }

    public void StopIngameMusic()
    {
        StopGameplaySoundMusicOverride();
        _audioManager.Music.StopIngameMusic();
        ResetDynamicMusicPlayback();
    }

    public void StopLastToDieIngameMusic()
    {
        _audioManager.Music.StopLastToDieIngameMusic();
    }

    private void PlayDeathCamSoundIfNeeded()
    {
        _audioManager.Events.PlayDeathCamSoundIfNeeded();
    }

    private void PlayDemoknightChargeReadySoundIfNeeded()
    {
        _audioManager.Events.PlayDemoknightChargeReadySoundIfNeeded();
    }

    private void PlayBuffBannerReadySoundIfNeeded()
    {
        _audioManager.Events.PlayBuffBannerReadySoundIfNeeded();
    }

    public void ResetBuffBannerReadySoundObservation()
    {
        _audioManager.Events.ResetBuffBannerReadySoundObservation();
    }

    private void PlayRoundEndSoundIfNeeded()
    {
        _audioManager.Events.PlayRoundEndSoundIfNeeded();
    }

    private void PlayKillFeedAnnouncementSounds()
    {
        _audioManager.Events.PlayKillFeedAnnouncementSounds();
    }

    private void PlayLastToDieGameOverSound()
    {
        _audioManager.Music.PlayLastToDieGameOverSound();
    }

    public void StopLastToDieGameOverSound()
    {
        _audioManager.Music.StopLastToDieGameOverSound();
    }

    private void PlayPendingSoundEvents()
    {
        _audioManager.Events.PlayPendingSoundEvents();
    }

    private void PlayPredictedGibSound(float worldX, float worldY)
    {
        if (!_audioAvailable)
        {
            return;
        }

        var soundEvent = new WorldSoundEvent("Gibbing", worldX, worldY, SourceFrame: (ulong)Math.Max(0, _world.Frame));
        if (ShouldSuppressPredictedGibSoundEcho(soundEvent))
        {
            return;
        }

        var sound = _runtimeAssets.GetSound(soundEvent.SoundName);
        if (sound is null)
        {
            if (OperatingSystem.IsBrowser())
            {
                EnqueuePendingBrowserSoundEvent(soundEvent.SoundName, worldX, worldY);
            }

            return;
        }

        var (volume, pan) = GetWorldSoundMix(worldX, worldY);
        if (volume <= 0f)
        {
            return;
        }

        TryPlaySound(sound, volume, 0f, pan);
        RememberPlayedGibSound(soundEvent);
    }

    private void PlayPredictedPrimaryFireSound()
    {
        if (!_audioAvailable || _runtimeAssets is null)
        {
            return;
        }

        var player = GetImmediatePrimaryPresentationPlayer();
        var soundName = ResolvePredictedPrimaryFireSoundName(player);
        if (string.IsNullOrWhiteSpace(soundName)
            || IsManagedRapidFirePresentation(player, soundName)
            || player.HasPrimaryBehavior(BuiltInGameplayBehaviorIds.WhippingCord))
        {
            // Looped minigun/flamethrower audio is started by the rapid-fire
            // controller, which also keeps it alive while the trigger is held.
            // Whipping Cord plays its hit cue after wind-up from the sim event.
            return;
        }

        var soundEvent = new WorldSoundEvent(
            soundName,
            player.X,
            player.Y,
            SourceFrame: (ulong)Math.Max(0, _world.Frame),
            SourcePlayerId: player.Id);
        if (ShouldSuppressPredictedProjectileSoundEcho(soundName, soundEvent))
        {
            return;
        }

        var sound = _runtimeAssets.GetSound(soundName);
        if (sound is null)
        {
            return;
        }

        var (volume, pan) = GetWorldSoundMix(soundEvent);
        if (volume <= 0f || !TryPlaySound(sound, volume, 0f, pan))
        {
            return;
        }

        // The authoritative event carries a non-zero EventId. Keeping this
        // local EventId=0 entry lets the normal event pump suppress that echo.
        RememberPlayedProjectileSound(soundName, soundEvent);
    }

    private static string? ResolvePredictedPrimaryFireSoundName(PlayerEntity player)
    {
        if (player.IsExperimentalDemoknightEnabled)
        {
            return ExperimentalDemoknightCatalog.EyelanderSwingSoundName;
        }

        var weapon = player.IsAcquiredWeaponEquipped
            ? player.AcquiredWeapon
            : player.IsExperimentalOffhandSelected
                ? player.ExperimentalOffhandWeapon
                : player.PrimaryWeapon;
        if (weapon is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(weapon.FireSoundName))
        {
            return weapon.FireSoundName;
        }

        var behaviorId = player.IsAcquiredWeaponEquipped
            ? player.AcquiredBehaviorId
            : player.IsExperimentalOffhandSelected
                ? player.EquippedBehaviorId ?? player.SecondaryBehaviorId ?? player.UtilityBehaviorId
                : player.PrimaryBehaviorId;
        if (!string.IsNullOrWhiteSpace(behaviorId)
            && CharacterClassCatalog.RuntimeRegistry.TryGetPrimaryWeaponBinding(behaviorId, out var binding)
            && !string.IsNullOrWhiteSpace(binding.FireSoundName))
        {
            return binding.FireSoundName;
        }

        // Quote Curly's primary blade is the bubble weapon. Its secondary
        // ability owns the blade throw cue, so the broad Blade kind fallback
        // must not invent BladeSnd for the primary action.
        if (weapon.Kind == PrimaryWeaponKind.Blade
            && string.Equals(weapon.ItemId, "plugin.quote-curly.weapon.blade", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return weapon.Kind switch
        {
            PrimaryWeaponKind.PelletGun => "ShotgunSnd",
            PrimaryWeaponKind.FlameThrower => "FlamethrowerSnd",
            PrimaryWeaponKind.RocketLauncher => "RocketSnd",
            PrimaryWeaponKind.MineLauncher => "MinegunSnd",
            PrimaryWeaponKind.Minigun => "ChaingunSnd",
            PrimaryWeaponKind.Rifle => "SniperSnd",
            PrimaryWeaponKind.Revolver => "RevolverSnd",
            PrimaryWeaponKind.Blade => "BladeSnd",
            _ => null,
        };
    }

    public void BeginExplosionSoundDeduplicationFrame()
    {
        _playedExplosionSoundsThisFrame.Clear();
    }

    public bool HasPlayedExplosionSoundThisFrame(float x, float y)
    {
        const float epsilon = 0.01f;
        for (var index = 0; index < _playedExplosionSoundsThisFrame.Count; index += 1)
        {
            var played = _playedExplosionSoundsThisFrame[index];
            if (MathF.Abs(played.X - x) <= epsilon
                && MathF.Abs(played.Y - y) <= epsilon)
            {
                return true;
            }
        }

        return false;
    }

    public void RecordPlayedExplosionSoundThisFrame(float x, float y)
    {
        _playedExplosionSoundsThisFrame.Add(new PlayedExplosionSoundThisFrame(x, y));
    }

    public void EnqueuePendingBrowserSoundEvent(string soundName, float x, float y)
    {
        if (!OperatingSystem.IsBrowser() || string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        while (_pendingBrowserSoundEvents.Count >= BrowserPendingSoundEventLimit)
        {
            _pendingBrowserSoundEvents.RemoveAt(0);
        }

        _pendingBrowserSoundEvents.Add(new PendingBrowserSoundEvent(soundName, x, y, BrowserPendingSoundEventLifetimeTicks));
    }

    private void ResetPendingBrowserSoundEvents()
    {
        _pendingBrowserSoundEvents.Clear();
    }

    private static bool IsGibSoundEvent(WorldSoundEvent soundEvent)
    {
        return string.Equals(soundEvent.SoundName, "Gibbing", StringComparison.OrdinalIgnoreCase);
    }

    public void AdvanceRecentGibSoundEvents()
    {
        for (var index = _recentGibSoundEvents.Count - 1; index >= 0; index -= 1)
        {
            _recentGibSoundEvents[index].TicksRemaining -= 1;
            if (_recentGibSoundEvents[index].TicksRemaining <= 0)
            {
                _recentGibSoundEvents.RemoveAt(index);
            }
        }
    }

    public bool ShouldSuppressPredictedGibSoundEcho(WorldSoundEvent soundEvent)
    {
        if (!IsGibSoundEvent(soundEvent))
        {
            return false;
        }

        var isNetworkEvent = soundEvent.EventId != 0;
        for (var index = 0; index < _recentGibSoundEvents.Count; index += 1)
        {
            var recent = _recentGibSoundEvents[index];
            if (recent.IsNetworkEvent == isNetworkEvent)
            {
                continue;
            }

            var deltaX = soundEvent.X - recent.X;
            var deltaY = soundEvent.Y - recent.Y;
            if ((deltaX * deltaX) + (deltaY * deltaY) <= RecentGibSoundEchoDistanceSquared)
            {
                return true;
            }
        }

        return false;
    }

    public void RememberPlayedGibSound(WorldSoundEvent soundEvent)
    {
        if (!IsGibSoundEvent(soundEvent))
        {
            return;
        }

        while (_recentGibSoundEvents.Count >= RecentGibSoundEchoLimit)
        {
            _recentGibSoundEvents.RemoveAt(0);
        }

        _recentGibSoundEvents.Add(new RecentGibSoundEvent(
            soundEvent.X,
            soundEvent.Y,
            soundEvent.EventId != 0,
            RecentGibSoundEchoLifetimeTicks));
    }

    private void ResetRecentGibSoundEvents()
    {
        _recentGibSoundEvents.Clear();
    }

    private static string NormalizeProjectileSoundEchoName(string soundName)
    {
        return string.Equals(soundName, "HealExplosionSnd", StringComparison.OrdinalIgnoreCase)
            ? "ExplosionSnd"
            : soundName;
    }

    internal static bool IsProjectileSoundEchoCandidate(string soundName)
    {
        return string.Equals(soundName, "FlareImpactSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "ExplosionSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "HealExplosionSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "RocketSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "DirecthitSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "MinegunSnd", StringComparison.OrdinalIgnoreCase)
            || IsWeaponFireSoundName(soundName);
    }

    private static bool IsManagedRapidFireSoundName(string soundName)
    {
        return string.Equals(soundName, "ChaingunSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "FlamethrowerSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "MedigunSnd", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedRapidFirePresentation(PlayerEntity player, string soundName)
    {
        if (!IsManagedRapidFireSoundName(soundName))
        {
            return false;
        }

        var weapon = player.IsAcquiredWeaponEquipped
            ? player.AcquiredWeapon
            : player.IsExperimentalOffhandSelected
                ? player.ExperimentalOffhandWeapon
                : player.PrimaryWeapon;
        if (weapon is null)
        {
            return false;
        }

        return IsManagedRapidFirePresentationForWeapon(weapon.Kind, soundName);
    }

    internal static bool IsManagedRapidFirePresentationForWeapon(PrimaryWeaponKind weaponKind, string soundName)
    {
        return soundName.Equals("ChaingunSnd", StringComparison.OrdinalIgnoreCase)
            ? weaponKind == PrimaryWeaponKind.Minigun
            : soundName.Equals("FlamethrowerSnd", StringComparison.OrdinalIgnoreCase)
                ? weaponKind == PrimaryWeaponKind.FlameThrower
                : soundName.Equals("MedigunSnd", StringComparison.OrdinalIgnoreCase)
                    && weaponKind == PrimaryWeaponKind.Medigun;
    }

    private static float GetProjectileSoundEchoDistanceSquared(string soundName)
    {
        return string.Equals(NormalizeProjectileSoundEchoName(soundName), "ExplosionSnd", StringComparison.OrdinalIgnoreCase)
            ? RecentProjectileExplosionSoundEchoDistanceSquared
            : RecentProjectileFireSoundEchoDistanceSquared;
    }

    internal static bool AreProjectileSoundEchoSourcesCompatible(
        string normalizedSoundName,
        int recentSourcePlayerId,
        int currentSourcePlayerId)
    {
        // Explosion sounds can be emitted by world objects and older
        // snapshots do not always carry an owner. Position remains the only
        // reliable correlation key for those events. Weapon-fire sounds are
        // different: suppressing solely by sound name and position can eat a
        // nearby remote player's legitimate shot when it happens to match the
        // locally predicted weapon. Require both events to identify the same
        // player, and prefer the duplicate over suppressing an unrelated shot
        // when either side lacks an identity.
        if (!IsWeaponFireSoundName(normalizedSoundName))
        {
            return true;
        }

        return recentSourcePlayerId >= 0
            && currentSourcePlayerId >= 0
            && recentSourcePlayerId == currentSourcePlayerId;
    }

    public void AdvanceRecentProjectileSoundEvents()
    {
        for (var index = _recentProjectileSoundEvents.Count - 1; index >= 0; index -= 1)
        {
            _recentProjectileSoundEvents[index].TicksRemaining -= 1;
            if (_recentProjectileSoundEvents[index].TicksRemaining <= 0)
            {
                _recentProjectileSoundEvents.RemoveAt(index);
            }
        }
    }

    public void AdvanceLowPriorityWorldSoundThrottle()
    {
        _lowPriorityWorldSoundsPlayedThisFrame = 0;
        for (var index = _recentLowPriorityWorldSoundEvents.Count - 1; index >= 0; index -= 1)
        {
            _recentLowPriorityWorldSoundEvents[index].TicksRemaining -= 1;
            if (_recentLowPriorityWorldSoundEvents[index].TicksRemaining <= 0)
            {
                _recentLowPriorityWorldSoundEvents.RemoveAt(index);
            }
        }
    }

    public void AdvanceLocalWeaponSoundFocus()
    {
        if (_localWeaponSoundFocusRemainingSeconds <= 0f)
        {
            _localWeaponSoundFocusRemainingSeconds = 0f;
            return;
        }

        _localWeaponSoundFocusRemainingSeconds = Math.Max(
            0f,
            _localWeaponSoundFocusRemainingSeconds - Math.Max(0f, _clientUpdateElapsedSeconds));
    }

    private void TriggerLocalWeaponSoundFocus()
    {
        _localWeaponSoundFocusRemainingSeconds = Math.Max(
            _localWeaponSoundFocusRemainingSeconds,
            LocalWeaponSoundFocusDurationSeconds);
    }

    public void TriggerLocalConfirmedWeaponFireFeedback(string soundName, WorldSoundEvent soundEvent)
    {
        if (!IsLocalPlayerSoundSource(soundEvent.SourcePlayerId) || !IsWeaponFireSoundName(soundName))
        {
            return;
        }

        TriggerLocalWeaponSoundFocus();
    }

    private static bool IsLowPriorityWorldSoundName(string soundName)
    {
        return string.Equals(soundName, "JumpSnd", StringComparison.OrdinalIgnoreCase);
    }

    private static float GetLowPriorityWorldSoundThrottleDistanceSquared(string soundName)
    {
        return string.Equals(soundName, "JumpSnd", StringComparison.OrdinalIgnoreCase)
            ? JumpSoundThrottleDistanceSquared
            : 0f;
    }

    public bool ShouldThrottleLowPriorityWorldSound(string resolvedSoundName, WorldSoundEvent soundEvent)
    {
        if (!IsLowPriorityWorldSoundName(resolvedSoundName) || IsLocalPlayerSoundSource(soundEvent.SourcePlayerId))
        {
            return false;
        }

        if (_lowPriorityWorldSoundsPlayedThisFrame >= LowPriorityWorldSoundFrameLimit)
        {
            return true;
        }

        var maxDistanceSquared = GetLowPriorityWorldSoundThrottleDistanceSquared(resolvedSoundName);
        for (var index = 0; index < _recentLowPriorityWorldSoundEvents.Count; index += 1)
        {
            var recent = _recentLowPriorityWorldSoundEvents[index];
            if (!string.Equals(recent.SoundName, resolvedSoundName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var deltaX = soundEvent.X - recent.X;
            var deltaY = soundEvent.Y - recent.Y;
            if ((deltaX * deltaX) + (deltaY * deltaY) <= maxDistanceSquared)
            {
                return true;
            }
        }

        return false;
    }

    public void RememberPlayedLowPriorityWorldSound(string resolvedSoundName, WorldSoundEvent soundEvent)
    {
        if (!IsLowPriorityWorldSoundName(resolvedSoundName) || IsLocalPlayerSoundSource(soundEvent.SourcePlayerId))
        {
            return;
        }

        _lowPriorityWorldSoundsPlayedThisFrame += 1;
        while (_recentLowPriorityWorldSoundEvents.Count >= LowPriorityWorldSoundThrottleLimit)
        {
            _recentLowPriorityWorldSoundEvents.RemoveAt(0);
        }

        _recentLowPriorityWorldSoundEvents.Add(new RecentLowPriorityWorldSoundEvent(
            resolvedSoundName,
            soundEvent.X,
            soundEvent.Y,
            LowPriorityWorldSoundThrottleLifetimeTicks));
    }

    public bool ShouldSuppressPredictedProjectileSoundEcho(string resolvedSoundName, WorldSoundEvent soundEvent)
    {
        if (!IsProjectileSoundEchoCandidate(resolvedSoundName))
        {
            return false;
        }

        var normalizedSoundName = NormalizeProjectileSoundEchoName(resolvedSoundName);
        var isNetworkEvent = soundEvent.EventId != 0;
        var maxDistanceSquared = GetProjectileSoundEchoDistanceSquared(normalizedSoundName);
        for (var index = 0; index < _recentProjectileSoundEvents.Count; index += 1)
        {
            var recent = _recentProjectileSoundEvents[index];
            if (recent.IsNetworkEvent == isNetworkEvent
                || !string.Equals(recent.SoundName, normalizedSoundName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!AreProjectileSoundEchoSourcesCompatible(
                    normalizedSoundName,
                    recent.SourcePlayerId,
                    soundEvent.SourcePlayerId))
            {
                continue;
            }

            var deltaX = soundEvent.X - recent.X;
            var deltaY = soundEvent.Y - recent.Y;
            if ((deltaX * deltaX) + (deltaY * deltaY) <= maxDistanceSquared)
            {
                // An echo match is one-shot. Leaving it in the recent list can
                // swallow the next legitimate shot during sustained fire.
                _recentProjectileSoundEvents.RemoveAt(index);
                return true;
            }
        }

        return false;
    }

    public void RememberPlayedProjectileSound(string resolvedSoundName, WorldSoundEvent soundEvent)
    {
        if (!IsProjectileSoundEchoCandidate(resolvedSoundName))
        {
            return;
        }

        while (_recentProjectileSoundEvents.Count >= RecentProjectileSoundEchoLimit)
        {
            _recentProjectileSoundEvents.RemoveAt(0);
        }

        _recentProjectileSoundEvents.Add(new RecentProjectileSoundEvent(
            NormalizeProjectileSoundEchoName(resolvedSoundName),
            soundEvent.X,
            soundEvent.Y,
            soundEvent.EventId != 0,
            soundEvent.SourcePlayerId,
            RecentProjectileSoundEchoLifetimeTicks));
    }

    private void ResetRecentProjectileSoundEvents()
    {
        _recentProjectileSoundEvents.Clear();
    }

    private void ResetLowPriorityWorldSoundThrottle()
    {
        _recentLowPriorityWorldSoundEvents.Clear();
        _lowPriorityWorldSoundsPlayedThisFrame = 0;
    }

    public bool TryPlaySound(SoundEffect? sound, float volume, float pitch, float pan)
    {
        if (!_audioAvailable || sound is null)
        {
            return false;
        }

        try
        {
            return sound.Play(volume * GetSoundEffectsVolumeScale(), pitch, pan);
        }
        catch (Exception ex)
        {
            var message = $"{ex.GetType().Name}: {ex.Message}";
            if (!string.Equals(_lastOneShotSoundFailureMessage, message, StringComparison.Ordinal))
            {
                _lastOneShotSoundFailureMessage = message;
                AddConsoleLine($"sound skipped while playing one-shot ({message})");
            }

            return false;
        }
    }

    private void PlayDirectMessageNotificationSound()
    {
        TryPlaySound(_runtimeAssets.GetSound("MessageSnd"), 0.88f, 0f, 0f);
    }

    public void DisableAudio(string reason, Exception ex)
    {
        if (!_audioAvailable)
        {
            return;
        }

        _audioAvailable = false;
        _audioManager.RapidFire.StopAndDisposeRapidFireWeaponAudio();
        StopMenuMusic();
        StopLastToDieMenuMusic();
        StopFaucetMusic();
        StopIngameMusic();
        StopLastToDieIngameMusic();
        StopLastToDieGameOverSound();
        StopGameplaySoundMusicOverride();
        DisposeDynamicMusic();
        _menuMusicInstance?.Dispose();
        _menuMusicInstance = null;
        _menuMusic?.Dispose();
        _menuMusic = null;
        _lastToDieMenuMusicInstance?.Dispose();
        _lastToDieMenuMusicInstance = null;
        _lastToDieMenuMusic?.Dispose();
        _lastToDieMenuMusic = null;
        _faucetMusicInstance?.Dispose();
        _faucetMusicInstance = null;
        _faucetMusic?.Dispose();
        _faucetMusic = null;
        _ingameMusicInstance?.Dispose();
        _ingameMusicInstance = null;
        _ingameMusic?.Dispose();
        _ingameMusic = null;
        _ingameCombatMusicInstance?.Dispose();
        _ingameCombatMusicInstance = null;
        _ingameCombatMusic?.Dispose();
        _ingameCombatMusic = null;
        _lastToDieIngameMusicInstance?.Dispose();
        _lastToDieIngameMusicInstance = null;
        _lastToDieIngameMusic?.Dispose();
        _lastToDieIngameMusic = null;
        _lastToDieGameOverSoundInstance?.Dispose();
        _lastToDieGameOverSoundInstance = null;
        _lastToDieGameOverSound?.Dispose();
        _lastToDieGameOverSound = null;
        AddConsoleLine($"audio disabled: {reason} ({ex.GetType().Name}: {ex.Message})");
    }

    private static string? FindLoopedMusicPath(string relativePath)
    {
        return GameplayAudioMusicController.FindLoopedMusicPath(relativePath);
    }

    public bool AllowsMenuMusic()
    {
        return _musicMode is MusicMode.MenuOnly or MusicMode.MenuAndInGame;
    }

    public bool AllowsIngameMusic()
    {
        return _musicMode is MusicMode.InGameOnly or MusicMode.MenuAndInGame;
    }

    private void UpdateLocalRapidFireWeaponAudio()
    {
        _audioManager.RapidFire.UpdateLocalRapidFireWeaponAudio();
    }

    public void ToggleAudioMute()
    {
        _audioMuted = !_audioMuted;
        ApplyAudioVolumeState();
        AddConsoleLine(_audioMuted ? "audio muted (F12)" : "audio unmuted (F12)");
    }

    public void ApplyAudioVolumeState()
    {
        ApplyAudioMuteState();
        UpdateCurrentMusicInstanceVolumes();
    }

    public void ApplyAudioMuteState()
    {
        try
        {
            SoundEffect.MasterVolume = _audioMuted ? 0f : GetNonLinearVolumeScale(_masterVolumePercent);
        }
        catch (Exception ex)
        {
            DisableAudio("updating audio mute", ex);
        }
    }

    private void UpdateCurrentMusicInstanceVolumes()
    {
        SetSoundEffectInstanceVolume(_menuMusicInstance, GetNonLinearVolumeScale(_menuMusicVolumePercent) * 0.8f);
        SetSoundEffectInstanceVolume(_lastToDieMenuMusicInstance, GetNonLinearVolumeScale(_menuMusicVolumePercent) * 0.82f);
        SetSoundEffectInstanceVolume(_faucetMusicInstance, GetNonLinearVolumeScale(_menuMusicVolumePercent) * 0.8f);
        var ingameMusicVolume = GetNonLinearVolumeScale(_ingameMusicVolumePercent) * (IsJukeboxAudible ? 0f : 1f);
        var gameplaySoundUnderlyingScale = GetGameplaySoundUnderlyingMusicVolumeScale();
        SetSoundEffectInstanceVolume(_ingameMusicInstance, ingameMusicVolume * 0.8f * _dynamicNormalMusicFade * gameplaySoundUnderlyingScale);
        SetSoundEffectInstanceVolume(_lastToDieIngameMusicInstance, ingameMusicVolume * 0.82f * gameplaySoundUnderlyingScale);
        SetSoundEffectInstanceVolume(_gameplaySoundMusicOverrideInstance, GetGameplaySoundMusicOverrideVolume() * (IsJukeboxAudible ? 0f : 1f));
        SetSoundEffectInstanceVolume(_lastToDieGameOverSoundInstance, ingameMusicVolume * 0.85f);
        UpdateDynamicMusicInstanceVolumes(ingameMusicVolume * gameplaySoundUnderlyingScale);
    }

    private static float GetNonLinearVolumeScale(int percent)
    {
        return Math.Clamp(MathF.Pow(percent / 100f, 1.5f), 0f, 1f);
    }

    private static void SetSoundEffectInstanceVolume(SoundEffectInstance? instance, float volume)
    {
        if (instance is null)
        {
            return;
        }

        try
        {
            instance.Volume = Math.Clamp(volume, 0f, 1f);
        }
        catch
        {
        }
    }

    public float GetSoundEffectsVolumeScale()
    {
        return _audioMuted ? 0f : GetNonLinearVolumeScale(_soundEffectsVolumePercent);
    }

    private bool IsLocalRapidFireWeaponSoundActive(PrimaryWeaponKind weaponKind)
    {
        return _audioManager.RapidFire.IsLocalRapidFireWeaponSoundActive(weaponKind);
    }

    public bool ShouldSuppressManagedRapidFireSound(WorldSoundEvent soundEvent)
    {
        return _audioManager.RapidFire.ShouldSuppressManagedRapidFireSound(soundEvent);
    }

    public Vector2 GetWorldSoundListenerPosition()
    {
        if (_hasGameplayCameraTopLeft)
        {
            return _gameplayCameraTopLeft + new Vector2(ViewportWidth * 0.5f, ViewportHeight * 0.5f);
        }

        return new Vector2(_world.LocalPlayer.X, _world.LocalPlayer.Y);
    }

    public (float Volume, float Pan) GetWorldSoundMix(float worldX, float worldY)
    {
        return _audioManager.RapidFire.GetWorldSoundMix(worldX, worldY);
    }

    internal static (float Volume, float Pan) GetBannerSoundMix(float worldX, float worldY, Vector2 listenerPosition)
        => GameplayRapidFireAudioController.GetBannerSoundMix(worldX, worldY, listenerPosition);

    internal static (float Volume, float Pan) GetFlareImpactSoundMix(float worldX, float worldY, Vector2 listenerPosition)
    {
        var mix = GameplayRapidFireAudioController.GetWorldSoundMix(worldX, worldY, listenerPosition);
        return (mix.Volume * 0.5f, mix.Pan);
    }

    public (float Volume, float Pan) GetWorldSoundMix(WorldSoundEvent soundEvent)
    {
        if (string.Equals(soundEvent.SoundName, "FlareImpactSnd", StringComparison.OrdinalIgnoreCase))
        {
            return GetFlareImpactSoundMix(soundEvent.X, soundEvent.Y, GetWorldSoundListenerPosition());
        }

        if (string.Equals(soundEvent.SoundName, "BuffbannerSnd", StringComparison.OrdinalIgnoreCase))
        {
            return GetBannerSoundMix(
                soundEvent.X,
                soundEvent.Y,
                GetWorldSoundListenerPosition());
        }

        var (volume, pan) = GetWorldSoundMix(soundEvent.X, soundEvent.Y);
        if (!IsWeaponFireSoundName(soundEvent.SoundName))
        {
            if (IsHealingCabinetSoundName(soundEvent.SoundName) && !IsLocalPlayerSoundSource(soundEvent.SourcePlayerId))
            {
                volume *= RemoteHealingCabinetSoundVolumeMultiplier;
            }

            return _localWeaponSoundFocusRemainingSeconds > 0f && !IsLocalPlayerSoundSource(soundEvent.SourcePlayerId)
                ? (volume * FocusedOtherWorldSoundVolumeMultiplier, pan)
                : (volume, pan);
        }

        if (IsLocalPlayerSoundSource(soundEvent.SourcePlayerId))
        {
            return (
                Math.Clamp(Math.Max(volume, LocalWeaponSoundMinimumVolume) * LocalWeaponSoundVolumeMultiplier, 0f, 1f),
                Math.Clamp(pan * LocalWeaponSoundPanMultiplier, -1f, 1f));
        }

        var remoteMultiplier = RemoteWeaponSoundVolumeMultiplier;
        if (_localWeaponSoundFocusRemainingSeconds > 0f)
        {
            remoteMultiplier *= FocusedRemoteWeaponSoundVolumeMultiplier;
        }

        return (volume * remoteMultiplier, pan);
    }

    public (float Volume, float Pan) GetLoopedWorldSoundMix(string soundName, float worldX, float worldY, bool isLocalSource)
    {
        var (volume, pan) = GetWorldSoundMix(worldX, worldY);
        if (!IsWeaponFireSoundName(soundName))
        {
            return (volume, pan);
        }

        if (isLocalSource)
        {
            return (
                Math.Clamp(Math.Max(volume, LocalWeaponSoundMinimumVolume) * LocalWeaponSoundVolumeMultiplier, 0f, 1f),
                Math.Clamp(pan * LocalWeaponSoundPanMultiplier, -1f, 1f));
        }

        return (volume * RemoteWeaponSoundVolumeMultiplier, pan);
    }

    public bool IsLocalPlayerSoundSource(int sourcePlayerId)
    {
        return sourcePlayerId >= 0 && sourcePlayerId == GetResolvedLocalPlayerId();
    }

    private static bool IsHealingCabinetSoundName(string soundName)
    {
        return string.Equals(soundName, "CbntHealSnd", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWeaponFireSoundName(string soundName)
    {
        return string.Equals(soundName, "PistolSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "ShotgunSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "RifleSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "RocketSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "DirecthitSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "MinegunSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "RevolverSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "SniperSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "BowSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "MedichaingunSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "ChaingunSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "FlamethrowerSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "FlaregunSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "MedigunSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "BladeSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "EyelanderSnd", StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, WhippingCordCatalog.AttackSoundName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(soundName, "KnifeSnd", StringComparison.OrdinalIgnoreCase);
    }

    public void StopLocalRapidFireWeaponAudio()
    {
        _audioManager.RapidFire.StopRapidFireWeaponAudio();
    }
}
