from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, Sequence

import numpy as np


FEATURE_COUNT_V4 = 110
FEATURE_COUNT_V5 = 101
FEATURE_COUNT_V6 = 147
FEATURE_COUNT_V7 = 172
FEATURE_COUNT = FEATURE_COUNT_V7
POSITION_SCALE = 2048.0
VELOCITY_SCALE = 32.0
DISTANCE_SCALE = 2048.0
TICK_SCALE = 300.0
MOVEMENT_SCALE = 512.0
PHYSICS_SCALE = 1024.0
SIZE_SCALE = 96.0
POSITION_DELTA_SCALE = 16.0
AIR_JUMP_SCALE = 4.0
SHORT_TICK_SCALE = 60.0


@dataclass(frozen=True)
class DatasetBatch:
    features: np.ndarray
    move_targets: np.ndarray
    binary_targets: np.ndarray
    aim_targets: np.ndarray
    resolved_phase_labels: np.ndarray
    requested_phase_labels: np.ndarray
    team_labels: np.ndarray
    class_labels: np.ndarray
    map_labels: np.ndarray
    capture_kind_labels: np.ndarray
    corrected_flags: np.ndarray
    sample_count: int
    file_count: int
    phase_counts: dict[str, int] = field(default_factory=dict)
    class_counts: dict[str, int] = field(default_factory=dict)
    team_counts: dict[str, int] = field(default_factory=dict)
    map_counts: dict[str, int] = field(default_factory=dict)
    capture_kind_counts: dict[str, int] = field(default_factory=dict)
    corrected_count: int = 0


@dataclass(frozen=True)
class DatasetFilter:
    resolved_phases: frozenset[str] = frozenset()
    requested_phases: frozenset[str] = frozenset()
    class_ids: frozenset[str] = frozenset()
    teams: frozenset[str] = frozenset()
    map_names: frozenset[str] = frozenset()
    capture_kinds: frozenset[str] = frozenset()
    corrected_only: bool = False
    success_only: bool = False
    carrying_intel: bool | None = None
    corrected_upweight: int = 1

    @property
    def is_active(self) -> bool:
        return any(
            (
                self.resolved_phases,
                self.requested_phases,
                self.class_ids,
                self.teams,
                self.map_names,
                self.capture_kinds,
                self.corrected_only,
                self.success_only,
                self.carrying_intel is not None,
                self.corrected_upweight > 1,
            )
        )


def _clamp(value: float, low: float, high: float) -> float:
    return max(low, min(high, value))


def _normalize_signed(value: float, scale: float) -> float:
    return _clamp(value / scale, -1.0, 1.0)


def _normalize_01(value: float, max_value: float) -> float:
    if max_value <= 0:
        return 0.0
    return _clamp(value / max_value, 0.0, 1.0)


def _normalize_control_owner(value: int) -> float:
    if value == 1:
        return -1.0
    if value == 2:
        return 1.0
    return 0.0


def _class_profile_one_hot(class_name: str) -> list[float]:
    light = class_name in {"Scout", "Sniper", "Spy"}
    heavy = class_name == "Heavy"
    standard = class_name in {"Engineer", "Pyro", "Soldier", "Demoman", "Medic", "Quote"}
    return [1.0 if light else 0.0, 1.0 if heavy else 0.0, 1.0 if standard else 0.0]


def _traversal_link_kind_one_hot(link_kind: str) -> list[float]:
    return [
        1.0 if link_kind == "Walk" else 0.0,
        1.0 if link_kind == "JumpUp" else 0.0,
        1.0 if link_kind == "JumpAcross" else 0.0,
        1.0 if link_kind == "DropDown" else 0.0,
        1.0 if link_kind == "Climb" else 0.0,
        1.0 if link_kind == "FallRecover" else 0.0,
        1.0 if link_kind == "Gate" else 0.0,
        1.0 if link_kind == "UnknownCandidate" else 0.0,
    ]


