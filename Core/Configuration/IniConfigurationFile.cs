using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace OpenGarrison.Core;

public sealed class IniConfigurationFile
{
    private readonly Dictionary<string, Dictionary<string, string>> _sections = new(StringComparer.OrdinalIgnoreCase);

    public static IniConfigurationFile Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var document = new IniConfigurationFile();
        if (!File.Exists(path))
        {
            return document;
        }

        var currentSection = string.Empty;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith(';') || line.StartsWith('#'))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']') && line.Length > 2)
            {
                currentSection = line[1..^1].Trim();
                document.GetOrCreateSection(currentSection);
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (key.Length == 0)
            {
                continue;
            }

            document.GetOrCreateSection(currentSection)[key] = value;
        }

        return document;
    }

    public string GetString(string section, string key, string fallback = "")
    {
        return TryGetValue(section, key, out var value)
            ? value
            : fallback;
    }

    public bool ContainsKey(string section, string key)
    {
        return TryGetValue(section, key, out _);
    }

    public int GetInt(string section, string key, int fallback = 0)
    {
        return TryGetValue(section, key, out var value)
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : fallback;
    }

    public bool GetBool(string section, string key, bool fallback = false)
    {
        if (!TryGetValue(section, key, out var value))
        {
            return fallback;
        }

        if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return bool.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    public void SetString(string section, string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        GetOrCreateSection(section)[key] = value ?? string.Empty;
    }

    public void SetInt(string section, string key, int value)
    {
        SetString(section, key, value.ToString(CultureInfo.InvariantCulture));
    }

    public void SetBool(string section, string key, bool value)
    {
        SetString(section, key, value ? "1" : "0");
    }

    public void Save(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var writer = new StreamWriter(path);
        var wroteAnySection = false;
        foreach (var (sectionName, entries) in _sections)
        {
            if (wroteAnySection)
            {
                writer.WriteLine();
            }

            if (!string.IsNullOrWhiteSpace(sectionName))
            {
                writer.WriteLine($"[{sectionName}]");
            }

            foreach (var (key, value) in entries)
            {
                writer.WriteLine($"{key}={value}");
            }

            wroteAnySection = true;
        }
    }

    private bool TryGetValue(string section, string key, out string value)
    {
        value = string.Empty;
        if (!_sections.TryGetValue(section ?? string.Empty, out var entries)
            || !entries.TryGetValue(key, out var foundValue))
        {
            return false;
        }

        value = foundValue ?? string.Empty;
        return true;
    }

    private Dictionary<string, string> GetOrCreateSection(string? section)
    {
        var sectionName = section ?? string.Empty;
        if (_sections.TryGetValue(sectionName, out var entries))
        {
            return entries;
        }

        entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        _sections[sectionName] = entries;
        return entries;
    }
}
