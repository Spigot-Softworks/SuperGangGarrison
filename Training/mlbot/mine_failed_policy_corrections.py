from __future__ import annotations

import argparse
import json
import subprocess
from pathlib import Path
from typing import Any

import numpy as np

from mlbot_dataset import vectorize_observation


BINARY_ACTIONS = ("Jump", "Crouch", "FirePrimary", "FireSecondary", "DropIntel")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Mine DAgger-style corrective replay samples by aligning failed on-policy "
            "states to successful teacher rollout actions."
        )
    )
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--failed-rollout-path", action="append", default=[])
    parser.add_argument("--teacher-rollout-dir", action="append", default=[])
    parser.add_argument("--teacher-rollout-path", action="append", default=[])
    parser.add_argument("--teacher-glob", default="*.json")
    parser.add_argument("--max-corrections-per-failed-rollout", type=int, default=700)
    parser.add_argument("--max-neighbor-distance", type=float, default=0.0)
    parser.add_argument(
        "--alignment-key",
        choices=("feature", "world-position"),
        default="feature",
        help="Choose how failed states are matched to successful teacher states.",
    )
    parser.add_argument("--vertical-waypoint-min-abs-y", type=float, default=64.0)
    parser.add_argument("--near-navigation-distance", type=float, default=260.0)
    parser.add_argument("--include-matching-actions", action="store_true")

    parser.add_argument("--rollout-project", default="")
    parser.add_argument("--model-onnx", default="")
    parser.add_argument("--map", default="TwodFortTwo")
    parser.add_argument("--team", default="Red")
    parser.add_argument("--class", dest="class_id", default="Scout")
    parser.add_argument("--task", default="ReturnIntel")
    parser.add_argument("--ticks", type=int, default=1800)
    parser.add_argument("--start-node-id", type=int, default=-1)
    parser.add_argument("--start-x", type=float, default=None)
    parser.add_argument("--start-y", type=float, default=None)
    parser.add_argument("--carrying-intel", dest="carrying_intel", action="store_true")
    parser.add_argument("--no-carrying-intel", dest="carrying_intel", action="store_false")
    parser.set_defaults(carrying_intel=None)
    parser.add_argument("--attempts", type=int, default=0)
    parser.add_argument("--seed", type=int, default=4100)
    parser.add_argument("--stochastic", action="store_true")
    parser.add_argument("--temperature", type=float, default=0.9)
    parser.add_argument("--disable-policy-overrides", action="store_true")
    parser.add_argument("--rollout-no-build", action="store_true")
    return parser.parse_args()


def load_json(path: Path) -> dict[str, Any]:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def write_json(path: Path, payload: dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)


def discover_teacher_paths(args: argparse.Namespace) -> list[Path]:
    paths = [Path(raw_path) for raw_path in args.teacher_rollout_path]
    for raw_dir in args.teacher_rollout_dir:
        directory = Path(raw_dir)
        if directory.is_dir():
            paths.extend(sorted(directory.rglob(args.teacher_glob)))

    unique_paths: list[Path] = []
    seen: set[Path] = set()
    for path in paths:
        if not path.is_file():
            continue
        resolved = path.resolve()
        if resolved not in seen:
            seen.add(resolved)
            unique_paths.append(resolved)
    return unique_paths


def context_key(rollout: dict[str, Any]) -> tuple[str, str, str, str]:
    return (
        str(rollout.get("LevelName", "")),
        str(rollout.get("Team", "")),
        str(rollout.get("ClassId", "")),
        str(rollout.get("TaskPhase", "")),
    )


def action_signature(action: dict[str, Any]) -> tuple[Any, ...]:
    return (
        int(action.get("MoveDirection", 0)),
        bool(action.get("Jump", False)),
        bool(action.get("Crouch", False)),
        bool(action.get("FirePrimary", False)),
        bool(action.get("FireSecondary", False)),
        bool(action.get("DropIntel", False)),
    )


