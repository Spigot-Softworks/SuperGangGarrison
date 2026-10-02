using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

internal sealed partial class PlayerPresentationBoundsSystem
{
    private static readonly Lazy<GameMakerAssetManifest> _gameMakerAssets = new(GameMakerRuntimeAssetManifestLoader.LoadPackagedOrProjectAssets);
    private static readonly object _presentationSpriteAssetCacheSync = new();
    private static readonly Dictionary<string, GameMakerSpriteAsset> _resolvedPresentationSpriteAssets = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _missingPresentationSpriteAssets = new(StringComparer.OrdinalIgnoreCase);

    internal void GetPlayerPresentationHitBounds(PlayerEntity player,
        out float left,
        out float top,
        out float right,
        out float bottom)
    {
        if (TryGetPlayerPresentationHitBounds(player, out left, out top, out right, out bottom))
        {
            return;
        }

        player.GetCollisionBounds(out left, out top, out right, out bottom);
    }

    internal void GetCachedPlayerPresentationHitBounds(
        PlayerEntity player,
        out float left,
        out float top,
        out float right,
        out float bottom)
    {
        if (_host.CombatRuntime.PresentationHitBoundsCacheFrame != _host.Frame)
        {
            _host.CombatRuntime.PresentationHitBoundsCache.Clear();
            _host.CombatRuntime.PresentationHitBoundsCacheFrame = _host.Frame;
        }

        var key = new PresentationHitBoundsCacheKey(
            player.X,
            player.Y,
            player.HorizontalSpeed,
            player.VerticalSpeed,
            player.PlayerScale,
            player.ClassId,
            player.GameplayClassId,
            player.Team,
            player.IsAlive,
            player.IsGrounded,
            player.IsHeavyEating,
            player.IsTaunting,
            player.IsSniperScoped,
            player.IsSourceFacingLeft,
            player.IsCarryingIntel,
            _host.IsPlayerHumiliated(player));
        if (_host.CombatRuntime.PresentationHitBoundsCache.TryGetValue(player.Id, out var cached)
            && cached.Key.Equals(key))
        {
            left = cached.Left;
            top = cached.Top;
            right = cached.Right;
            bottom = cached.Bottom;
            return;
        }

        GetPlayerPresentationHitBounds(player, out left, out top, out right, out bottom);
        _host.CombatRuntime.PresentationHitBoundsCache[player.Id] = new PresentationHitBoundsCacheEntry(
            key,
            left,
            top,
            right,
            bottom);
    }

    private bool TryGetPlayerPresentationHitBounds(PlayerEntity player,
        out float left,
        out float top,
        out float right,
        out float bottom)
    {
        left = 0f;
        top = 0f;
        right = 0f;
        bottom = 0f;
        var spriteName = GetPlayerPresentationBodySpriteName(player);
        if (string.IsNullOrWhiteSpace(spriteName))
        {
            return false;
        }

        if (!TryGetPresentationSpriteAsset(spriteName, out var sprite))
        {
            return false;
        }

        var mask = sprite.Mask;
        if (!mask.Left.HasValue || !mask.Top.HasValue || !mask.Right.HasValue || !mask.Bottom.HasValue)
        {
            return false;
        }

        var playerScale = player.PlayerScale;
        left = player.X + ((mask.Left.Value - sprite.OriginX) * playerScale);
        top = player.Y + ((mask.Top.Value - sprite.OriginY) * playerScale);
        right = player.X + (((mask.Right.Value - sprite.OriginX) + 1f) * playerScale);
        bottom = player.Y + (((mask.Bottom.Value - sprite.OriginY) + 1f) * playerScale);
        return true;
    }

    private static bool TryGetPresentationSpriteAsset(string spriteName, out GameMakerSpriteAsset sprite)
    {
        var normalizedSpriteName = spriteName.Trim();
        if (_gameMakerAssets.Value.Sprites.TryGetValue(normalizedSpriteName, out sprite!))
        {
            return true;
        }

        lock (_presentationSpriteAssetCacheSync)
        {
            if (_resolvedPresentationSpriteAssets.TryGetValue(normalizedSpriteName, out sprite!))
            {
                return true;
            }

            if (_missingPresentationSpriteAssets.Contains(normalizedSpriteName))
            {
                sprite = null!;
                return false;
            }
        }

        if (TryCreateGameplayPresentationSpriteAsset(normalizedSpriteName, out sprite!)
            || TryLoadFreshPresentationSpriteAsset(normalizedSpriteName, out sprite!))
        {
            lock (_presentationSpriteAssetCacheSync)
            {
                _resolvedPresentationSpriteAssets[normalizedSpriteName] = sprite;
                _missingPresentationSpriteAssets.Remove(normalizedSpriteName);
            }

            return true;
        }

        lock (_presentationSpriteAssetCacheSync)
        {
            _missingPresentationSpriteAssets.Add(normalizedSpriteName);
        }

        sprite = null!;
        return false;
    }

