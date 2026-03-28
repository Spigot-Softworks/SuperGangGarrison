namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{

    public void TriggerChatBubble(int frameIndex)
    {
        if (!IsAlive || frameIndex < 0)
        {
            return;
        }

        ChatBubbleFrameIndex = frameIndex;
        ChatBubbleTicksRemaining = ChatBubbleHoldTicks;
        ChatBubbleAlpha = 1f;
        IsChatBubbleVisible = true;
        IsChatBubbleFading = false;
    }

    public void ClearChatBubble()
    {
        IsChatBubbleVisible = false;
        ChatBubbleFrameIndex = 0;
        ChatBubbleAlpha = 0f;
        IsChatBubbleFading = false;
        ChatBubbleTicksRemaining = 0;
    }

    public void AdvanceChatBubbleState()
    {
        if (!IsChatBubbleVisible)
        {
            return;
        }

        if (!IsChatBubbleFading)
        {
            if (ChatBubbleTicksRemaining > 0)
            {
                ChatBubbleTicksRemaining -= 1;
            }

            if (ChatBubbleTicksRemaining <= 0)
            {
                IsChatBubbleFading = true;
            }

            return;
        }

        ChatBubbleAlpha = MathF.Max(0f, ChatBubbleAlpha - ChatBubbleFadePerTick);
        if (ChatBubbleAlpha > 0f)
        {
            return;
        }

        ClearChatBubble();
    }
}
