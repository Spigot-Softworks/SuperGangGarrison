namespace OpenGarrison.Core;

public abstract class SimulationEntity
{
    protected SimulationEntity(int id)
    {
        Id = id;
    }

    public int Id { get; }
}
