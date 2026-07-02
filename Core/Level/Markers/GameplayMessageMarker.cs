using System;
using System.Collections.Generic;
using System.Globalization;

namespace OpenGarrison.Core;

public enum GameplayMessageStyle
{
    Basic,
    Ltd,
    Dialogue,
    Chat,
    Notification,
    Notification2,
}

public enum GameplayMessageAnimation
{
    None,
    Fade,
    Typing,
    Spin,
    FromLeft,
    FromRight,
    FromBottom,
    FromTop,
    Ltd,
}

public enum GameplayMessageFont
{
    Default,
    GG2Build,
    Count,
    Timer,
}

public enum GameplayMessageAlignment
{
    Left,
    Center,
    Right,
}

public enum GameplayMessageEndMode
{
    Auto,
    Input,
    AutoOrInput,
}

public enum GameplayMessageOnEndAction
{
    None,
    FlashWhite,
    PlaySound,
    FadeOut,
    MapReset,
    MapTransition,
    MapTeleport,
}

public enum GameplayMessageChatTeam
{
    Auto,
    Red,
    Blue,
}

[Flags]
public enum GameplayMessageOnEndEffects
{
    None = 0,
    FlashWhite = 1 << 0,
    PlaySound = 1 << 1,
    FadeOut = 1 << 2,
    MapReset = 1 << 3,
    MapTransition = 1 << 4,
    MapTeleport = 1 << 5,
    Logic = 1 << 6,
}

public enum GameplayMessageDialogueBoxStyle
{
    Default,
    Blue,
    Red,
}

public readonly record struct GameplayMessageMarker(
    float X,
    float Y,
    string TriggerRef,
    string Text,
    GameplayMessageStyle Style,
    GameplayMessageDialogueBoxStyle DialogueBoxStyle,
    GameplayMessageAnimation Animation,
    GameplayMessageFont Font,
    float FontScale,
    GameplayMessageAlignment Alignment,
    GameplayMessageChatTeam ChatTeam,
    float ScreenX,
    float ScreenY,
    float Width,
    float Height,
    float DurationSeconds,
    float TypingDurationSeconds,
    GameplayMessageEndMode EndMode,
    string InputBinding,
    bool FreezeSimulation,
    string SoundName,
    string MusicName,
    bool MusicCrossfade,
    float MusicCrossfadeSeconds,
    bool MusicLoop,
    float MusicFadeAfterSeconds,
    string ImageResourceName,
    GameplayMessageAnimation ImageAnimation,
    float ImageOffsetX,
    float ImageOffsetY,
    float ImageWidth,
    float ImageHeight,
    GameplayMessageOnEndAction OnEndAction,
    GameplayMessageOnEndEffects OnEndEffects,
    string OnEndSoundName,
    float OnEndSeconds,
    string OnEndMapName,
    float OnEndTeleportX,
    float OnEndTeleportY,
    string OnEndTeleportExitRef,
    string OnEndTriggerRef,
    int OnEndTriggerNodeIndex = -1,
    int TriggerNodeIndex = -1)
{
    public bool UsesTrigger => TriggerNodeIndex >= 0;

    public bool UsesOnEndTrigger => OnEndTriggerNodeIndex >= 0;

    public GameplayMessageMarker WithTriggerNodeIndex(int triggerNodeIndex) =>
        this with { TriggerNodeIndex = triggerNodeIndex };

    public GameplayMessageMarker WithOnEndTriggerNodeIndex(int triggerNodeIndex) =>
        this with { OnEndTriggerNodeIndex = triggerNodeIndex };

    public GameplayMessageMarker WithOnEndTeleportPosition(float x, float y) =>
        this with { OnEndTeleportX = x, OnEndTeleportY = y };

    public GameplayMessageMarker WithLogicNodeIndices(int triggerNodeIndex, int onEndTriggerNodeIndex) =>
        this with { TriggerNodeIndex = triggerNodeIndex, OnEndTriggerNodeIndex = onEndTriggerNodeIndex };
}

