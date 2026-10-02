#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public sealed class RenderPipeline
{
    private readonly IRenderContext _context;
    private bool _preLaunchSplashDismissed;

    public RenderPipeline(IRenderContext context)
    {
        _context = context;
    }

    public void Draw(GameTime gameTime)
    {
        _context.LogBrowserDrawFrameState(gameTime);
        // Use interpolation clock value from Update() - don't re-sample during Draw()
        _context.ApplyFrameRateLimit();
        // Draw CPU duration excludes the optional frame-cap wait above.
        var browserDrawStartTimestamp = _context.ShouldMeasureClientPerformanceDurations()
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        _context.GraphicsDevice.Clear(new Color(24, 32, 48));
        _context.Gameplay.Frame.Draw(gameTime);

        _context.DrawBase(gameTime);
        _context.RecordBrowserDrawDuration(browserDrawStartTimestamp);

        if (!_preLaunchSplashDismissed)
        {
            _preLaunchSplashDismissed = true;
            PreLaunchSplash.Close();
        }
    }
}
