#nullable enable

using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using OpenGarrison.Core;

namespace OpenGarrison.Client;

/// <summary>
/// Soft team-coloured glows drawn on the art itself: players being healed (medic or
/// dispenser), stronger while ubered; engineer buildings; and grenades. Each glow fills
/// the sprite's own silhouette with additive team colour plus a faint one-pixel bleed,
/// and the same sources light their surroundings on lit maps (see
/// <see cref="AddTeamGlowLights"/>).
/// </summary>
public partial class Game1
{
    /// <summary>Glow strength for a heal target; uber and buildings are relative to this.</summary>
    private const float TeamGlowHealLevel = 0.75f;
    private const float TeamGlowUberLevel = 1.25f;
    private const float TeamGlowBuildingLevel = 0.55f;
    private const float TeamGlowGrenadeLevel = 0.8f;

    /// <summary>Additive team colour across the silhouette at level 1.</summary>
    private const float TeamGlowFillWeight = 0.1f;

    /// <summary>
    /// Additive colour per outline offset at level 1. The eight offsets overlap inside the
    /// silhouette and only partly outside it, so this reads as a soft edge bleed.
    /// </summary>
    private const float TeamGlowRimWeight = 0.035f;

    /// <summary>How fast a heal glow eases in and out, in levels per second.</summary>
    private const float TeamGlowRisePerSecond = 5f;
    private const float TeamGlowFallPerSecond = 2.5f;

    private static readonly Vector2[] TeamGlowCenterOffset = { Vector2.Zero };

    /// <summary>Pure additive colour: the sprite's alpha mask times the tint, added on top.</summary>
    private static readonly BlendState TeamGlowBlend = new()
    {
        ColorSourceBlend = Blend.One,
        ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.Zero,
        AlphaDestinationBlend = Blend.One,
    };

    private readonly Dictionary<int, (float Level, double Time)> _healGlowLevels = new();
    private readonly List<int> _staleHealGlows = new();
    private double _healGlowLastPrune;

    private static Color GetTeamGlowColor(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? new Color(70, 145, 255) : new Color(255, 70, 50);
    }

    private static Color GetTeamGlowTint(Color color, float strength)
    {
        strength = Math.Clamp(strength, 0f, 1f);
        return new Color((int)(color.R * strength), (int)(color.G * strength), (int)(color.B * strength), 255);
    }

    private bool AreTeamGlowsEnabled => !UsesClassicPlayerVisuals;

    /// <summary>
    /// A map lighting setting (0-100) as a glow multiplier. Unlit maps have no lighting
    /// settings, so their glows use the given default (full strength unless stated).
    /// </summary>
    private float GetTeamGlowSettingScale(int percent, int unlitPercent = 100)
    {
        return Math.Clamp((_world.Level.Lighting.IsActive ? percent : unlitPercent) / 100f, 0f, 1f);
    }

    private float HealGlowSettingScale => GetTeamGlowSettingScale(_world.Level.Lighting.HealGlow, MapLightingMetadata.DefaultHealGlow);

    /// <summary>Grenades follow the map's effect light strength, like rockets and flames.</summary>
    private float GrenadeGlowLevel => TeamGlowGrenadeLevel * GetTeamGlowSettingScale(_world.Level.Lighting.EffectLights);

    private float GetTeamGlowPulse(float seed)
    {
        return 1f + (0.15f * MathF.Sin((float)(_weatherClock.Elapsed.TotalSeconds * 6.5d) + seed));
    }

    /// <summary>
    /// How strongly a player glows this frame: eases toward full while a medic or a
    /// dispenser heals them, and above that while ubered.
    /// </summary>
    private float GetPlayerHealGlowLevel(PlayerEntity player)
    {
        var now = _weatherClock.Elapsed.TotalSeconds;
        var target = GetPlayerHealGlowTarget(player);
        var hadState = _healGlowLevels.TryGetValue(player.Id, out var state);
        var level = state.Level;
        if (!hadState)
        {
            level = 0f;
        }
        else
        {
            var step = (float)Math.Clamp(now - state.Time, 0d, 0.25d);
            level = level < target
                ? MathF.Min(target, level + (TeamGlowRisePerSecond * step))
                : MathF.Max(target, level - (TeamGlowFallPerSecond * step));
        }

        if (level > 0f || target > 0f)
        {
            _healGlowLevels[player.Id] = (level, now);
        }
        else if (hadState)
        {
            _healGlowLevels.Remove(player.Id);
        }

        PruneHealGlowLevels(now);
        return player.IsUbered ? level * GetTeamGlowPulse(player.Id * 0.9f) : level;
    }

