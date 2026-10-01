#nullable enable

using System.Collections.Generic;

namespace OpenGarrison.Client;

public partial class Game1
{
    public void ResetProcessedNetworkEventHistory()
    {
        _audioManager.Events.ResetProcessedNetworkSoundEventHistory();
        _processedNetworkVisualEventIds.Clear();
        _processedNetworkVisualEventOrder.Clear();
        _pluginManager.Events.ResetProcessedNetworkDamageEventHistory();
        _audioManager.Events.ResetProcessedKillFeedEventHistory();
    }

    public static bool ShouldProcessNetworkEvent(ulong eventId, HashSet<ulong> processedIds, Queue<ulong> processedOrder)
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

    public static bool HasProcessedNetworkEvent(ulong eventId, HashSet<ulong> processedIds)
    {
        return eventId != 0 && processedIds.Contains(eventId);
    }

    public static void MarkProcessedNetworkEvent(ulong eventId, HashSet<ulong> processedIds, Queue<ulong> processedOrder)
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
