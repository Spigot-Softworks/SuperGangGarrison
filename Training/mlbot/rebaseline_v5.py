from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run the V5 schema check and fixed regression baseline.")
    parser.add_argument("--rollout-project", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument(
        "--scenario-file",
        default=str(Path(__file__).with_name("config") / "v5_regression_matrix.json"),
    )
    parser.add_argument(
        "--model",
        action="append",
        default=[],
        help="name=path. Use name=direct-objective for the built-in direct objective policy.",
    )
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--allow-policy-overrides", action="store_true")
    return parser.parse_args()


def run(command: list[str]) -> None:
    completed = subprocess.run(command, check=False, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise SystemExit(completed.returncode)


def main() -> None:
    args = parse_args()
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    verify_command = [
        sys.executable,
        str(Path(__file__).with_name("verify_schema.py")),
        "--rollout-project",
        args.rollout_project,
    ]
    if args.rollout_no_build:
        verify_command.append("--rollout-no-build")
    run(verify_command)

    models = args.model or ["direct=direct-objective"]
    matrix_command = [
        sys.executable,
        str(Path(__file__).with_name("evaluate_policy_matrix.py")),
        "--rollout-project",
        args.rollout_project,
        "--output-dir",
        str(output_dir / "matrix"),
        "--scenario-file",
        args.scenario_file,
    ]
    for model in models:
        matrix_command.extend(["--model", model])
    if args.rollout_no_build:
        matrix_command.append("--rollout-no-build")
    if not args.allow_policy_overrides:
        matrix_command.append("--disable-policy-overrides")
    run(matrix_command)


if __name__ == "__main__":
    main()
