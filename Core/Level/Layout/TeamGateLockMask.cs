namespace OpenGarrison.Core;

[Flags]
public enum TeamGateLockMask : byte
{
    None = 0,
    Red = 1 << 0,
    Blue = 1 << 1,
}
