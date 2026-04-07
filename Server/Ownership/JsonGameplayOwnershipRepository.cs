using System.Text.Json;

namespace OpenGarrison.Server;

internal sealed class JsonGameplayOwnershipRepository(string path) : IGameplayOwnershipRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly object _gate = new();

    public IReadOnlyList<string> LoadOwnedItemIds(GameplayOwnershipIdentity identity)
    {
        lock (_gate)
        {
            var document = LoadDocument();
            var record = document.Players.FirstOrDefault(player =>
                string.Equals(player.IdentityKey, identity.Key, StringComparison.Ordinal));
            return record?.OwnedItemIds
                ?.Where(static itemId => !string.IsNullOrWhiteSpace(itemId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static itemId => itemId, StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<string>();
        }
    }

    public void SaveOwnedItemIds(GameplayOwnershipIdentity identity, IReadOnlyCollection<string> itemIds)
    {
        lock (_gate)
        {
            var document = LoadDocument();
            var normalizedItemIds = itemIds
                .Where(static itemId => !string.IsNullOrWhiteSpace(itemId))
                .Select(static itemId => itemId.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static itemId => itemId, StringComparer.Ordinal)
                .ToArray();

            var existingIndex = document.Players.FindIndex(player =>
                string.Equals(player.IdentityKey, identity.Key, StringComparison.Ordinal));
            if (normalizedItemIds.Length == 0)
            {
                if (existingIndex >= 0)
                {
                    document.Players.RemoveAt(existingIndex);
                    SaveDocument(document);
                }

                return;
            }

            var record = new GameplayOwnershipStorePlayerRecord
            {
                IdentityKey = identity.Key,
                DisplayName = identity.DisplayName,
                BadgeMask = identity.BadgeMask,
                LastUpdatedUtc = DateTimeOffset.UtcNow,
                OwnedItemIds = normalizedItemIds,
            };

            if (existingIndex >= 0)
            {
                document.Players[existingIndex] = record;
            }
            else
            {
                document.Players.Add(record);
            }

            document.Players.Sort(static (left, right) => string.Compare(left.IdentityKey, right.IdentityKey, StringComparison.Ordinal));
            SaveDocument(document);
        }
    }

    private GameplayOwnershipStoreDocument LoadDocument()
    {
        if (!File.Exists(path))
        {
            return new GameplayOwnershipStoreDocument();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GameplayOwnershipStoreDocument>(json, JsonOptions) ?? new GameplayOwnershipStoreDocument();
        }
        catch (JsonException)
        {
            return new GameplayOwnershipStoreDocument();
        }
        catch (IOException)
        {
            return new GameplayOwnershipStoreDocument();
        }
    }

    private void SaveDocument(GameplayOwnershipStoreDocument document)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(path, json);
    }

    private sealed class GameplayOwnershipStoreDocument
    {
        public int SchemaVersion { get; set; } = 1;

        public List<GameplayOwnershipStorePlayerRecord> Players { get; set; } = [];
    }

    private sealed class GameplayOwnershipStorePlayerRecord
    {
        public string IdentityKey { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public ulong BadgeMask { get; set; }

        public DateTimeOffset LastUpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

        public string[] OwnedItemIds { get; set; } = [];
    }
}
