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
            TimeLimitTicks = Math.Max(snapshot.TimeRemainingTicks, MatchRules.TimeLimitTicks),
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

    private void ApplySnapshotKillFeed(IReadOnlyList<SnapshotKillFeedEntry> killFeed)
    {
        _killFeed.Clear();
        for (var killFeedIndex = 0; killFeedIndex < killFeed.Count; killFeedIndex += 1)
        {
            var entry = killFeed[killFeedIndex];
            _killFeed.Add(new KillFeedEntry(
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
        }

        _killFeedTrimTicks = _killFeed.Count > 0 ? KillFeedLifetimeTicks : 0;
    }
}
