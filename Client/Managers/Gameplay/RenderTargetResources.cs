#nullable enable

using Microsoft.Xna.Framework.Graphics;

namespace OpenGarrison.Client;

public sealed class RenderTargetResources
{
    public RenderTarget2D? GameRenderTarget { get; set; }

    public RenderTarget2D? HudRenderTarget { get; set; }

    /// <summary>Presentation-resolution target for the sub-pixel gameplay world pass.</summary>
    public RenderTarget2D? WorldPresentationTarget { get; set; }

    public RenderTarget2D? DeathCamCaptureTarget { get; set; }

    public bool DeathCamCaptureValid { get; set; }
}
