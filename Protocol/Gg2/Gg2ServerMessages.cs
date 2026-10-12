using System;
using System.Collections.Generic;

namespace OpenGarrison.Protocol.Gg2;

/// <summary>A message a GG2 server sends, as GG2's ClientBeginStep reads it.</summary>
public abstract record Gg2ServerMessage(byte Type);

/// <summary>HELLO: the server's name, map, map MD5 (empty for internal maps) and plugins.</summary>
public sealed record Gg2ServerHello(string ServerName, string MapName, string MapMd5, bool PluginsRequired, string PluginList)
    : Gg2ServerMessage(Gg2Protocol.Hello);

/// <summary>A message with no payload (RESERVE_SLOT, SERVER_FULL, PASSWORD_REQUEST, ARENA_RESTART, ...).</summary>
public sealed record Gg2SignalMessage(byte SignalType) : Gg2ServerMessage(SignalType);

public sealed record Gg2KickMessage(byte Reason) : Gg2ServerMessage(Gg2Protocol.Kick);

public sealed record Gg2JoinUpdateMessage(byte LocalPlayerIndex, byte MapArea) : Gg2ServerMessage(Gg2Protocol.JoinUpdate);

public sealed record Gg2ChangeMapMessage(string MapName, string MapMd5) : Gg2ServerMessage(Gg2Protocol.ChangeMap)
{
    /// <summary>An internal (stock) map has no MD5; an external map is a downloadable PNG.</summary>
    public bool IsInternalMap => MapMd5.Length == 0;
}

public sealed record Gg2PlayerJoinMessage(string Name) : Gg2ServerMessage(Gg2Protocol.PlayerJoin);

public sealed record Gg2PlayerLeaveMessage(byte PlayerIndex) : Gg2ServerMessage(Gg2Protocol.PlayerLeave);

public sealed record Gg2PlayerChangeTeamMessage(byte PlayerIndex, byte Team) : Gg2ServerMessage(Gg2Protocol.PlayerChangeTeam);

public sealed record Gg2PlayerChangeClassMessage(byte PlayerIndex, byte ClassId) : Gg2ServerMessage(Gg2Protocol.PlayerChangeClass);

public sealed record Gg2PlayerChangeNameMessage(byte PlayerIndex, string Name) : Gg2ServerMessage(Gg2Protocol.PlayerChangeName);

/// <summary>PLAYER_SPAWN: the spawn point index within the team's list for that spawn group.</summary>
public sealed record Gg2PlayerSpawnMessage(byte PlayerIndex, byte SpawnPointId, byte SpawnGroup) : Gg2ServerMessage(Gg2Protocol.PlayerSpawn);

/// <summary>PLAYER_DEATH. Killer and assistant are <see cref="Gg2Protocol.NoPlayer"/> when absent.</summary>
public sealed record Gg2PlayerDeathMessage(byte Victim, byte Killer, byte Assistant, byte DamageSource) : Gg2ServerMessage(Gg2Protocol.PlayerDeath);

/// <summary>BALANCE: 255 alone announces balancing; otherwise the player switches team and takes this class.</summary>
public sealed record Gg2BalanceMessage(byte PlayerIndex, byte? NewClass) : Gg2ServerMessage(Gg2Protocol.Balance)
{
    public bool IsAnnouncement => PlayerIndex == Gg2Protocol.NoPlayer;
}

public sealed record Gg2ChatBubbleMessage(byte PlayerIndex, byte BubbleImage) : Gg2ServerMessage(Gg2Protocol.ChatBubble);

public sealed record Gg2BuildSentryMessage(byte PlayerIndex, ushort XTimes5, ushort YTimes5, sbyte Direction) : Gg2ServerMessage(Gg2Protocol.BuildSentry)
{
    public float X => XTimes5 / 5f;
    public float Y => YTimes5 / 5f;
}

public sealed record Gg2DestroySentryMessage(byte Owner, byte Killer, byte Assistant, byte DamageSource) : Gg2ServerMessage(Gg2Protocol.DestroySentry);

/// <summary>A one-player event: GRAB/SCORE/DROP_INTEL, UBER_CHARGED, UBER, OMNOMNOMNOM, TOGGLE_ZOOM.</summary>
public sealed record Gg2PlayerEventMessage(byte EventType, byte PlayerIndex) : Gg2ServerMessage(EventType);

public sealed record Gg2ReturnIntelMessage(byte Team) : Gg2ServerMessage(Gg2Protocol.ReturnIntel);

public sealed record Gg2GeneratorDestroyMessage(byte Team) : Gg2ServerMessage(Gg2Protocol.GeneratorDestroy);

public readonly record struct Gg2ArenaMvp(byte PlayerIndex, byte Kills, ushort Healing, byte Points);

public sealed record Gg2ArenaEndRoundMessage(
    byte Winner,
    byte RedWins,
    byte BlueWins,
    IReadOnlyList<Gg2ArenaMvp> RedMvps,
    IReadOnlyList<Gg2ArenaMvp> BlueMvps) : Gg2ServerMessage(Gg2Protocol.ArenaEndRound);

