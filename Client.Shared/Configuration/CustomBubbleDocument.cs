#nullable enable

using OpenGarrison.Core;
using System.Globalization;

namespace OpenGarrison.ClientShared;

public sealed class CustomBubbleDocument
{
    public const int BubbleWidth = 72;
    public const int BubbleHeight = 58;
    public const int BytesPerPixel = 8;
    public const int SlotCount = 3;
    public const int PaletteColorCount = 64;
    public const int Rgba64ByteCount = BubbleWidth * BubbleHeight * BytesPerPixel;
    private const int LegacyBubbleWidth = 32;
    private const int LegacyBubbleHeight = 32;
    private const int LegacyRgba64ByteCount = LegacyBubbleWidth * LegacyBubbleHeight * BytesPerPixel;
    private const int Rgba64Base64Length = ((Rgba64ByteCount + 2) / 3) * 4;
    private const int LegacyRgba64Base64Length = ((LegacyRgba64ByteCount + 2) / 3) * 4;
    private const int MaxPaletteColorTextLength = 16;
    private const int MaxBase64WhitespaceSlack = 16;
    private const long MaxDocumentBytes = 256 * 1024;
    public const string DefaultRelativePath = "CustomBubbles/custom-bubbles.json";

    public bool ShowCustomBubbles { get; set; } = true;

    public int SelectedSlot { get; set; }

    public List<CustomBubbleSlotDocument> Slots { get; set; } = [];

    public List<string> CustomPalette { get; set; } = [];

    public static CustomBubbleDocument Load(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            var browserDocument = new CustomBubbleDocument();
            browserDocument.Normalize();
            return browserDocument;
        }

        var resolvedPath = path ?? RuntimePaths.GetUserDataPath(DefaultRelativePath);
        if (!CanLoadDocument(resolvedPath))
        {
            var fallbackDocument = new CustomBubbleDocument();
            fallbackDocument.Normalize();
            TrySave(resolvedPath, fallbackDocument);
            return fallbackDocument;
        }

        CustomBubbleDocument document;
        try
        {
            document = JsonConfigurationFile.LoadOrCreate(resolvedPath, static () => new CustomBubbleDocument());
        }
        catch (IOException)
        {
            document = new CustomBubbleDocument();
        }
        catch (UnauthorizedAccessException)
        {
            document = new CustomBubbleDocument();
        }

