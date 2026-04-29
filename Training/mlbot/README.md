ML Bot Behavior Cloning
=======================

Purpose
-------
Train a behavior-cloning policy from in-game demonstration recordings produced by
`ml_demo_rec`.

Expected Demo Files
-------------------

The trainer reads JSON files created by the client/headless tools under:

- `.mlbot-data/`

Each sample contains:

- observation
- action
- next_observation
- resolved phase
- event flags

Model Contract
--------------

The exported ONNX model uses:

- input `obs`: shape `[batch, 172]` for the current V7 world-truth-plus-terrain-affordance schema
- output `move_logits`: shape `[batch, 3]`
- output `binary_logits`: shape `[batch, 5]`
- output `aim`: shape `[batch, 2]`

Feature Order
-------------

The Python code mirrors `MLBotFeatureVectorizer` from the C# runtime. V7 deliberately excludes
legacy nav graph and score-route features while retaining direct world-truth and local affordance
features. V7 adds direct collision-derived landing candidates for local vertical traversal. If you
change the feature vector in C#, update `mlbot_dataset.py` to match before training a new model.

Schema Verification
-------------------

Run this before training or promoting a model:

- `python verify_schema.py --rollout-project "../../MLBot.Tools/OpenGarrison.MLBot.Tools.csproj" --rollout-no-build`

World-Truth Fixtures
--------------------

`--start-node-id` is deprecated because node ids came from the legacy nav graph. Use direct
fixtures instead:

- `--start-x <world-x>`
- `--start-y <world-y>`
- `--carrying-intel` / `--no-carrying-intel`

The same fixture fields are supported by the V5 matrix runner through `config/v5_regression_matrix.json`.

Quick Start
-----------

1. Create demos in the client:
   - ``ml_demo_rec start auto``
   - play a run
   - ``ml_demo_rec stop``

2. Inspect demos:
   - `dotnet run --project MLBot.Tools -- demo-summary --root "<path-to-demo-root>"`

3. Train:
   - `python train_behavior_cloning.py --data-root "<path-to-demo-root>" --output-dir "<repo>/Training/mlbot/out"`

   Preferred V5 unified model path:
   - `python train_v5_unified.py --data-root "<repo>/.mlbot-data" --output-dir "<repo>/Training/mlbot/out/v5-unified"`

4. Rebaseline/promote:
   - `python rebaseline_v5.py --rollout-project "<repo>/MLBot.Tools/OpenGarrison.MLBot.Tools.csproj" --output-dir "<repo>/Training/mlbot/out/v5-rebaseline" --rollout-no-build`

   Preferred fast matrix gate:
   - `dotnet run --no-build --project "<repo>/MLBot.Tools/OpenGarrison.MLBot.Tools.csproj" -- eval-matrix --config "<repo>/Training/mlbot/config/v7_director_twod_ctf_chain_stack_20260426.json" --scenario-file "<repo>/Training/mlbot/config/v7_twod_ctf_chain_matrix_20260426.json" --output-dir "<repo>/Training/mlbot/out/fast-matrix" --model current="<repo>/Training/mlbot/out/v6-bc-outcome-mixed-repeat8-20260426/model.onnx"`

   `eval-matrix` runs all selected scenarios in one process, loads each model once, supports
   director stack configs through `--config`, and writes `matrix-summary.json`. Use it for V8
   promotion gates and inner-loop regression checks.

