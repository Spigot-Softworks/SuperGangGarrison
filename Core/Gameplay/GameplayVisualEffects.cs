namespace OpenGarrison.Core;

// Plugin visual effects ride the ordinary visual event stream, so they need no protocol of
// their own. A sprite effect plays a pack sprite once at a world position; a screen effect
// covers the view for a while. Both are told apart from stock effects by a name prefix.
public static class GameplayVisualEffects
{
    public const string SpritePrefix = "sprite:";
    public const string ScreenPrefix = "screen:";

    // Screen effects the client knows how to draw.
    public const string TimeStop = "time_stop";

    public const int MaxTicksPerFrame = 30;
    public const int MaxScreenEffectTicks = 30 * 30;

    // Visual event names travel as strings of at most 64 UTF-8 bytes.
    public const int MaxEffectNameBytes = 64;

    private const int TicksPerFrameMask = 0xFF;
    private const int FlipXFlag = 0x100;

    public static bool IsKnownScreenEffect(string effect)
        => string.Equals(effect, TimeStop, StringComparison.Ordinal);

    public static bool IsScreenEffectName(string effectName)
        => effectName.StartsWith(ScreenPrefix, StringComparison.Ordinal);

    // A sprite effect's playback settings, packed into the visual event's Count.
    public static int EncodeSpriteCount(int ticksPerFrame, bool flipX)
        => Math.Clamp(ticksPerFrame, 1, MaxTicksPerFrame) | (flipX ? FlipXFlag : 0);

    public static (int TicksPerFrame, bool FlipX) DecodeSpriteCount(int count)
        => (Math.Clamp(count & TicksPerFrameMask, 1, MaxTicksPerFrame), (count & FlipXFlag) != 0);
}