public static class GameplayMessageMetadata
{
    public const string EntityType = "gameplayMessage";
    public const string TriggerPropertyKey = "trigger";
    public const string TextPropertyKey = "text";
    public const string StylePropertyKey = "style";
    public const string DialogueBoxPropertyKey = "dialogueBox";
    public const string AnimationPropertyKey = "animation";
    public const string FontPropertyKey = "font";
    public const string FontScalePropertyKey = "fontSize";
    public const string AlignmentPropertyKey = "align";
    public const string ChatTeamPropertyKey = "chatTeam";
    public const string ScreenXPropertyKey = "screenX";
    public const string ScreenYPropertyKey = "screenY";
    public const string WidthPropertyKey = "width";
    public const string HeightPropertyKey = "height";
    public const string DurationPropertyKey = "duration";
    public const string TypingDurationPropertyKey = "typingDuration";
    public const string EndModePropertyKey = "end";
    public const string InputPropertyKey = "input";
    public const string FreezeSimulationPropertyKey = "freeze";
    public const string SoundPropertyKey = "sound";
    public const string MusicPropertyKey = "music";
    public const string MusicCrossfadePropertyKey = "musicCrossfade";
    public const string MusicCrossfadeSecondsPropertyKey = "musicCrossfadeSeconds";
    public const string MusicLoopPropertyKey = "musicLoop";
    public const string MusicFadeAfterSecondsPropertyKey = "musicFadeAfter";
    public const string ImagePropertyKey = "image";
    public const string ImageAnimationPropertyKey = "imageAnimation";
    public const string ImageOffsetXPropertyKey = "imageX";
    public const string ImageOffsetYPropertyKey = "imageY";
    public const string ImageWidthPropertyKey = "imageW";
    public const string ImageHeightPropertyKey = "imageH";
    public const string OnEndActionPropertyKey = "onEnd";
    public const string OnEndSoundPropertyKey = "onEndSound";
    public const string OnEndSecondsPropertyKey = "onEndSeconds";
    public const string OnEndMapPropertyKey = "onEndMap";
    public const string OnEndTeleportXPropertyKey = "onEndX";
    public const string OnEndTeleportYPropertyKey = "onEndY";
    public const string OnEndTeleportExitPropertyKey = "onEndTeleportExit";
    public const string OnEndFlashPropertyKey = "onEndFlash";
    public const string OnEndPlaySoundPropertyKey = "onEndPlaySound";
    public const string OnEndFadePropertyKey = "onEndFade";
    public const string OnEndMapResetPropertyKey = "onEndMapReset";
    public const string OnEndMapTransitionPropertyKey = "onEndMapTransition";
    public const string OnEndMapTeleportPropertyKey = "onEndMapTeleport";
    public const string OnEndLogicPropertyKey = "onEndLogic";
    public const string OnEndTriggerPropertyKey = "onEndTrigger";
    public const string DefaultText = "Enter text here";
    public const float DefaultWidth = 320f;
    public const float DefaultHeight = 72f;
    public const float DefaultTypingDurationSeconds = 1.5f;
    public const float MinWidth = 16f;
    public const float MinHeight = 16f;

    public const string DefaultProperties =
        "xscale=1;yscale=1;trigger=;text=Enter text here;style=basic;dialogueBox=default;animation=none;font=default;fontSize=1;align=center;chatTeam=auto;duration=3;typingDuration=1.5;end=auto;input=any;freeze=false;sound=;music=;musicCrossfade=true;musicCrossfadeSeconds=1.5;musicLoop=true;musicFadeAfter=0;image=;imageAnimation=none;imageX=0;imageY=0;imageW=64;imageH=64;onEnd=none;onEndFlash=false;onEndPlaySound=false;onEndFade=false;onEndMapReset=false;onEndMapTransition=false;onEndMapTeleport=false;onEndLogic=false;onEndSound=;onEndSeconds=1;onEndMap=;onEndX=0;onEndY=0;onEndTeleportExit=;onEndTrigger=";

    public static bool IsGameplayMessageEntityType(string type) =>
        type.Equals(EntityType, StringComparison.OrdinalIgnoreCase);

    public static GameplayMessageMarker FromProperties(
        float x,
        float y,
        IReadOnlyDictionary<string, string> properties) =>
        FromProperties(x, y, 1f, 1f, properties);

