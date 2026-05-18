#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public sealed class FriendListDocument
{
    public const string DefaultFileName = "friends.json";

    public List<FriendListEntry> Friends { get; set; } = [];

    public static FriendListDocument Load(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return new FriendListDocument();
        }

        var resolvedPath = path ?? RuntimePaths.GetUserDataPath(DefaultFileName);
        if (!File.Exists(resolvedPath))
        {
            var created = new FriendListDocument();
            created.Save(resolvedPath);
            return created;
        }

        try
        {
            var document = JsonConfigurationFile.LoadOrCreate<FriendListDocument>(resolvedPath);
            document.Normalize();
            document.Save(resolvedPath);
            return document;
        }
        catch
        {
            return new FriendListDocument();
        }
    }

    public void Save(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var resolvedPath = path ?? RuntimePaths.GetUserDataPath(DefaultFileName);
        Normalize();
        JsonConfigurationFile.Save(resolvedPath, this);
    }

    public bool TryAdd(string friendCode, string displayName = "")
    {
        if (!ClientIdentityDocument.TryNormalizeFriendCode(friendCode, out var normalized))
        {
            return false;
        }

        if (Friends.Any(friend => string.Equals(friend.FriendCode, normalized, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        Friends.Add(new FriendListEntry
        {
            FriendCode = normalized,
            DisplayName = displayName.Trim(),
            AddedAtUtc = DateTimeOffset.UtcNow,
        });
        Normalize();
        return true;
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= Friends.Count)
        {
            return false;
        }

        Friends.RemoveAt(index);
        Normalize();
        return true;
    }

    private void Normalize()
    {
        var normalized = new List<FriendListEntry>(Friends.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var friend in Friends)
        {
            if (!ClientIdentityDocument.TryNormalizeFriendCode(friend.FriendCode, out var friendCode)
                || !seen.Add(friendCode))
            {
                continue;
            }

            normalized.Add(new FriendListEntry
            {
                FriendCode = friendCode,
                DisplayName = friend.DisplayName?.Trim() ?? string.Empty,
                AddedAtUtc = friend.AddedAtUtc == default ? DateTimeOffset.UtcNow : friend.AddedAtUtc,
            });
        }

        normalized.Sort(static (left, right) => string.Compare(left.DisplayLabel, right.DisplayLabel, StringComparison.OrdinalIgnoreCase));
        Friends = normalized;
    }
}

public sealed class FriendListEntry
{
    public string FriendCode { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public DateTimeOffset AddedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName) ? FriendCode : DisplayName.Trim();
}
