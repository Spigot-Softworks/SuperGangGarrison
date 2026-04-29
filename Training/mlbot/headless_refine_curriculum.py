from __future__ import annotations

import argparse
import json
import subprocess
import sys
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any


@dataclass
class CurriculumNodeResult:
    start_node_id: int
    round_index: int
    baseline_success: bool
    trained_success: bool
    distilled_success: bool
    retained_success_rollouts: int
    selected_checkpoint: str
    selected_onnx: str
    train_output_dir: str
    distill_output_dir: str
    baseline_trace: dict[str, Any] | None
    trained_trace: dict[str, Any] | None
    distilled_trace: dict[str, Any] | None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Iteratively refine a headless MLBot policy over route start-node curriculum points."
    )
    parser.add_argument("--data-root", required=True)
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--init-checkpoint", required=True)
    parser.add_argument("--rollout-project", required=True)
    parser.add_argument("--map", required=True)
    parser.add_argument("--team", required=True)
    parser.add_argument("--class", dest="class_id", required=True)
    parser.add_argument("--task", required=True)
    parser.add_argument("--start-node-ids", required=True, help="Comma-separated start nodes, in curriculum order.")
    parser.add_argument("--ticks", type=int, default=1200)
    parser.add_argument("--final-eval-ticks", type=int, default=1800)
    parser.add_argument("--max-rounds-per-node", type=int, default=1)
    parser.add_argument("--stop-on-failure", action="store_true")
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--disable-policy-overrides", action="store_true")
    parser.add_argument("--cpu", action="store_true")
    parser.add_argument("--dry-run", action="store_true")

    parser.add_argument("--train-iterations", type=int, default=4)
    parser.add_argument("--train-episodes", type=int, default=24)
    parser.add_argument("--selection-episodes", type=int, default=8)
    parser.add_argument("--train-learning-rate", type=float, default=0.00007)
    parser.add_argument("--train-temperature", type=float, default=2.0)

    parser.add_argument("--distill-epochs", type=int, default=32)
    parser.add_argument("--distill-learning-rate", type=float, default=0.00008)
    parser.add_argument("--distill-batch-size", type=int, default=256)
    parser.add_argument("--distill-early-max-tick", type=int, default=160)
    parser.add_argument("--distill-top-k", type=int, default=1)
    return parser.parse_args()


def parse_start_node_ids(raw_value: str) -> list[int]:
    values: list[int] = []
    for raw_part in raw_value.split(","):
        part = raw_part.strip()
        if not part:
            continue
        values.append(int(part))
    if not values:
        raise ValueError("--start-node-ids did not contain any node ids")
    return values


def run_command(command: list[str], *, dry_run: bool) -> None:
    print(" ".join(command))
    if dry_run:
        return

    completed = subprocess.run(command, check=False, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(f"command failed with exit code {completed.returncode}: {' '.join(command)}")


def run_capture(command: list[str], *, dry_run: bool) -> str:
    print(" ".join(command))
    if dry_run:
        return ""

    completed = subprocess.run(command, check=False, capture_output=True, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(
            "command failed\n"
            f"exit_code={completed.returncode}\n"
            f"command={' '.join(command)}\n"
            f"stdout={completed.stdout}\n"
            f"stderr={completed.stderr}"
        )
    if completed.stdout:
        print(completed.stdout.strip())
    if completed.stderr:
        print(completed.stderr.strip())
    return completed.stdout


def evaluate_policy(
    args: argparse.Namespace,
    onnx_path: Path,
    output_dir: Path,
    start_node_id: int,
    ticks: int,
    label: str,
) -> dict[str, Any] | None:
    trace_path = output_dir / f"{label}-node-{start_node_id}.json"
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
            "--map",
            args.map,
            "--team",
            args.team,
            "--class",
            args.class_id,
            "--task",
            args.task,
            "--ticks",
            str(ticks),
            "--model",
            str(onnx_path),
            "--json-out",
            str(trace_path),
        ]
    )
    if start_node_id >= 0:
        command.extend(["--start-node-id", str(start_node_id)])
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")

    run_capture(command, dry_run=args.dry_run)
    if args.dry_run:
        return None

    with trace_path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def primary_event_succeeded(args: argparse.Namespace, rollout: dict[str, Any]) -> bool:
    if bool(rollout.get("Success", False)):
        return True
    task = args.task.casefold()
    if task == "returnintel":
        return any(step.get("TerminalReason") == "scored" for step in rollout.get("Steps", []))
    if task == "attackintel":
        return any(
            step.get("TerminalReason") == "picked_up_intel"
            or step.get("NextObservation", {}).get("IsCarryingIntel", False)
            for step in rollout.get("Steps", [])
        )
    return False


