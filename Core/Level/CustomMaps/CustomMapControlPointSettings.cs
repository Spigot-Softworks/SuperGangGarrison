namespace OpenGarrison.Core;

public sealed record CustomMapControlPointSettings(bool OverrideInitialOwnership)
{
    public static CustomMapControlPointSettings Default { get; } = new(false);
}
