using OpenGarrison.Core;

namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Describes the input the native vote menu can collect for a plugin-defined vote.
/// </summary>
public enum OpenGarrisonServerVoteTargetKind : byte
{
    None = 0,
    Player = 1,
    Map = 2,
}

/// <summary>
/// Immutable, server-resolved input for a plugin-defined vote. Player targets carry
/// a stable identity so a disconnected slot cannot silently become a different target.
/// </summary>
public sealed record OpenGarrisonServerVoteRequest(
    string VoteKindId,
    byte InitiatorSlot,
    string InitiatorName,
    string Argument,
    byte? TargetSlot,
    int? TargetUserId,
    string TargetIdentity,
    string TargetName,
    PlayerTeam? TargetTeam,
    string MapName,
    int MapAreaIndex);

/// <summary>
/// Result returned before a ballot is opened. The subject is the concise text shown
/// to voters; rejected requests never consume the global vote cooldown.
/// </summary>
public sealed record OpenGarrisonServerVoteValidationResult(
    bool Accepted,
    string Subject,
    string ErrorMessage)
{
    public static OpenGarrisonServerVoteValidationResult Accept(string subject)
        => new(true, subject, string.Empty);

    public static OpenGarrisonServerVoteValidationResult Reject(string errorMessage)
        => new(false, string.Empty, errorMessage);
}

/// <summary>
/// Registers one plugin-owned vote kind with the native voting system. Validation
/// runs when a player requests the vote; Apply runs once, and only after it passes.
/// </summary>
public sealed record OpenGarrisonServerVoteRegistration(
    string Id,
    string DisplayName,
    string Description,
    OpenGarrisonServerVoteTargetKind TargetKind,
    Func<OpenGarrisonServerVoteRequest, bool> Apply,
    Func<OpenGarrisonServerVoteRequest, OpenGarrisonServerVoteValidationResult>? Validate = null);
