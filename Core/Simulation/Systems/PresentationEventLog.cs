namespace OpenGarrison.Core;

/// <summary>
/// Owns the transient, presentation-facing state produced by the simulation:
/// queued sound/visual/gib/healing events, combat traces, sniper aim indicators,
/// and the kill feed. Rocket spawn events are owned by <see cref="ProjectileSystem"/>
/// and damage events by <see cref="CombatSystem"/>.
/// </summary>
internal sealed class PresentationEventLog
{
    /// <summary>Maximum number of entries retained in the kill feed.</summary>
    public const int MaxKillFeedEntries = 5;

    private readonly List<WorldSoundEvent> _soundEvents = new();
    private readonly List<WorldVisualEvent> _visualEvents = new();
    private readonly List<WorldGibSpawnEvent> _gibSpawnEvents = new();
    private readonly List<WorldHealingEvent> _healingEvents = new();
    private readonly List<WorldGameplayAbilityEvent> _gameplayAbilityEvents = new();
    private readonly List<CombatTrace> _combatTraces = new();
    private readonly List<SniperAimIndicator> _sniperAimIndicators = new();
    private readonly List<KillFeedEntry> _killFeed = new();
    private readonly List<int> _killFeedEntryLifetimes = new();
    private ulong _nextKillFeedEventId = 1;
    private long _lastKillFeedRecordedFrame = -1;

    public IReadOnlyList<WorldSoundEvent> SoundEvents => _soundEvents;
    public IReadOnlyList<WorldVisualEvent> VisualEvents => _visualEvents;
    public IReadOnlyList<WorldHealingEvent> HealingEvents => _healingEvents;
    public IReadOnlyList<CombatTrace> CombatTraces => _combatTraces;
    public IReadOnlyList<SniperAimIndicator> SniperAimIndicators => _sniperAimIndicators;
    public IReadOnlyList<KillFeedEntry> KillFeed => _killFeed;

    // Sound events

    public void AddSoundEvent(WorldSoundEvent soundEvent) => _soundEvents.Add(soundEvent);

    public bool ContainsSoundEvent(ulong eventId)
        => _soundEvents.Exists(pending => pending.EventId == eventId);

    public IReadOnlyList<WorldSoundEvent> DrainSoundEvents() => Drain(_soundEvents);

    // Visual events

    public void AddVisualEvent(WorldVisualEvent visualEvent) => _visualEvents.Add(visualEvent);

    public IReadOnlyList<WorldVisualEvent> DrainVisualEvents() => Drain(_visualEvents);

    // Gib spawn events

    public void AddGibSpawnEvent(WorldGibSpawnEvent gibSpawnEvent) => _gibSpawnEvents.Add(gibSpawnEvent);

    public IReadOnlyList<WorldGibSpawnEvent> DrainGibSpawnEvents() => Drain(_gibSpawnEvents);

    // Healing events

    public void AddHealingEvent(WorldHealingEvent healingEvent) => _healingEvents.Add(healingEvent);

    public IReadOnlyList<WorldHealingEvent> DrainHealingEvents() => Drain(_healingEvents);

    // Gameplay ability events (drained by the host; not cleared on round restart)

    public IReadOnlyList<WorldGameplayAbilityEvent> GameplayAbilityEvents => _gameplayAbilityEvents;

    public void AddGameplayAbilityEvent(WorldGameplayAbilityEvent abilityEvent)
        => _gameplayAbilityEvents.Add(abilityEvent);

    public IReadOnlyList<WorldGameplayAbilityEvent> DrainGameplayAbilityEvents()
        => Drain(_gameplayAbilityEvents);

    // Combat traces

    public void AddCombatTrace(CombatTrace trace) => _combatTraces.Add(trace);

    public void ClearCombatTraces() => _combatTraces.Clear();

    /// <summary>Ages every trace by one tick and drops the ones that expire.</summary>
    public void AdvanceCombatTraces()
    {
        for (var traceIndex = _combatTraces.Count - 1; traceIndex >= 0; traceIndex -= 1)
        {
            var trace = _combatTraces[traceIndex];
            if (trace.TicksRemaining <= 1)
            {
                _combatTraces.RemoveAt(traceIndex);
                continue;
            }

            _combatTraces[traceIndex] = trace with { TicksRemaining = trace.TicksRemaining - 1 };
        }
    }

    // Sniper aim indicators

    public void AddSniperAimIndicator(SniperAimIndicator indicator) => _sniperAimIndicators.Add(indicator);

