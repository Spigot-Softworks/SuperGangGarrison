namespace OpenGarrison.Client.Plugins;

/// <summary>
/// The kind of entity targeted by a damage event.
/// </summary>
public enum DamageTargetKind : byte
{
    /// <summary>No target.</summary>
    None = 0,
    /// <summary>A player was targeted.</summary>
    Player = 1,
    /// <summary>A sentry was targeted.</summary>
    Sentry = 2,
    /// <summary>A generator was targeted.</summary>
    Generator = 3,
}