    public static GameplayMessageMarker FromProperties(
        float x,
        float y,
        float xScale,
        float yScale,
        IReadOnlyDictionary<string, string> properties)
    {
        var usesNormalPlacement = UsesNormalPlacement(properties, xScale, yScale);
        var screenX = usesNormalPlacement
            ? x
            : ParseFloat(properties, ScreenXPropertyKey, 0.5f);
        var screenY = usesNormalPlacement
            ? y
            : ParseFloat(properties, ScreenYPropertyKey, 0.25f);
        var width = usesNormalPlacement
            ? ResolveNormalPlacementWidth(xScale)
            : ParsePositiveFloat(properties, WidthPropertyKey, DefaultWidth, MinWidth, 4096f);
        var height = usesNormalPlacement
            ? ResolveNormalPlacementHeight(yScale)
            : ParsePositiveFloat(properties, HeightPropertyKey, DefaultHeight, MinHeight, 4096f);

        var onEndAction = ParseOnEndAction(properties);

        return new(
            x,
            y,
            ReadProperty(properties, TriggerPropertyKey, string.Empty),
            ReadProperty(properties, TextPropertyKey, DefaultText),
            ParseStyle(properties),
            ParseDialogueBoxStyle(properties),
            ParseAnimation(properties),
            ParseFont(properties),
            ParsePositiveFloat(properties, FontScalePropertyKey, 1f, 0.1f, 8f),
            ParseAlignment(properties),
            ParseChatTeam(properties),
            screenX,
            screenY,
            width,
            height,
            ParsePositiveFloat(properties, DurationPropertyKey, 3f, 0.1f, 3600f),
            ParsePositiveFloat(properties, TypingDurationPropertyKey, DefaultTypingDurationSeconds, 0.05f, 3600f),
            ParseEndMode(properties),
            ReadProperty(properties, InputPropertyKey, "any"),
            ParseBool(properties, FreezeSimulationPropertyKey, false),
            ReadProperty(properties, SoundPropertyKey, string.Empty),
            ReadProperty(properties, MusicPropertyKey, string.Empty),
            ParseMusicCrossfade(properties),
            ParsePositiveFloat(properties, MusicCrossfadeSecondsPropertyKey, 1.5f, 0f, 60f),
            ParseMusicLoop(properties),
            ParsePositiveFloat(properties, MusicFadeAfterSecondsPropertyKey, 0f, 0f, 3600f),
            ReadProperty(properties, ImagePropertyKey, string.Empty),
            ParseImageAnimation(properties),
            ParseFloat(properties, ImageOffsetXPropertyKey, 0f),
            ParseFloat(properties, ImageOffsetYPropertyKey, 0f),
            ParsePositiveFloat(properties, ImageWidthPropertyKey, 64f, 1f, 4096f),
            ParsePositiveFloat(properties, ImageHeightPropertyKey, 64f, 1f, 4096f),
            onEndAction,
            ParseOnEndEffects(properties, onEndAction),
            ReadProperty(properties, OnEndSoundPropertyKey, string.Empty),
            ParsePositiveFloat(properties, OnEndSecondsPropertyKey, 1f, 0.05f, 3600f),
            ReadProperty(properties, OnEndMapPropertyKey, string.Empty),
            ParseFloat(properties, OnEndTeleportXPropertyKey, 0f),
            ParseFloat(properties, OnEndTeleportYPropertyKey, 0f),
            ReadProperty(properties, OnEndTeleportExitPropertyKey, string.Empty),
            ReadProperty(properties, OnEndTriggerPropertyKey, string.Empty));
    }

    public static GameplayMessageStyle ParseStyle(IReadOnlyDictionary<string, string>? properties) =>
        ReadOptional(properties, StylePropertyKey) switch
        {
            string value when value.Equals("ltd", StringComparison.OrdinalIgnoreCase) => GameplayMessageStyle.Ltd,
            string value when value.Equals("dialogue", StringComparison.OrdinalIgnoreCase)
                || value.Equals("dialog", StringComparison.OrdinalIgnoreCase) => GameplayMessageStyle.Dialogue,
            string value when value.Equals("chat", StringComparison.OrdinalIgnoreCase)
                || value.Equals("chatPopup", StringComparison.OrdinalIgnoreCase) => GameplayMessageStyle.Chat,
            string value when value.Equals("notification", StringComparison.OrdinalIgnoreCase)
                || value.Equals("notice", StringComparison.OrdinalIgnoreCase) => GameplayMessageStyle.Notification,
            string value when value.Equals("notification2", StringComparison.OrdinalIgnoreCase)
                || value.Equals("notice2", StringComparison.OrdinalIgnoreCase)
                || value.Equals("bar", StringComparison.OrdinalIgnoreCase) => GameplayMessageStyle.Notification2,
            _ => GameplayMessageStyle.Basic,
        };

    public static GameplayMessageDialogueBoxStyle ParseDialogueBoxStyle(IReadOnlyDictionary<string, string>? properties) =>
        ReadOptional(properties, DialogueBoxPropertyKey) switch
        {
            string value when value.Equals("blue", StringComparison.OrdinalIgnoreCase)
                || value.Equals("2", StringComparison.OrdinalIgnoreCase)
                || value.Equals("dialogue2", StringComparison.OrdinalIgnoreCase) => GameplayMessageDialogueBoxStyle.Blue,
            string value when value.Equals("red", StringComparison.OrdinalIgnoreCase)
                || value.Equals("3", StringComparison.OrdinalIgnoreCase)
                || value.Equals("dialogue3", StringComparison.OrdinalIgnoreCase) => GameplayMessageDialogueBoxStyle.Red,
            _ => GameplayMessageDialogueBoxStyle.Default,
        };

    public static GameplayMessageAnimation ParseAnimation(IReadOnlyDictionary<string, string>? properties) =>
        ParseAnimation(properties, AnimationPropertyKey, allowStyleFallback: true);

    public static GameplayMessageAnimation ParseImageAnimation(IReadOnlyDictionary<string, string>? properties) =>
        ParseAnimation(properties, ImageAnimationPropertyKey, allowStyleFallback: false);

