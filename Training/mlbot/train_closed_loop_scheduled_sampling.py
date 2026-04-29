from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=(
            "Run closed-loop scheduled-sampling refinement: export student-state "
            "teacher recoveries, distill them, and promote only matrix-improving candidates."
        )
    )
    parser.add_argument("--output-root", required=True)
    parser.add_argument("--rollout-project", required=True)
    parser.add_argument("--scenario-file", required=True)
    parser.add_argument("--init-checkpoint", required=True)
    parser.add_argument("--init-model", required=True)
    parser.add_argument("--teacher-model", required=True)
    parser.add_argument("--teacher-return-replay-bank", default="")
    parser.add_argument("--teacher-return-replay-engage-distance", type=float, default=3000.0)
    parser.add_argument("--teacher-return-replay-max-score", type=float, default=2.0)
    parser.add_argument("--teacher-replay-bank", default="")
    parser.add_argument("--teacher-replay-task", default="ReturnIntel")
    parser.add_argument("--teacher-replay-engage-distance", type=float, default=3000.0)
    parser.add_argument("--teacher-replay-max-score", type=float, default=2.0)
    parser.add_argument("--teacher-replay-requires-carrying-intel", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--teacher-return-finalizer-model", default="")
    parser.add_argument("--teacher-return-finalizer-engage-distance", type=float, default=0.0)
    parser.add_argument("--teacher-return-finalizer-map", default="")
    parser.add_argument("--teacher-return-finalizer-team", default="")
    parser.add_argument("--teacher-return-finalizer-class", default="")
    parser.add_argument("--teacher-return-finalizer-after-options", action="store_true")
    parser.add_argument("--teacher-return-chunk-model", default="")
    parser.add_argument("--teacher-return-chunk-engage-distance", type=float, default=0.0)
    parser.add_argument("--teacher-return-chunk-commit-ticks", type=int, default=0)
    parser.add_argument(
        "--teacher-return-chunk-spec",
        action="append",
        default=[],
        help=(
            "Chunk option as model|engage_distance|commit_ticks|map_filter|team_filter|class_filter"
            "[|min_rel_x|max_rel_x|min_rel_y|max_rel_y]. Later specs wrap earlier specs."
        ),
    )
    parser.add_argument("--teacher-return-chunk-selector-model", default="")
    parser.add_argument(
        "--teacher-return-selected-chunk-spec",
        action="append",
        default=[],
        help="Selector chunk option as model|commit_ticks. Option 0 is the base policy; options 1..N follow this order.",
    )
    parser.add_argument("--teacher-return-hierarchical-chunk-model", default="")
    parser.add_argument("--teacher-return-hierarchical-chunk-commit-ticks", default="")
    parser.add_argument(
        "--teacher-task-option-spec",
        action="append",
        default=[],
        help="Teacher task option as model|phase|engage_distance|map_filter|team_filter|class_filter.",
    )
    parser.add_argument("--include-attack", action="store_true")
    parser.add_argument(
        "--trigger-requires-carrying-intel",
        action=argparse.BooleanOptionalAction,
        default=True,
        help="Require carrying intel before teacher intervention. Disable for AttackIntel recovery.",
    )
    parser.add_argument("--anchor-data-root", required=True)
    parser.add_argument("--teacher-rollout-dir", action="append", default=[])
    parser.add_argument("--target-map", required=True)
    parser.add_argument("--target-team", required=True)
    parser.add_argument("--target-class", required=True)
    parser.add_argument("--target-task", required=True)
    parser.add_argument("--target-ticks", type=int, required=True)
    parser.add_argument("--target-start-node-id", type=int, default=-1)
    parser.add_argument("--target-start-x", type=float, default=None)
    parser.add_argument("--target-start-y", type=float, default=None)
    parser.add_argument("--target-start-vx", type=float, default=None)
    parser.add_argument("--target-start-vy", type=float, default=None)
    parser.add_argument("--target-carrying-intel", action=argparse.BooleanOptionalAction, default=None)
    parser.add_argument(
        "--distill-regression-eval",
        action="append",
        default=[],
        help="Forwarded to distill_rollout_policy.py as map,team,class,task,ticks[,start...].",
    )
    parser.add_argument(
        "--distill-regression-from-scenario-file",
        action="store_true",
        help="Use every matrix scenario as a distillation regression eval.",
    )
    parser.add_argument("--rounds", type=int, default=3)
    parser.add_argument("--epochs", type=int, default=16)
    parser.add_argument("--batches-per-epoch", type=int, default=24)
    parser.add_argument("--learning-rate", type=float, default=8e-5)
    parser.add_argument("--batch-size", type=int, default=512)
    parser.add_argument("--intervention-horizon", type=int, default=3600)
    parser.add_argument("--pre-intervention-label-window", type=int, default=120)
    parser.add_argument("--trigger-navigation-distance", type=float, default=3000.0)
    parser.add_argument("--corrective-replay-sample-weight", type=float, default=3.0)
    parser.add_argument("--action-margin-coef", type=float, default=4.0)
    parser.add_argument("--bc-coef", type=float, default=0.3)
    parser.add_argument("--reference-kl-coef", type=float, default=0.04)
    parser.add_argument("--segment-mode", default="terminal-window")
    parser.add_argument("--segment-pre-ticks", type=int, default=240)
    parser.add_argument("--segment-post-ticks", type=int, default=1200)
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--disable-policy-overrides", action="store_true")
    parser.add_argument("--freeze-backbone", action="store_true")
    parser.add_argument("--regression-gated-updates", action="store_true")
    parser.add_argument("--target-first-selection", action="store_true")
    parser.add_argument("--require-target-success-retention", action="store_true")
    parser.add_argument("--require-regression-success-retention", action="store_true")
    parser.add_argument("--reset-optimizer-on-reject", action="store_true")
    parser.add_argument("--stop-on-reject", action="store_true")
    parser.add_argument(
        "--require-success-count-improvement",
        action="store_true",
        help="Reject candidates unless they improve the number of successful matrix scenarios.",
    )
    parser.add_argument("--cpu", action="store_true")
    return parser.parse_args()


def run_command(command: list[str], *, cwd: Path) -> None:
    print(" ".join(command))
    completed = subprocess.run(command, cwd=cwd, check=False, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(f"command failed with exit code {completed.returncode}: {' '.join(command)}")


def run_capture(command: list[str], *, cwd: Path) -> str:
    print(" ".join(command))
    completed = subprocess.run(command, cwd=cwd, check=False, capture_output=True, text=True, encoding="utf-8")
    if completed.returncode != 0:
        raise RuntimeError(
            "command failed\n"
            f"exit_code={completed.returncode}\n"
            f"command={' '.join(command)}\nstdout={completed.stdout}\nstderr={completed.stderr}"
        )
    if completed.stdout:
        print(completed.stdout.strip())
    if completed.stderr:
        print(completed.stderr.strip())
    return completed.stdout


def evaluate_matrix(
    args: argparse.Namespace,
    model_path: Path,
    output_dir: Path,
    label: str,
    cwd: Path,
) -> dict[str, Any]:
    command = [
        sys.executable,
        str(Path(__file__).with_name("evaluate_policy_matrix.py")),
        "--rollout-project",
        args.rollout_project,
        "--output-dir",
        str(output_dir),
        "--model",
        f"{label}={model_path}",
        "--scenario-file",
        args.scenario_file,
    ]
    if args.rollout_no_build:
        command.append("--rollout-no-build")
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")
    run_capture(command, cwd=cwd)
    with (output_dir / "matrix-summary.json").open("r", encoding="utf-8") as handle:
        return json.load(handle)


def matrix_score(summary: dict[str, Any]) -> tuple[float, ...]:
    results = summary.get("results", [])
    success_count = sum(1 for result in results if result.get("success"))
    attack_successes = sum(
        1 for result in results if str(result.get("success_criterion", "")).lower() == "attack_pickup" and result.get("success")
    )
    return_successes = sum(
        1 for result in results if str(result.get("success_criterion", "")).lower() == "return_score" and result.get("success")
    )
    full_successes = sum(
        1 for result in results if str(result.get("success_criterion", "")).lower() == "full_score" and result.get("success")
    )
    min_nav_sum = sum(float(result.get("min_navigation_distance", 1_000_000.0)) for result in results)
    final_nav_sum = sum(float(result.get("final_navigation_distance", 1_000_000.0)) for result in results)
    tick_sum = sum(float(result.get("ticks_elapsed", 1_000_000.0)) for result in results)
    return (
        float(success_count),
        float(return_successes + full_successes),
        float(full_successes),
        float(return_successes),
        float(attack_successes),
        -min_nav_sum,
        -final_nav_sum,
        -tick_sum,
    )


def matrix_success_count(summary: dict[str, Any]) -> int:
    return sum(1 for result in summary.get("results", []) if result.get("success"))


def should_accept_candidate(
    args: argparse.Namespace,
    candidate_summary: dict[str, Any],
    baseline_success_count: int,
    candidate_score: tuple[float, ...],
    accepted_score: tuple[float, ...],
) -> bool:
    if args.require_success_count_improvement and matrix_success_count(candidate_summary) <= baseline_success_count:
        return False
    return candidate_score > accepted_score


def export_interventions(args: argparse.Namespace, student_model: Path, output_dir: Path, cwd: Path) -> None:
    command = [
        sys.executable,
        str(Path(__file__).with_name("export_replay_teacher_interventions.py")),
        "--rollout-project",
        args.rollout_project,
        "--scenario-file",
        args.scenario_file,
        "--output-dir",
        str(output_dir),
        "--student-model",
        str(student_model),
        "--teacher-model",
        args.teacher_model,
        "--trigger-navigation-distance",
        str(args.trigger_navigation_distance),
        "--intervention-horizon",
        str(args.intervention_horizon),
        "--pre-intervention-label-window",
        str(args.pre_intervention_label_window),
        "--buffer-risk-only",
    ]
    if args.teacher_return_replay_bank:
        command.extend(
            [
                "--teacher-return-replay-bank",
                args.teacher_return_replay_bank,
                "--teacher-return-replay-engage-distance",
                str(args.teacher_return_replay_engage_distance),
                "--teacher-return-replay-max-score",
                str(args.teacher_return_replay_max_score),
            ]
        )
    if args.teacher_replay_bank:
        command.extend(
            [
                "--teacher-replay-bank",
                args.teacher_replay_bank,
                "--teacher-replay-task",
                args.teacher_replay_task,
                "--teacher-replay-engage-distance",
                str(args.teacher_replay_engage_distance),
                "--teacher-replay-max-score",
                str(args.teacher_replay_max_score),
            ]
        )
        if not args.teacher_replay_requires_carrying_intel:
            command.append("--no-teacher-replay-requires-carrying-intel")
    if args.teacher_return_finalizer_model:
        command.extend(["--teacher-return-finalizer-model", args.teacher_return_finalizer_model])
        command.extend(["--teacher-return-finalizer-engage-distance", str(args.teacher_return_finalizer_engage_distance)])
        if args.teacher_return_finalizer_map:
            command.extend(["--teacher-return-finalizer-map", args.teacher_return_finalizer_map])
        if args.teacher_return_finalizer_team:
            command.extend(["--teacher-return-finalizer-team", args.teacher_return_finalizer_team])
        if args.teacher_return_finalizer_class:
            command.extend(["--teacher-return-finalizer-class", args.teacher_return_finalizer_class])
        if args.teacher_return_finalizer_after_options:
            command.append("--teacher-return-finalizer-after-options")
    if args.teacher_return_chunk_model:
        command.extend(["--teacher-return-chunk-model", args.teacher_return_chunk_model])
        command.extend(["--teacher-return-chunk-engage-distance", str(args.teacher_return_chunk_engage_distance)])
        if args.teacher_return_chunk_commit_ticks > 0:
            command.extend(["--teacher-return-chunk-commit-ticks", str(args.teacher_return_chunk_commit_ticks)])
    for spec in args.teacher_return_chunk_spec:
        command.extend(["--teacher-return-chunk-spec", spec])
    if args.teacher_return_chunk_selector_model:
        command.extend(["--teacher-return-chunk-selector-model", args.teacher_return_chunk_selector_model])
    for spec in args.teacher_return_selected_chunk_spec:
        command.extend(["--teacher-return-selected-chunk-spec", spec])
    if args.teacher_return_hierarchical_chunk_model:
        command.extend(["--teacher-return-hierarchical-chunk-model", args.teacher_return_hierarchical_chunk_model])
    if args.teacher_return_hierarchical_chunk_commit_ticks:
        command.extend(["--teacher-return-hierarchical-chunk-commit-ticks", args.teacher_return_hierarchical_chunk_commit_ticks])
    for spec in args.teacher_task_option_spec:
        command.extend(["--teacher-task-option-spec", spec])
    if args.include_attack:
        command.append("--include-attack")
    if args.trigger_requires_carrying_intel:
        command.append("--trigger-requires-carrying-intel")
    else:
        command.append("--no-trigger-requires-carrying-intel")
    if args.rollout_no_build:
        command.append("--rollout-no-build")
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")
    run_capture(command, cwd=cwd)


def scenario_to_regression_eval(scenario: dict[str, Any]) -> str:
    parts = [
        str(scenario["level_name"]),
        str(scenario["team"]),
        str(scenario["class_id"]),
        str(scenario["task"]),
        str(int(scenario["ticks"])),
    ]
    start_x = scenario.get("start_x")
    start_y = scenario.get("start_y")
    if start_x is not None or start_y is not None:
        if start_x is None or start_y is None:
            raise ValueError(f"scenario {scenario.get('name', '<unnamed>')} must provide both start_x and start_y")
        parts.extend([str(float(start_x)), str(float(start_y))])
        carrying_intel = scenario.get("carrying_intel")
        if carrying_intel is not None or scenario.get("start_vx") is not None or scenario.get("start_vy") is not None:
            parts.append("" if carrying_intel is None else str(bool(carrying_intel)).lower())
        if scenario.get("start_vx") is not None or scenario.get("start_vy") is not None:
            if scenario.get("start_vx") is None or scenario.get("start_vy") is None:
                raise ValueError(f"scenario {scenario.get('name', '<unnamed>')} must provide both start_vx and start_vy")
            parts.extend([str(float(scenario["start_vx"])), str(float(scenario["start_vy"]))])
    elif int(scenario.get("start_node_id", -1)) >= 0:
        parts.append(str(int(scenario["start_node_id"])))
    return ",".join(parts)


def build_regression_eval_args(args: argparse.Namespace) -> list[str]:
    regression_evals = list(args.distill_regression_eval)
    if args.distill_regression_from_scenario_file:
        with Path(args.scenario_file).open("r", encoding="utf-8") as handle:
            payload: dict[str, Any] = json.load(handle)
        regression_evals.extend(scenario_to_regression_eval(item) for item in payload.get("scenarios", []))

    command: list[str] = []
    for regression_eval in regression_evals:
        command.extend(["--regression-eval", regression_eval])
    return command


def distill_round(
    args: argparse.Namespace,
    round_dir: Path,
    init_checkpoint: Path,
    intervention_dir: Path,
    cwd: Path,
) -> tuple[Path, Path]:
    output_dir = round_dir / "distill"
    intervention_paths = verified_intervention_paths(intervention_dir)
    command = [
        sys.executable,
        str(Path(__file__).with_name("distill_rollout_policy.py")),
        "--data-root",
        args.anchor_data_root,
        "--output-dir",
        str(output_dir),
        "--init-checkpoint",
        str(init_checkpoint),
        "--rollout-glob",
        "*.json",
        "--allow-mixed-rollout-contexts",
        "--top-k-per-rollout-context",
        "6",
        "--teacher-selection-key",
        "fast-success",
        "--segment-mode",
        args.segment_mode,
        "--segment-pre-ticks",
        str(args.segment_pre_ticks),
        "--segment-post-ticks",
        str(args.segment_post_ticks),
        "--epochs",
        str(args.epochs),
        "--batches-per-epoch",
        str(args.batches_per_epoch),
        "--learning-rate",
        str(args.learning_rate),
        "--batch-size",
        str(args.batch_size),
        "--balance-rollout-contexts",
        "--bc-coef",
        str(args.bc_coef),
        "--reference-kl-coef",
        str(args.reference_kl_coef),
        "--action-margin-coef",
        str(args.action_margin_coef),
        "--move-margin",
        "1.5",
        "--binary-margin",
        "1.0",
        "--corrective-replay-sample-weight",
        str(args.corrective_replay_sample_weight),
        "--success-only",
        "--rollout-project",
        args.rollout_project,
        "--map",
        args.target_map,
        "--team",
        args.target_team,
        "--class",
        args.target_class,
        "--task",
        args.target_task,
        "--ticks",
        str(args.target_ticks),
        "--task-conditioned-heads",
    ]
    if args.target_start_node_id >= 0:
        command.extend(["--start-node-id", str(args.target_start_node_id)])
    if args.target_start_x is not None and args.target_start_y is not None:
        command.extend(["--start-x", str(args.target_start_x), "--start-y", str(args.target_start_y)])
    elif args.target_start_x is not None or args.target_start_y is not None:
        raise ValueError("--target-start-x and --target-start-y must be supplied together")
    if args.target_start_vx is not None and args.target_start_vy is not None:
        command.extend(["--start-vx", str(args.target_start_vx), "--start-vy", str(args.target_start_vy)])
    elif args.target_start_vx is not None or args.target_start_vy is not None:
        raise ValueError("--target-start-vx and --target-start-vy must be supplied together")
    if args.target_carrying_intel is True:
        command.append("--carrying-intel")
    elif args.target_carrying_intel is False:
        command.append("--no-carrying-intel")
    for intervention_path in intervention_paths:
        command.extend(["--rollout-path", str(intervention_path)])
    for teacher_rollout_dir in args.teacher_rollout_dir:
        command.extend(["--rollout-dir", teacher_rollout_dir])
    command.extend(build_regression_eval_args(args))
    if args.rollout_no_build:
        command.append("--rollout-no-build")
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")
    if args.freeze_backbone:
        command.append("--freeze-backbone")
    if args.regression_gated_updates:
        command.append("--regression-gated-updates")
    if args.target_first_selection:
        command.append("--target-first-selection")
    if args.require_target_success_retention:
        command.append("--require-target-success-retention")
    if args.require_regression_success_retention:
        command.append("--require-regression-success-retention")
    if args.reset_optimizer_on_reject:
        command.append("--reset-optimizer-on-reject")
    if args.cpu:
        command.append("--cpu")
    run_command(command, cwd=cwd)
    return output_dir / "model.pt", output_dir / "model.onnx"


def verified_intervention_paths(intervention_dir: Path) -> list[Path]:
    summary_path = intervention_dir / "intervention-export-summary.json"
    if not summary_path.is_file():
        raise FileNotFoundError(f"intervention summary missing: {summary_path}")
    with summary_path.open("r", encoding="utf-8") as handle:
        summary = json.load(handle)
    paths: list[Path] = []
    for item in summary:
        if not isinstance(item, dict) or not item.get("included") or not item.get("success"):
            continue
        path = Path(str(item.get("path", "")))
        if path.is_file():
            paths.append(path)
    if not paths:
        raise ValueError(f"no verified intervention documents found in {summary_path}")
    return paths


def write_json(path: Path, payload: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8") as handle:
        json.dump(payload, handle, indent=2)


def main() -> None:
    args = parse_args()
    cwd = Path.cwd()
    output_root = Path(args.output_root)
    output_root.mkdir(parents=True, exist_ok=True)

    accepted_checkpoint = Path(args.init_checkpoint)
    accepted_model = Path(args.init_model)
    baseline_summary = evaluate_matrix(args, accepted_model, output_root / "baseline-matrix", "baseline", cwd)
    accepted_score = matrix_score(baseline_summary)
    accepted_success_count = matrix_success_count(baseline_summary)
    history: list[dict[str, Any]] = [
        {
            "round": 0,
            "accepted": True,
            "model": str(accepted_model),
            "checkpoint": str(accepted_checkpoint),
            "score": list(accepted_score),
        }
    ]

    for round_index in range(1, args.rounds + 1):
        round_dir = output_root / f"round-{round_index:02d}"
        intervention_dir = round_dir / "interventions"
        export_interventions(args, accepted_model, intervention_dir, cwd)
        candidate_checkpoint, candidate_model = distill_round(
            args,
            round_dir,
            accepted_checkpoint,
            intervention_dir,
            cwd,
        )
        candidate_summary = evaluate_matrix(
            args,
            candidate_model,
            round_dir / "candidate-matrix",
            f"round_{round_index:02d}",
            cwd,
        )
        candidate_score = matrix_score(candidate_summary)
        accepted = should_accept_candidate(
            args,
            candidate_summary,
            accepted_success_count,
            candidate_score,
            accepted_score,
        )
        if accepted:
            accepted_score = candidate_score
            accepted_success_count = matrix_success_count(candidate_summary)
            accepted_checkpoint = candidate_checkpoint
            accepted_model = candidate_model
            shutil.copy2(candidate_checkpoint, output_root / "accepted-model.pt")
            shutil.copy2(candidate_model, output_root / "accepted-model.onnx")

        history.append(
            {
                "round": round_index,
                "accepted": accepted,
                "candidate_checkpoint": str(candidate_checkpoint),
                "candidate_model": str(candidate_model),
                "score": list(candidate_score),
                "accepted_score": list(accepted_score),
                "matrix_summary": str(round_dir / "candidate-matrix" / "matrix-summary.json"),
                "interventions": str(intervention_dir),
            }
        )
        write_json(output_root / "scheduled-sampling-summary.json", history)
        print(f"round={round_index} accepted={accepted} score={candidate_score} accepted_score={accepted_score}")
        if not accepted and args.stop_on_reject:
            break

    if not (output_root / "accepted-model.onnx").exists():
        shutil.copy2(accepted_checkpoint, output_root / "accepted-model.pt")
        shutil.copy2(accepted_model, output_root / "accepted-model.onnx")
    write_json(output_root / "scheduled-sampling-summary.json", history)


if __name__ == "__main__":
    main()
