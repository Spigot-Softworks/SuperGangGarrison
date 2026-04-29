from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Train one unified V5 policy across available MLBot data.")
    parser.add_argument("--data-root", required=True)
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--epochs", type=int, default=30)
    parser.add_argument("--batch-size", type=int, default=512)
    parser.add_argument("--learning-rate", type=float, default=3e-4)
    parser.add_argument("--hidden-size", type=int, default=256)
    parser.add_argument("--max-samples-per-group", type=int, default=0)
    parser.add_argument("--binary-positive-weight-max", type=float, default=4.0)
    parser.add_argument("--binary-positive-weights", default="12,1,1,1,1")
    parser.add_argument("--fine-tune-from", default="")
    parser.add_argument("--rollout-project", default="")
    parser.add_argument("--rollout-map", default="")
    parser.add_argument("--rollout-team", default="")
    parser.add_argument("--rollout-class", default="")
    parser.add_argument("--rollout-task", default="")
    parser.add_argument("--rollout-ticks", type=int, default=1800)
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--include-failures", action="store_true")
    parser.add_argument("--cpu", action="store_true")
    return parser.parse_args()


def main() -> None:
    args = parse_args()
    command = [
        sys.executable,
        str(Path(__file__).with_name("train_behavior_cloning.py")),
        "--data-root",
        args.data_root,
        "--output-dir",
        args.output_dir,
        "--epochs",
        str(args.epochs),
        "--batch-size",
        str(args.batch_size),
        "--learning-rate",
        str(args.learning_rate),
        "--hidden-size",
        str(args.hidden_size),
        "--balance-groups",
        "team_phase_capture_override",
        "--split-groups",
        "team_phase_capture_override",
        "--task-conditioned-heads",
        "--disable-policy-overrides",
        "--weighted-action-loss",
        "--binary-positive-weight-max",
        str(args.binary_positive_weight_max),
    ]
    if args.binary_positive_weights:
        command.extend(["--binary-positive-weights", args.binary_positive_weights])
    if not args.include_failures:
        command.append("--success-only")
    if args.max_samples_per_group > 0:
        command.extend(["--max-samples-per-group", str(args.max_samples_per_group)])
    if args.fine_tune_from:
        command.extend(["--fine-tune-from", args.fine_tune_from])
    if args.rollout_project:
        command.extend(
            [
                "--rollout-project",
                args.rollout_project,
                "--rollout-map",
                args.rollout_map,
                "--rollout-team",
                args.rollout_team,
                "--rollout-class",
                args.rollout_class,
                "--rollout-task",
                args.rollout_task,
                "--rollout-ticks",
                str(args.rollout_ticks),
            ]
        )
        if args.rollout_no_build:
            command.append("--rollout-no-build")
    if args.cpu:
        command.append("--cpu")

    completed = subprocess.run(command, check=False, text=True, encoding="utf-8")
    raise SystemExit(completed.returncode)


if __name__ == "__main__":
    main()
