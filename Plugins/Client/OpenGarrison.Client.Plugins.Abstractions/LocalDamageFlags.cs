using System.Diagnostics.CodeAnalysis;
using System;

namespace OpenGarrison.Client.Plugins;

/// <summary>
/// Additional flags describing how damage was dealt.
/// </summary>
[Flags]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Public plugin API enum; the Flags suffix is intentional and descriptive.")]
public enum LocalDamageFlags : byte
{
    /// <summary>No special flags.</summary>
    None = 0,
    /// <summary>The target was hit mid-air (airshot).</summary>
    Airshot = 1 << 0,
    /// <summary>The damage was evaded.</summary>
    Evaded = 1 << 1,
    /// <summary>The damage involved a ghost dash.</summary>
    GhostDash = 1 << 2,
    /// <summary>The damage was blocked by a civilian umbrella.</summary>
    CivvieUmbrellaBlock = 1 << 3,
    /// <summary>The damage is an afterburn tick.</summary>
    AfterburnTick = 1 << 4,
    /// <summary>The target was gibbed.</summary>
    Gibbed = 1 << 5,
    /// <summary>The damage is a status effect tick.</summary>
    StatusTick = 1 << 6,
    /// <summary>The damage was critical.</summary>
    Critical = 1 << 7,
}
