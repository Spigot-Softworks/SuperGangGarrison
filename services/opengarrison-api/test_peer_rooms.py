import json
import os
from pathlib import Path
import tempfile
import unittest
from contextlib import ExitStack
from unittest.mock import patch
import uuid
from urllib.parse import urlsplit

from fastapi.testclient import TestClient
from starlette.websockets import WebSocketDisconnect
import app as api


class PeerRoomTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.environment = patch.dict(os.environ, {
            "OPENGARRISON_API_DB": str(Path(self.temp.name) / "api.db"),
            "OPENGARRISON_RELAY_PUBLIC_BASE_URL": "https://api.example.test",
            "OPENGARRISON_ROOMS_ENABLED": "0", "OPENGARRISON_ROOM_CAPACITY": "1",
        })
        self.environment.start()
        api.peer_rooms.clear()
        api.relay_room_lookup_attempts.clear()
        self.client = TestClient(api.app)
        self.client.__enter__()
        self.owner = self.identity("A")

    def tearDown(self):
        self.client.__exit__(None, None, None)
        api.peer_rooms.clear()
        self.environment.stop()
        self.temp.cleanup()

    @staticmethod
    def identity(letter):
        return dict(clientId=uuid.uuid4().hex, clientSecret=uuid.uuid4().hex, friendCode=f"OG2-AAAA-AAA{letter}",
                    displayName="Player " + letter, requestId=uuid.uuid4().hex, protocolVersion=95, contentId="test-content")

    def post(self, operation, payload):
        return self.client.post("/api/peer-rooms/" + operation, json=payload)

    def create(self, kind="LastToDie"):
        response = self.post("create", dict(self.owner, kind=kind))
        self.assertEqual(200, response.status_code, response.text)
        return response.json()

    def join(self, room, letter, **changes):
        payload = dict(self.identity(letter), code=room["code"], kind=room["kind"], **changes)
        response = self.post("join", payload)
        self.assertEqual(200, response.status_code, response.text)
        return payload, response.json()

    def ws(self, grant):
        url = urlsplit(grant["endpoint"])
        return self.client.websocket_connect(url.path + "?" + url.query)

    def receive(self, ws, kind, predicate=lambda _: True):
        for _ in range(40):
            message = ws.receive()
            if message.get("bytes") is not None:
                if kind == "bytes": return message["bytes"]
                continue
            value = json.loads(message.get("text", "{}"))
            if value.get("type") == kind and predicate(value): return value
        self.fail("Expected room response: " + kind)

    def test_four_seats_and_idempotency_without_managed_workers(self):
        room = self.create()
        self.assertEqual(room, self.create())
        guests = [self.join(room, letter) for letter in "BCD"]
        self.assertEqual([2, 3, 4], [grant["slot"] for _, grant in guests])
        full = self.post("join", dict(self.identity("E"), code=room["code"]))
        self.assertEqual(409, full.status_code)
        for request, grant in guests:
            self.assertEqual(grant, self.post("join", request).json())
        self.assertEqual(4, len(api.peer_rooms[room["code"]].seats))
        self.assertEqual(409, self.post("create", dict(self.owner, contentId="changed")).status_code)

    def test_preregistered_desktop_identity_can_create_a_peer_room(self):
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
        self.assertEqual(1, room["slot"])
        self.assertEqual(self.owner["clientId"], api.peer_rooms[room["code"]].owner)

    def test_rankings_and_room_auth_accept_both_uuid_spellings(self):
        identity = self.identity("F")
        identity["clientId"] = str(uuid.UUID(identity["clientId"]))

        rankings = self.client.post("/api/last-to-die/rankings", json=identity)
        self.assertEqual(200, rankings.status_code, rankings.text)

        room = self.post("create", identity)
        self.assertEqual(200, room.status_code, room.text)
        self.assertEqual(uuid.UUID(identity["clientId"]).hex, api.peer_rooms[room.json()["code"]].owner)

    def test_rooms_do_not_consume_the_managed_game_process_limit(self):
        for letter in "ABCD":
            self.assertEqual(200, self.post("create", self.identity(letter)).status_code)
        self.assertEqual(4, len(api.peer_rooms))

    def test_cancel_handles_both_sides_of_allocation_race(self):
        self.assertEqual(200, self.post("cancel", self.owner).status_code)
        self.assertEqual(409, self.post("create", self.owner).status_code)
        self.owner = self.identity("B")
        room = self.create()
        self.assertEqual(200, self.post("cancel", self.owner).status_code)
        self.assertNotIn(room["code"], api.peer_rooms)

    def test_identity_version_mode_and_socket_admission_are_enforced(self):
        room = self.create()
        self.assertEqual(426, self.post("join", dict(self.identity("B"), code=room["code"], protocolVersion=94)).status_code)
        self.assertEqual(426, self.post("join", dict(self.identity("C"), code=room["code"], contentId="other")).status_code)
        self.assertEqual(404, self.post("join", dict(self.identity("D"), code=room["code"], kind="Practice")).status_code)
        stolen = dict(self.owner, code=room["code"], clientSecret="wrong")
        self.assertIn(self.post("join", stolen).status_code, (401, 403))
        with self.assertRaises(WebSocketDisconnect):
            with self.client.websocket_connect(f'/api/peer-rooms/ws/{room["code"]}/1?token=wrong'): pass

    def test_relay_keeps_three_guests_separate_and_targets_signaling(self):
        room = self.create()
        guests = [self.join(room, letter)[1] for letter in "BCD"]
        with ExitStack() as stack:
            host = stack.enter_context(self.ws(room))
            sockets = [stack.enter_context(self.ws(grant)) for grant in guests]
            for slot, guest in enumerate(sockets, 2):
                guest.send_bytes(bytes([1, slot, 42]))
                self.assertEqual(bytes([slot, slot, 42]), self.receive(host, "bytes"))
                host.send_bytes(bytes([slot, 99, slot]))
                self.assertEqual(bytes([1, 99, slot]), self.receive(guest, "bytes"))
            host.send_json(dict(type="signal", target=4, data="offer for fourth player"))
            signal = self.receive(sockets[2], "signal")
            self.assertEqual(1, signal["source"])
            self.assertEqual("offer for fourth player", signal["data"])

    def test_host_controls_settings_start_and_return_lobby(self):
        room = self.create("Practice")
        _, guest_grant = self.join(room, "B")
        with self.ws(room) as host, self.ws(guest_grant) as guest:
            state = self.receive(host, "state", lambda s: all(p["connected"] for p in s["players"]) and len(s["players"]) == 2)
            guest.send_json(dict(type="settings", revision=state["revision"], settings=dict(map="Truefort")))
            self.receive(guest, "error")
            guest.send_json(dict(type="start")); self.receive(guest, "error")
            host.send_json(dict(type="start")); self.receive(host, "error")
            host.send_json(dict(type="settings", revision=state["revision"], settings=dict(map="Truefort", redBots=2, blueBots=3, tickRate=60)))
            self.receive(host, "state", lambda s: s["settings"]["map"] == "Truefort")
            guest.send_json(dict(type="team", team="Blue"))
            self.receive(guest, "state", lambda s: s["players"][1]["team"] == "Blue")
            host.send_json(dict(type="ready", ready=True)); guest.send_json(dict(type="ready", ready=True))
            self.receive(host, "state", lambda s: all(p["ready"] for p in s["players"]))
            host.send_json(dict(type="start"))
            running = self.receive(host, "state", lambda s: s["phase"] == "Playing")
            self.assertEqual(2, running["generation"])
            self.join(room, "C")  # Practice supports late joins.
            host.send_json(dict(type="lobby"))
            lobby = self.receive(host, "state", lambda s: s["phase"] == "Lobby" and s["generation"] == 3)
            self.assertFalse(any(p["ready"] for p in lobby["players"]))
            self.assertEqual("Truefort", lobby["settings"]["map"])

    def test_ltd_can_start_with_empty_seats_but_new_players_wait_for_lobby(self):
        room = self.create()
        with self.ws(room) as host:
            self.receive(host, "state")
            host.send_json(dict(type="ready", ready=True)); self.receive(host, "state", lambda s: s["players"][0]["ready"])
            host.send_json(dict(type="start")); self.receive(host, "state", lambda s: s["phase"] == "Playing")
            self.assertEqual(409, self.post("join", dict(self.identity("B"), code=room["code"])).status_code)
            self.assertEqual(200, self.post("join", dict(self.owner, code=room["code"])).status_code)

    def test_bad_commands_are_rejected_without_losing_room_and_host_leave_closes(self):
        room = self.create()
        _, grant = self.join(room, "B")
        with self.ws(room) as host, self.ws(grant) as guest:
            for bad in ([], None, dict(type="signal", target=1, data={}), dict(type="settings", settings=None)):
                guest.send_json(bad); self.receive(guest, "error")
            host.send_json(dict(type="leave"))
            self.assertEqual("The host left the room.", self.receive(guest, "closed")["reason"])
            self.assertNotIn(room["code"], api.peer_rooms)

    def test_confirmed_host_leave_allows_immediate_room_creation(self):
        room = self.create()
        _, guest_grant = self.join(room, "B")
        with self.ws(room) as host, self.ws(guest_grant) as guest:
            self.receive(host, "state", lambda s: len(s["players"]) == 2)
            leave = dict(self.owner, code=room["code"])
            self.assertEqual(200, self.post("leave", leave).status_code)
            self.assertEqual(200, self.post("leave", leave).status_code)
            self.assertNotIn(room["code"], api.peer_rooms)
            self.assertEqual("The host left the room.", self.receive(guest, "closed")["reason"])
            replacement = self.post("create", dict(self.owner, requestId=uuid.uuid4().hex, kind="Practice"))
            self.assertEqual(200, replacement.status_code, replacement.text)

    def test_confirmed_guest_leave_releases_an_active_seat_without_removing_others(self):
        room = self.create("Practice")
        guest_request, guest_grant = self.join(room, "B")
        with self.ws(room) as host, self.ws(guest_grant):
            self.receive(host, "state", lambda s: len(s["players"]) == 2 and s["players"][1]["connected"])
            stranger = dict(self.identity("C"), code=room["code"])
            self.assertEqual(200, self.post("leave", stranger).status_code)
            self.assertEqual(2, len(api.peer_rooms[room["code"]].seats))
            self.assertEqual(200, self.post("leave", guest_request).status_code)
            self.receive(host, "state", lambda s: len(s["players"]) == 1)
            self.assertEqual(2, self.join(room, "D")[1]["slot"])

    def test_confirmed_leave_rejects_stolen_identity(self):
        room = self.create()
        response = self.post("leave", dict(self.owner, code=room["code"], clientSecret="wrong"))
        self.assertIn(response.status_code, (401, 403))
        self.assertIn(room["code"], api.peer_rooms)


if __name__ == "__main__": unittest.main()
