#nullable enable

using System;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Sounds about the local player's own state, heard only by them: landing, shotgun shell
/// reloads, "weapon ready" cues after a finished reload or a weapon switch, the pistol's
/// mid-reload click and pain from heavy single hits. Plus the death voice choice, which
/// every client makes for itself.
/// </summary>
/// <remarks>
/// Weapon cues are edge-detected once per frame from the same predicted presentation
/// state the weapon animations use (<see cref="GetRenderWeaponAmmoCount"/> and friends),
/// so they line up with what is on screen and need no new network events.
/// </remarks>
public partial class Game1
{
    public const string LandingSoundName = "LandingSnd";
    public const string ShotgunShellReloadSoundName = "ShotgunShellReloadSnd";
    public const string ShotgunReadySoundName = "ShotgunReadySnd";
    public const string SmgReadySoundName = "SMGReadySnd";
    public const string FlareReadySoundName = "FlareReadySnd";
    public const string PistolReadySoundName = "PistolReadySnd";
    public const string PistolHalfReloadSoundName = "PistolHalfReloadSnd";
    public const string NailgunReloadSoundName = "NailReloadSnd";
    public const string HeavyPainSoundName = "PlayerHeavyPainSnd";
    public const string VeryHeavyPainSoundName = "PlayerVeryHeavyPainSnd";
    public const string FunnyDeathSoundName = "PlayerFunnyDieSnd";

    public const int HeavyPainDamageThreshold = 20;
    public const int VeryHeavyPainDamageThreshold = 50;

    /// <summary>Chance that a player whose body is sent flying gets the funny death voice.</summary>
    public const float FunnyDeathChance = 0.2f;

    /// <summary>Corpse launch speed (world px per tick) that counts as "sent flying".</summary>
    public const float FlyingCorpseSpeed = 7.5f;

    private const float LocalCueVolume = 0.9f;
    private const float LandingCueVolume = 0.8f;
    private const float LandingMinimumAirborneSeconds = 0.15f;
    private const float WeaponReadyCueDelaySeconds = 0.12f;
    private const float PistolHalfReloadProgress = 0.75f;
    private const float PainCueCooldownSeconds = 0.35f;
    private const float DeathVoiceCorpseSearchDistanceSquared = 64f * 64f;

    private const DamageEventFlags PainIgnoredDamageFlags =
        DamageEventFlags.AfterburnTick
        | DamageEventFlags.StatusTick
        | DamageEventFlags.Evaded
        | DamageEventFlags.GhostDash
        | DamageEventFlags.CivvieUmbrellaBlock;

    public enum LocalWeaponCueKind
    {
        None,
        Shotgun,
        Smg,
        Flare,
        Pistol,
        Nailgun,
    }

    private bool _localCueTracking;
    private int _localCuePlayerId = -1;
    private PlayerClass _localCueClassId;
    private string _localCueWeaponKey = string.Empty;
    private int _localCuePreviousAmmo;
    private int _localCuePreviousReloadTicks;
    private int _localCueReloadCycleTicks;
    private bool _localCueHalfReloadPlayed;
    private string? _localCuePendingReadySound;
    private float _localCuePendingReadyDelaySeconds;
    private bool _localCueWasAirborne;
    private float _localCueAirborneSeconds;
    private float _localPainCueCooldownSeconds;

    private void UpdateLocalPlayerAudioCues(float elapsedSeconds)
    {
        elapsedSeconds = Math.Clamp(elapsedSeconds, 0f, 0.1f);
        _localPainCueCooldownSeconds = MathF.Max(0f, _localPainCueCooldownSeconds - elapsedSeconds);
        var player = _world.LocalPlayer;
        if (!_audioAvailable || _runtimeAssets is null || !player.IsAlive || _networkClient.IsSpectator)
        {
            _localCueTracking = false;
            _localCuePendingReadySound = null;
            return;
        }

        // A fresh life or a class change starts silently: spawning with a weapon is not "switching to" it.
        if (!_localCueTracking || _localCuePlayerId != player.Id || _localCueClassId != player.ClassId)
        {
            BeginLocalPlayerAudioCueTracking(player);
            return;
        }

        UpdateLocalLandingCue(player, elapsedSeconds);
        UpdateLocalWeaponCues(player, elapsedSeconds);
    }

    private void BeginLocalPlayerAudioCueTracking(PlayerEntity player)
    {
        _localCueTracking = true;
        _localCuePlayerId = player.Id;
        _localCueClassId = player.ClassId;
        _localCueWeaponKey = GetLocalWeaponCueKey(player);
        _localCuePreviousAmmo = GetRenderWeaponAmmoCount(player);
        _localCuePreviousReloadTicks = GetRenderWeaponReloadTicks(player);
        _localCueReloadCycleTicks = _localCuePreviousReloadTicks;
        _localCueHalfReloadPlayed = false;
        _localCuePendingReadySound = null;
        _localCueWasAirborne = !GetPlayerRenderIsGrounded(player);
        _localCueAirborneSeconds = 0f;
    }

