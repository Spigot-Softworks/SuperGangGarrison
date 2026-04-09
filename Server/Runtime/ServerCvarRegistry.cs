using System.Globalization;
using OpenGarrison.Server.Plugins;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal sealed class ServerCvarRegistry : IOpenGarrisonServerCvarRegistry
{
    private const string ProtectedValueMask = "<protected>";
    private readonly Dictionary<string, CvarRegistration> _cvarsByName = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _runtimeProtectedNames = new(StringComparer.OrdinalIgnoreCase);
    private string? _runtimeProtectionPath;

    public void EnableRuntimeProtectionPersistence(string path)
    {
        _runtimeProtectionPath = path;
        var document = JsonConfigurationFile.LoadOrCreate(path, static () => new CvarProtectionDocument());
        _runtimeProtectedNames.Clear();
        for (var index = 0; index < document.ProtectedNames.Count; index += 1)
        {
            if (TryGetRegistration(document.ProtectedNames[index], out var registration)
                && !registration.IsProtected)
            {
                _runtimeProtectedNames.Add(registration.Name);
            }
        }

        PersistRuntimeProtectionOverrides();
    }

    public IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll(bool includeProtectedValues)
    {
        return _cvarsByName.Values
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(entry => ToInfo(entry, includeProtectedValues))
            .ToArray();
    }

    public IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll()
    {
        return GetAll(includeProtectedValues: false);
    }

    public bool TryGet(string name, bool includeProtectedValue, out OpenGarrisonServerCvarInfo cvar)
    {
        cvar = default;
        if (!TryGetRegistration(name, out var registration))
        {
            return false;
        }

        cvar = ToInfo(registration, includeProtectedValue);
        return true;
    }

    public bool TryGet(string name, out OpenGarrisonServerCvarInfo cvar)
    {
        return TryGet(name, includeProtectedValue: false, out cvar);
    }

    public bool TrySet(string name, string value, bool allowProtectedMutation, out OpenGarrisonServerCvarInfo cvar, out string error)
    {
        cvar = default;
        error = string.Empty;
        if (!TryGetRegistration(name, out var registration))
        {
            error = "Unknown cvar.";
            return false;
        }

        if (registration.IsReadOnly)
        {
            error = "Cvar is read-only.";
            cvar = ToInfo(registration, includeProtectedValue: false);
            return false;
        }

        if (IsProtected(registration.Name) && !allowProtectedMutation)
        {
            error = "Cvar is protected.";
            cvar = ToInfo(registration, includeProtectedValue: false);
            return false;
        }

        if (!registration.TrySet(value?.Trim() ?? string.Empty, out error))
        {
            cvar = ToInfo(registration, includeProtectedValue: false);
            return false;
        }

        cvar = ToInfo(registration, allowProtectedMutation);
        return true;
    }

    public bool TrySet(string name, string value, out OpenGarrisonServerCvarInfo cvar, out string error)
    {
        return TrySet(name, value, allowProtectedMutation: false, out cvar, out error);
    }

    public bool TryProtect(string name, out OpenGarrisonServerCvarInfo cvar, out string error)
    {
        cvar = default;
        error = string.Empty;
        if (!TryGetRegistration(name, out var registration))
        {
            error = "Unknown cvar.";
            return false;
        }

        if (!registration.IsProtected)
        {
            _runtimeProtectedNames.Add(registration.Name);
            PersistRuntimeProtectionOverrides();
        }

        cvar = ToInfo(registration, includeProtectedValue: false);
        return true;
    }

    public void RegisterString(
        string name,
        string description,
        string defaultValue,
        Func<string> getter,
        Func<string, string?>? setter = null,
        bool isProtected = false)
    {
        Register(new CvarRegistration(
            name,
            description,
            OpenGarrisonServerCvarValueType.String,
            defaultValue,
            getter,
            setter is null
                ? static _ => "Cvar is read-only."
                : value => setter(value),
            isProtected,
            setter is null,
            MinimumNumericValue: null,
            MaximumNumericValue: null));
    }

    public void RegisterInteger(
        string name,
        string description,
        int defaultValue,
        Func<int> getter,
        Action<int>? setter = null,
        int? minValue = null,
        int? maxValue = null)
    {
        Register(new CvarRegistration(
            name,
            description,
            OpenGarrisonServerCvarValueType.Integer,
            defaultValue.ToString(CultureInfo.InvariantCulture),
            () => getter().ToString(CultureInfo.InvariantCulture),
            setter is null
                ? static _ => "Cvar is read-only."
                : value => TryParseInteger(value, minValue, maxValue, setter),
            IsProtected: false,
            IsReadOnly: setter is null,
            minValue,
            maxValue));
    }

    public void RegisterBoolean(
        string name,
        string description,
        bool defaultValue,
        Func<bool> getter,
        Action<bool>? setter = null)
    {
        Register(new CvarRegistration(
            name,
            description,
            OpenGarrisonServerCvarValueType.Boolean,
            defaultValue ? "true" : "false",
            () => getter() ? "true" : "false",
            setter is null
                ? static _ => "Cvar is read-only."
                : value => TryParseBoolean(value, setter),
            IsProtected: false,
            IsReadOnly: setter is null,
            MinimumNumericValue: null,
            MaximumNumericValue: null));
    }

    public void RegisterFloat(
        string name,
        string description,
        float defaultValue,
        Func<float> getter,
        Action<float>? setter = null,
        float? minValue = null,
        float? maxValue = null)
    {
        Register(new CvarRegistration(
            name,
            description,
            OpenGarrisonServerCvarValueType.Float,
            defaultValue.ToString("G9", CultureInfo.InvariantCulture),
            () => getter().ToString("G9", CultureInfo.InvariantCulture),
            setter is null
                ? static _ => "Cvar is read-only."
                : value => TryParseFloat(value, minValue, maxValue, setter),
            IsProtected: false,
            IsReadOnly: setter is null,
            minValue,
            maxValue));
    }

    private static string? TryParseInteger(string text, int? minValue, int? maxValue, Action<int> setter)
    {
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return "Value must be an integer.";
        }

        if (minValue.HasValue && parsedValue < minValue.Value)
        {
            return $"Value must be at least {minValue.Value}.";
        }

        if (maxValue.HasValue && parsedValue > maxValue.Value)
        {
            return $"Value must be at most {maxValue.Value}.";
        }

        setter(parsedValue);
        return null;
    }

    private static string? TryParseBoolean(string text, Action<bool> setter)
    {
        if (!TryNormalizeBoolean(text, out var parsedValue))
        {
            return "Value must be one of: true, false, on, off, yes, no, 1, 0.";
        }

        setter(parsedValue);
        return null;
    }

    private static string? TryParseFloat(string text, float? minValue, float? maxValue, Action<float> setter)
    {
        if (!float.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsedValue))
        {
            return "Value must be a number.";
        }

        if (minValue.HasValue && parsedValue < minValue.Value)
        {
            return $"Value must be at least {minValue.Value.ToString("G9", CultureInfo.InvariantCulture)}.";
        }

        if (maxValue.HasValue && parsedValue > maxValue.Value)
        {
            return $"Value must be at most {maxValue.Value.ToString("G9", CultureInfo.InvariantCulture)}.";
        }

        setter(parsedValue);
        return null;
    }

    private static bool TryNormalizeBoolean(string text, out bool value)
    {
        switch (text.Trim().ToLowerInvariant())
        {
            case "1":
            case "true":
            case "on":
            case "yes":
                value = true;
                return true;
            case "0":
            case "false":
            case "off":
            case "no":
                value = false;
                return true;
            default:
                value = false;
                return false;
        }
    }

    private void Register(CvarRegistration registration)
    {
        if (!TryNormalizeName(registration.Name, out var normalizedName))
        {
            throw new InvalidOperationException($"Invalid cvar name \"{registration.Name}\".");
        }

        registration = registration with { Name = normalizedName };
        if (_cvarsByName.ContainsKey(registration.Name))
        {
            throw new InvalidOperationException($"Cvar \"{registration.Name}\" is already registered.");
        }

        _cvarsByName[registration.Name] = registration;
    }

    private bool TryGetRegistration(string name, out CvarRegistration registration)
    {
        registration = default!;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return _cvarsByName.TryGetValue(name.Trim(), out registration!);
    }

    private static bool TryNormalizeName(string name, out string normalizedName)
    {
        normalizedName = name?.Trim() ?? string.Empty;
        if (normalizedName.Length == 0 || normalizedName.Length > 64)
        {
            return false;
        }

        for (var index = 0; index < normalizedName.Length; index += 1)
        {
            var character = normalizedName[index];
            if ((character >= 'a' && character <= 'z')
                || (character >= 'A' && character <= 'Z')
                || (character >= '0' && character <= '9')
                || character == '_'
                || character == '.')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool IsProtected(string name)
    {
        return _runtimeProtectedNames.Contains(name)
            || (_cvarsByName.TryGetValue(name, out var registration) && registration.IsProtected);
    }

    private void PersistRuntimeProtectionOverrides()
    {
        if (string.IsNullOrWhiteSpace(_runtimeProtectionPath))
        {
            return;
        }

        JsonConfigurationFile.Save(
            _runtimeProtectionPath,
            new CvarProtectionDocument
            {
                ProtectedNames = _runtimeProtectedNames
                    .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList()
            });
    }

    private OpenGarrisonServerCvarInfo ToInfo(CvarRegistration registration, bool includeProtectedValue)
    {
        var isProtected = IsProtected(registration.Name);
        return new OpenGarrisonServerCvarInfo(
            registration.Name,
            registration.Description,
            registration.ValueType,
            registration.DefaultValue,
            isProtected && !includeProtectedValue ? ProtectedValueMask : registration.GetCurrentValue(),
            isProtected,
            registration.IsReadOnly,
            registration.MinimumNumericValue,
            registration.MaximumNumericValue);
    }

    private sealed class CvarProtectionDocument
    {
        public int SchemaVersion { get; set; } = 1;

        public List<string> ProtectedNames { get; set; } = [];
    }

    private sealed record CvarRegistration(
        string Name,
        string Description,
        OpenGarrisonServerCvarValueType ValueType,
        string DefaultValue,
        Func<string> GetCurrentValue,
        Func<string, string?> TrySetValue,
        bool IsProtected,
        bool IsReadOnly,
        double? MinimumNumericValue,
        double? MaximumNumericValue)
    {
        public bool TrySet(string value, out string error)
        {
            error = TrySetValue(value) ?? string.Empty;
            return error.Length == 0;
        }
    }
}
