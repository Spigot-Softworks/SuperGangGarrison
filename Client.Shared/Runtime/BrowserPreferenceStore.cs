using System;
using System.Collections.Generic;

namespace OpenGarrison.ClientShared;

/// <summary>Browser host loads supported documents before constructing the game.</summary>
public static class BrowserPreferenceStore
{
    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal);
    private static Action<string, string>? _save;
    public static bool IsAvailable { get; private set; }
    public static Func<string, System.Threading.Tasks.Task<bool>>? CopyText { get; set; }

    public static void Initialize(IReadOnlyDictionary<string, string> values, Action<string, string> save)
    {
        Values.Clear();
        foreach (var entry in values) Values[entry.Key] = entry.Value;
        _save = save;
        IsAvailable = true;
    }

    public static string? Read(string key) => Values.GetValueOrDefault(key);

    public static void Write(string key, string value)
    {
        if (Values.TryGetValue(key, out var previous) && previous == value) return;
        Values[key] = value;
        try { _save?.Invoke(key, value); }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Preferences still work for this page when storage is unavailable.
        }
    }
}
