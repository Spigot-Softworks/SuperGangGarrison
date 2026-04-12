#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.BotAI;

/// <summary>
/// Interface for bot reaction/emote controllers that trigger chat bubble emotes
/// based on observed game events. Implementations can be used client-side (practice)
/// or server-side (online bots).
/// </summary>
public interface IBotReactionController
{
    /// <summary>
    /// Resets all reaction state for a fresh session.
    /// </summary>
    void Reset();

    /// <summary>
    /// Called once per simulation tick to update bot reaction states and trigger emotes.
    /// </summary>
    /// <param name="world">The simulation world.</param>
    /// <param name="controlledBotSlots">The bot slots owned by this controller.</param>
    /// <param name="observedEvents">Optional observed events from other participants.</param>
    void UpdateReactions(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledBotSlots,
        BotObservedEvents? observedEvents = null);
}

/// <summary>
/// Observed events from other participants that bots may react to.
/// </summary>
public readonly struct BotObservedEvents
{
    public BotObservedEvents(
        bool localPlayerAcquiredWeapon,
        bool localPlayerMultiKill,
        bool localPlayerStartedRaging,
        bool captureCelebration,
        IReadOnlyList<BotObservedDeath>? observedDeaths)
    {
        LocalPlayerAcquiredWeapon = localPlayerAcquiredWeapon;
        LocalPlayerMultiKill = localPlayerMultiKill;
        LocalPlayerStartedRaging = localPlayerStartedRaging;
        CaptureCelebration = captureCelebration;
        ObservedDeaths = observedDeaths ?? [];
    }

    /// <summary>
    /// Whether the local/authoritative player picked up a weapon.
    /// </summary>
    public bool LocalPlayerAcquiredWeapon { get; init; }

    /// <summary>
    /// Whether the local/authoritative player achieved a multi-kill (3+).
    /// </summary>
    public bool LocalPlayerMultiKill { get; init; }

    /// <summary>
    /// Whether the local/authoritative player started raging.
    /// </summary>
    public bool LocalPlayerStartedRaging { get; init; }

    /// <summary>
    /// Whether a cap celebration should trigger.
    /// </summary>
    public bool CaptureCelebration { get; init; }

    /// <summary>
    /// Positions and slots of observed bot deaths on the opposing team.
    /// </summary>
    public IReadOnlyList<BotObservedDeath> ObservedDeaths { get; init; } = [];
}

/// <summary>
/// Represents an observed bot death for reaction purposes.
/// Uses raw float X/Y instead of Vector2 to avoid MonoGame dependency.
/// </summary>
public readonly struct BotObservedDeath
{
    public BotObservedDeath(float x, float y, byte slot)
    {
        X = x;
        Y = y;
        Slot = slot;
    }

    public float X { get; init; }
    public float Y { get; init; }
    public byte Slot { get; init; }
}

/// <summary>
/// Bot reaction state machine for Last-to-Die style emote behavior.
/// This can be used client-side for practice bots or server-side for online bots.
/// </summary>
public sealed class BotReactionController : IBotReactionController
{
    // Reaction frame constants (must match asset indices)
    private const int ReactionFrameZ1 = 20;  // Teammate died
    private const int ReactionFrameZ2 = 21;  // Idle
    private const int ReactionFrameZ4 = 23;  // Enemy picked up weapon
    private const int ReactionFrameZ5 = 24;  // Victory celebration
    private const int ReactionFrameZ6 = 25;  // Enemy spotted
    private const int ReactionFrameZ7 = 26;  // Enemy is raging
    private const int ReactionFrameZ9 = 28;  // Enemy multi-kill
    private const int ReactionFrameMedic = 45; // Low health medic call

    // Distance thresholds
    private const float AwarenessDistance = 900f;
    private const float TeammateDeathDistance = 420f;
    private const float LowHealthFraction = 0.35f;
    private const float LowHealthRecoveryFraction = 0.55f;

    // Cooldown constants
    private const double ReactionCooldownSeconds = 3.0;
    private const double IdleReactionCooldownSeconds = 8.0;

    private sealed class BotReactionState
    {
        public bool WasAlive { get; set; }
        public int LastKnownHealth { get; set; }
        public float LastKnownX { get; set; }
        public float LastKnownY { get; set; }
        public bool SawTargetLastTick { get; set; }
        public bool LowHealthCallIssued { get; set; }
        public long LastDamageTakenFrame { get; set; }
        public long LastReactionFrame { get; set; } = long.MinValue / 4;
        public long LastIdleReactionFrame { get; set; } = long.MinValue / 4;
    }

    private readonly Dictionary<byte, BotReactionState> _reactionStates = [];
    private readonly int _ticksPerSecond;

    // Observed state from previous tick
    private bool _previousLocalPlayerAcquiredWeapon;
    private int _previousLocalMultiKillCount;
    private bool _previousLocalRaging;
    private bool _captureCelebrated;

    public BotReactionController(int ticksPerSecond)
    {
        _ticksPerSecond = ticksPerSecond;
    }

