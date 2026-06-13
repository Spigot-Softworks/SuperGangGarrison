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
        _processedNetworkDamageEventIds.Clear();
        _processedNetworkDamageEventOrder.Clear();
        _processedKillFeedEventIds.Clear();
        _processedKillFeedEventOrder.Clear();
    }

    private static bool ShouldProcessNetworkEvent(ulong eventId, HashSet<ulong> processedIds, Queue<ulong> processedOrder)
    {
        if (eventId == 0)
        {
            return true;
        }

        if (HasProcessedNetworkEvent(eventId, processedIds))
        {
            return false;
        }

        MarkProcessedNetworkEvent(eventId, processedIds, processedOrder);
        return true;
    }

    private static bool HasProcessedNetworkEvent(ulong eventId, HashSet<ulong> processedIds)
    {
        return eventId != 0 && processedIds.Contains(eventId);
    }

    private static void MarkProcessedNetworkEvent(ulong eventId, HashSet<ulong> processedIds, Queue<ulong> processedOrder)
    {
        if (eventId == 0 || !processedIds.Add(eventId))
        {
            return;
        }

        processedOrder.Enqueue(eventId);
        while (processedOrder.Count > ProcessedNetworkEventHistoryLimit)
        {
            processedIds.Remove(processedOrder.Dequeue());
        }
    }
}
