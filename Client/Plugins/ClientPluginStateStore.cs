using System;
using System.Text.Json;
using Microsoft.Xna.Framework.Input;

namespace OpenGarrison.Client;

internal sealed class ClientPluginStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly Action<string> _log;
    private ClientPluginStateDocument _document;

    public ClientPluginStateStore(string path, Action<string> log)
    {
        _path = path;
        _log = log;
        _document = LoadDocument(path, log);
    }

    public bool IsPluginEnabled(string pluginId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        if (_document.PluginEnabledStates.TryGetValue(pluginId, out var enabled))
        {
            return enabled;
        }

        return !string.Equals(pluginId, "randombackgrounds", StringComparison.OrdinalIgnoreCase);
    }

    public void SetPluginEnabled(string pluginId, bool enabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);

        if (_document.PluginEnabledStates.TryGetValue(pluginId, out var currentValue)
            && currentValue == enabled)
        {
            return;
        }

        _document.PluginEnabledStates[pluginId] = enabled;
        SaveDocument();
    }

    public Keys GetPluginHotkey(string pluginId, string hotkeyId, Keys defaultKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hotkeyId);

        if (_document.PluginHotkeyStates.TryGetValue(pluginId, out var pluginHotkeys)
            && pluginHotkeys.TryGetValue(hotkeyId, out var serializedKey)
            && Enum.TryParse<Keys>(serializedKey, ignoreCase: true, out var parsedKey))
        {
            return parsedKey;
        }

        return defaultKey;
    }

    public void SetPluginHotkey(string pluginId, string hotkeyId, Keys key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginId);
        ArgumentException.ThrowIfNullOrWhiteSpace(hotkeyId);

        if (!_document.PluginHotkeyStates.TryGetValue(pluginId, out var pluginHotkeys))
        {
            pluginHotkeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _document.PluginHotkeyStates[pluginId] = pluginHotkeys;
        }

        var serializedKey = key.ToString();
        if (pluginHotkeys.TryGetValue(hotkeyId, out var currentValue)
            && string.Equals(currentValue, serializedKey, StringComparison.Ordinal))
        {
            return;
        }

        pluginHotkeys[hotkeyId] = serializedKey;
        SaveDocument();
    }

    private void SaveDocument()
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var normalized = new ClientPluginStateDocument();
            foreach (var entry in _document.PluginEnabledStates
                         .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                normalized.PluginEnabledStates[entry.Key] = entry.Value;
            }

            foreach (var pluginEntry in _document.PluginHotkeyStates
                         .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                var normalizedHotkeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var hotkeyEntry in pluginEntry.Value.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
                {
                    normalizedHotkeys[hotkeyEntry.Key] = hotkeyEntry.Value;
                }

                normalized.PluginHotkeyStates[pluginEntry.Key] = normalizedHotkeys;
            }

            File.WriteAllText(_path, JsonSerializer.Serialize(normalized, SerializerOptions));
            _document = normalized;
        }
        catch (Exception ex)
        {
            _log($"[plugin] failed to save client plugin state: {ex.Message}");
        }
    }

    private static ClientPluginStateDocument LoadDocument(string path, Action<string> log)
    {
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<ClientPluginStateDocument>(File.ReadAllText(path));
                if (loaded is not null)
                {
                    return Normalize(loaded);
                }
            }
        }
        catch (Exception ex)
        {
            log($"[plugin] failed to load client plugin state: {ex.Message}");
        }

        return new ClientPluginStateDocument();
    }

    private static ClientPluginStateDocument Normalize(ClientPluginStateDocument document)
    {
        var normalized = new ClientPluginStateDocument();
        foreach (var entry in document.PluginEnabledStates)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
            {
                continue;
            }

            normalized.PluginEnabledStates[entry.Key] = entry.Value;
        }

        foreach (var pluginEntry in document.PluginHotkeyStates)
        {
            if (string.IsNullOrWhiteSpace(pluginEntry.Key))
            {
                continue;
            }

            var normalizedHotkeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var hotkeyEntry in pluginEntry.Value)
            {
                if (string.IsNullOrWhiteSpace(hotkeyEntry.Key) || string.IsNullOrWhiteSpace(hotkeyEntry.Value))
                {
                    continue;
                }

                normalizedHotkeys[hotkeyEntry.Key] = hotkeyEntry.Value;
            }

            if (normalizedHotkeys.Count > 0)
            {
                normalized.PluginHotkeyStates[pluginEntry.Key] = normalizedHotkeys;
            }
        }

        return normalized;
    }
}

internal sealed class ClientPluginStateDocument
{
    public Dictionary<string, bool> PluginEnabledStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public Dictionary<string, Dictionary<string, string>> PluginHotkeyStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