    public void ClearSniperAimIndicators() => _sniperAimIndicators.Clear();

    // Kill feed

    public ulong AllocateKillFeedEventId() => _nextKillFeedEventId++;

    public bool ContainsKillFeedEventId(ulong eventId)
    {
        for (var index = 0; index < _killFeed.Count; index += 1)
        {
            if (_killFeed[index].EventId == eventId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Ages the oldest entry by one tick and removes it once it expires.</summary>
    public void AdvanceKillFeed()
    {
        if (_killFeed.Count == 0)
        {
            return;
        }

        _killFeedEntryLifetimes[0] -= 1;
        if (_killFeedEntryLifetimes[0] > 0)
        {
            return;
        }

        _killFeed.RemoveAt(0);
        _killFeedEntryLifetimes.RemoveAt(0);
    }

    /// <summary>
    /// Appends an entry recorded by the local simulation at <paramref name="frame"/>.
    /// An entry identical to the previous one recorded on the same frame is dropped.
    /// </summary>
    public void AppendKillFeedEntry(KillFeedEntry entry, long frame, int lifetimeTicks)
    {
        if (IsDuplicateOfPreviousEntry(entry, frame))
        {
            return;
        }

        _killFeed.Add(entry);
        _killFeedEntryLifetimes.Add(lifetimeTicks);
        _lastKillFeedRecordedFrame = frame;
        TrimKillFeed();
    }

    /// <summary>
    /// Inserts an authoritative entry received from a snapshot, keeping the feed ordered
    /// by ascending event id so newer events always appear at the bottom.
    /// </summary>
    public void InsertKillFeedEntryOrdered(KillFeedEntry entry, int lifetimeTicks)
    {
        var insertIndex = _killFeed.Count;
        for (var index = 0; index < _killFeed.Count; index += 1)
        {
            if (_killFeed[index].EventId > entry.EventId)
            {
                insertIndex = index;
                break;
            }
        }

        _killFeed.Insert(insertIndex, entry);
        _killFeedEntryLifetimes.Insert(insertIndex, lifetimeTicks);
    }

    /// <summary>Drops the oldest entries until the feed fits <see cref="MaxKillFeedEntries"/>.</summary>
    public void TrimKillFeed()
    {
        while (_killFeed.Count > MaxKillFeedEntries)
        {
            _killFeed.RemoveAt(0);
            _killFeedEntryLifetimes.RemoveAt(0);
        }
    }

    /// <summary>
    /// Clears the state that must not survive a round restart. Gib spawn events and the
    /// kill feed event id counter are intentionally preserved.
    /// </summary>
    public void ClearForRoundRestart()
    {
        _killFeedEntryLifetimes.Clear();
        _combatTraces.Clear();
        _killFeed.Clear();
        _soundEvents.Clear();
        _visualEvents.Clear();
        _healingEvents.Clear();
    }

    private bool IsDuplicateOfPreviousEntry(KillFeedEntry entry, long frame)
    {
        if (_killFeed.Count == 0 || _lastKillFeedRecordedFrame != frame)
        {
            return false;
        }

        var previousEntry = _killFeed[^1];
        return previousEntry.AssistName == entry.AssistName
            && previousEntry.AssistTeam == entry.AssistTeam
            && previousEntry.AssistPlayerId == entry.AssistPlayerId
            && previousEntry.KillerName == entry.KillerName
            && previousEntry.KillerTeam == entry.KillerTeam
            && previousEntry.WeaponSpriteName == entry.WeaponSpriteName
            && previousEntry.VictimName == entry.VictimName
            && previousEntry.VictimTeam == entry.VictimTeam
            && previousEntry.MessageText == entry.MessageText
            && previousEntry.MessageHighlightStart == entry.MessageHighlightStart
            && previousEntry.MessageHighlightLength == entry.MessageHighlightLength
            && previousEntry.KillerPlayerId == entry.KillerPlayerId
            && previousEntry.VictimPlayerId == entry.VictimPlayerId
            && InvolvedPlayerIdsEqual(previousEntry.InvolvedPlayerIds, entry.InvolvedPlayerIds)
            && previousEntry.SpecialType == entry.SpecialType;
    }

    private static bool InvolvedPlayerIdsEqual(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index += 1)
        {
            if (left[index] != right[index])
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<T> Drain<T>(List<T> queue)
    {
        if (queue.Count == 0)
        {
            return [];
        }

        var drained = queue.ToArray();
        queue.Clear();
        return drained;
    }
}
