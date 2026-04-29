from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build a filtered ReturnIntel replay bank from successful rollout/demo documents."
    )
    parser.add_argument("--source", action="append", required=True, help="Source file or directory.")
    parser.add_argument("--output-dir", required=True)
    parser.add_argument(
        "--map-distance-limit",
        action="append",
        default=[],
        help="Per-map maximum ObjectiveDistance to keep, formatted MapName=distance.",
    )
    parser.add_argument("--clean", action="store_true")
    return parser.parse_args()


def parse_map_limits(raw_values: list[str]) -> dict[str, float]:
    limits: dict[str, float] = {}
    for raw_value in raw_values:
        name, separator, value = raw_value.partition("=")
        if not separator or not name.strip():
            raise ValueError("--map-distance-limit must be formatted MapName=distance")
        limits[name.strip()] = float(value)
    if not limits:
        raise ValueError("at least one --map-distance-limit is required")
    return limits


def iter_paths(raw_sources: list[str]) -> list[Path]:
    paths: list[Path] = []
    for raw_source in raw_sources:
        source = Path(raw_source)
        if source.is_file():
            paths.append(source)
        elif source.is_dir():
            paths.extend(sorted(source.rglob("*.json")))
    return paths


def load_document(path: Path) -> dict[str, Any] | None:
    try:
        with path.open("r", encoding="utf-8-sig") as handle:
            document = json.load(handle)
    except (OSError, json.JSONDecodeError):
        return None
    return document if isinstance(document, dict) else None


def frame_observation(frame: dict[str, Any]) -> dict[str, Any]:
    observation = frame.get("Observation", {})
    return observation if isinstance(observation, dict) else {}


def keep_frame(frame: dict[str, Any], map_limits: dict[str, float]) -> bool:
    observation = frame_observation(frame)
    level_name = str(observation.get("LevelName", ""))
    limit = map_limits.get(level_name)
    if limit is None:
        return False
    return (
        str(observation.get("TaskPhase", "")) == "ReturnIntel"
        and bool(observation.get("IsCarryingIntel", False))
        and float(observation.get("ObjectiveDistance", 1_000_000.0)) <= limit
    )


def filter_document(document: dict[str, Any], map_limits: dict[str, float]) -> dict[str, Any] | None:
    if bool(document.get("Success")) and isinstance(document.get("Steps"), list):
        steps = [step for step in document["Steps"] if isinstance(step, dict) and keep_frame(step, map_limits)]
        if steps:
            filtered = dict(document)
            filtered["Steps"] = steps
            return filtered

    metadata = document.get("Metadata", {})
    if isinstance(metadata, dict) and bool(metadata.get("Success")) and isinstance(document.get("Samples"), list):
        samples = [sample for sample in document["Samples"] if isinstance(sample, dict) and keep_frame(sample, map_limits)]
        if samples:
            filtered = dict(document)
            filtered["Samples"] = samples
            return filtered

    return None


def main() -> None:
    args = parse_args()
    map_limits = parse_map_limits(args.map_distance_limit)
    output_dir = Path(args.output_dir)
    if args.clean and output_dir.exists():
        shutil.rmtree(output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    summary: list[dict[str, Any]] = []
    for path in iter_paths(args.source):
        document = load_document(path)
        if document is None:
            continue
        filtered = filter_document(document, map_limits)
        if filtered is None:
            continue

        output_path = output_dir / path.name
        with output_path.open("w", encoding="utf-8") as handle:
            json.dump(filtered, handle, indent=2)
        frame_count = len(filtered.get("Steps") or filtered.get("Samples") or [])
        summary.append({"source": str(path), "output": str(output_path), "frames": frame_count})
        print(f"included source={path} frames={frame_count} output={output_path}")

    with (output_dir / "return-replay-bank-summary.json").open("w", encoding="utf-8") as handle:
        json.dump({"map_distance_limits": map_limits, "items": summary}, handle, indent=2)
    print(f"saved summary={output_dir / 'return-replay-bank-summary.json'} items={len(summary)}")


if __name__ == "__main__":
    main()