    public void Reset()
    {
        _reactionStates.Clear();
        _previousLocalPlayerAcquiredWeapon = false;
        _previousLocalMultiKillCount = 0;
        _previousLocalRaging = false;
        _captureCelebrated = false;
    }

    public void UpdateReactions(
        SimulationWorld world,
        IReadOnlyDictionary<byte, ControlledBotSlot> controlledBotSlots,
        BotObservedEvents? observedEvents = null)
    {
        if (controlledBotSlots.Count == 0)
        {
            return;
        }

        // Get the "target" player - the one bots react to.
        // On client: this is LocalPlayer
        // On server: this could be a configurable observer or omitted for pure ambient reactions
        PlayerEntity? targetPlayer = null;
        if (world.TryGetNetworkPlayer(SimulationWorld.LocalPlayerSlot, out var local))
        {
            targetPlayer = local;
        }

        // Determine what local events occurred this tick
        var acquiredWeapon = false;
        var multiKill = false;
        var startedRaging = false;
        var captureCelebration = false;

        if (targetPlayer != null && observedEvents.HasValue)
        {
            var events = observedEvents.Value;

            acquiredWeapon = events.LocalPlayerAcquiredWeapon
                && !_previousLocalPlayerAcquiredWeapon;
            multiKill = events.LocalPlayerMultiKill
                && _previousLocalMultiKillCount < 3;
            startedRaging = events.LocalPlayerStartedRaging
                && !_previousLocalRaging;
            captureCelebration = events.CaptureCelebration
                && !_captureCelebrated;
        }

        // Collect observed enemy deaths (for teammate death reactions)
        var observedDeaths = observedEvents?.ObservedDeaths ?? [];

        // Prune stale states
        PruneReactionStates(controlledBotSlots.Keys);

        // Update each bot's reaction state
        foreach (var entry in controlledBotSlots)
        {
            var slot = entry.Key;
            var botSlot = entry.Value;

            if (!world.TryGetNetworkPlayer(slot, out var bot) || bot is null)
            {
                continue;
            }

            var state = GetOrCreateReactionState(slot, bot, world.Frame);

            if (!bot.IsAlive)
            {
                state.WasAlive = false;
                state.SawTargetLastTick = false;
                state.LowHealthCallIssued = false;
                state.LastKnownHealth = bot.Health;
                state.LastKnownX = bot.X;
                state.LastKnownY = bot.Y;
                continue;
            }

            // Initialize state on respawn
            if (!state.WasAlive)
            {
                state.LastDamageTakenFrame = world.Frame;
                state.LastIdleReactionFrame = world.Frame;
                state.LowHealthCallIssued = false;
            }

            // Track damage
            if (state.LastKnownHealth > bot.Health)
            {
                state.LastDamageTakenFrame = world.Frame;
                state.LastIdleReactionFrame = world.Frame;
            }

            // Determine what the bot observes
            var seesTarget = targetPlayer != null && CanBotSeeTarget(bot, targetPlayer, world);
            var sawTeammateDie = CanBotSeeObservedDeath(bot, slot, observedDeaths, world);
            var lowHealthRequested = ShouldCallMedic(bot, state);
            var idleReactionRequested = ShouldTriggerIdleReaction(bot, state, seesTarget, world);

            // Determine which emote to play
            var reactionFrame = DetermineReactionFrame(
                captureCelebration, startedRaging, multiKill, acquiredWeapon,
                seesTarget, sawTeammateDie, lowHealthRequested, idleReactionRequested,
                state);

            // Trigger the emote
            if (reactionFrame >= 0 && TryTriggerReaction(world, slot, reactionFrame, state))
            {
                if (lowHealthRequested)
                {
                    state.LowHealthCallIssued = true;
                }

                if (idleReactionRequested)
                {
                    state.LastIdleReactionFrame = world.Frame;
                }
            }

            // Clear low health call when recovered
            if (GetHealthFraction(bot) >= LowHealthRecoveryFraction)
            {
                state.LowHealthCallIssued = false;
            }

            // Update state for next tick
            state.WasAlive = true;
            state.SawTargetLastTick = seesTarget;
            state.LastKnownHealth = bot.Health;
            state.LastKnownX = bot.X;
            state.LastKnownY = bot.Y;
        }

        // Update observed state for next tick
        if (targetPlayer != null)
        {
            _previousLocalPlayerAcquiredWeapon = targetPlayer.AcquiredWeaponClassId.HasValue;
            _previousLocalMultiKillCount = targetPlayer.CurrentMultiKillCount;
            _previousLocalRaging = targetPlayer.IsRaging;
        }

        if (captureCelebration)
        {
            _captureCelebrated = true;
        }
    }

    private void PruneReactionStates(IEnumerable<byte> activeSlots)
    {
        var staleSlots = new List<byte>();
        foreach (var slot in _reactionStates.Keys)
        {
            if (!activeSlots.Contains(slot))
            {
                staleSlots.Add(slot);
            }
        }

        foreach (var slot in staleSlots)
        {
            _reactionStates.Remove(slot);
        }
    }

