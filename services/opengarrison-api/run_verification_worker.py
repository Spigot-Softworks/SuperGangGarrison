"""Run one installed game verifier per job; publish only its computed result."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
import subprocess
import tempfile
import time
import uuid
from pathlib import Path

import app
from run_verification import initialize, publish_participant

LEASE_SECONDS = 3700


def claim():
    now = int(time.time())
    with app.connect_db() as db:
        initialize(db)
        db.execute("BEGIN IMMEDIATE")
        db.execute("UPDATE run_verification_jobs SET status='pending', lease_id='' WHERE status='verifying' AND updated_at < ?",
                   (now - LEASE_SECONDS,))
        row = db.execute("SELECT * FROM run_verification_jobs WHERE status='pending' AND ruleset=? ORDER BY created_at LIMIT 1",
                         (os.environ.get("OPENGARRISON_REPLAY_RULESET", ""),)).fetchone()
        if row is None:
            return None
        lease = uuid.uuid4().hex
        db.execute("UPDATE run_verification_jobs SET status='verifying', lease_id=?, updated_at=?, attempts=attempts+1 WHERE id=?",
                   (lease, now, row["id"]))
        return dict(row) | {"lease_id": lease}


def publish(job, result):
    participants = result.get("Participants", [])
    actor = next((p for p in participants if str(p.get("ClientId", "")).replace("-", "").lower()
                  == job["client_id"].replace("-", "").lower()), None)
    if actor is None:
        raise ValueError("The submitting device did not participate in this run")
    score, rounds = actor.get("ScoreUnits"), result.get("CompletedRounds")
    difficulty, survivor = result.get("Difficulty"), actor.get("SurvivorId")
    if type(score) is not int or type(rounds) is not int or not 0 <= score <= 2147483647 or not 0 <= rounds <= 1000000:
        raise ValueError("The verifier returned an invalid result")
    if difficulty not in (0, 1) or survivor not in app.LAST_TO_DIE_SURVIVOR_IDS:
        raise ValueError("The verifier returned an invalid survivor or difficulty")
    attempt_id = str(uuid.UUID(result["AttemptId"]))
    result_json = json.dumps(result, sort_keys=True, separators=(",", ":"))
    with app.connect_db() as db:
        db.execute("BEGIN IMMEDIATE")
        active = db.execute("SELECT 1 FROM run_verification_jobs WHERE id=? AND status='verifying' AND lease_id=?",
                            (job["id"], job["lease_id"])).fetchone()
        if active is None:
            return
        existing = db.execute("SELECT result_json FROM verified_run_results WHERE attempt_id=?", (attempt_id,)).fetchone()
        if existing is not None and existing["result_json"] != result_json:
            raise ValueError("Run identity conflicts with a verified result")
        db.execute("INSERT OR IGNORE INTO verified_run_results VALUES (?, ?, ?)", (attempt_id, result_json, int(time.time())))
        publish_participant(db, job["account_id"], job["client_id"], result)
        db.execute("UPDATE run_verification_jobs SET status='verified', reason='', recording=NULL, updated_at=? WHERE id=?",
                   (int(time.time()), job["id"]))


def fail(job, status, reason):
    with app.connect_db() as db:
        db.execute("""UPDATE run_verification_jobs SET status=?, reason=?, updated_at=?,
            recording=CASE WHEN ?='rejected' THEN NULL ELSE recording END
            WHERE id=? AND lease_id=? AND status='verifying'""",
            (status, reason[:240], int(time.time()), status, job["id"], job["lease_id"]))


def process_one(executable: str) -> bool:
    job = claim()
    if job is None:
        return False
    try:
        if job["ruleset"] != os.environ.get("OPENGARRISON_REPLAY_RULESET", ""):
            fail(job, "pending", "Waiting for the matching game verifier")
            return False
        proof = bytes(job["recording"])
        if hashlib.sha256(proof).hexdigest() != job["digest"]:
            raise ValueError("Stored recording is corrupt")
        with tempfile.TemporaryDirectory(prefix="og2-verify-") as scratch:
            recording = Path(scratch) / "run.gz"
            recording.write_bytes(proof)
            # Pass only process/runtime settings, never service or account credentials.
            allowed = {"PATH", "SYSTEMROOT", "WINDIR", "COMSPEC", "TEMP", "TMP", "TMPDIR",
                       "LANG", "LC_ALL", "DOTNET_ROOT", "DOTNET_ROOT_X64"}
            environment = {key: value for key, value in os.environ.items() if key.upper() in allowed}
            environment["OPENGARRISON_USER_DATA_ROOT"] = str(Path(scratch) / "user-data")
            # Bound log storage: runtime diagnostics are not part of the result protocol.
            completed = subprocess.run([executable, "--verify", str(recording)],
                stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                env=environment, cwd=scratch, timeout=3600, check=False)
            if completed.returncode != 0:
                fail(job, "rejected", "The recording could not reproduce a completed run")
            else:
                publish(job, json.loads(completed.stdout))
    except (OSError, subprocess.TimeoutExpired):
        fail(job, "pending" if job["attempts"] < 2 else "failed", "Verification worker unavailable or timed out; recording retained")
    except (ValueError, TypeError, KeyError):
        fail(job, "rejected", "The recording or computed result was invalid")
    return True


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--verifier", required=True, help="Absolute path to the installed RunVerifier executable")
    parser.add_argument("--once", action="store_true")
    args = parser.parse_args()
    executable = str(Path(args.verifier).resolve(strict=True))
    supported = subprocess.check_output([executable, "--ruleset"], timeout=30, text=True).strip()
    if supported != os.environ.get("OPENGARRISON_REPLAY_RULESET", ""):
        raise SystemExit("Installed verifier does not match OPENGARRISON_REPLAY_RULESET")
    while True:
        worked = process_one(executable)
        if args.once:
            return
        if not worked:
            time.sleep(5)


if __name__ == "__main__":
    main()
