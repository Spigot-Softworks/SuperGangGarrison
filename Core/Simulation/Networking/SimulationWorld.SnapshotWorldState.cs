using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private bool EnsureSnapshotLevelLoaded(SnapshotMessage snapshot)
    {
        return ((string.Equals(Level.Name, snapshot.LevelName, StringComparison.OrdinalIgnoreCase)
                && Level.MapAreaIndex == snapshot.MapAreaIndex)
            && MathF.Abs(Level.MapScale - snapshot.MapScale) <= 0.0001f)
            || TryLoadLevel(snapshot.LevelName, snapshot.MapAreaIndex, preservePlayerStats: false, mapScale: snapshot.MapScale);
    }

    private void ApplySnapshotWorldState(SnapshotMessage snapshot)
    {
        MatchRules = MatchRules with
        {
            Mode = (GameModeKind)snapshot.GameMode,
            TimeLimitTicks = snapshot.TimeLimitTicks > 0
                ? Math.Max(snapshot.TimeRemainingTicks, snapshot.TimeLimitTicks)
                : Math.Max(snapshot.TimeRemainingTicks, MatchRules.TimeLimitTicks),
        };
        MatchState = new MatchState(
            (MatchPhase)snapshot.MatchPhase,
            snapshot.TimeRemainingTicks,
            snapshot.WinnerTeam == 0 ? null : (PlayerTeam)snapshot.WinnerTeam);
        LocalDeathCam = snapshot.LocalDeathCam is null
            ? null
            : new LocalDeathCamState(
                snapshot.LocalDeathCam.FocusX,
                snapshot.LocalDeathCam.FocusY,
                snapshot.LocalDeathCam.KillMessage,
                snapshot.LocalDeathCam.KillerName,
                snapshot.LocalDeathCam.KillerTeam == 0 ? null : (PlayerTeam)snapshot.LocalDeathCam.KillerTeam,
                snapshot.LocalDeathCam.Health,
                snapshot.LocalDeathCam.MaxHealth,
                snapshot.LocalDeathCam.RemainingTicks,
                snapshot.LocalDeathCam.InitialTicks);
        RedCaps = snapshot.RedCaps;
        BlueCaps = snapshot.BlueCaps;
        SpectatorCount = Math.Max(0, snapshot.SpectatorCount);
        ApplySnapshotCompetitiveReadyUp(snapshot.CompetitiveReadyUpPhase, snapshot.CompetitiveReadyUpTicksRemaining);

        ApplySnapshotSpectators(snapshot.Players);
        ApplySnapshotObjectives(snapshot);
        ApplySnapshotKillFeed(snapshot.KillFeed);
    }

    private void ApplySnapshotSpectators(IReadOnlyList<SnapshotPlayerState> players)
    {
        _spectators.Clear();
        for (var playerIndex = 0; playerIndex < players.Count; playerIndex += 1)
        {
            var player = players[playerIndex];
            if ((!player.IsSpectator && !player.IsAwaitingJoin) || string.IsNullOrWhiteSpace(player.Name))
            {
                continue;
            }

            _spectators.Add(new ScoreboardSpectatorEntry(
                player.Name,
                player.BadgeMask,
                player.IsAwaitingJoin));
        }
    }

    private void ApplySnapshotObjectives(SnapshotMessage snapshot)
    {
        ApplySnapshotArena(snapshot);
        ApplySnapshotControlPoints(snapshot);
        ApplySnapshotKoth(snapshot);
        ApplySnapshotGenerators(snapshot);
        RedIntel.ApplyNetworkState(
            snapshot.RedIntel.X,
            snapshot.RedIntel.Y,
            snapshot.RedIntel.IsAtBase,
            snapshot.RedIntel.IsDropped,
            snapshot.RedIntel.ReturnTicksRemaining);
        BlueIntel.ApplyNetworkState(
            snapshot.BlueIntel.X,
            snapshot.BlueIntel.Y,
            snapshot.BlueIntel.IsAtBase,
            snapshot.BlueIntel.IsDropped,
            snapshot.BlueIntel.ReturnTicksRemaining);
    }

    private void ApplySnapshotArena(SnapshotMessage snapshot)
    {
        if ((GameModeKind)snapshot.GameMode != GameModeKind.Arena)
        {
            _arenaPointTeam = null;
            _arenaCappingTeam = null;
            _arenaCappingTicks = 0f;
            _arenaCappers = 0;
            _arenaUnlockTicksRemaining = 0;
            _arenaRedConsecutiveWins = 0;
            _arenaBlueConsecutiveWins = 0;
            return;
        }

        _arenaPointTeam = snapshot.ArenaPointTeam == 0 ? null : (PlayerTeam)snapshot.ArenaPointTeam;
        _arenaCappingTeam = snapshot.ArenaCappingTeam == 0 ? null : (PlayerTeam)snapshot.ArenaCappingTeam;
        _arenaCappingTicks = Math.Max(0f, snapshot.ArenaCappingTicks);
        _arenaCappers = Math.Max(0, snapshot.ArenaCappers);
        _arenaUnlockTicksRemaining = Math.Max(0, snapshot.ArenaUnlockTicksRemaining);
        _arenaRedConsecutiveWins = Math.Max(0, snapshot.ArenaRedConsecutiveWins);
        _arenaBlueConsecutiveWins = Math.Max(0, snapshot.ArenaBlueConsecutiveWins);
    }

    private void ApplySnapshotKillFeed(IReadOnlyList<SnapshotKillFeedEntry> killFeed)
    {
        // Advance local aging one step per snapshot received so entries expire
        // at roughly the same rate as they do on the server.
        AdvanceKillFeed();

        // Additive merge: only add entries whose EventId is not yet in the local
        // list. This prevents flicker when the kill feed is absent from a snapshot
        // due to budget trimming, and lets the client maintain its own state.
        for (var killFeedIndex = 0; killFeedIndex < killFeed.Count; killFeedIndex += 1)
        {
            var entry = killFeed[killFeedIndex];
            var alreadyPresent = false;
            for (var existingIndex = 0; existingIndex < _killFeed.Count; existingIndex += 1)
            {
                if (_killFeed[existingIndex].EventId == entry.EventId)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (alreadyPresent)
            {
                continue;
            }

            // Insert in EventId-ascending order so oldest entries are first and
            // new events always appear at the bottom of the feed.
            var insertIndex = _killFeed.Count;
            for (var i = 0; i < _killFeed.Count; i += 1)
            {
                if (_killFeed[i].EventId > entry.EventId)
                {
                    insertIndex = i;
                    break;
                }
            }

            var isLocalInvolved = entry.KillerPlayerId == LocalPlayer.Id || entry.VictimPlayerId == LocalPlayer.Id;
            var lifetime = isLocalInvolved ? KillFeedLocalInvolvedLifetimeTicks : KillFeedLifetimeTicks;
            _killFeed.Insert(insertIndex, new KillFeedEntry(
                entry.KillerName,
                (PlayerTeam)entry.KillerTeam,
                entry.WeaponSpriteName,
                entry.VictimName,
                (PlayerTeam)entry.VictimTeam,
                entry.MessageText,
                entry.MessageHighlightStart,
                entry.MessageHighlightLength,
                entry.KillerPlayerId,
                entry.VictimPlayerId,
                (KillFeedSpecialType)entry.SpecialType,
                entry.EventId));
            _killFeedEntryLifetimes.Insert(insertIndex, lifetime);
        }

        // Keep the local list bounded in case the client accumulates more entries
        // than the server would normally allow.
        while (_killFeed.Count > 5)
        {
            _killFeed.RemoveAt(0);
            _killFeedEntryLifetimes.RemoveAt(0);
        }
    }
}
