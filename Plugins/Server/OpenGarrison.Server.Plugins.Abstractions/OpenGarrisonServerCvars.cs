namespace OpenGarrison.Server.Plugins;

/// <summary>
/// The value type of a server cvar.
/// </summary>
public enum OpenGarrisonServerCvarValueType
{
    /// <summary>A string value.</summary>
    String = 0,
    /// <summary>An integer value.</summary>
    Integer,
    /// <summary>A float value.</summary>
    Float,
    /// <summary>A boolean value.</summary>
    Boolean,
}

/// <summary>
/// Snapshot of a server cvar's definition and current value.
/// </summary>
/// <param name="Name">The cvar name.</param>
/// <param name="Description">The cvar description.</param>
/// <param name="ValueType">The cvar value type.</param>
/// <param name="DefaultValue">The default value.</param>
/// <param name="CurrentValue">The current value.</param>
/// <param name="IsProtected">Whether the value is protected (hidden unless requested).</param>
/// <param name="IsReadOnly">Whether the value is read-only.</param>
/// <param name="MinimumNumericValue">The minimum numeric value, if any.</param>
/// <param name="MaximumNumericValue">The maximum numeric value, if any.</param>
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

/// <summary>
/// Registry plugins use to read and mutate server cvars.
/// </summary>
public interface IOpenGarrisonServerCvarRegistry
{
    /// <summary>
    /// Gets all cvars, excluding protected values by default.
    /// </summary>
    /// <returns>The cvar snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll()
    {
        return GetAll(includeProtectedValues: false);
    }

    /// <summary>
    /// Gets all cvars.
    /// </summary>
    /// <param name="includeProtectedValues">Whether to include protected values.</param>
    /// <returns>The cvar snapshots.</returns>
    IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll(bool includeProtectedValues);

    /// <summary>
    /// Tries to get a cvar by name, excluding protected values by default.
    /// </summary>
    /// <param name="name">The cvar name.</param>
    /// <param name="cvar">The cvar snapshot.</param>
    /// <returns>True when the cvar exists.</returns>
    bool TryGet(string name, out OpenGarrisonServerCvarInfo cvar)
    {
        return TryGet(name, includeProtectedValues: false, out cvar);
    }

    /// <summary>
    /// Tries to get a cvar by name.
    /// </summary>
    /// <param name="name">The cvar name.</param>
    /// <param name="includeProtectedValues">Whether to include the protected value.</param>
    /// <param name="cvar">The cvar snapshot.</param>
    /// <returns>True when the cvar exists.</returns>
    bool TryGet(string name, bool includeProtectedValues, out OpenGarrisonServerCvarInfo cvar);

    /// <summary>
    /// Tries to set a cvar value, without allowing protected mutation by default.
    /// </summary>
    /// <param name="name">The cvar name.</param>
    /// <param name="value">The new value.</param>
    /// <param name="cvar">The updated cvar snapshot.</param>
    /// <param name="error">The error message when the set fails.</param>
    /// <returns>True when the value was set.</returns>
    bool TrySet(string name, string value, out OpenGarrisonServerCvarInfo cvar, out string error)
    {
        return TrySet(name, value, allowProtectedMutation: false, out cvar, out error);
    }

    /// <summary>
    /// Tries to set a cvar value.
    /// </summary>
    /// <param name="name">The cvar name.</param>
    /// <param name="value">The new value.</param>
    /// <param name="allowProtectedMutation">Whether protected cvars may be mutated.</param>
    /// <param name="cvar">The updated cvar snapshot.</param>
    /// <param name="error">The error message when the set fails.</param>
    /// <returns>True when the value was set.</returns>
    bool TrySet(string name, string value, bool allowProtectedMutation, out OpenGarrisonServerCvarInfo cvar, out string error);

    /// <summary>
    /// Tries to mark a cvar as protected (its value is hidden unless requested).
    /// </summary>
    /// <param name="name">The cvar name.</param>
    /// <param name="cvar">The updated cvar snapshot.</param>
    /// <param name="error">The error message when protection fails.</param>
    /// <returns>True when the cvar was protected.</returns>
    bool TryProtect(string name, out OpenGarrisonServerCvarInfo cvar, out string error);
}
