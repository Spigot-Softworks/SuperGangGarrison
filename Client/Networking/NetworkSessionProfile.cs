#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// The rules that differ between a session on an SGG server and one on a GG2 server, in
/// one place. Client code asks the profile a named question ("does this server
/// acknowledge inputs?") instead of checking which kind of server it is, so a new GG2
/// difference is added here once rather than as another scattered connection check.
/// </summary>
public sealed class NetworkSessionProfile
{
    public static NetworkSessionProfile Sgg { get; } = new(isGg2: false);

    public static NetworkSessionProfile Gg2 { get; } = new(isGg2: true);

    private NetworkSessionProfile(bool isGg2)
    {
        IsGg2 = isGg2;
    }

    /// <summary>A stock GG2 server (through the GG2 connection or the browser gateway).</summary>
    public bool IsGg2 { get; }

    // ----- Protocol and input -----

    /// <summary>The server reports which input it processed last (GG2 only echoes key state).</summary>
    public bool AcknowledgesInputs => !IsGg2;

    /// <summary>Only GG2's stock controls reach the server; SGG-only buttons are dropped.</summary>
    public bool SendsStockInputOnly => IsGg2;

    /// <summary>Aim is measured from the centre of the view (GG2's ClientInputstate), not the player.</summary>
    public bool MeasuresAimFromViewCenter => IsGg2;

    /// <summary>Local shots may start their presentation before the server confirms them.</summary>
    public bool StartsLocalFirePresentationImmediately => !IsGg2;

    /// <summary>Client prediction is the player's choice (GG2 Prediction setting) rather than the server's.</summary>
    public bool PredictionIsPlayerOption => IsGg2;

    /// <summary>Resting players arrive at their exact position; don't extrapolate them.</summary>
    public bool SendsRestingPositionsDirectly => IsGg2;

    /// <summary>Every snapshot is complete, so only the first full one starts the warm-up baseline.</summary>
    public bool EverySnapshotIsFull => IsGg2;

    // ----- Session services -----

    /// <summary>SGG accounts, custom bubble uploads and similar services exist on the server.</summary>
    public bool SupportsGameplayAccounts => !IsGg2;

    /// <summary>The server owns the player's size and position (no Last to Die profile resets).</summary>
    public bool ServerOwnsPlayerScale => IsGg2;

    /// <summary>Client plugins may offset the camera.</summary>
    public bool AllowsPluginCameraOffset => !IsGg2;

    /// <summary>Camera panning and smoothing follow the player's settings (GG2 keeps its fixed camera).</summary>
    public bool AllowsCameraPanningAndSmoothing => !IsGg2;

    // ----- Teams and classes -----

    /// <summary>
    /// Teams come from the server's roster: a team request may be refused silently, a spectator
    /// slot means "pick a team", and the team menu reopens when the server moves us to spectate.
    /// </summary>
    public bool AssignsTeamsThroughRoster => IsGg2;

    /// <summary>Joining the match starts a new presentation epoch (GG2 joins mid-stream).</summary>
    public bool LocalJoinStartsPresentationEpoch => !IsGg2;

    /// <summary>Only the ten stock GG2 classes can be picked.</summary>
    public bool StockClassesOnly => IsGg2;

    /// <summary>Loadouts, secondary weapons, ability rows and weapon swaps exist.</summary>
    public bool OffersLoadouts => !IsGg2;

    /// <summary>Engineers build from the build wheel (GG2 builds its one sentry directly).</summary>
    public bool OffersBuildWheel => !IsGg2;

    // ----- Presentation -----

    /// <summary>Players always draw with the original GG2 sprites and animations.</summary>
    public bool ForcesClassicPlayerVisuals => IsGg2;

    /// <summary>GG2's Quote slot is shown as the Impostor with GG2's blade.</summary>
    public bool QuoteAppearsAsImpostor => IsGg2;

    /// <summary>Firing animations start from the server's weapon sound, not from cooldown changes.</summary>
    public bool StartsFireAnimationsFromSounds => IsGg2;

    /// <summary>Shotgun-style weapons reload one shell at a time with GG2's timing.</summary>
    public bool InsertsShellsOneAtATime => IsGg2;

    /// <summary>Corpses may be kept for the whole round (Infinite corpse duration).</summary>
    public bool AllowsRetainedCorpses => !IsGg2;

    /// <summary>How long the heavy's sandvich animation lasts.</summary>
    public int HeavyEatAnimationTicks => IsGg2 ? Gg2HeavyEatAnimationTicks : PlayerEntity.HeavyEatDurationTicks;

    /// <summary>GG2's omnomnomnom animation: 32 frames at 0.25 per tick.</summary>
    public const int Gg2HeavyEatAnimationTicks = 128;
}