5. Optional action-outcome pretraining data:
   - `dotnet run --project MLBot.Tools -- export-rollout --compact-rollout --out "<repo>/Training/mlbot/out/outcome/rollout.json" --map TwodFortTwo --team Red --class Scout --task AttackIntel --ticks 1800 --model "<model.onnx>" --disable-policy-overrides`
   - `dotnet run --project MLBot.Tools -- export-outcome-dataset --compact --out "<repo>/Training/mlbot/out/outcome/outcome-dataset.json" --rollout-path "<repo>/Training/mlbot/out/outcome/rollout.json" --horizon 30 --jump-hold-ticks 8`
   - `python train_outcome_predictor.py --data-path "<repo>/Training/mlbot/out/outcome/outcome-dataset.json" --output-dir "<repo>/Training/mlbot/out/outcome/model"`
   - `python train_behavior_cloning.py --data-root "<demo-root>" --output-dir "<repo>/Training/mlbot/out/bc-outcome" --outcome-data-path "<repo>/Training/mlbot/out/outcome/outcome-dataset.json" --outcome-aux-coef 0.25`

   `train_behavior_cloning.py` uses the outcome head only during training. The exported policy ONNX
   still exposes the normal `obs`, `move_logits`, `binary_logits`, and `aim` contract.
   Compact rollouts and compact outcome datasets are preferred for mining because they avoid
   writing duplicated transition observations and per-sample start observations.

6. Optional outcome-policy pseudo demos:
   - `python build_outcome_policy_dataset.py --data-path "<repo>/Training/mlbot/out/outcome/outcome-dataset.json" --output-dir "<repo>/Training/mlbot/out/outcome-policy" --repeat-selected 4`
   - `python train_behavior_cloning.py --data-root "<demo-root>" --extra-data-root "<repo>/Training/mlbot/out/outcome-policy" --output-dir "<repo>/Training/mlbot/out/bc-mixed" --success-only --task-conditioned-heads`

   Outcome-policy labels are generated from simulator counterfactuals. Treat them as curriculum
   data and gate them with deterministic rollout matrices before promotion.
   V7 scoring explicitly rewards progress toward the best reachable upward landing so alternating
   stair transfers are not punished just because they briefly move away from the objective.

7. Optional outcome-policy sequence chunks:
   - `python train_outcome_policy_chunk_head.py --data-path "<repo>/Training/mlbot/out/outcome/outcome-dataset.json" --output-dir "<repo>/Training/mlbot/out/outcome-chunk" --task-phase AttackIntel --team Red --class-id Scout --horizon 16 --jump-hold-ticks 8`

   Use this for recovery windows where the important behavior is a short action sequence, such as
   jump-then-release. Prefer failed closed-loop traces for recovery labels; successful rollouts may
   miss pinned/stuck dynamic states.
   `mine_outcome_recovery_chunks.py` is the preferred closed-loop wrapper. It can continue from an
   existing option stack with repeated `--initial-task-chunk-spec` values, rejects low-score or
   low-margin counterfactual labels, and now accepts a mined chunk only after an immediate candidate
   matrix improves by `--min-improvement-pixels` or succeeds.
   Add `--sequence-search` for narrow final-approach failures where fixed action templates are too
   coarse. Sequence search beam-searches short move/jump timelines in the simulator, emits the best
   sequence per anchor by default, and still relies on the candidate matrix gate before appending a
   chunk.
   For near-objective completion chunks, `train_outcome_policy_chunk_head.py --require-success` can
   restrict labels to counterfactuals that actually scored/captured, and repeated `--action-name`
   filters can isolate a simple successful action family such as `right`.

8. Exported model:
   - `<output-dir>/model.onnx`

9. Use model in game:
   - set environment variable `OG_BOT_MODE=ml`
   - set environment variable `OG_MLBOT_MODEL_PATH=<absolute path to model.onnx>`

Notes
-----

- It trains from flat structured observations, not images.
- Behavior cloning, headless rollout refinement, intervention DAgger, rollout distillation,
  and matrix evaluation scripts all share the same feature vector.
- Promotion/evaluation runs should use `--disable-policy-overrides` unless the goal is explicitly
  measuring runtime override behavior.
- V7/V6 task chunks are legacy curriculum scaffolding, not the desired deployed shape. V8 work
  should distill useful teacher behavior into one shared policy conditioned by direct class physics
  and world-state inputs.
- First V8 standalone scoring proof:
  `Training/mlbot/out/v8-red-scout-return-standalone-score-proof-20260426/model.onnx` scores
  `twod_red_scout_return_score_from_pickup` with no runtime chunk stack and overrides disabled.
  Treat this as a narrow proof of the reset loop, not a broad promotion.
