namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public bool CanNetworkPlayerChangeTeamInCurrentMode(byte slot)
        => VipRules.CanNetworkPlayerChangeTeamInCurrentMode(slot);
    public bool CanNetworkPlayerSelectClassInCurrentMode(byte slot, CharacterClassDefinition definition)
        => VipRules.CanNetworkPlayerSelectClassInCurrentMode(slot, definition);
    public void ConfigurePracticeVipRules(bool enabled)
        => VipRules.ConfigurePracticeVipRules(enabled);
    public bool IsVipModeActive => VipRules.IsVipModeActive;
    public bool TryGetVipSlot(PlayerTeam team, out byte slot)
        => VipRules.TryGetVipSlot(team, out slot);
    public bool TrySetPreferredVipSlot(PlayerTeam team, byte slot)
        => VipRules.TrySetPreferredVipSlot(team, slot);
    public int VipAssignmentVersion => VipRules.VipAssignmentVersion;
    public bool VipRequiresDualVip => VipRules.VipRequiresDualVip;
    public IReadOnlyDictionary<PlayerTeam, byte> VipSlotsByTeam => VipRules.VipSlotsByTeam;
    public bool VipWarmupActive => VipRules.VipWarmupActive;
    public int VipWarmupTicksRemaining => VipRules.VipWarmupTicksRemaining;
}