    private static GameplayMessageAnimation ParseAnimation(
        IReadOnlyDictionary<string, string>? properties,
        string propertyKey,
        bool allowStyleFallback)
    {
        var value = ReadOptional(properties, propertyKey)
            ?? (allowStyleFallback ? ReadOptional(properties, StylePropertyKey) : null);
        return value switch
        {
            string animation when animation.Equals("fade", StringComparison.OrdinalIgnoreCase) => GameplayMessageAnimation.Fade,
            string animation when animation.Equals("typing", StringComparison.OrdinalIgnoreCase) => GameplayMessageAnimation.Typing,
            string animation when animation.Equals("spin", StringComparison.OrdinalIgnoreCase) => GameplayMessageAnimation.Spin,
            string animation when animation.Equals("fromLeft", StringComparison.OrdinalIgnoreCase)
                || animation.Equals("left", StringComparison.OrdinalIgnoreCase) => GameplayMessageAnimation.FromLeft,
            string animation when animation.Equals("fromRight", StringComparison.OrdinalIgnoreCase)
                || animation.Equals("right", StringComparison.OrdinalIgnoreCase) => GameplayMessageAnimation.FromRight,
            string animation when animation.Equals("fromBottom", StringComparison.OrdinalIgnoreCase)
                || animation.Equals("bottom", StringComparison.OrdinalIgnoreCase) => GameplayMessageAnimation.FromBottom,
            string animation when animation.Equals("fromTop", StringComparison.OrdinalIgnoreCase)
                || animation.Equals("top", StringComparison.OrdinalIgnoreCase) => GameplayMessageAnimation.FromTop,
            string animation when animation.Equals("ltd", StringComparison.OrdinalIgnoreCase)
                || animation.Equals("lastToDie", StringComparison.OrdinalIgnoreCase) => GameplayMessageAnimation.Ltd,
            _ => GameplayMessageAnimation.None,
        };
    }

    public static GameplayMessageFont ParseFont(IReadOnlyDictionary<string, string>? properties) =>
        ReadOptional(properties, FontPropertyKey) switch
        {
            string value when value.Equals("count", StringComparison.OrdinalIgnoreCase) => GameplayMessageFont.Count,
            string value when value.Equals("timer", StringComparison.OrdinalIgnoreCase) => GameplayMessageFont.Timer,
            string value when value.Equals("gg2build", StringComparison.OrdinalIgnoreCase)
                || value.Equals("build", StringComparison.OrdinalIgnoreCase)
                || value.Equals("menu", StringComparison.OrdinalIgnoreCase) => GameplayMessageFont.GG2Build,
            _ => GameplayMessageFont.Default,
        };

    public static GameplayMessageAlignment ParseAlignment(IReadOnlyDictionary<string, string>? properties) =>
        ReadOptional(properties, AlignmentPropertyKey) switch
        {
            string value when value.Equals("left", StringComparison.OrdinalIgnoreCase) => GameplayMessageAlignment.Left,
            string value when value.Equals("right", StringComparison.OrdinalIgnoreCase) => GameplayMessageAlignment.Right,
            _ => GameplayMessageAlignment.Center,
        };

    public static GameplayMessageChatTeam ParseChatTeam(IReadOnlyDictionary<string, string>? properties) =>
        ReadOptional(properties, ChatTeamPropertyKey) switch
        {
            string value when value.Equals("red", StringComparison.OrdinalIgnoreCase) => GameplayMessageChatTeam.Red,
            string value when value.Equals("blue", StringComparison.OrdinalIgnoreCase) => GameplayMessageChatTeam.Blue,
            _ => GameplayMessageChatTeam.Auto,
        };

    public static GameplayMessageEndMode ParseEndMode(IReadOnlyDictionary<string, string>? properties) =>
        ReadOptional(properties, EndModePropertyKey) switch
        {
            string value when value.Equals("input", StringComparison.OrdinalIgnoreCase)
                || value.Equals("manual", StringComparison.OrdinalIgnoreCase) => GameplayMessageEndMode.Input,
            string value when value.Equals("both", StringComparison.OrdinalIgnoreCase)
                || value.Equals("autoOrInput", StringComparison.OrdinalIgnoreCase) => GameplayMessageEndMode.AutoOrInput,
            _ => GameplayMessageEndMode.Auto,
        };

    public static bool ParseMusicCrossfade(IReadOnlyDictionary<string, string> properties) =>
        ParseBool(properties, MusicCrossfadePropertyKey, true);

    public static bool ParseMusicLoop(IReadOnlyDictionary<string, string> properties) =>
        ParseBool(properties, MusicLoopPropertyKey, true);

