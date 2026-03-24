namespace OpenGarrison.Core;

public static class CharacterClassCatalog
{
    private const float LegacyWidth = 24f;
    private const float LegacyHeight = 36f;

    public static PrimaryWeaponDefinition Scattergun { get; } = new(
        DisplayName: "Scattergun",
        Kind: PrimaryWeaponKind.PelletGun,
        MaxAmmo: 6,
        AmmoPerShot: 1,
        ProjectilesPerShot: 6,
        ReloadDelayTicks: 20,
        AmmoReloadTicks: 15,
        SpreadDegrees: 7f,
        MinShotSpeed: 11f,
        AdditionalRandomShotSpeed: 4f);

    public static PrimaryWeaponDefinition Shotgun { get; } = new(
        DisplayName: "Shotgun",
        Kind: PrimaryWeaponKind.PelletGun,
        MaxAmmo: 8,
        AmmoPerShot: 1,
        ProjectilesPerShot: 4,
        ReloadDelayTicks: 20,
        AmmoReloadTicks: 15,
        SpreadDegrees: 5f,
        MinShotSpeed: 11f,
        AdditionalRandomShotSpeed: 4f);

    public static PrimaryWeaponDefinition Flamethrower { get; } = new(
        DisplayName: "Flamethrower",
        Kind: PrimaryWeaponKind.FlameThrower,
        MaxAmmo: 200,
        AmmoPerShot: 2,
        ProjectilesPerShot: 1,
        ReloadDelayTicks: 1,
        AmmoReloadTicks: 0,
        SpreadDegrees: 5f,
        MinShotSpeed: 5f,
        AdditionalRandomShotSpeed: 5f,
        AutoReloads: false,
        AmmoRegenPerTick: 1);

    public static PrimaryWeaponDefinition RocketLauncher { get; } = new(
        DisplayName: "Rocket Launcher",
        Kind: PrimaryWeaponKind.RocketLauncher,
        MaxAmmo: 4,
        AmmoPerShot: 1,
        ProjectilesPerShot: 1,
        ReloadDelayTicks: 30,
        AmmoReloadTicks: 25,
        SpreadDegrees: 0f,
        MinShotSpeed: 13f,
        AdditionalRandomShotSpeed: 0f);

    public static PrimaryWeaponDefinition MineLauncher { get; } = new(
        DisplayName: "Mine Launcher",
        Kind: PrimaryWeaponKind.MineLauncher,
        MaxAmmo: 8,
        AmmoPerShot: 1,
        ProjectilesPerShot: 1,
        ReloadDelayTicks: 26,
        AmmoReloadTicks: 20,
        SpreadDegrees: 0f,
        MinShotSpeed: 12f,
        AdditionalRandomShotSpeed: 0f);

    public static PrimaryWeaponDefinition Minigun { get; } = new(
        DisplayName: "Minigun",
        Kind: PrimaryWeaponKind.Minigun,
        MaxAmmo: 200,
        AmmoPerShot: 4,
        ProjectilesPerShot: 1,
        ReloadDelayTicks: 2,
        AmmoReloadTicks: 0,
        SpreadDegrees: 7f,
        MinShotSpeed: 12f,
        AdditionalRandomShotSpeed: 1f,
        AutoReloads: false,
        AmmoRegenPerTick: 1);

    public static PrimaryWeaponDefinition Rifle { get; } = new(
        DisplayName: "Rifle",
        Kind: PrimaryWeaponKind.Rifle,
        MaxAmmo: 1,
        AmmoPerShot: 0,
        ProjectilesPerShot: 1,
        ReloadDelayTicks: 40,
        AmmoReloadTicks: 0,
        SpreadDegrees: 0f,
        MinShotSpeed: 0f,
        AdditionalRandomShotSpeed: 0f,
        AutoReloads: false);

    public static PrimaryWeaponDefinition Medigun { get; } = new(
        DisplayName: "Medigun",
        Kind: PrimaryWeaponKind.Medigun,
        MaxAmmo: 40,
        AmmoPerShot: 0,
        ProjectilesPerShot: 1,
        ReloadDelayTicks: 3,
        AmmoReloadTicks: 0,
        SpreadDegrees: 0f,
        MinShotSpeed: 0f,
        AdditionalRandomShotSpeed: 0f,
        AutoReloads: false);

    public static PrimaryWeaponDefinition Revolver { get; } = new(
        DisplayName: "Revolver",
        Kind: PrimaryWeaponKind.Revolver,
        MaxAmmo: 6,
        AmmoPerShot: 1,
        ProjectilesPerShot: 1,
        ReloadDelayTicks: 18,
        AmmoReloadTicks: 55,
        SpreadDegrees: 1f,
        MinShotSpeed: 20f,
        AdditionalRandomShotSpeed: 0f,
        RefillsAllAtOnce: true);

