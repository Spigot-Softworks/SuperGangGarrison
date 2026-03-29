using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using OpenGarrison.Core;

internal static partial class ServerHelpers
{
    internal static string BuildLobbyServerName(string serverName, SimulationWorld world, IReadOnlyDictionary<byte, ClientSession> clients, bool passwordRequired, int maxPlayableClients)
    {
        var spectatorCount = clients.Keys.Count(IsSpectatorSlot);
        var playerCount = Math.Max(0, clients.Count - spectatorCount);
        var mapLabel = GetLobbyMapLabel(world);
        var display = $"[{mapLabel}] {serverName} [{playerCount}/{maxPlayableClients}]";
        return passwordRequired ? $"!private!{display}" : display;
    }

    internal static string GetLobbyMapLabel(SimulationWorld world)
    {
        var prefix = world.MatchRules.Mode switch
        {
            GameModeKind.ControlPoint => "cp",
            GameModeKind.Arena => "arena",
            GameModeKind.Generator => "gen",
            _ => "ctf",
        };

        return $"{prefix}_{world.Level.Name}".ToLowerInvariant();
    }

    internal static byte[] ParseProtocolUuid(string uuid)
    {
        var cleaned = uuid.Replace("-", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        if (cleaned.Length != 32)
        {
            return Array.Empty<byte>();
        }

        var bytes = new byte[16];
        for (var index = 0; index < 16; index += 1)
        {
            var slice = cleaned.Substring(index * 2, 2);
            if (!byte.TryParse(slice, NumberStyles.HexNumber, null, out var value))
            {
                return Array.Empty<byte>();
            }

            bytes[index] = value;
        }

        return bytes;
    }

    internal static List<string> LoadMapRotation(string? configuredRotationPath, IReadOnlyList<string>? stockMapRotation = null)
    {
        var rotationPath = ResolveMapRotationPath(configuredRotationPath);
        if (rotationPath is not null)
        {
            return File.ReadAllLines(rotationPath)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#'))
                .ToList();
        }

        return stockMapRotation?.Where(entry => !string.IsNullOrWhiteSpace(entry)).ToList()
            ?? new List<string>();
    }

    private static string? ResolveMapRotationPath(string? configuredRotationPath)
    {
        if (string.IsNullOrWhiteSpace(configuredRotationPath))
        {
            return null;
        }

        if (Path.IsPathRooted(configuredRotationPath))
        {
            return File.Exists(configuredRotationPath) ? configuredRotationPath : null;
        }

        var appCandidate = Path.Combine(RuntimePaths.ApplicationRoot, configuredRotationPath);
        if (File.Exists(appCandidate))
        {
            return appCandidate;
        }

        var currentDirectoryCandidate = Path.Combine(Directory.GetCurrentDirectory(), configuredRotationPath);
        if (File.Exists(currentDirectoryCandidate))
        {
            return currentDirectoryCandidate;
        }

        return ProjectSourceLocator.FindFile(configuredRotationPath);
    }

    internal static int EnsureMapRotationIndex(List<string> rotation, string requestedMap, string loadedMapName)
    {
        if (rotation.Count == 0)
        {
            rotation.Add(loadedMapName);
            return 0;
        }

        var index = FindMapRotationIndex(rotation, requestedMap);
        if (index >= 0)
        {
            return index;
        }

        rotation.Insert(0, requestedMap);
        return 0;
    }

    internal static int FindMapRotationIndex(IReadOnlyList<string> rotation, string mapName)
    {
        var normalized = NormalizeMapName(mapName);
        for (var index = 0; index < rotation.Count; index += 1)
        {
            if (string.Equals(NormalizeMapName(rotation[index]), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    internal static string NormalizeMapName(string mapName)
    {
        if (string.IsNullOrWhiteSpace(mapName))
        {
            return string.Empty;
        }

        var trimmed = mapName.Trim();
        var underscoreIndex = trimmed.IndexOf('_');
        if (underscoreIndex > 0)
        {
            var prefix = trimmed[..underscoreIndex];
            if (prefix.Equals("ctf", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("cp", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("arena", StringComparison.OrdinalIgnoreCase)
                || prefix.Equals("gen", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[(underscoreIndex + 1)..];
            }
        }

        return trimmed;
    }

    internal static bool TryApplyPendingMapChange(
        SimulationWorld world,
        List<string> mapRotation,
        ref int mapRotationIndex,
        Action<string> log)
    {
        if (!world.IsMapChangeReady)
        {
            return false;
        }

        var winner = world.MatchState.WinnerTeam;
        var currentArea = world.Level.MapAreaIndex;
        var totalAreas = world.Level.MapAreaCount;
        var preserveStats = false;
        string nextMap;
        var nextArea = 1;

        if (winner == PlayerTeam.Red && currentArea < totalAreas)
        {
            nextMap = world.Level.Name;
            nextArea = currentArea + 1;
            preserveStats = true;
            log($"[server] advancing to {nextMap} area {nextArea}/{totalAreas} (winner red)");
        }
        else if (mapRotation.Count > 0)
        {
            mapRotationIndex = (mapRotationIndex + 1) % mapRotation.Count;
            nextMap = mapRotation[mapRotationIndex];
            log($"[server] advancing to next map {nextMap} (winner {(winner.HasValue ? winner.Value.ToString() : "tie")})");
        }
        else
        {
            nextMap = world.Level.Name;
            log("[server] map rotation empty; restarting current map.");
        }

        if (!world.ApplyPendingMapChange(nextMap, nextArea, preserveStats))
        {
            log($"[server] failed to apply map change to {nextMap}; restarting round.");
            return false;
        }

        log($"[server] now running {world.Level.Name} area {world.Level.MapAreaIndex}/{world.Level.MapAreaCount}");
        return true;
    }
}
