#nullable enable
using OpenGarrison.ClientShared;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static bool IsRestrictedBrowserEdition => ClientDistribution.IsRestricted;
}
