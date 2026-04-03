using System;

namespace OpenGarrison.Client.Plugins;

[Flags]
public enum LocalDamageFlags : byte
{
    None = 0,
    Airshot = 1 << 0,
}
