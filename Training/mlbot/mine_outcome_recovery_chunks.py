from __future__ import annotations

import argparse
import json
import subprocess
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any


@dataclass(frozen=True)
class Scenario:
    name: str
    level_name: str
    team: str
    class_id: str
    task: str
    ticks: int
    start_x: float | None = None
    start_y: float | None = None
    start_vx: float | None = None
    start_vy: float | None = None
    carrying_intel: bool | None = None
    success_criterion: str = "terminal_success"


@dataclass
class IterationRecord:
    iteration: int
    matrix_dir: str
    failure_scenario: str | None
    diagnostic_path: str | None
    likely_issue: str | None
    best_objective_distance: float | None
    chunk_spec: str | None
    chunk_dir: str | None
    candidate_matrix_dir: str | None = None
    candidate_best_objective_distance: float | None = None
    candidate_success: bool | None = None
    candidate_accepted: bool | None = None
    candidate_rejection_reason: str | None = None


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Closed-loop mine narrow outcome-policy recovery chunks from matrix failures.")
    parser.add_argument("--config", required=True, help="Director stack JSON containing task chunks/options.")
    parser.add_argument("--scenario-file", required=True)
    parser.add_argument("--model", required=True, help="name=path for the candidate base policy.")
    parser.add_argument(
        "--initial-task-chunk-spec",
        action="append",
        default=[],
        help="Existing task chunk spec to include before mining new recovery chunks.",
    )
    parser.add_argument("--output-dir", required=True)
    parser.add_argument("--rollout-project", default="MLBot.Tools/OpenGarrison.MLBot.Tools.csproj")
    parser.add_argument("--rollout-no-build", action="store_true")
    parser.add_argument("--max-iterations", type=int, default=3)
    parser.add_argument("--focus-scenario", action="append", default=[])
    parser.add_argument("--horizons", default="15,30,60,120")
    parser.add_argument(
        "--actions",
        default=(
            "idle,left,right,jump,jump_left,jump_right,"
            "jump_left_then_right,jump_right_then_left,jump_then_left,jump_then_right,"
            "tap_jump,tap_jump_left,tap_jump_right,jump_tap_then_left,jump_tap_then_right,"
            "left_then_tap_jump,right_then_tap_jump,left_then_tap_jump_right,right_then_tap_jump_left,"
            "double_jump_left_early,double_jump_right_early,double_jump_left_mid,double_jump_right_mid,"
            "double_jump_left_late,double_jump_right_late,double_jump_left_then_right,double_jump_right_then_left"
        ),
        help="Comma-separated outcome action templates to probe.",
    )
    parser.add_argument("--anchor-stride", type=int, default=8)
    parser.add_argument("--max-anchors", type=int, default=180)
    parser.add_argument("--stall-window", type=int, default=90)
    parser.add_argument("--distance-margin", type=float, default=240.0)
    parser.add_argument("--relative-x-margin", type=float, default=240.0)
    parser.add_argument("--relative-y-margin", type=float, default=220.0)
    parser.add_argument("--chunk-horizon", type=int, default=16)
    parser.add_argument("--chunk-commit-ticks", type=int, default=16)
    parser.add_argument("--jump-hold-ticks", type=int, default=8)
    parser.add_argument("--train-epochs", type=int, default=18)
    parser.add_argument("--train-batches-per-epoch", type=int, default=32)
    parser.add_argument("--train-batch-size", type=int, default=256)
    parser.add_argument("--train-learning-rate", type=float, default=2.5e-4)
    parser.add_argument("--min-selected-score", type=float, default=25.0)
    parser.add_argument("--min-best-margin", type=float, default=8.0)
    parser.add_argument("--min-improvement-pixels", type=float, default=5.0)
    parser.add_argument("--generalize-team", action="store_true")
    parser.add_argument("--generalize-class", action="store_true")
    parser.add_argument("--mirror-augment", action="store_true")
    parser.add_argument("--mirror-center-x", type=float, default=0.0)
    parser.add_argument("--sequence-search", action=argparse.BooleanOptionalAction, default=False)
    parser.add_argument("--sequence-beam-width", type=int, default=24)
    parser.add_argument("--sequence-top-k", type=int, default=1)
    parser.add_argument("--sequence-segment-ticks", type=int, default=3)
    parser.add_argument(
        "--sequence-max-counterfactuals-per-horizon",
        type=int,
        default=20000,
        help=(
            "Soft budget for sequence-search counterfactual rollouts per exported horizon. "
            "The miner lowers --max-anchors for that horizon when the requested beam search would exceed this."
        ),
    )
    parser.add_argument("--disable-policy-overrides", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--compact-rollouts", action=argparse.BooleanOptionalAction, default=True)
    parser.add_argument("--compact-outcomes", action=argparse.BooleanOptionalAction, default=True)
    return parser.parse_args()


def load_scenarios(path: Path, focus_names: set[str]) -> list[Scenario]:
    payload = json.loads(path.read_text(encoding="utf-8"))
    scenarios: list[Scenario] = []
    for item in payload.get("scenarios", []):
        name = str(item["name"])
        if focus_names and name not in focus_names:
            continue
        scenarios.append(
            Scenario(
                name=name,
                level_name=str(item["level_name"]),
                team=str(item["team"]),
                class_id=str(item["class_id"]),
                task=str(item["task"]),
                ticks=int(item["ticks"]),
                start_x=float(item["start_x"]) if item.get("start_x") is not None else None,
                start_y=float(item["start_y"]) if item.get("start_y") is not None else None,
                start_vx=float(item["start_vx"]) if item.get("start_vx") is not None else None,
                start_vy=float(item["start_vy"]) if item.get("start_vy") is not None else None,
                carrying_intel=bool(item["carrying_intel"]) if item.get("carrying_intel") is not None else None,
                success_criterion=str(item.get("success_criterion", "terminal_success")),
            )
        )
    if not scenarios:
        raise ValueError("no scenarios selected")
    return scenarios


def load_stack_args(config_path: Path) -> list[str]:
    config = json.loads(config_path.read_text(encoding="utf-8"))
    args: list[str] = []
    if config.get("return_hierarchical_chunk_model"):
        args.extend(["--return-hierarchical-chunk-model", str(config["return_hierarchical_chunk_model"])])
    if config.get("return_hierarchical_chunk_commit_ticks"):
        args.extend(
            [
                "--return-hierarchical-chunk-commit-ticks",
                ",".join(str(value) for value in config["return_hierarchical_chunk_commit_ticks"]),
            ]
        )
    for spec in config.get("task_options", []):
        args.extend(["--task-option-spec", str(spec)])
    for spec in config.get("task_chunks", []):
        args.extend(["--task-chunk-spec", str(spec)])
    return args


def run_command(command: list[str], cwd: Path) -> subprocess.CompletedProcess[str]:
    completed = subprocess.run(command, cwd=cwd, text=True, encoding="utf-8", capture_output=True)
    if completed.returncode != 0:
        raise RuntimeError(
            f"command failed ({completed.returncode})\n"
            f"command={' '.join(command)}\nstdout={completed.stdout}\nstderr={completed.stderr}"
        )
    return completed


def scenario_file_for_iteration(scenarios: list[Scenario], output_path: Path) -> Path:
    payload = {"schema": "mlbot-recovery-miner-scenarios", "scenarios": [asdict(scenario) for scenario in scenarios]}
    output_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    return output_path


def run_matrix(
    args: argparse.Namespace,
    repo_root: Path,
    scenario_file: Path,
    output_dir: Path,
    stack_args: list[str],
    mined_chunk_specs: list[str],
) -> Path:
    command = [
        str(repo_root / "Training/mlbot/.venv/Scripts/python.exe"),
        str(repo_root / "Training/mlbot/evaluate_policy_matrix.py"),
        "--rollout-project",
        str(repo_root / args.rollout_project),
        "--output-dir",
        str(output_dir),
        "--model",
        args.model,
        "--scenario-file",
        str(scenario_file),
    ]
    if args.rollout_no_build:
        command.append("--rollout-no-build")
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")
    command.extend(stack_args)
    for spec in mined_chunk_specs:
        command.extend(["--task-chunk-spec", spec])
    run_command(command, repo_root)
    return output_dir / "matrix-summary.json"


def first_failure(summary_path: Path) -> dict[str, Any] | None:
    payload = json.loads(summary_path.read_text(encoding="utf-8"))
    for result in payload.get("results", []):
        if not bool(result.get("success")):
            return result
    return None


def first_result(summary_path: Path) -> dict[str, Any] | None:
    payload = json.loads(summary_path.read_text(encoding="utf-8"))
    results = payload.get("results", [])
    if not results:
        return None
    result = results[0]
    return result if isinstance(result, dict) else None


def append_scenario_start(command: list[str], scenario: Scenario) -> None:
    if scenario.start_x is not None and scenario.start_y is not None:
        command.extend(["--start-x", str(scenario.start_x), "--start-y", str(scenario.start_y)])
    if scenario.start_vx is not None and scenario.start_vy is not None:
        command.extend(["--start-vx", str(scenario.start_vx), "--start-vy", str(scenario.start_vy)])
    if scenario.carrying_intel is True:
        command.append("--carrying-intel")
    elif scenario.carrying_intel is False:
        command.append("--no-carrying-intel")


def export_failed_rollout(
    args: argparse.Namespace,
    repo_root: Path,
    scenario: Scenario,
    output_path: Path,
    stack_args: list[str],
    mined_chunk_specs: list[str],
) -> None:
    model_path = args.model.partition("=")[2]
    command = [
        "dotnet",
        "run",
        "--project",
        str(repo_root / args.rollout_project),
    ]
    if args.rollout_no_build:
        command.append("--no-build")
    command.extend(
        [
            "--",
            "export-rollout",
            "--map",
            scenario.level_name,
            "--team",
            scenario.team,
            "--class",
            scenario.class_id,
            "--task",
            scenario.task,
            "--ticks",
            str(scenario.ticks),
            "--out",
            str(output_path),
        ]
    )
    if model_path.strip().lower() not in {"", "direct", "direct-objective"}:
        command.extend(["--model", model_path])
    if args.disable_policy_overrides:
        command.append("--disable-policy-overrides")
    if args.compact_rollouts:
        command.append("--compact-rollout")
    append_scenario_start(command, scenario)
    command.extend(stack_args)
    for spec in mined_chunk_specs:
        command.extend(["--task-chunk-spec", spec])
    run_command(command, repo_root)


def analyze_rollout(args: argparse.Namespace, repo_root: Path, rollout_path: Path, output_path: Path) -> dict[str, Any]:
    command = [
        "dotnet",
        "run",
        "--project",
        str(repo_root / args.rollout_project),
    ]
    if args.rollout_no_build:
        command.append("--no-build")
    command.extend(
        [
            "--",
            "analyze-rollout",
            "--in",
            str(rollout_path),
            "--out",
            str(output_path),
            "--stall-window",
            str(args.stall_window),
        ]
    )
    run_command(command, repo_root)
    return json.loads(output_path.read_text(encoding="utf-8"))


def build_window(args: argparse.Namespace, diagnostic: dict[str, Any]) -> dict[str, float]:
    observation = diagnostic["StallObservation"]
    objective = observation["Objective"]
    distance = float(observation["ObjectiveDistance"])
    relative_x = float(objective["RelativeX"])
    relative_y = float(objective["RelativeY"])
    return {
        "min_distance": max(0.0, distance - args.distance_margin),
        "max_distance": distance + args.distance_margin,
        "min_x": relative_x - args.relative_x_margin,
        "max_x": relative_x + args.relative_x_margin,
        "min_y": relative_y - args.relative_y_margin,
        "max_y": relative_y + args.relative_y_margin,
    }


def export_outcome_datasets(
    args: argparse.Namespace,
    repo_root: Path,
    scenario: Scenario,
    rollout_path: Path,
    output_dir: Path,
    window: dict[str, float],
) -> list[Path]:
    paths: list[Path] = []
    for raw_horizon in args.horizons.split(","):
        horizon = int(raw_horizon.strip())
        max_anchors = int(args.max_anchors)
        sequence_budget_note = ""
        if args.sequence_search:
            per_anchor = estimate_sequence_counterfactuals_per_anchor(
                horizon,
                int(args.sequence_segment_ticks),
                int(args.sequence_beam_width),
            )
            budget = int(args.sequence_max_counterfactuals_per_horizon)
            if budget > 0 and per_anchor > 0:
                budgeted_anchor_limit = max(1, budget // per_anchor)
                if max_anchors <= 0 or budgeted_anchor_limit < max_anchors:
                    max_anchors = budgeted_anchor_limit
                    sequence_budget_note = (
                        f" sequence_budget={budget} per_anchor={per_anchor} "
                        f"budgeted_max_anchors={max_anchors}"
                    )

        output_path = output_dir / f"outcome-h{horizon}.json"
        command = [
            "dotnet",
            "run",
            "--project",
            str(repo_root / args.rollout_project),
        ]
        if args.rollout_no_build:
            command.append("--no-build")
        command.extend(
            [
                "--",
                "export-outcome-dataset",
                "--rollout-path",
                str(rollout_path),
                "--out",
                str(output_path),
                "--horizon",
                str(horizon),
                "--jump-hold-ticks",
                str(args.jump_hold_ticks),
                "--anchor-stride",
                str(args.anchor_stride),
                "--max-anchors",
                str(max_anchors),
                "--min-objective-distance",
                f"{window['min_distance']:.3f}",
                "--max-objective-distance",
                f"{window['max_distance']:.3f}",
                "--team",
                scenario.team,
                "--class",
                scenario.class_id,
                "--task",
                scenario.task,
            ]
        )
        for action_name in [part.strip() for part in args.actions.split(",") if part.strip()]:
            command.extend(["--action", action_name])
        if args.compact_outcomes:
            command.append("--compact")
        if args.sequence_search:
            command.extend(
                [
                    "--sequence-search",
                    "--sequence-beam-width",
                    str(args.sequence_beam_width),
                    "--sequence-top-k",
                    str(args.sequence_top_k),
                    "--sequence-segment-ticks",
                    str(args.sequence_segment_ticks),
                ]
            )
        if scenario.carrying_intel is True:
            command.append("--carrying-intel-only")
        elif scenario.carrying_intel is False:
            command.append("--exclude-carrying-intel")
        if sequence_budget_note:
            print(f"export_outcomes horizon={horizon}{sequence_budget_note}")
        run_command(command, repo_root)
        paths.append(output_path)
    return paths


def estimate_sequence_counterfactuals_per_anchor(horizon: int, segment_ticks: int, beam_width: int) -> int:
    segment_ticks = max(1, segment_ticks)
    beam_width = max(1, beam_width)
    segments = max(1, (horizon + segment_ticks - 1) // segment_ticks)
    primitive_count = 6
    beam_count = 1
    total = 0
    for _ in range(segments):
        expansions = beam_count * primitive_count
        total += expansions
        beam_count = min(beam_width, expansions)
    return total


def train_chunk(
    args: argparse.Namespace,
    repo_root: Path,
    scenario: Scenario,
    outcome_paths: list[Path],
    output_dir: Path,
    window: dict[str, float],
) -> str:
    train_team = "" if args.generalize_team else scenario.team
    train_class_id = "" if args.generalize_class else scenario.class_id
    spec_team = "" if args.generalize_team else scenario.team
    spec_class_id = "" if args.generalize_class else scenario.class_id
    command = [
        str(repo_root / "Training/mlbot/.venv/Scripts/python.exe"),
        str(repo_root / "Training/mlbot/train_outcome_policy_chunk_head.py"),
        "--output-dir",
        str(output_dir),
        "--task-phase",
        scenario.task,
        "--team",
        train_team,
        "--class-id",
        train_class_id,
        "--horizon",
        str(args.chunk_horizon),
        "--jump-hold-ticks",
        str(args.jump_hold_ticks),
        "--min-objective-distance",
        f"{window['min_distance']:.3f}",
        "--max-objective-distance",
        f"{window['max_distance']:.3f}",
        "--min-objective-relative-x",
        f"{window['min_x']:.3f}",
        "--max-objective-relative-x",
        f"{window['max_x']:.3f}",
        "--min-objective-relative-y",
        f"{window['min_y']:.3f}",
        "--max-objective-relative-y",
        f"{window['max_y']:.3f}",
        "--epochs",
        str(args.train_epochs),
        "--batches-per-epoch",
        str(args.train_batches_per_epoch),
        "--batch-size",
        str(args.train_batch_size),
        "--learning-rate",
        str(args.train_learning_rate),
        "--min-selected-score",
        str(args.min_selected_score),
        "--min-best-margin",
        str(args.min_best_margin),
    ]
    for path in outcome_paths:
        command.extend(["--data-path", str(path)])
    if args.mirror_augment:
        command.append("--mirror-augment")
        command.extend(["--mirror-center-x", str(args.mirror_center_x)])
    try:
        run_command(command, repo_root)
    except RuntimeError as exception:
        output_dir.mkdir(parents=True, exist_ok=True)
        (output_dir / "chunk-error.txt").write_text(str(exception), encoding="utf-8")
        return ""

    chunk_model = output_dir / "chunk_model.onnx"
    if not chunk_model.exists():
        return ""
    requires_carrying = "true" if scenario.task.casefold() == "returnintel" else "false"
    return (
        f"{chunk_model}|{scenario.task}|{window['max_distance']:.0f}|{args.chunk_commit_ticks}|"
        f"{scenario.level_name}|{spec_team}|{spec_class_id}|"
        f"{window['min_x']:.0f}|{window['max_x']:.0f}|{window['min_y']:.0f}|{window['max_y']:.0f}|"
        f"{requires_carrying}|{window['min_distance']:.0f}"
    )


def main() -> None:
    args = parse_args()
    repo_root = Path.cwd()
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    scenarios = load_scenarios(Path(args.scenario_file), set(args.focus_scenario))
    scenario_by_name = {scenario.name: scenario for scenario in scenarios}
    scenario_file = scenario_file_for_iteration(scenarios, output_dir / "selected-scenarios.json")
    stack_args = load_stack_args(Path(args.config))
    mined_chunk_specs: list[str] = [str(spec) for spec in args.initial_task_chunk_spec if str(spec).strip()]
    records: list[IterationRecord] = []

    for iteration in range(args.max_iterations + 1):
        iteration_dir = output_dir / f"iteration-{iteration:02d}"
        matrix_dir = iteration_dir / "matrix"
        summary_path = run_matrix(args, repo_root, scenario_file, matrix_dir, stack_args, mined_chunk_specs)
        failure = first_failure(summary_path)
        if failure is None:
            records.append(IterationRecord(iteration, str(matrix_dir), None, None, None, None, None, None))
            break
        if iteration >= args.max_iterations:
            records.append(
                IterationRecord(
                    iteration,
                    str(matrix_dir),
                    str(failure["scenario"]["name"]),
                    None,
                    None,
                    float(failure.get("min_navigation_distance", 0.0)),
                    None,
                    None,
                )
            )
            break

        scenario_name = str(failure["scenario"]["name"])
        scenario = scenario_by_name[scenario_name]
        rollout_path = iteration_dir / f"{scenario.name}.rollout.json"
        diagnostic_path = iteration_dir / f"{scenario.name}.diagnostic.json"
        export_failed_rollout(args, repo_root, scenario, rollout_path, stack_args, mined_chunk_specs)
        diagnostic = analyze_rollout(args, repo_root, rollout_path, diagnostic_path)
        window = build_window(args, diagnostic)
        outcome_dir = iteration_dir / "outcomes"
        outcome_dir.mkdir(parents=True, exist_ok=True)
        outcome_paths = export_outcome_datasets(args, repo_root, scenario, rollout_path, outcome_dir, window)
        chunk_dir = iteration_dir / "chunk"
        chunk_spec = train_chunk(args, repo_root, scenario, outcome_paths, chunk_dir, window)
        if not chunk_spec:
            records.append(
                IterationRecord(
                    iteration,
                    str(matrix_dir),
                    scenario.name,
                    str(diagnostic_path),
                    f"{diagnostic.get('LikelyIssue', '')}: no_trainable_counterfactual",
                    float(diagnostic.get("BestObjectiveDistance", 0.0)),
                    None,
                    str(chunk_dir),
                )
            )
            print(
                f"iteration={iteration} no_trainable_counterfactual scenario={scenario.name} "
                f"issue={diagnostic.get('LikelyIssue')} best={float(diagnostic.get('BestObjectiveDistance', 0.0)):.1f}"
            )
            break
        candidate_matrix_dir = iteration_dir / "candidate-matrix"
        candidate_summary_path = run_matrix(
            args,
            repo_root,
            scenario_file,
            candidate_matrix_dir,
            stack_args,
            [*mined_chunk_specs, chunk_spec],
        )
        candidate_result = first_result(candidate_summary_path)
        candidate_success = bool(candidate_result and candidate_result.get("success"))
        candidate_best_distance = (
            float(candidate_result.get("min_navigation_distance", 0.0))
            if candidate_result is not None
            else None
        )
        current_best_distance = float(failure.get("min_navigation_distance", diagnostic.get("BestObjectiveDistance", 0.0)))
        improved = candidate_success or (
            candidate_best_distance is not None
            and candidate_best_distance <= current_best_distance - float(args.min_improvement_pixels)
        )
        if not improved:
            rejection_reason = (
                "candidate_worse_or_flat"
                if candidate_best_distance is not None
                else "candidate_missing_result"
            )
            records.append(
                IterationRecord(
                    iteration,
                    str(matrix_dir),
                    scenario.name,
                    str(diagnostic_path),
                    str(diagnostic.get("LikelyIssue", "")),
                    float(diagnostic.get("BestObjectiveDistance", 0.0)),
                    chunk_spec,
                    str(chunk_dir),
                    str(candidate_matrix_dir),
                    candidate_best_distance,
                    candidate_success,
                    False,
                    rejection_reason,
                )
            )
            print(
                f"iteration={iteration} rejected scenario={scenario.name} issue={diagnostic.get('LikelyIssue')} "
                f"current={current_best_distance:.1f} candidate={candidate_best_distance if candidate_best_distance is not None else 'none'}"
            )
            break

        mined_chunk_specs.append(chunk_spec)
        records.append(
            IterationRecord(
                iteration,
                str(matrix_dir),
                scenario.name,
                str(diagnostic_path),
                str(diagnostic.get("LikelyIssue", "")),
                float(diagnostic.get("BestObjectiveDistance", 0.0)),
                chunk_spec,
                str(chunk_dir),
                str(candidate_matrix_dir),
                candidate_best_distance,
                candidate_success,
                True,
                None,
            )
        )
        print(
            f"iteration={iteration} accepted scenario={scenario.name} issue={diagnostic.get('LikelyIssue')} "
            f"current={current_best_distance:.1f} candidate={candidate_best_distance if candidate_best_distance is not None else 'none'}"
        )

    summary = {
        "model": args.model,
        "scenario_file": str(args.scenario_file),
        "config": str(args.config),
        "mined_chunk_specs": mined_chunk_specs,
        "iterations": [asdict(record) for record in records],
    }
    (output_dir / "recovery-miner-summary.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
    print(f"saved recovery_summary={output_dir / 'recovery-miner-summary.json'}")


if __name__ == "__main__":
    main()
