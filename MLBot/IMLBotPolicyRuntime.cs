using OpenGarrison.MLBot.Contracts;

namespace OpenGarrison.MLBot;

public interface IMLBotPolicyRuntime
{
    MLBotAction Evaluate(in MLBotObservation observation);
}
