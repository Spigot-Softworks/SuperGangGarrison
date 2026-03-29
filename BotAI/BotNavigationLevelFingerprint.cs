using OpenGarrison.Core;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenGarrison.BotAI;

public static class BotNavigationLevelFingerprint
{
    public static string Compute(SimpleLevel level)
    {
        ArgumentNullException.ThrowIfNull(level);

        var builder = new StringBuilder(32_768);
        builder.AppendLine(level.Name);
        builder.AppendLine(level.Mode.ToString());
        builder.Append(level.MapAreaIndex).Append('|').Append(level.MapAreaCount).AppendLine();
        builder.AppendFormat(CultureInfo.InvariantCulture, "{0:F3}|{1:F3}", level.Bounds.Width, level.Bounds.Height).AppendLine();
        builder.AppendLine(level.BackgroundAssetName ?? string.Empty);
        builder.AppendFormat(CultureInfo.InvariantCulture, "{0:F3}", level.FloorY).AppendLine();
        AppendSpawns(builder, "local", [level.LocalSpawn]);
        AppendSpawns(builder, "red", level.RedSpawns);
        AppendSpawns(builder, "blue", level.BlueSpawns);
        AppendIntelBases(builder, level.IntelBases);
        AppendRoomObjects(builder, level.RoomObjects);
        AppendSolids(builder, level.Solids);
        AppendUnsupportedEntities(builder, level.UnsupportedSourceEntities);

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void AppendSpawns(StringBuilder builder, string label, IReadOnlyList<SpawnPoint> spawns)
    {
        builder.Append(label).Append(':').Append(spawns.Count).AppendLine();
        for (var index = 0; index < spawns.Count; index += 1)
        {
            builder
                .AppendFormat(CultureInfo.InvariantCulture, "{0:F3}|{1:F3}", spawns[index].X, spawns[index].Y)
                .AppendLine();
        }
    }

    private static void AppendIntelBases(StringBuilder builder, IReadOnlyList<IntelBaseMarker> intelBases)
    {
        builder.Append("intel:").Append(intelBases.Count).AppendLine();
        for (var index = 0; index < intelBases.Count; index += 1)
        {
            var marker = intelBases[index];
            builder
                .Append(marker.Team)
                .Append('|')
                .AppendFormat(CultureInfo.InvariantCulture, "{0:F3}|{1:F3}", marker.X, marker.Y)
                .AppendLine();
        }
    }

    private static void AppendRoomObjects(StringBuilder builder, IReadOnlyList<RoomObjectMarker> roomObjects)
    {
        builder.Append("room:").Append(roomObjects.Count).AppendLine();
        for (var index = 0; index < roomObjects.Count; index += 1)
        {
            var marker = roomObjects[index];
            builder
                .Append(marker.Type)
                .Append('|')
                .Append(marker.Team?.ToString() ?? "-")
                .Append('|')
                .Append(marker.SourceName)
                .Append('|')
                .Append(marker.SpriteName)
                .Append('|')
                .AppendFormat(CultureInfo.InvariantCulture, "{0:F3}|{1:F3}|{2:F3}|{3:F3}", marker.X, marker.Y, marker.Width, marker.Height)
                .AppendLine();
        }
    }

    private static void AppendSolids(StringBuilder builder, IReadOnlyList<LevelSolid> solids)
    {
        builder.Append("solid:").Append(solids.Count).AppendLine();
        for (var index = 0; index < solids.Count; index += 1)
        {
            var solid = solids[index];
            builder
                .AppendFormat(CultureInfo.InvariantCulture, "{0:F3}|{1:F3}|{2:F3}|{3:F3}", solid.X, solid.Y, solid.Width, solid.Height)
                .AppendLine();
        }
    }

    private static void AppendUnsupportedEntities(StringBuilder builder, IReadOnlyList<string> unsupportedEntities)
    {
        builder.Append("unsupported:").Append(unsupportedEntities.Count).AppendLine();
        for (var index = 0; index < unsupportedEntities.Count; index += 1)
        {
            builder.Append(unsupportedEntities[index]).AppendLine();
        }
    }
}