    private float GetPlayerHealGlowTarget(PlayerEntity player)
    {
        if (!player.IsAlive)
        {
            return 0f;
        }

        if (player.IsUbered)
        {
            return TeamGlowUberLevel;
        }

        foreach (var medic in EnumerateRenderablePlayers())
        {
            if (ReferenceEquals(medic, player)
                || !medic.IsAlive
                || !medic.IsMedicHealing
                || medic.Team != player.Team
                || medic.IsExperimentalEngineerFreezeRayPresented
                || medic.IsExperimentalEngineerEssenceExtractorPresented)
            {
                continue;
            }

            if (medic.MedicHealTargetId == player.Id
                || medic.ExperimentalAdditionalMedicBeamTargetPlayerId1 == player.Id
                || medic.ExperimentalAdditionalMedicBeamTargetPlayerId2 == player.Id)
            {
                return TeamGlowHealLevel;
            }
        }

        var target = 0f;
        foreach (var dispenser in _world.Sentries)
        {
            if (dispenser.IsDispenser
                && dispenser.IsBuilt
                && dispenser.Team == player.Team
                && _dispenserBeamAlphas.TryGetValue((dispenser.Id, player.Id), out var fade))
            {
                target = MathF.Max(target, TeamGlowHealLevel * Math.Clamp(fade, 0f, 1f));
            }
        }

        return target;
    }

    private void PruneHealGlowLevels(double now)
    {
        if (now - _healGlowLastPrune < 2d)
        {
            return;
        }

        _healGlowLastPrune = now;
        _staleHealGlows.Clear();
        foreach (var (id, state) in _healGlowLevels)
        {
            if (now - state.Time > 2d)
            {
                _staleHealGlows.Add(id);
            }
        }

        foreach (var id in _staleHealGlows)
        {
            _healGlowLevels.Remove(id);
        }
    }

    private void BeginTeamGlowBatch()
    {
        _spriteBatch.End();
        _spriteBatch.Begin(
            SpriteSortMode.Deferred,
            TeamGlowBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            _gameplayWorldRasterizerState,
            null,
            GetActiveGameplayWorldSpriteBatchTransform());
    }

    private void EndTeamGlowBatch()
    {
        _spriteBatch.End();
        _spriteBatch.Begin(
            samplerState: SamplerState.PointClamp,
            rasterizerState: _gameplayWorldRasterizerState,
            transformMatrix: GetActiveGameplayWorldSpriteBatchTransform());
    }

    /// <summary>
    /// Heal / uber glow over a player's body and weapon, drawn right after them. Fades
    /// with the player's visibility, so cloaked spies stay hidden.
    /// </summary>
    internal void DrawPlayerHealGlow(
        PlayerEntity player,
        Vector2 renderPosition,
        Vector2 cameraPosition,
        float visibilityAlpha,
        PlayerBodySpriteSelection bodySelection,
        bool drawWeapon)
    {
        if (!AreTeamGlowsEnabled)
        {
            return;
        }

        var level = GetPlayerHealGlowLevel(player) * Math.Clamp(visibilityAlpha, 0f, 1f) * HealGlowSettingScale;
        if (level <= 0.01f)
        {
            return;
        }

        var color = GetTeamGlowColor(player.Team);
        var rim = GetTeamGlowTint(color, TeamGlowRimWeight * level);
        var fill = GetTeamGlowTint(color, TeamGlowFillWeight * level);
        var weaponDefinition = _gameplayWeaponRenderController.GetWeaponRenderDefinitionProxy(player);
        drawWeapon = drawWeapon && !weaponDefinition.UseTorsoReplacement && !weaponDefinition.HasCompanionTorso;

        BeginTeamGlowBatch();
        _gameplayPlayerSpriteRenderController.TryDrawPlayerHealingOutlineAtPosition(player, renderPosition, cameraPosition, rim, bodySelection);
        _gameplayPlayerSpriteRenderController.TryDrawPlayerHealingOutlineAtPosition(player, renderPosition, cameraPosition, fill, bodySelection, TeamGlowCenterOffset);
        if (drawWeapon)
        {
            _gameplayWeaponRenderController.TryDrawPlayerHealingWeaponOutlineAtPosition(player, renderPosition, cameraPosition, rim, bodySelection);
            _gameplayWeaponRenderController.TryDrawPlayerHealingWeaponOutlineAtPosition(player, renderPosition, cameraPosition, fill, bodySelection, TeamGlowCenterOffset);
        }

        EndTeamGlowBatch();
    }

