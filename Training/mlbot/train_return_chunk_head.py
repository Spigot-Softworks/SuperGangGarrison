from __future__ import annotations

import argparse
import copy
import json
from pathlib import Path
from typing import Any

import numpy as np
import torch
import torch.nn as nn
import torch.nn.functional as F

from mlbot_dataset import DISTANCE_SCALE, FEATURE_COUNT, vectorize_observation


BINARY_ACTIONS = ("Jump", "Crouch", "FirePrimary", "FireSecondary", "DropIntel")

CLASS_PHYSICS_PROFILES: dict[str, dict[str, Any]] = {
    "Scout": {
        "MaxAirJumps": 1,
        "RunPower": 1.4,
        "MaxRunSpeed": 238.00005,
        "GroundAcceleration": 33.2093,
        "GroundDeceleration": 33.2093,
        "Gravity": 540.0,
        "JumpSpeed": 249.0,
        "Width": 13.0,
        "Height": 34.0,
    },
    "Soldier": {
        "MaxAirJumps": 0,
        "RunPower": 0.9,
        "MaxRunSpeed": 153.00003,
        "GroundAcceleration": 21.348835,
        "GroundDeceleration": 21.348835,
        "Gravity": 540.0,
        "JumpSpeed": 249.0,
        "Width": 13.0,
        "Height": 32.0,
    },
    "Demoman": {
        "MaxAirJumps": 0,
        "RunPower": 1.0,
        "MaxRunSpeed": 170.00003,
        "GroundAcceleration": 23.720928,
        "GroundDeceleration": 23.720928,
        "Gravity": 540.0,
        "JumpSpeed": 249.0,
        "Width": 15.0,
        "Height": 34.0,
    },
    "Heavy": {
        "MaxAirJumps": 0,
        "RunPower": 0.8,
        "MaxRunSpeed": 136.00003,
        "GroundAcceleration": 18.976744,
        "GroundDeceleration": 18.976744,
        "Gravity": 540.0,
        "JumpSpeed": 249.0,
        "Width": 19.0,
        "Height": 36.0,
    },
    "Pyro": {
        "MaxAirJumps": 0,
        "RunPower": 1.1,
        "MaxRunSpeed": 187.00003,
        "GroundAcceleration": 26.093025,
        "GroundDeceleration": 26.093025,
        "Gravity": 540.0,
        "JumpSpeed": 249.0,
        "Width": 15.0,
        "Height": 30.0,
    },
}