    private static bool TryLoadFreshPresentationSpriteAsset(string spriteName, out GameMakerSpriteAsset sprite)
    {
        try
        {
            var freshManifest = GameMakerRuntimeAssetManifestLoader.LoadPackagedOrProjectAssets();
            return freshManifest.Sprites.TryGetValue(spriteName, out sprite!);
        }
        catch (FileNotFoundException)
        {
            sprite = null!;
            return false;
        }
    }

    private static bool TryCreateGameplayPresentationSpriteAsset(string spriteName, out GameMakerSpriteAsset sprite)
    {
        foreach (var modPack in CharacterClassCatalog.RuntimeRegistry.ModPacks)
        {
            if (modPack.Assets.Sprites.TryGetValue(spriteName, out var definition)
                && TryCreateGameplayPresentationSpriteAsset(definition, out sprite!))
            {
                return true;
            }
        }

        if (StockGameplayModCatalog.Definition.Assets.Sprites.TryGetValue(spriteName, out var stockDefinition)
            && TryCreateGameplayPresentationSpriteAsset(stockDefinition, out sprite!))
        {
            return true;
        }

        sprite = null!;
        return false;
    }

    private static bool TryCreateGameplayPresentationSpriteAsset(
        GameplaySpriteAssetDefinition definition,
        out GameMakerSpriteAsset sprite)
    {
        var mask = definition.Mask;
        if (mask is null
            || !mask.Left.HasValue
            || !mask.Top.HasValue
            || !mask.Right.HasValue
            || !mask.Bottom.HasValue)
        {
            sprite = null!;
            return false;
        }

        sprite = new GameMakerSpriteAsset(
            definition.Id,
            MetadataPath: $"gameplay:{definition.Id}",
            FramePaths: definition.FramePaths,
            OriginX: definition.OriginX,
            OriginY: definition.OriginY,
            Preload: true,
            Transparent: true,
            Mask: new GameMakerSpriteMask(
                mask.Separate,
                mask.Shape,
                mask.BoundsMode,
                mask.Left,
                mask.Top,
                mask.Right,
                mask.Bottom));
        return true;
    }

    internal string? GetPlayerPresentationBodySpriteName(PlayerEntity player)
    {
        if (_host.IsPlayerHumiliated(player))
        {
            if (player.IsQuoteCurly)
            {
                return GetGameplayPresentationSpriteName(
                    player.GameplayClassId,
                    player.Team,
                    static presentation => presentation.HumiliationSuffix ?? presentation.BaseSuffix,
                    "HS");
            }

            return GetPresentationSpriteName(
                player.ClassId,
                player.Team,
                static presentation => presentation.HumiliationSuffix ?? presentation.BaseSuffix,
                "HS");
        }

        if (player.IsQuoteCurly)
        {
            if (player.IsTaunting)
            {
                return GetGameplayPresentationSpriteName(
                    player.GameplayClassId,
                    player.Team,
                    static presentation => presentation.TauntSuffix ?? presentation.BaseSuffix,
                    "TauntS");
            }

            var quoteHorizontalSourceStepSpeed = MathF.Abs(player.HorizontalSpeed) / LegacyMovementModel.SourceTicksPerSecond;
            var quoteAppearsAirborne = !player.IsGrounded;
            if (quoteAppearsAirborne && HasGroundSupportForPresentation(player))
            {
                quoteAppearsAirborne = false;
            }

            if (quoteAppearsAirborne)
            {
                return GetGameplayPresentationSpriteName(
                    player.GameplayClassId,
                    player.Team,
                    static presentation => presentation.JumpSuffix ?? presentation.BaseSuffix,
                    "JumpS");
            }

            if (quoteHorizontalSourceStepSpeed >= 0.2f)
            {
                return GetGameplayPresentationSpriteName(
                    player.GameplayClassId,
                    player.Team,
                    static presentation => presentation.RunSuffix ?? presentation.BaseSuffix,
                    "RunS");
            }

            return GetGameplayPresentationSpriteName(
                player.GameplayClassId,
                player.Team,
                static presentation => presentation.BaseSuffix,
                "S");
        }

        if (player.ClassId == PlayerClass.Quote)
        {
            return GetPlayerSpriteName(player.ClassId, player.Team);
        }

        if (player.IsHeavyEating)
        {
            return player.ClassId == PlayerClass.Heavy
                ? GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.HeavyEatSuffix ?? presentation.BaseSuffix, "OmnomnomnomS")
                : GetPlayerSpriteName(player.ClassId, player.Team);
        }

