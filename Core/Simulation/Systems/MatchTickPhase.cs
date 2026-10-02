namespace OpenGarrison.Core;

/// <summary>World-level match steps the match phase sequences within a tick.</summary>
internal interface IMatchPhaseHost
{
    byte LocalPlayerSlot { get; }

    // Concrete type on purpose: iterating it every tick must not allocate an enumerator.
    SortedSet<byte> EnabledAdditionalPlayerSlots { get; }

    bool EnemyPlayerEnabled { get; }

    PlayerEntity EnemyPlayer { get; }

    PlayerEntity FriendlyDummy { get; }

    bool TryGetNetworkPlayer(byte slot, out PlayerEntity player);

    void ApplyExperimentalRageEffects();

    void AdvanceMedicUberEffects();

    void AdvanceVipState();

    void AdvanceKillFeed();

    void AdvanceLocalDeathCam();

    void EmitPendingMedicUberReadyPresentation();

    void AdvanceExperimentalRageState();

    void UpdateAuxiliaryControlPointStateIfNeeded();

    void TickForegroundSpriteJungle();

    void TickSpritesheetPlayback();
}

/// <summary>Match-facing slices of a tick: effects, presentation, chat bubbles, objectives, and resolution.</summary>
internal sealed class MatchTickPhase : IMatchTickPhase
{
    private readonly IMatchPhaseHost _host;
    private readonly MatchObjectiveSystem _objectives;

    public MatchTickPhase(IMatchPhaseHost host, MatchObjectiveSystem objectives)
    {
        _host = host;
        _objectives = objectives;
    }

    public void AdvancePrePlayerMatchPhase()
    {
        _host.ApplyExperimentalRageEffects();
        _host.AdvanceMedicUberEffects();
        _host.AdvanceVipState();
    }

    public void AdvancePresentationAndChatPhase()
    {
        _host.AdvanceKillFeed();
        _host.AdvanceLocalDeathCam();

        AdvanceNetworkPlayerChatBubbleState(_host.LocalPlayerSlot);
        foreach (var slot in _host.EnabledAdditionalPlayerSlots)
        {
            AdvanceNetworkPlayerChatBubbleState(slot);
        }

        if (_host.EnemyPlayerEnabled)
        {
            _host.EnemyPlayer.AdvanceChatBubbleState();
        }

        _host.FriendlyDummy.AdvanceChatBubbleState();
    }

    public void AdvancePostPlayerMatchPhase()
    {
        _host.EmitPendingMedicUberReadyPresentation();
        _host.AdvanceExperimentalRageState();
        _objectives.AdvanceObjectives();
        _host.UpdateAuxiliaryControlPointStateIfNeeded();
        _host.TickForegroundSpriteJungle();
        _host.TickSpritesheetPlayback();
        _objectives.AdvanceResolution();
    }

    private void AdvanceNetworkPlayerChatBubbleState(byte slot)
    {
        if (_host.TryGetNetworkPlayer(slot, out var player))
        {
            player.AdvanceChatBubbleState();
        }
    }
}
