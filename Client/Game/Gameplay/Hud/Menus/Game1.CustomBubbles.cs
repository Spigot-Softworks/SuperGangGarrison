#nullable enable

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using OpenGarrison.Protocol;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;

namespace OpenGarrison.Client;

public partial class Game1
{
    private const int CustomBubbleShellOriginX = 5;
    private const int CustomBubbleShellOriginY = 57;
    private const int CustomBubbleShellPixelWidth = 72;
    private const int CustomBubbleShellPixelHeight = 58;
    private const int CustomBubbleDrawableInsetPixels = 2;
    private const float CustomBubbleGameplayScale = 0.5f;

    private readonly CustomBubbleDocument _customBubbleDocument = CustomBubbleDocument.Load();
    private readonly Dictionary<byte, CustomBubbleRenderState> _customBubbleRenderStatesByPlayerSlot = new();
    private CustomBubbleEditorController? _customBubbleEditorController;
    private CustomBubbleRenderState? _localCustomBubbleRenderState;
    private LoadedSpriteFrame? _customBubbleShellFrame;
    private bool[]? _customBubbleCanvasMask;
    private bool[]? _customBubbleShellInteriorMask;
    private bool _customBubbleEditorOpen;
    private bool _customBubbleEditorReturnToOptions;
    private bool _customBubbleEditorReturnFromGameplayOptions;
    private bool _customBubbleEditorReturnToProfile;
    private bool _showCustomBubbles = true;
    private int _selectedCustomBubbleSlot;

    private CustomBubbleEditorController CustomBubbleEditor => _customBubbleEditorController ??= new CustomBubbleEditorController(this);

    private void ApplyLoadedCustomBubbleSettings()
    {
        _customBubbleDocument.Normalize();
        _showCustomBubbles = _customBubbleDocument.ShowCustomBubbles;
        _selectedCustomBubbleSlot = CustomBubbleDocument.NormalizeSlotIndex(_customBubbleDocument.SelectedSlot);
    }

    private void ToggleCustomBubbleVisibilitySetting()
    {
        _showCustomBubbles = !_showCustomBubbles;
        PersistCustomBubbleDocument();
    }

    private void CycleCustomBubbleSlotSetting()
    {
        _selectedCustomBubbleSlot = (_selectedCustomBubbleSlot + 1) % CustomBubbleDocument.SlotCount;
        PersistCustomBubbleDocument();
        InvalidateLocalCustomBubbleRenderState();
        UploadSelectedCustomBubbleState();
    }

    private void SelectCustomBubbleSlotSetting(int slotIndex)
    {
        _selectedCustomBubbleSlot = CustomBubbleDocument.NormalizeSlotIndex(slotIndex);
        PersistCustomBubbleDocument();
        InvalidateLocalCustomBubbleRenderState();
        UploadSelectedCustomBubbleState();
    }

    private void PersistCustomBubbleDocument()
    {
        _customBubbleDocument.ShowCustomBubbles = _showCustomBubbles;
        _customBubbleDocument.SelectedSlot = _selectedCustomBubbleSlot;
        _customBubbleDocument.Save();
    }

    private string GetSelectedCustomBubbleSlotLabel()
    {
        return GetCustomBubbleSlotLabel(_selectedCustomBubbleSlot);
    }

    private static string GetCustomBubbleSlotLabel(int slotIndex)
    {
        return $"Bubble {CustomBubbleDocument.NormalizeSlotIndex(slotIndex) + 1}";
    }

    private void OpenCustomBubbleEditorFromProfile(int slotIndex)
    {
        SelectCustomBubbleSlotSetting(slotIndex);
        OpenCustomBubbleEditor(returnToOptions: false, returnFromGameplayOptions: false, returnToProfile: true);
    }

    private void OpenCustomBubbleEditor(bool returnToOptions, bool returnFromGameplayOptions, bool returnToProfile = false)
    {
        _customBubbleEditorOpen = true;
        _customBubbleEditorReturnToOptions = returnToOptions;
        _customBubbleEditorReturnFromGameplayOptions = returnFromGameplayOptions;
        _customBubbleEditorReturnToProfile = returnToProfile;
        _optionsMenuOpen = false;
        _optionsMenuOpenedFromGameplay = false;
        _pluginOptionsMenuOpen = false;
        _pluginOptionsMenuOpenedFromGameplay = false;
        _controlsMenuOpen = false;
        _controlsMenuOpenedFromGameplay = false;
        _pendingControlsBinding = null;
        _pendingControllerControlsBinding = null;
        _friendsMenuOpen = false;
        _inGameMenuOpen = false;
        _inGameMenuAwaitingEscapeRelease = false;
        _editingPlayerName = false;
        CustomBubbleEditor.Open(_selectedCustomBubbleSlot);
    }

