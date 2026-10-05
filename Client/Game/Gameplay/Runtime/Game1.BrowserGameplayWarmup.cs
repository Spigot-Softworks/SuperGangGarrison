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
        "RunS",
        "JumpS",
        "DeadS",
        "IntelS",
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
        "ImpostorPortraitAnimationS",
    ];

    private static readonly string[] BrowserWarmupEngineerStructureSprites =
    [
        "SentryRed",
        "SentryBlue",
        "SentryTurretS",
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
            if (CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(classId, out _))
            {
                WarmBrowserClassAssets(classId, team, includeExtendedAnimations: false);
            }
        }

        WarmBrowserGameplayClassAssets(
            CharacterClassCatalog.QuoteGameplayClassId,
            team,
            includeExtendedAnimations: false);

        WarmBrowserEngineerStructureAssets();

        WarmBrowserSprite("CrosshairS");
        WarmBrowserSprite(ContinuousCrosshairSpriteName);
        WarmBrowserSprite("IntelTimerS");
    }

    private static readonly string[] BrowserWarmupLocalCueSounds =
    [
        LandingSoundName,
        ShotgunShellReloadSoundName,
        ShotgunReadySoundName,
        SmgReadySoundName,
        FlareReadySoundName,
        PistolReadySoundName,
        PistolHalfReloadSoundName,
        NailgunReloadSoundName,
        HeavyPainSoundName,
        VeryHeavyPainSoundName,
    ];

    private void WarmBrowserPlayableClassAssets(PlayerClass classId, PlayerTeam team)
    {
        WarmBrowserClassAssets(classId, team, includeExtendedAnimations: true);
        // Local cues play the instant they trigger, so load them before they are needed.
        foreach (var soundName in BrowserWarmupLocalCueSounds)
        {
            WarmBrowserSound(soundName);
        }

        switch (classId)
        {
            case PlayerClass.Heavy:
                WarmBrowserSound(MinigunLoopSoundName);
                WarmBrowserSound(MinigunShotSoundName);
                break;
            case PlayerClass.Pyro:
                WarmBrowserSound("FlamethrowerSnd");
                break;
            case PlayerClass.Medic:
                WarmBrowserSound("MedigunSnd");
                break;
            case PlayerClass.Engineer:
                WarmBrowserEngineerStructureAssets();
                break;
        }
    }

    internal static IReadOnlyList<string> GetBrowserStructureWarmupSpriteNames(PlayerClass classId)
    {
        return classId == PlayerClass.Engineer
            ? BrowserWarmupEngineerStructureSprites
            : Array.Empty<string>();
    }

    private void WarmBrowserEngineerStructureAssets()
    {
        foreach (var spriteName in GetBrowserStructureWarmupSpriteNames(PlayerClass.Engineer))
        {
            WarmBrowserSprite(spriteName);
        }
    }

    private void WarmBrowserClassAssets(PlayerClass classId, PlayerTeam team, bool includeExtendedAnimations)
    {
        if (!OperatingSystem.IsBrowser())
        {
            return;
        }

        if (!CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(classId, out _))
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

    private void WarmBrowserGameplayClassAssets(string gameplayClassId, PlayerTeam team, bool includeExtendedAnimations)
    {
        if (!OperatingSystem.IsBrowser()
            || !CharacterClassCatalog.RuntimeRegistry.TryGetClassBinding(gameplayClassId, out _))
        {
            return;
        }

        var presentation = CharacterClassCatalog.RuntimeRegistry.GetClassDefinition(gameplayClassId).Presentation;
        var prefix = presentation?.SpritePrefix;
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return;
        }

        var teamName = team == PlayerTeam.Blue ? "Blue" : "Red";
        var suffixes = includeExtendedAnimations
            ? BrowserWarmupQuoteFullBodySuffixes
            : BrowserWarmupQuoteSpawnBodySuffixes;
        foreach (var suffix in suffixes)
        {
            WarmBrowserSprite($"{prefix}{teamName}{suffix}");
        }

        if (includeExtendedAnimations)
        {
            WarmBrowserSprite($"{prefix}{teamName}HS");
        }

        WarmBrowserWeaponPresentation(CharacterClassCatalog.RuntimeRegistry.GetPrimaryItem(gameplayClassId).Presentation);
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
