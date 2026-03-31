using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client.Plugins.BubbleWheel;

public sealed class BubbleWheelPlugin :
    IOpenGarrisonClientPlugin,
    IOpenGarrisonClientLifecycleHooks,
    IOpenGarrisonClientBubbleMenuHooks
{
    private const int StripFrameCount = 20;
    private static readonly Vector2 WheelOrigin = new(100f, 100f);
    private IOpenGarrisonClientPluginContext? _context;
    private Texture2D? _bubbleWheelStrip;
    private Texture2D? _menuWheelZ;
    private Texture2D? _menuWheelX;
    private Texture2D? _menuWheelC;
    private Texture2D? _menuWheelX2R;
    private Texture2D? _menuWheelX2B;

    public string Id => "bubblewheel";

    public string DisplayName => "Bubble Wheel";

    public Version Version => new(1, 0, 0);

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
        _context = context;
    }

    public void Shutdown()
    {
        DisposeTexture(ref _bubbleWheelStrip);
        DisposeTexture(ref _menuWheelZ);
        DisposeTexture(ref _menuWheelX);
        DisposeTexture(ref _menuWheelC);
        DisposeTexture(ref _menuWheelX2R);
        DisposeTexture(ref _menuWheelX2B);
    }

    public void OnClientStarting()
    {
    }

    public void OnClientStarted()
    {
        EnsureTexturesLoaded();
    }

    public void OnClientStopping()
    {
    }

    public void OnClientStopped()
    {
    }

    public ClientBubbleMenuUpdateResult? TryHandleBubbleMenuInput(ClientBubbleMenuInputState inputState)
    {
        if (!inputState.LeftMousePressed)
        {
            return null;
        }

        return inputState.Kind switch
        {
            ClientBubbleMenuKind.Z => ResolveWheelSelection(inputState.SelectedSlotOrDefault(), 19),
            ClientBubbleMenuKind.C => ResolveWheelSelection(inputState.SelectedSlotOrDefault(), 35),
            ClientBubbleMenuKind.X => ResolveXSelection(inputState),
            _ => null,
        };
    }

    public bool TryDrawBubbleMenu(IOpenGarrisonClientHudCanvas canvas, ClientBubbleMenuRenderState renderState)
    {
        EnsureTexturesLoaded();
        if (_bubbleWheelStrip is null)
        {
            return false;
        }

        var center = new Vector2(canvas.ViewportWidth / 2f, canvas.ViewportHeight / 2f);
        var stripFrameWidth = _bubbleWheelStrip.Width / StripFrameCount;
        if (stripFrameWidth <= 0)
        {
            return false;
        }

        for (var index = 0; index < 10; index += 1)
        {
            var frameIndex = index == renderState.SelectedSlot ? index + 10 : index;
            var sourceRectangle = new Rectangle(
                stripFrameWidth * frameIndex,
                0,
                stripFrameWidth,
                _bubbleWheelStrip.Height);
            canvas.DrawScreenTexture(
                _bubbleWheelStrip,
                center,
                Color.White * renderState.Alpha,
                Vector2.One,
                sourceRectangle,
                0f,
                WheelOrigin);
        }

        var menuTexture = GetMenuTexture(renderState);
        if (menuTexture is null)
        {
            return false;
        }

        canvas.DrawScreenTexture(
            menuTexture,
            center,
            Color.White * renderState.Alpha,
            Vector2.One,
            null,
            0f,
            WheelOrigin);
        return true;
    }

    private ClientBubbleMenuUpdateResult? ResolveWheelSelection(int selectedSlot, int frameBase)
    {
        if (selectedSlot <= 0)
        {
            return new ClientBubbleMenuUpdateResult(CloseMenu: true);
        }

        return new ClientBubbleMenuUpdateResult(BubbleFrame: frameBase + selectedSlot);
    }

    private ClientBubbleMenuUpdateResult? ResolveXSelection(ClientBubbleMenuInputState inputState)
    {
        var selectedSlot = inputState.SelectedSlotOrDefault();
        if (inputState.XPageIndex == 0)
        {
            if (selectedSlot == 0)
            {
                return new ClientBubbleMenuUpdateResult(CloseMenu: true);
            }

            if (selectedSlot == 1)
            {
                return new ClientBubbleMenuUpdateResult(NewXPageIndex: 1);
            }

            if (selectedSlot == 2)
            {
                return new ClientBubbleMenuUpdateResult(NewXPageIndex: 2);
            }

            return selectedSlot is >= 3 and <= 9
                ? new ClientBubbleMenuUpdateResult(BubbleFrame: 26 + selectedSlot)
                : null;
        }

        var offset = inputState.XPageIndex == 2 ? 10 : 0;
        var bubbleFrame = selectedSlot == 0
            ? 9 + offset
            : (selectedSlot - 1) + offset;
        return new ClientBubbleMenuUpdateResult(BubbleFrame: bubbleFrame);
    }

    private Texture2D? GetMenuTexture(ClientBubbleMenuRenderState renderState)
    {
        return renderState.Kind switch
        {
            ClientBubbleMenuKind.Z => _menuWheelZ,
            ClientBubbleMenuKind.C => _menuWheelC,
            ClientBubbleMenuKind.X when renderState.XPageIndex == 1 => _menuWheelX2R,
            ClientBubbleMenuKind.X when renderState.XPageIndex == 2 => _menuWheelX2B,
            ClientBubbleMenuKind.X => _menuWheelX,
            _ => null,
        };
    }

    private void EnsureTexturesLoaded()
    {
        if (_context is null || _bubbleWheelStrip is not null)
        {
            return;
        }

        var resourceDirectory = Path.Combine(_context.PluginDirectory, "Resources", "PrOF", "BubbleWheel");
        _bubbleWheelStrip = LoadTexture(resourceDirectory, "BubbleWheelStrip.png");
        _menuWheelZ = LoadTexture(resourceDirectory, "MenuWheelZ.png");
        _menuWheelX = LoadTexture(resourceDirectory, "MenuWheelX.png");
        _menuWheelC = LoadTexture(resourceDirectory, "MenuWheelC.png");
        _menuWheelX2R = LoadTexture(resourceDirectory, "MenuWheelX2R.png");
        _menuWheelX2B = LoadTexture(resourceDirectory, "MenuWheelX2B.png");
    }

    private Texture2D? LoadTexture(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
        {
            _context?.Log($"missing texture at {path}");
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            return Texture2D.FromStream(_context!.GraphicsDevice, stream);
        }
        catch (Exception ex)
        {
            _context?.Log($"failed to load texture {fileName}: {ex.Message}");
            return null;
        }
    }

    private static void DisposeTexture(ref Texture2D? texture)
    {
        if (texture is null)
        {
            return;
        }

        try
        {
            texture.Dispose();
        }
        catch
        {
        }

        texture = null;
    }
}

internal static class BubbleWheelInputStateExtensions
{
    public static int SelectedSlotOrDefault(this ClientBubbleMenuInputState inputState)
    {
        if (inputState.DistanceFromCenter < 30f)
        {
            return 0;
        }

        var aimDirection = inputState.AimDirectionDegrees + 90f;
        while (aimDirection >= 360f)
        {
            aimDirection -= 360f;
        }

        return Math.Clamp((int)(aimDirection / 40f) + 1, 1, 9);
    }
}
