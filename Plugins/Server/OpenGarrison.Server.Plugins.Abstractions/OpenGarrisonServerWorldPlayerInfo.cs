using OpenGarrison.Core;

namespace OpenGarrison.Server.Plugins;

/// <summary>
/// Filters for a query over every player in the simulation, including bots.
/// </summary>
/// <param name="CenterX">The query center X, or null to skip the distance filter.</param>
/// <param name="CenterY">The query center Y, or null to skip the distance filter.</param>
/// <param name="Radius">The maximum distance from the center, or null for no limit.</param>
/// <param name="Team">Only players on this team, if set.</param>
/// <param name="ExcludePlayerId">A player id to leave out, such as the caller.</param>
/// <param name="AliveOnly">Whether to leave out dead players.</param>
/// <param name="Limit">The maximum number of results.</param>
public sealed record OpenGarrisonServerWorldPlayerQuery(
    float? CenterX = null,
    float? CenterY = null,
    float? Radius = null,
    PlayerTeam? Team = null,
    int? ExcludePlayerId = null,
    bool AliveOnly = true,
    int Limit = 64);

/// <summary>
/// A simulated player's movement and combat state, for gameplay scripts.
/// </summary>
/// <param name="PlayerId">The player id.</param>
/// <param name="Team">The team.</param>
/// <param name="PlayerClass">The legacy player class slot (Custom for plugin classes).</param>
/// <param name="GameplayClassId">The gameplay class id.</param>
/// <param name="IsAlive">Whether the player is alive.</param>
/// <param name="WorldX">The world X coordinate.</param>
/// <param name="WorldY">The world Y coordinate.</param>
/// <param name="Left">The collision box left edge.</param>
/// <param name="Top">The collision box top edge.</param>
/// <param name="Right">The collision box right edge.</param>
/// <param name="Bottom">The collision box bottom edge.</param>
/// <param name="VelocityX">The horizontal velocity.</param>
/// <param name="VelocityY">The vertical velocity.</param>
/// <param name="Health">The current health.</param>
/// <param name="MaxHealth">The maximum health.</param>
/// <param name="IsGrounded">Whether the player is on the ground.</param>
/// <param name="IsFacingLeft">Whether the player faces left.</param>
/// <param name="Distance">The distance from the query center, or null when no center was given.</param>
public sealed record OpenGarrisonServerWorldPlayerInfo(
    int PlayerId,
    PlayerTeam Team,
    PlayerClass PlayerClass,
    string GameplayClassId,
    bool IsAlive,
    float WorldX,
    float WorldY,
    float Left,
    float Top,
    float Right,
    float Bottom,
    float VelocityX,
    float VelocityY,
    int Health,
    int MaxHealth,
    bool IsGrounded,
    bool IsFacingLeft,
    float? Distance);
