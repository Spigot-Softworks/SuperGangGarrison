using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenGarrison.Core;

public sealed partial class SimulationWorld
{
    private static partial class MatchObjectiveFlowSystem
    {
        public static void AdvanceMatchState(SimulationWorld world)
        {
            if (world.MatchRules.Mode == GameModeKind.Arena)
            {
                AdvanceArenaMatchState(world);
                return;
            }

            if (world.MatchRules.Mode == GameModeKind.ControlPoint)
            {
                AdvanceControlPointMatchState(world);
                return;
            }

            if (SimulationWorld.IsKothMode(world.MatchRules.Mode))
            {
                world.AdvanceKothMatchState();
                return;
            }

            if (world.MatchRules.Mode == GameModeKind.Generator)
            {
                AdvanceGeneratorMatchState(world);
                return;
            }

            if (world.MatchRules.Mode == GameModeKind.TeamDeathmatch)
            {
                AdvanceTeamDeathmatchMatchState(world);
                return;
            }

            if (world.MatchState.IsEnded)
            {
                return;
            }

            var capWinner = GetCapLimitWinner(world);
            if (capWinner.HasValue)
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = capWinner };
                world.QueuePendingMapChange();
                return;
            }

            if (world.MatchState.Phase == MatchPhase.Overtime)
            {
                if (!AreCaptureTheFlagObjectivesSettled(world))
                {
                    return;
                }

                world.MatchState = world.MatchState with
                {
                    Phase = MatchPhase.Ended,
                    WinnerTeam = GetHigherCapWinner(world),
                };
                world.QueuePendingMapChange();
                return;
            }

            if (world.MatchState.TimeRemainingTicks > 0)
            {
                world.MatchState = world.MatchState with { TimeRemainingTicks = world.MatchState.TimeRemainingTicks - 1 };
                if (world.MatchState.TimeRemainingTicks > 0)
                {
                    return;
                }
            }

            if (AreCaptureTheFlagObjectivesSettled(world))
            {
                world.MatchState = world.MatchState with { Phase = MatchPhase.Ended, WinnerTeam = GetHigherCapWinner(world) };
                world.QueuePendingMapChange();
                return;
            }

            world.MatchState = world.MatchState with { Phase = MatchPhase.Overtime, WinnerTeam = null };
        }

        public static void UpdateGeneratorState(SimulationWorld world)
        {
            _ = world;
            // Generator objectives are passive. Damage resolution happens in the combat systems.
        }
    }
}
