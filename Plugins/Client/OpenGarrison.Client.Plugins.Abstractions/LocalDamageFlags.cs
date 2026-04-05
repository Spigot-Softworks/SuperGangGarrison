using System.Diagnostics.CodeAnalysis;
using System;

namespace OpenGarrison.Client.Plugins;

[Flags]
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Public plugin API enum; the Flags suffix is intentional and descriptive.")]
public enum LocalDamageFlags : byte
{
    None = 0,
    Airshot = 1 << 0,
}