def _mode_one_hot(mode: str) -> list[float]:
    return [
        1.0 if mode == "CaptureTheFlag" else 0.0,
        1.0 if mode == "Arena" else 0.0,
        1.0 if mode == "ControlPoint" else 0.0,
        1.0 if mode == "KingOfTheHill" else 0.0,
        1.0 if mode == "DoubleKingOfTheHill" else 0.0,
        1.0 if mode == "Generator" else 0.0,
        1.0 if mode == "TeamDeathmatch" else 0.0,
    ]


def _sign(value: float) -> float:
    if value > 0.0:
        return 1.0
    if value < 0.0:
        return -1.0
    return 0.0


def _is_encoded_friendly(value: int, team: str) -> bool:
    encoded_team = 1 if team == "Red" else 2
    return value == encoded_team


def _is_encoded_enemy(value: int, team: str) -> bool:
    encoded_team = 1 if team == "Red" else 2
    return value != 0 and value != encoded_team


def vectorize_observation(observation: dict) -> np.ndarray:
    objective = observation.get("Objective", {})
    control_point = observation.get("ControlPointObjective", {})
    probes = observation.get("Probes", {})
    enemy = observation.get("NearestVisibleEnemy", {})
    teammate = observation.get("NearestVisibleTeammate", {})
    task_phase = observation.get("TaskPhase", "None")

    features: list[float] = []
    features.append(_normalize_signed(float(observation.get("BotX", 0.0)), POSITION_SCALE))
    features.append(_normalize_signed(float(observation.get("BotY", 0.0)), POSITION_SCALE))
    features.append(_normalize_signed(float(observation.get("VelocityX", 0.0)), VELOCITY_SCALE))
    features.append(_normalize_signed(float(observation.get("VelocityY", 0.0)), VELOCITY_SCALE))
    features.append(1.0 if observation.get("IsGrounded", False) else 0.0)
    features.append(1.0 if float(observation.get("FacingDirectionX", 1.0)) >= 0.0 else -1.0)
    features.append(_normalize_01(float(observation.get("Health", 0.0)), max(1.0, float(observation.get("MaxHealth", 1.0)))))
    features.append(1.0 if observation.get("IsCarryingIntel", False) else 0.0)

    for phase_name in ("AttackIntel", "ReturnIntel", "CaptureObjective", "DefendObjective"):
        features.append(1.0 if task_phase == phase_name else 0.0)

    features.extend(_class_profile_one_hot(str(observation.get("ClassId", "Scout"))))

    features.append(1.0 if objective.get("HasObjective", False) else 0.0)
    features.append(_normalize_signed(float(objective.get("RelativeX", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(objective.get("RelativeY", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(objective.get("HomeRelativeX", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(objective.get("HomeRelativeY", 0.0)), DISTANCE_SCALE))

    features.append(_normalize_01(float(probes.get("ForwardFootObstacleDistance", 0.0)), 64.0))
    features.append(_normalize_01(float(probes.get("ForwardHeadObstacleDistance", 0.0)), 64.0))
    features.append(_normalize_01(float(probes.get("GroundAheadDistance", 0.0)), 64.0))
    features.append(_normalize_01(float(probes.get("DropAheadDepth", 0.0)), 96.0))
    features.append(_normalize_01(float(probes.get("CeilingDistance", 0.0)), 64.0))
    features.append(_normalize_01(float(probes.get("LedgeHeightAhead", 0.0)), 96.0))
    features.append(1.0 if probes.get("BlockingGateAhead", False) else 0.0)

    features.append(1.0 if enemy.get("Exists", False) else 0.0)
    features.append(_normalize_signed(float(enemy.get("RelativeX", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(enemy.get("RelativeY", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_01(float(enemy.get("Distance", 0.0)), DISTANCE_SCALE))
    features.append(1.0 if enemy.get("HasLineOfSight", False) else 0.0)

    features.append(1.0 if teammate.get("Exists", False) else 0.0)
    features.append(_normalize_signed(float(teammate.get("RelativeX", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(teammate.get("RelativeY", 0.0)), DISTANCE_SCALE))

    features.append(_normalize_01(float(observation.get("StuckTicks", 0.0)), TICK_SCALE))
    features.append(_normalize_01(float(observation.get("ObjectiveDistance", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(observation.get("ObjectiveDistanceDelta", 0.0)), 128.0))
    features.append(_normalize_control_owner(int(observation.get("ControlPointOwner", 0))))
    features.append(_normalize_control_owner(int(observation.get("ControlPointCappingTeam", 0))))
    features.append(_clamp(float(observation.get("ControlPointCaptureProgress", 0.0)), 0.0, 1.0))
    features.append(1.0 if observation.get("ControlPointLocked", False) else 0.0)
    features.append(1.0 if observation.get("IsRespawning", False) else 0.0)
    features.append(_normalize_01(float(observation.get("RemainingAirJumps", 0.0)), max(1.0, float(observation.get("MaxAirJumps", 1.0)))))
    features.append(_normalize_01(float(observation.get("MaxAirJumps", 0.0)), AIR_JUMP_SCALE))
    features.append(_normalize_signed(float(observation.get("RunPower", 0.0)), 12.0))
    features.append(_normalize_signed(float(observation.get("MaxRunSpeed", 0.0)), MOVEMENT_SCALE))
    features.append(_normalize_signed(float(observation.get("GroundAcceleration", 0.0)), PHYSICS_SCALE))
    features.append(_normalize_signed(float(observation.get("GroundDeceleration", 0.0)), PHYSICS_SCALE))
    features.append(_normalize_signed(float(observation.get("Gravity", 0.0)), PHYSICS_SCALE))
    features.append(_normalize_signed(float(observation.get("JumpSpeed", 0.0)), MOVEMENT_SCALE))
    features.append(_normalize_01(float(observation.get("Width", 0.0)), 64.0))
    features.append(_normalize_01(float(observation.get("Height", 0.0)), SIZE_SCALE))
    features.append(_normalize_signed(float(observation.get("PreviousVelocityX", 0.0)), MOVEMENT_SCALE))
    features.append(_normalize_signed(float(observation.get("PreviousVelocityY", 0.0)), MOVEMENT_SCALE))
    features.append(_normalize_signed(float(observation.get("PreviousPositionDeltaX", 0.0)), POSITION_DELTA_SCALE))
    features.append(_normalize_signed(float(observation.get("PreviousPositionDeltaY", 0.0)), POSITION_DELTA_SCALE))
    features.append(_normalize_signed(float(observation.get("PreviousObjectiveDistanceDelta", 0.0)), 128.0))
    features.append(1.0 if float(observation.get("PreviousFacingDirectionX", 1.0)) >= 0.0 else -1.0)
    features.append(1.0 if observation.get("PreviousIsGrounded", False) else 0.0)
    features.append(_normalize_01(float(observation.get("AirborneTicks", 0.0)), SHORT_TICK_SCALE))
    features.append(_normalize_01(float(observation.get("JumpTicks", 0.0)), SHORT_TICK_SCALE))
    features.append(_normalize_01(float(probes.get("LeftFootObstacleDistance", 64.0)), 64.0))
    features.append(_normalize_01(float(probes.get("LeftHeadObstacleDistance", 64.0)), 64.0))
    features.append(_normalize_01(float(probes.get("RightFootObstacleDistance", 64.0)), 64.0))
    features.append(_normalize_01(float(probes.get("RightHeadObstacleDistance", 64.0)), 64.0))
    features.append(_normalize_01(float(probes.get("LeftGroundDistance", 64.0)), 64.0))
    features.append(_normalize_01(float(probes.get("RightGroundDistance", 64.0)), 64.0))
    features.append(_normalize_01(float(probes.get("LeftDropDepth", 96.0)), 96.0))
    features.append(_normalize_01(float(probes.get("RightDropDepth", 96.0)), 96.0))
    features.append(1.0 if probes.get("TouchingLeftWall", False) else 0.0)
    features.append(1.0 if probes.get("TouchingRightWall", False) else 0.0)
    features.append(1.0 if probes.get("TouchingCeiling", False) else 0.0)

    features.append(_normalize_signed(float(observation.get("PreviousMoveInput", 0.0)), 1.0))
    features.append(1.0 if observation.get("PreviousJumpPressed", False) else 0.0)
    features.append(1.0 if observation.get("PreviousJumpHeld", False) else 0.0)
    features.append(1.0 if observation.get("PreviousDropInput", False) else 0.0)
    features.append(1.0 if observation.get("PreviousActionFirePrimary", False) else 0.0)
    features.append(1.0 if observation.get("PreviousActionFireSecondary", False) else 0.0)
    features.append(1.0 if observation.get("PreviousActionDropIntel", False) else 0.0)
    features.append(_normalize_01(float(observation.get("FramesSinceJumpPressed", SHORT_TICK_SCALE)), SHORT_TICK_SCALE))
    features.append(_normalize_01(float(observation.get("FramesSinceJumpReleased", SHORT_TICK_SCALE)), SHORT_TICK_SCALE))
    features.append(1.0 if control_point.get("HasObjective", False) else 0.0)
    features.append(_normalize_01(float(control_point.get("Index", 0.0)), 8.0))
    features.append(_normalize_control_owner(int(control_point.get("Owner", 0))))
    features.append(_normalize_control_owner(int(control_point.get("CappingTeam", 0))))
    features.append(_clamp(float(control_point.get("CaptureProgress", 0.0)), 0.0, 1.0))
    features.append(_normalize_signed(float(control_point.get("CaptureProgressDelta", 0.0)), 1.0))
    features.append(1.0 if control_point.get("IsLocked", False) else 0.0)
    features.append(_normalize_01(float(control_point.get("FriendlyCappers", 0.0)), 6.0))
    features.append(_normalize_01(float(control_point.get("EnemyCappers", 0.0)), 6.0))
    features.append(_normalize_01(float(control_point.get("TotalCappers", 0.0)), 8.0))
    features.append(1.0 if control_point.get("IsContested", False) else 0.0)
    features.append(1.0 if control_point.get("IsPlayerInCaptureZone", False) else 0.0)
    features.append(_normalize_01(float(control_point.get("TimeOnPointTicks", 0.0)), TICK_SCALE))
    features.append(_normalize_01(float(control_point.get("TimeSinceLeftPointTicks", 0.0)), TICK_SCALE))
    features.append(_normalize_01(float(control_point.get("FriendlyKothTimerTicksRemaining", 0.0)), 5400.0))
    features.append(_normalize_01(float(control_point.get("EnemyKothTimerTicksRemaining", 0.0)), 5400.0))
    features.append(_normalize_01(float(control_point.get("KothUnlockTicksRemaining", 0.0)), 900.0))
    features.append(1.0 if control_point.get("IsKothMode", False) else 0.0)
    features.append(1.0 if control_point.get("IsDoubleKothMode", False) else 0.0)

    team = str(observation.get("Team", "Red"))
    mode = str(observation.get("Mode", "CaptureTheFlag"))
    objective_relative_x = float(objective.get("RelativeX", 0.0))
    objective_relative_y = float(objective.get("RelativeY", 0.0))
    objective_direction_x = _sign(objective_relative_x) if objective.get("HasObjective", False) else 0.0
    objective_direction_y = _sign(objective_relative_y) if objective.get("HasObjective", False) else 0.0

    features.append(1.0 if team == "Red" else -1.0)
    features.extend(_mode_one_hot(mode))
    features.append(objective_direction_x)
    features.append(objective_direction_y)
    features.append(_normalize_01(abs(objective_relative_x), DISTANCE_SCALE))
    features.append(_normalize_01(abs(objective_relative_y), DISTANCE_SCALE))
    features.append(_normalize_signed(float(observation.get("VelocityX", 0.0)) * objective_direction_x, MOVEMENT_SCALE))
    features.append(_normalize_signed(float(observation.get("VelocityY", 0.0)) * objective_direction_y, MOVEMENT_SCALE))
    features.append(_normalize_signed(float(observation.get("PreviousPositionDeltaX", 0.0)) * objective_direction_x, POSITION_DELTA_SCALE))
    features.append(_normalize_signed(float(observation.get("PreviousPositionDeltaY", 0.0)) * objective_direction_y, POSITION_DELTA_SCALE))

    objective_side_is_right = objective_direction_x >= 0.0
    objective_side_foot = float(probes.get("RightFootObstacleDistance" if objective_side_is_right else "LeftFootObstacleDistance", 64.0))
    objective_side_head = float(probes.get("RightHeadObstacleDistance" if objective_side_is_right else "LeftHeadObstacleDistance", 64.0))
    objective_side_ground = float(probes.get("RightGroundDistance" if objective_side_is_right else "LeftGroundDistance", 64.0))
    objective_side_drop = float(probes.get("RightDropDepth" if objective_side_is_right else "LeftDropDepth", 96.0))
    opposite_side_foot = float(probes.get("LeftFootObstacleDistance" if objective_side_is_right else "RightFootObstacleDistance", 64.0))
    opposite_side_head = float(probes.get("LeftHeadObstacleDistance" if objective_side_is_right else "RightHeadObstacleDistance", 64.0))
    opposite_side_ground = float(probes.get("LeftGroundDistance" if objective_side_is_right else "RightGroundDistance", 64.0))
    opposite_side_drop = float(probes.get("LeftDropDepth" if objective_side_is_right else "RightDropDepth", 96.0))
    touching_wall_on_objective_side = bool(probes.get("TouchingRightWall" if objective_side_is_right else "TouchingLeftWall", False))
    touching_wall_opposite_objective = bool(probes.get("TouchingLeftWall" if objective_side_is_right else "TouchingRightWall", False))

    features.append(_normalize_01(objective_side_foot, 64.0))
    features.append(_normalize_01(objective_side_head, 64.0))
    features.append(_normalize_01(objective_side_ground, 64.0))
    features.append(_normalize_01(objective_side_drop, 96.0))
    features.append(_normalize_01(opposite_side_foot, 64.0))
    features.append(_normalize_01(opposite_side_head, 64.0))
    features.append(_normalize_01(opposite_side_ground, 64.0))
    features.append(_normalize_01(opposite_side_drop, 96.0))
    features.append(1.0 if touching_wall_on_objective_side else 0.0)
    features.append(1.0 if touching_wall_opposite_objective else 0.0)
    features.append(1.0 if objective_side_foot <= 8.0 else 0.0)
    features.append(1.0 if objective_side_head <= 8.0 else 0.0)

    objective_distance = float(observation.get("ObjectiveDistance", 0.0))
    features.append(1.0 if objective_relative_y < -12.0 else 0.0)
    features.append(1.0 if objective_relative_y > 12.0 else 0.0)
    features.append(1.0 if abs(objective_relative_x) <= 32.0 else 0.0)
    features.append(1.0 if abs(objective_relative_x) <= 96.0 else 0.0)
    features.append(1.0 if abs(objective_relative_y) <= 32.0 else 0.0)
    features.append(1.0 if abs(objective_relative_y) <= 96.0 else 0.0)
    features.append(1.0 if objective_distance <= 64.0 else 0.0)
    features.append(1.0 if objective_distance <= 128.0 else 0.0)
    features.append(1.0 if objective_distance <= 256.0 else 0.0)
    features.append(1.0 if objective_distance <= 512.0 else 0.0)

    owner = int(control_point.get("Owner", 0))
    capping_team = int(control_point.get("CappingTeam", 0))
    encoded_team = 1 if team == "Red" else 2
    features.append(1.0 if _is_encoded_friendly(owner, team) else 0.0)
    features.append(1.0 if _is_encoded_enemy(owner, team) else 0.0)
    features.append(1.0 if owner == 0 else 0.0)
    features.append(1.0 if _is_encoded_friendly(capping_team, team) else 0.0)
    features.append(1.0 if _is_encoded_enemy(capping_team, team) else 0.0)
    features.append(1.0 if capping_team == 0 else 0.0)
    features.append(1.0 if control_point.get("HasObjective", False) and (not control_point.get("IsLocked", False) or control_point.get("IsKothMode", False)) else 0.0)
    features.append(
        1.0
        if control_point.get("IsPlayerInCaptureZone", False)
        and int(control_point.get("FriendlyCappers", 0)) > 0
        and int(control_point.get("EnemyCappers", 0)) <= 0
        and (capping_team == encoded_team or owner == encoded_team)
        else 0.0
    )

    terrain = observation.get("TerrainAffordance", {})
    if not isinstance(terrain, dict):
        terrain = {}

    features.append(1.0 if terrain.get("HasLeftLanding", False) else 0.0)
    features.append(_normalize_signed(float(terrain.get("LeftLandingRelativeX", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(terrain.get("LeftLandingRelativeY", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(terrain.get("LeftLandingSurfaceDeltaY", 0.0)), 384.0))
    features.append(_normalize_signed(float(terrain.get("LeftLandingObjectiveDistanceDelta", 0.0)), 512.0))
    features.append(1.0 if terrain.get("LeftLandingIsHigher", False) else 0.0)
    features.append(1.0 if terrain.get("LeftLandingRequiresJump", False) else 0.0)

    features.append(1.0 if terrain.get("HasRightLanding", False) else 0.0)
    features.append(_normalize_signed(float(terrain.get("RightLandingRelativeX", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(terrain.get("RightLandingRelativeY", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(terrain.get("RightLandingSurfaceDeltaY", 0.0)), 384.0))
    features.append(_normalize_signed(float(terrain.get("RightLandingObjectiveDistanceDelta", 0.0)), 512.0))
    features.append(1.0 if terrain.get("RightLandingIsHigher", False) else 0.0)
    features.append(1.0 if terrain.get("RightLandingRequiresJump", False) else 0.0)

    features.append(1.0 if terrain.get("HasBestUpwardLanding", False) else 0.0)
    features.append(_normalize_signed(float(terrain.get("BestUpwardLandingRelativeX", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(terrain.get("BestUpwardLandingRelativeY", 0.0)), DISTANCE_SCALE))
    features.append(_normalize_signed(float(terrain.get("BestUpwardLandingSurfaceDeltaY", 0.0)), 384.0))
    features.append(_normalize_signed(float(terrain.get("BestUpwardLandingObjectiveDistanceDelta", 0.0)), 512.0))
    features.append(_clamp(float(terrain.get("BestUpwardLandingDirection", 0.0)), -1.0, 1.0))
    features.append(1.0 if terrain.get("BestUpwardLandingMovesAwayFromObjective", False) else 0.0)
    features.append(_normalize_01(float(terrain.get("BestUpwardLandingHorizontalGap", 0.0)), 384.0))
    features.append(_normalize_01(float(terrain.get("BestUpwardLandingHeadroom", 0.0)), 160.0))
    features.append(_normalize_01(float(terrain.get("CurrentSurfaceClearanceLeft", 0.0)), 384.0))
    features.append(_normalize_01(float(terrain.get("CurrentSurfaceClearanceRight", 0.0)), 384.0))

    array = np.asarray(features, dtype=np.float32)
    if array.shape[0] != FEATURE_COUNT:
        raise ValueError(f"feature count mismatch: expected {FEATURE_COUNT}, got {array.shape[0]}")
    return array


def encode_action(sample: dict) -> tuple[int, np.ndarray, np.ndarray]:
    action = sample["Action"]
    move_target = int(action["MoveDirection"]) + 1
    binary_target = np.asarray(
        [
            1.0 if action["Jump"] else 0.0,
            1.0 if action["Crouch"] else 0.0,
            1.0 if action["FirePrimary"] else 0.0,
            1.0 if action["FireSecondary"] else 0.0,
            1.0 if action["DropIntel"] else 0.0,
        ],
        dtype=np.float32,
    )
    observation = sample["Observation"]
    aim_target = np.asarray(
        [
            _normalize_signed(action["AimWorldX"] - observation["BotX"], DISTANCE_SCALE),
            _normalize_signed(action["AimWorldY"] - observation["BotY"], DISTANCE_SCALE),
        ],
        dtype=np.float32,
    )
    return move_target, binary_target, aim_target


def iter_demo_paths(data_root: Path) -> Iterable[Path]:
    yield from sorted(data_root.rglob("*.json"))


def parse_csv_filter(value: str | None) -> frozenset[str]:
    if not value:
        return frozenset()

    return frozenset(item.strip() for item in value.split(",") if item.strip())


def build_dataset_filter(
    resolved_phases: Sequence[str] | None = None,
    requested_phases: Sequence[str] | None = None,
    class_ids: Sequence[str] | None = None,
    teams: Sequence[str] | None = None,
    map_names: Sequence[str] | None = None,
    capture_kinds: Sequence[str] | None = None,
    corrected_only: bool = False,
    success_only: bool = False,
    carrying_intel: bool | None = None,
    corrected_upweight: int = 1,
) -> DatasetFilter:
    return DatasetFilter(
        resolved_phases=frozenset(resolved_phases or ()),
        requested_phases=frozenset(requested_phases or ()),
        class_ids=frozenset(class_ids or ()),
        teams=frozenset(teams or ()),
        map_names=frozenset(map_names or ()),
        capture_kinds=frozenset(capture_kinds or ()),
        corrected_only=corrected_only,
        success_only=success_only,
        carrying_intel=carrying_intel,
        corrected_upweight=max(1, corrected_upweight),
    )


def _matches_filter(document: dict, sample: dict, dataset_filter: DatasetFilter) -> bool:
    if not dataset_filter.is_active:
        return True

    metadata = document.get("Metadata", {})
    if dataset_filter.requested_phases and metadata.get("RequestedPhase") not in dataset_filter.requested_phases:
        return False
    if dataset_filter.class_ids and metadata.get("ClassId") not in dataset_filter.class_ids:
        return False
    if dataset_filter.teams and metadata.get("Team") not in dataset_filter.teams:
        return False
    if dataset_filter.map_names and metadata.get("LevelName") not in dataset_filter.map_names:
        return False
    capture_kind = metadata.get("CaptureKind", "Demonstration")
    if dataset_filter.capture_kinds and capture_kind not in dataset_filter.capture_kinds:
        return False
    if dataset_filter.success_only and not bool(metadata.get("Success")):
        return False
    if dataset_filter.resolved_phases and sample.get("ResolvedPhase") not in dataset_filter.resolved_phases:
        return False
    if dataset_filter.corrected_only and not bool(sample.get("UsedHumanOverride")):
        return False
    if dataset_filter.carrying_intel is not None:
        observation = sample.get("Observation", {})
        if bool(observation.get("IsCarryingIntel", False)) != dataset_filter.carrying_intel:
            return False
    return True


def _increment_counter(counter: dict[str, int], key: str) -> None:
    counter[key] = counter.get(key, 0) + 1


def load_behavior_cloning_dataset(
    data_root: Path,
    dataset_filter: DatasetFilter | None = None,
    *,
    retarget_class_id: str | None = None,
) -> DatasetBatch:
    dataset_filter = dataset_filter or DatasetFilter()
    features: list[np.ndarray] = []
    move_targets: list[int] = []
    binary_targets: list[np.ndarray] = []
    aim_targets: list[np.ndarray] = []
    resolved_phase_labels: list[str] = []
    requested_phase_labels: list[str] = []
    team_labels: list[str] = []
    class_labels: list[str] = []
    map_labels: list[str] = []
    capture_kind_labels: list[str] = []
    corrected_flags: list[bool] = []
    included_files: set[Path] = set()
    phase_counts: dict[str, int] = {}
    class_counts: dict[str, int] = {}
    team_counts: dict[str, int] = {}
    map_counts: dict[str, int] = {}
    capture_kind_counts: dict[str, int] = {}
    corrected_count = 0

    for path in iter_demo_paths(data_root):
        with path.open("r", encoding="utf-8-sig") as handle:
            document = json.load(handle)

        if not isinstance(document, dict):
            continue

        samples = document.get("Samples", [])
        for sample in samples:
            if not _matches_filter(document, sample, dataset_filter):
                continue

            repeat_count = dataset_filter.corrected_upweight if bool(sample.get("UsedHumanOverride")) else 1
            observation = sample["Observation"]
            if retarget_class_id:
                observation = dict(observation)
                observation["ClassId"] = retarget_class_id

            feature_vector = vectorize_observation(observation)
            move_target, binary_target, aim_target = encode_action(sample)
            metadata = document.get("Metadata", {})
            resolved_phase = sample.get("ResolvedPhase", "Unknown")
            requested_phase = metadata.get("RequestedPhase", "Unknown")
            class_id = metadata.get("ClassId", "Unknown")
            team = metadata.get("Team", "Unknown")
            map_name = metadata.get("LevelName", "Unknown")
            capture_kind = metadata.get("CaptureKind", "Demonstration")
            corrected = bool(sample.get("UsedHumanOverride"))
            for _ in range(repeat_count):
                features.append(feature_vector.copy())
                move_targets.append(move_target)
                binary_targets.append(binary_target.copy())
                aim_targets.append(aim_target.copy())
                resolved_phase_labels.append(resolved_phase)
                requested_phase_labels.append(requested_phase)
                team_labels.append(team)
                class_labels.append(class_id)
                map_labels.append(map_name)
                capture_kind_labels.append(capture_kind)
                corrected_flags.append(corrected)
                included_files.add(path)

                _increment_counter(phase_counts, resolved_phase)
                _increment_counter(class_counts, class_id)
                _increment_counter(team_counts, team)
                _increment_counter(map_counts, map_name)
                _increment_counter(capture_kind_counts, capture_kind)
                corrected_count += 1 if corrected else 0

    if not features:
        raise ValueError(f"no demonstration samples found under {data_root}")

    return DatasetBatch(
        features=np.stack(features).astype(np.float32),
        move_targets=np.asarray(move_targets, dtype=np.int64),
        binary_targets=np.stack(binary_targets).astype(np.float32),
        aim_targets=np.stack(aim_targets).astype(np.float32),
        resolved_phase_labels=np.asarray(resolved_phase_labels, dtype=np.str_),
        requested_phase_labels=np.asarray(requested_phase_labels, dtype=np.str_),
        team_labels=np.asarray(team_labels, dtype=np.str_),
        class_labels=np.asarray(class_labels, dtype=np.str_),
        map_labels=np.asarray(map_labels, dtype=np.str_),
        capture_kind_labels=np.asarray(capture_kind_labels, dtype=np.str_),
        corrected_flags=np.asarray(corrected_flags, dtype=np.bool_),
        sample_count=len(features),
        file_count=len(included_files),
        phase_counts=phase_counts,
        class_counts=class_counts,
        team_counts=team_counts,
        map_counts=map_counts,
        capture_kind_counts=capture_kind_counts,
        corrected_count=corrected_count,
    )
