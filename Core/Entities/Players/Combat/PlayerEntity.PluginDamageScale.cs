namespace OpenGarrison.Core;

// A plugin-set scale on the damage a player takes (the damage_taken_scale status), such as a
// class that is tougher in one stance. It lasts until a world frame, so a plugin keeps it up
// by refreshing it each tick.
public sealed partial class PlayerEntity
{
    public const float MinPluginDamageTakenScale = 0.05f;
    public const float MaxPluginDamageTakenScale = 10f;

    private float _pluginDamageTakenScale = 1f;
    private long _pluginDamageTakenScaleUntilFrame = -1;

    public void SetPluginDamageTakenScale(float scale, long untilFrame)
    {
        _pluginDamageTakenScale = Math.Clamp(scale, MinPluginDamageTakenScale, MaxPluginDamageTakenScale);
        _pluginDamageTakenScaleUntilFrame = untilFrame;
    }

    public float GetPluginDamageTakenScale(long frame)
        => IsAlive && frame <= _pluginDamageTakenScaleUntilFrame ? _pluginDamageTakenScale : 1f;
}
