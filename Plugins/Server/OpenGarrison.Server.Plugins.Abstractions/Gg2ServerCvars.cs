namespace OpenGarrison.Server.Plugins;

public enum OpenGarrisonServerCvarValueType
{
    String = 0,
    Integer,
    Float,
    Boolean,
}

public readonly record struct OpenGarrisonServerCvarInfo(
    string Name,
    string Description,
    OpenGarrisonServerCvarValueType ValueType,
    string DefaultValue,
    string CurrentValue,
    bool IsProtected,
    bool IsReadOnly,
    double? MinimumNumericValue = null,
    double? MaximumNumericValue = null);

public interface IOpenGarrisonServerCvarRegistry
{
    IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll();

    bool TryGet(string name, out OpenGarrisonServerCvarInfo cvar);

    bool TrySet(string name, string value, out OpenGarrisonServerCvarInfo cvar, out string error);
}
