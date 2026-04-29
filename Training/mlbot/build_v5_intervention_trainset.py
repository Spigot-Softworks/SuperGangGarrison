from __future__ import annotations

import argparse
import json
import shutil
from collections import Counter
from pathlib import Path
from typing import Any


SUPPORTED_SCHEMA_VERSIONS = {"mlbot-demo-v5", "mlbot-demo-v6"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Build a curated V5 trainset from clean seed demos plus useful DAgger interventions."
    )
    parser.add_argument("--seed-root", default=".mlbot-data/v5-seed")
    parser.add_argument("--intervention-root", default=".mlbot-data/v5-interventions")
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--minimum-manual-override-frames", type=int, default=1)
    parser.add_argument("--clean", action="store_true")
    return parser.parse_args()


def load_document(path: Path) -> dict[str, Any] | None:
    try:
        with path.open("r", encoding="utf-8-sig") as handle:
            document = json.load(handle)
    except (OSError, json.JSONDecodeError):
        return None
    return document if isinstance(document, dict) else None


def count_override_frames(document: dict[str, Any]) -> int:
    samples = document.get("Samples", [])
    if not isinstance(samples, list):
        return 0
    return sum(1 for sample in samples if isinstance(sample, dict) and bool(sample.get("UsedHumanOverride")))


def copy_included(source_path: Path, source_root: Path, output_root: Path, prefix: str) -> Path:
    relative_path = source_path.relative_to(source_root)
    target_path = output_root / prefix / relative_path
    target_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source_path, target_path)
    return target_path


def classify_seed_document(document: dict[str, Any]) -> tuple[bool, str]:
    metadata = document.get("Metadata", {})
    if not isinstance(metadata, dict):
        return False, "missing_metadata"
    if metadata.get("SchemaVersion") not in SUPPORTED_SCHEMA_VERSIONS:
        return False, "non_v5_schema"
    if not bool(metadata.get("Success")):
        return False, "seed_not_successful"
    return True, "seed_success"


def classify_intervention_document(
    document: dict[str, Any],
    minimum_manual_override_frames: int,
) -> tuple[bool, str, int]:
    metadata = document.get("Metadata", {})
    if not isinstance(metadata, dict):
        return False, "missing_metadata", 0
    if metadata.get("SchemaVersion") not in SUPPORTED_SCHEMA_VERSIONS:
        return False, "non_v5_schema", 0
    if metadata.get("CaptureKind") != "DaggerAssist":
        return False, "not_dagger_assist", 0

    override_frames = count_override_frames(document)
    if bool(metadata.get("Success")):
        return True, "intervention_success", override_frames
    if override_frames >= minimum_manual_override_frames:
        return True, "manual_with_overrides", override_frames
    return False, "manual_without_overrides", override_frames


def iter_json_files(root: Path) -> list[Path]:
    return sorted(path for path in root.rglob("*.json") if path.is_file())


def main() -> None:
    args = parse_args()
    seed_root = Path(args.seed_root)
    intervention_root = Path(args.intervention_root)
    output_root = Path(args.output_root)

    if args.clean and output_root.exists():
        shutil.rmtree(output_root)
    output_root.mkdir(parents=True, exist_ok=True)

    included: list[dict[str, Any]] = []
    excluded: list[dict[str, Any]] = []
    reason_counts: Counter[str] = Counter()
    map_phase_counts: Counter[str] = Counter()
    total_override_frames = 0

    for path in iter_json_files(seed_root):
        document = load_document(path)
        if document is None:
            reason_counts["unreadable_seed"] += 1
            excluded.append({"source": str(path), "reason": "unreadable_seed"})
            continue

        include, reason = classify_seed_document(document)
        reason_counts[reason] += 1
        if not include:
            excluded.append({"source": str(path), "reason": reason})
            continue

        target = copy_included(path, seed_root, output_root, "seed")
        metadata = document.get("Metadata", {})
        key = f"{metadata.get('LevelName')}|{metadata.get('Team')}|{metadata.get('ClassId')}|{metadata.get('RequestedPhase')}"
        map_phase_counts[key] += 1
        included.append({"source": str(path), "target": str(target), "reason": reason, "override_frames": 0})

    for path in iter_json_files(intervention_root):
        document = load_document(path)
        if document is None:
            reason_counts["unreadable_intervention"] += 1
            excluded.append({"source": str(path), "reason": "unreadable_intervention"})
            continue

        include, reason, override_frames = classify_intervention_document(
            document,
            args.minimum_manual_override_frames,
        )
        reason_counts[reason] += 1
        if not include:
            excluded.append({"source": str(path), "reason": reason, "override_frames": override_frames})
            continue

        target = copy_included(path, intervention_root, output_root, "interventions")
        metadata = document.get("Metadata", {})
        key = f"{metadata.get('LevelName')}|{metadata.get('Team')}|{metadata.get('ClassId')}|{metadata.get('RequestedPhase')}"
        map_phase_counts[key] += 1
        total_override_frames += override_frames
        included.append(
            {
                "source": str(path),
                "target": str(target),
                "reason": reason,
                "override_frames": override_frames,
            }
        )

    manifest = {
        "seed_root": str(seed_root),
        "intervention_root": str(intervention_root),
        "output_root": str(output_root),
        "minimum_manual_override_frames": args.minimum_manual_override_frames,
        "included_file_count": len(included),
        "excluded_file_count": len(excluded),
        "total_intervention_override_frames": total_override_frames,
        "reason_counts": dict(sorted(reason_counts.items())),
        "map_team_class_phase_counts": dict(sorted(map_phase_counts.items())),
        "included": included,
        "excluded": excluded,
    }
    manifest_path = output_root / "trainset-manifest.json"
    with manifest_path.open("w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)

    print(
        "trainset "
        f"included={manifest['included_file_count']} "
        f"excluded={manifest['excluded_file_count']} "
        f"override_frames={total_override_frames} "
        f"output={output_root}"
    )
    print(f"manifest={manifest_path}")
    print(f"reason_counts={manifest['reason_counts']}")


if __name__ == "__main__":
    main()
