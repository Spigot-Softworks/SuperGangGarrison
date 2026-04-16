using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace OpenGarrison.Core;

public sealed class PracticeBotDisplayNamePool
{
    public const string DefaultNamesRelativePath = "Client/practice-bot-names.txt";

    private readonly Dictionary<byte, string> _displayNamesBySlot = new();
    private readonly HashSet<string> _usedDisplayNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _availableDisplayNames = new();
    private readonly List<string> _sourceDisplayNames;

    public PracticeBotDisplayNamePool()
        : this(LoadDefaultNames(), shuffleNames: true)
    {
    }

    public PracticeBotDisplayNamePool(IEnumerable<string> displayNames, bool shuffleNames = true)
    {
        ArgumentNullException.ThrowIfNull(displayNames);

        _sourceDisplayNames = NormalizeDisplayNames(displayNames);
        Reset(shuffleNames);
    }

    public static List<string> LoadDefaultNames()
    {
        var path = ResolveDefaultNamesPath();
        return string.IsNullOrWhiteSpace(path)
            ? []
            : LoadNamesFromFile(path);
    }

    public static string? ResolveDefaultNamesPath()
    {
        var projectFilePath = ProjectSourceLocator.FindFile(DefaultNamesRelativePath);
        if (!string.IsNullOrWhiteSpace(projectFilePath))
        {
            return projectFilePath;
        }

        var configPath = RuntimePaths.GetConfigPath("practice-bot-names.txt");
        return File.Exists(configPath) ? configPath : null;
    }

    public string GetOrAssign(byte slot, PlayerTeam team, int teamBotNumber)
    {
        if (_displayNamesBySlot.TryGetValue(slot, out var existing))
        {
            return existing;
        }

        while (_availableDisplayNames.Count > 0)
        {
            var candidate = _availableDisplayNames.Dequeue();
            if (!_usedDisplayNames.Add(candidate))
            {
                continue;
            }

            _displayNamesBySlot[slot] = candidate;
            return candidate;
        }

        var teamLabel = team == PlayerTeam.Blue ? "BLU" : "RED";
        var fallbackNumber = Math.Max(1, teamBotNumber);
        while (true)
        {
            var fallback = $"{teamLabel} Bot {fallbackNumber}";
            fallbackNumber += 1;
            if (!_usedDisplayNames.Add(fallback))
            {
                continue;
            }

            _displayNamesBySlot[slot] = fallback;
            return fallback;
        }
    }

    public void Reserve(string displayName)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            _usedDisplayNames.Add(displayName.Trim());
        }
    }

    public void ReleaseSlot(byte slot)
    {
        _displayNamesBySlot.Remove(slot);
    }

    public void Reset()
    {
        Reset(shuffleNames: true);
    }

    private void Reset(bool shuffleNames)
    {
        _displayNamesBySlot.Clear();
        _usedDisplayNames.Clear();
        _availableDisplayNames.Clear();

        var availableNames = new List<string>(_sourceDisplayNames);
        if (shuffleNames)
        {
            ShuffleNames(availableNames);
        }

        for (var index = 0; index < availableNames.Count; index += 1)
        {
            _availableDisplayNames.Enqueue(availableNames[index]);
        }
    }

    private static List<string> LoadNamesFromFile(string path)
    {
        var names = new List<string>();
        if (!File.Exists(path))
        {
            return names;
        }

        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (!seenNames.Add(trimmed))
            {
                continue;
            }

            names.Add(trimmed);
        }

        return names;
    }

    private static List<string> NormalizeDisplayNames(IEnumerable<string> displayNames)
    {
        var names = new List<string>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var displayName in displayNames)
        {
            var trimmed = displayName.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (!seenNames.Add(trimmed))
            {
                continue;
            }

            names.Add(trimmed);
        }

        return names;
    }

    private static void ShuffleNames(List<string> names)
    {
        for (var index = names.Count - 1; index > 0; index -= 1)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(index + 1);
            (names[index], names[swapIndex]) = (names[swapIndex], names[index]);
        }
    }
}
