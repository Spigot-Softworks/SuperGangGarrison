using System.Globalization;
using OpenGarrison.Server.Plugins;

namespace OpenGarrison.Server;

internal sealed class ServerCvarRegistry : IOpenGarrisonServerCvarRegistry
{
    private const string ProtectedValueMask = "<protected>";
    private readonly Dictionary<string, CvarRegistration> _cvarsByName = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<OpenGarrisonServerCvarInfo> GetAll()
    {
        return _cvarsByName.Values
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Select(ToInfo)
            .ToArray();
    }

    public bool TryGet(string name, out OpenGarrisonServerCvarInfo cvar)
    {
        cvar = default;
        if (!TryGetRegistration(name, out var registration))
        {
            return false;
        }

        cvar = ToInfo(registration);
        return true;
    }

    public bool TrySet(string name, string value, out OpenGarrisonServerCvarInfo cvar, out string error)
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
            cvar = ToInfo(registration);
            return false;
        }

        if (!registration.TrySet(value?.Trim() ?? string.Empty, out error))
        {
            cvar = ToInfo(registration);
            return false;
        }

        cvar = ToInfo(registration);
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

    private static OpenGarrisonServerCvarInfo ToInfo(CvarRegistration registration)
    {
        return new OpenGarrisonServerCvarInfo(
            registration.Name,
            registration.Description,
            registration.ValueType,
            registration.DefaultValue,
            registration.IsProtected ? ProtectedValueMask : registration.GetCurrentValue(),
            registration.IsProtected,
            registration.IsReadOnly,
            registration.MinimumNumericValue,
            registration.MaximumNumericValue);
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