def navigation_distance(observation: dict[str, Any]) -> float:
    waypoint = observation.get("Waypoint", {})
    if waypoint.get("HasWaypoint", False) and not waypoint.get("IsFinalWaypoint", False):
        return float(waypoint.get("Distance", observation.get("ObjectiveDistance", float("inf"))))
    return float(observation.get("ObjectiveDistance", float("inf")))


def is_hard_state(step: dict[str, Any], teacher_action: dict[str, Any], args: argparse.Namespace) -> bool:
    observation = step["Observation"]
    waypoint = observation.get("Waypoint", {})
    policy_action = step["Action"]
    actions_differ = action_signature(policy_action) != action_signature(teacher_action)
    vertical_waypoint = abs(float(waypoint.get("RelativeY", 0.0))) >= args.vertical_waypoint_min_abs_y
    near_route_target = navigation_distance(observation) <= args.near_navigation_distance
    if args.include_matching_actions:
        return vertical_waypoint or near_route_target or actions_differ
    return actions_differ and (vertical_waypoint or near_route_target)


def corrected_action(failed_observation: dict[str, Any], teacher_step: dict[str, Any]) -> dict[str, Any]:
    teacher_observation = teacher_step["Observation"]
    teacher_action = teacher_step["Action"]
    aim_relative_x = float(teacher_action.get("AimWorldX", teacher_observation.get("BotX", 0.0))) - float(
        teacher_observation.get("BotX", 0.0)
    )
    aim_relative_y = float(teacher_action.get("AimWorldY", teacher_observation.get("BotY", 0.0))) - float(
        teacher_observation.get("BotY", 0.0)
    )
    return {
        "MoveDirection": int(teacher_action.get("MoveDirection", 0)),
        "Jump": bool(teacher_action.get("Jump", False)),
        "Crouch": bool(teacher_action.get("Crouch", False)),
        "FirePrimary": bool(teacher_action.get("FirePrimary", False)),
        "FireSecondary": bool(teacher_action.get("FireSecondary", False)),
        "DropIntel": bool(teacher_action.get("DropIntel", False)),
        "AimWorldX": float(failed_observation.get("BotX", 0.0)) + aim_relative_x,
        "AimWorldY": float(failed_observation.get("BotY", 0.0)) + aim_relative_y,
    }


def world_position_vector(observation: dict[str, Any]) -> np.ndarray:
    return np.asarray(
        [
            float(observation.get("BotX", 0.0)),
            float(observation.get("BotY", 0.0)),
            0.25 * float(observation.get("ObjectiveDistance", 0.0)),
        ],
        dtype=np.float32,
    )


def observation_alignment_vector(observation: dict[str, Any], args: argparse.Namespace) -> np.ndarray:
    if args.alignment_key == "world-position":
        return world_position_vector(observation)
    return vectorize_observation(observation).astype(np.float32)


def build_teacher_index(
    teacher_rollouts: list[dict[str, Any]],
    args: argparse.Namespace,
) -> dict[tuple[str, str, str, str], dict[str, Any]]:
    by_context: dict[tuple[str, str, str, str], list[dict[str, Any]]] = {}
    for rollout in teacher_rollouts:
        if not rollout.get("Success", False):
            continue
        by_context.setdefault(context_key(rollout), []).extend(rollout.get("Steps", []))

    index: dict[tuple[str, str, str, str], dict[str, Any]] = {}
    for key, steps in by_context.items():
        if not steps:
            continue
        features = np.stack([observation_alignment_vector(step["Observation"], args) for step in steps]).astype(np.float32)
        index[key] = {
            "features": features,
            "steps": steps,
        }
    return index


def nearest_teacher_step(
    failed_observation: dict[str, Any],
    teacher_bucket: dict[str, Any],
    args: argparse.Namespace,
) -> tuple[dict[str, Any], float]:
    query = observation_alignment_vector(failed_observation, args)
    deltas = teacher_bucket["features"] - query
    distances = np.einsum("ij,ij->i", deltas, deltas)
    nearest_index = int(np.argmin(distances))
    return teacher_bucket["steps"][nearest_index], float(np.sqrt(distances[nearest_index]))