    public static PrimaryWeaponDefinition Blade { get; } = new(
        DisplayName: "Blade",
        Kind: PrimaryWeaponKind.Blade,
        MaxAmmo: 100,
        AmmoPerShot: 0,
        ProjectilesPerShot: 1,
        ReloadDelayTicks: 5,
        AmmoReloadTicks: 0,
        SpreadDegrees: 4f,
        MinShotSpeed: 10f,
        AdditionalRandomShotSpeed: 2f,
        AutoReloads: false,
        AmmoRegenPerTick: 1);

    public static CharacterClassDefinition Scout { get; } = CreateDefinition(
        PlayerClass.Scout,
        "Scout",
        Scattergun,
        maxHealth: 100,
        runPower: 1.4f,
        jumpStrength: LegacyMovementModel.DefaultJumpStrength,
        maxAirJumps: 1,
        tauntLengthFrames: 8);

    public static CharacterClassDefinition Engineer { get; } = CreateDefinition(
        PlayerClass.Engineer,
        "Engineer",
        Shotgun,
        maxHealth: 120,
        runPower: 1f,
        jumpStrength: LegacyMovementModel.DefaultJumpStrength,
        maxAirJumps: 0,
        tauntLengthFrames: 12);

    public static CharacterClassDefinition Pyro { get; } = CreateDefinition(
        PlayerClass.Pyro,
        "Pyro",
        Flamethrower,
        maxHealth: 120,
        runPower: 1.1f,
        jumpStrength: LegacyMovementModel.DefaultJumpStrength,
        maxAirJumps: 0,
        tauntLengthFrames: 9);

    public static CharacterClassDefinition Soldier { get; } = CreateDefinition(
        PlayerClass.Soldier,
        "Soldier",
        RocketLauncher,
        maxHealth: 175,
        runPower: 0.9f,
        jumpStrength: LegacyMovementModel.DefaultJumpStrength,
        maxAirJumps: 0,
        tauntLengthFrames: 15);

    public static CharacterClassDefinition Demoman { get; } = CreateDefinition(
        PlayerClass.Demoman,
        "Demoman",
        MineLauncher,
        maxHealth: 120,
        runPower: 1f,
        jumpStrength: LegacyMovementModel.DefaultJumpStrength,
        maxAirJumps: 0,
        tauntLengthFrames: 10);

    public static CharacterClassDefinition Heavy { get; } = CreateDefinition(
        PlayerClass.Heavy,
        "Heavy",
        Minigun,
        maxHealth: 200,
        runPower: 0.8f,
        jumpStrength: LegacyMovementModel.DefaultJumpStrength,
        maxAirJumps: 0,
        tauntLengthFrames: 11);

    public static CharacterClassDefinition Sniper { get; } = CreateDefinition(
        PlayerClass.Sniper,
        "Sniper",
        Rifle,
        maxHealth: 120,
        runPower: 0.9f,
        jumpStrength: LegacyMovementModel.DefaultJumpStrength,
        maxAirJumps: 0,
        tauntLengthFrames: 12);

    public static CharacterClassDefinition Medic { get; } = CreateDefinition(
        PlayerClass.Medic,
        "Medic",
        Medigun,
        maxHealth: 120,
        runPower: 1.09f,
        jumpStrength: LegacyMovementModel.DefaultJumpStrength,
        maxAirJumps: 0,
        tauntLengthFrames: 10);

    public static CharacterClassDefinition Spy { get; } = CreateDefinition(
        PlayerClass.Spy,
        "Spy",
        Revolver,
        maxHealth: 100,
        runPower: 1.08f,
        jumpStrength: LegacyMovementModel.DefaultJumpStrength,
        maxAirJumps: 0,
        tauntLengthFrames: 10);

    public static CharacterClassDefinition Quote { get; } = CreateDefinition(
        PlayerClass.Quote,
        "Quote",
        Blade,
        maxHealth: 140,
        runPower: 1.07f,
        jumpStrength: LegacyMovementModel.DefaultJumpStrength,
        maxAirJumps: 0,
        tauntLengthFrames: 16);

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

    private static CharacterClassDefinition CreateDefinition(
        PlayerClass id,
        string displayName,
        PrimaryWeaponDefinition primaryWeapon,
        int maxHealth,
        float runPower,
        float jumpStrength,
        int maxAirJumps,
        int tauntLengthFrames)
    {
        return new CharacterClassDefinition(
            Id: id,
            DisplayName: displayName,
            PrimaryWeapon: primaryWeapon,
            MaxHealth: maxHealth,
            Width: LegacyWidth,
            Height: LegacyHeight,
            RunPower: runPower,
            JumpStrength: jumpStrength,
            MaxRunSpeed: LegacyMovementModel.GetMaxRunSpeed(runPower),
            GroundAcceleration: LegacyMovementModel.GetContinuousRunDrive(runPower),
            GroundDeceleration: LegacyMovementModel.GetContinuousRunDrive(runPower),
            Gravity: LegacyMovementModel.GetGravityPerSecondSquared(),
            JumpSpeed: LegacyMovementModel.GetJumpSpeed(jumpStrength),
            MaxAirJumps: maxAirJumps,
            TauntLengthFrames: tauntLengthFrames);
    }
}
