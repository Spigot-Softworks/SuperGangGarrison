#nullable enable
using OpenGarrison.ClientShared;

namespace OpenGarrison.Client;

public partial class Game1
{
    public static bool IsRestrictedBrowserEdition => ClientDistribution.IsRestricted;
}