    private void CloseCustomBubbleEditor()
    {
        var returnToOptions = _customBubbleEditorReturnToOptions;
        var returnFromGameplay = _customBubbleEditorReturnFromGameplayOptions;
        var returnToProfile = _customBubbleEditorReturnToProfile;
        DismissCustomBubbleEditor();
        if (returnToProfile)
        {
            _mainMenuOverlayStateController.OpenFriendsMenu();
            _friendsMenuTab = FriendsMenuTab.Bubble;
            _editingFriendCode = false;
            _editingFriendMessage = false;
            _editingFriendNickname = false;
            _menuStatusMessage = string.Empty;
        }
        else if (returnToOptions)
        {
            OpenOptionsMenu(returnFromGameplay);
            _optionsPageIndex = 2;
        }
    }

    private void DismissCustomBubbleEditor()
    {
        if (!_customBubbleEditorOpen)
        {
            return;
        }

        _customBubbleEditorOpen = false;
        _customBubbleEditorReturnToOptions = false;
        _customBubbleEditorReturnFromGameplayOptions = false;
        _customBubbleEditorReturnToProfile = false;
        _customBubbleEditorController?.Close();
    }

    private void SaveCustomBubbleEditorPixels(int slotIndex, byte[] pixels)
    {
        _selectedCustomBubbleSlot = CustomBubbleDocument.NormalizeSlotIndex(slotIndex);
        _customBubbleDocument.SetSlotPixels(_selectedCustomBubbleSlot, pixels);
        PersistCustomBubbleDocument();
        InvalidateLocalCustomBubbleRenderState();
        UploadSelectedCustomBubbleState();
    }

    private void UpdateCustomBubbleEditor(KeyboardState keyboard, MouseState mouse)
    {
        CustomBubbleEditor.Update(keyboard, mouse);
    }

    private void DrawCustomBubbleEditor()
    {
        CustomBubbleEditor.Draw();
    }

    private bool CanTriggerCustomBubble(KeyboardState keyboard, MouseState mouse)
    {
        return IsBindingPressed(keyboard, mouse, _inputBindings.CustomBubble)
            && !ShouldCloseBubbleMenuForGameplayState()
            && !IsLocalSpectatorPresentationActive()
            && _world.LocalPlayer.IsAlive
            && !IsGameplayInputBlocked();
    }

    private void UpdateCustomBubbleHotkey(KeyboardState keyboard, MouseState mouse)
    {
        if (!CanTriggerCustomBubble(keyboard, mouse)
            || !_customBubbleDocument.HasSlot(_selectedCustomBubbleSlot))
        {
            return;
        }

        ApplyLocalChatBubble(ChatBubbleFrameCatalog.GetCustomBubbleFrame(_selectedCustomBubbleSlot));
    }

    private void UploadSelectedCustomBubbleState()
    {
        if (!_networkClient.IsConnected || _networkClient.IsAwaitingWelcome || _networkClient.IsReplayConnection)
        {
            return;
        }

        if (_customBubbleDocument.TryGetSlotPixels(_selectedCustomBubbleSlot, out var pixels, out var revision))
        {
            _networkClient.SendCustomBubbleUpload((byte)_selectedCustomBubbleSlot, revision, pixels);
            return;
        }

        _networkClient.SendCustomBubbleClear();
    }

    private void HandleCustomBubbleStateMessage(CustomBubbleStateMessage message)
    {
        if (message.PlayerSlot == 0
            || message.Rgba64Pixels.Length != CustomBubbleDocument.Rgba64ByteCount
            || message.Slot >= CustomBubbleDocument.SlotCount)
        {
            return;
        }

        if (_customBubbleRenderStatesByPlayerSlot.TryGetValue(message.PlayerSlot, out var existing))
        {
            existing.Dispose();
        }

        _customBubbleRenderStatesByPlayerSlot[message.PlayerSlot] = new CustomBubbleRenderState(
            message.Slot,
            message.Revision,
            (byte[])message.Rgba64Pixels.Clone());
    }

