namespace OpenGarrison.Core;

public static class ChatBubbleFrameCatalog
{
    public const int Alert = 20;
    public const int Question = 21;
    public const int Happy = 24;
    public const int ObjectiveAlert = 33;
    public const int ThumbsUp = 36;
    public const int Attack = 41;
    public const int Shield = 42;
    public const int Confirm = 43;
    public const int Deny = 44;
    public const int Medic = 45;
    public const int Burning = 49;

    public static int GetIntelFrame(PlayerTeam team)
    {
        return team == PlayerTeam.Blue ? 19 : 9;
    }

    public static int GetClassPortraitFrame(PlayerClass playerClass, PlayerTeam team)
    {
        var offset = team == PlayerTeam.Blue ? 10 : 0;
        return playerClass switch
        {
            PlayerClass.Scout => offset,
            PlayerClass.Pyro => offset + 1,
            PlayerClass.Soldier => offset + 2,
            PlayerClass.Demoman => offset + 3,
            PlayerClass.Heavy => offset + 4,
            PlayerClass.Engineer => offset + 5,
            PlayerClass.Medic => offset + 6,
            PlayerClass.Sniper => offset + 7,
            PlayerClass.Spy => offset + 8,
            _ => Alert,
        };
    }
}
