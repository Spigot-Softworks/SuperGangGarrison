using OpenGarrison.ClientShared;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class CustomBubbleDocumentTests
{
    [Fact]
    public void RoundTripsSettingsAndRgba64Slot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opengarrison-custom-bubbles-{Guid.NewGuid():N}.json");
        try
        {
            var pixels = CreatePixels(17);
            var document = new CustomBubbleDocument
            {
                ShowCustomBubbles = false,
                SelectedSlot = 2,
            };
            document.SetSlotPixels(1, pixels);
            document.Save(path);

            var loaded = CustomBubbleDocument.Load(path);

            Assert.False(loaded.ShowCustomBubbles);
            Assert.Equal(2, loaded.SelectedSlot);
            Assert.True(loaded.TryGetSlotPixels(1, out var loadedPixels, out var revision));
            Assert.Equal(pixels, loadedPixels);
            Assert.Equal(1u, revision);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void NormalizesSelectedSlotAndRejectsInvalidPixelPayload()
    {
        var document = new CustomBubbleDocument
        {
            SelectedSlot = 99,
            Slots =
            [
                new CustomBubbleSlotDocument { Rgba64Base64 = Convert.ToBase64String([1, 2, 3]), Revision = 5 },
            ],
        };

        document.Normalize();

        Assert.Equal(2, document.SelectedSlot);
        Assert.Equal(CustomBubbleDocument.SlotCount, document.Slots.Count);
        Assert.False(document.HasSlot(0));
    }

    [Fact]
    public void SetSlotPixelsRequiresExactRgba64Length()
    {
        var document = new CustomBubbleDocument();

        Assert.Throws<ArgumentException>(() => document.SetSlotPixels(0, new byte[CustomBubbleDocument.Rgba64ByteCount - 1]));
    }

    [Fact]
    public void CustomPaletteNormalizesAndRoundTripsRgbaHex()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opengarrison-custom-palette-{Guid.NewGuid():N}.json");
        try
        {
            var document = new CustomBubbleDocument
            {
                CustomPalette = ["not-a-color"],
            };

            document.Normalize();
            Assert.Equal(CustomBubbleDocument.PaletteColorCount, document.CustomPalette.Count);
            Assert.False(document.TryGetCustomPaletteColorHex(0, out _));

            document.SetCustomPaletteColorHex(5, "#1234ABCD");
            document.Save(path);

            var loaded = CustomBubbleDocument.Load(path);

            Assert.True(loaded.TryGetCustomPaletteColorHex(5, out var colorHex));
            Assert.Equal("1234ABCD", colorHex);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static byte[] CreatePixels(byte seed)
    {
        var pixels = new byte[CustomBubbleDocument.Rgba64ByteCount];
        for (var index = 0; index < pixels.Length; index += 1)
        {
            pixels[index] = (byte)(seed + index);
        }

        return pixels;
    }
}
