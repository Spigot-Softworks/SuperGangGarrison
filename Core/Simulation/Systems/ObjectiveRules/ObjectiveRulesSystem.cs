namespace OpenGarrison.Core;

internal sealed partial class ObjectiveRulesSystem
{
    private readonly IObjectiveRulesHost _host;

    public ObjectiveRulesSystem(IObjectiveRulesHost host)
    {
        _host = host;
    }
}
