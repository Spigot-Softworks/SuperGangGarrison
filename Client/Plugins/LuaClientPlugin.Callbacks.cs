using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        _hudWidgets.Clear();
        _abilityHudWidgets.Clear();
        _scoreboardPanels.Clear();
        _scoreboardPlayerActions.Clear();
        _chatFilters.Clear();
        _chatCommands.Clear();
        _callbackCache.Clear();
        _pendingLocalDamageEvents.Clear();
        _pendingHealEvents.Clear();
        _cachedClientState = DynValue.Nil;
        _cachedClientStateKey = null;
        _cachedClientRuntimeState = DynValue.Nil;
        _cachedClientRuntimeStateKey = null;
        _cachedPlayerMarkers = DynValue.Nil;
        _cachedPlayerMarkersWorldFrame = null;
        _cachedSentryMarkers = DynValue.Nil;
        _cachedSentryMarkersWorldFrame = null;
        _cachedObjectiveMarkers = DynValue.Nil;
        _cachedObjectiveMarkersWorldFrame = null;
        _cachedClientFrameEventTable = null;
        _cachedClientFrameEvent = DynValue.Nil;
        _cachedGameplayHudCanvasTable = null;
        _cachedGameplayHudCanvas = DynValue.Nil;
        _cachedScoreboardHudCanvasTable = null;
        _cachedScoreboardHudCanvas = DynValue.Nil;
        _activeHudCanvas = null;
        _activeScoreboardCanvas = null;
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
        var hasFrameCallback = HasCallback("on_client_frame");
        var hasPendingDamageCallback = _pendingLocalDamageEvents.Count > 0 && HasCallback("on_local_damage");
        var hasPendingHealCallback = _pendingHealEvents.Count > 0 && HasCallback("on_heal");
        if (!hasFrameCallback && !hasPendingDamageCallback && !hasPendingHealCallback)
        {
            _pendingLocalDamageEvents.Clear();
            _pendingHealEvents.Clear();
            return;
        }

        ExecuteInPhase(LuaCallbackPhase.Update, () =>
        {
            if (hasPendingDamageCallback)
            {
                FlushPendingLocalDamageEvents();
            }
            else
            {
                _pendingLocalDamageEvents.Clear();
            }

            if (hasPendingHealCallback)
            {
                FlushPendingHealEvents();
            }
            else
            {
                _pendingHealEvents.Clear();
            }

            if (hasFrameCallback)
            {
                CallIfPresent("on_client_frame", GetCachedClientFrameEvent(e));
            }
        });
    }

    public void OnWorldSound(ClientWorldSoundEvent e)
    {
        if (!HasCallback("on_world_sound"))
        {
            return;
        }

        ExecuteInPhase(LuaCallbackPhase.Update, () => CallIfPresent("on_world_sound", ToDynValue(e)));
    }

    public void OnServerPluginMessage(ClientPluginMessageEnvelope e)
    {
        if (!HasCallback("on_server_plugin_message"))
        {
            return;
        }

        ExecuteInPhase(LuaCallbackPhase.Update, () => CallIfPresent("on_server_plugin_message", ToDynValue(e)));
    }

    public void OnLocalDamage(LocalDamageEvent e)
    {
        if (_script is null || _pluginTable is null || !HasCallback("on_local_damage"))
        {
            return;
        }

        _pendingLocalDamageEvents.Add(ToDynValue(e));
    }

    public void OnHeal(ClientHealEvent e)
    {
        if (_script is null || _pluginTable is null || !HasCallback("on_heal"))
        {
            return;
        }

        _pendingHealEvents.Add(ToDynValue(e));
    }

    public Vector2 GetCameraOffset()
    {
        if (_script is null || _pluginTable is null || !HasCallback("get_camera_offset"))
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

        var hasLegacyCallback = HasCallback("on_gameplay_hud_draw");
        var hasAbilityHudWidgets = HasGameplayAbilityHudWidgets();
        if (!hasLegacyCallback && _hudWidgets.Count == 0 && !hasAbilityHudWidgets)
        {
            return;
        }

        ExecuteInPhase(LuaCallbackPhase.GameplayHudDraw, () =>
        {
            _activeHudCanvas = canvas;
            try
            {
                var canvasValue = GetCachedGameplayHudCanvas(canvas);
                if (hasLegacyCallback)
                {
                    CallIfPresent("on_gameplay_hud_draw", canvasValue);
                }

                DrawRegisteredHudWidgets(canvasValue);
                DrawGameplayAbilityHudWidgets(canvasValue);
            }
            finally
            {
                _activeHudCanvas = null;
            }
        });
    }

    public ClientScoreboardPanelLocation ScoreboardPanelLocation => ExecuteInPhase(LuaCallbackPhase.ScoreboardQuery, ReadScoreboardLocation);

    public int ScoreboardPanelOrder => ExecuteInPhase(LuaCallbackPhase.ScoreboardQuery, ReadScoreboardOrder);

    public void OnScoreboardDraw(IOpenGarrisonClientScoreboardCanvas canvas, ClientScoreboardRenderState state)
    {
        if (_script is null || _pluginTable is null)
        {
            return;
        }

        var hasLegacyCallback = HasCallback("on_scoreboard_draw");
        if (!hasLegacyCallback && _scoreboardPanels.Count == 0)
        {
            return;
        }

        ExecuteInPhase(LuaCallbackPhase.ScoreboardDraw, () =>
        {
            _activeHudCanvas = canvas;
            _activeScoreboardCanvas = canvas;
            try
            {
                var canvasValue = GetCachedScoreboardHudCanvas(canvas);
                var stateValue = ToDynValue(state);
                if (hasLegacyCallback)
                {
                    CallIfPresent("on_scoreboard_draw", canvasValue, stateValue);
                }

                DrawRegisteredScoreboardPanels(canvasValue, stateValue);
            }
            finally
            {
                _activeScoreboardCanvas = null;
                _activeHudCanvas = null;
            }
        });
    }

    public IReadOnlyList<ClientScoreboardPlayerAction> GetScoreboardPlayerActions(ClientScoreboardPlayerActionContext context)
    {
        if (_script is null || _pluginTable is null || _scoreboardPlayerActions.Count == 0)
        {
            return Array.Empty<ClientScoreboardPlayerAction>();
        }

        return _scoreboardPlayerActions
            .OrderBy(static action => action.Order)
            .ThenBy(static action => action.Id, StringComparer.Ordinal)
            .Select(action => new ClientScoreboardPlayerAction(
                action.Id,
                action.Label,
                action.Order,
                activateContext => ExecuteInPhase(
                    LuaCallbackPhase.ScoreboardInteraction,
                    () => TryInvokeRegisteredCallback(
                        "scoreboard player action",
                        action.Id,
                        action.ActivateCallback,
                        out _,
                        ToDynValue(activateContext)))))
            .ToArray();
    }

    public ClientChatSubmitResult BeforeChatSubmit(ClientChatSubmitContext context)
    {
        if (_script is null || _pluginTable is null || _chatFilters.Count == 0)
        {
            return new ClientChatSubmitResult(context.Text, context.TeamOnly);
        }

        return ExecuteInPhase(LuaCallbackPhase.ChatInteraction, () =>
        {
            var result = new ClientChatSubmitResult(context.Text, context.TeamOnly);
            foreach (var filter in _chatFilters
                         .OrderBy(static filter => filter.Order)
                         .ThenBy(static filter => filter.Id, StringComparer.Ordinal))
            {
                if (!TryInvokeRegisteredCallback(
                        "chat filter",
                        filter.Id,
                        filter.Callback,
                        out var callbackResult,
                        ToDynValue(new ClientChatSubmitContext(result.Text, result.TeamOnly))))
                {
                    continue;
                }

                result = ReadChatSubmitResult(callbackResult, result);
                if (result.IsCancelled || result.IsHandled)
                {
                    return result;
                }
            }

            return result;
        });
    }

    public bool TryHandleChatCommand(ClientChatSubmitContext context)
    {
        if (_script is null
            || _pluginTable is null
            || _chatCommands.Count == 0
            || !TryParseChatCommand(context.Text, out var commandName, out var arguments))
        {
            return false;
        }

        var command = _chatCommands
            .FirstOrDefault(candidate =>
                string.Equals(candidate.Name, commandName, StringComparison.OrdinalIgnoreCase)
                || candidate.Aliases.Any(alias => string.Equals(alias, commandName, StringComparison.OrdinalIgnoreCase)));
        if (command is null)
        {
            return false;
        }

        return ExecuteInPhase(LuaCallbackPhase.ChatInteraction, () =>
        {
            if (!TryInvokeRegisteredCallback(
                    "chat command",
                    command.Name,
                    command.Callback,
                    out var result,
                    ToDynValue(new LuaClientChatCommandContext(
                        context.Text,
                        context.TeamOnly,
                        commandName,
                        arguments,
                        SplitCommandArguments(arguments)))))
            {
                return false;
            }

            if (result.IsNil() || result.Type == DataType.Void)
            {
                return true;
            }

            if (result.Type == DataType.Boolean)
            {
                return result.Boolean;
            }

            if (result.Type == DataType.Table)
            {
                return ReadOptionalBoolField(result.Table, defaultValue: true, "handled", "Handled", "isHandled", "IsHandled")
                    || ReadOptionalBoolField(result.Table, defaultValue: false, "cancel", "Cancel", "cancelled", "Cancelled", "canceled", "Canceled");
            }

            return result.CastToBool();
        });
    }

    public ClientPluginMainMenuBackgroundOverride? GetMainMenuBackgroundOverride()
    {
        if (_script is null || _pluginTable is null || !HasCallback("get_main_menu_background_override"))
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
        if (_script is null || _pluginTable is null || _callbacksDisabled || !HasCallback("try_draw_dead_body"))
        {
            return false;
        }

        return ExecuteInPhase(LuaCallbackPhase.DeadBodyDraw, () =>
        {
            _activeHudCanvas = canvas;
            try
            {
                if (!TryInvokeCallback("try_draw_dead_body", out var result, GetCachedGameplayHudCanvas(canvas), ToDynValue(deadBody)))
                {
                    return false;
                }

                return result.CastToBool();
            }
            finally
            {
                _activeHudCanvas = null;
            }
        });
    }

    public ClientBubbleMenuUpdateResult? TryHandleBubbleMenuInput(ClientBubbleMenuInputState inputState)
    {
        if (_script is null || _pluginTable is null || !HasCallback("try_handle_bubble_menu_input"))
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
        if (_script is null || _pluginTable is null || !HasCallback("try_draw_bubble_menu"))
        {
            return false;
        }

        return ExecuteInPhase(LuaCallbackPhase.BubbleMenuDraw, () =>
        {
            _activeHudCanvas = canvas;
            try
            {
                if (!TryInvokeCallback("try_draw_bubble_menu", out var result, GetCachedGameplayHudCanvas(canvas), ToDynValue(renderState)))
                {
                    return false;
                }

                return result.CastToBool();
            }
            finally
            {
                _activeHudCanvas = null;
            }
        });
    }

    public IReadOnlyList<ClientPluginOptionsSection> GetOptionsSections()
    {
        if (_script is null || _pluginTable is null || !HasCallback("get_options_sections"))
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

        if (!TryGetCachedCallbackFunction(callbackName, out var function))
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

    private bool HasCallback(string callbackName)
    {
        if (_script is null || _pluginTable is null || _callbacksDisabled)
        {
            return false;
        }

        return TryGetCachedCallbackFunction(callbackName, out _);
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
