using OpenGarrison.GameplayModding;

namespace OpenGarrison.Core;

public static class CharacterClassCatalog
{
    public static GameplayModPackDefinition StockModPack => StockGameplayModCatalog.Definition;

    public static PrimaryWeaponDefinition Scattergun { get; } = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.scattergun"]);

    public static PrimaryWeaponDefinition Shotgun { get; } = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.shotgun"]);

    public static PrimaryWeaponDefinition Flamethrower { get; } = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.flamethrower"]);

    public static PrimaryWeaponDefinition RocketLauncher { get; } = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.rocketlauncher"]);

    public static PrimaryWeaponDefinition MineLauncher { get; } = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.minelauncher"]);

    public static PrimaryWeaponDefinition Minigun { get; } = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.minigun"]);

    public static PrimaryWeaponDefinition Rifle { get; } = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.rifle"]);

    public static PrimaryWeaponDefinition Medigun { get; } = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.medigun"]);

    public static PrimaryWeaponDefinition Revolver { get; } = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.revolver"]);

    public static PrimaryWeaponDefinition Blade { get; } = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.Definition.Items["weapon.blade"]);

    public static CharacterClassDefinition Scout { get; } = CreateDefinition(PlayerClass.Scout);

    public static CharacterClassDefinition Engineer { get; } = CreateDefinition(PlayerClass.Engineer);

    public static CharacterClassDefinition Pyro { get; } = CreateDefinition(PlayerClass.Pyro);

    public static CharacterClassDefinition Soldier { get; } = CreateDefinition(PlayerClass.Soldier);

    public static CharacterClassDefinition Demoman { get; } = CreateDefinition(PlayerClass.Demoman);

    public static CharacterClassDefinition Heavy { get; } = CreateDefinition(PlayerClass.Heavy);

    public static CharacterClassDefinition Sniper { get; } = CreateDefinition(PlayerClass.Sniper);

    public static CharacterClassDefinition Medic { get; } = CreateDefinition(PlayerClass.Medic);

    public static CharacterClassDefinition Spy { get; } = CreateDefinition(PlayerClass.Spy);

    public static CharacterClassDefinition Quote { get; } = CreateDefinition(PlayerClass.Quote);

    public static CharacterClassDefinition GetDefinition(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Engineer => Engineer,
            PlayerClass.Pyro => Pyro,
            PlayerClass.Soldier => Soldier,
            PlayerClass.Demoman => Demoman,
            PlayerClass.Heavy => Heavy,
            PlayerClass.Sniper => Sniper,
            PlayerClass.Medic => Medic,
            PlayerClass.Spy => Spy,
            PlayerClass.Quote => Quote,
            _ => Scout,
        };
    }

    private static PrimaryWeaponDefinition CreatePrimaryWeaponDefinition(GameplayItemDefinition item)
    {
        return new PrimaryWeaponDefinition(
            DisplayName: item.DisplayName,
            Kind: ToPrimaryWeaponKind(item.BehaviorId),
            MaxAmmo: item.Ammo.MaxAmmo,
            AmmoPerShot: item.Ammo.AmmoPerUse,
            ProjectilesPerShot: item.Ammo.ProjectilesPerUse,
            ReloadDelayTicks: item.Ammo.UseDelaySourceTicks,
            AmmoReloadTicks: item.Ammo.ReloadSourceTicks,
            SpreadDegrees: item.Ammo.SpreadDegrees,
            MinShotSpeed: item.Ammo.MinProjectileSpeed,
            AdditionalRandomShotSpeed: item.Ammo.AdditionalProjectileSpeed,
            AutoReloads: item.Ammo.AutoReloads,
            AmmoRegenPerTick: item.Ammo.AmmoRegenPerTick,
            RefillsAllAtOnce: item.Ammo.RefillsAllAtOnce);
    }

    private static CharacterClassDefinition CreateDefinition(PlayerClass playerClass)
    {
        var gameplayClass = StockGameplayModCatalog.GetClassDefinition(playerClass);
        var movement = gameplayClass.Movement;
        var primaryWeapon = CreatePrimaryWeaponDefinition(StockGameplayModCatalog.GetPrimaryItem(playerClass));
        var width = movement.CollisionRight - movement.CollisionLeft;
        var height = movement.CollisionBottom - movement.CollisionTop;

        return new CharacterClassDefinition(
            Id: playerClass,
            DisplayName: gameplayClass.DisplayName,
            PrimaryWeapon: primaryWeapon,
            MaxHealth: movement.MaxHealth,
            Width: width,
            Height: height,
            CollisionLeft: movement.CollisionLeft,
            CollisionTop: movement.CollisionTop,
            CollisionRight: movement.CollisionRight,
            CollisionBottom: movement.CollisionBottom,
            RunPower: movement.RunPower,
            JumpStrength: movement.JumpStrength,
            MaxRunSpeed: LegacyMovementModel.GetMaxRunSpeed(movement.RunPower),
            GroundAcceleration: LegacyMovementModel.GetContinuousRunDrive(movement.RunPower),
            GroundDeceleration: LegacyMovementModel.GetContinuousRunDrive(movement.RunPower),
            Gravity: LegacyMovementModel.GetGravityPerSecondSquared(),
            JumpSpeed: LegacyMovementModel.GetJumpSpeed(movement.JumpStrength),
            MaxAirJumps: movement.MaxAirJumps,
            TauntLengthFrames: movement.TauntLengthFrames);
    }

    private static PrimaryWeaponKind ToPrimaryWeaponKind(string behaviorId)
    {
        return behaviorId switch
        {
            BuiltInGameplayBehaviorIds.PelletGun => PrimaryWeaponKind.PelletGun,
            BuiltInGameplayBehaviorIds.Flamethrower => PrimaryWeaponKind.FlameThrower,
            BuiltInGameplayBehaviorIds.RocketLauncher => PrimaryWeaponKind.RocketLauncher,
            BuiltInGameplayBehaviorIds.MineLauncher => PrimaryWeaponKind.MineLauncher,
            BuiltInGameplayBehaviorIds.Minigun => PrimaryWeaponKind.Minigun,
            BuiltInGameplayBehaviorIds.Rifle => PrimaryWeaponKind.Rifle,
            BuiltInGameplayBehaviorIds.Medigun => PrimaryWeaponKind.Medigun,
            BuiltInGameplayBehaviorIds.Revolver => PrimaryWeaponKind.Revolver,
            BuiltInGameplayBehaviorIds.Blade => PrimaryWeaponKind.Blade,
            _ => throw new InvalidOperationException($"Unsupported stock primary behavior id \"{behaviorId}\"."),
        };
    }
}
