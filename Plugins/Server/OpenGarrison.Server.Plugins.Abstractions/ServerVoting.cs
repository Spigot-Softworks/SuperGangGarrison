using OpenGarrison.Core;

namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Describes the input the native vote menu can collect for a plugin-defined vote.
/// </summary>
public enum OpenGarrisonServerVoteTargetKind : byte
{
    /// <summary>No target.</summary>
    None = 0,
    /// <summary>A player target.</summary>
    Player = 1,
    /// <summary>A map target.</summary>
    Map = 2,
}

/// <summary>
/// Immutable, server-resolved input for a plugin-defined vote. Player targets carry
/// a stable identity so a disconnected slot cannot silently become a different target.
/// </summary>
/// <param name="VoteKindId">The registered vote kind id.</param>
/// <param name="InitiatorSlot">The initiator's slot.</param>
/// <param name="InitiatorName">The initiator's name.</param>
/// <param name="Argument">The raw argument text.</param>
/// <param name="TargetSlot">The target player slot, if any.</param>
/// <param name="TargetUserId">The target user id, if any.</param>
/// <param name="TargetIdentity">The stable target identity.</param>
/// <param name="TargetName">The target name.</param>
/// <param name="TargetTeam">The target team, if any.</param>
/// <param name="MapName">The map name.</param>
/// <param name="MapAreaIndex">The map area index.</param>
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
/// <param name="Accepted">Whether the vote request was accepted.</param>
/// <param name="Subject">The concise text shown to voters.</param>
/// <param name="ErrorMessage">The rejection reason, when not accepted.</param>
public sealed record OpenGarrisonServerVoteValidationResult(
    bool Accepted,
    string Subject,
    string ErrorMessage)
{
    /// <summary>Creates an accepted vote validation result.</summary>
    /// <param name="subject">The concise text shown to voters.</param>
    public static OpenGarrisonServerVoteValidationResult Accept(string subject)
        => new(true, subject, string.Empty);

    /// <summary>Creates a rejected vote validation result.</summary>
    /// <param name="errorMessage">The reason the vote request was rejected.</param>
    public static OpenGarrisonServerVoteValidationResult Reject(string errorMessage)
        => new(false, string.Empty, errorMessage);
}

/// <summary>
/// Registers one plugin-owned vote kind with the native voting system. Validation
/// runs when a player requests the vote; Apply runs once, and only after it passes.
/// </summary>
/// <param name="Id">The vote kind id.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Description">The description.</param>
/// <param name="TargetKind">The target kind the vote collects.</param>
/// <param name="Apply">Applies the passed vote.</param>
/// <param name="Validate">Validates a vote request before the ballot opens, if any.</param>
public sealed record OpenGarrisonServerVoteRegistration(
    string Id,
    string DisplayName,
    string Description,
    OpenGarrisonServerVoteTargetKind TargetKind,
    Func<OpenGarrisonServerVoteRequest, bool> Apply,
    Func<OpenGarrisonServerVoteRequest, OpenGarrisonServerVoteValidationResult>? Validate = null);
