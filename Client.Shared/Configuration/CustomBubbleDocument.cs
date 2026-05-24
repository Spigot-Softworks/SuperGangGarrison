#nullable enable

using OpenGarrison.Core;

namespace OpenGarrison.ClientShared;

public sealed class CustomBubbleDocument
{
    public const int BubbleWidth = 72;
    public const int BubbleHeight = 58;
    public const int BytesPerPixel = 8;
    public const int SlotCount = 3;
    public const int Rgba64ByteCount = BubbleWidth * BubbleHeight * BytesPerPixel;
    private const int LegacyBubbleWidth = 32;
    private const int LegacyBubbleHeight = 32;
    private const int LegacyRgba64ByteCount = LegacyBubbleWidth * LegacyBubbleHeight * BytesPerPixel;
    public const string DefaultRelativePath = "CustomBubbles/custom-bubbles.json";

    public bool ShowCustomBubbles { get; set; } = true;

    public int SelectedSlot { get; set; }

    public List<CustomBubbleSlotDocument> Slots { get; set; } = [];

    public static CustomBubbleDocument Load(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            var browserDocument = new CustomBubbleDocument();
            browserDocument.Normalize();
            return browserDocument;
        }

        var resolvedPath = path ?? RuntimePaths.GetUserDataPath(DefaultRelativePath);
        var document = JsonConfigurationFile.LoadOrCreate(resolvedPath, static () => new CustomBubbleDocument());
        document.Normalize();
        document.Save(resolvedPath);
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
        if (string.IsNullOrWhiteSpace(slot.Rgba64Base64))
        {
            return false;
        }

        try
        {
            var decoded = Convert.FromBase64String(slot.Rgba64Base64);
            if (decoded.Length == Rgba64ByteCount)
            {
                pixels = decoded;
                revision = slot.Revision;
                return true;
            }

            if (decoded.Length == LegacyRgba64ByteCount)
            {
                pixels = UpgradeLegacyPixels(decoded);
                revision = slot.Revision;
                return true;
            }
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
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

    public void Normalize()
    {
        SelectedSlot = NormalizeSlotIndex(SelectedSlot);
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
            if (string.IsNullOrWhiteSpace(slot.Rgba64Base64))
            {
                slot.Rgba64Base64 = string.Empty;
                continue;
            }

            try
            {
                var decoded = Convert.FromBase64String(slot.Rgba64Base64);
                if (decoded.Length == Rgba64ByteCount)
                {
                    continue;
                }

                if (decoded.Length == LegacyRgba64ByteCount)
                {
                    slot.Rgba64Base64 = Convert.ToBase64String(UpgradeLegacyPixels(decoded));
                    continue;
                }
            }
            catch (FormatException)
            {
            }

            slot.Rgba64Base64 = string.Empty;
        }
    }

    public static int NormalizeSlotIndex(int slotIndex)
    {
        return Math.Clamp(slotIndex, 0, SlotCount - 1);
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
