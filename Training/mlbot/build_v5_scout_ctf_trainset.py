from __future__ import annotations

import argparse
import json
import shutil
from collections import Counter
from pathlib import Path
from typing import Any


DEFAULT_MAPS = {"TwodFortTwo", "Waterway"}
SUPPORTED_SCHEMA_VERSIONS = {"mlbot-demo-v5", "mlbot-demo-v6"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Build a narrow V5 Scout CTF trainset from seed demos and teacher rollouts.")
    parser.add_argument("--seed-root", default=".mlbot-data/v5-seed")
    parser.add_argument("--teacher-root", default=".mlbot-data/v5-teacher-scout-ctf-20260425/teacher-demos")
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--maps", default="TwodFortTwo,Waterway")
    parser.add_argument("--seed-repeat", type=int, default=1)
    parser.add_argument("--teacher-repeat", type=int, default=1)
    parser.add_argument("--clean", action="store_true")
    return parser.parse_args()


def load_document(path: Path) -> dict[str, Any] | None:
    try:
        with path.open("r", encoding="utf-8-sig") as handle:
            document = json.load(handle)
    except (OSError, json.JSONDecodeError):
        return None
    return document if isinstance(document, dict) else None


def is_scout_ctf_document(document: dict[str, Any], maps: set[str]) -> bool:
    metadata = document.get("Metadata", {})
    if not isinstance(metadata, dict):
        return False
    if metadata.get("SchemaVersion") not in SUPPORTED_SCHEMA_VERSIONS:
        return False
    if not bool(metadata.get("Success")):
        return False
    if metadata.get("ClassId") != "Scout":
        return False
    if metadata.get("Mode") != "CaptureTheFlag":
        return False
    if metadata.get("LevelName") not in maps:
        return False
    return metadata.get("RequestedPhase") in {"AttackIntel", "ReturnIntel", "None"}


def copy_included(path: Path, source_root: Path, output_root: Path, prefix: str, repeat_index: int) -> Path:
    relative_path = path.relative_to(source_root)
    if repeat_index > 0:
        relative_path = relative_path.with_name(f"{relative_path.stem}__repeat_{repeat_index:03d}{relative_path.suffix}")
    target_path = output_root / prefix / relative_path
    target_path.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, target_path)
    return target_path


def add_documents(
    source_root: Path,
    output_root: Path,
    prefix: str,
    maps: set[str],
    repeat: int,
    included: list[dict[str, Any]],
    reason_counts: Counter[str],
) -> None:
    if not source_root.exists():
        reason_counts[f"{prefix}_missing_root"] += 1
        return
    if repeat < 1:
        raise ValueError(f"{prefix} repeat must be at least 1")

    for path in sorted(source_root.rglob("*.json")):
        document = load_document(path)
        if document is None:
            reason_counts[f"{prefix}_unreadable"] += 1
            continue
        if not is_scout_ctf_document(document, maps):
            reason_counts[f"{prefix}_filtered"] += 1
            continue
        metadata = document["Metadata"]
        for repeat_index in range(repeat):
            target = copy_included(path, source_root, output_root, prefix, repeat_index)
            reason_counts[f"{prefix}_included"] += 1
            included.append(
                {
                    "source": str(path),
                    "target": str(target),
                    "map": metadata.get("LevelName"),
                    "team": metadata.get("Team"),
                    "phase": metadata.get("RequestedPhase"),
                    "ticks": metadata.get("TickCount"),
                    "repeat_index": repeat_index,
                }
            )


def main() -> None:
    args = parse_args()
    seed_root = Path(args.seed_root)
    teacher_root = Path(args.teacher_root)
    output_root = Path(args.output_root)
    maps = {item.strip() for item in args.maps.split(",") if item.strip()} or DEFAULT_MAPS

    if args.clean and output_root.exists():
        shutil.rmtree(output_root)
    output_root.mkdir(parents=True, exist_ok=True)

    included: list[dict[str, Any]] = []
    reason_counts: Counter[str] = Counter()
    add_documents(seed_root, output_root, "seed", maps, args.seed_repeat, included, reason_counts)
    add_documents(teacher_root, output_root, "teacher", maps, args.teacher_repeat, included, reason_counts)

    manifest = {
        "seed_root": str(seed_root),
        "teacher_root": str(teacher_root),
        "output_root": str(output_root),
        "maps": sorted(maps),
        "seed_repeat": args.seed_repeat,
        "teacher_repeat": args.teacher_repeat,
        "included_file_count": len(included),
        "reason_counts": dict(sorted(reason_counts.items())),
        "included": included,
    }
    manifest_path = output_root / "trainset-manifest.json"
    with manifest_path.open("w", encoding="utf-8") as handle:
        json.dump(manifest, handle, indent=2)
    print(f"scout_ctf_trainset included={len(included)} output={output_root}")
    print(f"manifest={manifest_path}")
    print(f"reason_counts={manifest['reason_counts']}")


if __name__ == "__main__":
    main()