def count_success_rollouts(args: argparse.Namespace, rollout_root: Path) -> int:
    if not rollout_root.is_dir():
        return 0

    count = 0
    for path in rollout_root.rglob("episode-*.json"):
        with path.open("r", encoding="utf-8") as handle:
            rollout = json.load(handle)
        if primary_event_succeeded(args, rollout):
            count += 1
    return count


def build_train_command(
    args: argparse.Namespace,
    train_output_dir: Path,
    checkpoint_path: Path,
    start_node_id: int,
) -> list[str]:
    command = [
        sys.executable,
        "-X",
        "utf8",
        str(Path(__file__).with_name("train_headless_policy_gradient.py")),
        "--data-root",
        args.data_root,
        "--output-dir",
        str(train_output_dir),
        "--init-checkpoint",
        str(checkpoint_path),
        "--rollout-project",
        args.rollout_project,
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
        "--start-node-id",
        str(start_node_id),
        "--iterations",
        str(args.train_iterations),
        "--episodes-per-iter",
        str(args.train_episodes),
        "--selection-episodes-per-iter",
        str(args.selection_episodes),
        "--learning-rate",
        str(args.train_learning_rate),
        "--temperature",
        str(args.train_temperature),
        "--entropy-coef",
        "0.003",
        "--bc-coef",
        "0",
        "--self-imitation-coef",
        "5",
        "--action-margin-coef",
        "2",
        "--reference-kl-coef",
        "0.005",
        "--capture-kinds",
        "NoSuchKind",
        "--allow-empty-bc-anchor",
        "--self-imitation-top-k",
        "10",
        "--self-imitation-segment-mode",
        "return-breakthrough",
        "--self-imitation-segment-pre-ticks",
        "90",
        "--self-imitation-segment-post-ticks",
        "420",
        "--self-imitation-segment-corridor-slack",
        "96",
        "--self-imitation-segment-min-improvement",
        "12",
        "--discard-rollouts",
        "--keep-successful-rollouts",
    ]
    if args.rollout_no_build:
        command.append("--rollout-no-build")
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")
    if args.cpu:
        command.append("--cpu")
    return command


def build_distill_command(
    args: argparse.Namespace,
    distill_output_dir: Path,
    train_output_dir: Path,
    checkpoint_path: Path,
    start_node_id: int,
) -> list[str]:
    command = [
        sys.executable,
        "-X",
        "utf8",
        str(Path(__file__).with_name("distill_rollout_policy.py")),
        "--data-root",
        args.data_root,
        "--output-dir",
        str(distill_output_dir),
        "--init-checkpoint",
        str(checkpoint_path),
        "--rollout-dir",
        str(train_output_dir / "headless-rollouts"),
        "--top-k",
        str(args.distill_top_k),
        "--teacher-selection-key",
        "fast-success",
        "--segment-mode",
        "full",
        "--epochs",
        str(args.distill_epochs),
        "--learning-rate",
        str(args.distill_learning_rate),
        "--batch-size",
        str(args.distill_batch_size),
        "--bc-coef",
        "0",
        "--reference-kl-coef",
        "0.005",
        "--action-margin-coef",
        "4",
        "--move-margin",
        "2.0",
        "--binary-margin",
        "1.5",
        "--early-max-tick",
        str(args.distill_early_max_tick),
        "--early-sample-weight",
        "3",
        "--jump-sample-weight",
        "2",
        "--counter-waypoint-sample-weight",
        "3",
        "--neutral-move-sample-weight",
        "2",
        "--capture-kinds",
        "NoSuchKind",
        "--allow-empty-bc-anchor",
        "--rollout-project",
        args.rollout_project,
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
        "--start-node-id",
        str(start_node_id),
    ]
    if args.rollout_no_build:
        command.append("--rollout-no-build")
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")
    if args.cpu:
        command.append("--cpu")
    return command


