using System;
using System.Text.Json;

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

        return normalized;
    }
}

internal sealed class ClientPluginStateDocument
{
    public Dictionary<string, bool> PluginEnabledStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
