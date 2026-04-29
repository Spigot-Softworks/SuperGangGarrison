from __future__ import annotations

import argparse
import json
import subprocess
import tempfile
from pathlib import Path
from typing import Any

from mlbot_dataset import FEATURE_COUNT, FEATURE_COUNT_V7, vectorize_observation


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Verify C# and Python MLBot feature schema agreement.")
    parser.add_argument("--manifest", default="", help="Existing schema manifest JSON from MLBot.Tools schema-manifest.")
    parser.add_argument("--rollout-project", default="", help="MLBot.Tools csproj used to generate a manifest when --manifest is omitted.")
    parser.add_argument("--rollout-no-build", action="store_true")
    return parser.parse_args()


def load_manifest(args: argparse.Namespace) -> dict[str, Any]:
    if args.manifest:
        with Path(args.manifest).open("r", encoding="utf-8") as handle:
            return json.load(handle)
    if not args.rollout_project:
        raise ValueError("provide --manifest or --rollout-project")

    with tempfile.TemporaryDirectory() as raw_tmp:
        manifest_path = Path(raw_tmp) / "mlbot-schema.json"
        command = ["dotnet", "run", "--project", args.rollout_project]
        if args.rollout_no_build:
            command.append("--no-build")
        command.extend(["--", "schema-manifest", "--out", str(manifest_path)])
        completed = subprocess.run(command, check=False, capture_output=True, text=True, encoding="utf-8")
        if completed.returncode != 0:
            raise RuntimeError(
                "schema manifest generation failed\n"
                f"command={' '.join(command)}\nstdout={completed.stdout}\nstderr={completed.stderr}"
            )
        with manifest_path.open("r", encoding="utf-8") as handle:
            return json.load(handle)


def main() -> None:
    args = parse_args()
    manifest = load_manifest(args)
    feature_counts = manifest.get("FeatureCounts", {})
    current_schema = str(manifest.get("CurrentSchema", ""))
    current_feature_count = int(manifest.get("CurrentFeatureCount", -1))
    python_vector = vectorize_observation({})

    failures: list[str] = []
    if current_schema != "V7":
        failures.append(f"CurrentSchema expected V7, got {current_schema}")
    if current_feature_count != FEATURE_COUNT:
        failures.append(f"CurrentFeatureCount expected {FEATURE_COUNT}, got {current_feature_count}")
    if int(feature_counts.get("V7", -1)) != FEATURE_COUNT_V7:
        failures.append(f"FeatureCounts.V7 expected {FEATURE_COUNT_V7}, got {feature_counts.get('V7')}")
    if python_vector.shape[0] != FEATURE_COUNT:
        failures.append(f"Python vector length expected {FEATURE_COUNT}, got {python_vector.shape[0]}")

    if failures:
        for failure in failures:
            print(f"schema_error={failure}")
        raise SystemExit(1)

    print(f"schema_ok current={current_schema} feature_count={FEATURE_COUNT} demo_schema={manifest.get('DemoSchemaVersion')}")


if __name__ == "__main__":
    main()