class ReturnChunkModel(nn.Module):
    def __init__(self, horizon: int, input_size: int = FEATURE_COUNT, hidden_size: int = 256) -> None:
        super().__init__()
        self.horizon = horizon
        self.backbone = nn.Sequential(
            nn.Linear(input_size, hidden_size),
            nn.ReLU(),
            nn.Linear(hidden_size, hidden_size),
            nn.ReLU(),
        )
        self.move_head = nn.Linear(hidden_size, horizon * 3)
        self.binary_head = nn.Linear(hidden_size, horizon * 5)
        self.aim_head = nn.Linear(hidden_size, horizon * 2)

    def forward(self, obs: torch.Tensor) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        hidden = self.backbone(obs)
        batch = obs.shape[0]
        move_logits = self.move_head(hidden).reshape(batch, self.horizon, 3)
        binary_logits = self.binary_head(hidden).reshape(batch, self.horizon, 5)
        aim = torch.tanh(self.aim_head(hidden).reshape(batch, self.horizon, 2))
        return move_logits, binary_logits, aim


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Train a short task-scoped action-chunk option head.")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--rollout-path", action="append", default=[])
    parser.add_argument("--rollout-dir", action="append", default=[])
    parser.add_argument("--rollout-glob", default="*.json")
    parser.add_argument("--init-checkpoint", default="")
    parser.add_argument("--horizon", type=int, default=8)
    parser.add_argument("--epochs", type=int, default=30)
    parser.add_argument("--batches-per-epoch", type=int, default=64)
    parser.add_argument("--batch-size", type=int, default=512)
    parser.add_argument("--learning-rate", type=float, default=2e-4)
    parser.add_argument("--reference-kl-coef", type=float, default=0.0)
    parser.add_argument("--seed", type=int, default=1337)
    parser.add_argument("--success-only", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--task-phase", default="ReturnIntel")
    parser.add_argument("--requires-carrying-intel", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--min-objective-distance", type=float, default=0.0)
    parser.add_argument("--max-objective-distance", type=float, default=0.0)
    parser.add_argument("--min-objective-relative-x", type=float, default=None)
    parser.add_argument("--max-objective-relative-x", type=float, default=None)
    parser.add_argument("--min-objective-relative-y", type=float, default=None)
    parser.add_argument("--max-objective-relative-y", type=float, default=None)
    parser.add_argument("--max-samples", type=int, default=0)
    parser.add_argument("--mirror-augment", action="store_true")
    parser.add_argument(
        "--mirror-center-x",
        type=float,
        default=0.0,
        help="World X mirror center for --mirror-augment. For TwodFortTwo CTF bases this is 1713.",
    )
    parser.add_argument(
        "--jump-sample-weight",
        type=float,
        default=1.0,
        help="Multiply samples whose target chunk contains at least one jump action.",
    )
    parser.add_argument(
        "--stuck-sample-weight",
        type=float,
        default=1.0,
        help="Multiply samples whose source observation has StuckTicks above --stuck-ticks-threshold.",
    )
    parser.add_argument("--stuck-ticks-threshold", type=float, default=0.0)
    parser.add_argument(
        "--jump-positive-weight",
        type=float,
        default=1.0,
        help="Positive-class BCE weight for the jump channel inside chunk targets.",
    )
    parser.add_argument(
        "--objective-window-sample-weight",
        type=float,
        default=1.0,
        help="Multiply samples whose objective-relative fields match the optional objective window filters.",
    )
    parser.add_argument(
        "--advance-stalled-jump-targets",
        action="store_true",
        help=(
            "For grounded stalled anchors, train the current observation against the next near-future jump "
            "chunk instead of replaying the teacher's idle delay."
        ),
    )
    parser.add_argument("--advance-stall-threshold", type=float, default=8.0)
    parser.add_argument("--advance-max-lookahead", type=int, default=24)
    parser.add_argument("--advance-requires-grounded", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument(
        "--augment-class-profile",
        action="append",
        default=[],
        choices=sorted(CLASS_PHYSICS_PROFILES),
        help="Duplicate training anchors with a different class id and direct physics fields.",
    )
    parser.add_argument("--window-min-objective-distance", type=float, default=0.0)
    parser.add_argument("--window-max-objective-distance", type=float, default=0.0)
    parser.add_argument("--window-min-objective-relative-x", type=float, default=None)
    parser.add_argument("--window-max-objective-relative-x", type=float, default=None)
    parser.add_argument("--window-min-objective-relative-y", type=float, default=None)
    parser.add_argument("--window-max-objective-relative-y", type=float, default=None)
    parser.add_argument("--cpu", action="store_true")
    return parser.parse_args()


def swap_team(value: Any) -> Any:
    if value == "Red":
        return "Blue"
    if value == "Blue":
        return "Red"
    return value


def flip_optional_number(container: dict[str, Any], key: str) -> None:
    if key in container and container[key] is not None:
        container[key] = -float(container[key])


def mirror_world_x(container: dict[str, Any], key: str, center_x: float) -> None:
    if key in container and container[key] is not None:
        container[key] = (2.0 * center_x) - float(container[key])


def swap_prefixed_fields(container: dict[str, Any], left_prefix: str, right_prefix: str, suffixes: tuple[str, ...]) -> None:
    for suffix in suffixes:
        left_key = f"{left_prefix}{suffix}"
        right_key = f"{right_prefix}{suffix}"
        left_value = container.get(left_key)
        right_value = container.get(right_key)
        if right_key in container:
            container[left_key] = right_value
        if left_key in container:
            container[right_key] = left_value


def mirror_observation(observation: dict[str, Any], center_x: float) -> dict[str, Any]:
    mirrored = copy.deepcopy(observation)
    mirrored["Team"] = swap_team(mirrored.get("Team"))
    for key in ("BotX",):
        mirror_world_x(mirrored, key, center_x)
    for key in (
        "VelocityX",
        "FacingDirectionX",
        "PreviousVelocityX",
        "PreviousPositionDeltaX",
        "PreviousFacingDirectionX",
        "PreviousMoveInput",
    ):
        flip_optional_number(mirrored, key)

    objective = mirrored.get("Objective")
    if isinstance(objective, dict):
        for key in ("WorldX", "HomeX"):
            mirror_world_x(objective, key, center_x)
        for key in ("RelativeX", "HomeRelativeX"):
            flip_optional_number(objective, key)

    waypoint = mirrored.get("Waypoint")
    if isinstance(waypoint, dict):
        mirror_world_x(waypoint, "WorldX", center_x)
        flip_optional_number(waypoint, "RelativeX")

    traversal = mirrored.get("Traversal")
    if isinstance(traversal, dict):
        for key in (
            "ExpectedMoveDirection",
            "CurrentNodeRelativeX",
            "TargetNodeRelativeX",
            "SegmentDeltaX",
        ):
            flip_optional_number(traversal, key)

    for actor_key in ("NearestVisibleEnemy", "NearestVisibleTeammate"):
        actor = mirrored.get(actor_key)
        if isinstance(actor, dict):
            flip_optional_number(actor, "RelativeX")

    probes = mirrored.get("Probes")
    if isinstance(probes, dict):
        swap_prefixed_fields(
            probes,
            "Left",
            "Right",
            ("FootObstacleDistance", "HeadObstacleDistance", "GroundDistance", "DropDepth"),
        )
        left_wall = probes.get("TouchingLeftWall")
        right_wall = probes.get("TouchingRightWall")
        if "TouchingRightWall" in probes:
            probes["TouchingLeftWall"] = right_wall
        if "TouchingLeftWall" in probes:
            probes["TouchingRightWall"] = left_wall

    terrain = mirrored.get("TerrainAffordance")
    if isinstance(terrain, dict):
        swap_prefixed_fields(
            terrain,
            "LeftLanding",
            "RightLanding",
            (
                "RelativeX",
                "RelativeY",
                "SurfaceDeltaY",
                "ObjectiveDistanceDelta",
                "IsHigher",
                "RequiresJump",
            ),
        )
        has_left = terrain.get("HasLeftLanding")
        has_right = terrain.get("HasRightLanding")
        if "HasRightLanding" in terrain:
            terrain["HasLeftLanding"] = has_right
        if "HasLeftLanding" in terrain:
            terrain["HasRightLanding"] = has_left
        flip_optional_number(terrain, "LeftLandingRelativeX")
        flip_optional_number(terrain, "RightLandingRelativeX")
        flip_optional_number(terrain, "BestUpwardLandingRelativeX")
        flip_optional_number(terrain, "BestUpwardLandingDirection")
        left_clearance = terrain.get("CurrentSurfaceClearanceLeft")
        right_clearance = terrain.get("CurrentSurfaceClearanceRight")
        if "CurrentSurfaceClearanceRight" in terrain:
            terrain["CurrentSurfaceClearanceLeft"] = right_clearance
        if "CurrentSurfaceClearanceLeft" in terrain:
            terrain["CurrentSurfaceClearanceRight"] = left_clearance

    return mirrored


def mirror_action(action: dict[str, Any], center_x: float) -> dict[str, Any]:
    mirrored = dict(action)
    mirrored["MoveDirection"] = -int(mirrored.get("MoveDirection", 0))
    mirror_world_x(mirrored, "AimWorldX", center_x)
    return mirrored


def discover_rollout_paths(args: argparse.Namespace) -> list[Path]:
    paths = [Path(raw_path) for raw_path in args.rollout_path if Path(raw_path).is_file()]
    for raw_dir in args.rollout_dir:
        directory = Path(raw_dir)
        if directory.is_dir():
            paths.extend(sorted(directory.rglob(args.rollout_glob)))

    unique: list[Path] = []
    seen: set[Path] = set()
    for path in paths:
        resolved = path.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        unique.append(resolved)
    return unique


def load_rollout(path: Path) -> dict[str, Any] | None:
    with path.open("r", encoding="utf-8-sig") as handle:
        payload = json.load(handle)
    if not isinstance(payload, dict):
        return None
    steps = normalize_steps(payload)
    if not steps:
        return None
    payload = dict(payload)
    payload["Steps"] = steps
    if "Success" not in payload:
        payload["Success"] = bool(payload.get("Metadata", {}).get("Success", False))
    return payload


def normalize_steps(payload: dict[str, Any]) -> list[dict[str, Any]]:
    steps = payload.get("Steps")
    if isinstance(steps, list) and steps:
        return [step for step in steps if isinstance(step, dict)]

    samples = payload.get("Samples")
    if not isinstance(samples, list) or not samples:
        return []

    normalized: list[dict[str, Any]] = []
    for index, sample in enumerate(samples):
        if not isinstance(sample, dict):
            continue
        observation = sample.get("Observation")
        action = sample.get("Action")
        if not isinstance(observation, dict) or not isinstance(action, dict):
            continue
        normalized.append(
            {
                "Tick": sample.get("Tick", index + 1),
                "Observation": observation,
                "Action": action,
            }
        )
    return normalized


def is_chunk_anchor(observation: dict[str, Any], args: argparse.Namespace) -> bool:
    if str(observation.get("TaskPhase", "")).casefold() != str(args.task_phase).casefold():
        return False
    if args.requires_carrying_intel and not observation.get("IsCarryingIntel", False):
        return False
    distance = float(observation.get("ObjectiveDistance", 0.0))
    if args.min_objective_distance > 0.0 and distance < args.min_objective_distance:
        return False
    if args.max_objective_distance > 0.0 and distance > args.max_objective_distance:
        return False
    objective = observation.get("Objective", {})
    if not objective.get("HasObjective", False):
        return False
    relative_x = float(objective.get("RelativeX", 0.0))
    relative_y = float(objective.get("RelativeY", 0.0))
    if args.min_objective_relative_x is not None and relative_x < args.min_objective_relative_x:
        return False
    if args.max_objective_relative_x is not None and relative_x > args.max_objective_relative_x:
        return False
    if args.min_objective_relative_y is not None and relative_y < args.min_objective_relative_y:
        return False
    if args.max_objective_relative_y is not None and relative_y > args.max_objective_relative_y:
        return False
    return True


def move_direction_to_index(move_direction: int) -> int:
    return max(0, min(2, int(move_direction) + 1))


def action_binary_vector(action: dict[str, Any]) -> np.ndarray:
    return np.asarray([1.0 if action.get(name, False) else 0.0 for name in BINARY_ACTIONS], dtype=np.float32)


def action_aim_target(source_observation: dict[str, Any], action: dict[str, Any]) -> np.ndarray:
    bot_x = float(source_observation.get("BotX", 0.0))
    bot_y = float(source_observation.get("BotY", 0.0))
    aim_x = float(action.get("AimWorldX", bot_x))
    aim_y = float(action.get("AimWorldY", bot_y))
    return np.asarray(
        [
            np.clip((aim_x - bot_x) / DISTANCE_SCALE, -1.0, 1.0),
            np.clip((aim_y - bot_y) / DISTANCE_SCALE, -1.0, 1.0),
        ],
        dtype=np.float32,
    )


def matches_objective_window(observation: dict[str, Any], args: argparse.Namespace) -> bool:
    if args.objective_window_sample_weight <= 1.0:
        return False
    objective = observation.get("Objective", {})
    if not objective.get("HasObjective", False):
        return False
    distance = float(observation.get("ObjectiveDistance", 0.0))
    relative_x = float(objective.get("RelativeX", 0.0))
    relative_y = float(objective.get("RelativeY", 0.0))
    if args.window_min_objective_distance > 0.0 and distance < args.window_min_objective_distance:
        return False
    if args.window_max_objective_distance > 0.0 and distance > args.window_max_objective_distance:
        return False
    if args.window_min_objective_relative_x is not None and relative_x < args.window_min_objective_relative_x:
        return False
    if args.window_max_objective_relative_x is not None and relative_x > args.window_max_objective_relative_x:
        return False
    if args.window_min_objective_relative_y is not None and relative_y < args.window_min_objective_relative_y:
        return False
    if args.window_max_objective_relative_y is not None and relative_y > args.window_max_objective_relative_y:
        return False
    return True


def retarget_observation_class_profile(observation: dict[str, Any], class_id: str) -> dict[str, Any]:
    profile = CLASS_PHYSICS_PROFILES[class_id]
    retargeted = dict(observation)
    retargeted["ClassId"] = class_id
    for key, value in profile.items():
        retargeted[key] = value
    max_air_jumps = int(profile["MaxAirJumps"])
    retargeted["MaxAirJumps"] = max_air_jumps
    retargeted["RemainingAirJumps"] = min(int(retargeted.get("RemainingAirJumps", 0)), max_air_jumps)
    return retargeted


def build_chunk_dataset(args: argparse.Namespace) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, dict[str, Any]]:
    features: list[np.ndarray] = []
    move_targets: list[np.ndarray] = []
    binary_targets: list[np.ndarray] = []
    aim_targets: list[np.ndarray] = []
    sample_weights: list[float] = []
    source_counts: dict[str, int] = {}
    mirrored_sample_count = 0

    def target_index_for_anchor(observation: dict[str, Any], steps: list[dict[str, Any]], index: int) -> int:
        if not args.advance_stalled_jump_targets:
            return index
        if args.advance_requires_grounded and not bool(observation.get("IsGrounded", False)):
            return index
        if float(observation.get("StuckTicks", 0.0)) < float(args.advance_stall_threshold):
            return index
        max_lookahead = min(len(steps) - index, max(1, int(args.advance_max_lookahead)))
        for offset in range(1, max_lookahead):
            action = steps[index + offset].get("Action", {})
            if isinstance(action, dict) and bool(action.get("Jump", False)):
                return index + offset
        return index

    def append_sample(observation: dict[str, Any], steps: list[dict[str, Any]], index: int) -> bool:
        if not is_chunk_anchor(observation, args):
            return False
        feature = vectorize_observation(observation).astype(np.float32)
        target_index = target_index_for_anchor(observation, steps, index)
        chunk_moves = np.zeros(args.horizon, dtype=np.int64)
        chunk_binary = np.zeros((args.horizon, 5), dtype=np.float32)
        chunk_aim = np.zeros((args.horizon, 2), dtype=np.float32)
        for offset in range(args.horizon):
            action_index = min(target_index + offset, len(steps) - 1)
            action = steps[action_index].get("Action", {})
            chunk_moves[offset] = move_direction_to_index(int(action.get("MoveDirection", 0)))
            chunk_binary[offset, :] = action_binary_vector(action)
            chunk_aim[offset, :] = action_aim_target(observation, action)
        features.append(feature)
        move_targets.append(chunk_moves)
        binary_targets.append(chunk_binary)
        aim_targets.append(chunk_aim)
        weight = 1.0
        if args.jump_sample_weight > 1.0 and np.any(chunk_binary[:, 0] > 0.5):
            weight *= args.jump_sample_weight
        if (
            args.stuck_sample_weight > 1.0
            and args.stuck_ticks_threshold > 0.0
            and float(observation.get("StuckTicks", 0.0)) >= args.stuck_ticks_threshold
        ):
            weight *= args.stuck_sample_weight
        if matches_objective_window(observation, args):
            weight *= args.objective_window_sample_weight
        sample_weights.append(weight)
        return True

    for path in discover_rollout_paths(args):
        rollout = load_rollout(path)
        if rollout is None:
            continue
        if args.success_only and not bool(rollout.get("Success", False)):
            continue
        steps = rollout["Steps"]
        mirrored_steps: list[dict[str, Any]] = []
        if args.mirror_augment:
            if args.mirror_center_x <= 0.0:
                raise ValueError("--mirror-center-x must be positive when --mirror-augment is enabled")
            for original_step in steps:
                mirrored_steps.append(
                    {
                        **original_step,
                        "Observation": mirror_observation(original_step.get("Observation", {}), args.mirror_center_x),
                        "Action": mirror_action(original_step.get("Action", {}), args.mirror_center_x),
                    }
                )
        source_sample_count = 0
        for index, step in enumerate(steps):
            observation = step.get("Observation", {})
            if append_sample(observation, steps, index):
                source_sample_count += 1
            for class_id in args.augment_class_profile:
                if append_sample(retarget_observation_class_profile(observation, class_id), steps, index):
                    source_sample_count += 1
            if mirrored_steps and append_sample(mirrored_steps[index].get("Observation", {}), mirrored_steps, index):
                mirrored_sample_count += 1
                for class_id in args.augment_class_profile:
                    if append_sample(retarget_observation_class_profile(mirrored_steps[index].get("Observation", {}), class_id), mirrored_steps, index):
                        mirrored_sample_count += 1
        if source_sample_count > 0:
            source_counts[str(path)] = source_sample_count

    if not features:
        raise ValueError(f"no {args.task_phase} chunk samples were found")

    if args.max_samples > 0 and len(features) > args.max_samples:
        rng = np.random.default_rng(args.seed)
        indices = rng.choice(len(features), size=args.max_samples, replace=False)
        features = [features[int(index)] for index in indices]
        move_targets = [move_targets[int(index)] for index in indices]
        binary_targets = [binary_targets[int(index)] for index in indices]
        aim_targets = [aim_targets[int(index)] for index in indices]
        sample_weights = [sample_weights[int(index)] for index in indices]

    summary = {
        "sample_count": len(features),
        "mirrored_sample_count": mirrored_sample_count,
        "mirror_augment": bool(args.mirror_augment),
        "mirror_center_x": float(args.mirror_center_x),
        "source_counts": source_counts,
        "horizon": args.horizon,
        "task_phase": args.task_phase,
        "requires_carrying_intel": args.requires_carrying_intel,
        "min_objective_relative_x": args.min_objective_relative_x,
        "max_objective_relative_x": args.max_objective_relative_x,
        "min_objective_relative_y": args.min_objective_relative_y,
        "max_objective_relative_y": args.max_objective_relative_y,
        "sample_weight_mean": float(np.mean(sample_weights)),
        "sample_weight_max": float(np.max(sample_weights)),
        "jump_sample_weight": args.jump_sample_weight,
        "stuck_sample_weight": args.stuck_sample_weight,
        "stuck_ticks_threshold": args.stuck_ticks_threshold,
        "jump_positive_weight": args.jump_positive_weight,
        "objective_window_sample_weight": args.objective_window_sample_weight,
        "advance_stalled_jump_targets": bool(args.advance_stalled_jump_targets),
        "advance_stall_threshold": float(args.advance_stall_threshold),
        "advance_max_lookahead": int(args.advance_max_lookahead),
        "advance_requires_grounded": bool(args.advance_requires_grounded),
        "augment_class_profile": list(args.augment_class_profile),
        "window_min_objective_distance": args.window_min_objective_distance,
        "window_max_objective_distance": args.window_max_objective_distance,
        "window_min_objective_relative_x": args.window_min_objective_relative_x,
        "window_max_objective_relative_x": args.window_max_objective_relative_x,
        "window_min_objective_relative_y": args.window_min_objective_relative_y,
        "window_max_objective_relative_y": args.window_max_objective_relative_y,
    }
    return (
        torch.tensor(np.stack(features), dtype=torch.float32),
        torch.tensor(np.stack(move_targets), dtype=torch.long),
        torch.tensor(np.stack(binary_targets), dtype=torch.float32),
        torch.tensor(np.stack(aim_targets), dtype=torch.float32),
        torch.tensor(sample_weights, dtype=torch.float32),
        summary,
    )


def load_backbone_from_checkpoint(model: ReturnChunkModel, checkpoint_path: Path) -> None:
    state = torch.load(checkpoint_path, map_location="cpu")
    if not isinstance(state, dict):
        raise ValueError(f"checkpoint did not contain a state dict: {checkpoint_path}")
    target_state = model.state_dict()
    updates: dict[str, torch.Tensor] = {}
    for key, target_value in target_state.items():
        if not key.startswith("backbone.") or key not in state:
            continue
        source_value = state[key]
        if not isinstance(source_value, torch.Tensor):
            continue
        if tuple(source_value.shape) == tuple(target_value.shape):
            updates[key] = source_value
            continue
        if key == "backbone.0.weight" and source_value.ndim == 2 and target_value.ndim == 2:
            if source_value.shape[0] == target_value.shape[0] and source_value.shape[1] < target_value.shape[1]:
                padded_value = torch.zeros_like(target_value)
                padded_value[:, : source_value.shape[1]] = source_value
                updates[key] = padded_value
                print(f"adapted checkpoint input layer from {source_value.shape[1]} to {target_value.shape[1]} features")
                continue
        raise ValueError(
            f"checkpoint backbone shape mismatch for {key}: "
            f"source={tuple(source_value.shape)} target={tuple(target_value.shape)}"
        )
    target_state.update(updates)
    model.load_state_dict(target_state)
    print(f"loaded_backbone_parameters={len(updates)}")


def sample_batch(
    features: torch.Tensor,
    moves: torch.Tensor,
    binary: torch.Tensor,
    aim: torch.Tensor,
    weights: torch.Tensor,
    batch_size: int,
) -> tuple[torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor, torch.Tensor]:
    indices = torch.randint(0, features.shape[0], (batch_size,))
    return features[indices], moves[indices], binary[indices], aim[indices], weights[indices]


def weighted_mean(values: torch.Tensor, sample_weights: torch.Tensor) -> torch.Tensor:
    while sample_weights.ndim < values.ndim:
        sample_weights = sample_weights.unsqueeze(-1)
    return (values * sample_weights).sum() / sample_weights.sum().clamp_min(1e-6)


def train(args: argparse.Namespace) -> None:
    torch.manual_seed(args.seed)
    np.random.seed(args.seed)
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    device = torch.device("cuda" if torch.cuda.is_available() and not args.cpu else "cpu")

    features, moves, binary, aim, weights, dataset_summary = build_chunk_dataset(args)
    features = features.to(device)
    moves = moves.to(device)
    binary = binary.to(device)
    aim = aim.to(device)
    weights = weights.to(device)

    model = ReturnChunkModel(args.horizon).to(device)
    if args.init_checkpoint:
        load_backbone_from_checkpoint(model, Path(args.init_checkpoint))
        model.to(device)
    optimizer = torch.optim.AdamW(model.parameters(), lr=args.learning_rate, weight_decay=1e-4)
    binary_positive_weights = torch.ones(5, dtype=torch.float32, device=device)
    binary_positive_weights[0] = max(1.0, float(args.jump_positive_weight))
    metrics: list[dict[str, Any]] = []

    for epoch in range(1, args.epochs + 1):
        model.train()
        total_loss = 0.0
        total_move_loss = 0.0
        total_binary_loss = 0.0
        total_aim_loss = 0.0
        for _ in range(max(1, args.batches_per_epoch)):
            batch_features, batch_moves, batch_binary, batch_aim, batch_weights = sample_batch(
                features,
                moves,
                binary,
                aim,
                weights,
                args.batch_size,
            )
            optimizer.zero_grad(set_to_none=True)
            move_logits, binary_logits, aim_pred = model(batch_features)
            move_loss_by_frame = F.cross_entropy(
                move_logits.reshape(-1, 3),
                batch_moves.reshape(-1),
                reduction="none",
            ).reshape(batch_moves.shape)
            move_loss = weighted_mean(move_loss_by_frame.mean(dim=1), batch_weights)
            binary_loss_by_channel = F.binary_cross_entropy_with_logits(
                binary_logits,
                batch_binary,
                reduction="none",
            )
            binary_loss_by_channel = torch.where(
                batch_binary > 0.5,
                binary_loss_by_channel * binary_positive_weights.view(1, 1, -1),
                binary_loss_by_channel,
            )
            binary_loss_by_frame = binary_loss_by_channel.mean(dim=2)
            binary_loss = weighted_mean(binary_loss_by_frame.mean(dim=1), batch_weights)
            aim_loss_by_frame = F.l1_loss(aim_pred, batch_aim, reduction="none").mean(dim=2)
            aim_loss = weighted_mean(aim_loss_by_frame.mean(dim=1), batch_weights)
            loss = move_loss + binary_loss + aim_loss
            loss.backward()
            optimizer.step()

            total_loss += float(loss.detach().cpu())
            total_move_loss += float(move_loss.detach().cpu())
            total_binary_loss += float(binary_loss.detach().cpu())
            total_aim_loss += float(aim_loss.detach().cpu())

        denominator = max(1, args.batches_per_epoch)
        with torch.no_grad():
            move_logits, binary_logits, aim_pred = model(features)
            move_accuracy = float((move_logits.argmax(dim=2) == moves).float().mean().detach().cpu())
            binary_accuracy = float(((binary_logits > 0.0) == (binary > 0.5)).float().mean().detach().cpu())
            aim_mae = float(torch.abs(aim_pred - aim).mean().detach().cpu())
        metric = {
            "epoch": epoch,
            "loss": total_loss / denominator,
            "move_loss": total_move_loss / denominator,
            "binary_loss": total_binary_loss / denominator,
            "aim_loss": total_aim_loss / denominator,
            "move_accuracy": move_accuracy,
            "binary_accuracy": binary_accuracy,
            "aim_mae": aim_mae,
        }
        metrics.append(metric)
        print(
            f"epoch={epoch} loss={metric['loss']:.4f} move_acc={move_accuracy:.3f} "
            f"binary_acc={binary_accuracy:.3f} aim_mae={aim_mae:.4f}"
        )

    checkpoint_path = output_dir / "chunk_model.pt"
    onnx_path = output_dir / "chunk_model.onnx"
    torch.save(model.state_dict(), checkpoint_path)
    export_onnx(model, onnx_path, args.horizon)
    with (output_dir / "metrics.json").open("w", encoding="utf-8") as handle:
        json.dump(metrics, handle, indent=2)
    with (output_dir / "dataset-summary.json").open("w", encoding="utf-8") as handle:
        json.dump(dataset_summary, handle, indent=2)
    print(f"saved checkpoint={checkpoint_path}")
    print(f"saved onnx={onnx_path}")


def export_onnx(model: ReturnChunkModel, onnx_path: Path, horizon: int) -> None:
    model.eval()
    dummy_input = torch.zeros(1, FEATURE_COUNT, dtype=torch.float32)
    torch.onnx.export(
        model,
        dummy_input,
        onnx_path,
        input_names=["obs"],
        output_names=["chunk_move_logits", "chunk_binary_logits", "chunk_aim"],
        dynamic_axes={
            "obs": {0: "batch"},
            "chunk_move_logits": {0: "batch"},
            "chunk_binary_logits": {0: "batch"},
            "chunk_aim": {0: "batch"},
        },
        opset_version=17,
        dynamo=False,
    )


if __name__ == "__main__":
    train(parse_args())
