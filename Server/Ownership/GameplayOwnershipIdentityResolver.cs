using System.Security.Cryptography;
using System.Text;
using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal sealed class GameplayOwnershipIdentityResolver(PersistentGameplayOwnershipIdentityMode mode)
{
    public PersistentGameplayOwnershipIdentityMode Mode => mode;

    public bool PersistenceEnabled => mode != PersistentGameplayOwnershipIdentityMode.Disabled;

    public bool TryResolve(string? playerName, ulong badgeMask, out GameplayOwnershipIdentity identity)
    {
        if (mode == PersistentGameplayOwnershipIdentityMode.Disabled)
        {
            identity = default;
            return false;
        }

        var normalizedName = NormalizePlayerName(playerName);
        if (normalizedName.Length == 0)
        {
            identity = default;
            return false;
        }

        var source = $"{normalizedName}|{badgeMask}";
        var key = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        identity = new GameplayOwnershipIdentity(
            $"name-badge:{key}",
            normalizedName,
            badgeMask);
        return true;
    }

    private static string NormalizePlayerName(string? playerName)
    {
        return string.IsNullOrWhiteSpace(playerName)
            ? string.Empty
            : playerName.Trim().ToLowerInvariant();
    }
}