def export_failed_rollouts(args: argparse.Namespace, output_dir: Path) -> list[Path]:
    if args.attempts <= 0:
        return []
    if not args.rollout_project or not args.model_onnx:
        raise ValueError("--rollout-project and --model-onnx are required when --attempts is positive")

    rollout_dir = output_dir / "policy-rollouts"
    rollout_dir.mkdir(parents=True, exist_ok=True)
    paths: list[Path] = []
    for attempt in range(args.attempts):
        seed = args.seed + attempt
        output_path = rollout_dir / f"policy-{args.map}-{args.team}-{args.class_id}-{args.task}-seed-{seed}.json"
        command = [
            "dotnet",
            "run",
            "--project",
            args.rollout_project,
        ]
        if args.rollout_no_build:
            command.append("--no-build")
        command.extend(
            [
                "--",
                "export-rollout",
                "--map",
                args.map,
                "--team",
                args.team,
                "--class",
                args.class_id,
                "--task",
                args.task,
                "--ticks",
                str(args.ticks),
                "--model",
                args.model_onnx,
                "--out",
                str(output_path),
                "--seed",
                str(seed),
                "--temperature",
                str(args.temperature),
            ]
        )
        if args.start_node_id >= 0:
            command.extend(["--start-node-id", str(args.start_node_id)])
        if args.start_x is not None and args.start_y is not None:
            command.extend(["--start-x", str(args.start_x), "--start-y", str(args.start_y)])
        elif args.start_x is not None or args.start_y is not None:
            raise ValueError("world-truth start fixtures require both --start-x and --start-y")
        if args.carrying_intel is True:
            command.append("--carrying-intel")
        elif args.carrying_intel is False:
            command.append("--no-carrying-intel")
        if args.stochastic:
            command.append("--stochastic")
        if args.disable_policy_overrides:
            command.append("--disable-policy-overrides")

        subprocess.run(command, check=True)
        paths.append(output_path)
    return paths


