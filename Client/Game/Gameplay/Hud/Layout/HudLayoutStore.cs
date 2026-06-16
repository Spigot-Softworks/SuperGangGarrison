#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

internal static class HudLayoutStore
{
    public const string DefaultFileName = "OpenGarrison.hud.json";
    private const string UserConfigDirectoryName = "config";

    public static HudLayoutProfile Load(string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return new HudLayoutProfile();
        }

        if (path is null)
        {
            return LoadDefault(GetDefaultPath(), GetLegacyDefaultPath());
        }

        var resolvedPath = path;
        var document = JsonConfigurationFile.LoadOrCreate(resolvedPath, static () => new HudLayoutDocument());
        return document.ToProfile();
    }

    public static void Save(HudLayoutProfile profile, string? path = null)
    {
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        var resolvedPath = path ?? GetDefaultPath();
        JsonConfigurationFile.Save(resolvedPath, HudLayoutDocument.FromProfile(profile));
    }

    internal static HudLayoutProfile LoadDefault(string resolvedPath, string legacyPath)
    {
        if (!File.Exists(resolvedPath) && IsDistinctExistingFile(legacyPath, resolvedPath))
        {
            var migratedDocument = JsonConfigurationFile.LoadOrCreate(legacyPath, static () => new HudLayoutDocument());
            var migratedProfile = migratedDocument.ToProfile();
            JsonConfigurationFile.Save(resolvedPath, HudLayoutDocument.FromProfile(migratedProfile));
            return migratedProfile;
        }

        var document = JsonConfigurationFile.LoadOrCreate(resolvedPath, static () => new HudLayoutDocument());
        return document.ToProfile();
    }

    internal static string GetDefaultPath()
    {
        return RuntimePaths.GetUserDataPath(Path.Combine(UserConfigDirectoryName, DefaultFileName));
    }

    private static string GetLegacyDefaultPath()
    {
        return RuntimePaths.GetConfigPath(DefaultFileName);
    }

    private static bool IsDistinctExistingFile(string legacyPath, string resolvedPath)
    {
        if (!File.Exists(legacyPath))
        {
            return false;
        }

        return !string.Equals(
            Path.GetFullPath(legacyPath),
            Path.GetFullPath(resolvedPath),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    }
}

internal sealed class HudLayoutDocument
{
    public int Version { get; set; } = 1;

    public HudLayoutEditorDocument Editor { get; set; } = new();

    public Dictionary<string, HudElementLayoutOverride> Elements { get; set; } = new(StringComparer.Ordinal);

    public HudLayoutProfile ToProfile()
    {
        var profile = new HudLayoutProfile
        {
            GridVisible = Editor.GridVisible,
            SnapEnabled = Editor.SnapEnabled,
            MinorGridSize = Math.Max(2, Editor.MinorGridSize),
            MajorGridSize = Math.Max(2, Editor.MajorGridSize),
            HudOpacity = HudLayoutProfile.NormalizeHudOpacity(Editor.HudOpacity),
        };

        if (Version != 1)
        {
            return profile;
        }

        foreach (var (id, entry) in Elements)
        {
            if (!profile.Defaults.ContainsKey(id))
            {
                profile.UnknownOverrides[id] = entry;
                continue;
            }

            profile.Overrides[id] = entry;
        }

        return profile;
    }

    public static HudLayoutDocument FromProfile(HudLayoutProfile profile)
    {
        var elements = new Dictionary<string, HudElementLayoutOverride>(profile.UnknownOverrides, StringComparer.Ordinal);
        foreach (var (id, entry) in profile.Overrides)
        {
            elements[id] = entry;
        }

        return new HudLayoutDocument
        {
            Version = 1,
            Editor = new HudLayoutEditorDocument
            {
                GridVisible = profile.GridVisible,
                SnapEnabled = profile.SnapEnabled,
                MinorGridSize = Math.Max(2, profile.MinorGridSize),
                MajorGridSize = Math.Max(2, profile.MajorGridSize),
                HudOpacity = HudLayoutProfile.NormalizeHudOpacity(profile.HudOpacity),
            },
            Elements = elements,
        };
    }
}

internal sealed class HudLayoutEditorDocument
{
    public bool GridVisible { get; set; } = true;

    public bool SnapEnabled { get; set; } = true;

    public int MinorGridSize { get; set; } = 16;

    public int MajorGridSize { get; set; } = 64;

    public float HudOpacity { get; set; } = HudLayoutProfile.MaxHudOpacity;
}
