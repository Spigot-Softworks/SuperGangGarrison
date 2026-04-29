from __future__ import annotations

import argparse
import json
from copy import deepcopy
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Mirror a successful CTF rollout to the opposite TwodFortTwo team.")
    parser.add_argument("--input", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--mirror-x", type=float, required=True)
    parser.add_argument("--team", choices=("Red", "Blue"), required=True)
    parser.add_argument("--class-id", default="")
    parser.add_argument("--task", default="")
    return parser.parse_args()


def flip_number(value: Any) -> Any:
    if isinstance(value, (int, float)):
        return -value
    return value


def mirror_world_x(value: Any, mirror_x: float) -> Any:
    if isinstance(value, (int, float)):
        return mirror_x - float(value)
    return value


def swap_keys(document: dict[str, Any], left_key: str, right_key: str) -> None:
    left = document.get(left_key)
    right = document.get(right_key)
    if left_key in document or right_key in document:
        document[left_key] = right
        document[right_key] = left


def mirror_objective(document: dict[str, Any], mirror_x: float) -> None:
    if not isinstance(document, dict):
        return

    for key in ("WorldX", "HomeX"):
        if key in document:
            document[key] = mirror_world_x(document[key], mirror_x)
    for key in ("RelativeX", "HomeRelativeX"):
        if key in document:
            document[key] = flip_number(document[key])


def mirror_waypoint(document: dict[str, Any], mirror_x: float) -> None:
    if not isinstance(document, dict):
        return

    if "WorldX" in document:
        document["WorldX"] = mirror_world_x(document["WorldX"], mirror_x)
    if "RelativeX" in document:
        document["RelativeX"] = flip_number(document["RelativeX"])


def mirror_traversal(document: dict[str, Any]) -> None:
    if not isinstance(document, dict):
        return

    for key in (
        "ExpectedMoveDirection",
        "CurrentNodeRelativeX",
        "TargetNodeRelativeX",
        "SegmentDeltaX",
    ):
        if key in document:
            document[key] = flip_number(document[key])


def mirror_probes(document: dict[str, Any]) -> None:
    if not isinstance(document, dict):
        return

    for left, right in (
        ("LeftFootObstacleDistance", "RightFootObstacleDistance"),
        ("LeftHeadObstacleDistance", "RightHeadObstacleDistance"),
        ("LeftGroundDistance", "RightGroundDistance"),
        ("LeftDropDepth", "RightDropDepth"),
        ("TouchingLeftWall", "TouchingRightWall"),
    ):
        swap_keys(document, left, right)


def mirror_terrain(document: dict[str, Any]) -> None:
    if not isinstance(document, dict):
        return

    left_suffixes = (
        "LandingRelativeX",
        "LandingRelativeY",
        "LandingSurfaceDeltaY",
        "LandingObjectiveDistanceDelta",
        "LandingIsHigher",
        "LandingRequiresJump",
    )
    for suffix in left_suffixes:
        swap_keys(document, f"Left{suffix}", f"Right{suffix}")

    for key in ("LeftLandingRelativeX", "RightLandingRelativeX", "BestUpwardLandingRelativeX"):
        if key in document:
            document[key] = flip_number(document[key])
    if "BestUpwardLandingDirection" in document:
        document["BestUpwardLandingDirection"] = flip_number(document["BestUpwardLandingDirection"])

    swap_keys(document, "CurrentSurfaceClearanceLeft", "CurrentSurfaceClearanceRight")


def mirror_relative_actor(document: dict[str, Any]) -> None:
    if not isinstance(document, dict):
        return

    if "RelativeX" in document:
        document["RelativeX"] = flip_number(document["RelativeX"])


def mirror_observation(observation: Any, mirror_x: float, team: str, class_id: str, task: str) -> Any:
    if not isinstance(observation, dict):
        return observation

    result = deepcopy(observation)
    result["Team"] = team
    if class_id:
        result["ClassId"] = class_id
    if task:
        result["TaskPhase"] = task

    for key in ("BotX",):
        if key in result:
            result[key] = mirror_world_x(result[key], mirror_x)
    for key in (
        "VelocityX",
        "FacingDirectionX",
        "PreviousVelocityX",
        "PreviousPositionDeltaX",
        "PreviousFacingDirectionX",
        "PreviousMoveInput",
    ):
        if key in result:
            result[key] = flip_number(result[key])

    mirror_objective(result.get("Objective", {}), mirror_x)
    mirror_waypoint(result.get("Waypoint", {}), mirror_x)
    mirror_traversal(result.get("Traversal", {}))
    mirror_probes(result.get("Probes", {}))
    mirror_relative_actor(result.get("NearestVisibleEnemy", {}))
    mirror_relative_actor(result.get("NearestVisibleTeammate", {}))
    mirror_terrain(result.get("TerrainAffordance", {}))
    return result


def mirror_action(action: Any, mirror_x: float) -> Any:
    if not isinstance(action, dict):
        return action

    result = deepcopy(action)
    if "MoveDirection" in result:
        result["MoveDirection"] = flip_number(result["MoveDirection"])
    if "AimWorldX" in result:
        result["AimWorldX"] = mirror_world_x(result["AimWorldX"], mirror_x)
    return result


def mirror_rollout(rollout: dict[str, Any], mirror_x: float, team: str, class_id: str, task: str) -> dict[str, Any]:
    result = deepcopy(rollout)
    result["Team"] = team
    if class_id:
        result["ClassId"] = class_id
    if task:
        result["TaskPhase"] = task
    result["MirroredFromTeam"] = rollout.get("Team")
    result["MirrorX"] = mirror_x

    steps = result.get("Steps", [])
    if isinstance(steps, list):
        for step in steps:
            if not isinstance(step, dict):
                continue
            step["Observation"] = mirror_observation(step.get("Observation"), mirror_x, team, class_id, task)
            step["NextObservation"] = mirror_observation(step.get("NextObservation"), mirror_x, team, class_id, task)
            step["Action"] = mirror_action(step.get("Action"), mirror_x)

    return result


def main() -> None:
    args = parse_args()
    input_path = Path(args.input)
    output_path = Path(args.output)
    with input_path.open("r", encoding="utf-8") as handle:
        rollout = json.load(handle)

    mirrored = mirror_rollout(
        rollout,
        mirror_x=args.mirror_x,
        team=args.team,
        class_id=args.class_id,
        task=args.task,
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)
    with output_path.open("w", encoding="utf-8") as handle:
        json.dump(mirrored, handle, indent=2)
    print(f"saved mirrored rollout={output_path}")


if __name__ == "__main__":
    main()
