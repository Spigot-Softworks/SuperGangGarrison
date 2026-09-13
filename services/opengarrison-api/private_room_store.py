"""Durable room allocation shared by the API and the native-server worker."""
from __future__ import annotations

import json
import os
import secrets
import sqlite3
import time
from contextlib import contextmanager
from pathlib import Path

ACTIVE_STATES = ("queued", "starting", "ready", "closing")
ROOM_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"


def load_release() -> dict:
    path = Path(os.environ.get("OPENGARRISON_ROOM_RELEASE_MANIFEST", "/opt/opengarrison-rooms/room-release.json"))
    value = json.loads(path.read_text(encoding="utf-8-sig"))
    if not isinstance(value.get("protocolVersion"), int) or not value.get("contentId") or not value.get("buildVersion"):
        raise ValueError("Room release manifest is incomplete")
    executable = (path.parent / value["serverExecutable"]).resolve()
    if not executable.is_relative_to(path.parent.resolve()) or not executable.is_file():
        raise ValueError("Approved room server is missing or outside its release")
    value["executable"] = str(executable)
    value["releaseRoot"] = str(path.parent.resolve())
    return value


class RoomCapacityError(OverflowError):
    def __init__(self, code: str, room_id: str = ""):
        super().__init__(code)
        self.code = code
        self.room_id = room_id


class RoomStore:
    def __init__(self, path: str):
        self.path = path
        Path(path).parent.mkdir(parents=True, exist_ok=True)
        with self.connect() as db:
            db.executescript("""
                CREATE TABLE IF NOT EXISTS private_rooms (
                    id TEXT PRIMARY KEY, code TEXT UNIQUE, owner TEXT NOT NULL,
                    owner_friend TEXT NOT NULL, request_id TEXT NOT NULL,
                    status TEXT NOT NULL, difficulty TEXT NOT NULL, maximum_players INTEGER NOT NULL,
                    protocol_version INTEGER NOT NULL, build_version TEXT NOT NULL, content_id TEXT NOT NULL,
                    created REAL NOT NULL, updated REAL NOT NULL, last_activity REAL NOT NULL,
                    ready_at REAL NOT NULL DEFAULT 0, worker_id TEXT NOT NULL DEFAULT '',
                    worker_lease REAL NOT NULL DEFAULT 0, port INTEGER NOT NULL DEFAULT 0,
                    process_id INTEGER NOT NULL DEFAULT 0, server_token TEXT NOT NULL,
                    phase TEXT NOT NULL DEFAULT 'Lobby', phase_since REAL NOT NULL DEFAULT 0, message TEXT NOT NULL DEFAULT '',
                    UNIQUE(owner, request_id)
                );
                CREATE TABLE IF NOT EXISTS private_room_seats (
                    room_id TEXT NOT NULL REFERENCES private_rooms(id), slot INTEGER NOT NULL,
                    client_id TEXT NOT NULL, request_id TEXT NOT NULL, token TEXT NOT NULL UNIQUE,
                    expires REAL NOT NULL, last_seen REAL NOT NULL,
                    connection_id TEXT NOT NULL DEFAULT '',
                    PRIMARY KEY(room_id, slot), UNIQUE(room_id, client_id)
                );
                CREATE TABLE IF NOT EXISTS private_room_cancelled_requests (
                    client_id TEXT NOT NULL, request_id TEXT NOT NULL, created REAL NOT NULL,
                    PRIMARY KEY(client_id, request_id)
                );
            """)

    @contextmanager
    def connect(self, write: bool = False):
        db = sqlite3.connect(self.path, timeout=10)
        db.row_factory = sqlite3.Row
        db.execute("PRAGMA foreign_keys=ON")
        try:
            if write:
                db.execute("BEGIN IMMEDIATE")
            yield db
            db.commit()
        except BaseException:
            db.rollback()
            raise
        finally:
            db.close()

    @staticmethod
    def create_code(db: sqlite3.Connection) -> str:
        for _ in range(100):
            code = "".join(secrets.choice(ROOM_ALPHABET) for _ in range(4))
            if db.execute("SELECT 1 FROM private_rooms WHERE code=?", (code,)).fetchone() is None:
                return code
        raise RuntimeError("No room codes available")

    def create(self, owner: str, friend: str, request: dict, release: dict, capacity: int, *, cancelled: bool = False):
        now = time.time()
        with self.connect(write=True) as db:
            existing = db.execute("SELECT * FROM private_rooms WHERE owner=? AND request_id=?", (owner, request["requestId"])).fetchone()
            if existing is not None:
                if cancelled:
                    db.execute("UPDATE private_rooms SET status='closing', message='Room cancelled.', updated=? WHERE id=? AND status IN ('queued','starting','ready')", (now, existing["id"]))
                    return db.execute("SELECT * FROM private_rooms WHERE id=?", (existing["id"],)).fetchone()
                if existing["maximum_players"] != request["maximumPlayers"] or existing["difficulty"] != request["difficulty"]:
                    raise ValueError("Creation request key was reused with different settings")
                return existing
            if not cancelled:
                count = db.execute("SELECT COUNT(*) FROM private_rooms WHERE status IN ('queued','starting','ready','closing')").fetchone()[0]
                owned = db.execute("SELECT id,status FROM private_rooms WHERE owner=? AND status IN ('queued','starting','ready','closing') LIMIT 1", (owner,)).fetchone()
                if owned is not None:
                    raise RoomCapacityError("owned_room_closing" if owned["status"] == "closing" else "owned_room_active", owned["id"])
                if count >= capacity:
                    raise RoomCapacityError("capacity_full")
            room_id = secrets.token_hex(16)
            code = None if cancelled or request["maximumPlayers"] == 1 else self.create_code(db)
            db.execute("""INSERT INTO private_rooms
                (id,code,owner,owner_friend,request_id,status,difficulty,maximum_players,protocol_version,build_version,content_id,created,updated,last_activity,server_token)
                VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
                (room_id, code, owner, friend, request["requestId"], "cancelled" if cancelled else "queued",
                 request["difficulty"], request["maximumPlayers"], release["protocolVersion"], release["buildVersion"], release["contentId"], now, now, now, secrets.token_urlsafe(32)))
            if not cancelled:
                db.execute("INSERT INTO private_room_seats (room_id,slot,client_id,request_id,token,expires,last_seen) VALUES (?,1,?,?,?,?,?)",
                           (room_id, owner, request["requestId"], secrets.token_urlsafe(32), now + 43200, now))
            return db.execute("SELECT * FROM private_rooms WHERE id=?", (room_id,)).fetchone()

    def rows(self, sql: str, values: tuple = ()):
        with self.connect() as db:
            return db.execute(sql, values).fetchall()

    def update(self, sql: str, values: tuple = ()):
        with self.connect(write=True) as db:
            return db.execute(sql, values).rowcount
