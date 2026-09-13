"""Single-machine worker for approved native LTD rooms; run as its own service."""
from __future__ import annotations

import asyncio
import json
import logging
from logging.handlers import RotatingFileHandler
import os
from pathlib import Path
import secrets
import shutil
import signal
import socket
import time
import urllib.request

from private_room_store import RoomStore, load_release


class RoomWorker:
    def __init__(self):
        self.store = RoomStore(os.environ.get("OPENGARRISON_API_DB", "/var/lib/opengarrison-api/opengarrison.db"))
        self.release = load_release()
        self.root = Path(os.environ.get("OPENGARRISON_ROOM_DATA_ROOT", "/var/lib/opengarrison-rooms")).resolve()
        self.root.mkdir(parents=True, exist_ok=True)
        self.worker_id = secrets.token_hex(16)
        self.processes = {}
        self.log_tasks = {}
        self.stopping = False
        self.next_cleanup = 0

    def clean_retired_rooms(self, now):
        if now < self.next_cleanup:
            return
        self.next_cleanup = now + 3600
        cutoff = now - 7 * 86400
        for room in self.store.rows("SELECT * FROM private_rooms WHERE status IN ('closed','failed','cancelled') AND updated<? AND process_id=0", (cutoff,)):
            directory = self.room_directory(room["id"])
            if directory.exists():
                shutil.rmtree(directory)
            with self.store.connect(write=True) as db:
                db.execute("DELETE FROM private_room_seats WHERE room_id=?", (room["id"],))
                db.execute("DELETE FROM private_rooms WHERE id=?", (room["id"],))
        self.store.update("DELETE FROM private_room_cancelled_requests WHERE created<?", (cutoff,))

    def room_directory(self, room_id):
        if len(room_id) != 32 or any(character not in "0123456789abcdef" for character in room_id):
            raise ValueError("Invalid room directory identity")
        path = (self.root / room_id).resolve()
        if not path.is_relative_to(self.root):
            raise ValueError("Room directory escaped its data root")
        return path

    def choose_port(self):
        occupied = {row["port"] for row in self.store.rows("SELECT port FROM private_rooms WHERE status IN ('starting','ready','closing')")}
        first = int(os.environ.get("OPENGARRISON_ROOM_FIRST_PORT", "31000"))
        for port in range(first, min(first + 256, 65536)):
            if port in occupied:
                continue
            try:
                with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as tcp, socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as udp:
                    tcp.bind(("127.0.0.1", port))
                    udp.bind(("127.0.0.1", port))
                    return port
            except OSError:
                continue
        raise OSError("No room listener ports are available")

    async def start_room(self, room):
        room_id = room["id"]
        now = time.time()
        changed = self.store.update("UPDATE private_rooms SET status='starting',worker_id=?,worker_lease=?,updated=? WHERE id=? AND status='queued'",
                                    (self.worker_id, now + 20, now, room_id))
        if not changed:
            return
        try:
            if room["protocol_version"] != self.release["protocolVersion"] or room["content_id"] != self.release["contentId"]:
                raise ValueError("Queued room needs a different approved server release")
            directory = self.room_directory(room_id)
            directory.mkdir(parents=True, exist_ok=True)
            port = self.choose_port()
            environment = os.environ.copy()
            environment.update(OPENGARRISON_MANAGED_ROOM_ID=room_id,
                               OPENGARRISON_MANAGED_ROOM_TOKEN=room["server_token"],
                               OPENGARRISON_ROOM_CONTENT_ID=room["content_id"],
                               OPENGARRISON_USER_DATA_ROOT=str(directory))
            # An operator's desktop relay setting must not make a managed worker announce itself.
            for key in ("OPENGARRISON_RELAY_HOST_URL", "OPENGARRISON_PUBLIC_WEBSOCKET_URL",
                        "OPENGARRISON_QUIC_PORT", "OPENGARRISON_WEBSOCKET_CERTIFICATE_PATH",
                        "OPENGARRISON_WEBSOCKET_CERTIFICATE_PASSWORD"):
                environment.pop(key, None)
            command = [self.release["executable"], "--last-to-die", "--last-to-die-difficulty", room["difficulty"],
                       "--slots", str(room["maximum_players"]), "--port", str(port), "--websocket-port", str(port),
                       "--no-lobby", "--no-autobalance", "--no-event-log", "--user-data-root", str(directory)]
            if self.release["executable"].endswith(".dll"):
                command.insert(0, os.environ.get("OPENGARRISON_DOTNET", "dotnet"))
            process = await asyncio.create_subprocess_exec(*command, cwd=str(Path(self.release["executable"]).parent), env=environment,
                stdin=asyncio.subprocess.DEVNULL, stdout=asyncio.subprocess.PIPE, stderr=asyncio.subprocess.STDOUT,
                start_new_session=os.name != "nt", **({"creationflags": 0x08000000} if os.name == "nt" else {}))
            self.processes[room_id] = process
            self.log_tasks[room_id] = asyncio.create_task(self.capture_log(room_id, process))
            self.store.update("UPDATE private_rooms SET process_id=?,port=?,updated=? WHERE id=?", (process.pid, port, time.time(), room_id))
            logging.info("Room %s starting with pid %d", room_id, process.pid)
        except (OSError, ValueError) as exception:
            logging.exception("Room %s failed to start", room_id)
            self.store.update("UPDATE private_rooms SET status='failed',message=?,updated=? WHERE id=?",
                              ("The room server could not start. Try again later.", time.time(), room_id))

    async def capture_log(self, room_id, process):
        handler = RotatingFileHandler(self.room_directory(room_id) / "server.log", maxBytes=2 * 1024 * 1024, backupCount=2, encoding="utf-8")
        try:
            # Bound reads even if a diagnostic writes an unusually long line.
            while chunk := await process.stdout.read(16384):
                handler.emit(logging.LogRecord("room", logging.INFO, "", 0, chunk.decode("utf-8", errors="replace").rstrip(), (), None))
        finally:
            handler.close()

    def probe(self, room):
        request = urllib.request.Request(f"http://127.0.0.1:{room['port']}/opengarrison/session", headers={"X-OG-Room-Token": room["server_token"]})
        with urllib.request.urlopen(request, timeout=2) as response:
            return json.load(response)

    async def stop_room(self, room, message, failed=False):
        room_id = room["id"]
        process = self.processes.pop(room_id, None)
        if process is not None and process.returncode is None:
            process.terminate()
            try:
                await asyncio.wait_for(process.wait(), 8)
            except asyncio.TimeoutError:
                process.kill()
                await process.wait()
        elif process is None and room["process_id"] and os.name != "nt":
            # Recover only a child whose command line contains this exact room data root.
            pid = room["process_id"]
            try:
                command = Path(f"/proc/{pid}/cmdline").read_bytes().split(b"\0")
                if str(self.room_directory(room_id)).encode() in command and self.release["executable"].encode() in command:
                    os.kill(pid, signal.SIGTERM)
                    await asyncio.sleep(0.2)
                    if Path(f"/proc/{pid}").exists():
                        # The PID has not been recycled if the identity still matches.
                        current = Path(f"/proc/{pid}/cmdline").read_bytes().split(b"\0")
                        if command == current:
                            os.kill(pid, signal.SIGKILL)
            except (FileNotFoundError, ProcessLookupError):
                pass
        task = self.log_tasks.pop(room_id, None)
        if task is not None:
            await asyncio.gather(task, return_exceptions=True)
        self.store.update("UPDATE private_rooms SET status=?,message=?,process_id=0,worker_lease=0,updated=? WHERE id=?",
                          ("failed" if failed else "closed", message, time.time(), room_id))
        self.store.update("UPDATE private_room_seats SET connection_id='',expires=0 WHERE room_id=?", (room_id,))
        logging.info("Room %s stopped: %s", room_id, message)

    async def tick(self):
        self.clean_retired_rooms(time.time())
        if not hasattr(self, "health_failures"):
            self.health_failures = {}
        for room in self.store.rows("SELECT * FROM private_rooms WHERE status IN ('queued','starting','ready','closing') ORDER BY created"):
            now = time.time()
            if room["status"] == "queued":
                await self.start_room(room)
                continue
            if room["worker_id"] not in ("", self.worker_id):
                if room["worker_lease"] >= now:
                    continue
                await self.stop_room(room, "The room worker restarted. Please create a new room.", failed=True)
                continue
            if room["status"] == "closing":
                await self.stop_room(room, room["message"] or "Room closed.")
                continue
            process = self.processes.get(room["id"])
            if process is None or process.returncode is not None:
                await self.stop_room(room, "The game server stopped unexpectedly.", failed=True)
                continue
            self.store.update("UPDATE private_rooms SET worker_lease=? WHERE id=? AND worker_id=?", (now + 20, room["id"], self.worker_id))
            if room["status"] == "starting" and now - room["updated"] > 120:
                await self.stop_room(room, "Game server startup timed out.", failed=True)
                continue
            try:
                description = await asyncio.to_thread(self.probe, room)
            except (OSError, ValueError):
                first_failure = self.health_failures.setdefault(room["id"], now)
                if room["status"] == "ready" and now - first_failure > 15:
                    await self.stop_room(room, "The game server stopped responding.", failed=True)
                continue
            self.health_failures.pop(room["id"], None)
            if description.get("roomId") != room["id"] or description.get("kind") != "LastToDie" or description.get("protocolVersion") != room["protocol_version"] or description.get("contentId") != room["content_id"]:
                await self.stop_room(room, "Game server session verification failed.", failed=True)
                continue
            if description.get("status") != "ready":
                continue
            phase = description.get("phase", "Lobby")
            phase_since = room["phase_since"] if room["phase"] == phase and room["phase_since"] else now
            if room["status"] == "starting":
                self.store.update("UPDATE private_rooms SET status='ready',ready_at=?,phase=?,phase_since=?,updated=? WHERE id=? AND status='starting'",
                                  (now, phase, phase_since, now, room["id"]))
                continue
            self.store.update("UPDATE private_rooms SET phase=?,phase_since=? WHERE id=?", (phase, phase_since, room["id"]))
            if phase == "Lobby":
                # The native lobby has a shorter reconnect reservation. Keep a conservative minute here.
                self.store.update("DELETE FROM private_room_seats WHERE room_id=? AND slot=2 AND connection_id='' AND last_seen<?", (room["id"], now - 60))
            owner = self.store.rows("SELECT last_seen FROM private_room_seats WHERE room_id=? AND slot=1", (room["id"],))
            if now - room["ready_at"] > 45 and (not owner or now - owner[0]["last_seen"] > 30):
                await self.stop_room(room, "The room owner disconnected.")
            elif phase in ("Lobby", "Won", "Lost") and now - phase_since > 600:
                await self.stop_room(room, "The idle room expired.")
            elif phase == "Paused" and now - phase_since > 1800:
                await self.stop_room(room, "The paused solo run expired.")

    async def run(self):
        try:
            while not self.stopping:
                await self.tick()
                await asyncio.sleep(1)
        finally:
            for room in self.store.rows("SELECT * FROM private_rooms WHERE worker_id=? AND status IN ('starting','ready','closing')", (self.worker_id,)):
                await self.stop_room(room, "The room service is restarting.")


async def main():
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(message)s")
    worker = RoomWorker()
    loop = asyncio.get_running_loop()
    if os.name != "nt":
        for sig in (signal.SIGTERM, signal.SIGINT):
            loop.add_signal_handler(sig, lambda: setattr(worker, "stopping", True))
    await worker.run()


if __name__ == "__main__":
    asyncio.run(main())
