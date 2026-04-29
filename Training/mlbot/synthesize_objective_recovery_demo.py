from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Convert failed rollout states into BC recovery samples using only "
            "world-truth objective geometry. This is training data generation, "
            "not a runtime policy override."
        )
    )
    parser.add_argument("--rollout", required=True)
    parser.add_argument("--out", required=True)
    parser.add_argument("--capture-kind", default="SyntheticRecovery")
    parser.add_argument("--label", default="objective_recovery")
    parser.add_argument("--max-samples", type=int, default=0)
    parser.add_argument("--near-distance", type=float, default=640.0)
    parser.add_argument("--include-carrying-intel", action="store_true")
    return parser.parse_args()


def sign_to_direction(value: float, deadzone: float = 18.0) -> int:
    if value > deadzone:
        return 1
    if value < -deadzone:
        return -1
    return 0


def build_recovery_action(observation: dict[str, Any]) -> dict[str, Any]:
    objective = observation.get("Objective", {})
    relative_x = float(objective.get("RelativeX", 0.0))
    relative_y = float(objective.get("RelativeY", 0.0))
    objective_world_x = float(objective.get("WorldX", observation.get("BotX", 0.0) + relative_x))
    objective_world_y = float(objective.get("WorldY", observation.get("BotY", 0.0) + relative_y))

    grounded = bool(observation.get("IsGrounded", False))
    stuck_ticks = float(observation.get("StuckTicks", 0.0))
    velocity_x = abs(float(observation.get("VelocityX", 0.0)))
    needs_vertical_progress = relative_y < -36.0
    stalled_on_ground = grounded and stuck_ticks >= 2.0
    grounded_without_lateral_progress = grounded and velocity_x < 12.0 and abs(relative_x) > 32.0

    return {
        "MoveDirection": sign_to_direction(relative_x),
        "Jump": bool(grounded and (needs_vertical_progress or stalled_on_ground or grounded_without_lateral_progress)),
        "Crouch": False,
        "FirePrimary": False,
        "FireSecondary": False,
        "DropIntel": False,
        "AimWorldX": objective_world_x,
        "AimWorldY": objective_world_y,
    }


def should_include_step(step: dict[str, Any], args: argparse.Namespace) -> bool:
    observation = step.get("Observation", {})
    if not args.include_carrying_intel and bool(observation.get("IsCarryingIntel", False)):
        return False
    if not observation.get("Objective", {}).get("HasObjective", False):
        return False
    if args.near_distance > 0 and float(observation.get("ObjectiveDistance", 0.0)) > args.near_distance:
        return False
    return True


def main() -> None:
    args = parse_args()
    rollout_path = Path(args.rollout)
    with rollout_path.open("r", encoding="utf-8-sig") as handle:
        rollout = json.load(handle)

    samples: list[dict[str, Any]] = []
    for step in rollout.get("Steps", []):
        if not should_include_step(step, args):
            continue
        samples.append(
            {
                "Tick": step.get("Tick", len(samples) + 1),
                "Observation": step["Observation"],
                "Action": build_recovery_action(step["Observation"]),
                "ResolvedPhase": rollout.get("TaskPhase", "AttackIntel"),
                "UsedHumanOverride": True,
            }
        )
        if args.max_samples > 0 and len(samples) >= args.max_samples:
            break

    if not samples:
        raise ValueError(f"no eligible recovery samples in {rollout_path}")

    metadata = {
        "SchemaVersion": "mlbot-demo-v3",
        "LevelName": rollout.get("LevelName", ""),
        "MapAreaIndex": 0,
        "Mode": "CaptureTheFlag",
        "Team": rollout.get("Team", ""),
        "ClassId": rollout.get("ClassId", ""),
        "RequestedPhase": rollout.get("TaskPhase", "AttackIntel"),
        "CaptureKind": args.capture_kind,
        "PolicyModelPath": rollout.get("ModelPath", ""),
        "Label": args.label,
        "RecordedAtUtc": "",
        "TickCount": len(samples),
        "CaptureMaxTicks": len(samples),
        "ShortCapture": True,
        "Success": True,
        "Outcome": "synthetic_recovery",
    }

    output = {"Metadata": metadata, "Samples": samples}
    out_path = Path(args.out)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with out_path.open("w", encoding="utf-8") as handle:
        json.dump(output, handle, indent=2)
    print(f"saved synthetic recovery samples={len(samples)} out={out_path}")


if __name__ == "__main__":
    main()