    public static GameplayMessageOnEndAction ParseOnEndAction(IReadOnlyDictionary<string, string>? properties) =>
        ReadOptional(properties, OnEndActionPropertyKey) switch
        {
            string value when value.Equals("flashWhite", StringComparison.OrdinalIgnoreCase)
                || value.Equals("flash", StringComparison.OrdinalIgnoreCase) => GameplayMessageOnEndAction.FlashWhite,
            string value when value.Equals("playSound", StringComparison.OrdinalIgnoreCase)
                || value.Equals("sound", StringComparison.OrdinalIgnoreCase) => GameplayMessageOnEndAction.PlaySound,
            string value when value.Equals("fadeOut", StringComparison.OrdinalIgnoreCase)
                || value.Equals("fade", StringComparison.OrdinalIgnoreCase) => GameplayMessageOnEndAction.FadeOut,
            string value when value.Equals("mapReset", StringComparison.OrdinalIgnoreCase)
                || value.Equals("reset", StringComparison.OrdinalIgnoreCase) => GameplayMessageOnEndAction.MapReset,
            string value when value.Equals("mapTransition", StringComparison.OrdinalIgnoreCase)
                || value.Equals("transition", StringComparison.OrdinalIgnoreCase)
                || value.Equals("map", StringComparison.OrdinalIgnoreCase) => GameplayMessageOnEndAction.MapTransition,
            string value when value.Equals("mapTeleport", StringComparison.OrdinalIgnoreCase)
                || value.Equals("teleport", StringComparison.OrdinalIgnoreCase) => GameplayMessageOnEndAction.MapTeleport,
            _ => GameplayMessageOnEndAction.None,
        };

    public static GameplayMessageOnEndEffects ParseOnEndEffects(IReadOnlyDictionary<string, string>? properties) =>
        ParseOnEndEffects(properties, ParseOnEndAction(properties));

    public static GameplayMessageOnEndEffects ParseOnEndEffects(
        IReadOnlyDictionary<string, string>? properties,
        GameplayMessageOnEndAction legacyAction)
    {
        var effects = GameplayMessageOnEndEffects.None;
        AddEffectIfEnabled(ref effects, properties, OnEndFlashPropertyKey, GameplayMessageOnEndEffects.FlashWhite, legacyAction);
        AddEffectIfEnabled(ref effects, properties, OnEndPlaySoundPropertyKey, GameplayMessageOnEndEffects.PlaySound, legacyAction);
        AddEffectIfEnabled(ref effects, properties, OnEndFadePropertyKey, GameplayMessageOnEndEffects.FadeOut, legacyAction);
        AddEffectIfEnabled(ref effects, properties, OnEndMapResetPropertyKey, GameplayMessageOnEndEffects.MapReset, legacyAction);
        AddEffectIfEnabled(ref effects, properties, OnEndMapTransitionPropertyKey, GameplayMessageOnEndEffects.MapTransition, legacyAction);
        AddEffectIfEnabled(ref effects, properties, OnEndMapTeleportPropertyKey, GameplayMessageOnEndEffects.MapTeleport, legacyAction);
        AddEffectIfEnabled(ref effects, properties, OnEndLogicPropertyKey, GameplayMessageOnEndEffects.Logic, legacyAction);
        return effects;
    }

    private static void AddEffectIfEnabled(
        ref GameplayMessageOnEndEffects effects,
        IReadOnlyDictionary<string, string>? properties,
        string propertyKey,
        GameplayMessageOnEndEffects effect,
        GameplayMessageOnEndAction legacyAction)
    {
        if (ParseOnEndEffectEnabled(properties, propertyKey, effect, legacyAction))
        {
            effects |= effect;
        }
    }

    public static bool ParseOnEndEffectEnabled(
        IReadOnlyDictionary<string, string>? properties,
        string propertyKey,
        GameplayMessageOnEndEffects effect)
    {
        return ParseOnEndEffectEnabled(properties, propertyKey, effect, ParseOnEndAction(properties));
    }

    private static bool ParseOnEndEffectEnabled(
        IReadOnlyDictionary<string, string>? properties,
        string propertyKey,
        GameplayMessageOnEndEffects effect,
        GameplayMessageOnEndAction legacyAction)
    {
        if (properties is not null && properties.TryGetValue(propertyKey, out var raw))
        {
            return DamageTriggerMetadata.ParseBoolProperty(raw);
        }

        return LegacyOnEndActionToEffect(legacyAction).HasFlag(effect);
    }

    private static GameplayMessageOnEndEffects LegacyOnEndActionToEffect(GameplayMessageOnEndAction action) =>
        action switch
        {
            GameplayMessageOnEndAction.FlashWhite => GameplayMessageOnEndEffects.FlashWhite,
            GameplayMessageOnEndAction.PlaySound => GameplayMessageOnEndEffects.PlaySound,
            GameplayMessageOnEndAction.FadeOut => GameplayMessageOnEndEffects.FadeOut,
            GameplayMessageOnEndAction.MapReset => GameplayMessageOnEndEffects.MapReset,
            GameplayMessageOnEndAction.MapTransition => GameplayMessageOnEndEffects.MapTransition,
            GameplayMessageOnEndAction.MapTeleport => GameplayMessageOnEndEffects.MapTeleport,
            _ => GameplayMessageOnEndEffects.None,
        };

    private static bool UsesNormalPlacement(IReadOnlyDictionary<string, string> properties, float xScale, float yScale) =>
        HasExplicitPlacementScale(xScale)
        || HasExplicitPlacementScale(yScale)
        || HasProperty(properties, "xscale")
        || HasProperty(properties, "yscale")
        || !HasAnyLegacyPlacementProperty(properties);

    private static bool HasExplicitPlacementScale(float scale) =>
        float.IsFinite(scale)
        && MathF.Abs(MathF.Abs(scale) - 1f) > 0.0001f;

