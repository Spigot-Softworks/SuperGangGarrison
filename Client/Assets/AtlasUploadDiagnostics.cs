using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class AtlasUploadDiagnostics
{
    private const string EnableEnvironmentVariable = "OPENGARRISON_VERIFY_ATLAS_UPLOADS";

    public static bool Enabled => !OperatingSystem.IsBrowser()
        && string.Equals(Environment.GetEnvironmentVariable(EnableEnvironmentVariable), "1", StringComparison.Ordinal);

    public static void VerifyUploadedPage(
        GraphicsDevice graphicsDevice,
        string relativePath,
        byte[] sourceBytes,
        Color[] uploadedPixels,
        Texture2D texture)
    {
        if (!Enabled)
        {
            return;
        }

        try
        {
            var readbackPixels = new Color[uploadedPixels.Length];
            texture.GetData(readbackPixels);

            var mismatchCount = 0;
            var firstMismatch = -1;
            for (var index = 0; index < uploadedPixels.Length; index += 1)
            {
                if (uploadedPixels[index] == readbackPixels[index])
                {
                    continue;
                }

                mismatchCount += 1;
                if (firstMismatch < 0)
                {
                    firstMismatch = index;
                }
            }

            var adapter = graphicsDevice.Adapter;
            var line = FormattableString.Invariant(
                $"timestamp={DateTimeOffset.Now:O} path={relativePath} size={texture.Width}x{texture.Height} adapter=\"{adapter.Description}\" profile={graphicsDevice.GraphicsProfile} pngSha256={Hash(sourceBytes)} setDataSha256={HashPixels(uploadedPixels)} readbackSha256={HashPixels(readbackPixels)} mismatches={mismatchCount} firstMismatch={firstMismatch}");
            AppendLine(line);
        }
        catch (Exception exception)
        {
            AppendLine(FormattableString.Invariant(
                $"timestamp={DateTimeOffset.Now:O} path={relativePath} verificationError={exception.GetType().Name}: {exception.Message}"));
        }
    }

    private static string Hash(byte[] bytes)
        => Convert.ToHexString(SHA256.HashData(bytes));

    private static string HashPixels(Color[] pixels)
        => Convert.ToHexString(SHA256.HashData(MemoryMarshal.AsBytes(pixels.AsSpan())));

    private static void AppendLine(string line)
    {
        try
        {
            File.AppendAllText(RuntimePaths.GetLogPath("client-atlas-diagnostics.log"), line + Environment.NewLine);
        }
        catch
        {
        }
    }
}
