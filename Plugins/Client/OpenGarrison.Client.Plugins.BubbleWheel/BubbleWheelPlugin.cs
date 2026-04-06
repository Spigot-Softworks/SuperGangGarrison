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
    private ClientBubbleMenuKind _lastBubbleMenuKind = ClientBubbleMenuKind.None;
    private int _lastBubbleMenuXPageIndex;
    private int _lastHoveredSlot = -1;

    public string Id => "bubblewheel";

    public string DisplayName => "Bubble Wheel";

    public Version Version => new(1, 0, 0);

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
        _context = context;
    }

    public void Shutdown()
    {
        _bubbleWheelStrip = null;
        _menuWheelZ = null;
        _menuWheelX = null;
        _menuWheelC = null;
        _menuWheelX2R = null;
        _menuWheelX2B = null;
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
        if (inputState.Kind == ClientBubbleMenuKind.None)
        {
            ResetHoverSelectionState();
            return null;
        }

        if (inputState.Kind == ClientBubbleMenuKind.X
            && inputState.PressedDigit is >= 1 and <= 3)
        {
            var requestedPageIndex = inputState.PressedDigit.Value - 1;
            if (requestedPageIndex != inputState.XPageIndex)
            {
                _lastBubbleMenuKind = inputState.Kind;
                _lastBubbleMenuXPageIndex = requestedPageIndex;
                _lastHoveredSlot = -1;
                return new ClientBubbleMenuUpdateResult(NewXPageIndex: requestedPageIndex, ClearBubbleSelection: true);
            }
        }

        var selectedSlot = inputState.SelectedSlotOrDefault();
        var menuChanged = inputState.Kind != _lastBubbleMenuKind || inputState.XPageIndex != _lastBubbleMenuXPageIndex;
        var slotChanged = selectedSlot != _lastHoveredSlot;

        _lastBubbleMenuKind = inputState.Kind;
        _lastBubbleMenuXPageIndex = inputState.XPageIndex;

        if (!menuChanged && !slotChanged && !inputState.QPressed)
        {
            return null;
        }

        _lastHoveredSlot = selectedSlot;

        var result = inputState.Kind switch
        {
            ClientBubbleMenuKind.Z => ResolveWheelSelection(selectedSlot, 19),
            ClientBubbleMenuKind.C => ResolveWheelSelection(selectedSlot, 35),
            ClientBubbleMenuKind.X => ResolveXSelection(inputState, selectedSlot),
            _ => null,
        };

        if (result is null)
        {
            return null;
        }

        if (result.NewXPageIndex.HasValue)
        {
            _lastBubbleMenuXPageIndex = result.NewXPageIndex.Value;
        }

        return result;
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

    private static ClientBubbleMenuUpdateResult? ResolveWheelSelection(int selectedSlot, int frameBase)
    {
        if (selectedSlot <= 0)
        {
            return new ClientBubbleMenuUpdateResult(ClearBubbleSelection: true);
        }

        return new ClientBubbleMenuUpdateResult(BubbleFrame: frameBase + selectedSlot);
    }

    private static ClientBubbleMenuUpdateResult? ResolveXSelection(ClientBubbleMenuInputState inputState, int selectedSlot)
    {
        if (inputState.XPageIndex == 0)
        {
            if (selectedSlot == 0)
            {
                return new ClientBubbleMenuUpdateResult(ClearBubbleSelection: true);
            }

            if (selectedSlot is 1 or 2)
            {
                return new ClientBubbleMenuUpdateResult(ClearBubbleSelection: true);
            }

            return selectedSlot is >= 3 and <= 9
                ? new ClientBubbleMenuUpdateResult(BubbleFrame: 26 + selectedSlot)
                : null;
        }

        if (inputState.QPressed)
        {
            return new ClientBubbleMenuUpdateResult(BubbleFrame: inputState.XPageIndex == 2 ? 48 : 47);
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

        _bubbleWheelStrip = RegisterTexture("bubblewheel-strip", "Resources/PrOF/BubbleWheel/BubbleWheelStrip.png");
        _menuWheelZ = RegisterTexture("bubblewheel-z", "Resources/PrOF/BubbleWheel/MenuWheelZ.png");
        _menuWheelX = RegisterTexture("bubblewheel-x", "Resources/PrOF/BubbleWheel/MenuWheelX.png");
        _menuWheelC = RegisterTexture("bubblewheel-c", "Resources/PrOF/BubbleWheel/MenuWheelC.png");
        _menuWheelX2R = RegisterTexture("bubblewheel-x2r", "Resources/PrOF/BubbleWheel/MenuWheelX2R.png");
        _menuWheelX2B = RegisterTexture("bubblewheel-x2b", "Resources/PrOF/BubbleWheel/MenuWheelX2B.png");
    }

    private Texture2D? RegisterTexture(string assetId, string relativePath)
    {
        if (_context is null)
        {
            return null;
        }
        try
        {
            _context.Assets.RegisterTextureAsset(assetId, relativePath);
            if (_context.Assets.TryGetTextureAsset(assetId, out var texture))
            {
                return texture;
            }

            _context.Log($"registered texture asset unavailable: {assetId}");
            return null;
        }
        catch (Exception ex)
        {
            _context.Log($"failed to load texture {assetId}: {ex.Message}");
            return null;
        }
    }

    private void ResetHoverSelectionState()
    {
        _lastBubbleMenuKind = ClientBubbleMenuKind.None;
        _lastBubbleMenuXPageIndex = 0;
        _lastHoveredSlot = -1;
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
