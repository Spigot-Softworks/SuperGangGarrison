#nullable enable

using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    private void ResetProcessedNetworkEventHistory()
    {
        _processedNetworkSoundEventIds.Clear();
        _processedNetworkSoundEventOrder.Clear();
        _processedNetworkVisualEventIds.Clear();
        _processedNetworkVisualEventOrder.Clear();
        _processedKillFeedEventIds.Clear();
        _processedKillFeedEventOrder.Clear();
    }

    private static bool ShouldProcessNetworkEvent(ulong eventId, HashSet<ulong> processedIds, Queue<ulong> processedOrder)
    {
        if (eventId == 0)
        {
            return true;
        }

        if (!processedIds.Add(eventId))
        {
            return false;
        }

        processedOrder.Enqueue(eventId);
        while (processedOrder.Count > ProcessedNetworkEventHistoryLimit)
        {
            processedIds.Remove(processedOrder.Dequeue());
        }

        return true;
    }
}
