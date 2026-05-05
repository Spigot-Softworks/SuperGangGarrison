#nullable enable

using System;
using System.IO;

namespace OpenGarrison.Client;

public partial class Game1
{
    private bool TryQueueLegacyReplay(string replayPath, bool addConsoleFeedback)
    {
        if (_networkClient.IsConnected && !_networkClient.IsReplayConnection)
        {
            if (addConsoleFeedback)
            {
                AddConsoleLine("replay queue is only available when idle or while a replay is playing.");
            }

            return false;
        }

        if (_gameplaySessionKind is GameplaySessionKind.Practice or GameplaySessionKind.LastToDie)
        {
            if (addConsoleFeedback)
            {
                AddConsoleLine("replay queue is unavailable during offline gameplay sessions.");
            }

            return false;
        }

        var normalizedReplayPath = replayPath.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReplayPath))
        {
            if (addConsoleFeedback)
            {
                AddConsoleLine("usage: replay_queue <path>");
            }

            return false;
        }

        if (!_networkClient.IsReplayConnection && string.IsNullOrWhiteSpace(_activeReplayPath))
        {
            return TryPlayLegacyReplay(normalizedReplayPath, addConsoleFeedback, clearQueuedReplays: false);
        }

        _queuedReplayPaths.Enqueue(normalizedReplayPath);
        if (addConsoleFeedback)
        {
            AddConsoleLine(
                $"queued replay {GetReplayDisplayName(normalizedReplayPath)} ({_queuedReplayPaths.Count} waiting)");
        }

        return true;
    }

    private bool TryHandleReplayDisconnect(string disconnectReason)
    {
        if (!string.Equals(disconnectReason, "Replay ended.", StringComparison.Ordinal))
        {
            _activeReplayPath = null;
            _queuedReplayPaths.Clear();
            return false;
        }

        var finishedReplayPath = _activeReplayPath;
        _activeReplayPath = null;
        if (_queuedReplayPaths.Count == 0)
        {
            return false;
        }

        var nextReplayPath = _queuedReplayPaths.Dequeue();
        if (TryPlayLegacyReplay(nextReplayPath, addConsoleFeedback: true, clearQueuedReplays: false))
        {
            AddConsoleLine(
                $"queued replay advanced: {GetReplayDisplayName(finishedReplayPath)} -> {GetReplayDisplayName(nextReplayPath)} ({_queuedReplayPaths.Count} remaining)");
            return true;
        }

        ReturnToMainMenu(_menuStatusMessage);
        return true;
    }

    private void ClearReplayQueue(bool clearActiveReplayPath)
    {
        _queuedReplayPaths.Clear();
        if (clearActiveReplayPath)
        {
            _activeReplayPath = null;
        }
    }

    private string GetReplayQueueStatus()
    {
        if (_queuedReplayPaths.Count == 0)
        {
            return "replay queue empty";
        }

        var nextReplayPath = _queuedReplayPaths.Peek();
        return $"replay queue count={_queuedReplayPaths.Count} next={GetReplayDisplayName(nextReplayPath)}";
    }

    private static string GetReplayDisplayName(string? replayPath)
    {
        if (string.IsNullOrWhiteSpace(replayPath))
        {
            return "(unknown)";
        }

        var trimmedPath = replayPath.Trim().Trim('"');
        return Path.GetFileName(trimmedPath);
    }
}
