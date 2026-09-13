import json
import logging
import os
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch
import uuid

from fastapi.testclient import TestClient
from starlette.websockets import WebSocketDisconnect

import app as api
from private_room_store import RoomStore
from private_rooms import RoomAdmissionLogFilter


class RoomAdmissionLogTests(unittest.TestCase):
    def test_websocket_tokens_are_redacted_in_formatted_uvicorn_records(self):
        record = logging.LogRecord("uvicorn.error", logging.INFO, "", 0,
            '%s - "WebSocket %s" [accepted]', ("127.0.0.1", "/api/private-rooms/ws/room/1?token=private-value"), None)
        self.assertTrue(RoomAdmissionLogFilter().filter(record))
        self.assertNotIn("private-value", record.getMessage())
        self.assertIn("token=[redacted]", record.getMessage())
        self.assertIn("[accepted]", record.getMessage())

    def test_other_log_messages_are_preserved(self):
        record = logging.LogRecord("uvicorn.error", logging.INFO, "", 0, "room %s ready", ("123",), None)
        RoomAdmissionLogFilter().filter(record)
        self.assertEqual("room 123 ready", record.getMessage())


class PrivateRoomTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "server").write_bytes(b"test executable")
        (self.root / "room-release.json").write_text(json.dumps(dict(protocolVersion=94, contentId="test-content", buildVersion="test", serverExecutable="server")))
        self.environment = patch.dict(os.environ, {
            "OPENGARRISON_API_DB": str(self.root / "api.db"),
            "OPENGARRISON_ROOMS_ENABLED": "1",
            "OPENGARRISON_ROOM_CAPACITY": "2",
            "OPENGARRISON_ROOM_RELEASE_MANIFEST": str(self.root / "room-release.json"),
            "OPENGARRISON_RELAY_PUBLIC_BASE_URL": "https://api.example.test",
        })
        self.environment.start()
        api.relay_room_lookup_attempts.clear()
        self.client = TestClient(api.app)
        self.client.__enter__()
        self.store = RoomStore(str(self.root / "api.db"))
        self.owner = self.identity("A")
        self.guest = self.identity("B")

    def tearDown(self):
        self.client.__exit__(None, None, None)
        self.environment.stop()
        self.temp.cleanup()

    @staticmethod
    def identity(letter):
        return dict(clientId=uuid.uuid4().hex, clientSecret=uuid.uuid4().hex, friendCode=f"OG2-AAAA-AAA{letter}",
                    displayName="Room tester", requestId=uuid.uuid4().hex, protocolVersion=94, buildVersion="test", contentId="test-content")

    def post(self, operation, payload):
        return self.client.post("/api/private-rooms/" + operation, json=payload)

    def create(self, maximum=2):
        response = self.post("create", dict(self.owner, maximumPlayers=maximum))
        self.assertEqual(200, response.status_code, response.text)
        return response.json()

    def ready(self, room):
        self.store.update("UPDATE private_rooms SET status='ready',worker_lease=?,ready_at=?,phase_since=? WHERE id=?",
                          (time.time() + 20, time.time(), time.time(), room["roomId"]))

    def test_create_is_idempotent_and_reserves_owner_slot(self):
        first = self.create()
        second = self.create()
        self.assertEqual(first["roomId"], second["roomId"])
        self.assertEqual(1, len(self.store.rows("SELECT * FROM private_rooms")))
        seat = self.store.rows("SELECT * FROM private_room_seats")[0]
        self.assertEqual(1, seat["slot"])
        self.assertEqual(self.owner["clientId"], seat["client_id"])

    def test_preregistered_desktop_identity_can_create_a_private_room(self):
        self.assertNotIn("-", self.owner["clientId"])
        registered = self.client.post(
            "/api/client/register",
            json={
                "clientId": self.owner["clientId"],
                "clientSecret": self.owner["clientSecret"],
                "friendCode": self.owner["friendCode"],
                "displayName": self.owner["displayName"],
            },
        )
        self.assertEqual(200, registered.status_code, registered.text)

        room = self.create()
        seat = self.store.rows(
            "SELECT client_id FROM private_room_seats WHERE room_id=? AND slot=1",
            (room["roomId"],),
        )[0]
        self.assertEqual(self.owner["clientId"], seat["client_id"])

    def test_solo_is_private_and_has_no_invite_code(self):
        room = self.create(1)
        self.assertEqual("", room["roomCode"])
        self.ready(room)
        self.assertEqual(404, self.post("join", dict(self.guest, roomId=room["roomId"])).status_code)

    def test_two_players_receive_distinct_endpoints_and_third_cannot_evict(self):
        room = self.create()
        self.ready(room)
        guest = self.post("join", dict(self.guest, code=room["roomCode"]))
        owner = self.post("join", dict(self.owner, roomId=room["roomId"]))
        self.assertEqual(200, guest.status_code, guest.text)
        self.assertEqual(200, owner.status_code, owner.text)
        self.assertIn("/1?token=", owner.json()["endpoint"])
        self.assertIn("/2?token=", guest.json()["endpoint"])
        self.assertTrue(owner.json()["isOwner"])
        self.assertFalse(guest.json()["isOwner"])
        third = self.post("join", dict(self.identity("C"), code=room["roomCode"]))
        self.assertEqual(409, third.status_code)
        self.assertEqual(self.guest["clientId"], self.store.rows("SELECT client_id FROM private_room_seats WHERE slot=2")[0][0])

    def test_join_checks_worker_lease_and_content(self):
        room = self.create()
        self.assertEqual(409, self.post("join", dict(self.guest, code=room["roomCode"])).status_code)
        self.ready(room)
        self.assertEqual(426, self.post("join", dict(self.guest, code=room["roomCode"], contentId="wrong")).status_code)
        self.store.update("UPDATE private_rooms SET worker_lease=0")
        self.assertEqual(409, self.post("join", dict(self.guest, code=room["roomCode"])).status_code)

    def test_cancel_before_create_prevents_late_allocation(self):
        self.assertEqual(200, self.post("cancel", self.owner).status_code)
        room = self.create()
        self.assertEqual("cancelled", room["status"])
        self.assertEqual([], self.store.rows("SELECT * FROM private_room_seats"))

    def test_cancel_before_guest_lookup_prevents_late_reservation(self):
        room = self.create()
        self.ready(room)
        request = dict(self.guest, code=room["roomCode"])
        self.assertEqual(200, self.post("cancel", request).status_code)
        self.assertEqual(409, self.post("join", request).status_code)
        self.assertEqual([], self.store.rows("SELECT * FROM private_room_seats WHERE slot=2"))

    def test_capacity_and_one_room_per_owner(self):
        self.create()
        owned = self.post("create", dict(self.owner, requestId=uuid.uuid4().hex))
        self.assertEqual(503, owned.status_code)
        self.assertEqual("owned_room_active", owned.json()["detail"]["code"])
        self.assertEqual(200, self.post("create", self.guest).status_code)
        full = self.post("create", self.identity("C"))
        self.assertEqual(503, full.status_code)
        self.assertEqual({"code": "capacity_full", "roomId": ""}, full.json()["detail"])

    def test_restart_waits_for_owner_cleanup_without_releasing_global_capacity(self):
        room = self.create()
        self.ready(room)
        leave = self.post("leave", dict(self.owner, roomId=room["roomId"]))
        self.assertEqual("closing", leave.json()["status"])
        request = dict(self.owner, requestId=uuid.uuid4().hex, maximumPlayers=1)
        closing = self.post("create", request)
        self.assertEqual(503, closing.status_code)
        self.assertEqual({"code": "owned_room_closing", "roomId": room["roomId"]}, closing.json()["detail"])
        self.assertEqual(200, self.post("create", self.guest).status_code)
        # The native process still occupies a slot until the worker stops it.
        full = self.post("create", self.identity("C"))
        self.assertEqual({"code": "capacity_full", "roomId": ""}, full.json()["detail"])
        self.store.update("UPDATE private_rooms SET status='closed' WHERE id=?", (room["roomId"],))
        resumed = self.post("create", request)
        self.assertEqual(200, resumed.status_code)
        self.assertEqual("queued", resumed.json()["status"])
        self.assertEqual(resumed.json()["roomId"], self.post("create", request).json()["roomId"])

    def test_disabled_hosting_has_distinct_reason(self):
        with patch.dict(os.environ, {"OPENGARRISON_ROOMS_ENABLED": "0"}):
            response = self.post("create", self.owner)
        self.assertEqual(503, response.status_code)
        self.assertEqual("hosting_unavailable", response.json()["detail"]["code"])

    def test_only_owner_can_close_and_read_allocation(self):
        room = self.create()
        self.ready(room)
        self.assertEqual(404, self.post("status", dict(self.guest, roomId=room["roomId"])).status_code)
        self.post("leave", dict(self.guest, roomId=room["roomId"]))
        self.assertEqual("ready", self.store.rows("SELECT status FROM private_rooms")[0][0])
        self.post("leave", dict(self.owner, roomId=room["roomId"]))
        self.assertEqual("closing", self.store.rows("SELECT status FROM private_rooms")[0][0])

    def test_new_guest_cannot_join_a_run_already_playing(self):
        room = self.create()
        self.ready(room)
        self.store.update("UPDATE private_rooms SET phase='Playing'")
        self.assertEqual(409, self.post("join", dict(self.guest, code=room["roomCode"])).status_code)

    def test_admission_requires_valid_unoccupied_seat(self):
        room = self.create()
        self.ready(room)
        with self.assertRaises(WebSocketDisconnect):
            with self.client.websocket_connect(f"/api/private-rooms/ws/{room['roomId']}/1?token=wrong"):
                self.fail("Invalid token was admitted")
        self.assertEqual("", self.store.rows("SELECT connection_id FROM private_room_seats")[0][0])

    def test_reusing_create_key_with_changed_settings_is_rejected(self):
        self.create()
        self.assertEqual(409, self.post("create", dict(self.owner, maximumPlayers=1)).status_code)

    def test_guest_cancellation_by_room_id_does_not_allocate_another_room(self):
        room = self.create()
        self.ready(room)
        request = dict(self.guest, roomId=room["roomId"])
        self.assertEqual(200, self.post("join", request).status_code)
        self.assertEqual(200, self.post("cancel", request).status_code)
        self.assertEqual(1, len(self.store.rows("SELECT * FROM private_rooms")))
        self.assertEqual([], self.store.rows("SELECT * FROM private_room_seats WHERE slot=2"))

    def test_gateway_forwards_two_independent_binary_peers_and_releases_seats(self):
        import asyncio
        from urllib.parse import urlsplit
        room = self.create()
        self.ready(room)
        self.store.update("UPDATE private_rooms SET port=31000")
        peers = []

        class NativePeer:
            def __init__(self, url, **kwargs):
                self.headers = kwargs["additional_headers"]
                self.queue = asyncio.Queue()
                peers.append(self)

            async def __aenter__(self):
                return self

            async def __aexit__(self, *_):
                pass

            async def send(self, data):
                await self.queue.put(self.headers["X-OG-Player-Slot"].encode() + b":" + data)

            def __aiter__(self):
                return self

            async def __anext__(self):
                return await self.queue.get()

        guest = self.post("join", dict(self.guest, code=room["roomCode"])).json()
        owner = self.post("join", dict(self.owner, roomId=room["roomId"])).json()
        paths = [urlsplit(grant["endpoint"]) for grant in (guest, owner)]
        with patch("private_rooms.connect_websocket", NativePeer):
            with self.client.websocket_connect(paths[0].path + "?" + paths[0].query) as guest_socket:
                with self.client.websocket_connect(paths[1].path + "?" + paths[1].query) as owner_socket:
                    guest_socket.send_bytes(b"guest input")
                    owner_socket.send_bytes(b"owner input")
                    self.assertEqual(b"2:guest input", guest_socket.receive_bytes())
                    self.assertEqual(b"1:owner input", owner_socket.receive_bytes())
                    # A slow account/database operation must not stall live
                    # game packets on the ASGI event loop.
                    from concurrent.futures import ThreadPoolExecutor
                    import threading
                    entered = threading.Event()
                    resume = threading.Event()
                    verify = api.verify_client
                    def delayed_verify(*args, **kwargs):
                        entered.set()
                        resume.wait(5)
                        return verify(*args, **kwargs)
                    def round_trip():
                        owner_socket.send_bytes(b"while authenticating")
                        return owner_socket.receive_bytes()
                    with ThreadPoolExecutor(max_workers=2) as pool, patch.object(api, "verify_client", delayed_verify):
                        request = pool.submit(self.post, "status", dict(self.owner, roomId=room["roomId"]))
                        try:
                            self.assertTrue(entered.wait(2), "Authentication did not start")
                            self.assertEqual(b"1:while authenticating", pool.submit(round_trip).result(timeout=2))
                        finally:
                            resume.set()
                        self.assertEqual(200, request.result(timeout=5).status_code)
                    with self.assertRaises(WebSocketDisconnect):
                        with self.client.websocket_connect(paths[0].path + "?" + paths[0].query):
                            self.fail("An occupied seat was admitted twice")
        self.assertEqual([self.guest["clientId"], self.owner["clientId"]], [p.headers["X-OG-Client-Id"] for p in peers])
        deadline = time.monotonic() + 2
        while time.monotonic() < deadline and any(row["connection_id"] for row in self.store.rows("SELECT * FROM private_room_seats")):
            time.sleep(0.01)
        self.assertTrue(all(not row["connection_id"] for row in self.store.rows("SELECT * FROM private_room_seats")))


if __name__ == "__main__":
    unittest.main()
