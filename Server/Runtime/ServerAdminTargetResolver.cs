using System;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal readonly record struct ServerAdminTargetQueryOptions(
    byte? SourceSlot = null,
    bool AllowMultiple = true,
    bool RequireAlive = false,
    bool IncludeSpectators = true);

internal readonly record struct ServerAdminTargetResolution(
    bool Success,
    string Query,
    string MatchKind,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<OpenGarrisonServerPlayerInfo> Targets);

internal sealed class ServerAdminTargetResolver(Func<IReadOnlyList<OpenGarrisonServerPlayerInfo>> playersGetter)
{
    public ServerAdminTargetResolution Resolve(string query, ServerAdminTargetQueryOptions options)
    {
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
        {
            return CreateError(query, "empty_query", "Target is required.");
        }

        var candidates = FilterEligiblePlayers(playersGetter(), options);
        if (normalizedQuery.StartsWith('@'))
        {
            return ResolveSelector(query, normalizedQuery, candidates, options);
        }

        if (normalizedQuery.StartsWith('#')
            && int.TryParse(normalizedQuery[1..], out var userId))
        {
            var player = candidates.FirstOrDefault(candidate => candidate.UserId == userId);
            return player == default
                ? CreateError(query, "target_not_found", $"No player matched {normalizedQuery}.")
                : CreateSuccess(query, "userid", [player]);
        }

        var exactMatches = candidates
            .Where(candidate => string.Equals(candidate.Name, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (exactMatches.Length == 1)
        {
            return CreateSuccess(query, "exact_name", exactMatches);
        }

        if (exactMatches.Length > 1)
        {
            return CreateError(query, "ambiguous_name", $"Multiple players share the exact name \"{normalizedQuery}\".");
        }

        var partialMatches = candidates
            .Where(candidate => candidate.Name.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (partialMatches.Length == 1)
        {
            return CreateSuccess(query, "partial_name", partialMatches);
        }

        if (partialMatches.Length > 1)
        {
            return CreateError(query, "ambiguous_name", $"Multiple players matched \"{normalizedQuery}\".");
        }

        return CreateError(query, "target_not_found", $"No player matched \"{normalizedQuery}\".");
    }

    private static OpenGarrisonServerPlayerInfo[] FilterEligiblePlayers(
        IReadOnlyList<OpenGarrisonServerPlayerInfo> players,
        ServerAdminTargetQueryOptions options)
    {
        return players
            .Where(player => options.IncludeSpectators || !player.IsSpectator)
            .Where(player => !options.RequireAlive || (!player.IsSpectator && player.IsAlive))
            .ToArray();
    }

    private static ServerAdminTargetResolution ResolveSelector(
        string originalQuery,
        string normalizedQuery,
        OpenGarrisonServerPlayerInfo[] candidates,
        ServerAdminTargetQueryOptions options)
    {
        var targets = normalizedQuery.ToLowerInvariant() switch
        {
            "@all" => candidates,
            "@alive" => candidates.Where(static player => !player.IsSpectator && player.IsAlive).ToArray(),
            "@dead" => candidates.Where(static player => !player.IsSpectator && !player.IsAlive).ToArray(),
            "@red" => candidates.Where(static player => player.Team == PlayerTeam.Red).ToArray(),
            "@blue" => candidates.Where(static player => player.Team == PlayerTeam.Blue).ToArray(),
            "@me" => ResolveSelf(options.SourceSlot, candidates),
            _ => null,
        };

        if (targets is null)
        {
            return CreateError(originalQuery, "unknown_selector", $"Unknown target selector \"{normalizedQuery}\".");
        }

        if (normalizedQuery == "@me" && options.SourceSlot is null)
        {
            return CreateError(originalQuery, "source_slot_required", "@me is only available from a player-backed admin session.");
        }

        if (targets.Length == 0)
        {
            return CreateError(originalQuery, "target_not_found", $"No players matched {normalizedQuery}.");
        }

        return CreateSuccess(originalQuery, "selector", targets, normalizedQuery, options.AllowMultiple);
    }

    private static OpenGarrisonServerPlayerInfo[] ResolveSelf(byte? sourceSlot, OpenGarrisonServerPlayerInfo[] candidates)
    {
        return sourceSlot is null
            ? []
            : candidates.Where(player => player.Slot == sourceSlot.Value).ToArray();
    }

    private static ServerAdminTargetResolution CreateSuccess(
        string query,
        string matchKind,
        OpenGarrisonServerPlayerInfo[] targets,
        string? selector = null,
        bool allowMultiple = true)
    {
        if (!allowMultiple && targets.Length > 1)
        {
            var description = selector ?? query.Trim();
            return CreateError(query, "multiple_targets", $"{description} matched {targets.Length} players; refine the target.");
        }

        return new ServerAdminTargetResolution(
            Success: true,
            Query: query,
            MatchKind: matchKind,
            ErrorCode: null,
            ErrorMessage: null,
            Targets: targets);
    }

    private static ServerAdminTargetResolution CreateError(string query, string errorCode, string errorMessage)
    {
        return new ServerAdminTargetResolution(
            Success: false,
            Query: query,
            MatchKind: string.Empty,
            ErrorCode: errorCode,
            ErrorMessage: errorMessage,
            Targets: Array.Empty<OpenGarrisonServerPlayerInfo>());
    }
}
