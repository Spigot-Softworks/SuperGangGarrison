from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build suffix rollout teachers and world-start scenarios from a successful rollout."
    )
    parser.add_argument("--rollout", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--tick", action="append", type=int, required=True)
    parser.add_argument("--name-prefix", default="")
    parser.add_argument("--extra-ticks", type=int, default=240)
    parser.add_argument("--min-ticks", type=int, default=300)
    return parser.parse_args()


def safe_name(value: str) -> str:
    return "".join(ch.lower() if ch.isalnum() else "_" for ch in value).strip("_")


def find_start_index(steps: list[dict[str, Any]], source_tick: int) -> int:
    for index, step in enumerate(steps):
        if int(step.get("Tick", index)) >= source_tick:
            return index
    return max(0, len(steps) - 1)


def scenario_from_observation(
    name: str,
    rollout: dict[str, Any],
    observation: dict[str, Any],
    max_ticks: int,
) -> dict[str, Any]:
    objective_distance = float(observation.get("ObjectiveDistance", 0.0))
    rollout_task_phase = str(rollout.get("TaskPhase", "") or "")
    task_phase = str(observation.get("TaskPhase", "") or rollout_task_phase)
    if not task_phase or task_phase.casefold() == "none":
        task_phase = rollout_task_phase
    success_criterion = {
        "AttackIntel": "attack_pickup",
        "ReturnIntel": "return_score",
        "CaptureObjective": "capture",
    }.get(task_phase, "terminal_success")
    return {
        "name": name,
        "level_name": str(rollout.get("LevelName", observation.get("LevelName", ""))),
        "team": str(rollout.get("Team", observation.get("Team", ""))),
        "class_id": str(rollout.get("ClassId", observation.get("ClassId", ""))),
        "task": task_phase,
        "ticks": max_ticks,
        "start_x": float(observation.get("BotX", 0.0)),
        "start_y": float(observation.get("BotY", 0.0)),
        "start_vx": float(observation.get("VelocityX", 0.0)),
        "start_vy": float(observation.get("VelocityY", 0.0)),
        "carrying_intel": bool(observation.get("IsCarryingIntel", False)),
        "start_is_grounded": bool(observation.get("IsGrounded", False)),
        "start_remaining_air_jumps": int(observation.get("RemainingAirJumps", 0)),
        "start_facing_dir_x": float(observation.get("FacingDirectionX", 1.0)),
        "start_prev_move": int(observation.get("PreviousMoveInput", 0)),
        "start_prev_jump_held": bool(observation.get("PreviousJumpHeld", False)),
        "start_prev_drop": bool(observation.get("PreviousDropInput", False)),
        "start_prev_fire_primary": bool(observation.get("PreviousActionFirePrimary", False)),
        "start_prev_fire_secondary": bool(observation.get("PreviousActionFireSecondary", False)),
        "start_prev_dx": float(observation.get("PreviousPositionDeltaX", 0.0)),
        "start_prev_dy": float(observation.get("PreviousPositionDeltaY", 0.0)),
        "start_prev_vx": float(observation.get("PreviousVelocityX", 0.0)),
        "start_prev_vy": float(observation.get("PreviousVelocityY", 0.0)),
        "start_prev_facing_dir_x": float(observation.get("PreviousFacingDirectionX", 0.0)),
        "start_prev_is_grounded": bool(observation.get("PreviousIsGrounded", False)),
        "start_objective_distance": objective_distance,
        "start_objective_distance_delta": float(observation.get("ObjectiveDistanceDelta", 0.0)),
        "start_prev_objective_distance_delta": float(observation.get("PreviousObjectiveDistanceDelta", 0.0)),
        "start_airborne_ticks": float(observation.get("AirborneTicks", 0.0)),
        "start_jump_ticks": float(observation.get("JumpTicks", 0.0)),
        "start_frames_since_jump_pressed": float(observation.get("FramesSinceJumpPressed", 60.0)),
        "start_frames_since_jump_released": float(observation.get("FramesSinceJumpReleased", 60.0)),
        "success_criterion": success_criterion,
    }


def build_suffix_rollout(rollout: dict[str, Any], start_index: int) -> dict[str, Any]:
    suffix_steps = rollout["Steps"][start_index:]
    task_phase = str(suffix_steps[0].get("Observation", {}).get("TaskPhase", "") or rollout.get("TaskPhase", ""))
    return {
        **rollout,
        "TaskPhase": task_phase,
        "TicksElapsed": len(suffix_steps),
        "Steps": suffix_steps,
        "CurriculumSourceTick": int(suffix_steps[0].get("Tick", start_index)) if suffix_steps else 0,
        "CurriculumStartIndex": start_index,
    }


def main() -> None:
    args = parse_args()
    rollout_path = Path(args.rollout)
    output_dir = Path(args.output_dir)
    suffix_dir = output_dir / "suffix-rollouts"
    suffix_dir.mkdir(parents=True, exist_ok=True)

    with rollout_path.open("r", encoding="utf-8") as handle:
        rollout: dict[str, Any] = json.load(handle)

    steps = rollout.get("Steps", [])
    if not isinstance(steps, list) or not steps:
        raise ValueError("rollout has no Steps")

    scenario_records: list[dict[str, Any]] = []
    summary_records: list[dict[str, Any]] = []
    prefix = args.name_prefix or safe_name(rollout_path.stem)
    for source_tick in args.tick:
        start_index = find_start_index(steps, source_tick)
        suffix = build_suffix_rollout(rollout, start_index)
        first_observation = suffix["Steps"][0]["Observation"]
        remaining = max(1, len(suffix["Steps"]))
        max_ticks = max(args.min_ticks, remaining + args.extra_ticks)
        scenario_name = f"{prefix}_from_tick_{source_tick}"
        suffix_path = suffix_dir / f"{safe_name(scenario_name)}.json"
        with suffix_path.open("w", encoding="utf-8") as handle:
            json.dump(suffix, handle, indent=2)

        scenario = scenario_from_observation(scenario_name, rollout, first_observation, max_ticks)
        scenario_records.append(scenario)
        summary_records.append(
            {
                "source_tick": source_tick,
                "start_index": start_index,
                "suffix_rollout": str(suffix_path),
                "scenario": scenario,
                "remaining_teacher_steps": remaining,
                "start_objective_distance": float(first_observation.get("ObjectiveDistance", 0.0)),
            }
        )

    scenario_path = output_dir / "curriculum-scenarios.json"
    with scenario_path.open("w", encoding="utf-8") as handle:
        json.dump({"schema": "mlbot-backward-curriculum-v1", "scenarios": scenario_records}, handle, indent=2)
    with (output_dir / "curriculum-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(summary_records, handle, indent=2)
    print(f"saved scenarios={scenario_path}")
    print(f"saved suffix_dir={suffix_dir}")


if __name__ == "__main__":
    main()