    private void HandleCustomBubbleClearMessage(CustomBubbleClearMessage message)
    {
        if (_customBubbleRenderStatesByPlayerSlot.Remove(message.PlayerSlot, out var existing))
        {
            existing.Dispose();
        }
    }

    private void ClearRemoteCustomBubbleStates()
    {
        foreach (var state in _customBubbleRenderStatesByPlayerSlot.Values)
        {
            state.Dispose();
        }

        _customBubbleRenderStatesByPlayerSlot.Clear();
    }

    private LoadedSpriteFrame? GetCustomBubbleShellFrame()
    {
        if (_customBubbleShellFrame is not null)
        {
            return _customBubbleShellFrame;
        }

        _customBubbleShellFrame = LoadSpriteFrameFromPath(ContentRoot.GetPath("Sprites", "InGameElements", "CustomBubble", "big_bubble.png"))
            ?? LoadCustomBubbleShellFrameFromFallback();
        _customBubbleCanvasMask = null;
        _customBubbleShellInteriorMask = null;
        return _customBubbleShellFrame;
    }

    private LoadedSpriteFrame? LoadCustomBubbleShellFrameFromFallback()
    {
        if (OperatingSystem.IsBrowser())
        {
            return null;
        }

        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Content", "Sprites", "InGameElements", "CustomBubble", "big_bubble.png"),
            Path.Combine(Environment.CurrentDirectory, "Content", "Sprites", "InGameElements", "CustomBubble", "big_bubble.png"),
            Path.Combine(Environment.CurrentDirectory, "Core", "Content", "Sprites", "InGameElements", "CustomBubble", "big_bubble.png"),
            ProjectSourceLocator.FindFile(Path.Combine("Core", "Content", "Sprites", "InGameElements", "CustomBubble", "big_bubble.png")) ?? string.Empty,
            Path.Combine(Environment.CurrentDirectory, "big_bubble.png"),
        };

        foreach (var candidate in candidates)
        {
            var frame = LoadSpriteFrameFromPath(candidate);
            if (frame is not null)
            {
                return frame;
            }
        }

