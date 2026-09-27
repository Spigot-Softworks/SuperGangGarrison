namespace OpenGarrison.Core.BotBrain;

public interface INavigationGraphProvider
{
    NavGraph? GetGraph(SimpleLevel level);
}
