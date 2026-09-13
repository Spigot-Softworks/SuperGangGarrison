#nullable enable
using System;
using System.Linq;
using OpenGarrison.Core;
using OpenGarrison.Protocol;

namespace OpenGarrison.Client;

public partial class Game1
{
    private (string Map, int Area)? _offlinePracticeNextMap;
    private double _offlinePracticeMapChangeAt;
    private bool IsOfflinePracticeVote => IsPracticeSessionActive && !_networkClient.IsConnected;
    private void OpenPracticeVoteMenu()
    {
        if (!IsOfflinePracticeVote) { _networkClient.SendVoteCommand(VoteCommandKind.OpenMenu); return; }
        var maps = BuildAllPracticeMapEntries().Select(map => new VoteMenuMapEntry(map.LevelName, map.DisplayName, 1)).ToArray();
        HandleVoteMenuMessage(new VoteMenuMessage(maps, [], false, false, 0, 0));
    }
    private void SubmitOfflinePracticeMapVote(string map, int area, bool nextRound)
    {
        if (!IsOfflinePracticeVote) return;
        if (nextRound)
        {
            _offlinePracticeNextMap = (map, area);
            _offlinePracticeMapChangeAt = 0;
            _menuStatusMessage = "Next map: " + map;
        }
        else ChangeOfflinePracticeMap(map, area);
    }
    private void ChangeOfflinePracticeMap(string map, int area)
    {
        _offlinePracticeNextMap = null; _offlinePracticeMapChangeAt = 0;
        BeginPracticeSession(map);
        if (area > 1) _world.TryLoadLevel(map, area, preservePlayerStats: false);
    }
    private void UpdateOfflinePracticeMapVote()
    {
        if (!IsOfflinePracticeVote || _offlinePracticeNextMap is not { } next || !_world.MatchState.IsEnded) return;
        if (_offlinePracticeMapChangeAt == 0) _offlinePracticeMapChangeAt = VoiceClockSeconds + 5;
        if (VoiceClockSeconds >= _offlinePracticeMapChangeAt) ChangeOfflinePracticeMap(next.Map, next.Area);
    }
}
