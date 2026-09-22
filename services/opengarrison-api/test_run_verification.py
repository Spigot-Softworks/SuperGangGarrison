import gzip
import json
import subprocess
import tempfile
import shutil
from pathlib import Path
import os
import unittest
import uuid
from unittest.mock import patch

import app
import run_verification
import run_verification_worker as worker
import test_accounts


class RunVerificationTests(unittest.TestCase):
    setUp = test_accounts.AccountPersistenceTests.setUp
    tearDown = test_accounts.AccountPersistenceTests.tearDown
    _register = test_accounts.AccountPersistenceTests._register
    _create_gameplay_session = test_accounts.AccountPersistenceTests._create_gameplay_session

    _auth = staticmethod(test_accounts.AccountPersistenceTests._auth)

    def identity(self, code="OG2-ABCD-EFGH"):
        client_id = str(uuid.uuid4())
        registered = self._register(client_id, "secret", code)
        token = self._create_gameplay_session(client_id, "secret", registered["friendCode"])
        return client_id, token

    def submit(self, token, proof=b"test run"):
        with patch.dict(os.environ, {"OPENGARRISON_REPLAY_RULESET": "test-ruleset"}):
            return self.client.post("/api/last-to-die/recordings", content=gzip.compress(proof, mtime=0),
                headers={"Authorization": "Bearer " + token, "X-Run-Ruleset": "test-ruleset"})

    def test_upload_is_authenticated_idempotent_and_does_not_award_a_score(self):
        _, token = self.identity()
        self.assertEqual(401, self.submit("").status_code)
        first = self.submit(token)
        self.assertEqual(202, first.status_code, first.text)
        self.assertEqual(first.json()["id"], self.submit(token).json()["id"])
        with app.connect_db() as db:
            self.assertEqual(0, db.execute("SELECT COUNT(*) FROM last_to_die_runs").fetchone()[0])
        _, other = self.identity("OG2-MNPQ-RSTU")
        response = self.client.get("/api/last-to-die/recordings/" + first.json()["id"],
                                   headers={"Authorization": "Bearer " + other})
        self.assertEqual(404, response.status_code)

    @unittest.skipUnless(os.environ.get("OG2_TEST_VERIFIER_PATH") and os.environ.get("OG2_TEST_RECORDING_PATH"),
                         "Requires a freshly recorded game and the standalone verifier")
    def test_real_recording_runs_through_worker_into_shared_ledger(self):
        installation = tempfile.TemporaryDirectory(prefix="og2-verifier-install-")
        self.addCleanup(installation.cleanup)
        source = Path(os.environ["OG2_TEST_VERIFIER_PATH"]).resolve()
        installed = Path(installation.name) / "app"
        shutil.copytree(source.parent, installed)
        executable = str(installed / source.name)
        proof = Path(os.environ["OG2_TEST_RECORDING_PATH"]).read_bytes()
        recording = json.loads(gzip.decompress(proof))
        expected = recording["ExpectedOutcome"]
        actor = expected["Participants"][0]
        client_id = actor["ClientId"]
        registered = self._register(client_id, "test-secret", "OG2-ABCD-EFGH")
        token = self._create_gameplay_session(client_id, "test-secret", registered["friendCode"])
        ruleset = subprocess.check_output([executable, "--ruleset"], text=True).strip()
        with patch.dict(os.environ, {"OPENGARRISON_REPLAY_RULESET": ruleset}):
            response = self.client.post("/api/last-to-die/recordings", content=proof,
                headers={"Authorization": "Bearer " + token, "X-Run-Ruleset": ruleset})
            self.assertEqual(202, response.status_code, response.text)
            self.assertTrue(worker.process_one(executable))
        status = self.client.get("/api/last-to-die/recordings/" + response.json()["id"],
                                 headers={"Authorization": "Bearer " + token})
        self.assertEqual("verified", status.json()["status"], status.text)
        with app.connect_db() as db:
            result = db.execute("SELECT score_units, round_number FROM last_to_die_runs").fetchone()
            self.assertEqual(actor["ScoreUnits"], result["score_units"])
            self.assertEqual(expected["CompletedRounds"], result["round_number"])

    def test_queue_limit_and_upload_limit_are_enforced(self):
        _, token = self.identity()
        with patch.object(run_verification, "MAX_PENDING_PER_ACCOUNT", 1):
            self.assertEqual(202, self.submit(token).status_code)
            self.assertEqual(429, self.submit(token, b"another run").status_code)
        with patch.object(run_verification, "MAX_UPLOAD_BYTES", 3):
            self.assertEqual(413, self.submit(token).status_code)

    def test_computed_results_share_the_leaderboard_and_guests_claim_only_their_own_result(self):
        owner, token = self.identity()
        guest, guest_token = self.identity("OG2-MNPQ-RSTU")
        _, stranger = self.identity("OG2-WXYZ-2345")
        self.assertEqual(202, self.submit(token).status_code)
        with patch.dict(os.environ, {"OPENGARRISON_REPLAY_RULESET": "test-ruleset"}):
            job = worker.claim()
        attempt = str(uuid.uuid4())
        result = {"AttemptId": attempt, "CompletedRounds": 3, "Difficulty": 0,
            "Participants": [
                {"Slot": 1, "ClientId": owner, "SurvivorId": "ltd.survivor.engineer", "ScoreUnits": 1250},
                {"Slot": 2, "ClientId": guest, "SurvivorId": "ltd.survivor.spy", "ScoreUnits": 750}]}
        worker.publish(job, result)
        worker.publish(job, result)  # stale completion cannot duplicate an award
        route = "/api/last-to-die/recordings/claims/" + attempt
        for _ in range(2):
            self.assertEqual(200, self.client.post(route, headers={"Authorization": "Bearer " + guest_token}).status_code)
        self.assertEqual(403, self.client.post(route, headers={"Authorization": "Bearer " + stranger}).status_code)
        with app.connect_db() as db:
            scores = db.execute("SELECT score_units FROM last_to_die_runs ORDER BY score_units DESC").fetchall()
            self.assertEqual([1250, 750], [row[0] for row in scores])
            self.assertIsNone(db.execute("SELECT recording FROM run_verification_jobs").fetchone()[0])

    def test_failed_replay_does_not_publish_and_stale_worker_cannot_complete(self):
        _, token = self.identity()
        self.submit(token)
        with patch.dict(os.environ, {"OPENGARRISON_REPLAY_RULESET": "test-ruleset"}):
            job = worker.claim()
        worker.fail(job, "rejected", "Invalid recording")
        with app.connect_db() as db:
            self.assertEqual(0, db.execute("SELECT COUNT(*) FROM last_to_die_runs").fetchone()[0])
            self.assertEqual("rejected", db.execute("SELECT status FROM run_verification_jobs").fetchone()[0])


if __name__ == "__main__":
    unittest.main()