def main() -> int:
    args = parse_args()
    start_node_ids = parse_start_node_ids(args.start_node_ids)
    output_root = Path(args.output_root)
    output_root.mkdir(parents=True, exist_ok=True)
    checkpoint_path = Path(args.init_checkpoint)
    onnx_path = checkpoint_path.with_suffix(".onnx")
    if not args.dry_run and not checkpoint_path.is_file():
        raise FileNotFoundError(checkpoint_path)

    results: list[CurriculumNodeResult] = []
    for start_node_id in start_node_ids:
        node_solved = False
        for round_index in range(1, args.max_rounds_per_node + 1):
            node_output_dir = output_root / f"node-{start_node_id}-round-{round_index:02d}"
            node_output_dir.mkdir(parents=True, exist_ok=True)
            baseline_trace = evaluate_policy(
                args,
                onnx_path,
                node_output_dir,
                start_node_id,
                args.ticks,
                "baseline",
            )
            baseline_success = bool(baseline_trace and baseline_trace.get("Success", False))
            if baseline_success:
                results.append(
                    CurriculumNodeResult(
                        start_node_id=start_node_id,
                        round_index=round_index,
                        baseline_success=True,
                        trained_success=True,
                        distilled_success=True,
                        retained_success_rollouts=0,
                        selected_checkpoint=str(checkpoint_path),
                        selected_onnx=str(onnx_path),
                        train_output_dir="",
                        distill_output_dir="",
                        baseline_trace=baseline_trace,
                        trained_trace=baseline_trace,
                        distilled_trace=baseline_trace,
                    )
                )
                node_solved = True
                break

            train_output_dir = node_output_dir / "train"
            train_command = build_train_command(args, train_output_dir, checkpoint_path, start_node_id)
            run_command(train_command, dry_run=args.dry_run)
            trained_checkpoint = train_output_dir / "model.pt"
            trained_onnx = train_output_dir / "model.onnx"
            trained_trace = evaluate_policy(
                args,
                trained_onnx,
                node_output_dir,
                start_node_id,
                args.ticks,
                "trained",
            )
            trained_success = bool(trained_trace and trained_trace.get("Success", False))
            retained_success_rollouts = 0 if args.dry_run else count_success_rollouts(args, train_output_dir / "headless-rollouts")

            distill_output_dir = node_output_dir / "distill"
            distilled_trace: dict[str, Any] | None = None
            distilled_success = False
            selected_checkpoint = trained_checkpoint
            selected_onnx = trained_onnx
            if retained_success_rollouts > 0 or args.dry_run:
                distill_command = build_distill_command(
                    args,
                    distill_output_dir,
                    train_output_dir,
                    trained_checkpoint,
                    start_node_id,
                )
                run_command(distill_command, dry_run=args.dry_run)
                distilled_checkpoint = distill_output_dir / "model.pt"
                distilled_onnx = distill_output_dir / "model.onnx"
                distilled_trace = evaluate_policy(
                    args,
                    distilled_onnx,
                    node_output_dir,
                    start_node_id,
                    args.ticks,
                    "distilled",
                )
                distilled_success = bool(distilled_trace and distilled_trace.get("Success", False))
                if distilled_success:
                    selected_checkpoint = distilled_checkpoint
                    selected_onnx = distilled_onnx

            if trained_success and not distilled_success:
                selected_checkpoint = trained_checkpoint
                selected_onnx = trained_onnx

            results.append(
                CurriculumNodeResult(
                    start_node_id=start_node_id,
                    round_index=round_index,
                    baseline_success=baseline_success,
                    trained_success=trained_success,
                    distilled_success=distilled_success,
                    retained_success_rollouts=retained_success_rollouts,
                    selected_checkpoint=str(selected_checkpoint),
                    selected_onnx=str(selected_onnx),
                    train_output_dir=str(train_output_dir),
                    distill_output_dir=str(distill_output_dir),
                    baseline_trace=baseline_trace,
                    trained_trace=trained_trace,
                    distilled_trace=distilled_trace,
                )
            )

            if trained_success or distilled_success:
                checkpoint_path = selected_checkpoint
                onnx_path = selected_onnx
                node_solved = True
                break

        if not node_solved and args.stop_on_failure:
            break

    final_trace = evaluate_policy(
        args,
        onnx_path,
        output_root,
        -1,
        args.final_eval_ticks,
        "final-full-route",
    )
    summary = {
        "final_checkpoint": str(checkpoint_path),
        "final_onnx": str(onnx_path),
        "final_trace": final_trace,
        "results": [asdict(result) for result in results],
    }
    summary_path = output_root / "curriculum-summary.json"
    if not args.dry_run:
        with summary_path.open("w", encoding="utf-8") as handle:
            json.dump(summary, handle, indent=2)
    print(json.dumps(summary, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
