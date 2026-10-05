#nullable enable

using System;
using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

public partial class Game1
{
    private GraphicsDevice? _textureUploadSyncDevice;

    /// <summary>
    /// Applies <see cref="DesktopGLTextureUploadSync"/> once per graphics device and logs
    /// the result. Cheap after the first successful call.
    /// </summary>
    private void EnsureTextureUploadSyncPolicy()
    {
        var device = GraphicsDevice;
        if (device is null || ReferenceEquals(device, _textureUploadSyncDevice))
        {
            return;
        }

        if (!DesktopGLTextureUploadSync.EnsureInstalled() && !OperatingSystem.IsBrowser())
        {
            // Entry points may not be loaded yet; try again next frame, but log failures
            // that will not resolve themselves.
            if (DesktopGLTextureUploadSync.Status.StartsWith("GL entry points", StringComparison.Ordinal))
            {
                return;
            }
        }

        _textureUploadSyncDevice = device;
        var status = DesktopGLTextureUploadSync.Status;
        AddConsoleLine($"gl texture uploads: {status}");
        LogClientPerformanceLine(
            $"event=client_gl_texture_upload_sync finish_skipped={(DesktopGLTextureUploadSync.FinishSkipped ? "true" : "false")} status=\"{status}\"");
    }
}
