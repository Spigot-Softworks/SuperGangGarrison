using System;

namespace OpenGarrison.Protocol.Gg2;

/// <summary>
/// Message ids and fixed values of the GG2 2.9.2 protocol, from GG2 Modern's Constants.xml
/// (Modern/GML-GG2-Modern). GG2 messages carry no length prefix: each one's size follows
/// from its type and, for state updates, from the roster and the map (see
/// <see cref="Gg2ServerMessageReader"/>).
/// </summary>
public static class Gg2Protocol
{
    /// <summary>GG2 Modern's VERSION constant.</summary>
    public const int Version = 29200;

    /// <summary>GG2's PROTOCOL_UUID, sent as 16 bytes in text order after HELLO.</summary>
    public const string ProtocolUuid = "b31c2209-4256-9a19-d0ef-c71c5373bd75";

    /// <summary>GG2's MAX_PLAYERNAME_LENGTH.</summary>
    public const int MaxPlayerNameLength = 20;

    /// <summary>GG2's simulation rate: every step is one tick at 30 Hz.</summary>
    public const int TicksPerSecond = 30;

    /// <summary>The server sends QUICK_UPDATE every this many ticks.</summary>
    public const int QuickUpdateInterval = 7;

    /// <summary>"No player" in player-index fields.</summary>
    public const byte NoPlayer = 255;

    public const byte Hello = 0;
    public const byte PlayerJoin = 1;
    public const byte PlayerLeave = 2;
    public const byte PlayerChangeTeam = 3;
    public const byte PlayerChangeClass = 4;
    public const byte PlayerSpawn = 5;
    public const byte InputState = 6;
    public const byte ChangeMap = 7;
    public const byte FullUpdate = 8;
    public const byte QuickUpdate = 9;
    public const byte PlayerDeath = 10;
    public const byte ServerFull = 11;
    public const byte RedTeamCap = 12;
    public const byte BlueTeamCap = 13;
    public const byte MapEnd = 14;
    public const byte ChatBubble = 15;
    public const byte BuildSentry = 16;
    public const byte DestroySentry = 17;
    public const byte Balance = 18;
    public const byte GrabIntel = 19;
    public const byte ScoreIntel = 20;
    public const byte DropIntel = 21;
    public const byte UberCharged = 22;
    public const byte Uber = 23;
    public const byte Omnomnomnom = 24;
    public const byte PasswordRequest = 25;
    public const byte PasswordWrong = 27;
    public const byte CapsUpdate = 28;
    public const byte CpCaptured = 30;
    public const byte PlayerChangeName = 31;
    public const byte GeneratorDestroy = 32;
    public const byte ArenaWaitForPlayers = 33;
    public const byte ArenaEndRound = 34;
    public const byte ArenaRestart = 35;
    public const byte UnlockCp = 36;
    public const byte ServerKick = 37;
    public const byte Kick = 38;
    public const byte KickName = 39;
    public const byte ArenaStartRound = 40;
    public const byte ToggleZoom = 41;
    public const byte ReturnIntel = 42;
    public const byte IncompatibleProtocol = 43;
    public const byte JoinUpdate = 44;
    public const byte DownloadMap = 45;
    public const byte SentryPosition = 46;
    public const byte RewardUpdate = 47;
    public const byte RewardRequest = 50;
    public const byte RewardChallengeCode = 51;
    public const byte RewardChallengeResponse = 52;
    public const byte MessageString = 53;
    public const byte WeaponFire = 54;
    public const byte PluginPacket = 55;
    public const byte KickBadPluginPacket = 56;
    public const byte Ping = 57;
    public const byte ClientSettings = 58;
    public const byte KickMultiClient = 59;
    public const byte ReserveSlot = 60;

    public const byte TeamRed = 0;
    public const byte TeamBlue = 1;
    public const byte TeamSpectator = 2;
    public const byte TeamAny = 3;

    public const byte ClassScout = 0;
    public const byte ClassSoldier = 1;
    public const byte ClassSniper = 2;
    public const byte ClassDemoman = 3;
    public const byte ClassMedic = 4;
    public const byte ClassEngineer = 5;
    public const byte ClassHeavy = 6;
    public const byte ClassSpy = 7;
    public const byte ClassPyro = 8;
    public const byte ClassQuote = 9;
    public const int ClassCount = 10;

    /// <summary>Character keyState bits (GG2 PlayerControl).</summary>
    public const byte KeyTaunt = 0x01;
    public const byte KeyDown = 0x02;
    public const byte KeySecondary = 0x08;
    public const byte KeyPrimary = 0x10;
    public const byte KeyRight = 0x20;
    public const byte KeyLeft = 0x40;
    public const byte KeyUp = 0x80;

    /// <summary>The 16 bytes GG2's parseUuid writes for <see cref="ProtocolUuid"/>, in text order.</summary>
    public static byte[] GetProtocolUuidBytes()
        => Convert.FromHexString(ProtocolUuid.Replace("-", string.Empty, StringComparison.Ordinal));
}

/// <summary>Which HUD object a GG2 map creates; it decides the HUD block of state updates.</summary>
public enum Gg2HudKind
{
    None,
    CaptureTheFlag,
    ControlPoint,
    KingOfTheHill,
    DoubleKingOfTheHill,
    Arena,
    TeamDeathmatch,
    Generator,
    Invasion,
}

/// <summary>
/// What a GG2 state update's size depends on from the current map: the HUD object, the
/// number of control points (ControlPointHUD writes one block per point) and the number of
/// moving platforms (one block each in quick and full updates).
/// </summary>
public readonly record struct Gg2MapLayout(Gg2HudKind HudKind, int ControlPointCount, int MovingPlatformCount)
{
    public static Gg2MapLayout Unknown => new(Gg2HudKind.None, 0, 0);
}
