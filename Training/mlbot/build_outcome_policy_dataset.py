from __future__ import annotations

import argparse
import json
from collections import defaultdict
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Convert mlbot-outcome-v1 simulator probes into direct policy-improvement pseudo demos."
    )
    parser.add_argument("--data-path", action="append", default=[])
    parser.add_argument("--data-dir", action="append", default=[])
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--min-best-margin", type=float, default=0.0)
    parser.add_argument("--min-selected-score", type=float, default=-1.0e9)
    parser.add_argument("--repeat-selected", type=int, default=1)
    parser.add_argument("--max-samples", type=int, default=0)
    parser.add_argument("--seed", type=int, default=1337)
    return parser.parse_args()


def discover_paths(args: argparse.Namespace) -> list[Path]:
    paths = [Path(path) for path in args.data_path]
    for raw_dir in args.data_dir:
        directory = Path(raw_dir)
        if directory.is_dir():
            paths.extend(sorted(directory.rglob("*.json")))

    unique: list[Path] = []
    seen: set[Path] = set()
    for path in paths:
        if not path.is_file():
            continue
        resolved = path.resolve()
        if resolved in seen:
            continue
        seen.add(resolved)
        unique.append(resolved)
    return unique


def action_score(sample: dict[str, Any]) -> float:
    objective_delta = float(sample.get("ObjectiveDistanceDelta", 0.0))
    min_objective_delta = float(sample.get("MinObjectiveDistanceDelta", 0.0))
    max_vertical_gain = float(sample.get("MaxVerticalGain", 0.0))
    upward_landing_progress = float(sample.get("UpwardLandingProgress", 0.0))
    total_reward = float(sample.get("TotalReward", 0.0))
    hit_wall = bool(sample.get("HitWall", False))
    success = bool(sample.get("Success", False))
    ended_near_upward_landing = bool(sample.get("EndedNearUpwardLanding", False))
    moved = abs(float(sample.get("DeltaX", 0.0))) + abs(float(sample.get("DeltaY", 0.0)))
    max_distance_from_start = float(sample.get("MaxDistanceFromStart", moved))
    wall_contact_ticks = int(sample.get("WallContactTicks", 1 if hit_wall else 0))
    no_progress_ticks = int(sample.get("NoProgressTicks", 0))
    objective_regression_ticks = int(sample.get("ObjectiveRegressionTicks", 0))
    useful_progress_ticks = int(sample.get("UsefulProgressTicks", 0))
    jump_ticks = int(sample.get("JumpTicks", 1 if bool(sample.get("Jump", False)) else 0))
    actual_ticks = max(1, int(sample.get("ActualTicks", sample.get("HorizonTicks", 1))))

    if success:
        # Terminal success can change the resolved objective/phase, so the final
        # distance delta may look like a large regression after scoring.
        objective_delta = max(objective_delta, min_objective_delta, 0.0)
        min_objective_delta = max(min_objective_delta, 0.0)

    score = total_reward
    score += objective_delta * 0.55
    score += min_objective_delta * 0.35
    score += max_vertical_gain * 0.08
    score += upward_landing_progress * 0.75
    score += max_distance_from_start * 0.06
    score += useful_progress_ticks * 3.0
    if ended_near_upward_landing:
        score += 225.0
    if success:
        score += 20000.0
    if hit_wall:
        score -= 75.0
    if hit_wall and useful_progress_ticks <= 1:
        score -= 175.0
    score -= wall_contact_ticks * 4.0
    score -= no_progress_ticks * 3.5
    score -= objective_regression_ticks * 8.0
    if moved < 2.0:
        score -= 125.0
    if max_distance_from_start < 16.0:
        score -= 90.0
    if jump_ticks >= max(2, actual_ticks // 3) and max_vertical_gain < 8.0 and upward_landing_progress < 8.0:
        score -= 160.0
    if str(sample.get("ActionName", "")).lower() == "idle":
        score -= 125.0
    return score


def action_payload(sample: dict[str, Any], observation: dict[str, Any]) -> dict[str, Any]:
    objective = observation.get("Objective", {})
    aim_x = float(objective.get("WorldX", observation.get("BotX", 0.0)))
    aim_y = float(objective.get("WorldY", observation.get("BotY", 0.0)))
    return {
        "MoveDirection": int(sample.get("MoveDirection", 0)),
        "Jump": bool(sample.get("Jump", False)),
        "Crouch": bool(sample.get("Crouch", False)),
        "FirePrimary": False,
        "FireSecondary": False,
        "DropIntel": False,
        "AimWorldX": aim_x,
        "AimWorldY": aim_y,
    }


def iter_outcome_samples(document: dict[str, Any], path: Path) -> list[dict[str, Any]]:
    schema = document.get("SchemaVersion")
    if schema == "mlbot-outcome-v1":
        samples = document.get("Samples", [])
        return [sample for sample in samples if isinstance(sample, dict)]
    if schema != "mlbot-outcome-compact-v1":
        return []

    anchors: dict[int, dict[str, Any]] = {}
    for anchor in document.get("Anchors", []):
        if not isinstance(anchor, dict):
            continue
        observation = anchor.get("StartObservation")
        if not isinstance(observation, dict):
            continue
        anchors[int(anchor.get("AnchorId", -1))] = anchor

    expanded: list[dict[str, Any]] = []
    for sample in document.get("Samples", []):
        if not isinstance(sample, dict):
            continue
        anchor = anchors.get(int(sample.get("AnchorId", -1)))
        if anchor is None:
            continue
        expanded_sample = dict(sample)
        expanded_sample["StartObservation"] = anchor["StartObservation"]
        expanded_sample["SourcePath"] = str(anchor.get("SourcePath", path))
        expanded_sample["SourceTick"] = int(anchor.get("SourceTick", 0))
        expanded_sample["HorizonTicks"] = int(sample.get("HorizonTicks", document.get("HorizonTicks", 0)))
        expanded.append(expanded_sample)
    return expanded


def load_best_samples(paths: list[Path], min_best_margin: float, min_selected_score: float) -> tuple[list[dict[str, Any]], dict[str, Any]]:
    groups: dict[tuple[str, int, int], list[dict[str, Any]]] = defaultdict(list)
    source_files: dict[str, int] = {}
    action_counts: dict[str, int] = {}
    rejected_low_score = 0
    rejected_low_margin = 0
    accepted_files = 0
    scanned_samples = 0

    for path in paths:
        with path.open("r", encoding="utf-8-sig") as handle:
            document = json.load(handle)
        if document.get("SchemaVersion") not in {"mlbot-outcome-v1", "mlbot-outcome-compact-v1"}:
            continue

        samples = iter_outcome_samples(document, path)
        if not isinstance(samples, list):
            continue

        accepted = 0
        for sample in samples:
            observation = sample.get("StartObservation")
            if not isinstance(observation, dict):
                continue
            source_path = str(sample.get("SourcePath", path))
            source_tick = int(sample.get("SourceTick", 0))
            horizon = int(sample.get("HorizonTicks", document.get("HorizonTicks", 0)))
            groups[(source_path, source_tick, horizon)].append(sample)
            scanned_samples += 1
            accepted += 1
        if accepted:
            accepted_files += 1
            source_files[str(path)] = accepted

    selected: list[dict[str, Any]] = []
    for candidates in groups.values():
        ranked = sorted(candidates, key=action_score, reverse=True)
        if len(ranked) > 1 and action_score(ranked[0]) - action_score(ranked[1]) < min_best_margin:
            rejected_low_margin += 1
            continue
        if action_score(ranked[0]) < min_selected_score:
            rejected_low_score += 1
            continue
        best = ranked[0]
        observation = best["StartObservation"]
        action_name = str(best.get("ActionName", "unknown"))
        action_counts[action_name] = action_counts.get(action_name, 0) + 1
        selected.append(
            {
                "Observation": observation,
                "Action": action_payload(best, observation),
                "ResolvedPhase": str(observation.get("TaskPhase", "Unknown")),
                "UsedHumanOverride": False,
                "OutcomePolicy": {
                    "ActionName": action_name,
                    "HorizonTicks": int(best.get("HorizonTicks", 0)),
                    "Score": action_score(best),
                    "TotalReward": float(best.get("TotalReward", 0.0)),
                    "ObjectiveDistanceDelta": float(best.get("ObjectiveDistanceDelta", 0.0)),
                    "MinObjectiveDistanceDelta": float(best.get("MinObjectiveDistanceDelta", 0.0)),
                    "MaxVerticalGain": float(best.get("MaxVerticalGain", 0.0)),
                    "UpwardLandingProgress": float(best.get("UpwardLandingProgress", 0.0)),
                    "EndedNearUpwardLanding": bool(best.get("EndedNearUpwardLanding", False)),
                    "HitWall": bool(best.get("HitWall", False)),
                    "Success": bool(best.get("Success", False)),
                },
            }
        )

    summary = {
        "source_file_counts": source_files,
        "accepted_files": accepted_files,
        "scanned_samples": scanned_samples,
        "anchor_horizon_groups": len(groups),
        "selected_samples": len(selected),
        "rejected_low_score": rejected_low_score,
        "rejected_low_margin": rejected_low_margin,
        "min_selected_score": min_selected_score,
        "min_best_margin": min_best_margin,
        "action_counts": action_counts,
    }
    return selected, summary


def group_samples(samples: list[dict[str, Any]]) -> dict[tuple[str, str, str, str], list[dict[str, Any]]]:
    grouped: dict[tuple[str, str, str, str], list[dict[str, Any]]] = defaultdict(list)
    for sample in samples:
        observation = sample["Observation"]
        key = (
            str(observation.get("LevelName", "Unknown")),
            str(observation.get("Team", "Unknown")),
            str(observation.get("ClassId", "Unknown")),
            str(observation.get("TaskPhase", "Unknown")),
        )
        grouped[key].append(sample)
    return grouped


def main() -> None:
    args = parse_args()
    paths = discover_paths(args)
    if not paths:
        raise ValueError("no outcome dataset JSON files found")

    samples, summary = load_best_samples(paths, float(args.min_best_margin), float(args.min_selected_score))
    if args.max_samples > 0:
        import random

        rng = random.Random(int(args.seed))
        rng.shuffle(samples)
        samples = samples[: int(args.max_samples)]
        summary["selected_samples"] = len(samples)
        summary["max_samples"] = int(args.max_samples)

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    repeat_count = max(1, int(args.repeat_selected))
    if repeat_count > 1:
        samples = [dict(sample) for sample in samples for _ in range(repeat_count)]
        summary["repeat_selected"] = repeat_count
        summary["selected_samples_after_repeat"] = len(samples)

    grouped = group_samples(samples)
    for index, ((level, team, class_id, phase), group) in enumerate(sorted(grouped.items()), start=1):
        document = {
            "SchemaVersion": "mlbot-outcome-policy-v1",
            "Metadata": {
                "LevelName": level,
                "Team": team,
                "ClassId": class_id,
                "RequestedPhase": phase,
                "CaptureKind": "OutcomePolicy",
                "Success": True,
            },
            "Samples": group,
        }
        safe_name = f"{index:03d}_{level}_{team}_{class_id}_{phase}.json".replace(" ", "_")
        with (output_dir / safe_name).open("w", encoding="utf-8") as handle:
            json.dump(document, handle, indent=2)

    summary["output_files"] = len(grouped)
    with (output_dir / "summary.json").open("w", encoding="utf-8") as handle:
        json.dump(summary, handle, indent=2)
    print(
        f"saved outcome_policy_dir={output_dir} files={len(grouped)} "
        f"samples={len(samples)} actions={summary['action_counts']}"
    )


if __name__ == "__main__":
    main()
