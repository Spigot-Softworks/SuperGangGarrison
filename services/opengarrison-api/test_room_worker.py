import json
import os
from pathlib import Path
import tempfile
import time
import unittest
from unittest.mock import patch

from room_worker import RoomWorker


class Process:
    returncode = None
    terminated = False

    def terminate(self):
        self.terminated = True
        self.returncode = 0

    async def wait(self):
        return self.returncode


class RoomWorkerTests(unittest.IsolatedAsyncioTestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        (self.root / "server").write_bytes(b"placeholder")
        self.release = dict(protocolVersion=94, buildVersion="test", contentId="content", serverExecutable="server")
        (self.root / "release.json").write_text(json.dumps(self.release))
        self.env = patch.dict(os.environ, dict(OPENGARRISON_API_DB=str(self.root / "api.db"),
            OPENGARRISON_ROOM_RELEASE_MANIFEST=str(self.root / "release.json"), OPENGARRISON_ROOM_DATA_ROOT=str(self.root / "data")))
        self.env.start()
        self.worker = RoomWorker()
        self.room = self.worker.store.create("owner", "friend", dict(requestId="request", maximumPlayers=1, difficulty="standard"), self.release, 2)
        self.worker.store.update("UPDATE private_rooms SET worker_id=?,status='starting',port=32001,updated=? WHERE id=?",
                                 (self.worker.worker_id, time.time(), self.room["id"]))
        self.process = Process()
        self.worker.processes[self.room["id"]] = self.process

    def tearDown(self):
        self.env.stop()
        self.temp.cleanup()

    def row(self):
        return self.worker.store.rows("SELECT * FROM private_rooms")[0]

    def describe(self, room):
        return dict(roomId=room["id"], kind="LastToDie", protocolVersion=94, contentId="content", status="ready", phase="Lobby")

    async def test_server_is_not_ready_until_native_descriptor_matches(self):
        self.worker.probe = self.describe
        await self.worker.tick()
        self.assertEqual("ready", self.row()["status"])
        self.assertGreater(self.row()["worker_lease"], time.time())
        self.worker.probe = lambda room: dict(self.describe(room), contentId="wrong")
        await self.worker.tick()
        self.assertEqual("failed", self.row()["status"])
        self.assertTrue(self.process.terminated)

    async def test_startup_deadline_stops_process(self):
        self.worker.store.update("UPDATE private_rooms SET updated=?", (time.time() - 121,))
        await self.worker.tick()
        self.assertEqual("failed", self.row()["status"])
        self.assertTrue(self.process.terminated)

    async def test_owner_disconnect_closes_ready_room_and_revokes_seat(self):
        self.worker.probe = self.describe
        self.worker.store.update("UPDATE private_rooms SET status='ready',ready_at=?", (time.time() - 50,))
        self.worker.store.update("UPDATE private_room_seats SET last_seen=?", (time.time() - 31,))
        await self.worker.tick()
        self.assertEqual("closed", self.row()["status"])
        self.assertTrue(self.process.terminated)
        self.assertEqual(0, self.worker.store.rows("SELECT expires FROM private_room_seats")[0][0])

    async def test_hung_ready_server_is_stopped_after_health_deadline(self):
        self.worker.store.update("UPDATE private_rooms SET status='ready',ready_at=?", (time.time(),))
        self.worker.health_failures = {self.room["id"]: time.time() - 16}
        def failed_probe(_):
            raise OSError("not responding")
        self.worker.probe = failed_probe
        await self.worker.tick()
        self.assertEqual("failed", self.row()["status"])
        self.assertTrue(self.process.terminated)

    def test_retention_only_removes_verified_retired_room_directory(self):
        self.worker.store.update("UPDATE private_rooms SET status='closed',updated=?", (time.time() - 8 * 86400,))
        directory = self.worker.room_directory(self.room["id"])
        directory.mkdir()
        (directory / "server.log").write_text("old room")
        untouched = self.root / "unrelated.txt"
        untouched.write_text("keep")
        self.worker.clean_retired_rooms(time.time())
        self.assertFalse(directory.exists())
        self.assertTrue(untouched.exists())
        self.assertEqual([], self.worker.store.rows("SELECT * FROM private_rooms"))
        with self.assertRaises(ValueError):
            self.worker.room_directory("../outside")


if __name__ == "__main__":
    unittest.main()
