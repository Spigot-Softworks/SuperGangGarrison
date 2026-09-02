import os
import tempfile
import unittest
from urllib.parse import urlsplit

from fastapi.testclient import TestClient

import app as opengarrison_api


class RelayRoomTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary_directory = tempfile.TemporaryDirectory()
        os.environ["OPENGARRISON_API_DB"] = os.path.join(
            self._temporary_directory.name,
            "opengarrison-test.db",
        )
        os.environ.pop("OPENGARRISON_RELAY_PUBLIC_BASE_URL", None)
        opengarrison_api.relay_sessions.clear()
        opengarrison_api.relay_session_ids_by_room_code.clear()
        opengarrison_api.relay_room_lookup_attempts.clear()
        self.client = TestClient(opengarrison_api.app)
        self.client.__enter__()

    def tearDown(self) -> None:
        self.client.__exit__(None, None, None)
        self._temporary_directory.cleanup()

    def test_room_code_normalization_rejects_ambiguous_characters(self) -> None:
        self.assertEqual("K7PM", opengarrison_api.normalize_relay_room_code(" k7-pm "))
        self.assertEqual("", opengarrison_api.normalize_relay_room_code("ABCI"))
        self.assertEqual("", opengarrison_api.normalize_relay_room_code("ABC0"))

    def test_openapi_documents_relay_websocket_path(self) -> None:
        path = opengarrison_api.app.openapi()["paths"]["/api/relay/ws/{session_id}/{role}"]
        self.assertTrue(path["x-websocket"])
        self.assertEqual(["wss", "ws64"], path["x-websocket-protocols"])

    def test_created_room_resolves_only_after_host_relay_connects(self) -> None:
        created = self._create_session()
        room_code = created["roomCode"]
        self.assertRegex(room_code, r"^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$")

        starting = self.client.get(f"/api/relay/room/{room_code}")
        self.assertEqual(409, starting.status_code)

        host_path = self._websocket_path(created["hostWebSocketUrl"])
        with self.client.websocket_connect(host_path) as host:
            resolved = self.client.get(f"/api/relay/room/{room_code.lower()}")
            self.assertEqual(200, resolved.status_code)
            payload = resolved.json()
            self.assertEqual(room_code, payload["roomCode"])
            self.assertEqual("OG2-ABCD-EFGH-JKLM", payload["friendCode"])
            self.assertTrue(payload["guestWebSocketUrl"].startswith("ws64://testserver/"))

            by_friend = self.client.get("/api/relay/friend/og2abcdefghjklm")
            self.assertEqual(200, by_friend.status_code)
            self.assertEqual(room_code, by_friend.json()["roomCode"])

            guest_path = self._websocket_path(payload["guestWebSocketUrl"])
            with self.client.websocket_connect(guest_path) as guest:
                host.send_bytes(b"host-to-guest")
                self.assertEqual(b"host-to-guest", guest.receive_bytes())
                guest.send_bytes(b"guest-to-host")
                self.assertEqual(b"guest-to-host", host.receive_bytes())

    def test_replacing_owner_session_invalidates_previous_room_code(self) -> None:
        first = self._create_session()
        second = self._create_session()

        self.assertNotEqual(first["roomCode"], second["roomCode"])
        missing = self.client.get(f"/api/relay/room/{first['roomCode']}")
        self.assertEqual(404, missing.status_code)

    def _create_session(self) -> dict[str, str]:
        response = self.client.post(
            "/api/relay/session",
            json={
                "clientId": "relay-room-test-client",
                "clientSecret": "relay-room-test-secret",
                "friendCode": "OG2-ABCD-EFGH-JKLM",
                "displayName": "Relay Tester",
            },
        )
        self.assertEqual(200, response.status_code, response.text)
        return response.json()

    @staticmethod
    def _websocket_path(value: str) -> str:
        parsed = urlsplit(value)
        return parsed.path + (f"?{parsed.query}" if parsed.query else "")


if __name__ == "__main__":
    unittest.main()
