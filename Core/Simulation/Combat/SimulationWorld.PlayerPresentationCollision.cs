using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private static readonly Lazy<GameMakerAssetManifest> s_gameMakerAssets = new(GameMakerAssetManifestImporter.ImportProjectAssets);

    private static void GetPlayerPresentationHitBounds(
        SimulationWorld world,
        PlayerEntity player,
        out float left,
        out float top,
        out float right,
        out float bottom)
    {
        if (TryGetPlayerPresentationHitBounds(world, player, out left, out top, out right, out bottom))
        {
            return;
        }

        player.GetCollisionBounds(out left, out top, out right, out bottom);
    }

    private static bool TryGetPlayerPresentationHitBounds(
        SimulationWorld world,
        PlayerEntity player,
        out float left,
        out float top,
        out float right,
        out float bottom)
    {
        left = 0f;
        top = 0f;
        right = 0f;
        bottom = 0f;
        var spriteName = GetPlayerPresentationBodySpriteName(world, player);
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
        if (s_gameMakerAssets.Value.Sprites.TryGetValue(spriteName, out sprite!))
        {
            return true;
        }

        var freshManifest = GameMakerAssetManifestImporter.ImportProjectAssets();
        return freshManifest.Sprites.TryGetValue(spriteName, out sprite!);
    }

    private static string? GetPlayerPresentationBodySpriteName(SimulationWorld world, PlayerEntity player)
    {
        if (world.IsPlayerHumiliated(player))
        {
            return GetPresentationSpriteName(
                player.ClassId,
                player.Team,
                static presentation => presentation.HumiliationSuffix ?? presentation.BaseSuffix,
                "HS");
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
        if (appearsAirborne && HasGroundSupportForPresentation(world, player))
        {
            appearsAirborne = false;
        }

        if (appearsAirborne)
        {
            return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.JumpSuffix ?? presentation.BaseSuffix, "JumpS");
        }

        if (horizontalSourceStepSpeed < 0.2f)
        {
            return GetStandingPresentationSpriteName(world, player);
        }

        if (player.ClassId == PlayerClass.Heavy && horizontalSourceStepSpeed < 3f)
        {
            return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.WalkSuffix ?? presentation.RunSuffix ?? presentation.BaseSuffix, "WalkS");
        }

        return GetPresentationSpriteName(player.ClassId, player.Team, static presentation => presentation.RunSuffix ?? presentation.BaseSuffix, "RunS");
    }

    private static string? GetStandingPresentationSpriteName(SimulationWorld world, PlayerEntity player)
    {
        var leanDirection = GetPlayerLeanDirectionForPresentation(world, player);
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

    private static int GetPlayerLeanDirectionForPresentation(SimulationWorld world, PlayerEntity player)
    {
        var playerScale = player.PlayerScale;
        var bottom = player.Bottom + (2f * playerScale);
        var openRight = !IsPointBlockedForPresentation(world, player, player.X + (6f * playerScale), bottom)
            && !IsPointBlockedForPresentation(world, player, player.X + (2f * playerScale), bottom);
        var openLeft = !IsPointBlockedForPresentation(world, player, player.X - (7f * playerScale), bottom)
            && !IsPointBlockedForPresentation(world, player, player.X - (3f * playerScale), bottom);
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
            openRight = !IsPointBlockedForPresentation(world, player, player.Right - playerScale, bottom);
            openLeft = !IsPointBlockedForPresentation(world, player, player.Left, bottom);
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

    private static bool HasGroundSupportForPresentation(SimulationWorld world, PlayerEntity player)
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
        return IsPointBlockedForPresentation(world, player, leftProbeX, probeY)
            || IsPointBlockedForPresentation(world, player, centerProbeX, probeY)
            || IsPointBlockedForPresentation(world, player, rightProbeX, probeY);
    }

    private static bool IsPointBlockedForPresentation(SimulationWorld world, PlayerEntity player, float x, float y)
    {
        foreach (var solid in world.Level.Solids)
        {
            if (x >= solid.Left && x < solid.Right && y >= solid.Top && y < solid.Bottom)
            {
                return true;
            }
        }

        foreach (var gate in world.Level.GetBlockingTeamGates(player.Team, player.IsCarryingIntel))
        {
            if (x >= gate.Left && x < gate.Right && y >= gate.Top && y < gate.Bottom)
            {
                return true;
            }
        }

        foreach (var wall in world.Level.GetRoomObjects(RoomObjectType.PlayerWall))
        {
            if (x >= wall.Left && x < wall.Right && y >= wall.Top && y < wall.Bottom)
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetPlayerSpriteName(PlayerClass classId, PlayerTeam team)
    {
        return GetPresentationSpriteName(classId, team, static presentation => presentation.BaseSuffix, "S");
    }

    private static string? GetPresentationSpriteName(
        PlayerClass classId,
        PlayerTeam team,
        Func<GameplayClassPresentationDefinition, string> suffixSelector,
        string legacySuffix)
    {
        var presentation = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(classId).Presentation;
        return GetTeamSpriteName(classId, team, presentation is null ? legacySuffix : suffixSelector(presentation));
    }

    private static string? GetPresentationFacingSpriteName(
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

    private static string? GetTeamSpriteName(PlayerClass classId, PlayerTeam team, string suffix)
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

    private static string? GetPlayerSpritePrefix(PlayerClass classId)
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
