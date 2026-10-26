namespace OpenGarrison.Core;

public enum PlayerClass
{
    Scout = 1,
    Engineer = 2,
    Pyro = 3,
    Soldier = 4,
    Demoman = 5,
    Heavy = 6,
    Sniper = 7,
    Medic = 8,
    Spy = 9,
    Quote = 10,

    // Neutral slot shared by plugin classes that declare `basePlayerClass: "Custom"`.
    // It never has a legacy runtime binding and matches no class-specific engine
    // branch; such classes are identified by their gameplay class id instead.
    Custom = 11,
}

public static class PlayerClassSlots
{
    // The legacy slots with stock runtime bindings, in enum order. Use this instead of
    // Enum.GetValues<PlayerClass>() wherever every slot must resolve a stock definition.
    public static readonly PlayerClass[] Stock =
    [
        PlayerClass.Scout,
        PlayerClass.Engineer,
        PlayerClass.Pyro,
        PlayerClass.Soldier,
        PlayerClass.Demoman,
        PlayerClass.Heavy,
        PlayerClass.Sniper,
        PlayerClass.Medic,
        PlayerClass.Spy,
        PlayerClass.Quote,
    ];

    public static bool IsStock(PlayerClass playerClass)
    {
        return playerClass >= PlayerClass.Scout && playerClass <= PlayerClass.Quote;
    }
}
