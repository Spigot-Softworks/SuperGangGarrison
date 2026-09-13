using OpenGarrison.GameplayModding;
using OpenGarrison.Protocol;

namespace OpenGarrison.Core;

public sealed partial class PlayerEntity
{
    public Protocol64UmbrellaState? CaptureProtocol64UmbrellaState()
        => ClassId == PlayerClass.Quote && HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella)
            ? new(CivvieUmbrellaChargeTicks, IsCivvieUmbrellaActive, IsCivvieUmbrellaBroken,
                CivvieUmbrellaOpeningElapsedTicks, CivvieUmbrellaOpeningSequence,
                CivvieUmbrellaOpeningAirblastTriggered, CivvieUmbrellaAirLiftUsed,
                CivvieUmbrellaOpeningTickAccumulator)
            : null;

    private void HydrateProtocol64UmbrellaState(Protocol64UmbrellaState? state)
    {
        if (state is null || ClassId != PlayerClass.Quote
            || !HasSecondaryBehavior(BuiltInGameplayBehaviorIds.CivvieUmbrella))
        {
            ResetCivvieUmbrellaState();
            return;
        }

        CivvieUmbrellaChargeTicks = Math.Clamp(state.ChargeTicks, 0, CivvieUmbrellaMaxChargeTicks);
        IsCivvieUmbrellaBroken = state.IsBroken;
        IsCivvieUmbrellaActive = IsAlive && state.IsActive && !IsCivvieUmbrellaBroken && CivvieUmbrellaChargeTicks > 0;
        CivvieUmbrellaOpeningElapsedTicks = Math.Clamp(state.OpeningElapsedTicks, 0, CivvieUmbrellaOpeningDurationTicks);
        CivvieUmbrellaOpeningSequence = Math.Max(0, state.OpeningSequence);
        CivvieUmbrellaOpeningAirblastTriggered = state.OpeningAirblastTriggered;
        CivvieUmbrellaAirLiftUsed = state.AirLiftUsed;
        CivvieUmbrellaOpeningTickAccumulator = double.IsFinite(state.OpeningTickAccumulator)
            ? Math.Clamp(state.OpeningTickAccumulator, 0, Math.BitDecrement(1d)) : 0;
    }
}
