using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal sealed class PlayerSkinCatalog
{
    public int Version { get; set; }
    public string DefaultSet { get; set; } = "Elkondo";
    public Dictionary<string, Dictionary<string, string>> Sets { get; set; } = new();
    public Dictionary<string, PlayerSkinDefinition> Skins { get; set; } = new();

    public static PlayerSkinCatalog Load()
    {
        var path = ContentRoot.GetPath("PlayerSkins.json");
        using var stream = !OperatingSystem.IsBrowser() && File.Exists(path)
            ? File.OpenRead(path)
            : typeof(PlayerSkinCatalog).Assembly.GetManifestResourceStream("OpenGarrison.PlayerSkins.json")
                ?? throw new InvalidDataException("The default player skin catalog is missing.");
        return Read(stream);
    }

    internal static PlayerSkinCatalog Read(Stream stream)
    {
        var catalog = JsonSerializer.Deserialize(stream, PlayerSkinJsonContext.Default.PlayerSkinCatalog)
            ?? throw new InvalidDataException("The player skin catalog is empty.");
        catalog.Validate();
        return catalog;
    }

    public PlayerSkinDefinition? Find(string classId, PlayerTeam team, string? set = null)
    {
        if (!Sets.TryGetValue(set ?? DefaultSet, out var classes)
            || !classes.TryGetValue(classId, out var id) || !Skins.TryGetValue(id, out var skin))
            return null;
        return skin.Teams.ContainsKey(team.ToString()) ? skin : null;
    }

    private void Validate()
    {
        if (Version != 2) throw new InvalidDataException($"Unsupported player skin catalog version {Version}.");
        if (!Sets.ContainsKey(DefaultSet)) throw new InvalidDataException($"Default player sprite set '{DefaultSet}' does not exist.");
        foreach (var classes in Sets.Values)
        foreach (var (classId, id) in classes)
            if (!Skins.ContainsKey(id)) throw new InvalidDataException($"Player skin '{id}' selected for '{classId}' does not exist.");
        foreach (var (id, skin) in Skins)
        {
            void Require(bool condition, string message)
            {
                if (!condition) throw new InvalidDataException($"Player skin '{id}': {message}");
            }
            Require(skin.Poses.Length > 0, "at least one pose is required.");
            Require(!string.IsNullOrWhiteSpace(skin.BodySprite), "bodySprite is required.");
            Require(skin.Origin.Length == 2, "origin must have two coordinates.");
            Require(skin.PixelScale is >= 1 and <= 8, "pixelScale must be an integer from 1 to 8.");
            Require(float.IsFinite(skin.PixelsPerRunFrame) && skin.PixelsPerRunFrame > 0, "pixelsPerRunFrame must be positive.");
            Require(float.IsFinite(skin.EquipmentOffset), "equipmentOffset must be finite.");
            if (skin.Weapon is { } weapon)
            {
                Require(weapon.Pivot.Length == 2 && !string.IsNullOrWhiteSpace(weapon.Sprite)
                    && !string.IsNullOrWhiteSpace(weapon.ItemId), "weapon sprite, itemId and pivot are required.");
                Require(weapon.Muzzle.Length is 0 or 2, "weapon muzzle must have two coordinates when supplied.");
                Require(weapon.AttachmentOffset.Length == 2, "weapon attachmentOffset must have two coordinates.");
                Require(float.IsFinite(weapon.FirePlaybackRate) && weapon.FirePlaybackRate > 0,
                    "weapon firePlaybackRate must be positive.");
            }
            foreach (var required in new[] { "idle", "run" })
                Require(skin.Clips.ContainsKey(required), $"clip '{required}' is missing.");
            foreach (var (name, clip) in skin.Clips)
            {
                Require(clip.Frames.Length > 0 && float.IsFinite(clip.FramesPerSecond) && clip.FramesPerSecond > 0,
                    $"clip '{name}' must have frames and a positive frame rate.");
                foreach (var frame in clip.Frames)
                    Require(frame >= 0 && frame < skin.Poses.Length, $"clip '{name}' references missing pose {frame}.");
            }
            foreach (var pose in skin.Poses)
            {
                Require(pose.WeaponOffset.Length == 2, "weaponOffset must have two coordinates.");
                Require(float.IsFinite(pose.EquipmentOffset), "equipmentOffset must be finite.");
                if (pose.WeaponSkin is { } weaponSkinId)
                    Require(Skins.TryGetValue(weaponSkinId, out var weaponSkin) && weaponSkin.Weapon is not null
                        && pose.WeaponSkinPose >= 0 && pose.WeaponSkinPose < weaponSkin.Poses.Length,
                        "weaponSkin must reference an existing weapon pose.");
            }
        }
    }
}

internal sealed class PlayerSkinDefinition
{
    public string BodySprite { get; set; } = "";
    public string? CloakedBodySprite { get; set; }
    public int[] Origin { get; set; } = [];
    public int PixelScale { get; set; } = 1;
    public float PixelsPerRunFrame { get; set; } = 15;
    public float EquipmentOffset { get; set; }
    public Dictionary<string, JsonElement> Teams { get; set; } = new();
    public Dictionary<string, PlayerSkinClip> Clips { get; set; } = new();
    public PlayerSkinPose[] Poses { get; set; } = [];
    public PlayerSkinWeapon? Weapon { get; set; }
    public string SpriteForTeam(string sprite, PlayerTeam team)
    {
        var name = team.ToString();
        if (!Teams.ContainsKey(name)) throw new InvalidDataException($"Skin '{BodySprite}' has no {name} art.");
        return sprite.Replace("{team}", name, StringComparison.Ordinal);
    }
}

internal sealed class PlayerSkinClip
{
    public int[] Frames { get; set; } = [];
    public float FramesPerSecond { get; set; } = 12;
    public bool Loop { get; set; } = true;
    public float Duration => Frames.Length / FramesPerSecond;
    public int Sample(float position)
    {
        var frame = Math.Max(0, (int)MathF.Floor(position));
        return Frames[Loop ? frame % Frames.Length : Math.Min(frame, Frames.Length - 1)];
    }
}

internal sealed class PlayerSkinPose
{
    public string? WeaponSkin { get; set; }
    public int WeaponSkinPose { get; set; }
    public int[] WeaponOffset { get; set; } = [0, 0];
    public float EquipmentOffset { get; set; }
}

internal sealed class PlayerSkinWeapon
{
    public string ItemId { get; set; } = "";
    public string MatchSprite { get; set; } = "";
    public string Sprite { get; set; } = "";
    public string? FireSprite { get; set; }
    public float FirePlaybackRate { get; set; } = 1;
    public int[] AttachmentOffset { get; set; } = [0, 0];
    public string? ReloadSprite { get; set; }
    public int[] Pivot { get; set; } = [];
    public int[] Muzzle { get; set; } = [];
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(PlayerSkinCatalog))]
internal partial class PlayerSkinJsonContext : JsonSerializerContext;
