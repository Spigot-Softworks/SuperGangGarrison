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
    public const int UberReady = 46;
    public const int Burning = 49;
    public const int CustomBubbleFrameBase = 1000;
    public const int CustomBubbleSlotCount = 3;

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
            PlayerClass.Medic => offset + 5,
            PlayerClass.Engineer => offset + 6,
            PlayerClass.Spy => offset + 7,
            PlayerClass.Sniper => offset + 8,
            PlayerClass.Quote => team == PlayerTeam.Blue ? 48 : 47,
            _ => Alert,
        };
    }

    public static int GetCustomBubbleFrame(int slotIndex)
    {
        return CustomBubbleFrameBase + Math.Clamp(slotIndex, 0, CustomBubbleSlotCount - 1);
    }

    public static bool TryGetCustomBubbleSlot(int frameIndex, out int slotIndex)
    {
        slotIndex = frameIndex - CustomBubbleFrameBase;
        if (slotIndex is >= 0 and < CustomBubbleSlotCount)
        {
            return true;
        }

        slotIndex = -1;
        return false;
    }
}
