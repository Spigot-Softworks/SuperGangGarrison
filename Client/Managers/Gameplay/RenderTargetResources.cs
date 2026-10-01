#nullable enable

using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

public sealed class RenderTargetResources
{
    public RenderTarget2D? GameRenderTarget { get; set; }

    public RenderTarget2D? HudRenderTarget { get; set; }

    public RenderTarget2D? DeathCamCaptureTarget { get; set; }

    public bool DeathCamCaptureValid { get; set; }
}