    private static bool HasAnyLegacyPlacementProperty(IReadOnlyDictionary<string, string> properties) =>
        HasProperty(properties, ScreenXPropertyKey)
        || HasProperty(properties, ScreenYPropertyKey)
        || HasProperty(properties, WidthPropertyKey)
        || HasProperty(properties, HeightPropertyKey);

    private static bool HasProperty(IReadOnlyDictionary<string, string> properties, string key) =>
        properties.TryGetValue(key, out var value)
        && !string.IsNullOrWhiteSpace(value);

    private static float ResolveNormalPlacementWidth(float xScale) =>
        float.Clamp(DefaultWidth * NormalizePlacementScale(xScale), MinWidth, 4096f);

    private static float ResolveNormalPlacementHeight(float yScale) =>
        float.Clamp(DefaultHeight * NormalizePlacementScale(yScale), MinHeight, 4096f);

    private static float NormalizePlacementScale(float scale) =>
        float.IsFinite(scale) && MathF.Abs(scale) > 0f ? MathF.Abs(scale) : 1f;

    public static string CycleStylePropertyValue(string? value) =>
        ToStylePropertyValue(Next(ParseStyle(ToDictionary(StylePropertyKey, value)), GetStyleCycle()));

    public static string CycleAnimationPropertyValue(string? value) =>
        ToAnimationPropertyValue(Next(ParseAnimation(ToDictionary(AnimationPropertyKey, value)), GetAnimationCycle()));

    public static string CycleDialogueBoxStylePropertyValue(string? value) =>
        ToDialogueBoxStylePropertyValue(Next(ParseDialogueBoxStyle(ToDictionary(DialogueBoxPropertyKey, value)), GetDialogueBoxStyleCycle()));

    public static string CycleImageAnimationPropertyValue(string? value) =>
        ToAnimationPropertyValue(Next(ParseImageAnimation(ToDictionary(ImageAnimationPropertyKey, value)), GetImageAnimationCycle()));

    public static string CycleFontPropertyValue(string? value) =>
        ToFontPropertyValue(Next(ParseFont(ToDictionary(FontPropertyKey, value)), GetFontCycle()));

    public static string CycleAlignmentPropertyValue(string? value) =>
        ToAlignmentPropertyValue(Next(ParseAlignment(ToDictionary(AlignmentPropertyKey, value)), GetAlignmentCycle()));

    public static string CycleChatTeamPropertyValue(string? value) =>
        ToChatTeamPropertyValue(Next(ParseChatTeam(ToDictionary(ChatTeamPropertyKey, value)), GetChatTeamCycle()));

    public static string CycleEndModePropertyValue(string? value) =>
        ToEndModePropertyValue(Next(ParseEndMode(ToDictionary(EndModePropertyKey, value)), GetEndModeCycle()));

    public static string CycleMusicCrossfadePropertyValue(string? value) =>
        ParseBool(ToDictionary(MusicCrossfadePropertyKey, value), MusicCrossfadePropertyKey, true) ? "false" : "true";

    public static string CycleMusicLoopPropertyValue(string? value) =>
        ParseBool(ToDictionary(MusicLoopPropertyKey, value), MusicLoopPropertyKey, true) ? "false" : "true";

    public static string CycleOnEndActionPropertyValue(string? value) =>
        ToOnEndActionPropertyValue(Next(ParseOnEndAction(ToDictionary(OnEndActionPropertyKey, value)), GetOnEndActionCycle()));

    public static string ToStylePropertyValue(GameplayMessageStyle style) =>
        style switch
        {
            GameplayMessageStyle.Ltd => "ltd",
            GameplayMessageStyle.Dialogue => "dialogue",
            GameplayMessageStyle.Chat => "chat",
            GameplayMessageStyle.Notification => "notification",
            GameplayMessageStyle.Notification2 => "notification2",
            _ => "basic",
        };

    public static string ToAnimationPropertyValue(GameplayMessageAnimation animation) =>
        animation switch
        {
            GameplayMessageAnimation.Fade => "fade",
            GameplayMessageAnimation.Typing => "typing",
            GameplayMessageAnimation.Spin => "spin",
            GameplayMessageAnimation.FromLeft => "fromLeft",
            GameplayMessageAnimation.FromRight => "fromRight",
            GameplayMessageAnimation.FromBottom => "fromBottom",
            GameplayMessageAnimation.FromTop => "fromTop",
            GameplayMessageAnimation.Ltd => "ltd",
            _ => "none",
        };

    public static string ToDialogueBoxStylePropertyValue(GameplayMessageDialogueBoxStyle style) =>
        style switch
        {
            GameplayMessageDialogueBoxStyle.Blue => "blue",
            GameplayMessageDialogueBoxStyle.Red => "red",
            _ => "default",
        };