    /// <summary>Landing replaces the old jump sound. Short hops (stairs, slope seams) stay silent.</summary>
    private void UpdateLocalLandingCue(PlayerEntity player, float elapsedSeconds)
    {
        if (!GetPlayerRenderIsGrounded(player))
        {
            _localCueWasAirborne = true;
            _localCueAirborneSeconds += elapsedSeconds;
            return;
        }

        if (_localCueWasAirborne && _localCueAirborneSeconds >= LandingMinimumAirborneSeconds)
        {
            PlayLocalCue(LandingSoundName, LandingCueVolume);
        }

        _localCueWasAirborne = false;
        _localCueAirborneSeconds = 0f;
    }

    private void UpdateLocalWeaponCues(PlayerEntity player, float elapsedSeconds)
    {
        if (_localCuePendingReadySound is not null)
        {
            _localCuePendingReadyDelaySeconds -= elapsedSeconds;
            if (_localCuePendingReadyDelaySeconds <= 0f)
            {
                PlayLocalCue(_localCuePendingReadySound, LocalCueVolume);
                _localCuePendingReadySound = null;
            }
        }

        var weapon = GetRenderWeaponStats(player);
        var kind = ResolveLocalWeaponCueKind(weapon);
        var ammo = GetRenderWeaponAmmoCount(player);
        var maxAmmo = Math.Max(1, GetRenderWeaponMaxShells(player));
        var reloadTicks = GetRenderWeaponReloadTicks(player);
        var weaponKey = GetLocalWeaponCueKey(player);
        if (!string.Equals(weaponKey, _localCueWeaponKey, StringComparison.Ordinal))
        {
            // Switched weapons: cue it if it can fire right away; otherwise the cue waits for its reload.
            _localCueWeaponKey = weaponKey;
            _localCuePendingReadySound = null;
            _localCueHalfReloadPlayed = false;
            _localCueReloadCycleTicks = reloadTicks;
            if (ammo >= Math.Max(1, weapon.AmmoPerShot) && GetLocalWeaponSwitchReadySound(kind) is { } switchCue)
            {
                PlayLocalCue(switchCue, LocalCueVolume);
            }

            _localCuePreviousAmmo = ammo;
            _localCuePreviousReloadTicks = reloadTicks;
            return;
        }

        // A shot (or a reload timer starting from idle) begins a new reload cycle.
        if (ammo < _localCuePreviousAmmo || (_localCuePreviousReloadTicks <= 0 && reloadTicks > 0))
        {
            _localCueReloadCycleTicks = reloadTicks;
            _localCueHalfReloadPlayed = false;
        }
        else if (reloadTicks > _localCueReloadCycleTicks)
        {
            _localCueReloadCycleTicks = reloadTicks;
        }

        if (kind == LocalWeaponCueKind.Pistol
            && !_localCueHalfReloadPlayed
            && ammo < maxAmmo
            && reloadTicks > 0
            && _localCueReloadCycleTicks > 0
            && 1f - (reloadTicks / (float)_localCueReloadCycleTicks) >= PistolHalfReloadProgress)
        {
            _localCueHalfReloadPlayed = true;
            PlayLocalCue(PistolHalfReloadSoundName, LocalCueVolume);
        }

        if (ammo > _localCuePreviousAmmo)
        {
            var reloadFinished = ammo >= maxAmmo;
            if (kind == LocalWeaponCueKind.Shotgun)
            {
                if (ammo - _localCuePreviousAmmo == 1)
                {
                    PlayLocalCue(ShotgunShellReloadSoundName, LocalCueVolume);
                }

                if (reloadFinished)
                {
                    // Let the last shell land before the "ready" cue.
                    _localCuePendingReadySound = ShotgunReadySoundName;
                    _localCuePendingReadyDelaySeconds = WeaponReadyCueDelaySeconds;
                }
            }
            else if (reloadFinished && GetLocalWeaponReloadReadySound(kind) is { } readyCue)
            {
                PlayLocalCue(readyCue, LocalCueVolume);
            }
        }

        _localCuePreviousAmmo = ammo;
        _localCuePreviousReloadTicks = reloadTicks;
    }

    /// <summary>Active weapon slot plus item, so swap-station primaries count as switches too.</summary>
    private string GetLocalWeaponCueKey(PlayerEntity player) =>
        $"{GetActiveWeaponTag(player) ?? "primary"}|{GetRenderWeaponStats(player).ItemId ?? GetRenderWeaponStats(player).DisplayName}";

