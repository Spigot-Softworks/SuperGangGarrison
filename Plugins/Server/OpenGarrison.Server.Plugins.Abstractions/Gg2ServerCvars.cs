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
    IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll()
    {
        return GetAll(includeProtectedValues: false);
    }

    IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll(bool includeProtectedValues);

    bool TryGet(string name, out OpenGarrisonServerCvarInfo cvar)
    {
        return TryGet(name, includeProtectedValue: false, out cvar);
    }

    bool TryGet(string name, bool includeProtectedValue, out OpenGarrisonServerCvarInfo cvar);

    bool TrySet(string name, string value, out OpenGarrisonServerCvarInfo cvar, out string error)
    {
        return TrySet(name, value, allowProtectedMutation: false, out cvar, out error);
    }

    bool TrySet(string name, string value, bool allowProtectedMutation, out OpenGarrisonServerCvarInfo cvar, out string error);

    bool TryProtect(string name, out OpenGarrisonServerCvarInfo cvar, out string error);
}