    private BotReactionState GetOrCreateReactionState(byte slot, PlayerEntity bot, long currentFrame)
    {
        if (_reactionStates.TryGetValue(slot, out var state))
        {
            return state;
        }

        state = new BotReactionState
        {
            WasAlive = bot.IsAlive,
            LastKnownHealth = bot.Health,
            LastKnownX = bot.X,
            LastKnownY = bot.Y,
            LastDamageTakenFrame = currentFrame,
            LastIdleReactionFrame = currentFrame,
        };
        _reactionStates[slot] = state;
        return state;
    }

    private int DetermineReactionFrame(
        bool captureCelebration, bool startedRaging, bool multiKill, bool acquiredWeapon,
        bool seesTarget, bool sawTeammateDie, bool lowHealthRequested, bool idleReactionRequested,
        BotReactionState state)
    {
        // Priority order (highest to lowest):
        // 1. Victory celebration (always triggers)
        // 2. Enemy raging (bypasses cooldown)
        // 3. Enemy multi-kill (bypasses cooldown)
        // 4. Enemy picked up weapon
        // 5. Enemy spotted (first time seeing target)
        // 6. Teammate died
        // 7. Low health medic call
        // 8. Idle reaction

        if (captureCelebration)
        {
            return ReactionFrameZ5;
        }

        if (startedRaging && seesTarget)
        {
            return ReactionFrameZ7;
        }

        if (multiKill && seesTarget)
        {
            return ReactionFrameZ9;
        }

        if (acquiredWeapon && seesTarget)
        {
            return ReactionFrameZ4;
        }

        if (seesTarget && !state.SawTargetLastTick)
        {
            return ReactionFrameZ6;
        }

        if (sawTeammateDie)
        {
            return ReactionFrameZ1;
        }

        if (lowHealthRequested)
        {
            return ReactionFrameMedic;
        }

        if (idleReactionRequested)
        {
            return ReactionFrameZ2;
        }

        return -1;
    }

    private bool TryTriggerReaction(SimulationWorld world, byte slot, int bubbleFrame, BotReactionState state)
    {
        var cooldownTicks = Math.Max(1, _ticksPerSecond * ReactionCooldownSeconds);

        // Certain high-priority reactions bypass cooldown
        if (!BypassesCooldown(bubbleFrame) && world.Frame - state.LastReactionFrame < cooldownTicks)
        {
            return false;
        }

        if (!world.TryTriggerNetworkPlayerChatBubble(slot, bubbleFrame))
        {
            return false;
        }

        state.LastReactionFrame = world.Frame;
        return true;
    }

    private static bool BypassesCooldown(int bubbleFrame)
    {
        return bubbleFrame is ReactionFrameZ1 or ReactionFrameZ4 or ReactionFrameZ7 or ReactionFrameZ9;
    }

    private bool CanBotSeeTarget(PlayerEntity bot, PlayerEntity target, SimulationWorld world)
    {
        if (!bot.IsAlive || !target.IsAlive)
        {
            return false;
        }

        var maxDistanceSquared = AwarenessDistance * AwarenessDistance;
        var dx = target.X - bot.X;
        var dy = target.Y - bot.Y;
        var distanceSquared = dx * dx + dy * dy;

        return distanceSquared <= maxDistanceSquared
            && world.CombatTestHasLineOfSight(bot, target);
    }

    private bool CanBotSeeObservedDeath(
        PlayerEntity bot,
        byte observingSlot,
        IReadOnlyList<BotObservedDeath> observedDeaths,
        SimulationWorld world)
    {
        if (!bot.IsAlive || observedDeaths.Count == 0)
        {
            return false;
        }

        var maxDistanceSquared = TeammateDeathDistance * TeammateDeathDistance;

        for (var index = 0; index < observedDeaths.Count; index++)
        {
            var death = observedDeaths[index];
            if (death.Slot == observingSlot)
            {
                continue;
            }

            var dx = death.X - bot.X;
            var dy = death.Y - bot.Y;
            var distanceSquared = dx * dx + dy * dy;

            if (distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            if (world.CombatTestHasObstacleLineOfSight(bot.X, bot.Y, death.X, death.Y))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ShouldCallMedic(PlayerEntity bot, BotReactionState state)
    {
        return bot.IsAlive
            && !state.LowHealthCallIssued
            && GetHealthFraction(bot) <= LowHealthFraction;
    }

    private bool ShouldTriggerIdleReaction(PlayerEntity bot, BotReactionState state, bool seesTarget, SimulationWorld world)
    {
        if (!bot.IsAlive || seesTarget)
        {
            return false;
        }

        var idleThresholdTicks = Math.Max(1, _ticksPerSecond * IdleReactionCooldownSeconds);
        return world.Frame - state.LastDamageTakenFrame >= idleThresholdTicks
            && world.Frame - state.LastIdleReactionFrame >= idleThresholdTicks;
    }

    private static float GetHealthFraction(PlayerEntity player)
    {
        return player.MaxHealth <= 0
            ? 0f
            : Math.Clamp(player.Health / (float)player.MaxHealth, 0f, 1f);
    }
}
