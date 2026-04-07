using System.Globalization;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MoonSharp.Interpreter;
using OpenGarrison.Client.Plugins;
using OpenGarrison.PluginHost;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImageSharpRectangle = SixLabors.ImageSharp.Rectangle;

namespace OpenGarrison.Client;

internal sealed class LuaClientPlugin(
    OpenGarrisonPluginManifest manifest,
    string pluginDirectory) : IOpenGarrisonClientPlugin,
    IOpenGarrisonClientLifecycleHooks,
    IOpenGarrisonClientUpdateHooks,
    IOpenGarrisonClientHudHooks,
    IOpenGarrisonClientScoreboardHooks,
    IOpenGarrisonClientSoundHooks,
    IOpenGarrisonClientDamageHooks,
    IOpenGarrisonClientCameraHooks,
    IOpenGarrisonClientDeadBodyHooks,
    IOpenGarrisonClientBubbleMenuHooks,
    IOpenGarrisonClientMainMenuHooks,
    IOpenGarrisonClientOptionsHooks
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    private static readonly BindingFlags PublicInstanceProperties = BindingFlags.Instance | BindingFlags.Public;
    private static readonly BindingFlags PublicInstanceFields = BindingFlags.Instance | BindingFlags.Public;

    private Script? _script;
    private Table? _pluginTable;
    private IOpenGarrisonClientPluginContext? _context;
    private readonly Dictionary<string, SoundEffect> _registeredSounds = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Texture2D> _registeredTextures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClientPluginTextureAtlas> _registeredTextureAtlases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ClientPluginTextureRegion> _registeredTextureRegions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OwnedTextureSequence> _ownedTextureSequences = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<DynValue> _pendingLocalDamageEvents = [];
    private LuaCallbackPhase _currentCallbackPhase = LuaCallbackPhase.None;
    private bool _callbacksDisabled;
    private const long CallbackAutoYieldCounter = 1000;
    private const int MaxCallbackResumeCount = 4096;

    public string Id => manifest.Id;

    public string DisplayName => manifest.DisplayName;

    public Version Version { get; } = Version.TryParse(manifest.Version, out var version)
        ? version
        : new Version(1, 0, 0, 0);

    public void Initialize(IOpenGarrisonClientPluginContext context)
    {
        try
        {
            _context = context;
            var entryPointPath = Path.GetFullPath(Path.Combine(pluginDirectory, manifest.EntryPoint));
            if (!File.Exists(entryPointPath))
            {
                throw new FileNotFoundException($"Lua entry point was not found: {entryPointPath}", entryPointPath);
            }

            _script = new Script(CoreModules.Preset_SoftSandbox);
            _script.Options.DebugPrint = message => context.Log($"[lua] {message}");

            var hostTable = CreateHostTable(_script, context);
            var result = _script.DoFile(entryPointPath);
            _pluginTable = ResolvePluginTable(_script, result);
            ExecuteInPhase(
                LuaCallbackPhase.Initialize,
                () => CallIfPresent("initialize", rethrowOnFailure: true, DynValue.NewTable(hostTable)));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Lua plugin \"{manifest.Id}\" failed to initialize from \"{GetManifestPath()}\": {ex.Message}", ex);
        }
    }

    public void Shutdown()
    {
        ExecuteInPhase(LuaCallbackPhase.Shutdown, () => CallIfPresent("shutdown"));
        foreach (var textureSequence in _ownedTextureSequences.Values)
        {
            textureSequence.Dispose();
        }

        _ownedTextureSequences.Clear();
        _registeredSounds.Clear();
        _registeredTextures.Clear();
        _registeredTextureAtlases.Clear();
        _registeredTextureRegions.Clear();
        _pendingLocalDamageEvents.Clear();
        _pluginTable = null;
        _script = null;
        _context = null;
        _callbacksDisabled = false;
    }

    public void OnClientStarting() => ExecuteInPhase(LuaCallbackPhase.Lifecycle, () => CallIfPresent("on_client_starting"));

    public void OnClientStarted() => ExecuteInPhase(LuaCallbackPhase.Lifecycle, () => CallIfPresent("on_client_started"));

    public void OnClientStopping() => ExecuteInPhase(LuaCallbackPhase.Lifecycle, () => CallIfPresent("on_client_stopping"));

    public void OnClientStopped() => ExecuteInPhase(LuaCallbackPhase.Lifecycle, () => CallIfPresent("on_client_stopped"));

    public void OnClientFrame(ClientFrameEvent e)
    {
        ExecuteInPhase(LuaCallbackPhase.Update, () =>
        {
            FlushPendingLocalDamageEvents();
            CallIfPresent("on_client_frame", ToDynValue(e));
        });
    }

    public void OnWorldSound(ClientWorldSoundEvent e) => ExecuteInPhase(LuaCallbackPhase.Update, () => CallIfPresent("on_world_sound", ToDynValue(e)));

    public void OnLocalDamage(LocalDamageEvent e)
    {
        if (_script is null || _pluginTable is null)
        {
            return;
        }

        _pendingLocalDamageEvents.Add(ToDynValue(e));
    }

    public Vector2 GetCameraOffset()
    {
        if (_script is null || _pluginTable is null)
        {
            return Vector2.Zero;
        }

        return ExecuteInPhase(LuaCallbackPhase.Query, () =>
        {
            if (!TryInvokeCallback("get_camera_offset", out var result))
            {
                return Vector2.Zero;
            }

            return result.Type == DataType.Table
                ? ReadVector2FromTable(result.Table)
                : Vector2.Zero;
        });
    }

    public void OnGameplayHudDraw(IOpenGarrisonClientHudCanvas canvas)
    {
        if (_script is null || _pluginTable is null)
        {
            return;
        }

        ExecuteInPhase(
            LuaCallbackPhase.GameplayHudDraw,
            () => CallIfPresent("on_gameplay_hud_draw", DynValue.NewTable(CreateHudCanvasTable(_script, canvas, rightAlignedText: false))));
    }

    public ClientScoreboardPanelLocation ScoreboardPanelLocation => ExecuteInPhase(LuaCallbackPhase.ScoreboardQuery, ReadScoreboardLocation);

    public int ScoreboardPanelOrder => ExecuteInPhase(LuaCallbackPhase.ScoreboardQuery, ReadScoreboardOrder);

    public void OnScoreboardDraw(IOpenGarrisonClientScoreboardCanvas canvas, ClientScoreboardRenderState state)
    {
        if (_script is null || _pluginTable is null)
        {
            return;
        }

        ExecuteInPhase(
            LuaCallbackPhase.ScoreboardDraw,
            () => CallIfPresent("on_scoreboard_draw", DynValue.NewTable(CreateHudCanvasTable(_script, canvas, rightAlignedText: true)), ToDynValue(state)));
    }

    public ClientPluginMainMenuBackgroundOverride? GetMainMenuBackgroundOverride()
    {
        if (_script is null || _pluginTable is null)
        {
            return null;
        }

        return ExecuteInPhase(LuaCallbackPhase.Query, () =>
        {
            if (!TryInvokeCallback("get_main_menu_background_override", out var result) || result.Type != DataType.Table)
            {
                return null;
            }

            var imagePath = GetStringField(result.Table, "imagePath", "image_path");
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            var attributionText = GetStringField(result.Table, "attributionText", "attribution_text") ?? string.Empty;
            return new ClientPluginMainMenuBackgroundOverride(imagePath, attributionText);
        });
    }

    public bool TryDrawDeadBody(IOpenGarrisonClientHudCanvas canvas, ClientDeadBodyRenderState deadBody)
    {
        if (_script is null || _pluginTable is null || _callbacksDisabled)
        {
            return false;
        }

        return ExecuteInPhase(LuaCallbackPhase.DeadBodyDraw, () =>
        {
            if (!TryInvokeCallback("try_draw_dead_body", out var result, DynValue.NewTable(CreateHudCanvasTable(_script, canvas, rightAlignedText: false)), ToDynValue(deadBody)))
            {
                return false;
            }

            return result.CastToBool();
        });
    }

    public ClientBubbleMenuUpdateResult? TryHandleBubbleMenuInput(ClientBubbleMenuInputState inputState)
    {
        if (_script is null || _pluginTable is null)
        {
            return null;
        }

        return ExecuteInPhase(LuaCallbackPhase.BubbleMenuInput, () =>
        {
            if (!TryInvokeCallback("try_handle_bubble_menu_input", out var result, ToDynValue(inputState)) || result.Type != DataType.Table)
            {
                return null;
            }

            return new ClientBubbleMenuUpdateResult(
                ReadNullableIntFromTable(result.Table, "bubbleFrame", "bubble_frame"),
                ReadNullableIntFromTable(result.Table, "newXPageIndex", "new_x_page_index"),
                ReadBoolFromTable(result.Table, "closeMenu", "close_menu"),
                ReadBoolFromTable(result.Table, "clearBubbleSelection", "clear_bubble_selection"));
        });
    }

    public bool TryDrawBubbleMenu(IOpenGarrisonClientHudCanvas canvas, ClientBubbleMenuRenderState renderState)
    {
        if (_script is null || _pluginTable is null)
        {
            return false;
        }

        return ExecuteInPhase(LuaCallbackPhase.BubbleMenuDraw, () =>
        {
            if (!TryInvokeCallback("try_draw_bubble_menu", out var result, DynValue.NewTable(CreateHudCanvasTable(_script, canvas, rightAlignedText: false)), ToDynValue(renderState)))
            {
                return false;
            }

            return result.CastToBool();
        });
    }

    public IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections()
    {
        if (_script is null || _pluginTable is null)
        {
            return Array.Empty<ClientPluginOptionsSection>();
        }

        return ExecuteInPhase<IReadOnlyList<ClientPluginOptionsSection>>(LuaCallbackPhase.OptionsQuery, () =>
        {
            if (!TryInvokeCallback("get_options_sections", out var result) || result.Type != DataType.Table)
            {
                return Array.Empty<ClientPluginOptionsSection>();
            }

            return ConvertOptionSections(result.Table);
        });
    }

    private void CallIfPresent(string functionName, params DynValue[] args)
    {
        CallIfPresent(functionName, rethrowOnFailure: false, args);
    }

    private void FlushPendingLocalDamageEvents()
    {
        if (_pendingLocalDamageEvents.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _pendingLocalDamageEvents.Count; index += 1)
        {
            CallIfPresent("on_local_damage", _pendingLocalDamageEvents[index]);
        }

        _pendingLocalDamageEvents.Clear();
    }

    private void CallIfPresent(string functionName, bool rethrowOnFailure, params DynValue[] args)
    {
        if (!TryInvokeCallback(functionName, out _, rethrowOnFailure, args))
        {
            return;
        }
    }

    private bool TryInvokeCallback(string callbackName, out DynValue result, params DynValue[] args)
    {
        return TryInvokeCallback(callbackName, out result, rethrowOnFailure: false, args);
    }

    private bool TryInvokeCallback(string callbackName, out DynValue result, bool rethrowOnFailure, params DynValue[] args)
    {
        result = DynValue.Nil;
        if (_script is null || _pluginTable is null || _callbacksDisabled)
        {
            return false;
        }

        var function = _pluginTable.Get(callbackName);
        if (function.Type is not (DataType.Function or DataType.ClrFunction))
        {
            return false;
        }

        try
        {
            result = InvokeCallbackWithLimits(function, args);
            return true;
        }
        catch (Exception ex)
        {
            DisableCallbacks($"{callbackName} failed during {DescribePhase(_currentCallbackPhase)}: {ex.Message}");
            LogCallbackFailure(callbackName, ex);
            if (rethrowOnFailure)
            {
                throw;
            }

            return false;
        }
    }

    private DynValue InvokeCallbackWithLimits(DynValue function, DynValue[] args)
    {
        if (_script is null)
        {
            return DynValue.Nil;
        }

        var coroutine = _script.CreateCoroutine(function).Coroutine;
        coroutine.AutoYieldCounter = CallbackAutoYieldCounter;

        var stopwatch = Stopwatch.StartNew();
        var maxDuration = GetMaxCallbackDuration(_currentCallbackPhase);
        var resumeCount = 0;
        var firstResume = true;
        while (true)
        {
            var result = firstResume ? coroutine.Resume(args) : coroutine.Resume();
            firstResume = false;
            resumeCount += 1;

            if (coroutine.State == CoroutineState.Dead)
            {
                return result;
            }

            if (resumeCount >= MaxCallbackResumeCount)
            {
                throw new TimeoutException($"Lua callback exceeded the resume budget of {MaxCallbackResumeCount} slices.");
            }

            if (stopwatch.Elapsed > maxDuration)
            {
                throw new TimeoutException($"Lua callback exceeded the {maxDuration.TotalMilliseconds:0.##}ms budget.");
            }
        }
    }

    private Table CreateHostTable(Script script, IOpenGarrisonClientPluginContext context)
    {
        var host = new Table(script)
        {
            ["plugin_id"] = context.PluginId,
            ["plugin_directory"] = context.PluginDirectory,
            ["config_directory"] = context.ConfigDirectory,
        };

        host["log"] = DynValue.NewCallback((_, args) =>
        {
            context.Log(ReadStringArgument(args, 0));
            return DynValue.Nil;
        });
        host["random_int"] = DynValue.NewCallback((_, args) =>
            DynValue.NewNumber(Random.Shared.Next(ReadIntArgument(args, 0))));
        host["load_json_config"] = DynValue.NewCallback((_, args) =>
        {
            var relativePath = ReadStringArgument(args, 0);
            var defaultValue = ReadArgument(args, 1);
            var path = ResolveConfigPath(context.ConfigDirectory, relativePath);
            if (!File.Exists(path))
            {
                SaveLuaTableJson(path, defaultValue);
                return defaultValue;
            }

            var json = File.ReadAllText(path);
            var value = JsonSerializer.Deserialize<JsonElement>(json, JsonOptions);
            return ToDynValue(value);
        });
        host["save_json_config"] = DynValue.NewCallback((_, args) =>
        {
            var relativePath = ReadStringArgument(args, 0);
            var value = ReadArgument(args, 1);
            var path = ResolveConfigPath(context.ConfigDirectory, relativePath);
            SaveLuaTableJson(path, value);
            return DynValue.True;
        });
        host["get_manifest"] = DynValue.NewCallback((_, _) => ToDynValue(context.Manifest));
        host["get_host_api"] = DynValue.NewCallback((_, _) => ToDynValue(context.HostApi));
        host["get_client_state"] = DynValue.NewCallback((_, _) => ToDynValue(CreateClientStateSnapshot(context.ClientState)));
        host["try_get_player_world_position"] = DynValue.NewCallback((_, args) =>
        {
            var playerId = ReadIntArgument(args, 0);
            return context.ClientState.TryGetPlayerWorldPosition(playerId, out var position)
                ? DynValue.NewTable(CreateVectorTable(script, position.X, position.Y))
                : DynValue.Nil;
        });
        host["is_player_visible"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.ClientState.IsPlayerVisibleToLocalViewer(ReadIntArgument(args, 0))));
        host["is_player_cloaked"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.ClientState.IsPlayerCloaked(ReadIntArgument(args, 0))));
        host["vec2"] = DynValue.NewCallback((_, args) =>
            DynValue.NewTable(CreateVectorTable(script, ReadOptionalFloatArgument(args, 0, 0f), ReadOptionalFloatArgument(args, 1, 0f))));
        host["color"] = DynValue.NewCallback((_, args) =>
            DynValue.NewTable(CreateColorTable(
                script,
                ReadOptionalFloatArgument(args, 0, 255f),
                ReadOptionalFloatArgument(args, 1, 255f),
                ReadOptionalFloatArgument(args, 2, 255f),
                ReadOptionalFloatArgument(args, 3, 255f))));
        host["random_float"] = DynValue.NewCallback((_, _) => DynValue.NewNumber(Random.Shared.NextSingle()));
        host["register_sound_asset"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            var relativePath = ReadStringArgument(args, 1);
            context.Assets.RegisterSoundAsset(assetId, relativePath);
            if (!context.Assets.TryGetSoundAsset(assetId, out var sound))
            {
                return DynValue.False;
            }

            _registeredSounds[assetId] = sound;
            return DynValue.True;
        });
        host["register_texture_asset"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            var relativePath = ReadStringArgument(args, 1);
            if (!CanRegisterTextureAsset("register_texture_asset", assetId))
            {
                return DynValue.False;
            }

            context.Assets.RegisterTextureAsset(assetId, relativePath);
            if (!context.Assets.TryGetTextureAsset(assetId, out var texture))
            {
                return DynValue.False;
            }

            _registeredTextures[assetId] = texture;
            return DynValue.True;
        });
        host["register_texture_atlas_asset"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            var relativePath = ReadStringArgument(args, 1);
            var frameWidth = ReadIntArgument(args, 2);
            var frameHeight = ReadIntArgument(args, 3);
            if (!CanRegisterTextureAsset("register_texture_atlas_asset", assetId))
            {
                return DynValue.False;
            }

            context.Assets.RegisterTextureAtlasAsset(assetId, relativePath, frameWidth, frameHeight);
            if (!context.Assets.TryGetTextureAtlasAsset(assetId, out var atlas))
            {
                return DynValue.False;
            }

            _registeredTextureAtlases[assetId] = atlas;
            return DynValue.True;
        });
        host["register_texture_region_asset"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            var textureAssetId = ReadStringArgument(args, 1);
            var rectangle = new Rectangle(ReadIntArgument(args, 2), ReadIntArgument(args, 3), ReadIntArgument(args, 4), ReadIntArgument(args, 5));
            if (!CanRegisterTextureAsset("register_texture_region_asset", assetId))
            {
                return DynValue.False;
            }

            context.Assets.RegisterTextureRegionAsset(assetId, textureAssetId, rectangle);
            if (!context.Assets.TryGetTextureRegionAsset(assetId, out var region))
            {
                return DynValue.False;
            }

            _registeredTextureRegions[assetId] = region;
            return DynValue.True;
        });
        host["register_legacy_animation_asset"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            var relativePath = ReadStringArgument(args, 1);
            var frameCount = ReadOptionalIntArgument(args, 2, 1);
            var applyChromaKey = ReadOptionalBoolArgument(args, 3, true);
            if (!CanRegisterTextureAsset("register_legacy_animation_asset", assetId))
            {
                return DynValue.False;
            }

            var absolutePath = ResolvePluginPath(context.PluginDirectory, relativePath);
            if (!TryLoadTextureSequence(context.GraphicsDevice, absolutePath, frameCount, applyChromaKey, out var textureSequence))
            {
                return DynValue.False;
            }

            if (_ownedTextureSequences.Remove(assetId, out var existingSequence))
            {
                existingSequence.Dispose();
            }

            _ownedTextureSequences[assetId] = textureSequence;
            return DynValue.NewNumber(textureSequence.Frames.Count);
        });
        host["play_sound"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            if (!_registeredSounds.TryGetValue(assetId, out var sound)
                && (!context.Assets.TryGetSoundAsset(assetId, out sound) || sound is null))
            {
                return DynValue.False;
            }

            _registeredSounds[assetId] = sound;
            var volume = ReadOptionalFloatArgument(args, 1, 1f);
            var pitch = ReadOptionalFloatArgument(args, 2, 0f);
            var pan = ReadOptionalFloatArgument(args, 3, 0f);
            return DynValue.NewBoolean(sound.Play(volume, pitch, pan));
        });
        host["register_menu_entry"] = DynValue.NewCallback((_, args) =>
        {
            var menuEntryId = ReadStringArgument(args, 0);
            var label = ReadStringArgument(args, 1);
            var locationText = ReadStringArgument(args, 2);
            var callbackName = ReadStringArgument(args, 3);
            var order = ReadOptionalIntArgument(args, 4, 0);
            if (!Enum.TryParse<ClientPluginMenuLocation>(locationText, ignoreCase: true, out var location))
            {
                throw new InvalidOperationException($"Unknown client menu location \"{locationText}\".");
            }

            context.Ui.RegisterMenuEntry(menuEntryId, label, location, () => InvokeMenuCallback(callbackName), order);
            return DynValue.True;
        });
        host["show_notice"] = DynValue.NewCallback((_, args) =>
        {
            context.Ui.ShowNotice(
                ReadStringArgument(args, 0),
                ReadOptionalIntArgument(args, 1, 200),
                ReadOptionalBoolArgument(args, 2, true));
            return DynValue.True;
        });
        host["register_hotkey"] = DynValue.NewCallback((_, args) =>
        {
            var hotkeyId = ReadStringArgument(args, 0);
            var displayName = ReadStringArgument(args, 1);
            var defaultKeyText = ReadStringArgument(args, 2);
            if (!TryParseKey(defaultKeyText, out var defaultKey))
            {
                defaultKey = Keys.None;
            }

            var registeredKey = context.Hotkeys.RegisterHotkey(hotkeyId, displayName, defaultKey);
            return DynValue.NewString(registeredKey.ToString());
        });
        host["was_hotkey_pressed"] = DynValue.NewCallback((_, args) =>
            DynValue.NewBoolean(context.Hotkeys.WasHotkeyPressed(ReadStringArgument(args, 0))));
        host["format_key_display_name"] = DynValue.NewCallback((_, args) =>
            DynValue.NewString(FormatKeyDisplayName(ReadStringArgument(args, 0))));
        host["list_files"] = DynValue.NewCallback((_, args) =>
        {
            var relativeDirectory = ReadStringArgument(args, 0);
            var searchPattern = ReadOptionalStringArgument(args, 1, "*");
            var directoryPath = ResolvePluginPath(context.PluginDirectory, relativeDirectory, defaultRelativePath: ".");
            if (!Directory.Exists(directoryPath))
            {
                return DynValue.NewTable(new Table(script));
            }

            var files = Directory.EnumerateFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return ToDynValue(files);
        });

        return host;
    }

    private Table CreateHudCanvasTable(Script script, IOpenGarrisonClientHudCanvas canvas, bool rightAlignedText)
    {
        var table = new Table(script)
        {
            ["viewport_width"] = canvas.ViewportWidth,
            ["viewport_height"] = canvas.ViewportHeight,
        };

        table["world_to_screen"] = DynValue.NewCallback((_, args) =>
        {
            var worldPosition = ReadVector2Argument(args, 0, 1);
            var screenPosition = canvas.WorldToScreen(worldPosition);
            return DynValue.NewTable(CreateVectorTable(script, SanitizeFiniteFloat(screenPosition.X, 0f), SanitizeFiniteFloat(screenPosition.Y, 0f)));
        });
        table["measure_bitmap_text_width"] = DynValue.NewCallback((_, args) =>
            DynValue.NewNumber(canvas.MeasureBitmapTextWidth(ReadStringArgument(args, 0), ClampPositiveFiniteFloat(ReadOptionalFloatArgument(args, 1, 1f), 1f))));
        table["measure_bitmap_text_height"] = DynValue.NewCallback((_, args) =>
            DynValue.NewNumber(canvas.MeasureBitmapTextHeight(ClampPositiveFiniteFloat(ReadOptionalFloatArgument(args, 0, 1f), 1f))));
        table["fill_screen_rectangle"] = DynValue.NewCallback((_, args) =>
        {
            canvas.FillScreenRectangle(ReadRectangleArgument(args, 0), ReadColorArgument(args, 4));
            return DynValue.True;
        });
        table["draw_screen_rectangle_outline"] = DynValue.NewCallback((_, args) =>
        {
            canvas.DrawScreenRectangleOutline(ReadRectangleArgument(args, 0), ReadColorArgument(args, 4), ReadOptionalIntArgument(args, 5, 1));
            return DynValue.True;
        });
        table["draw_bitmap_text"] = DynValue.NewCallback((_, args) =>
        {
            canvas.DrawBitmapText(
                ReadStringArgument(args, 0),
                ReadVector2Argument(args, 1, 2),
                ReadColorArgument(args, 3),
                ClampPositiveFiniteFloat(ReadOptionalFloatArgument(args, 4, 1f), 1f));
            return DynValue.True;
        });
        table["draw_bitmap_text_centered"] = DynValue.NewCallback((_, args) =>
        {
            canvas.DrawBitmapTextCentered(
                ReadStringArgument(args, 0),
                ReadVector2Argument(args, 1, 2),
                ReadColorArgument(args, 3),
                ClampPositiveFiniteFloat(ReadOptionalFloatArgument(args, 4, 1f), 1f));
            return DynValue.True;
        });
        table["draw_bitmap_text_world"] = DynValue.NewCallback((_, args) =>
        {
            var worldPosition = ReadVector2Argument(args, 1, 2);
            var position = canvas.WorldToScreen(worldPosition);
            canvas.DrawBitmapText(
                ReadStringArgument(args, 0),
                position,
                ReadColorArgument(args, 3),
                ClampPositiveFiniteFloat(ReadOptionalFloatArgument(args, 4, 1f), 1f));
            return DynValue.True;
        });
        table["draw_bitmap_text_centered_world"] = DynValue.NewCallback((_, args) =>
        {
            var worldPosition = ReadVector2Argument(args, 1, 2);
            var position = canvas.WorldToScreen(worldPosition);
            canvas.DrawBitmapTextCentered(
                ReadStringArgument(args, 0),
                position,
                ReadColorArgument(args, 3),
                ClampPositiveFiniteFloat(ReadOptionalFloatArgument(args, 4, 1f), 1f));
            return DynValue.True;
        });
        table["draw_screen_texture"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            if (!TryResolveTexture(assetId, out var texture))
            {
                return DynValue.False;
            }

            canvas.DrawScreenTexture(
                texture,
                ReadVector2Argument(args, 1, 2),
                ReadColorArgument(args, 3),
                ReadVector2Argument(args, 4, 5),
                null,
                ReadOptionalFloatArgument(args, 8, 0f),
                ReadOptionalOriginArgument(args, 6, 7));
            return DynValue.True;
        });
        table["draw_level_background"] = DynValue.NewCallback((_, args) =>
        {
            if (!canvas.TryGetLevelBackgroundTexture(out var texture))
            {
                return DynValue.False;
            }

            var destination = ReadRectangleArgument(args, 0);
            var sourceRectangle = new Rectangle(ReadIntArgument(args, 4), ReadIntArgument(args, 5), ReadIntArgument(args, 6), ReadIntArgument(args, 7));
            var scale = new Vector2(
                destination.Width / (float)Math.Max(1, sourceRectangle.Width),
                destination.Height / (float)Math.Max(1, sourceRectangle.Height));
            canvas.DrawScreenTexture(
                texture,
                new Vector2(destination.X, destination.Y),
                ReadColorArgument(args, 8),
                scale,
                sourceRectangle);
            return DynValue.True;
        });
        table["draw_level_background_view"] = DynValue.NewCallback((_, args) =>
        {
            if (!canvas.TryGetLevelBackgroundTexture(out var texture))
            {
                return DynValue.False;
            }

            var destination = ReadRectangleArgument(args, 0);
            var viewLeft = ReadOptionalFloatArgument(args, 4, 0f);
            var viewTop = ReadOptionalFloatArgument(args, 5, 0f);
            var visibleWorldWidth = ReadOptionalFloatArgument(args, 6, 1f);
            var visibleWorldHeight = ReadOptionalFloatArgument(args, 7, 1f);
            var levelWidth = Math.Max(1f, ReadOptionalFloatArgument(args, 8, 1f));
            var levelHeight = Math.Max(1f, ReadOptionalFloatArgument(args, 9, 1f));
            var sourceRectangle = CalculateTextureSourceRectangle(texture, viewLeft, viewTop, visibleWorldWidth, visibleWorldHeight, levelWidth, levelHeight);
            var scale = new Vector2(
                destination.Width / (float)Math.Max(1, sourceRectangle.Width),
                destination.Height / (float)Math.Max(1, sourceRectangle.Height));
            canvas.DrawScreenTexture(
                texture,
                new Vector2(destination.X, destination.Y),
                ReadColorArgument(args, 10),
                scale,
                sourceRectangle);
            return DynValue.True;
        });
        table["draw_screen_sprite"] = DynValue.NewCallback((_, args) =>
        {
            return DynValue.NewBoolean(canvas.TryDrawScreenSprite(
                ReadStringArgument(args, 0),
                ReadIntArgument(args, 1),
                ReadVector2Argument(args, 2, 3),
                ReadColorArgument(args, 4),
                ReadVector2Argument(args, 5, 6)));
        });
        table["draw_world_texture"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            if (!TryResolveTexture(assetId, out var texture))
            {
                return DynValue.False;
            }

            canvas.DrawWorldTexture(
                texture,
                ReadVector2Argument(args, 1, 2),
                ReadColorArgument(args, 3),
                ReadVector2Argument(args, 4, 5),
                null,
                ReadOptionalFloatArgument(args, 8, 0f),
                ReadOptionalOriginArgument(args, 6, 7));
            return DynValue.True;
        });
        table["draw_screen_texture_region"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            if (!TryResolveTextureRegion(assetId, out var region))
            {
                return DynValue.False;
            }

            canvas.DrawScreenTexture(
                region.Texture,
                ReadVector2Argument(args, 1, 2),
                ReadColorArgument(args, 3),
                ReadVector2Argument(args, 4, 5),
                region.SourceRectangle,
                ReadOptionalFloatArgument(args, 8, 0f),
                ReadOptionalOriginArgument(args, 6, 7));
            return DynValue.True;
        });
        table["draw_world_texture_region"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            if (!TryResolveTextureRegion(assetId, out var region))
            {
                return DynValue.False;
            }

            canvas.DrawWorldTexture(
                region.Texture,
                ReadVector2Argument(args, 1, 2),
                ReadColorArgument(args, 3),
                ReadVector2Argument(args, 4, 5),
                region.SourceRectangle,
                ReadOptionalFloatArgument(args, 8, 0f),
                ReadOptionalOriginArgument(args, 6, 7));
            return DynValue.True;
        });
        table["draw_screen_texture_atlas_frame"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            if (!TryResolveTextureAtlas(assetId, out var atlas))
            {
                return DynValue.False;
            }

            var frameIndex = ReadIntArgument(args, 1);
            if (!atlas.TryGetFrameSourceRectangle(frameIndex, out var sourceRectangle))
            {
                return DynValue.False;
            }

            canvas.DrawScreenTexture(
                atlas.Texture,
                ReadVector2Argument(args, 2, 3),
                ReadColorArgument(args, 4),
                ReadVector2Argument(args, 5, 6),
                sourceRectangle,
                ReadOptionalFloatArgument(args, 9, 0f),
                ReadOptionalOriginArgument(args, 7, 8));
            return DynValue.True;
        });
        table["draw_world_texture_atlas_frame"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            if (!TryResolveTextureAtlas(assetId, out var atlas))
            {
                return DynValue.False;
            }

            var frameIndex = ReadIntArgument(args, 1);
            if (!atlas.TryGetFrameSourceRectangle(frameIndex, out var sourceRectangle))
            {
                return DynValue.False;
            }

            canvas.DrawWorldTexture(
                atlas.Texture,
                ReadVector2Argument(args, 2, 3),
                ReadColorArgument(args, 4),
                ReadVector2Argument(args, 5, 6),
                sourceRectangle,
                ReadOptionalFloatArgument(args, 9, 0f),
                ReadOptionalOriginArgument(args, 7, 8));
            return DynValue.True;
        });
        table["draw_world_animation_frame"] = DynValue.NewCallback((_, args) =>
        {
            var assetId = ReadStringArgument(args, 0);
            if (!TryResolveAnimationTexture(assetId, ReadIntArgument(args, 1), out var texture))
            {
                return DynValue.False;
            }

            canvas.DrawWorldTexture(
                texture,
                ReadVector2Argument(args, 2, 3),
                ReadColorArgument(args, 4),
                ReadVector2Argument(args, 5, 6),
                null,
                ReadOptionalFloatArgument(args, 9, 0f),
                ReadOptionalOriginArgument(args, 7, 8));
            return DynValue.True;
        });
        if (rightAlignedText && canvas is IOpenGarrisonClientScoreboardCanvas scoreboardCanvas)
        {
            table["draw_bitmap_text_right_aligned"] = DynValue.NewCallback((_, args) =>
            {
                scoreboardCanvas.DrawBitmapTextRightAligned(
                    ReadStringArgument(args, 0),
                    ReadVector2Argument(args, 1, 2),
                    ReadColorArgument(args, 3),
                    ReadOptionalFloatArgument(args, 4, 1f));
                return DynValue.True;
            });
        }

        return table;
    }

    private List<ClientPluginOptionsSection> ConvertOptionSections(Table table)
    {
        var sections = new List<ClientPluginOptionsSection>();
        foreach (var pair in table.Pairs.Where(pair => pair.Value.Type == DataType.Table))
        {
            var sectionTable = pair.Value.Table;
            var title = GetStringField(sectionTable, "title") ?? DisplayName;
            var itemsField = sectionTable.Get("items");
            if (itemsField.Type != DataType.Table)
            {
                continue;
            }

            var items = new List<ClientPluginOptionItem>();
            foreach (var itemPair in itemsField.Table.Pairs.Where(itemPair => itemPair.Value.Type == DataType.Table))
            {
                var itemTable = itemPair.Value.Table;
                var item = TryCreateOptionItem(itemTable);
                if (item is not null)
                {
                    items.Add(item);
                }
            }

            sections.Add(new ClientPluginOptionsSection(title, items));
        }

        return sections;
    }

    private ClientPluginOptionItem? TryCreateOptionItem(Table itemTable)
    {
        var label = GetStringField(itemTable, "label");
        var kind = GetStringField(itemTable, "kind");
        if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(kind))
        {
            return null;
        }

        return kind.ToLowerInvariant() switch
        {
            "boolean" => new LuaBooleanOptionItem(label, itemTable, InvokeOptionValueLabel, InvokeOptionBooleanToggle),
            "integer" => new LuaIntegerOptionItem(label, itemTable, InvokeOptionValueLabel, InvokeOptionIntegerAdvance),
            "choice" => new LuaChoiceOptionItem(label, itemTable, InvokeOptionValueLabel, InvokeOptionChoiceAdvance),
            "key" => new ClientPluginKeyOptionItem(label, () => ReadOptionKeyValue(itemTable), key => InvokeOptionKeySetter(itemTable, key), _ => InvokeOptionValueLabel(itemTable)),
            _ => null,
        };
    }

    private string InvokeOptionValueLabel(Table itemTable)
    {
        if (_script is null)
        {
            return string.Empty;
        }

        var callbackName = GetStringField(itemTable, "getValueLabel", "get_value_label");
        if (!string.IsNullOrWhiteSpace(callbackName))
        {
            return ExecuteInPhase(LuaCallbackPhase.OptionsQuery, () =>
            {
                if (!TryInvokeCallback(callbackName, out var result))
                {
                    return string.Empty;
                }

                return result.IsNil() ? string.Empty : result.CastToString();
            });
        }

        return GetStringField(itemTable, "valueLabel", "value_label") ?? string.Empty;
    }

    private void InvokeOptionBooleanToggle(Table itemTable)
    {
        ExecuteInPhase(LuaCallbackPhase.OptionsInteraction, () => InvokeNamedCallback(itemTable, "activate", "on_activate"));
    }

    private void InvokeOptionIntegerAdvance(Table itemTable)
    {
        ExecuteInPhase(LuaCallbackPhase.OptionsInteraction, () => InvokeNamedCallback(itemTable, "activate", "on_activate"));
    }

    private void InvokeOptionChoiceAdvance(Table itemTable)
    {
        ExecuteInPhase(LuaCallbackPhase.OptionsInteraction, () => InvokeNamedCallback(itemTable, "activate", "on_activate"));
    }

    private Keys ReadOptionKeyValue(Table itemTable)
    {
        if (_script is null)
        {
            return Keys.None;
        }

        var callbackName = GetStringField(itemTable, "getKey", "get_key");
        if (string.IsNullOrWhiteSpace(callbackName))
        {
            return Keys.None;
        }

        return ExecuteInPhase(LuaCallbackPhase.OptionsQuery, () =>
        {
            if (!TryInvokeCallback(callbackName, out var result))
            {
                return Keys.None;
            }

            return TryParseKey(result.CastToString(), out var key) ? key : Keys.None;
        });
    }

    private void InvokeOptionKeySetter(Table itemTable, Keys key)
    {
        if (_script is null || _pluginTable is null)
        {
            return;
        }

        var callbackName = GetStringField(itemTable, "setKey", "set_key");
        if (string.IsNullOrWhiteSpace(callbackName))
        {
            return;
        }

        ExecuteInPhase(LuaCallbackPhase.OptionsInteraction, () =>
            TryInvokeCallback(callbackName, out _, DynValue.NewString(key.ToString())));
    }

    private void InvokeNamedCallback(Table itemTable, params string[] callbackFields)
    {
        if (_script is null || _pluginTable is null)
        {
            return;
        }

        var callbackName = GetStringField(itemTable, callbackFields);
        if (string.IsNullOrWhiteSpace(callbackName))
        {
            return;
        }

        TryInvokeCallback(callbackName, out _);
    }

    private ClientScoreboardPanelLocation ReadScoreboardLocation()
    {
        var text = ReadPluginStringValue("scoreboard_panel_location", "get_scoreboard_panel_location");
        return Enum.TryParse<ClientScoreboardPanelLocation>(text, ignoreCase: true, out var location)
            ? location
            : ClientScoreboardPanelLocation.Footer;
    }

    private int ReadScoreboardOrder()
    {
        if (_script is null || _pluginTable is null)
        {
            return 0;
        }

        var getter = _pluginTable.Get("get_scoreboard_panel_order");
        if (getter.Type is DataType.Function or DataType.ClrFunction)
        {
            if (!TryInvokeCallback("get_scoreboard_panel_order", out var result))
            {
                return 0;
            }

            return result.Type == DataType.Number ? (int)result.Number : 0;
        }

        var value = _pluginTable.Get("scoreboard_panel_order");
        return value.Type == DataType.Number ? (int)value.Number : 0;
    }

    private string ReadPluginStringValue(string fieldName, string getterName)
    {
        if (_script is null || _pluginTable is null)
        {
            return string.Empty;
        }

        var getter = _pluginTable.Get(getterName);
        if (getter.Type is DataType.Function or DataType.ClrFunction)
        {
            if (!TryInvokeCallback(getterName, out var result))
            {
                return string.Empty;
            }

            return result.IsNil() ? string.Empty : result.CastToString();
        }

        var value = _pluginTable.Get(fieldName);
        return value.IsNil() ? string.Empty : value.CastToString();
    }

    private void InvokeMenuCallback(string callbackName)
    {
        ExecuteInPhase(LuaCallbackPhase.MenuInteraction, () => CallIfPresent(callbackName));
    }

    private bool CanRegisterTextureAsset(string functionName, string assetId)
    {
        if (IsTextureRegistrationPhaseAllowed(_currentCallbackPhase))
        {
            return true;
        }

        _context?.Log(
            $"[lua-plugin] {functionName} rejected for {manifest.Id} asset \"{assetId}\" during {DescribePhase(_currentCallbackPhase)}. Register texture assets during initialize, lifecycle startup, or on_client_frame.");
        return false;
    }

    private static bool IsTextureRegistrationPhaseAllowed(LuaCallbackPhase phase)
    {
        return phase is LuaCallbackPhase.Initialize or LuaCallbackPhase.Lifecycle or LuaCallbackPhase.Update;
    }

    private static string DescribePhase(LuaCallbackPhase phase)
    {
        return phase switch
        {
            LuaCallbackPhase.None => "an unmanaged host call",
            LuaCallbackPhase.Initialize => "initialize",
            LuaCallbackPhase.Shutdown => "shutdown",
            LuaCallbackPhase.Lifecycle => "a lifecycle callback",
            LuaCallbackPhase.Update => "an update callback",
            LuaCallbackPhase.Query => "a query callback",
            LuaCallbackPhase.GameplayHudDraw => "gameplay HUD draw",
            LuaCallbackPhase.ScoreboardQuery => "a scoreboard query",
            LuaCallbackPhase.ScoreboardDraw => "scoreboard draw",
            LuaCallbackPhase.DeadBodyDraw => "dead-body draw",
            LuaCallbackPhase.BubbleMenuInput => "bubble-menu input",
            LuaCallbackPhase.BubbleMenuDraw => "bubble-menu draw",
            LuaCallbackPhase.OptionsQuery => "options query",
            LuaCallbackPhase.OptionsInteraction => "options interaction",
            LuaCallbackPhase.MenuInteraction => "menu interaction",
            _ => "an unknown callback",
        };
    }

    private void ExecuteInPhase(LuaCallbackPhase phase, Action action)
    {
        var previousPhase = _currentCallbackPhase;
        _currentCallbackPhase = phase;
        try
        {
            action();
        }
        finally
        {
            _currentCallbackPhase = previousPhase;
        }
    }

    private T ExecuteInPhase<T>(LuaCallbackPhase phase, Func<T> action)
    {
        var previousPhase = _currentCallbackPhase;
        _currentCallbackPhase = phase;
        try
        {
            return action();
        }
        finally
        {
            _currentCallbackPhase = previousPhase;
        }
    }

    private static TimeSpan GetMaxCallbackDuration(LuaCallbackPhase phase)
    {
        return phase switch
        {
            LuaCallbackPhase.Initialize => TimeSpan.FromMilliseconds(250),
            LuaCallbackPhase.Shutdown => TimeSpan.FromMilliseconds(50),
            LuaCallbackPhase.Lifecycle => TimeSpan.FromMilliseconds(50),
            LuaCallbackPhase.Update => TimeSpan.FromMilliseconds(8),
            LuaCallbackPhase.Query => TimeSpan.FromMilliseconds(4),
            LuaCallbackPhase.GameplayHudDraw => TimeSpan.FromMilliseconds(4),
            LuaCallbackPhase.ScoreboardQuery => TimeSpan.FromMilliseconds(4),
            LuaCallbackPhase.ScoreboardDraw => TimeSpan.FromMilliseconds(4),
            LuaCallbackPhase.DeadBodyDraw => TimeSpan.FromMilliseconds(4),
            LuaCallbackPhase.BubbleMenuInput => TimeSpan.FromMilliseconds(4),
            LuaCallbackPhase.BubbleMenuDraw => TimeSpan.FromMilliseconds(4),
            LuaCallbackPhase.OptionsQuery => TimeSpan.FromMilliseconds(20),
            LuaCallbackPhase.OptionsInteraction => TimeSpan.FromMilliseconds(20),
            LuaCallbackPhase.MenuInteraction => TimeSpan.FromMilliseconds(20),
            _ => TimeSpan.FromMilliseconds(8),
        };
    }

    private static Table ResolvePluginTable(Script script, DynValue result)
    {
        if (result.Type == DataType.Table)
        {
            return result.Table;
        }

        var globalPlugin = script.Globals.Get("plugin");
        if (globalPlugin.Type == DataType.Table)
        {
            return globalPlugin.Table;
        }

        throw new InvalidOperationException("Lua plugin entry point must return a plugin table or assign one to global 'plugin'.");
    }

    private DynValue ToDynValue(object? value)
    {
        if (_script is null)
        {
            return DynValue.Nil;
        }

        return ToDynValue(_script, value, depth: 0);
    }

    private static DynValue ToDynValue(Script script, object? value, int depth)
    {
        if (value is null)
        {
            return DynValue.Nil;
        }

        if (depth > 6)
        {
            return DynValue.NewString(value.ToString() ?? string.Empty);
        }

        switch (value)
        {
            case LocalDamageEvent localDamageEvent:
                return DynValue.NewTable(ToLocalDamageEventTable(script, localDamageEvent));
            case Vector2 vector:
                return DynValue.NewTable(CreateVectorTable(script, vector.X, vector.Y));
            case string text:
                return DynValue.NewString(text);
            case char character:
                return DynValue.NewString(character.ToString());
            case bool boolean:
                return DynValue.NewBoolean(boolean);
            case byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal:
                return DynValue.NewNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            case Enum flagsEnumValue when flagsEnumValue.GetType().IsDefined(typeof(FlagsAttribute), inherit: false):
                return DynValue.NewTable(ToFlagsTable(script, flagsEnumValue));
            case Enum enumValue:
                return DynValue.NewString(enumValue.ToString());
            case Version version:
                return DynValue.NewString(version.ToString());
            case JsonElement jsonElement:
                return ToDynValue(script, JsonElementToObject(jsonElement), depth + 1);
            case IDictionary<string, object?> dictionary:
                return ToDictionaryTable(script, dictionary, depth + 1);
            case System.Collections.IDictionary nonGenericDictionary:
                return ToDictionaryTable(
                    script,
                    nonGenericDictionary.Keys.Cast<object>()
                        .ToDictionary(key => key.ToString() ?? string.Empty, key => nonGenericDictionary[key]),
                    depth + 1);
            case IEnumerable<object?> sequence:
                return ToArrayTable(script, sequence, depth + 1);
            case System.Collections.IEnumerable nonGenericSequence when value is not string:
                return ToArrayTable(script, nonGenericSequence.Cast<object?>(), depth + 1);
        }

        var table = new Table(script);
        foreach (var property in value.GetType().GetProperties(PublicInstanceProperties))
        {
            if (!property.CanRead || property.GetIndexParameters().Length != 0)
            {
                continue;
            }

            var propertyValue = property.GetValue(value);
            var dynValue = ToDynValue(script, propertyValue, depth + 1);
            table[property.Name] = dynValue;
            var camelCaseName = ToCamelCase(property.Name);
            if (!string.Equals(camelCaseName, property.Name, StringComparison.Ordinal))
            {
                table[camelCaseName] = dynValue;
            }
        }

        foreach (var field in value.GetType().GetFields(PublicInstanceFields))
        {
            if (field.IsStatic)
            {
                continue;
            }

            var fieldValue = field.GetValue(value);
            var dynValue = ToDynValue(script, fieldValue, depth + 1);
            table[field.Name] = dynValue;
            var camelCaseName = ToCamelCase(field.Name);
            if (!string.Equals(camelCaseName, field.Name, StringComparison.Ordinal))
            {
                table[camelCaseName] = dynValue;
            }
        }

        return DynValue.NewTable(table);
    }

    private static DynValue ToArrayTable(Script script, IEnumerable<object?> values, int depth)
    {
        var table = new Table(script);
        var index = 1;
        foreach (var item in values)
        {
            table[index] = ToDynValue(script, item, depth);
            index += 1;
        }

        return DynValue.NewTable(table);
    }

    private static DynValue ToDictionaryTable(Script script, IDictionary<string, object?> values, int depth)
    {
        var table = new Table(script);
        foreach (var pair in values)
        {
            table[pair.Key] = ToDynValue(script, pair.Value, depth);
        }

        return DynValue.NewTable(table);
    }

    private static object CreateClientStateSnapshot(IOpenGarrisonClientReadOnlyState state)
    {
        var hasHealth = state.TryGetLocalPlayerHealth(out var health, out var maxHealth);
        var hasLocalPosition = state.TryGetLocalPlayerWorldPosition(out var localPosition);
        return new
        {
            state.IsConnected,
            state.IsMainMenuOpen,
            state.IsGameplayActive,
            state.IsGameplayInputBlocked,
            state.IsSpectator,
            state.IsLocalPlayerAlive,
            state.IsLocalPlayerScoped,
            state.IsLocalPlayerHealing,
            state.WorldFrame,
            state.TickRate,
            state.LocalPingMilliseconds,
            state.LevelName,
            state.LevelWidth,
            state.LevelHeight,
            state.ViewportWidth,
            state.ViewportHeight,
            HasLocalPlayerId = state.LocalPlayerId.HasValue,
            LocalPlayerId = state.LocalPlayerId ?? -1,
            LocalPlayerTeam = state.LocalPlayerTeam.ToString(),
            LocalPlayerClass = state.LocalPlayerClass.ToString(),
            LocalPlayerHealth = hasHealth ? health : -1,
            LocalPlayerMaxHealth = hasHealth ? maxHealth : -1,
            HasLocalPlayerPosition = hasLocalPosition,
            LocalPlayerWorldX = hasLocalPosition ? localPosition.X : 0f,
            LocalPlayerWorldY = hasLocalPosition ? localPosition.Y : 0f,
            CameraTopLeftX = state.CameraTopLeft.X,
            CameraTopLeftY = state.CameraTopLeft.Y,
            PlayerMarkers = state.GetPlayerMarkers(),
            SentryMarkers = state.GetSentryMarkers(),
            ObjectiveMarkers = state.GetObjectiveMarkers(),
        };
    }

    private bool TryResolveTexture(string assetId, out Texture2D texture)
    {
        if (_registeredTextures.TryGetValue(assetId, out texture!))
        {
            return true;
        }

        if (_context is not null && _context.Assets.TryGetTextureAsset(assetId, out texture))
        {
            _registeredTextures[assetId] = texture;
            return true;
        }

        texture = null!;
        return false;
    }

    private bool TryResolveTextureAtlas(string assetId, out ClientPluginTextureAtlas atlas)
    {
        if (_registeredTextureAtlases.TryGetValue(assetId, out atlas))
        {
            return true;
        }

        if (_context is not null && _context.Assets.TryGetTextureAtlasAsset(assetId, out atlas))
        {
            _registeredTextureAtlases[assetId] = atlas;
            return true;
        }

        atlas = default;
        return false;
    }

    private bool TryResolveTextureRegion(string assetId, out ClientPluginTextureRegion region)
    {
        if (_registeredTextureRegions.TryGetValue(assetId, out region))
        {
            return true;
        }

        if (_context is not null && _context.Assets.TryGetTextureRegionAsset(assetId, out region))
        {
            _registeredTextureRegions[assetId] = region;
            return true;
        }

        region = default;
        return false;
    }

    private bool TryResolveAnimationTexture(string assetId, int frameIndex, out Texture2D texture)
    {
        if (_ownedTextureSequences.TryGetValue(assetId, out var sequence)
            && frameIndex >= 0
            && frameIndex < sequence.Frames.Count)
        {
            texture = sequence.Frames[frameIndex];
            return true;
        }

        texture = null!;
        return false;
    }

    private static object? JsonElementToObject(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                property => property.Name,
                property => JsonElementToObject(property.Value)),
            JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var int64Value) => int64Value,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };
    }

    private static void SaveLuaTableJson(string path, DynValue value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
        var serialized = DynValueToPlainObject(value);
        var json = JsonSerializer.Serialize(serialized, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static object? DynValueToPlainObject(DynValue value)
    {
        return value.Type switch
        {
            DataType.Nil or DataType.Void => null,
            DataType.Boolean => value.Boolean,
            DataType.Number => value.Number,
            DataType.String => value.String,
            DataType.Table => LuaTableToPlainObject(value.Table),
            _ => value.ToString(),
        };
    }

    private static object LuaTableToPlainObject(Table table)
    {
        var numericEntries = table.Pairs.Where(pair => pair.Key.Type == DataType.Number).OrderBy(pair => pair.Key.Number).ToArray();
        var stringEntries = table.Pairs.Where(pair => pair.Key.Type == DataType.String).ToArray();
        if (stringEntries.Length == 0 && numericEntries.Length > 0)
        {
            return numericEntries.Select(pair => DynValueToPlainObject(pair.Value)).ToArray();
        }

        var dictionary = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var pair in stringEntries)
        {
            dictionary[pair.Key.String] = DynValueToPlainObject(pair.Value);
        }

        foreach (var pair in numericEntries)
        {
            dictionary[pair.Key.Number.ToString(CultureInfo.InvariantCulture)] = DynValueToPlainObject(pair.Value);
        }

        return dictionary;
    }

    private static string ResolveConfigPath(string configDirectory, string relativePath)
    {
        var normalizedRelativePath = string.IsNullOrWhiteSpace(relativePath) ? "config.json" : relativePath;
        return ResolveContainedPath(configDirectory, normalizedRelativePath, "Plugin config path escapes config directory.");
    }

    private static string ResolvePluginPath(string pluginDirectory, string relativePath, string defaultRelativePath = "")
    {
        var normalizedRelativePath = string.IsNullOrWhiteSpace(relativePath) ? defaultRelativePath : relativePath;
        return ResolveContainedPath(pluginDirectory, normalizedRelativePath, "Plugin path escapes plugin directory.");
    }

    private static string ResolveContainedPath(string rootDirectory, string relativePath, string errorMessage)
    {
        var fullRootDirectory = Path.GetFullPath(rootDirectory);
        var combinedPath = Path.GetFullPath(Path.Combine(fullRootDirectory, relativePath));
        if (!combinedPath.StartsWith(fullRootDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return combinedPath;
    }

    private static string ReadStringArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.IsNil() ? string.Empty : dynValue.CastToString();
    }

    private static string ReadOptionalStringArgument(CallbackArguments args, int index, string defaultValue)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.IsNil() ? defaultValue : dynValue.CastToString();
    }

    private static int ReadIntArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.Type == DataType.Number
            ? (int)dynValue.Number
            : int.Parse(dynValue.CastToString(), CultureInfo.InvariantCulture);
    }

    private static int ReadOptionalIntArgument(CallbackArguments args, int index, int defaultValue)
    {
        var dynValue = ReadArgument(args, index);
        return dynValue.IsNil()
            ? defaultValue
            : (dynValue.Type == DataType.Number ? (int)dynValue.Number : int.Parse(dynValue.CastToString(), CultureInfo.InvariantCulture));
    }

    private static float ReadOptionalFloatArgument(CallbackArguments args, int index, float defaultValue)
    {
        var dynValue = ReadArgument(args, index);
        if (dynValue.IsNil())
        {
            return defaultValue;
        }

        return dynValue.Type == DataType.Number
            ? SanitizeFiniteFloat((float)dynValue.Number, defaultValue)
            : SanitizeFiniteFloat(float.Parse(dynValue.CastToString(), CultureInfo.InvariantCulture), defaultValue);
    }

    private static bool ReadOptionalBoolArgument(CallbackArguments args, int index, bool defaultValue)
    {
        var dynValue = ReadArgument(args, index);
        if (dynValue.IsNil())
        {
            return defaultValue;
        }

        return dynValue.Type == DataType.Boolean
            ? dynValue.Boolean
            : bool.Parse(dynValue.CastToString());
    }

    private static Vector2 ReadVector2Argument(CallbackArguments args, int xIndex, int yIndex)
    {
        var value = ReadArgument(args, xIndex);
        if (value.Type == DataType.Table)
        {
            return ReadVector2FromTable(value.Table);
        }

        return new Vector2(ReadOptionalFloatArgument(args, xIndex, 0f), ReadOptionalFloatArgument(args, yIndex, 0f));
    }

    private static Rectangle ReadRectangleArgument(CallbackArguments args, int xIndex)
    {
        return new Rectangle(
            ReadIntArgument(args, xIndex),
            ReadIntArgument(args, xIndex + 1),
            Math.Max(0, ReadIntArgument(args, xIndex + 2)),
            Math.Max(0, ReadIntArgument(args, xIndex + 3)));
    }

    private static Vector2 ReadVector2FromTable(Table table)
    {
        return new Vector2(
            ReadNumberFromTable(table, "x", 0f),
            ReadNumberFromTable(table, "y", 0f));
    }

    private static Vector2? ReadOptionalOriginArgument(CallbackArguments args, int xIndex, int yIndex)
    {
        var xValue = ReadArgument(args, xIndex);
        var yValue = ReadArgument(args, yIndex);
        return xValue.IsNil() && yValue.IsNil()
            ? null
            : new Vector2(ReadOptionalFloatArgument(args, xIndex, 0f), ReadOptionalFloatArgument(args, yIndex, 0f));
    }

    private static Color ReadColorArgument(CallbackArguments args, int index)
    {
        var dynValue = ReadArgument(args, index);
        if (dynValue.Type == DataType.Table)
        {
            var table = dynValue.Table;
            var r = Math.Clamp(SanitizeFiniteFloat(ReadNumberFromTable(table, "r", 255f), 255f), 0f, 255f);
            var g = Math.Clamp(SanitizeFiniteFloat(ReadNumberFromTable(table, "g", 255f), 255f), 0f, 255f);
            var b = Math.Clamp(SanitizeFiniteFloat(ReadNumberFromTable(table, "b", 255f), 255f), 0f, 255f);
            var a = Math.Clamp(SanitizeFiniteFloat(ReadNumberFromTable(table, "a", 255f), 255f), 0f, 255f);
            return new Color((byte)r, (byte)g, (byte)b, (byte)a);
        }

        return Color.White;
    }

    private static float ReadNumberFromTable(Table table, string key, float defaultValue)
    {
        var value = table.Get(key);
        return value.Type == DataType.Number ? SanitizeFiniteFloat((float)value.Number, defaultValue) : defaultValue;
    }

    private static float SanitizeFiniteFloat(float value, float defaultValue)
    {
        return float.IsFinite(value) ? value : defaultValue;
    }

    private static float ClampPositiveFiniteFloat(float value, float defaultValue)
    {
        value = SanitizeFiniteFloat(value, defaultValue);
        return value > 0f ? value : defaultValue;
    }

    private static int? ReadNullableIntFromTable(Table table, params string[] keys)
    {
        for (var index = 0; index < keys.Length; index += 1)
        {
            var value = table.Get(keys[index]);
            if (value.Type == DataType.Number)
            {
                return (int)value.Number;
            }
        }

        return null;
    }

    private static bool ReadBoolFromTable(Table table, params string[] keys)
    {
        for (var index = 0; index < keys.Length; index += 1)
        {
            var value = table.Get(keys[index]);
            if (value.Type == DataType.Boolean)
            {
                return value.Boolean;
            }
        }

        return false;
    }

    private static Table CreateVectorTable(Script script, float x, float y)
    {
        return new Table(script)
        {
            ["x"] = x,
            ["y"] = y,
        };
    }

    private static Table CreateColorTable(Script script, float r, float g, float b, float a)
    {
        return new Table(script)
        {
            ["r"] = r,
            ["g"] = g,
            ["b"] = b,
            ["a"] = a,
        };
    }

    private static Table ToLocalDamageEventTable(Script script, LocalDamageEvent value)
    {
        var table = new Table(script);
        table["amount"] = value.Amount;
        table["targetKind"] = value.TargetKind.ToString();
        table["target_kind"] = value.TargetKind.ToString();
        table["targetEntityId"] = value.TargetEntityId;
        table["target_entity_id"] = value.TargetEntityId;
        table["targetWorldX"] = value.TargetWorldPosition.X;
        table["target_world_x"] = value.TargetWorldPosition.X;
        table["targetWorldY"] = value.TargetWorldPosition.Y;
        table["target_world_y"] = value.TargetWorldPosition.Y;
        table["targetWasKilled"] = value.TargetWasKilled;
        table["target_was_killed"] = value.TargetWasKilled;
        table["dealtByLocalPlayer"] = value.DealtByLocalPlayer;
        table["dealt_by_local_player"] = value.DealtByLocalPlayer;
        table["assistedByLocalPlayer"] = value.AssistedByLocalPlayer;
        table["assisted_by_local_player"] = value.AssistedByLocalPlayer;
        table["receivedByLocalPlayer"] = value.ReceivedByLocalPlayer;
        table["received_by_local_player"] = value.ReceivedByLocalPlayer;
        table["attackerPlayerId"] = value.AttackerPlayerId;
        table["attacker_player_id"] = value.AttackerPlayerId;
        table["assistedByPlayerId"] = value.AssistedByPlayerId;
        table["assisted_by_player_id"] = value.AssistedByPlayerId;
        var airshot = value.Flags.HasFlag(LocalDamageFlags.Airshot);
        table["airshot"] = airshot;
        table["flagsAirshot"] = airshot;
        table["flags_airshot"] = airshot;
        table["flagsRaw"] = value.Flags.ToString();
        table["flags_raw"] = value.Flags.ToString();
        return table;
    }

    private static Table ToFlagsTable(Script script, Enum value)
    {
        var table = new Table(script);
        var rawValue = value.ToString();
        table["raw"] = rawValue;
        table["flagsSignature"] = rawValue;
        table["flags_signature"] = rawValue;

        var enumType = value.GetType();
        foreach (var enumName in Enum.GetNames(enumType))
        {
            if (string.Equals(enumName, "None", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var enumValue = (Enum)Enum.Parse(enumType, enumName);
            var hasFlag = value.HasFlag(enumValue);
            table[enumName] = hasFlag;
            table[ToCamelCase(enumName)] = hasFlag;
            table[enumName.ToLowerInvariant()] = hasFlag;
        }

        return table;
    }

    private static bool TryLoadTextureSequence(
        GraphicsDevice graphicsDevice,
        string path,
        int frameCount,
        bool applyChromaKey,
        out OwnedTextureSequence textureSequence)
    {
        textureSequence = null!;
        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(path);
            var frames = new List<Texture2D>(Math.Max(1, frameCount));
            if (Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase))
            {
                for (var frameIndex = 0; frameIndex < image.Frames.Count; frameIndex += 1)
                {
                    using var frameImage = image.Frames.CloneFrame(frameIndex);
                    frames.Add(CreateTexture(graphicsDevice, frameImage, applyChromaKey));
                }
            }
            else
            {
                var safeFrameCount = Math.Max(1, frameCount);
                var frameWidth = Math.Max(1, image.Width / safeFrameCount);
                for (var frameIndex = 0; frameIndex < safeFrameCount; frameIndex += 1)
                {
                    var frameRectangle = new ImageSharpRectangle(frameIndex * frameWidth, 0, frameWidth, image.Height);
                    using var frameImage = image.Clone(clone => clone.Crop(frameRectangle));
                    frames.Add(CreateTexture(graphicsDevice, frameImage, applyChromaKey));
                }
            }

            textureSequence = new OwnedTextureSequence(frames);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Texture2D CreateTexture(GraphicsDevice graphicsDevice, SixLabors.ImageSharp.Image<Rgba32> image, bool applyChromaKey)
    {
        var pixelData = new Rgba32[image.Width * image.Height];
        image.CopyPixelDataTo(pixelData);
        if (applyChromaKey)
        {
            ApplyLegacyChromaKeyTransparency(pixelData, image.Width, image.Height);
        }

        var textureData = new Color[pixelData.Length];
        for (var index = 0; index < pixelData.Length; index += 1)
        {
            var pixel = pixelData[index];
            if (pixel.A == 0)
            {
                textureData[index] = Color.Transparent;
                continue;
            }

            var premultipliedRed = (pixel.R * pixel.A + 127) / 255;
            var premultipliedGreen = (pixel.G * pixel.A + 127) / 255;
            var premultipliedBlue = (pixel.B * pixel.A + 127) / 255;
            textureData[index] = new Color(
                (byte)premultipliedRed,
                (byte)premultipliedGreen,
                (byte)premultipliedBlue,
                pixel.A);
        }

        var texture = new Texture2D(graphicsDevice, image.Width, image.Height);
        texture.SetData(textureData);
        return texture;
    }

    private static void ApplyLegacyChromaKeyTransparency(Rgba32[] pixelData, int width, int height)
    {
        if (pixelData.Length == 0 || width <= 0 || height <= 0)
        {
            return;
        }

        var visited = new bool[pixelData.Length];
        var pending = new Queue<int>();
        for (var x = 0; x < width; x += 1)
        {
            TryQueueChromaKeyIndex(x, 0, width, height, pixelData, visited, pending);
            TryQueueChromaKeyIndex(x, height - 1, width, height, pixelData, visited, pending);
        }

        for (var y = 0; y < height; y += 1)
        {
            TryQueueChromaKeyIndex(0, y, width, height, pixelData, visited, pending);
            TryQueueChromaKeyIndex(width - 1, y, width, height, pixelData, visited, pending);
        }

        while (pending.Count > 0)
        {
            var index = pending.Dequeue();
            pixelData[index] = new Rgba32(0, 0, 0, 0);
            var x = index % width;
            var y = index / width;
            TryQueueChromaKeyIndex(x + 1, y, width, height, pixelData, visited, pending);
            TryQueueChromaKeyIndex(x - 1, y, width, height, pixelData, visited, pending);
            TryQueueChromaKeyIndex(x, y + 1, width, height, pixelData, visited, pending);
            TryQueueChromaKeyIndex(x, y - 1, width, height, pixelData, visited, pending);
        }
    }

    private static void TryQueueChromaKeyIndex(
        int x,
        int y,
        int width,
        int height,
        Rgba32[] pixelData,
        bool[] visited,
        Queue<int> pending)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        var index = (y * width) + x;
        if (visited[index])
        {
            return;
        }

        visited[index] = true;
        if (!IsLegacyChromaKeyGreen(pixelData[index]))
        {
            return;
        }

        pending.Enqueue(index);
    }

    private static bool IsLegacyChromaKeyGreen(Rgba32 pixel)
    {
        return pixel.A >= 128
            && pixel.G >= 90
            && pixel.R <= 48
            && pixel.G - pixel.R >= 56
            && pixel.G - pixel.B >= 32;
    }

    private static DynValue ReadArgument(CallbackArguments args, int index)
    {
        if (args.Count <= index)
        {
            return DynValue.Nil;
        }

        if (args.Count > index + 1 && args[0].Type == DataType.Table)
        {
            return args[index + 1];
        }

        return args[index];
    }

    private static string? GetStringField(Table table, params string[] names)
    {
        for (var index = 0; index < names.Length; index += 1)
        {
            var dynValue = table.Get(names[index]);
            if (dynValue.Type == DataType.String)
            {
                return dynValue.String;
            }
        }

        return null;
    }

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value) || char.IsLower(value[0]))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static bool TryParseKey(string? keyText, out Keys key)
    {
        if (string.IsNullOrWhiteSpace(keyText))
        {
            key = Keys.None;
            return false;
        }

        return Enum.TryParse(keyText, ignoreCase: true, out key);
    }

    private static string FormatKeyDisplayName(string? keyText)
    {
        if (!TryParseKey(keyText, out var key))
        {
            return string.IsNullOrWhiteSpace(keyText) ? Keys.None.ToString() : keyText;
        }

        return key switch
        {
            Keys.LeftShift => "LShift",
            Keys.RightShift => "RShift",
            Keys.LeftControl => "LCtrl",
            Keys.RightControl => "RCtrl",
            Keys.LeftAlt => "LAlt",
            Keys.RightAlt => "RAlt",
            Keys.OemTilde => "~",
            Keys.OemComma => ",",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemPipe => "\\",
            Keys.Space => "Space",
            Keys.PageUp => "PgUp",
            Keys.PageDown => "PgDn",
            _ => key.ToString(),
        };
    }

    private static Rectangle CalculateTextureSourceRectangle(
        Texture2D texture,
        float viewLeft,
        float viewTop,
        float visibleWorldWidth,
        float visibleWorldHeight,
        float levelWidth,
        float levelHeight)
    {
        var x = (int)MathF.Round((viewLeft / levelWidth) * texture.Width);
        var y = (int)MathF.Round((viewTop / levelHeight) * texture.Height);
        var width = (int)MathF.Round((visibleWorldWidth / levelWidth) * texture.Width);
        var height = (int)MathF.Round((visibleWorldHeight / levelHeight) * texture.Height);
        x = Math.Clamp(x, 0, Math.Max(0, texture.Width - 1));
        y = Math.Clamp(y, 0, Math.Max(0, texture.Height - 1));
        width = Math.Clamp(width, 1, Math.Max(1, texture.Width - x));
        height = Math.Clamp(height, 1, Math.Max(1, texture.Height - y));
        return new Rectangle(x, y, width, height);
    }

    private void LogCallbackFailure(string callbackName, Exception ex)
    {
        _context?.Log($"[lua-plugin] callback failed for {manifest.Id} callback \"{callbackName}\" manifest \"{GetManifestPath()}\": {ex.Message}");
    }

    private void DisableCallbacks(string reason)
    {
        if (_callbacksDisabled)
        {
            return;
        }

        _callbacksDisabled = true;
        _pendingLocalDamageEvents.Clear();
        _context?.Log($"[lua-plugin] disabled {manifest.Id}: {reason}");
    }

    private string GetManifestPath()
    {
        return OpenGarrisonPluginManifestLoader.GetManifestPath(pluginDirectory);
    }

    private enum LuaCallbackPhase
    {
        None,
        Initialize,
        Shutdown,
        Lifecycle,
        Update,
        Query,
        GameplayHudDraw,
        ScoreboardQuery,
        ScoreboardDraw,
        DeadBodyDraw,
        BubbleMenuInput,
        BubbleMenuDraw,
        OptionsQuery,
        OptionsInteraction,
        MenuInteraction,
    }

    private sealed class LuaBooleanOptionItem(
        string label,
        Table itemTable,
        Func<Table, string> valueLabelFactory,
        Action<Table> activate) : ClientPluginOptionItem(label)
    {
        public override string GetValueLabel() => valueLabelFactory(itemTable);

        public override void Activate() => activate(itemTable);
    }

    private sealed class LuaIntegerOptionItem(
        string label,
        Table itemTable,
        Func<Table, string> valueLabelFactory,
        Action<Table> activate) : ClientPluginOptionItem(label)
    {
        public override string GetValueLabel() => valueLabelFactory(itemTable);

        public override void Activate() => activate(itemTable);
    }

    private sealed class LuaChoiceOptionItem(
        string label,
        Table itemTable,
        Func<Table, string> valueLabelFactory,
        Action<Table> activate) : ClientPluginOptionItem(label)
    {
        public override string GetValueLabel() => valueLabelFactory(itemTable);

        public override void Activate() => activate(itemTable);
    }

    private sealed class OwnedTextureSequence(List<Texture2D> frames) : IDisposable
    {
        public List<Texture2D> Frames { get; } = frames;

        public void Dispose()
        {
            for (var index = 0; index < Frames.Count; index += 1)
            {
                try
                {
                    Frames[index].Dispose();
                }
                catch
                {
                }
            }

            Frames.Clear();
        }
    }
}