    public static string ToFontPropertyValue(GameplayMessageFont font) =>
        font switch
        {
            GameplayMessageFont.Count => "count",
            GameplayMessageFont.Timer => "timer",
            GameplayMessageFont.GG2Build => "gg2build",
            _ => "default",
        };

    public static string ToAlignmentPropertyValue(GameplayMessageAlignment alignment) =>
        alignment switch
        {
            GameplayMessageAlignment.Left => "left",
            GameplayMessageAlignment.Right => "right",
            _ => "center",
        };

    public static string ToChatTeamPropertyValue(GameplayMessageChatTeam team) =>
        team switch
        {
            GameplayMessageChatTeam.Red => "red",
            GameplayMessageChatTeam.Blue => "blue",
            _ => "auto",
        };

    public static string ToOnEndActionPropertyValue(GameplayMessageOnEndAction action) =>
        action switch
        {
            GameplayMessageOnEndAction.FlashWhite => "flashWhite",
            GameplayMessageOnEndAction.PlaySound => "playSound",
            GameplayMessageOnEndAction.FadeOut => "fadeOut",
            GameplayMessageOnEndAction.MapReset => "mapReset",
            GameplayMessageOnEndAction.MapTransition => "mapTransition",
            GameplayMessageOnEndAction.MapTeleport => "mapTeleport",
            _ => "none",
        };

    public static string ToEndModePropertyValue(GameplayMessageEndMode mode) =>
        mode switch
        {
            GameplayMessageEndMode.Input => "input",
            GameplayMessageEndMode.AutoOrInput => "both",
            _ => "auto",
        };

    public static string GetStyleDisplayLabel(string? value) =>
        ParseStyle(ToDictionary(StylePropertyKey, value)) switch
        {
            GameplayMessageStyle.Ltd => "LTD",
            GameplayMessageStyle.Dialogue => "Dialogue",
            GameplayMessageStyle.Chat => "Chat",
            GameplayMessageStyle.Notification => "Notification",
            GameplayMessageStyle.Notification2 => "Notification 2",
            _ => "Basic",
        };

    public static string GetAnimationDisplayLabel(string? value) =>
        ParseAnimation(ToDictionary(AnimationPropertyKey, value)) switch
        {
            GameplayMessageAnimation.Fade => "Fade",
            GameplayMessageAnimation.Typing => "Typing",
            GameplayMessageAnimation.Spin => "Spin in",
            GameplayMessageAnimation.FromLeft => "From left",
            GameplayMessageAnimation.FromRight => "From right",
            GameplayMessageAnimation.FromBottom => "From bottom",
            GameplayMessageAnimation.FromTop => "From top",
            GameplayMessageAnimation.Ltd => "LTD",
            _ => "None",
        };

    public static string GetImageAnimationDisplayLabel(string? value) =>
        ParseImageAnimation(ToDictionary(ImageAnimationPropertyKey, value)) switch
        {
            GameplayMessageAnimation.Fade => "Fade",
            GameplayMessageAnimation.Spin => "Spin in",
            GameplayMessageAnimation.FromLeft => "From left",
            GameplayMessageAnimation.FromRight => "From right",
            GameplayMessageAnimation.FromBottom => "From bottom",
            GameplayMessageAnimation.FromTop => "From top",
            GameplayMessageAnimation.Ltd => "LTD",
            _ => "None",
        };

    public static string GetDialogueBoxStyleDisplayLabel(string? value) =>
        ParseDialogueBoxStyle(ToDictionary(DialogueBoxPropertyKey, value)) switch
        {
            GameplayMessageDialogueBoxStyle.Blue => "Dialogue box 2",
            GameplayMessageDialogueBoxStyle.Red => "Dialogue box 3",
            _ => "Default",
        };

    public static string GetFontDisplayLabel(string? value) =>
        ParseFont(ToDictionary(FontPropertyKey, value)) switch
        {
            GameplayMessageFont.Count => "Count",
            GameplayMessageFont.Timer => "Timer",
            GameplayMessageFont.GG2Build => "GG2Build",
            _ => "Default",
        };

    public static string GetAlignmentDisplayLabel(string? value) =>
        ParseAlignment(ToDictionary(AlignmentPropertyKey, value)) switch
        {
            GameplayMessageAlignment.Left => "Left",
            GameplayMessageAlignment.Right => "Right",
            _ => "Center",
        };

    public static string GetChatTeamDisplayLabel(string? value) =>
        ParseChatTeam(ToDictionary(ChatTeamPropertyKey, value)) switch
        {
            GameplayMessageChatTeam.Red => "Red",
            GameplayMessageChatTeam.Blue => "Blue",
            _ => "Auto",
        };

    public static string GetOnEndActionDisplayLabel(string? value) =>
        ParseOnEndAction(ToDictionary(OnEndActionPropertyKey, value)) switch
        {
            GameplayMessageOnEndAction.FlashWhite => "Flash white",
            GameplayMessageOnEndAction.PlaySound => "Play sound",
            GameplayMessageOnEndAction.FadeOut => "Fade out",
            GameplayMessageOnEndAction.MapReset => "Map reset",
            GameplayMessageOnEndAction.MapTransition => "Map transition",
            GameplayMessageOnEndAction.MapTeleport => "Map teleport",
            _ => "None",
        };

