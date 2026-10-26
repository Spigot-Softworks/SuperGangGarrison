using System;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

/// <summary>
/// A move pose a plugin plays on a player: one of the class presentation's PoseSuffixes.
/// </summary>
/// <param name="Suffix">The pose sprite suffix (for example "KickS").</param>
/// <param name="TicksPerFrame">Source ticks each frame is shown.</param>
/// <param name="Loop">Whether the frames loop; otherwise the last frame holds.</param>
/// <param name="DurationTicks">How long the pose shows, in source ticks; 0 shows it until cleared.</param>
/// <param name="Sequence">Changes every time a pose starts, so a repeated pose restarts.</param>
public readonly record struct GameplayPlayerPose(string Suffix, int TicksPerFrame, bool Loop, int DurationTicks, int Sequence);

/// <summary>
/// Stores a player's pose as one replicated int, so it reaches clients with the snapshot.
/// Bits: pose index + 1 (0-6), ticks per frame (7-11), loop (12), duration (13-23), sequence (24-31).
/// </summary>
public static class GameplayPlayerPoses
{
    public const string ReplicatedStateOwnerId = "og.pose";
    public const string ReplicatedStateKey = "pose";
    public const int MaxPoses = 127;
    public const int MaxTicksPerFrame = 31;
    public const int MaxDurationTicks = 2047;

    public static bool TrySet(PlayerEntity player, string? suffix, int ticksPerFrame, bool loop, int durationTicks)
    {
        if (string.IsNullOrWhiteSpace(suffix))
        {
            // Cleared, the pose keeps its sequence (no pose index), so a pose set right after a
            // clear (a move cancelled into another in one tick) still counts as new on clients.
            if (player.TryGetReplicatedStateInt(ReplicatedStateOwnerId, ReplicatedStateKey, out var current))
            {
                return player.SetReplicatedStateInt(ReplicatedStateOwnerId, ReplicatedStateKey, current & unchecked((int)0xFF000000));
            }

            return true;
        }

        var poseSuffixes = GetPresentation(player)?.PoseSuffixes;
        var index = -1;
        for (var candidate = 0; poseSuffixes is not null && candidate < poseSuffixes.Count && candidate < MaxPoses; candidate += 1)
        {
            if (string.Equals(poseSuffixes[candidate], suffix.Trim(), StringComparison.Ordinal))
            {
                index = candidate;
                break;
            }
        }

        if (index < 0)
        {
            return false;
        }

        var sequence = player.TryGetReplicatedStateInt(ReplicatedStateOwnerId, ReplicatedStateKey, out var previous)
            ? ((int)((uint)previous >> 24) + 1) & 0xFF
            : 0;
        var packed = (index + 1)
            | (Math.Clamp(ticksPerFrame, 1, MaxTicksPerFrame) << 7)
            | ((loop ? 1 : 0) << 12)
            | (Math.Clamp(durationTicks, 0, MaxDurationTicks) << 13)
            | (sequence << 24);
        return player.SetReplicatedStateInt(ReplicatedStateOwnerId, ReplicatedStateKey, packed);
    }

    /// <summary>The raw replicated pose value, 0 when none is set (for change detection).</summary>
    public static int GetPackedState(PlayerEntity player)
        => player.TryGetReplicatedStateInt(ReplicatedStateOwnerId, ReplicatedStateKey, out var packed) ? packed : 0;

    public static bool TryGet(PlayerEntity player, out GameplayPlayerPose pose)
    {
        pose = default;
        if (!player.TryGetReplicatedStateInt(ReplicatedStateOwnerId, ReplicatedStateKey, out var packed))
        {
            return false;
        }

        var index = (packed & 0x7F) - 1;
        var poseSuffixes = GetPresentation(player)?.PoseSuffixes;
        if (index < 0 || poseSuffixes is null || index >= poseSuffixes.Count)
        {
            return false;
        }

        pose = new GameplayPlayerPose(
            poseSuffixes[index],
            (packed >> 7) & 0x1F,
            ((packed >> 12) & 1) != 0,
            (packed >> 13) & 0x7FF,
            (int)((uint)packed >> 24));
        return true;
    }

    private static GameplayClassPresentationDefinition? GetPresentation(PlayerEntity player)
    {
        var registry = CharacterClassCatalog.RuntimeRegistry;
        return registry.TryGetClassBinding(player.GameplayClassId, out _)
            ? registry.GetClassDefinition(player.GameplayClassId).Presentation
            : null;
    }
}
