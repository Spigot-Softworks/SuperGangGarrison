#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

internal static class HostSetupMainScreenLayout
{
    public static int GetPlaylistFileControlReserve(bool compactLayout)
    {
        return compactLayout ? 120 : 136;
    }

    public static Rectangle GetUsePlaylistFileToggleBounds(Rectangle rotationFileInputBounds, bool compactLayout)
    {
        var reserve = GetPlaylistFileControlReserve(compactLayout);
        return new Rectangle(
            rotationFileInputBounds.X - reserve,
            rotationFileInputBounds.Y,
            reserve - 8,
            rotationFileInputBounds.Height);
    }
}