    public static string GetEndModeDisplayLabel(string? value) =>
        ParseEndMode(ToDictionary(EndModePropertyKey, value)) switch
        {
            GameplayMessageEndMode.Input => "Input",
            GameplayMessageEndMode.AutoOrInput => "Auto/input",
            _ => "Auto",
        };

    private static string? ReadOptional(IReadOnlyDictionary<string, string>? properties, string key) =>
        properties is not null
        && properties.TryGetValue(key, out var value)
        && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string ReadProperty(IReadOnlyDictionary<string, string> properties, string key, string fallback) =>
        ReadOptional(properties, key) ?? fallback;

    private static bool ParseBool(IReadOnlyDictionary<string, string> properties, string key, bool fallback) =>
        properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? DamageTriggerMetadata.ParseBoolProperty(value)
            : fallback;

    private static float ParseFloat(IReadOnlyDictionary<string, string> properties, string key, float fallback)
    {
        if (properties.TryGetValue(key, out var value)
            && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            && float.IsFinite(parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static float ParsePositiveFloat(
        IReadOnlyDictionary<string, string> properties,
        string key,
        float fallback,
        float min,
        float max)
    {
        return float.Clamp(ParseFloat(properties, key, fallback), min, max);
    }

    private static Dictionary<string, string> ToDictionary(string key, string? value) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            [key] = value ?? string.Empty,
        };

    private static T Next<T>(T current, IReadOnlyList<T> values)
        where T : struct, Enum
    {
        for (var index = 0; index < values.Count; index += 1)
        {
            if (EqualityComparer<T>.Default.Equals(values[index], current))
            {
                return values[(index + 1) % values.Count];
            }
        }

        return values.Count > 0 ? values[0] : current;
    }

    private static GameplayMessageStyle[] GetStyleCycle() =>
    [
        GameplayMessageStyle.Basic,
        GameplayMessageStyle.Ltd,
        GameplayMessageStyle.Dialogue,
        GameplayMessageStyle.Chat,
        GameplayMessageStyle.Notification,
        GameplayMessageStyle.Notification2,
    ];

    private static GameplayMessageAnimation[] GetAnimationCycle() =>
    [
        GameplayMessageAnimation.None,
        GameplayMessageAnimation.Fade,
        GameplayMessageAnimation.Typing,
        GameplayMessageAnimation.Spin,
        GameplayMessageAnimation.FromLeft,
        GameplayMessageAnimation.FromRight,
        GameplayMessageAnimation.FromBottom,
        GameplayMessageAnimation.FromTop,
        GameplayMessageAnimation.Ltd,
    ];

    private static GameplayMessageAnimation[] GetImageAnimationCycle() =>
    [
        GameplayMessageAnimation.None,
        GameplayMessageAnimation.Fade,
        GameplayMessageAnimation.Spin,
        GameplayMessageAnimation.FromLeft,
        GameplayMessageAnimation.FromRight,
        GameplayMessageAnimation.FromBottom,
        GameplayMessageAnimation.FromTop,
        GameplayMessageAnimation.Ltd,
    ];

    private static GameplayMessageDialogueBoxStyle[] GetDialogueBoxStyleCycle() =>
    [
        GameplayMessageDialogueBoxStyle.Default,
        GameplayMessageDialogueBoxStyle.Blue,
        GameplayMessageDialogueBoxStyle.Red,
    ];

    private static GameplayMessageFont[] GetFontCycle() =>
    [
        GameplayMessageFont.Default,
        GameplayMessageFont.GG2Build,
        GameplayMessageFont.Count,
        GameplayMessageFont.Timer,
    ];

    private static GameplayMessageAlignment[] GetAlignmentCycle() =>
    [
        GameplayMessageAlignment.Left,
        GameplayMessageAlignment.Center,
        GameplayMessageAlignment.Right,
    ];

    private static GameplayMessageChatTeam[] GetChatTeamCycle() =>
    [
        GameplayMessageChatTeam.Auto,
        GameplayMessageChatTeam.Red,
        GameplayMessageChatTeam.Blue,
    ];

    private static GameplayMessageEndMode[] GetEndModeCycle() =>
    [
        GameplayMessageEndMode.Auto,
        GameplayMessageEndMode.Input,
        GameplayMessageEndMode.AutoOrInput,
    ];

    private static GameplayMessageOnEndAction[] GetOnEndActionCycle() =>
    [
        GameplayMessageOnEndAction.None,
        GameplayMessageOnEndAction.FlashWhite,
        GameplayMessageOnEndAction.PlaySound,
        GameplayMessageOnEndAction.FadeOut,
        GameplayMessageOnEndAction.MapReset,
        GameplayMessageOnEndAction.MapTransition,
        GameplayMessageOnEndAction.MapTeleport,
    ];
}