    /// <summary>
    /// Glow strength for an engineer building: steady once built, stronger in overdrive,
    /// scaled by the map's building glow setting.
    /// </summary>
    private float GetStructureGlowLevel(SentryEntity sentry)
    {
        var level = TeamGlowBuildingLevel * (sentry.IsBuilt ? 1f : 0.5f)
            * GetTeamGlowSettingScale(_world.Level.Lighting.BuildingGlow);
        return sentry.IsOverdriveActive ? level * 1.8f * GetTeamGlowPulse(sentry.Id * 1.3f) : level;
    }

    /// <summary>Glow over one already-drawn sprite frame (buildings, grenades).</summary>
    private void DrawSpriteFrameTeamGlow(
        LoadedSpriteFrame frame,
        Vector2 position,
        float rotation,
        Vector2 origin,
        Vector2 scale,
        SpriteEffects effects,
        PlayerTeam team,
        float level)
    {
        if (!AreTeamGlowsEnabled || level <= 0.01f)
        {
            return;
        }

        var color = GetTeamGlowColor(team);
        BeginTeamGlowBatch();
        DrawSpriteFrameOutline(frame, position, GetTeamGlowTint(color, TeamGlowRimWeight * level), rotation, origin, scale, effects);
        DrawSpriteFrameFlatColor(frame, position, GetTeamGlowTint(color, TeamGlowFillWeight * level), rotation, origin, scale, effects);
        EndTeamGlowBatch();
    }

    private void DrawSpriteTeamGlow(string spriteName, int frameIndex, float worldX, float worldY, Vector2 cameraPosition, float rotation, PlayerTeam team, float level)
    {
        var sprite = GetResolvedSprite(spriteName);
        if (sprite is null || sprite.Frames.Count == 0)
        {
            return;
        }

        DrawSpriteFrameTeamGlow(
            sprite.Frames[Math.Clamp(frameIndex, 0, sprite.Frames.Count - 1)],
            new Vector2(worldX - cameraPosition.X, worldY - cameraPosition.Y),
            rotation,
            sprite.Origin.ToVector2(),
            Vector2.One,
            SpriteEffects.None,
            team,
            level);
    }

    /// <summary>
    /// Team-coloured light from the same glowing things. Healed / ubered players use the
    /// heal glow setting and the player light range; buildings use the building glow
    /// setting and grenades the effect light strength, both with the effect range.
    /// </summary>
    private void AddTeamGlowLights(MapLighting lighting)
    {
        if (!AreTeamGlowsEnabled)
        {
            return;
        }

        var heal = lighting.HealGlow / 100f;
        var playerRange = lighting.PlayerRange / 100f;
        if (heal > 0f && playerRange > 0f)
        {
            AddHealGlowLights(heal, playerRange);
        }

        var range = lighting.EffectRange / 100f;
        if (range <= 0f)
        {
            return;
        }

        foreach (var sentry in _world.Sentries)
        {
            var level = GetStructureGlowLevel(sentry);
            if (level <= 0.01f)
            {
                continue;
            }

            var position = GetRenderPosition(sentry.Id, sentry.X, sentry.Y);
            _gameplayLightmap.AddLight(
                position.X,
                position.Y - 8f,
                56f * range,
                GetTeamGlowColor(sentry.Team),
                0.45f * level,
                glowBoost: 0.15f * level);
        }

        var effects = lighting.EffectLights / 100f;
        if (effects <= 0f)
        {
            return;
        }

        foreach (var grenade in _world.Grenades)
        {
            var position = GetRenderPosition(grenade.Id, grenade.X, grenade.Y);
            _gameplayLightmap.AddLight(
                position.X,
                position.Y,
                40f * range,
                GetTeamGlowColor(grenade.Team),
                effects * 0.5f * TeamGlowGrenadeLevel,
                glowBoost: effects * 0.2f * TeamGlowGrenadeLevel);
        }
    }

    private void AddHealGlowLights(float heal, float range)
    {
        foreach (var player in EnumerateRenderablePlayers())
        {
            if (!player.IsAlive || !HasFreshPlayerRenderHistory(player))
            {
                continue;
            }

            var level = GetPlayerHealGlowLevel(player) * Math.Clamp(GetPlayerVisibilityAlpha(player), 0f, 1f);
            if (level <= 0.01f)
            {
                continue;
            }

            var position = GetRenderPosition(player, allowInterpolation: true);
            _gameplayLightmap.AddLight(
                position.X,
                position.Y,
                64f * range,
                GetTeamGlowColor(player.Team),
                heal * 0.5f * level,
                glowBoost: heal * 0.2f * level);
        }
    }
}