        return null;
    }

    private bool TryGetCustomBubbleTextureForPlayer(PlayerEntity player, int slotIndex, out Texture2D texture)
    {
        texture = null!;
        if (slotIndex < 0 || slotIndex >= CustomBubbleDocument.SlotCount)
        {
            return false;
        }

        if (ReferenceEquals(player, _world.LocalPlayer))
        {
            return TryGetLocalCustomBubbleTexture(slotIndex, out texture);
        }

        if (!_world.TryGetPlayerNetworkSlot(player, out var playerSlot))
        {
            return false;
        }

        if (_networkClient.IsConnected
            && !_networkClient.IsSpectator
            && playerSlot == _networkClient.LocalPlayerSlot)
        {
            return TryGetLocalCustomBubbleTexture(slotIndex, out texture);
        }

        if (!_customBubbleRenderStatesByPlayerSlot.TryGetValue(playerSlot, out var state)
            || state.SlotIndex != slotIndex)
        {
            return false;
        }

        texture = state.GetOrCreateTexture(this);
        return texture is not null;
    }

    private bool TryGetLocalCustomBubbleTexture(int slotIndex, out Texture2D texture)
    {
        texture = null!;
        if (!_customBubbleDocument.TryGetSlotPixels(slotIndex, out var pixels, out var revision))
        {
            return false;
        }

        if (_localCustomBubbleRenderState is null
            || _localCustomBubbleRenderState.SlotIndex != slotIndex
            || _localCustomBubbleRenderState.Revision != revision)
        {
            InvalidateLocalCustomBubbleRenderState();
            _localCustomBubbleRenderState = new CustomBubbleRenderState(slotIndex, revision, pixels);
        }

        texture = _localCustomBubbleRenderState.GetOrCreateTexture(this);
        return texture is not null;
    }

    private void InvalidateLocalCustomBubbleRenderState()
    {
        _localCustomBubbleRenderState?.Dispose();
        _localCustomBubbleRenderState = null;
    }

    private bool IsCustomBubbleCanvasPixelInsideShell(int pixelIndex)
    {
        if (pixelIndex < 0 || pixelIndex >= CustomBubbleDocument.BubbleWidth * CustomBubbleDocument.BubbleHeight)
        {
            return false;
        }

        _customBubbleCanvasMask ??= BuildCustomBubbleCanvasMask();
        return _customBubbleCanvasMask[pixelIndex];
    }

    private bool[] BuildCustomBubbleCanvasMask()
    {
        var mask = new bool[CustomBubbleDocument.BubbleWidth * CustomBubbleDocument.BubbleHeight];
        var shellMask = GetCustomBubbleShellInteriorMask();
        if (shellMask is null)
        {
            return mask;
        }

        for (var y = 0; y < CustomBubbleDocument.BubbleHeight; y += 1)
        {
            for (var x = 0; x < CustomBubbleDocument.BubbleWidth; x += 1)
            {
                var index = (y * CustomBubbleDocument.BubbleWidth) + x;
                mask[index] = IsCustomBubbleShellPixelDrawable(shellMask, x, y);
            }
        }

        return mask;
    }

    private static bool IsCustomBubbleShellPixelDrawable(bool[] shellMask, int x, int y)
    {
        if (!shellMask[(y * CustomBubbleShellPixelWidth) + x])
        {
            return false;
        }

        for (var offsetY = -CustomBubbleDrawableInsetPixels; offsetY <= CustomBubbleDrawableInsetPixels; offsetY += 1)
        {
            for (var offsetX = -CustomBubbleDrawableInsetPixels; offsetX <= CustomBubbleDrawableInsetPixels; offsetX += 1)
            {
                var sampleX = x + offsetX;
                var sampleY = y + offsetY;
                if (sampleX < 0
                    || sampleY < 0
                    || sampleX >= CustomBubbleShellPixelWidth
                    || sampleY >= CustomBubbleShellPixelHeight
                    || !shellMask[(sampleY * CustomBubbleShellPixelWidth) + sampleX])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool[]? GetCustomBubbleShellInteriorMask()
    {
        if (_customBubbleShellInteriorMask is not null)
        {
            return _customBubbleShellInteriorMask;
        }

        var shellFrame = GetCustomBubbleShellFrame();
        if (shellFrame is null
            || shellFrame.Width != CustomBubbleShellPixelWidth
            || shellFrame.Height != CustomBubbleShellPixelHeight)
        {
            return null;
        }

        var pixels = new Color[CustomBubbleShellPixelWidth * CustomBubbleShellPixelHeight];
        if (!shellFrame.TryCopyPixelData(pixels))
        {
            var source = shellFrame.SourceRectangle ?? new Rectangle(0, 0, shellFrame.Texture.Width, shellFrame.Texture.Height);
            shellFrame.Texture.GetData(0, source, pixels, 0, pixels.Length);
        }

        var mask = new bool[pixels.Length];
        for (var index = 0; index < pixels.Length; index += 1)
        {
            var color = pixels[index];
            mask[index] = color.A > 0
                && color.R >= 222
                && color.G >= 222
                && color.B >= 222;
        }

        _customBubbleShellInteriorMask = mask;
        return _customBubbleShellInteriorMask;
    }

    private Texture2D CreateCustomBubbleShellTexture(byte[] pixels)
    {
        if (pixels.Length != CustomBubbleDocument.Rgba64ByteCount)
        {
            throw new ArgumentException($"Custom bubble data must be exactly {CustomBubbleDocument.Rgba64ByteCount} bytes.", nameof(pixels));
        }

        var shellMask = GetCustomBubbleShellInteriorMask();
        _customBubbleCanvasMask ??= BuildCustomBubbleCanvasMask();
        var colors = new Color[CustomBubbleShellPixelWidth * CustomBubbleShellPixelHeight];
        for (var y = 0; y < CustomBubbleShellPixelHeight; y += 1)
        {
            var sourceY = Math.Clamp(
                (int)MathF.Round((y / (float)(CustomBubbleShellPixelHeight - 1)) * (CustomBubbleDocument.BubbleHeight - 1)),
                0,
                CustomBubbleDocument.BubbleHeight - 1);
            for (var x = 0; x < CustomBubbleShellPixelWidth; x += 1)
            {
                var destinationIndex = (y * CustomBubbleShellPixelWidth) + x;
                var sourceX = Math.Clamp(
                    (int)MathF.Round((x / (float)(CustomBubbleShellPixelWidth - 1)) * (CustomBubbleDocument.BubbleWidth - 1)),
                    0,
                    CustomBubbleDocument.BubbleWidth - 1);
                var sourceIndex = (sourceY * CustomBubbleDocument.BubbleWidth) + sourceX;
                if (shellMask is not null && !shellMask[destinationIndex])
                {
                    colors[destinationIndex] = Color.Transparent;
                    continue;
                }

                if (!_customBubbleCanvasMask[sourceIndex])
                {
                    colors[destinationIndex] = Color.Transparent;
                    continue;
                }

                colors[destinationIndex] = ReadRgba64ColorPremultiplied(pixels, sourceIndex);
            }
        }

        var texture = new Texture2D(GraphicsDevice, CustomBubbleShellPixelWidth, CustomBubbleShellPixelHeight, false, SurfaceFormat.Color);
        texture.SetData(colors);
        return texture;
    }

    private static Color ReadRgba64ColorPremultiplied(byte[] pixels, int pixelIndex)
    {
        var offset = pixelIndex * CustomBubbleDocument.BytesPerPixel;
        var r16 = BinaryPrimitives.ReadUInt16LittleEndian(pixels.AsSpan(offset, 2));
        var g16 = BinaryPrimitives.ReadUInt16LittleEndian(pixels.AsSpan(offset + 2, 2));
        var b16 = BinaryPrimitives.ReadUInt16LittleEndian(pixels.AsSpan(offset + 4, 2));
        var a16 = BinaryPrimitives.ReadUInt16LittleEndian(pixels.AsSpan(offset + 6, 2));
        var a8 = ToByteChannel(a16);
        if (a8 == 0)
        {
            return Color.Transparent;
        }

        return new Color(
            Premultiply(ToByteChannel(r16), a8),
            Premultiply(ToByteChannel(g16), a8),
            Premultiply(ToByteChannel(b16), a8),
            a8);
    }

    private static Color ReadRgba64Color(byte[] pixels, int pixelIndex)
    {
        var offset = pixelIndex * CustomBubbleDocument.BytesPerPixel;
        return new Color(
            ToByteChannel(BinaryPrimitives.ReadUInt16LittleEndian(pixels.AsSpan(offset, 2))),
            ToByteChannel(BinaryPrimitives.ReadUInt16LittleEndian(pixels.AsSpan(offset + 2, 2))),
            ToByteChannel(BinaryPrimitives.ReadUInt16LittleEndian(pixels.AsSpan(offset + 4, 2))),
            ToByteChannel(BinaryPrimitives.ReadUInt16LittleEndian(pixels.AsSpan(offset + 6, 2))));
    }

    private static ulong ReadRgba64Pixel(byte[] pixels, int pixelIndex)
    {
        return BinaryPrimitives.ReadUInt64LittleEndian(pixels.AsSpan(pixelIndex * CustomBubbleDocument.BytesPerPixel, CustomBubbleDocument.BytesPerPixel));
    }

    private static void WriteRgba64Pixel(byte[] pixels, int pixelIndex, ulong value)
    {
        BinaryPrimitives.WriteUInt64LittleEndian(pixels.AsSpan(pixelIndex * CustomBubbleDocument.BytesPerPixel, CustomBubbleDocument.BytesPerPixel), value);
    }

    private static ulong PackRgba64Color(Color color)
    {
        var r16 = ToUShortChannel(color.R);
        var g16 = ToUShortChannel(color.G);
        var b16 = ToUShortChannel(color.B);
        var a16 = ToUShortChannel(color.A);
        return r16
            | ((ulong)g16 << 16)
            | ((ulong)b16 << 32)
            | ((ulong)a16 << 48);
    }

    private static ushort ToUShortChannel(byte value)
    {
        return (ushort)(value * 257);
    }

    private static byte ToByteChannel(ushort value)
    {
        return (byte)((value + 128) / 257);
    }

    private static byte Premultiply(byte value, byte alpha)
    {
        return (byte)((value * alpha + 127) / 255);
    }

    private sealed class CustomBubbleRenderState : IDisposable
    {
        private readonly byte[] _pixels;
        private Texture2D? _texture;

        public CustomBubbleRenderState(int slotIndex, uint revision, byte[] pixels)
        {
            SlotIndex = CustomBubbleDocument.NormalizeSlotIndex(slotIndex);
            Revision = revision;
            _pixels = pixels;
        }

        public int SlotIndex { get; }

        public uint Revision { get; }

        public Texture2D GetOrCreateTexture(Game1 game)
        {
            _texture ??= game.CreateCustomBubbleShellTexture(_pixels);
            return _texture;
        }

        public void Dispose()
        {
            _texture?.Dispose();
            _texture = null;
        }
    }
}