public sealed record Gg2MapEndMessage(string NextMap, byte Winners, byte MapArea) : Gg2ServerMessage(Gg2Protocol.MapEnd);

public sealed record Gg2RewardChallengeMessage(byte[] Challenge) : Gg2ServerMessage(Gg2Protocol.RewardChallengeCode);

public sealed record Gg2RewardUpdateMessage(byte PlayerIndex, string Rewards) : Gg2ServerMessage(Gg2Protocol.RewardUpdate);

public sealed record Gg2MessageStringMessage(string Text) : Gg2ServerMessage(Gg2Protocol.MessageString);

public sealed record Gg2SentryPositionMessage(byte PlayerIndex, ushort XTimes5, ushort YTimes5) : Gg2ServerMessage(Gg2Protocol.SentryPosition);

/// <summary>
/// WEAPON_FIRE: the shooter's exact position and speed at the shot, and the random seed
/// GG2 seeds the weapon's fire event with (doEventFireWeapon).
/// </summary>
public sealed record Gg2WeaponFireMessage(byte PlayerIndex, ushort XTimes5, ushort YTimes5, sbyte HorizontalSpeed85, sbyte VerticalSpeed85, ushort Seed)
    : Gg2ServerMessage(Gg2Protocol.WeaponFire)
{
    public float X => XTimes5 / 5f;
    public float Y => YTimes5 / 5f;
    public float HorizontalSpeed => HorizontalSpeed85 / 8.5f;
    public float VerticalSpeed => VerticalSpeed85 / 8.5f;
}

/// <summary>PLUGIN_PACKET: the plugin's packet id and its data.</summary>
public sealed record Gg2PluginPacketMessage(byte PluginId, byte[] Data) : Gg2ServerMessage(Gg2Protocol.PluginPacket);

public sealed record Gg2ClientSettingsMessage(byte PlayerIndex, byte QueueJump) : Gg2ServerMessage(Gg2Protocol.ClientSettings);

/// <summary>INPUTSTATE, QUICK_UPDATE, FULL_UPDATE or CAPS_UPDATE (GG2 serializeState).</summary>
public sealed record Gg2StateUpdateMessage(
    byte UpdateType,
    ushort TdmInvulnerabilityTicks,
    IReadOnlyList<Gg2PlayerState> Players,
    IReadOnlyList<Gg2PlatformState> Platforms,
    Gg2FullUpdateTail? Full,
    Gg2CapsUpdateTail? Caps) : Gg2ServerMessage(UpdateType)
{
    public bool IsInputState => UpdateType == Gg2Protocol.InputState;
    public bool IsQuickUpdate => UpdateType == Gg2Protocol.QuickUpdate;
    public bool IsFullUpdate => UpdateType == Gg2Protocol.FullUpdate;
    public bool IsCapsUpdate => UpdateType == Gg2Protocol.CapsUpdate;
}

/// <summary>Player event 12: stats in full updates, then the character and sentry if present.</summary>
public sealed record Gg2PlayerState(Gg2PlayerStats? Stats, Gg2CharacterState? Character, Gg2SentryState? Sentry);

/// <summary>Full-update player stats. DominationKills holds one count per other player, in roster order.</summary>
public sealed record Gg2PlayerStats(
    byte Kills,
    byte Deaths,
    byte Caps,
    byte Assists,
    byte Destruction,
    byte Stabs,
    ushort Healing,
    byte Defenses,
    byte Invulns,
    byte Bonus,
    byte Points,
    byte QueueJump,
    string Rewards,
    byte[] DominationKills);

/// <summary>Character event 12: inputs every update; position and status in quick and full ones.</summary>
public sealed record Gg2CharacterState(
    byte KeyState,
    ushort NetAimDirection,
    byte AimDistanceHalf,
    Gg2CharacterQuickState? Quick,
    Gg2CharacterFullState? Full)
{
    /// <summary>GameMaker degrees (counter-clockwise from +x, y up).</summary>
    public float AimDirectionDegrees => NetAimDirection * 360f / 65536f;

    public float AimDistance => AimDistanceHalf * 2f;
}

public readonly record struct Gg2CharacterQuickState(
    ushort XTimes5,
    ushort YTimes5,
    sbyte HorizontalSpeed85,
    sbyte VerticalSpeed85,
    byte Health,
    byte Ammo,
    byte StatusFlags)
{
    public float X => XTimes5 / 5f;
    public float Y => YTimes5 / 5f;
    public float HorizontalSpeed => HorizontalSpeed85 / 8.5f;
    public float VerticalSpeed => VerticalSpeed85 / 8.5f;
    public bool IsCloaked => (StatusFlags & 0x01) != 0;
    public int MoveStatus => (StatusFlags >> 1) & 0x07;
}

