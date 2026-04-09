using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using MoonSharp.Interpreter;
using OpenGarrison.Client.Plugins;

namespace OpenGarrison.Client;

internal sealed partial class LuaClientPlugin
{
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
        _pendingHealEvents.Clear();
        _cachedClientStateSnapshot = null;
        _cachedClientStateSnapshotWorldFrame = null;
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
            FlushPendingHealEvents();
            CallIfPresent("on_client_frame", ToDynValue(e));
        });
    }

    public void OnWorldSound(ClientWorldSoundEvent e) => ExecuteInPhase(LuaCallbackPhase.Update, () => CallIfPresent("on_world_sound", ToDynValue(e)));

    public void OnServerPluginMessage(ClientPluginMessageEnvelope e)
    {
        ExecuteInPhase(LuaCallbackPhase.Update, () => CallIfPresent("on_server_plugin_message", ToDynValue(e)));
    }

    public void OnLocalDamage(LocalDamageEvent e)
    {
        if (_script is null || _pluginTable is null)
        {
            return;
        }

        _pendingLocalDamageEvents.Add(ToDynValue(e));
    }

    public void OnHeal(ClientHealEvent e)
    {
        if (_script is null || _pluginTable is null)
        {
            return;
        }

        _pendingHealEvents.Add(ToDynValue(e));
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

    private void FlushPendingHealEvents()
    {
        if (_pendingHealEvents.Count == 0)
        {
            return;
        }

        for (var index = 0; index < _pendingHealEvents.Count; index += 1)
        {
            CallIfPresent("on_heal", _pendingHealEvents[index]);
        }

        _pendingHealEvents.Clear();
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
        var maxResumeCount = GetMaxCallbackResumeCount(_currentCallbackPhase);
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

            if (resumeCount >= maxResumeCount)
            {
                throw new TimeoutException($"Lua callback exceeded the resume budget of {maxResumeCount} slices.");
            }

            if (stopwatch.Elapsed > maxDuration)
            {
                throw new TimeoutException($"Lua callback exceeded the {maxDuration.TotalMilliseconds:0.##}ms budget.");
            }
        }
    }
}
