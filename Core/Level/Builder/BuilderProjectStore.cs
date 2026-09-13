using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenGarrison.Core;

/// <summary>A self-contained editing draft, including incomplete maps. Never a gameplay map.</summary>
public static class BuilderProjectStore
{
    public const string Extension = ".ogmap";
    public static void Save(CustomMapBuilderDocument document, string path)
    {
        var snapshot = document.NormalizeForEditing();
        var resources = snapshot.Resources.ToDictionary(pair => pair.Key,
            pair => pair.Value with { SourcePath = string.Empty, EmbeddedBytes = CustomMapBuilderResourceCodec.GetResourceBytes(pair.Value) },
            StringComparer.OrdinalIgnoreCase);
        var envelope = new BuilderProjectEnvelope
        {
            Document = snapshot with { Resources = resources, BackgroundImagePath = string.Empty, WalkmaskImagePath = string.Empty },
            Background = ReadOptional(snapshot.BackgroundImagePath),
            Walkmask = ReadOptional(snapshot.WalkmaskImagePath),
        };
        MapFileTransaction.Write(path, temporary =>
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(envelope, BuilderProjectJsonContext.Default.BuilderProjectEnvelope));
            _ = Read(temporary);
        });
    }

    public static CustomMapBuilderDocument Load(string path, string assetDirectory)
    {
        var envelope = Read(path);
        Directory.CreateDirectory(assetDirectory);
        string Materialize(byte[] data, string label)
        {
            if (data.Length == 0) return string.Empty;
            var target = Path.Combine(assetDirectory, label + "-" + Convert.ToHexString(SHA256.HashData(data)) + ".png");
            if (!File.Exists(target)) MapFileTransaction.Write(target, temporary => File.WriteAllBytes(temporary, data));
            return target;
        }
        return (envelope.Document! with
        {
            BackgroundImagePath = Materialize(envelope.Background, "background"),
            WalkmaskImagePath = Materialize(envelope.Walkmask, "walkmask"),
        }).NormalizeForEditing();
    }

    private static byte[] ReadOptional(string path) => string.IsNullOrWhiteSpace(path) ? [] : BuilderImageValidation.ReadFile(path);
    private static BuilderProjectEnvelope Read(string path)
    {
        if (new FileInfo(path).Length > 256L * 1024 * 1024) throw new InvalidDataException("Builder project exceeds 256 MB.");
        var result = JsonSerializer.Deserialize(File.ReadAllText(path), BuilderProjectJsonContext.Default.BuilderProjectEnvelope);
        if (result?.Version != 1 || result.Document is null || result.Background is null || result.Walkmask is null)
            throw new InvalidDataException("Invalid Builder project.");
        _ = result.Document.NormalizeForEditing();
        return result;
    }
}

public sealed class BuilderProjectEnvelope
{
    public int Version { get; set; } = 1;
    public CustomMapBuilderDocument? Document { get; set; }
    public byte[] Background { get; set; } = [];
    public byte[] Walkmask { get; set; } = [];
}

[JsonSerializable(typeof(BuilderProjectEnvelope))]
internal partial class BuilderProjectJsonContext : JsonSerializerContext { }
