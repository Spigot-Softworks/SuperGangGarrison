using System.Collections.Concurrent;

namespace OpenGarrison.Core;

// Local folders holding a mod pack's files (sprite frames). Plugin packs are not under the
// stock content roots, so whoever loads one records its folder here; a server serves the
// files from it and a client loads sprites from it. Registered under the active owner, so
// plugin shutdown or a disconnect removes the folder with the rest of the pack.
public sealed partial class GameplayRuntimeRegistry
{
    // Concurrent: the server's HTTP endpoint reads it off the simulation thread.
    private readonly ConcurrentDictionary<string, string> _packDirectories = new(StringComparer.Ordinal);

    public void RegisterPackDirectory(string modPackId, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modPackId);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var hadDirectory = _packDirectories.TryGetValue(modPackId, out var previousDirectory);
        _packDirectories[modPackId] = Path.GetFullPath(directory);
        ModPackRevision += 1;
        RecordRegistration(OwnedRegistrationKind.PackDirectory, modPackId, string.Empty, () =>
        {
            if (hadDirectory)
            {
                _packDirectories[modPackId] = previousDirectory!;
            }
            else
            {
                _packDirectories.TryRemove(modPackId, out _);
            }
        });
    }

    public bool TryGetPackDirectory(string? modPackId, out string directory)
    {
        if (!string.IsNullOrWhiteSpace(modPackId) && _packDirectories.TryGetValue(modPackId, out var found))
        {
            directory = found;
            return true;
        }

        directory = string.Empty;
        return false;
    }
}