    internal static LocalWeaponCueKind ResolveLocalWeaponCueKind(PrimaryWeaponDefinition weapon)
    {
        // The nailgun's fire sound is emitted by its executor, not its definition.
        if (weapon.ItemId?.Contains("nailgun", StringComparison.OrdinalIgnoreCase) == true)
        {
            return LocalWeaponCueKind.Nailgun;
        }

        var sound = weapon.FireSoundName;
        if (string.IsNullOrWhiteSpace(sound))
        {
            return weapon.Kind == PrimaryWeaponKind.PelletGun ? LocalWeaponCueKind.Shotgun : LocalWeaponCueKind.None;
        }

        if (sound.Equals("ShotgunSnd", StringComparison.OrdinalIgnoreCase)
            || sound.Equals("ScattergunSnd", StringComparison.OrdinalIgnoreCase))
        {
            return LocalWeaponCueKind.Shotgun;
        }

        if (sound.Equals("SMGSnd", StringComparison.OrdinalIgnoreCase)
            || sound.Equals("TommygunSnd", StringComparison.OrdinalIgnoreCase))
        {
            return LocalWeaponCueKind.Smg;
        }

        if (sound.Equals("FlaregunSnd", StringComparison.OrdinalIgnoreCase))
        {
            return LocalWeaponCueKind.Flare;
        }

        return sound.Equals("PistolSnd", StringComparison.OrdinalIgnoreCase)
            ? LocalWeaponCueKind.Pistol
            : LocalWeaponCueKind.None;
    }

    internal static string? GetLocalWeaponSwitchReadySound(LocalWeaponCueKind kind) => kind switch
    {
        LocalWeaponCueKind.Shotgun => ShotgunReadySoundName,
        LocalWeaponCueKind.Smg => SmgReadySoundName,
        LocalWeaponCueKind.Flare => FlareReadySoundName,
        LocalWeaponCueKind.Pistol => PistolReadySoundName,
        _ => null,
    };

    internal static string? GetLocalWeaponReloadReadySound(LocalWeaponCueKind kind) => kind switch
    {
        LocalWeaponCueKind.Nailgun => NailgunReloadSoundName,
        _ => GetLocalWeaponSwitchReadySound(kind),
    };

    /// <summary>Pain from one heavy hit on the local player (&gt;20 and &gt;50 damage).</summary>
    public void ObserveLocalPainDamage(int damageAmount, bool wasFatal, DamageEventFlags flags)
    {
        var soundName = ResolveLocalPainSoundName(damageAmount, wasFatal, flags);
        if (soundName is null || _localPainCueCooldownSeconds > 0f || !_world.LocalPlayer.IsAlive)
        {
            return;
        }

        // Several hits can report in one frame (and through both the local and network
        // paths); one grunt is enough.
        if (PlayLocalCue(soundName, LocalCueVolume))
        {
            _localPainCueCooldownSeconds = PainCueCooldownSeconds;
        }
    }

    internal static string? ResolveLocalPainSoundName(int damageAmount, bool wasFatal, DamageEventFlags flags)
    {
        if (wasFatal || damageAmount <= HeavyPainDamageThreshold || (flags & PainIgnoredDamageFlags) != 0)
        {
            return null;
        }

        return damageAmount > VeryHeavyPainDamageThreshold ? VeryHeavyPainSoundName : HeavyPainSoundName;
    }

    internal static bool IsPlayerDeathVoiceSoundName(string soundName) =>
        soundName.Equals("DeathSnd1", StringComparison.OrdinalIgnoreCase)
        || soundName.Equals("DeathSnd2", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Death voice for a non-gib death: the regular death cry, or now and then the funny one
    /// when the body is launched hard. Rolled on each client; it is presentation only.
    /// </summary>
    public string ResolvePlayerDeathVoiceSoundName(WorldSoundEvent soundEvent)
    {
        return IsCorpseFlyingNear(soundEvent.X, soundEvent.Y) && Random.Shared.NextSingle() < FunnyDeathChance
            ? FunnyDeathSoundName
            : soundEvent.SoundName;
    }

    private bool IsCorpseFlyingNear(float x, float y)
    {
        DeadBodyEntity? nearest = null;
        var nearestDistanceSquared = DeathVoiceCorpseSearchDistanceSquared;
        var bodies = _world.DeadBodies;
        for (var index = 0; index < bodies.Count; index += 1)
        {
            var body = bodies[index];
            var deltaX = body.X - x;
            var deltaY = body.Y - y;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);
            if (distanceSquared <= nearestDistanceSquared)
            {
                nearest = body;
                nearestDistanceSquared = distanceSquared;
            }
        }

        return nearest is not null && IsFlyingCorpseSpeed(nearest.HorizontalSpeed, nearest.VerticalSpeed);
    }

    internal static bool IsFlyingCorpseSpeed(float horizontalSpeed, float verticalSpeed) =>
        (horizontalSpeed * horizontalSpeed) + (verticalSpeed * verticalSpeed) >= FlyingCorpseSpeed * FlyingCorpseSpeed;

    private bool PlayLocalCue(string soundName, float volume)
    {
        return _runtimeAssets is not null && TryPlaySound(_runtimeAssets.GetSound(soundName), volume, 0f, 0f);
    }
}
