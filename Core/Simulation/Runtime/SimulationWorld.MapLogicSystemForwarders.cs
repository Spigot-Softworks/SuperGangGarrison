namespace OpenGarrison.Core;

// Forwarders kept for callers outside the world partials (Client, Server, bots,
// plugins, tests). Callers should move to the system directly over time.
public sealed partial class SimulationWorld
{
    public void AdvanceAuthoritativeMapLogicRuntime()
        => MapLogic.AdvanceAuthoritativeMapLogicRuntime();
    public void EvaluateMapLogicGraph(bool resetStatefulNodes = true)
        => MapLogic.EvaluateMapLogicGraph(resetStatefulNodes);
    public bool PulseMapLogicNode(int nodeIndex)
        => MapLogic.PulseMapLogicNode(nodeIndex);
    public void RefreshMapLogicRuntimeIfControlPointInputsChanged()
        => MapLogic.RefreshMapLogicRuntimeIfControlPointInputsChanged();
    public void SyncMapLogicRuntimeFromAuthoritativeControlPoints(bool newRound)
        => MapLogic.SyncMapLogicRuntimeFromAuthoritativeControlPoints(newRound);
    public void TickMapLogicTimers()
        => MapLogic.TickMapLogicTimers();
    public void TickMapLogicTimersOncePerFrame()
        => MapLogic.TickMapLogicTimersOncePerFrame();
    public void ApplyExplosiveDamageToDamageableZones(float originX, float originY, float blastRadius, float damage, float splashThresholdFactor = 0f, int excludeRoomObjectIndex = -1, PlayerTeam? damagingTeam = null, float minimumSplashDamage = 0f)
        => MapLogic.ApplyExplosiveDamageToDamageableZones(originX, originY, blastRadius, damage, splashThresholdFactor, excludeRoomObjectIndex, damagingTeam, minimumSplashDamage);
    public bool BlocksProjectileDamageableZone(int roomObjectIndex)
        => MapLogic.BlocksProjectileDamageableZone(roomObjectIndex);
    public float GetDamageableZoneHealth(int roomObjectIndex)
        => MapLogic.GetDamageableZoneHealth(roomObjectIndex);
    public bool TryApplyDamageableZoneDamage(int roomObjectIndex, float damage, PlayerTeam? damagingTeam = null)
        => MapLogic.TryApplyDamageableZoneDamage(roomObjectIndex, damage, damagingTeam);
    public int GetSpritesheetFrame(int roomObjectIndex)
        => MapLogic.GetSpritesheetFrame(roomObjectIndex);
    public SpritesheetPlaybackState SpritesheetPlaybackState => MapLogic.SpritesheetPlaybackState;
    public void TickSpritesheetPlayback()
        => MapLogic.TickSpritesheetPlayback();
    public void TickForegroundSpriteJungle()
        => MapLogic.TickForegroundSpriteJungle();
}
