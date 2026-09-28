#nullable enable

using Microsoft.Xna.Framework;

namespace OpenGarrison.Client;

public sealed class RenderPipeline
{
    private readonly IRenderContext _context;

    public RenderPipeline(IRenderContext context)
    {
        _context = context;
    }

    public void Draw(GameTime gameTime)
    {
        var browserDrawStartTimestamp = _context.ShouldMeasureClientPerformanceDurations()
            ? System.Diagnostics.Stopwatch.GetTimestamp()
            : 0L;
        _context.LogBrowserDrawFrameState(gameTime);
        // Use interpolation clock value from Update() - don't re-sample during Draw()
        _context.ApplyFrameRateLimit();
        _context.GraphicsDevice.Clear(new Color(24, 32, 48));
        _context.Gameplay.Frame.Draw(gameTime);

        _context.DrawBase(gameTime);
        _context.RecordBrowserDrawDuration(browserDrawStartTimestamp);

        if (!_context._preLaunchSplashDismissed)
        {
            _context._preLaunchSplashDismissed = true;
            PreLaunchSplash.Close();
        }
    }
}
