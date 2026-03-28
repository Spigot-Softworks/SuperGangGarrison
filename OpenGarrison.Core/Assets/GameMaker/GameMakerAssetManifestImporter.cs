using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OpenGarrison.Core;

public static class GameMakerAssetManifestImporter
{
    public static GameMakerAssetManifest ImportProjectAssets()
    {
        var sourceRootFile = ContentRoot.GetPath("Constants.xml");
        // var sourceRootFile = ProjectSourceLocator.FindFile("OpenGarrison.Core/Content/Constants.xml");
        if (sourceRootFile is null)
        {
            return new GameMakerAssetManifest(
                sourceRootPath: null,
                sprites: new Dictionary<string, GameMakerSpriteAsset>(StringComparer.OrdinalIgnoreCase),
                backgrounds: new Dictionary<string, GameMakerBackgroundAsset>(StringComparer.OrdinalIgnoreCase),
                sounds: new Dictionary<string, GameMakerSoundAsset>(StringComparer.OrdinalIgnoreCase));
        }

        var sourceRootPath = Path.GetDirectoryName(sourceRootFile)!;
        var sprites = ImportSprites(ContentRoot.GetPath("Sprites"));
        // var sprites = ImportSprites(Path.Combine(sourceRootPath, "Sprites"));
        var modernAssetSpriteMetadata = ContentRoot.GetPath("Sprites", "gg2FontS.xml");
        if (modernAssetSpriteMetadata is not null)
        {
            ImportSprite(modernAssetSpriteMetadata, sprites);
        }

        return new GameMakerAssetManifest(
            sourceRootPath,
            sprites,
            ImportBackgrounds(Path.Combine(sourceRootPath, "Backgrounds")),
            ImportSounds(Path.Combine(sourceRootPath, "Sounds")));
    }

    private static Dictionary<string, GameMakerSpriteAsset> ImportSprites(string spritesRootPath)
    {
        var sprites = new Dictionary<string, GameMakerSpriteAsset>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(spritesRootPath))
        {
            return sprites;
        }

        foreach (var metadataPath in Directory.GetFiles(spritesRootPath, "*.xml", SearchOption.AllDirectories))
        {
            ImportSprite(metadataPath, sprites);
        }

