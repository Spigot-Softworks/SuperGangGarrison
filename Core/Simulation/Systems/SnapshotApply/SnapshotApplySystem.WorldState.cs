using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

internal sealed partial class SnapshotApplySystem
{
    private bool EnsureSnapshotLevelLoaded(SnapshotMessage snapshot)
    {
        return ((string.Equals(_host.Level.Name, snapshot.LevelName, StringComparison.OrdinalIgnoreCase)
                && _host.Level.MapAreaIndex == snapshot.MapAreaIndex)
            && MathF.Abs(_host.Level.MapScale - snapshot.MapScale) <= 0.0001f)
            || _host.MapLifecycle.TryLoadLevel(snapshot.LevelName, snapshot.MapAreaIndex, preservePlayerStats: false, mapScale: snapshot.MapScale);
    }

    private void ApplySnapshotWorldState(SnapshotMessage snapshot)
    {
        _host.MatchRules = _host.MatchRules with
        {
            Mode = (GameModeKind)snapshot.GameMode,
            TimeLimitTicks = snapshot.TimeLimitTicks > 0
                ? Math.Max(snapshot.TimeRemainingTicks, snapshot.TimeLimitTicks)
                : Math.Max(snapshot.TimeRemainingTicks, _host.MatchRules.TimeLimitTicks),
            CapLimit = snapshot.CapLimit > 0
                ? Math.Clamp(snapshot.CapLimit, 1, 255)
                : _host.MatchRules.CapLimit,
        };
        _host.MatchState = new MatchState(
            (MatchPhase)snapshot.MatchPhase,
            snapshot.TimeRemainingTicks,
            snapshot.WinnerTeam == 0 ? null : (PlayerTeam)snapshot.WinnerTeam);
        _host.LocalDeathCam = snapshot.LocalDeathCam is null
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
        _host.RedCaps = snapshot.RedCaps;
        _host.BlueCaps = snapshot.BlueCaps;
        _host.SpectatorCount = Math.Max(0, snapshot.SpectatorCount);
        _host.ReadyUp.ApplySnapshotCompetitiveReadyUp(snapshot.CompetitiveReadyUpPhase, snapshot.CompetitiveReadyUpTicksRemaining);

        ApplySnapshotSpectators(snapshot.Players);
        ApplySnapshotObjectives(snapshot);
        ApplySnapshotKillFeed(snapshot.KillFeed);
    }

    private void ApplySnapshotSpectators(IReadOnlyList<SnapshotPlayerState> players)
    {
        _host.ClientSnapshots.Spectators.Clear();
        for (var playerIndex = 0; playerIndex < players.Count; playerIndex += 1)
        {
            var player = players[playerIndex];
            if ((!player.IsSpectator && !player.IsAwaitingJoin) || string.IsNullOrWhiteSpace(player.Name))
            {
                continue;
            }

            _host.ClientSnapshots.Spectators.Add(new ScoreboardSpectatorEntry(
                player.Name,
                player.BadgeMask,
                player.IsAwaitingJoin));
        }
    }

    private void ApplySnapshotObjectives(SnapshotMessage snapshot)
    {
        ApplySnapshotArena(snapshot);
        _host.ObjectiveRules.ApplySnapshotControlPoints(snapshot);
        _host.ObjectiveRules.ApplySnapshotKoth(snapshot);
        _host.ObjectiveRules.ApplySnapshotGenerators(snapshot);
        _host.RedIntel.ApplyNetworkState(
            snapshot.RedIntel.X,
            snapshot.RedIntel.Y,
            snapshot.RedIntel.IsAtBase,
            snapshot.RedIntel.IsDropped,
            snapshot.RedIntel.ReturnTicksRemaining);
        _host.BlueIntel.ApplyNetworkState(
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
            _host.Objectives.Arena.ResetForNewRound(unlockTicksRemaining: 0);
            _host.Objectives.Arena.ResetWinStreaks();
            return;
        }

        _host.Objectives.Arena.PointTeam = snapshot.ArenaPointTeam == 0 ? null : (PlayerTeam)snapshot.ArenaPointTeam;
        _host.Objectives.Arena.CappingTeam = snapshot.ArenaCappingTeam == 0 ? null : (PlayerTeam)snapshot.ArenaCappingTeam;
        _host.Objectives.Arena.CappingTicks = Math.Max(0f, snapshot.ArenaCappingTicks);
        _host.Objectives.Arena.Cappers = Math.Max(0, snapshot.ArenaCappers);
        _host.Objectives.Arena.UnlockTicksRemaining = Math.Max(0, snapshot.ArenaUnlockTicksRemaining);
        _host.Objectives.Arena.RedConsecutiveWins = Math.Max(0, snapshot.ArenaRedConsecutiveWins);
        _host.Objectives.Arena.BlueConsecutiveWins = Math.Max(0, snapshot.ArenaBlueConsecutiveWins);
    }

    private void ApplySnapshotKillFeed(IReadOnlyList<SnapshotKillFeedEntry> killFeed)
    {
        // Advance local aging one step per snapshot received so entries expire
        // at roughly the same rate as they do on the server.
        _host.KillFeed.AdvanceKillFeed();

        // Additive merge: only add entries whose EventId is not yet in the local
        // list. This prevents flicker when the kill feed is absent from a snapshot
        // due to budget trimming, and lets the client maintain its own state.
        for (var killFeedIndex = 0; killFeedIndex < killFeed.Count; killFeedIndex += 1)
        {
            var entry = killFeed[killFeedIndex];
            if (_host.PresentationEvents.ContainsKillFeedEventId(entry.EventId))
            {
                continue;
            }

            // Insert in EventId-ascending order so oldest entries are first and
            // new events always appear at the bottom of the feed.
            var isLocalInvolved = IsSnapshotKillFeedEntryLocalInvolved(entry, _host.LocalPlayer.Id);
            var lifetime = isLocalInvolved ? SimulationConstants.KillFeedLocalInvolvedLifetimeTicks : SimulationConstants.KillFeedLifetimeTicks;
            _host.PresentationEvents.InsertKillFeedEntryOrdered(new KillFeedEntry(
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
                entry.EventId)
            {
                AssistName = entry.AssistName,
                AssistTeam = (PlayerTeam)entry.AssistTeam,
                AssistPlayerId = entry.AssistPlayerId,
                InvolvedPlayerIds = entry.InvolvedPlayerIds,
            }, lifetime);
        }

        // Keep the local list bounded in case the client accumulates more entries
        // than the server would normally allow.
        _host.PresentationEvents.TrimKillFeed();
    }

    private static bool IsSnapshotKillFeedEntryLocalInvolved(SnapshotKillFeedEntry entry, int localPlayerId)
    {
        if (localPlayerId > 0 && (entry.KillerPlayerId == localPlayerId || entry.VictimPlayerId == localPlayerId || entry.AssistPlayerId == localPlayerId))
        {
            return true;
        }

        var involvedPlayerIds = entry.InvolvedPlayerIds;
        for (var index = 0; index < involvedPlayerIds.Count; index += 1)
        {
            if (involvedPlayerIds[index] == localPlayerId)
            {
                return true;
            }
        }

        return false;
    }
}
