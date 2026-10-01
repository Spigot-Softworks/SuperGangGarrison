#nullable enable

using System.Threading.Tasks;
using OpenGarrison.ClientShared;

namespace OpenGarrison.Client;

public sealed class BrowserBootstrapResources
{
    public Task<BrowserBootstrapAssetCatalog>? AssetsTask { get; set; }

    public BrowserBootstrapAssetCatalog? Assets { get; set; }

    public bool AssetsApplied { get; set; }

    public BrowserAtlasTextureCache? AtlasTextureCache { get; set; }

    public BrowserBootstrapAtlasTextureResolver? AtlasResolver { get; set; }
}
