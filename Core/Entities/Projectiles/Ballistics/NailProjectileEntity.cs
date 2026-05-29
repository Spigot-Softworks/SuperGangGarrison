namespace OpenGarrison.Core;

public sealed class NailProjectileEntity : NeedleProjectileEntity
{
    public new const int DamagePerHit = 12;

    public override int Damage => DamagePerHit;

    public NailProjectileEntity(
        int id,
        PlayerTeam team,
        int ownerId,
        float x,
        float y,
        float velocityX,
        float velocityY) : base(id, team, ownerId, x, y, velocityX, velocityY)
    {
    }
}