        return sprites;
    }

    private static void ImportSprite(string metadataPath, Dictionary<string, GameMakerSpriteAsset> sprites)
    {
        var metadataName = Path.GetFileName(metadataPath);
        if (metadataName.StartsWith("_resources", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var document = XDocument.Load(metadataPath);
        var root = document.Root;
        if (root?.Name != "sprite")
        {
            return;
        }

        var name = Path.GetFileNameWithoutExtension(metadataPath);
        var originElement = root.Element("origin");
        var maskElement = root.Element("mask");
        var boundsElement = maskElement?.Element("bounds");
        var imagesDirectory = Path.Combine(Path.GetDirectoryName(metadataPath)!, $"{name}.images");
        var framePaths = Directory.Exists(imagesDirectory)
            ? Directory
                .GetFiles(imagesDirectory, "*.png", SearchOption.TopDirectoryOnly)
                .OrderBy(path => ExtractTrailingNumber(Path.GetFileNameWithoutExtension(path)))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();

        sprites[name] = new GameMakerSpriteAsset(
            Name: name,
            MetadataPath: metadataPath,
            FramePaths: framePaths,
            OriginX: ReadIntAttribute(originElement, "x"),
            OriginY: ReadIntAttribute(originElement, "y"),
            Preload: ReadBoolElement(root, "preload"),
            Transparent: ReadBoolElement(root, "transparent"),
            Mask: new GameMakerSpriteMask(
                Separate: ReadBoolElement(maskElement, "separate"),
                Shape: ReadStringElement(maskElement, "shape"),
                BoundsMode: ReadStringAttribute(boundsElement, "mode"),
                Left: ReadNullableIntAttribute(boundsElement, "left"),
                Top: ReadNullableIntAttribute(boundsElement, "top"),
                Right: ReadNullableIntAttribute(boundsElement, "right"),
                Bottom: ReadNullableIntAttribute(boundsElement, "bottom")));
    }

    private static Dictionary<string, GameMakerBackgroundAsset> ImportBackgrounds(string backgroundsRootPath)
    {
        var backgrounds = new Dictionary<string, GameMakerBackgroundAsset>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(backgroundsRootPath))
        {
            return backgrounds;
        }

        foreach (var metadataPath in Directory.GetFiles(backgroundsRootPath, "*.xml", SearchOption.TopDirectoryOnly))
        {
            var metadataName = Path.GetFileName(metadataPath);
            if (metadataName.StartsWith("_resources", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var document = XDocument.Load(metadataPath);
            if (document.Root?.Name != "background")
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(metadataPath);
            var imagePath = Path.Combine(Path.GetDirectoryName(metadataPath)!, $"{name}.png");
            backgrounds[name] = new GameMakerBackgroundAsset(
                Name: name,
                MetadataPath: metadataPath,
                ImagePath: imagePath,
                Preload: ReadBoolElement(document.Root, "preload"),
                Transparent: ReadBoolElement(document.Root, "transparent"));
        }

        return backgrounds;
    }

    private static Dictionary<string, GameMakerSoundAsset> ImportSounds(string soundsRootPath)
    {
        var sounds = new Dictionary<string, GameMakerSoundAsset>(StringComparer.OrdinalIgnoreCase);
        if (!Directory.Exists(soundsRootPath))
        {
            return sounds;
        }

        foreach (var metadataPath in Directory.GetFiles(soundsRootPath, "*.xml", SearchOption.TopDirectoryOnly))
        {
            var metadataName = Path.GetFileName(metadataPath);
            if (metadataName.StartsWith("_resources", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var document = XDocument.Load(metadataPath);
            var root = document.Root;
            if (root?.Name != "sound")
            {
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(metadataPath);
            var payloadFilename = ReadStringElement(root, "filename");
            var audioPath = ResolveSoundPayloadPath(metadataPath, name, payloadFilename);
            sounds[name] = new GameMakerSoundAsset(
                Name: name,
                MetadataPath: metadataPath,
                AudioPath: audioPath,
                FileType: ReadStringElement(root, "filetype"),
                Kind: ReadStringElement(root, "kind"),
                Pan: ReadFloatElement(root, "pan"),
                Volume: ReadFloatElement(root, "volume"),
                Preload: ReadBoolElement(root, "preload"));
        }

        return sounds;
    }

    private static string ResolveSoundPayloadPath(string metadataPath, string assetName, string payloadFilename)
    {
        var soundDirectory = Path.GetDirectoryName(metadataPath)!;
        if (!string.IsNullOrWhiteSpace(payloadFilename))
        {
            var declaredPath = Path.Combine(soundDirectory, payloadFilename);
            if (File.Exists(declaredPath))
            {
                return declaredPath;
            }
        }

        var extension = Path.GetExtension(payloadFilename);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            var sameNamePath = Path.Combine(soundDirectory, assetName + extension);
            if (File.Exists(sameNamePath))
            {
                return sameNamePath;
            }
        }

        return string.IsNullOrWhiteSpace(payloadFilename)
            ? string.Empty
            : Path.Combine(soundDirectory, payloadFilename);
    }

    private static int ExtractTrailingNumber(string fileNameWithoutExtension)
    {
        var trailingDigits = new string(fileNameWithoutExtension.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(trailingDigits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : int.MaxValue;
    }

    private static bool ReadBoolElement(XElement? parent, string elementName)
    {
        return bool.TryParse(parent?.Element(elementName)?.Value, out var value) && value;
    }

    private static float ReadFloatElement(XElement? parent, string elementName)
    {
        return float.TryParse(parent?.Element(elementName)?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0f;
    }

    private static string ReadStringElement(XElement? parent, string elementName)
    {
        return parent?.Element(elementName)?.Value ?? string.Empty;
    }

    private static string ReadStringAttribute(XElement? element, string attributeName)
    {
        return element?.Attribute(attributeName)?.Value ?? string.Empty;
    }

    private static int ReadIntAttribute(XElement? element, string attributeName)
    {
        return int.TryParse(element?.Attribute(attributeName)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static int? ReadNullableIntAttribute(XElement? element, string attributeName)
    {
        return int.TryParse(element?.Attribute(attributeName)?.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }
}
