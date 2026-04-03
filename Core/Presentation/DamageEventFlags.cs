using System;
using System.Diagnostics.CodeAnalysis;

namespace OpenGarrison.Core;

[Flags]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Flag enum intentionally mirrors the existing damage-event naming scheme.")]
public enum DamageEventFlags : byte
{
    None = 0,
    Airshot = 1 << 0,
}
