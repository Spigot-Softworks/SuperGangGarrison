"""Durable, authenticated run uploads. Only the local verifier can publish results."""
from __future__ import annotations

import hashlib
import json
import os
import sqlite3
import time
import uuid
from typing import Any

from fastapi import HTTPException, Request

MAX_UPLOAD_BYTES = 16 * 1024 * 1024
MAX_PENDING_PER_ACCOUNT = 8
MAX_PENDING_GLOBAL = 256


def initialize(db: sqlite3.Connection) -> None:
    db.execute("""
        CREATE TABLE IF NOT EXISTS run_verification_jobs (
            id TEXT PRIMARY KEY, account_id TEXT NOT NULL, client_id TEXT NOT NULL,
            ruleset TEXT NOT NULL, digest TEXT NOT NULL, recording BLOB,
            status TEXT NOT NULL DEFAULT 'pending', reason TEXT NOT NULL DEFAULT '',
            created_at INTEGER NOT NULL, updated_at INTEGER NOT NULL,
            lease_id TEXT NOT NULL DEFAULT '', attempts INTEGER NOT NULL DEFAULT 0,
            UNIQUE(account_id, digest)
        )
    """)
    db.execute("CREATE INDEX IF NOT EXISTS idx_run_verification_queue ON run_verification_jobs(status, created_at)")
    db.execute("""CREATE TABLE IF NOT EXISTS verified_run_results (
        attempt_id TEXT PRIMARY KEY, result_json TEXT NOT NULL, created_at INTEGER NOT NULL)""")


def publish_participant(db, account_id, client_id, result):
    actor = next((p for p in result["Participants"] if str(p["ClientId"]).replace("-", "").lower()
                  == client_id.replace("-", "").lower()), None)
    if actor is None:
        raise HTTPException(status_code=403, detail="This device did not participate in the verified run")
    attempt = str(uuid.UUID(result["AttemptId"]))
    db.execute("""INSERT OR IGNORE INTO last_to_die_runs
        (submission_id, run_id, account_id, score_units, round_number, difficulty, survivor_id, policy_version, created_at)
        VALUES (?, ?, ?, ?, ?, ?, ?, 1, ?)""",
        ("verified:" + attempt + ":" + account_id, "verified:" + attempt, account_id,
         actor["ScoreUnits"], result["CompletedRounds"], "standard" if result["Difficulty"] == 0 else "hardcore",
         actor["SurvivorId"], int(time.time())))


def install_routes(api: Any, connect_db: Any, validate_session: Any) -> None:
    def authenticate(request: Request) -> tuple[str, str]:
        authorization = request.headers.get("authorization", "")
        if not authorization.startswith("Bearer ") or len(authorization) > 300 or len(authorization) <= 7:
            raise HTTPException(status_code=401, detail="Gameplay session required")
        with connect_db() as db:
            session = validate_session(db, authorization[7:])
            return str(session["account_id"]), str(session["client_id"])

    @api.post("/api/last-to-die/recordings/claims/{attempt_id}")
    def claim_result(attempt_id: str, request: Request) -> dict[str, str]:
        account_id, client_id = authenticate(request)
        try:
            attempt_id = str(uuid.UUID(attempt_id))
        except ValueError:
            raise HTTPException(status_code=400, detail="Invalid run identity")
        with connect_db() as db:
            initialize(db)
            row = db.execute("SELECT result_json FROM verified_run_results WHERE attempt_id=?", (attempt_id,)).fetchone()
            if row is None:
                return {"id": attempt_id, "status": "pending", "reason": "Waiting for the host's run verification"}
            publish_participant(db, account_id, client_id, json.loads(row["result_json"]))
        return {"id": attempt_id, "status": "verified", "reason": ""}

    @api.post("/api/last-to-die/recordings", status_code=202)
    async def submit(request: Request) -> dict[str, str]:
        account_id, client_id = authenticate(request)
        ruleset = request.headers.get("x-run-ruleset", "")
        supported = os.environ.get("OPENGARRISON_REPLAY_RULESET", "")
        if not supported:
            raise HTTPException(status_code=503, detail="Run verification is not configured; keep the recording and retry later")
        accepted_rulesets = {supported, *os.environ.get("OPENGARRISON_REPLAY_RULESETS", "").split(",")}
        if ruleset not in accepted_rulesets:
            raise HTTPException(status_code=409, detail="This run requires a different verifier version")
        body = bytearray()
        async for chunk in request.stream():
            if len(body) + len(chunk) > MAX_UPLOAD_BYTES:
                raise HTTPException(status_code=413, detail="Run recording is too large")
            body.extend(chunk)
        if not body or body[:2] != b"\x1f\x8b":
            raise HTTPException(status_code=400, detail="A gzip run recording is required")
        digest = hashlib.sha256(body).hexdigest()
        now = int(time.time())
        with connect_db() as db:
            initialize(db)
            # Serialize admission so simultaneous requests cannot bypass queue limits.
            db.execute("BEGIN IMMEDIATE")
            existing = db.execute("SELECT id, status, reason FROM run_verification_jobs WHERE account_id=? AND digest=?",
                                  (account_id, digest)).fetchone()
            if existing is not None:
                return dict(existing)
            counts = db.execute("""SELECT COUNT(*) AS total,
                COALESCE(SUM(account_id = ?), 0) AS own FROM run_verification_jobs
                WHERE status IN ('pending', 'verifying')""", (account_id,)).fetchone()
            if counts["own"] >= MAX_PENDING_PER_ACCOUNT or counts["total"] >= MAX_PENDING_GLOBAL:
                raise HTTPException(status_code=429, detail="Verification queue is full; retry later")
            job_id = uuid.uuid4().hex
            db.execute("""INSERT INTO run_verification_jobs
                (id, account_id, client_id, ruleset, digest, recording, created_at, updated_at)
                VALUES (?, ?, ?, ?, ?, ?, ?, ?)""",
                (job_id, account_id, client_id, ruleset, digest, bytes(body), now, now))
        return {"id": job_id, "status": "pending", "reason": ""}

    @api.get("/api/last-to-die/recordings/{job_id}")
    def status(job_id: str, request: Request) -> dict[str, str]:
        account_id, _ = authenticate(request)
        with connect_db() as db:
            initialize(db)
            row = db.execute("SELECT id, status, reason FROM run_verification_jobs WHERE id=? AND account_id=?",
                             (job_id, account_id)).fetchone()
            if row is None:
                raise HTTPException(status_code=404, detail="Run recording was not found")
            return dict(row)
