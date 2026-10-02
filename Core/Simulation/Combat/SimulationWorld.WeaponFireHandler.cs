namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    internal WeaponFireHandler WeaponHandler => _weaponFireHandler ??= new WeaponFireHandler(this);
    private WeaponFireHandler? _weaponFireHandler;
}
