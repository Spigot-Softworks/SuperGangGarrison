using System.Collections.Generic;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    private readonly Dictionary<int, int> _dominationKillCounts = new();

    public int ActiveDominationCount { get; private set; }

    public bool IsDominatingLocalViewer { get; private set; }

    public bool IsDominatedByLocalViewer { get; private set; }

    public int GetDominationKillCount(int otherPlayerId)
    {
        return _dominationKillCounts.TryGetValue(otherPlayerId, out var count)
            ? count
            : 0;
    }

    public void IncrementDominationKillCount(int otherPlayerId)
    {
        if (otherPlayerId <= 0)
        {
            return;
        }

        var previousCount = GetDominationKillCount(otherPlayerId);
        var nextCount = previousCount + 1;
        _dominationKillCounts[otherPlayerId] = nextCount;
        if (previousCount <= 3 && nextCount > 3)
        {
            ActiveDominationCount += 1;
        }
    }

    public void ClearDominationKillCount(int otherPlayerId)
    {
        if (!_dominationKillCounts.TryGetValue(otherPlayerId, out var previousCount))
        {
            return;
        }

        _dominationKillCounts.Remove(otherPlayerId);
        if (previousCount > 3)
        {
            ActiveDominationCount = int.Max(0, ActiveDominationCount - 1);
        }
    }

    public void ClearDominations()
    {
        _dominationKillCounts.Clear();
        ActiveDominationCount = 0;
        IsDominatingLocalViewer = false;
        IsDominatedByLocalViewer = false;
    }

    public void ApplyDominationViewState(int activeDominationCount, bool isDominatingLocalViewer, bool isDominatedByLocalViewer)
    {
        ActiveDominationCount = int.Max(0, activeDominationCount);
        IsDominatingLocalViewer = isDominatingLocalViewer;
        IsDominatedByLocalViewer = isDominatedByLocalViewer;
    }
}