def mine_failed_rollout(
    failed_path: Path,
    failed_rollout: dict[str, Any],
    teacher_index: dict[tuple[str, str, str, str], dict[str, Any]],
    args: argparse.Namespace,
) -> tuple[dict[str, Any] | None, dict[str, Any]]:
    key = context_key(failed_rollout)
    teacher_bucket = teacher_index.get(key)
    if teacher_bucket is None:
        return None, {
            "failed_rollout": str(failed_path),
            "skipped": True,
            "reason": f"no successful teacher rollout for context {key}",
        }

    candidates: list[dict[str, Any]] = []
    for step in failed_rollout.get("Steps", []):
        teacher_step, neighbor_distance = nearest_teacher_step(step["Observation"], teacher_bucket, args)
        if args.max_neighbor_distance > 0.0 and neighbor_distance > args.max_neighbor_distance:
            continue
        if not is_hard_state(step, teacher_step["Action"], args):
            continue

        corrected = {
            "Tick": int(step.get("Tick", 0)),
            "Observation": step["Observation"],
            "Action": corrected_action(step["Observation"], teacher_step),
            "NextObservation": step["NextObservation"],
            "Reward": step.get("Reward", {}),
            "IsTerminal": False,
            "IsSuccess": False,
            "TerminalReason": "",
            "CorrectionMetadata": {
                "sourceFailedRollout": str(failed_path),
                "sourceFailedTick": int(step.get("Tick", 0)),
                "teacherTick": int(teacher_step.get("Tick", 0)),
                "neighborDistance": neighbor_distance,
                "policyAction": step["Action"],
                "teacherAction": teacher_step["Action"],
                "navigationDistance": navigation_distance(step["Observation"]),
            },
        }
        candidates.append(corrected)

    candidates.sort(
        key=lambda item: (
            action_signature(item["CorrectionMetadata"]["policyAction"])
            == action_signature(item["CorrectionMetadata"]["teacherAction"]),
            item["CorrectionMetadata"]["navigationDistance"],
            item["CorrectionMetadata"]["neighborDistance"],
        )
    )
    if args.max_corrections_per_failed_rollout > 0:
        candidates = candidates[: args.max_corrections_per_failed_rollout]

    if not candidates:
        return None, {
            "failed_rollout": str(failed_path),
            "skipped": True,
            "reason": "no hard states matched mining filters",
        }

    document = {
        "LevelName": failed_rollout.get("LevelName"),
        "Team": failed_rollout.get("Team"),
        "ClassId": failed_rollout.get("ClassId"),
        "TaskPhase": failed_rollout.get("TaskPhase"),
        "ModelPath": failed_rollout.get("ModelPath"),
        "TicksElapsed": len(candidates),
        "Success": True,
        "TerminalReason": "corrective_replay",
        "TotalReward": 0.0,
        "CorrectionSource": "failed_policy_nearest_success_teacher",
        "SourceFailedRollout": str(failed_path),
        "Steps": candidates,
    }
    summary = {
        "failed_rollout": str(failed_path),
        "failed_success": bool(failed_rollout.get("Success", False)),
        "failed_terminal_reason": failed_rollout.get("TerminalReason", ""),
        "failed_ticks": int(failed_rollout.get("TicksElapsed", 0)),
        "correction_count": len(candidates),
        "mean_neighbor_distance": float(
            np.mean([item["CorrectionMetadata"]["neighborDistance"] for item in candidates])
        ),
        "mean_navigation_distance": float(
            np.mean([item["CorrectionMetadata"]["navigationDistance"] for item in candidates])
        ),
    }
    return document, summary


def main() -> None:
    args = parse_args()
    output_dir = Path(args.output_dir)
    corrections_dir = output_dir / "corrections"
    output_dir.mkdir(parents=True, exist_ok=True)

    exported_failed_paths = export_failed_rollouts(args, output_dir)
    failed_paths = [Path(raw_path) for raw_path in args.failed_rollout_path] + exported_failed_paths
    failed_paths = [path.resolve() for path in failed_paths if path.is_file()]
    if not failed_paths:
        raise ValueError("no failed rollout paths were provided or exported")

    teacher_paths = discover_teacher_paths(args)
    if not teacher_paths:
        raise ValueError("no teacher rollout paths were found")

    teacher_rollouts = [load_json(path) for path in teacher_paths]
    teacher_index = build_teacher_index(teacher_rollouts, args)
    if not teacher_index:
        raise ValueError("no successful teacher steps were available")

    summaries: list[dict[str, Any]] = []
    correction_paths: list[str] = []
    total_corrections = 0
    for failed_path in failed_paths:
        failed_rollout = load_json(failed_path)
        document, summary = mine_failed_rollout(failed_path, failed_rollout, teacher_index, args)
        summaries.append(summary)
        if document is None:
            continue
        output_path = corrections_dir / f"{failed_path.stem}-corrections.json"
        write_json(output_path, document)
        correction_paths.append(str(output_path))
        total_corrections += len(document["Steps"])

    summary_payload = {
        "failed_rollout_count": len(failed_paths),
        "teacher_rollout_count": len(teacher_paths),
        "teacher_contexts": ["|".join(key) for key in sorted(teacher_index)],
        "correction_document_count": len(correction_paths),
        "total_corrections": total_corrections,
        "correction_paths": correction_paths,
        "summaries": summaries,
    }
    write_json(output_dir / "correction-summary.json", summary_payload)
    print(
        f"failed_rollouts={len(failed_paths)} correction_docs={len(correction_paths)} "
        f"total_corrections={total_corrections} output={output_dir}"
    )


if __name__ == "__main__":
    main()