        if (player.IsTaunting)
        {
            return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.TauntSuffix ?? presentation.BaseSuffix, "TauntS");
        }

        if (player.ClassId == PlayerClass.Sniper && player.IsSniperScoped)
        {
            return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.ScopedSuffix ?? presentation.BaseSuffix, "CrouchS");
        }

        var horizontalSourceStepSpeed = MathF.Abs(player.HorizontalSpeed) / LegacyMovementModel.SourceTicksPerSecond;
        var appearsAirborne = !player.IsGrounded;
        if (appearsAirborne && HasGroundSupportForPresentation(player))
        {
            appearsAirborne = false;
        }

        if (appearsAirborne)
        {
            return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.JumpSuffix ?? presentation.BaseSuffix, "JumpS");
        }

        if (horizontalSourceStepSpeed < 0.2f)
        {
            return GetStandingPresentationSpriteName(player);
        }

        if (player.ClassId == PlayerClass.Heavy && horizontalSourceStepSpeed < 3f)
        {
            return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.WalkSuffix ?? presentation.RunSuffix ?? presentation.BaseSuffix, "WalkS");
        }

        return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.RunSuffix ?? presentation.BaseSuffix, "RunS");
    }

    private string? GetStandingPresentationSpriteName(PlayerEntity player)
    {
        var leanDirection = GetPlayerLeanDirectionForPresentation(player);
        if (leanDirection == 0)
        {
            return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.StandSuffix ?? presentation.BaseSuffix, "StandS");
        }

        var facingLeft = player.IsSourceFacingLeft;
        return leanDirection < 0
            ? GetPresentationFacingSpriteName(
                player.ClassId,
                player.Team,
                static presentation => presentation.LeanRightSuffix ?? presentation.BaseSuffix,
                static presentation => presentation.LeanLeftSuffix ?? presentation.BaseSuffix,
                facingLeft,
                "LeanRS",
                "LeanLS")
            : GetPresentationFacingSpriteName(
                player.ClassId,
                player.Team,
                static presentation => presentation.LeanLeftSuffix ?? presentation.BaseSuffix,
                static presentation => presentation.LeanRightSuffix ?? presentation.BaseSuffix,
                facingLeft,
                "LeanLS",
                "LeanRS");
    }

    private int GetPlayerLeanDirectionForPresentation(PlayerEntity player)
    {
        var playerScale = player.PlayerScale;
        var bottom = player.Bottom + (2f * playerScale);
        var openRight = !IsPointBlockedForPresentation(player, player.X + (6f * playerScale), bottom)
            && !IsPointBlockedForPresentation(player, player.X + (2f * playerScale), bottom);
        var openLeft = !IsPointBlockedForPresentation(player, player.X - (7f * playerScale), bottom)
            && !IsPointBlockedForPresentation(player, player.X - (3f * playerScale), bottom);
        var leanDirection = 0;
        if (openRight)
        {
            leanDirection = 1;
        }

        if (openLeft)
        {
            leanDirection = -1;
        }

        if (openRight && openLeft)
        {
            openRight = !IsPointBlockedForPresentation(player, player.Right - playerScale, bottom);
            openLeft = !IsPointBlockedForPresentation(player, player.Left, bottom);
            leanDirection = 0;
            if (openRight)
            {
                leanDirection = 1;
            }

            if (openLeft)
            {
                leanDirection = -1;
            }
        }

        return leanDirection;
    }

    private bool HasGroundSupportForPresentation(PlayerEntity player)
    {
        if (player.VerticalSpeed < 0f)
        {
            return false;
        }

        var playerScale = player.PlayerScale;
        var probeY = player.Bottom + playerScale;
        var leftProbeX = player.Left + MathF.Max(1f, 2f * playerScale);
        var centerProbeX = player.X;
        var rightProbeX = player.Right - MathF.Max(1f, 2f * playerScale);
        return IsPointBlockedForPresentation(player, leftProbeX, probeY)
            || IsPointBlockedForPresentation(player, centerProbeX, probeY)
            || IsPointBlockedForPresentation(player, rightProbeX, probeY);
    }

    private bool IsPointBlockedForPresentation(PlayerEntity player, float x, float y)
    {
        foreach (var solid in _host.Level.Solids)
        {
            if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
            {
                return true;
            }
        }

        foreach (var gate in _host.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
        {
            if (x >= gate.Left && x < gate.Right && y >= gate.Top && y < gate.Bottom)
            {
                return true;
            }
        }

        foreach (var wall in _host.Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
            {
                return true;
            }
        }

        return SimpleLevelBarrierCollision.BlocksPointForPlayer(_host.Level, player.Team, player.IsCarryingIntel, x, y);
    }

    internal static string? GetPlayerSpriteName(PlayerClass classId, PlayerTeam team)
    {
        return GetPresentationSpriteName(classId, team, static presentation => presentation.BaseSuffix, "S");
    }

    internal static string? GetPresentationSpriteName(
        PlayerClass classId,
        PlayerTeam team,
        Func<GameplayClassPresentationDefinition, string> suffixSelector,
        string legacySuffix)
    {
        var presentation = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(classId).Presentation;
        return GetTeamSpriteName(classId, team, presentation is null ? legacySuffix : suffixSelector(presentation));
    }

    private static string? GetGameplayPresentationSpriteName(
        string gameplayClassId,
        PlayerTeam team,
        Func<GameplayClassPresentationDefinition, string> suffixSelector,
        string legacySuffix)
    {
        var presentation = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(gameplayClassId).Presentation;
        return GetGameplayTeamSpriteName(
            gameplayClassId,
            team,
            presentation is null ? legacySuffix : suffixSelector(presentation));
    }

    private static string? GetGameplayTeamSpriteName(string gameplayClassId, PlayerTeam team, string suffix)
    {
        var prefix = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(gameplayClassId).Presentation?.SpritePrefix;
        if (prefix is null)
        {
            return null;
        }

        var teamName = team switch
        {
            PlayerTeam.Red => "Red",
            PlayerTeam.Blue => "Blue",
            _ => null,
        };

        return teamName is null ? null : $"{prefix}{teamName}{suffix}";
    }

    internal static string? GetPresentationFacingSpriteName(
        PlayerClass classId,
        PlayerTeam team,
        Func<GameplayClassPresentationDefinition, string> facingLeftSuffixSelector,
        Func<GameplayClassPresentationDefinition, string> facingRightSuffixSelector,
        bool facingLeft,
        string legacyFacingLeftSuffix,
        string legacyFacingRightSuffix)
    {
        var presentation = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(classId).Presentation;
        return GetTeamSpriteName(
            classId,
            team,
            presentation is null
                ? (facingLeft ? legacyFacingLeftSuffix : legacyFacingRightSuffix)
                : (facingLeft ? facingLeftSuffixSelector(presentation) : facingRightSuffixSelector(presentation)));
    }

    internal static string? GetTeamSpriteName(PlayerClass classId, PlayerTeam team, string suffix)
    {
        var prefix = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(classId).Presentation?.SpritePrefix ?? GetPlayerSpritePrefix(classId);
        if (prefix is null)
        {
            return null;
        }

        var teamName = team switch
        {
            PlayerTeam.Red => "Red",
            PlayerTeam.Blue => "Blue",
            _ => null,
        };

        return teamName is null ? null : $"{prefix}{teamName}{suffix}";
    }

    internal static string? GetPlayerSpritePrefix(PlayerClass classId)
    {
        return classId switch
        {
            PlayerClass.Scout => "Scout",
            PlayerClass.Engineer => "Engineer",
            PlayerClass.Pyro => "Pyro",
            PlayerClass.Soldier => "Soldier",
            PlayerClass.Demoman => "Demoman",
            PlayerClass.Heavy => "Heavy",
            PlayerClass.Sniper => "Sniper",
            PlayerClass.Medic => "Medic",
            PlayerClass.Spy => "Spy",
            PlayerClass.Quote => "Querly",
            _ => null,
        };
    }
}
