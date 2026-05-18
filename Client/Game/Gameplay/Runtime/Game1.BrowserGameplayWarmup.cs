#nullable enable

using OpenGarrison.Core;
using OpenGarrison.GameplayModding;

namespace OpenGarrison.Client;

public partial class Game1
{
    private static readonly PlayerClass[] BrowserWarmupClassOrder =
    [
        PlayerClass.Scout,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Heavy,
        PlayerClass.Demoman,
        PlayerClass.Medic,
        PlayerClass.Engineer,
        PlayerClass.Spy,
        PlayerClass.Sniper,
        PlayerClass.Quote,
    ];

    private static readonly string[] BrowserWarmupSpawnBodySuffixes =
    [
        "S",
        "StandS",
    ];

    private static readonly string[] BrowserWarmupFullBodySuffixes =
    [
        "S",
        "StandS",
        "RunS",
        "JumpS",
        "LeanLS",
        "LeanRS",
        "DeadS",
        "IntelS",
        "TauntS",
        "HS",
    ];

    private static readonly string[] BrowserWarmupQuoteSpawnBodySuffixes =
    [
        "S",
    ];

    private static readonly string[] BrowserWarmupQuoteFullBodySuffixes =
    [
        "S",
        "DeadS",
        "TauntS",
        "HS",
    ];

    private static readonly string[] BrowserWarmupClassSelectionSprites =
    [
        "ClassSelectS",
        "ClassSelectBS",
        "ClassSelectSpritesS",
        "ScoutPortraitAnimationS",
        "PyroPortraitAnimationS",
        "SoldierPortraitanimationS",
        "HeavyPortraitAnimationS",
        "DemomanPortraitAnimationS",
        "MedicPortraitAnimationS",
        "EngineerPortraitAnimationS",
        "SpyPortraitAnimationS",
        "SniperPortraitAnimationS",
        "RandomPortraitAnimationS",
    ];

    private void WarmBrowserClassSelectionAssets(PlayerTeam team)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        foreach (var spriteName in BrowserWarmupClassSelectionSprites)
        {
            WarmBrowserSprite(spriteName);
        }

        foreach (var classId in BrowserWarmupClassOrder)
        {
            WarmBrowserClassAssets(classId, team, includeExtendedAnimations: false);
        }

        WarmBrowserSprite("CrosshairS");
        WarmBrowserSprite("IntelTimerS");
    }

    private void WarmBrowserPlayableClassAssets(PlayerClass classId, PlayerTeam team)
    {
        WarmBrowserClassAssets(classId, team, includeExtendedAnimations: true);

        switch (classId)
        {
            case PlayerClass.Heavy:
                WarmBrowserSound("ChaingunSnd");
                break;
            case PlayerClass.Pyro:
                WarmBrowserSound("FlamethrowerSnd");
                break;
            case PlayerClass.Medic:
                WarmBrowserSound("MedigunSnd");
                break;
        }
    }

    private void WarmBrowserClassAssets(PlayerClass classId, PlayerTeam team, bool includeExtendedAnimations)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        var suffixes = classId == PlayerClass.Quote
            ? includeExtendedAnimations ? BrowserWarmupQuoteFullBodySuffixes : BrowserWarmupQuoteSpawnBodySuffixes
            : includeExtendedAnimations ? BrowserWarmupFullBodySuffixes : BrowserWarmupSpawnBodySuffixes;
        foreach (var suffix in suffixes)
        {
            WarmBrowserSprite(GameplayPlayerSpriteRenderController.GetTeamSpriteNameProxy(classId, team, suffix));
        }

        if (includeExtendedAnimations)
        {
            if (classId == PlayerClass.Heavy)
            {
                WarmBrowserSprite(GameplayPlayerSpriteRenderController.GetTeamSpriteNameProxy(classId, team, "WalkS"));
                WarmBrowserSprite(GameplayPlayerSpriteRenderController.GetTeamSpriteNameProxy(classId, team, "OmnomnomnomS"));
            }

            if (classId == PlayerClass.Sniper)
            {
                WarmBrowserSprite(GameplayPlayerSpriteRenderController.GetTeamSpriteNameProxy(classId, team, "CrouchS"));
            }
        }

        WarmBrowserWeaponPresentation(CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(classId).Presentation);
    }

    private void WarmBrowserWeaponPresentation(GameplayItemPresentationDefinition presentation)
    {
        WarmBrowserSprite(presentation.WorldSpriteName);
        WarmBrowserSprite(presentation.RecoilSpriteName);
        WarmBrowserSprite(presentation.ReloadSpriteName);
        WarmBrowserSprite(presentation.RecoilCarrierSpriteName);
        WarmBrowserSprite(presentation.RecoilOverlaySpriteName);
        WarmBrowserSprite(presentation.ReloadCarrierSpriteName);
        WarmBrowserSprite(presentation.ReloadOverlaySpriteName);
        WarmBrowserSprite(presentation.HudSpriteName);
    }

    private void WarmBrowserSprite(string? spriteName)
    {
        if (!OperatingSystem.IsBrowser() || string.IsNullOrWhiteSpace(spriteName))
        {
            return;
        }

        _ = GetResolvedSprite(spriteName);
    }

    private void WarmBrowserSound(string soundName)
    {
        if (!OperatingSystem.IsBrowser() || string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        _ = _runtimeAssets?.GetSound(soundName);
    }
}
