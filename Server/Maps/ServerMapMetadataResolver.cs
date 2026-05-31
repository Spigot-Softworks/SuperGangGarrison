using OpenGarrison.Core;

namespace OpenGarrison.Server;

internal sealed class ServerMapMetadataResolver(
    SimulationWorld world,
    Func<CustomMapDescriptor, string>? mapDownloadUrlProvider = null)
{
    private static readonly char[] SchemePrefixSeparators = ['/', '?', '#'];

    private string _cachedMapMetadataLevelName = string.Empty;
    private bool _cachedIsCustomMap;
    private string _cachedMapDownloadUrl = string.Empty;
    private string _cachedMapContentHash = string.Empty;

    public (bool IsCustomMap, string MapDownloadUrl, string MapContentHash) GetCurrentMapMetadata()
    {
        var levelName = world.Level.Name;
        if (string.Equals(_cachedMapMetadataLevelName, levelName, StringComparison.OrdinalIgnoreCase))
        {
            return (_cachedIsCustomMap, _cachedMapDownloadUrl, _cachedMapContentHash);
        }

        _cachedMapMetadataLevelName = levelName;
        if (CustomMapDescriptorResolver.TryResolve(levelName, out var descriptor))
        {
            _cachedIsCustomMap = true;
            var localDownloadUrl = NormalizeAdvertisedDownloadUrl(mapDownloadUrlProvider?.Invoke(descriptor) ?? string.Empty);
            _cachedMapDownloadUrl = string.IsNullOrWhiteSpace(localDownloadUrl)
                ? NormalizeAdvertisedDownloadUrl(descriptor.SourceUrl)
                : localDownloadUrl;
            _cachedMapContentHash = descriptor.ContentHash;
        }
        else
        {
            _cachedIsCustomMap = false;
            _cachedMapDownloadUrl = string.Empty;
            _cachedMapContentHash = string.Empty;
        }

        return (_cachedIsCustomMap, _cachedMapDownloadUrl, _cachedMapContentHash);
    }

    private static string NormalizeAdvertisedDownloadUrl(string downloadUrl)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return string.Empty;
        }

        var trimmedUrl = downloadUrl.Trim();
        if (Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var absoluteUri))
        {
            return string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                ? absoluteUri.ToString()
                : string.Empty;
        }

        if (TryGetSchemeLikePrefix(trimmedUrl))
        {
            return string.Empty;
        }

        return trimmedUrl;
    }

    private static bool TryGetSchemeLikePrefix(string value)
    {
        var colonIndex = value.IndexOf(':');
        if (colonIndex <= 0)
        {
            return false;
        }

        var separatorIndex = value.IndexOfAny(SchemePrefixSeparators);
        return separatorIndex < 0 || separatorIndex > colonIndex;
    }
}