        document.Normalize();
        TrySave(resolvedPath, document);
        return document;
    }

    public void Save(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var resolvedPath = path ?? RuntimePaths.GetUserDataPath(DefaultRelativePath);
        Normalize();
        JsonConfigurationFile.Save(resolvedPath, this);
    }

    public bool HasSlot(int slotIndex)
    {
        return TryGetSlotPixels(slotIndex, out _, out _);
    }

    public bool TryGetSlotPixels(int slotIndex, out byte[] pixels, out uint revision)
    {
        pixels = [];
        revision = 0;
        if (slotIndex < 0 || slotIndex >= SlotCount)
        {
            return false;
        }

        Normalize();
        var slot = Slots[slotIndex];
        if (string.IsNullOrEmpty(slot.Rgba64Base64))
        {
            return false;
        }

        if (TryDecodeSlotPixels(slot.Rgba64Base64, out var decoded, out _, out _))
        {
            pixels = decoded;
            revision = slot.Revision;
            return true;
        }

        return false;
    }

    public void SetSlotPixels(int slotIndex, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length != Rgba64ByteCount)
        {
            throw new ArgumentException($"Custom bubble data must be exactly {Rgba64ByteCount} bytes.", nameof(pixels));
        }

        Normalize();
        var normalizedSlot = NormalizeSlotIndex(slotIndex);
        var slot = Slots[normalizedSlot];
        slot.Rgba64Base64 = Convert.ToBase64String(pixels);
        slot.Revision = slot.Revision == uint.MaxValue ? 1u : slot.Revision + 1u;
    }

    public void ClearSlot(int slotIndex)
    {
        Normalize();
        var normalizedSlot = NormalizeSlotIndex(slotIndex);
        var slot = Slots[normalizedSlot];
        slot.Rgba64Base64 = string.Empty;
        slot.Revision = slot.Revision == uint.MaxValue ? 1u : slot.Revision + 1u;
    }

    public bool TryGetCustomPaletteColorHex(int colorIndex, out string colorHex)
    {
        colorHex = string.Empty;
        if (colorIndex < 0 || colorIndex >= PaletteColorCount)
        {
            return false;
        }

        Normalize();
        colorHex = CustomPalette[colorIndex];
        return !string.IsNullOrWhiteSpace(colorHex);
    }

    public void SetCustomPaletteColorHex(int colorIndex, string colorHex)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(colorHex);
        if (!TryNormalizeColorHex(colorHex, out var normalizedHex))
        {
            throw new ArgumentException("Custom palette colors must be 8-digit RGBA hex values.", nameof(colorHex));
        }

        Normalize();
        CustomPalette[NormalizePaletteIndex(colorIndex)] = normalizedHex;
    }

    public void Normalize()
    {
        SelectedSlot = NormalizeSlotIndex(SelectedSlot);
        NormalizeCustomPalette();
        Slots ??= [];
        while (Slots.Count < SlotCount)
        {
            Slots.Add(new CustomBubbleSlotDocument());
        }

        if (Slots.Count > SlotCount)
        {
            Slots.RemoveRange(SlotCount, Slots.Count - SlotCount);
        }

        for (var index = 0; index < Slots.Count; index += 1)
        {
            Slots[index] ??= new CustomBubbleSlotDocument();
            var slot = Slots[index];
            if (string.IsNullOrEmpty(slot.Rgba64Base64))
            {
                slot.Rgba64Base64 = string.Empty;
                continue;
            }

            if (TryDecodeSlotPixels(slot.Rgba64Base64, out _, out var normalizedBase64, out _))
            {
                if (!string.Equals(slot.Rgba64Base64, normalizedBase64, StringComparison.Ordinal))
                {
                    slot.Rgba64Base64 = normalizedBase64;
                }

                continue;
            }

            slot.Rgba64Base64 = string.Empty;
        }
    }

    public static int NormalizeSlotIndex(int slotIndex)
    {
        return Math.Clamp(slotIndex, 0, SlotCount - 1);
    }

    private static int NormalizePaletteIndex(int colorIndex)
    {
        return Math.Clamp(colorIndex, 0, PaletteColorCount - 1);
    }

    private void NormalizeCustomPalette()
    {
        CustomPalette ??= [];
        while (CustomPalette.Count < PaletteColorCount)
        {
            CustomPalette.Add(string.Empty);
        }

        if (CustomPalette.Count > PaletteColorCount)
        {
            CustomPalette.RemoveRange(PaletteColorCount, CustomPalette.Count - PaletteColorCount);
        }

        for (var index = 0; index < CustomPalette.Count; index += 1)
        {
            if (string.IsNullOrWhiteSpace(CustomPalette[index]))
            {
                CustomPalette[index] = string.Empty;
                continue;
            }

            if (TryNormalizeColorHex(CustomPalette[index], out var normalizedHex))
            {
                CustomPalette[index] = normalizedHex;
                continue;
            }

            CustomPalette[index] = string.Empty;
        }
    }

    private static bool TryNormalizeColorHex(string colorHex, out string normalizedHex)
    {
        normalizedHex = string.Empty;
        if (colorHex.Length > MaxPaletteColorTextLength)
        {
            return false;
        }

        var trimmed = colorHex.Trim().TrimStart('#');
        if (trimmed.Length != 8
            || !uint.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        normalizedHex = trimmed.ToUpperInvariant();
        return true;
    }

    private static bool TryDecodeSlotPixels(
        string? rgba64Base64,
        out byte[] pixels,
        out string normalizedBase64,
        out bool upgradedLegacy)
    {
        pixels = [];
        normalizedBase64 = string.Empty;
        upgradedLegacy = false;
        if (string.IsNullOrEmpty(rgba64Base64)
            || rgba64Base64.Length > Rgba64Base64Length + MaxBase64WhitespaceSlack)
        {
            return false;
        }

        var trimmed = rgba64Base64.Trim();
        if (trimmed.Length != Rgba64Base64Length && trimmed.Length != LegacyRgba64Base64Length)
        {
            return false;
        }

        byte[] decoded;
        try
        {
            decoded = Convert.FromBase64String(trimmed);
        }
        catch (FormatException)
        {
            return false;
        }

        if (decoded.Length == Rgba64ByteCount)
        {
            pixels = decoded;
            normalizedBase64 = trimmed;
            return true;
        }

        if (decoded.Length == LegacyRgba64ByteCount)
        {
            pixels = UpgradeLegacyPixels(decoded);
            normalizedBase64 = Convert.ToBase64String(pixels);
            upgradedLegacy = true;
            return true;
        }

        return false;
    }

    private static bool CanLoadDocument(string path)
    {
        if (!File.Exists(path))
        {
            return true;
        }

        try
        {
            return new FileInfo(path).Length <= MaxDocumentBytes;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void TrySave(string path, CustomBubbleDocument document)
    {
        try
        {
            document.Save(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static byte[] UpgradeLegacyPixels(byte[] legacyPixels)
    {
        var upgraded = new byte[Rgba64ByteCount];
        for (var y = 0; y < BubbleHeight; y += 1)
        {
            var sourceY = Math.Clamp(
                (int)MathF.Round((y / (float)(BubbleHeight - 1)) * (LegacyBubbleHeight - 1)),
                0,
                LegacyBubbleHeight - 1);
            for (var x = 0; x < BubbleWidth; x += 1)
            {
                var sourceX = Math.Clamp(
                    (int)MathF.Round((x / (float)(BubbleWidth - 1)) * (LegacyBubbleWidth - 1)),
                    0,
                    LegacyBubbleWidth - 1);
                var sourceOffset = ((sourceY * LegacyBubbleWidth) + sourceX) * BytesPerPixel;
                var destinationOffset = ((y * BubbleWidth) + x) * BytesPerPixel;
                Array.Copy(legacyPixels, sourceOffset, upgraded, destinationOffset, BytesPerPixel);
            }
        }

        return upgraded;
    }
}

public sealed class CustomBubbleSlotDocument
{
    public string Rgba64Base64 { get; set; } = string.Empty;

    public uint Revision { get; set; }
}
