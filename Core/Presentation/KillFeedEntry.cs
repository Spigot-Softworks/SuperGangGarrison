namespace OpenGarrison.Core;

public enum KillFeedSpecialType : byte
{
    None = 0,
    Domination = 1,
    Revenge = 2,
}

public sealed record KillFeedEntry(
    string KillerName,
    PlayerTeam KillerTeam,
    string WeaponSpriteName,
    string VictimName,
    PlayerTeam VictimTeam,
    string MessageText = "",
    int KillerPlayerId = -1,
    int VictimPlayerId = -1,
    KillFeedSpecialType SpecialType = KillFeedSpecialType.None,
    ulong EventId = 0);