/// <summary>
/// Full-update character extras. ClassValue is the class-specific byte: spy cloak alpha
/// (0-255), medic uber (charge * 255 / 2000), engineer nuts 'n' bolts, sniper charge (t).
/// IntelAlarm is the intel pickup cooldown (alarm[1]).
/// </summary>
public sealed record Gg2CharacterFullState(
    byte AnimationOffset,
    byte ClassValue,
    short IntelAlarm,
    byte HasIntel,
    short IntelRecharge,
    Gg2WeaponState Weapon);

/// <summary>
/// Weapon event 12. The base weapon sends ready/cooldown; Medigun sends only its heal
/// target (255 for none); Minegun adds its lobbed mines after the base bytes.
/// </summary>
public sealed record Gg2WeaponState(
    byte ReadyToShoot,
    byte CooldownTicks,
    byte? HealTarget,
    IReadOnlyList<Gg2MineState>? Mines);

public readonly record struct Gg2MineState(ushort XTimes5, ushort YTimes5, sbyte HorizontalSpeed5, sbyte VerticalSpeed5, byte Stickied)
{
    public float X => XTimes5 / 5f;
    public float Y => YTimes5 / 5f;
    public float HorizontalSpeed => HorizontalSpeed5 / 5f;
    public float VerticalSpeed => VerticalSpeed5 / 5f;
}

/// <summary>
/// Sentry event 12. Placement (start direction and position) comes in full updates only;
/// the packed byte holds built (0x80) and health (0x7F); an unbuilt sentry's fall speed
/// follows in full updates. Input updates carry no sentry bytes.
/// </summary>
public sealed record Gg2SentryState(
    sbyte? StartDirection,
    ushort? XTimes5,
    ushort? YTimes5,
    byte Packed,
    byte? VerticalSpeed)
{
    public bool Built => (Packed & 0x80) != 0;
    public int Health => Packed & 0x7F;
}

public readonly record struct Gg2PlatformState(ushort XTimes5, ushort YTimes5, byte MovementState);

/// <summary>Intelligence event 10: position and return alarm.</summary>
public readonly record struct Gg2IntelState(ushort XTimes5, ushort YTimes5, short ReturnAlarm)
{
    public float X => XTimes5 / 5f;
    public float Y => YTimes5 / 5f;
}

public sealed record Gg2FullUpdateTail(
    IReadOnlyList<Gg2IntelState> RedIntel,
    IReadOnlyList<Gg2IntelState> BlueIntel,
    byte CapLimit,
    byte RedCaps,
    byte BlueCaps,
    byte RespawnSeconds,
    Gg2HudState? Hud,
    byte[] ClassLimits);

public sealed record Gg2CapsUpdateTail(byte RedCaps, byte BlueCaps, byte RespawnSeconds, Gg2HudState? Hud);

/// <summary>A control point's event 12: owner and capping team (GG2 team values, -1 neutral) and progress.</summary>
public readonly record struct Gg2ControlPointState(sbyte Team, sbyte CappingTeam, ushort Capping);

public readonly record struct Gg2GeneratorState(ushort Health, ushort ShieldHealth);

/// <summary>The HUD object's event 12 block, by HUD kind.</summary>
public abstract record Gg2HudState;

/// <summary>CTFHUD: time limit in minutes and the round timer in ticks.</summary>
public sealed record Gg2CtfHudState(byte TimeLimitMinutes, uint Timer) : Gg2HudState;

public sealed record Gg2ControlPointHudState(byte TimeLimitMinutes, uint Timer, ushort SetupTimer, IReadOnlyList<Gg2ControlPointState> Points) : Gg2HudState;

/// <summary>KothHUD (one point) or DKothHUD (red's point, then blue's).</summary>
public sealed record Gg2KothHudState(ushort UnlockTicks, ushort RedTimer, ushort BlueTimer, IReadOnlyList<Gg2ControlPointState> Points) : Gg2HudState;

public sealed record Gg2ArenaRoundState(byte RedWins, byte BlueWins, byte State, sbyte Winners, ushort EndCount);

/// <summary>ArenaHUD. Round is sent in full updates only; RoundStart is GG2's roundStart (sent halved).</summary>
public sealed record Gg2ArenaHudState(Gg2ArenaRoundState? Round, byte TimeLimitMinutes, uint Timer, ushort UnlockTicks, int RoundStart, Gg2ControlPointState Point) : Gg2HudState;

public sealed record Gg2TeamDeathmatchHudState(byte TimeLimitMinutes, uint Timer, ushort KillLimit) : Gg2HudState;

/// <summary>GeneratorHUD. A destroyed generator sends nothing, so either may be absent.</summary>
public sealed record Gg2GeneratorHudState(byte TimeLimitMinutes, uint Timer, Gg2GeneratorState? Blue, Gg2GeneratorState? Red) : Gg2HudState;

public sealed record Gg2InvasionHudState(byte TimeLimitMinutes, uint Timer, ushort SetupTimer) : Gg2HudState;
