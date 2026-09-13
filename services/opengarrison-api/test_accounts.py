import os
import tempfile
import unittest

from fastapi.testclient import TestClient

import app as opengarrison_api


class AccountPersistenceTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary_directory = tempfile.TemporaryDirectory()
        os.environ["OPENGARRISON_API_DB"] = os.path.join(
            self._temporary_directory.name,
            "opengarrison-test.db",
        )
        self.client = TestClient(opengarrison_api.app)
        self.client.__enter__()

    def tearDown(self) -> None:
        self.client.__exit__(None, None, None)
        self._temporary_directory.cleanup()

    def test_short_codes_and_recovery_keys_normalize_without_ambiguous_characters(self) -> None:
        self.assertEqual("OG2-ABCD-EFGH", opengarrison_api.normalize_friend_code("og2 abcdefgh"))
        self.assertEqual("ABCD2345", opengarrison_api.normalize_recovery_key("abcd-2345"))
        self.assertEqual("", opengarrison_api.normalize_recovery_key("ABCD-0EFG"))
        self.assertEqual("", opengarrison_api.normalize_recovery_key("ABC"))

    def test_protect_and_login_links_a_second_device_to_the_same_account(self) -> None:
        first = self._register("first-device", "first-secret", "OG2-ABCD-EFGH")
        protected = self._protect("first-device", "first-secret", first["friendCode"])

        self.assertTrue(protected["isProtected"])
        self.assertRegex(protected["recoveryKey"], r"^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$")

        login = self.client.post(
            "/api/account/login",
            json={
                "clientId": "second-device",
                "clientSecret": "second-secret",
                "friendCode": first["friendCode"],
                "recoveryKey": protected["recoveryKey"],
            },
        )
        self.assertEqual(200, login.status_code, login.text)
        self.assertEqual(protected["accountId"], login.json()["accountId"])

        second_profile = self._profile("second-device", "second-secret", first["friendCode"])
        self.assertEqual(protected["accountId"], second_profile["accountId"])
        self.assertEqual(0, second_profile["lifetimePoints"])
        self.assertEqual(0, second_profile["walletBalance"])

        with opengarrison_api.connect_db() as db:
            devices = db.execute(
                """
                SELECT client_id, display_name, player_card_json
                FROM client_devices WHERE account_id = ? ORDER BY client_id
                """,
                (protected["accountId"],),
            ).fetchall()
        self.assertEqual(["first-device", "second-device"], [row["client_id"] for row in devices])
        self.assertEqual("Test Player", devices[1]["display_name"])
        self.assertEqual('{"background":"default"}', devices[1]["player_card_json"])

    def test_login_rehomes_a_guest_device_and_removes_its_empty_implicit_account(self) -> None:
        owner = self._register("owner-device", "owner-secret", "OG2-ABCD-EFGH")
        protected = self._protect("owner-device", "owner-secret", owner["friendCode"])
        guest = self._register("guest-device", "guest-secret", "OG2-MNPQ-RSTU")
        with opengarrison_api.connect_db() as db:
            guest_account_id = opengarrison_api.get_account_id_for_friend_code(db, guest["friendCode"])

        login = self._login(
            "guest-device",
            "guest-secret",
            owner["friendCode"],
            protected["recoveryKey"],
        )
        self.assertEqual(200, login.status_code, login.text)
        self.assertEqual(protected["accountId"], login.json()["accountId"])

        with opengarrison_api.connect_db() as db:
            orphan = db.execute(
                "SELECT 1 FROM accounts WHERE account_id = ?",
                (guest_account_id,),
            ).fetchone()
            guest_alias = opengarrison_api.get_account_id_for_friend_code(db, guest["friendCode"])
        self.assertIsNone(orphan)
        self.assertEqual("", guest_alias)

    def test_unlinked_device_cannot_claim_an_existing_friend_code(self) -> None:
        self._register("owner-device", "owner-secret", "OG2-ABCD-EFGH")
        conflict = self.client.post(
            "/api/client/register",
            json={
                "clientId": "attacker-device",
                "clientSecret": "attacker-secret",
                "friendCode": "OG2-ABCD-EFGH",
            },
        )
        self.assertEqual(409, conflict.status_code)

    def test_regenerating_recovery_key_invalidates_the_previous_key(self) -> None:
        registered = self._register("owner-device", "owner-secret", "OG2-ABCD-EFGH")
        first = self._protect("owner-device", "owner-secret", registered["friendCode"])
        second = self._protect("owner-device", "owner-secret", registered["friendCode"])
        self.assertNotEqual(first["recoveryKey"], second["recoveryKey"])

        old_login = self._login("old-key-device", "old-key-secret", registered["friendCode"], first["recoveryKey"])
        self.assertEqual(403, old_login.status_code)
        new_login = self._login("new-key-device", "new-key-secret", registered["friendCode"], second["recoveryKey"])
        self.assertEqual(200, new_login.status_code, new_login.text)

    def test_shortening_keeps_legacy_code_as_a_working_alias(self) -> None:
        legacy_code = "OG2-ABCD-EFGH-JKLM"
        self._register("legacy-device", "legacy-secret", legacy_code)
        heartbeat = self.client.post(
            "/api/presence/heartbeat",
            json={
                "clientId": "legacy-device",
                "clientSecret": "legacy-secret",
                "friendCode": legacy_code,
                "displayName": "Legacy Player",
                "status": "menu",
            },
        )
        self.assertEqual(200, heartbeat.status_code, heartbeat.text)

        shortened = self.client.post(
            "/api/account/friend-code/shorten",
            json=self._auth("legacy-device", "legacy-secret", legacy_code),
        )
        self.assertEqual(200, shortened.status_code, shortened.text)
        short_code = shortened.json()["friendCode"]
        self.assertRegex(short_code, r"^OG2-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$")

        old_alias_profile = self._profile("legacy-device", "legacy-secret", legacy_code)
        self.assertEqual(short_code, old_alias_profile["friendCode"])

        presence = self.client.get(f"/api/presence?codes={short_code}")
        self.assertEqual(200, presence.status_code, presence.text)
        self.assertTrue(presence.json()["friends"][0]["online"])
        self.assertEqual("Legacy Player", presence.json()["friends"][0]["displayName"])

    def test_friend_request_auto_accepts_after_requester_shortens_their_code(self) -> None:
        requester_legacy_code = "OG2-ABCD-EFGH-JKLM"
        requester = self._register("requester-device", "requester-secret", requester_legacy_code)
        recipient = self._register("recipient-device", "recipient-secret", "OG2-MNPQ-RSTU")

        pending = self.client.post(
            "/api/friends/request",
            json={
                **self._auth("requester-device", "requester-secret", requester["friendCode"]),
                "targetFriendCode": recipient["friendCode"],
            },
        )
        self.assertEqual(200, pending.status_code, pending.text)
        self.assertEqual("pending", pending.json()["status"])

        shortened = self.client.post(
            "/api/account/friend-code/shorten",
            json=self._auth("requester-device", "requester-secret", requester["friendCode"]),
        )
        self.assertEqual(200, shortened.status_code, shortened.text)

        accepted = self.client.post(
            "/api/friends/request",
            json={
                **self._auth("recipient-device", "recipient-secret", recipient["friendCode"]),
                "targetFriendCode": shortened.json()["friendCode"],
            },
        )
        self.assertEqual(200, accepted.status_code, accepted.text)
        self.assertEqual("accepted", accepted.json()["status"])
        self.assertEqual(shortened.json()["friendCode"], accepted.json()["friendCode"])

    def test_legacy_client_rows_migrate_idempotently(self) -> None:
        with opengarrison_api.connect_db() as db:
            current = opengarrison_api.now_seconds()
            db.execute(
                """
                INSERT INTO clients (
                    client_id, friend_code, secret_hash, display_name, player_card_json,
                    created_at, updated_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?)
                """,
                (
                    "pre-account-device",
                    "OG2-MNPQ-RSTU-VWXY",
                    opengarrison_api.secret_hash("pre-account-secret"),
                    "Migrated Player",
                    '{"background":"legacy"}',
                    current,
                    current,
                ),
            )

        opengarrison_api.initialize_db()
        opengarrison_api.initialize_db()
        profile = self._profile(
            "pre-account-device",
            "pre-account-secret",
            "OG2-MNPQ-RSTU-VWXY",
        )
        self.assertEqual("Migrated Player", profile["displayName"])
        self.assertEqual('{"background":"legacy"}', profile["playerCard"])

    def test_recovery_login_is_rate_limited_per_account_and_address(self) -> None:
        registered = self._register("rate-owner", "rate-secret", "OG2-ABCD-EFGH")
        self._protect("rate-owner", "rate-secret", registered["friendCode"])

        for attempt in range(opengarrison_api.LOGIN_FAILURE_LIMIT):
            response = self._login(
                f"bad-device-{attempt}",
                f"bad-secret-{attempt}",
                registered["friendCode"],
                "WXYZ-2345",
            )
            self.assertEqual(403, response.status_code)

        blocked = self._login("blocked-device", "blocked-secret", registered["friendCode"], "WXYZ-2345")
        self.assertEqual(429, blocked.status_code)

    def test_gameplay_session_awards_are_idempotent_and_ledger_repairs_cached_balance(self) -> None:
        registered = self._register("stats-device", "stats-secret", "OG2-ABCD-EFGH")
        session = self.client.post(
            "/api/game-session/create",
            json=self._auth("stats-device", "stats-secret", registered["friendCode"]),
        )
        self.assertEqual(200, session.status_code, session.text)
        token = session.json()["gameplayToken"]

        validated = self.client.post(
            "/api/game-session/validate",
            json={"gameplayToken": token},
        )
        self.assertEqual(200, validated.status_code, validated.text)

        award_payload = {
            "gameplayToken": token,
            "eventId": "match-1:42:kill:stats-device",
            "matchId": "match-1",
            "sourceFrame": 42,
            "eventType": "kill",
            "rawValue": 1,
            "pointsDelta": 100,
            "creditsDelta": 5,
            "policyVersion": 1,
        }
        awarded = self.client.post("/api/stats/award", json=award_payload)
        self.assertEqual(200, awarded.status_code, awarded.text)
        self.assertTrue(awarded.json()["applied"])
        self.assertEqual(100, awarded.json()["profile"]["lifetimePoints"])
        self.assertEqual(5, awarded.json()["profile"]["walletBalance"])

        duplicate = self.client.post("/api/stats/award", json=award_payload)
        self.assertEqual(200, duplicate.status_code, duplicate.text)
        self.assertFalse(duplicate.json()["applied"])
        self.assertEqual(100, duplicate.json()["profile"]["lifetimePoints"])
        self.assertEqual(5, duplicate.json()["profile"]["walletBalance"])

        conflict_payload = dict(award_payload)
        conflict_payload["creditsDelta"] = 6
        conflict = self.client.post("/api/stats/award", json=conflict_payload)
        self.assertEqual(409, conflict.status_code)

        account_id = awarded.json()["profile"]["accountId"]
        with opengarrison_api.connect_db() as db:
            db.execute(
                "UPDATE accounts SET lifetime_points = 999999, wallet_balance = 999999 WHERE account_id = ?",
                (account_id,),
            )

        repaired = self._profile("stats-device", "stats-secret", registered["friendCode"])
        self.assertEqual(100, repaired["lifetimePoints"])
        self.assertEqual(5, repaired["walletBalance"])

    def test_expired_gameplay_session_is_rejected(self) -> None:
        registered = self._register("expired-device", "expired-secret", "OG2-ABCD-EFGH")
        session = self.client.post(
            "/api/game-session/create",
            json=self._auth("expired-device", "expired-secret", registered["friendCode"]),
        )
        token = session.json()["gameplayToken"]
        with opengarrison_api.connect_db() as db:
            db.execute(
                "UPDATE gameplay_sessions SET expires_at = 0 WHERE token_hash = ?",
                (opengarrison_api.secret_hash(token),),
            )

        response = self.client.post(
            "/api/game-session/validate",
            json={"gameplayToken": token},
        )
        self.assertEqual(403, response.status_code)

    def test_points_lookup_and_leaderboard_are_ranked_paged_and_exclude_zero_scores(self) -> None:
        leader = self._register("leader-device", "leader-secret", "OG2-ABCD-EFGH")
        runner_up = self._register("runner-device", "runner-secret", "OG2-MNPQ-RSTU")
        self._register("zero-device", "zero-secret", "OG2-VWXY-2345")
        leader_token = self._create_gameplay_session("leader-device", "leader-secret", leader["friendCode"])
        runner_token = self._create_gameplay_session("runner-device", "runner-secret", runner_up["friendCode"])

        for token, event_id, raw_value, points in (
            (leader_token, "leader:kill", 3, 300),
            (runner_token, "runner:kill", 2, 200),
        ):
            awarded = self.client.post(
                "/api/stats/award",
                json={
                    "gameplayToken": token,
                    "eventId": event_id,
                    "matchId": "leaderboard-match",
                    "sourceFrame": 50,
                    "eventType": "kills",
                    "rawValue": raw_value,
                    "pointsDelta": points,
                    "creditsDelta": points,
                    "policyVersion": 1,
                },
            )
            self.assertEqual(200, awarded.status_code, awarded.text)

        own_points = self.client.post(
            "/api/stats/points",
            json={"gameplayToken": leader_token},
        )
        self.assertEqual(200, own_points.status_code, own_points.text)
        self.assertEqual(300, own_points.json()["profile"]["lifetimePoints"])
        self.assertEqual(1, own_points.json()["globalRank"])
        self.assertEqual(3, own_points.json()["stats"]["kills"])

        first_page = self.client.get("/api/stats/leaderboard?limit=1&offset=0")
        second_page = self.client.get("/api/stats/leaderboard?limit=1&offset=1")
        self.assertEqual(200, first_page.status_code, first_page.text)
        self.assertEqual(200, second_page.status_code, second_page.text)
        self.assertEqual(2, first_page.json()["total"])
        self.assertEqual(leader["friendCode"], first_page.json()["entries"][0]["friendCode"])
        self.assertEqual(1, first_page.json()["entries"][0]["rank"])
        self.assertEqual(runner_up["friendCode"], second_page.json()["entries"][0]["friendCode"])
        self.assertEqual(2, second_page.json()["entries"][0]["rank"])

    def test_last_to_die_runs_are_idempotent_and_rank_score_and_round_separately(self) -> None:
        score_leader = self._register("score-device", "score-secret", "OG2-ABCD-EFGH")
        round_leader = self._register("round-device", "round-secret", "OG2-MNPQ-RSTU")
        score_token = self._create_gameplay_session(
            "score-device", "score-secret", score_leader["friendCode"]
        )
        round_token = self._create_gameplay_session(
            "round-device", "round-secret", round_leader["friendCode"]
        )

        score_run = {
            "gameplayToken": score_token,
            "submissionId": "score-run:score-account",
            "runId": "score-run",
            "scoreUnits": 800,
            "roundNumber": 10,
            "difficulty": "standard",
            "policyVersion": 1,
        }
        first = self.client.post("/api/last-to-die/run", json=score_run)
        self.assertEqual(200, first.status_code, first.text)
        self.assertTrue(first.json()["applied"])
        duplicate = self.client.post("/api/last-to-die/run", json=score_run)
        self.assertEqual(200, duplicate.status_code, duplicate.text)
        self.assertFalse(duplicate.json()["applied"])

        conflicting = dict(score_run)
        conflicting["scoreUnits"] = 801
        conflict = self.client.post("/api/last-to-die/run", json=conflicting)
        self.assertEqual(409, conflict.status_code)

        round_run = {
            "gameplayToken": round_token,
            "submissionId": "round-run:round-account",
            "runId": "round-run",
            "scoreUnits": 650,
            "roundNumber": 14,
            "difficulty": "hardcore",
            "policyVersion": 1,
        }
        recorded_round = self.client.post("/api/last-to-die/run", json=round_run)
        self.assertEqual(200, recorded_round.status_code, recorded_round.text)

        rankings = self.client.post(
            "/api/last-to-die/rankings",
            json={
                **self._auth("score-device", "score-secret", score_leader["friendCode"]),
                "limit": 3,
            },
        )
        self.assertEqual(200, rankings.status_code, rankings.text)
        body = rankings.json()
        self.assertEqual(800, body["player"]["bestScoreUnits"])
        self.assertEqual(10, body["player"]["highestRound"])
        self.assertEqual(score_leader["friendCode"], body["scoreRecords"][0]["friendCode"])
        self.assertEqual(round_leader["friendCode"], body["roundRecords"][0]["friendCode"])

        public_rounds = self.client.get("/api/last-to-die/leaderboard?sort=round&limit=1")
        self.assertEqual(200, public_rounds.status_code, public_rounds.text)
        self.assertEqual(2, public_rounds.json()["total"])
        self.assertEqual(round_leader["friendCode"], public_rounds.json()["entries"][0]["friendCode"])

    def test_last_to_die_first_round_loss_keeps_score_and_zero_completed_rounds(self) -> None:
        player = self._register("first-round-device", "first-round-secret", "OG2-ZYXW-VUTS")
        token = self._create_gameplay_session(
            "first-round-device", "first-round-secret", player["friendCode"]
        )

        recorded = self.client.post(
            "/api/last-to-die/run",
            json={
                "gameplayToken": token,
                "submissionId": "first-round-loss:first-round-account",
                "runId": "first-round-loss",
                "scoreUnits": 350,
                "roundNumber": 0,
                "difficulty": "standard",
                "policyVersion": 1,
            },
        )

        self.assertEqual(200, recorded.status_code, recorded.text)
        self.assertEqual(350, recorded.json()["player"]["bestScoreUnits"])
        self.assertEqual(0, recorded.json()["player"]["highestRound"])
        self.assertEqual(1, recorded.json()["player"]["runsPlayed"])

    def _register(self, client_id: str, client_secret: str, friend_code: str) -> dict:
        response = self.client.post(
            "/api/client/register",
            json={
                "clientId": client_id,
                "clientSecret": client_secret,
                "friendCode": friend_code,
                "displayName": "Test Player",
                "playerCard": '{"background":"default"}',
            },
        )
        self.assertEqual(200, response.status_code, response.text)
        return response.json()

    def _protect(self, client_id: str, client_secret: str, friend_code: str) -> dict:
        response = self.client.post(
            "/api/account/protect",
            json=self._auth(client_id, client_secret, friend_code),
        )
        self.assertEqual(200, response.status_code, response.text)
        return response.json()

    def _profile(self, client_id: str, client_secret: str, friend_code: str) -> dict:
        response = self.client.post(
            "/api/account/profile",
            json=self._auth(client_id, client_secret, friend_code),
        )
        self.assertEqual(200, response.status_code, response.text)
        return response.json()

    def _create_gameplay_session(self, client_id: str, client_secret: str, friend_code: str) -> str:
        response = self.client.post(
            "/api/game-session/create",
            json=self._auth(client_id, client_secret, friend_code),
        )
        self.assertEqual(200, response.status_code, response.text)
        return response.json()["gameplayToken"]

    def _login(
        self,
        client_id: str,
        client_secret: str,
        friend_code: str,
        recovery_key: str,
    ):
        return self.client.post(
            "/api/account/login",
            json={
                "clientId": client_id,
                "clientSecret": client_secret,
                "friendCode": friend_code,
                "recoveryKey": recovery_key,
            },
        )

    @staticmethod
    def _auth(client_id: str, client_secret: str, friend_code: str) -> dict[str, str]:
        return {
            "clientId": client_id,
            "clientSecret": client_secret,
            "friendCode": friend_code,
        }


if __name__ == "__main__":
    unittest.main()
