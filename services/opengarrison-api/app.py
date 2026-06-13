from __future__ import annotations

import hashlib
import os
import re
import sqlite3
import time
from contextlib import contextmanager
from pathlib import Path
from typing import Any

from fastapi import FastAPI, HTTPException, Request
from fastapi.middleware.cors import CORSMiddleware
from pydantic import BaseModel


DEFAULT_DB_PATH = "/var/lib/opengarrison-api/opengarrison.db"
PRESENCE_TTL_SECONDS = 120
SERVER_TTL_SECONDS = 120
FRIEND_CODE_RE = re.compile(r"^OG2-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}(?:-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4})?(?:-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4})?$")


def now_seconds() -> int:
    return int(time.time())


def iso_from_seconds(value: int) -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime(value))


def db_path() -> str:
    return os.environ.get("OPENGARRISON_API_DB", DEFAULT_DB_PATH)


def clamp_int(value: int | None, minimum: int, maximum: int) -> int:
    if value is None:
        return minimum
    return max(minimum, min(maximum, int(value)))


def clean_text(value: str | None, maximum_length: int = 128) -> str:
    if not value:
        return ""
    return value.strip()[:maximum_length]


def clean_json_text(value: str | None, maximum_length: int = 4096) -> str:
    if not value:
        return ""
    return value.strip()[:maximum_length]


def normalize_friend_code(value: str | None) -> str:
    if not value:
        return ""
    compact = "".join(ch for ch in value.upper() if ch.isalnum())
    if compact.startswith("OG2"):
        compact = compact[3:]
    if len(compact) not in (8, 12, 16):
        return ""
    formatted = "OG2-" + "-".join(compact[index:index + 4] for index in range(0, len(compact), 4))
    return formatted if FRIEND_CODE_RE.match(formatted) else ""


def secret_hash(secret: str) -> str:
    return hashlib.sha256(secret.encode("utf-8")).hexdigest()


@contextmanager
def connect_db():
    path = Path(db_path())
    path.parent.mkdir(parents=True, exist_ok=True)
    connection = sqlite3.connect(path)
    connection.row_factory = sqlite3.Row
    try:
        yield connection
        connection.commit()
    finally:
        connection.close()


def initialize_db() -> None:
    with connect_db() as db:
        db.executescript(
            """
            PRAGMA journal_mode=WAL;

            CREATE TABLE IF NOT EXISTS clients (
                client_id TEXT PRIMARY KEY,
                friend_code TEXT NOT NULL UNIQUE,
                secret_hash TEXT NOT NULL,
                display_name TEXT NOT NULL DEFAULT '',
                player_card_json TEXT NOT NULL DEFAULT '',
                created_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS presence (
                client_id TEXT PRIMARY KEY,
                friend_code TEXT NOT NULL,
                display_name TEXT NOT NULL DEFAULT '',
                status TEXT NOT NULL DEFAULT 'menu',
                mode TEXT NOT NULL DEFAULT '',
                map TEXT NOT NULL DEFAULT '',
                server_name TEXT NOT NULL DEFAULT '',
                host TEXT NOT NULL DEFAULT '',
                udp_port INTEGER NOT NULL DEFAULT 0,
                websocket_port INTEGER NOT NULL DEFAULT 0,
                websocket_url TEXT NOT NULL DEFAULT '',
                joinable INTEGER NOT NULL DEFAULT 0,
                player_card_json TEXT NOT NULL DEFAULT '',
                updated_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS servers (
                server_id TEXT PRIMARY KEY,
                name TEXT NOT NULL DEFAULT '',
                host TEXT NOT NULL DEFAULT '',
                udp_port INTEGER NOT NULL DEFAULT 0,
                websocket_port INTEGER NOT NULL DEFAULT 0,
                websocket_url TEXT NOT NULL DEFAULT '',
                private INTEGER NOT NULL DEFAULT 0,
                map TEXT NOT NULL DEFAULT '',
                mode TEXT NOT NULL DEFAULT '',
                players INTEGER NOT NULL DEFAULT 0,
                max_players INTEGER NOT NULL DEFAULT 0,
                spectators INTEGER NOT NULL DEFAULT 0,
                protocol_version INTEGER NOT NULL DEFAULT 0,
                build_version TEXT NOT NULL DEFAULT '',
                release_channel TEXT NOT NULL DEFAULT '',
                compatibility_key TEXT NOT NULL DEFAULT '',
                request_ip TEXT NOT NULL DEFAULT '',
                last_seen INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS friend_requests (
                request_id INTEGER PRIMARY KEY AUTOINCREMENT,
                from_client_id TEXT NOT NULL DEFAULT '',
                from_friend_code TEXT NOT NULL,
                to_friend_code TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'pending',
                created_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL,
                UNIQUE(from_friend_code, to_friend_code)
            );

            CREATE TABLE IF NOT EXISTS direct_messages (
                message_id INTEGER PRIMARY KEY AUTOINCREMENT,
                sender_client_id TEXT NOT NULL DEFAULT '',
                sender_friend_code TEXT NOT NULL,
                recipient_friend_code TEXT NOT NULL,
                sender_display_name TEXT NOT NULL DEFAULT '',
                text TEXT NOT NULL,
                created_at INTEGER NOT NULL
            );
            """
        )
        ensure_column(db, "clients", "player_card_json", "TEXT NOT NULL DEFAULT ''")
        ensure_column(db, "presence", "player_card_json", "TEXT NOT NULL DEFAULT ''")
        ensure_column(db, "servers", "build_version", "TEXT NOT NULL DEFAULT ''")
        ensure_column(db, "servers", "release_channel", "TEXT NOT NULL DEFAULT ''")
        ensure_column(db, "servers", "compatibility_key", "TEXT NOT NULL DEFAULT ''")


def ensure_column(db: sqlite3.Connection, table: str, column: str, definition: str) -> None:
    existing_columns = {
        row["name"]
        for row in db.execute(f"PRAGMA table_info({table})").fetchall()
    }
    if column not in existing_columns:
        db.execute(f"ALTER TABLE {table} ADD COLUMN {column} {definition}")


def prune_expired(db: sqlite3.Connection) -> None:
    current = now_seconds()
    db.execute("DELETE FROM presence WHERE updated_at < ?", (current - PRESENCE_TTL_SECONDS,))
    db.execute("DELETE FROM servers WHERE last_seen < ?", (current - SERVER_TTL_SECONDS,))


def request_ip(request: Request) -> str:
    forwarded = request.headers.get("x-forwarded-for", "")
    if forwarded:
        return forwarded.split(",", 1)[0].strip()
    return request.client.host if request.client else ""


def verify_client(
    db: sqlite3.Connection,
    client_id: str,
    friend_code: str,
    client_secret: str,
    display_name: str,
    player_card_json: str = "",
) -> None:
    if not client_id or not client_secret or not friend_code:
        raise HTTPException(status_code=400, detail="client identity is required")

    current = now_seconds()
    hashed_secret = secret_hash(client_secret)
    existing = db.execute("SELECT client_id, secret_hash FROM clients WHERE client_id = ?", (client_id,)).fetchone()
    if existing is not None and existing["secret_hash"] != hashed_secret:
        raise HTTPException(status_code=403, detail="client secret mismatch")

    existing_code = db.execute(
        "SELECT client_id FROM clients WHERE friend_code = ? AND client_id <> ?",
        (friend_code, client_id),
    ).fetchone()
    if existing_code is not None:
        raise HTTPException(status_code=409, detail="friend code is already registered")

    db.execute(
        """
        INSERT INTO clients (client_id, friend_code, secret_hash, display_name, player_card_json, created_at, updated_at)
        VALUES (?, ?, ?, ?, ?, ?, ?)
        ON CONFLICT(client_id) DO UPDATE SET
            friend_code = excluded.friend_code,
            display_name = excluded.display_name,
            player_card_json = CASE
                WHEN excluded.player_card_json <> '' THEN excluded.player_card_json
                ELSE clients.player_card_json
            END,
            updated_at = excluded.updated_at
        """,
        (client_id, friend_code, hashed_secret, display_name, clean_json_text(player_card_json), current, current),
    )


def get_friend_display_name(db: sqlite3.Connection, friend_code: str) -> str:
    row = db.execute(
        """
        SELECT display_name FROM presence WHERE friend_code = ?
        UNION ALL
        SELECT display_name FROM clients WHERE friend_code = ?
        LIMIT 1
        """,
        (friend_code, friend_code),
    ).fetchone()
    return clean_text(row["display_name"], 64) if row is not None else ""


def serialize_friend_request(db: sqlite3.Connection, row: sqlite3.Row, own_friend_code: str) -> dict[str, Any]:
    incoming = row["to_friend_code"] == own_friend_code
    other_code = row["from_friend_code"] if incoming else row["to_friend_code"]
    return {
        "requestId": row["request_id"],
        "direction": "incoming" if incoming else "outgoing",
        "status": row["status"],
        "friendCode": other_code,
        "displayName": get_friend_display_name(db, other_code),
        "createdAtIso": iso_from_seconds(row["created_at"]),
        "updatedAtIso": iso_from_seconds(row["updated_at"]),
    }


def serialize_direct_message(row: sqlite3.Row, own_friend_code: str) -> dict[str, Any]:
    outgoing = row["sender_friend_code"] == own_friend_code
    return {
        "messageId": row["message_id"],
        "direction": "outgoing" if outgoing else "incoming",
        "friendCode": row["recipient_friend_code"] if outgoing else row["sender_friend_code"],
        "displayName": row["sender_display_name"],
        "text": row["text"],
        "createdAtIso": iso_from_seconds(row["created_at"]),
    }


class ServerRegistryRequest(BaseModel):
    action: str = "heartbeat"
    token: str = ""
    serverId: str = ""
    name: str = ""
    host: str = ""
    udpPort: int = 0
    webSocketPort: int = 0
    webSocketUrl: str = ""
    private: bool = False
    map: str = ""
    mode: str = ""
    players: int = 0
    maxPlayers: int = 0
    spectators: int = 0
    protocolVersion: int = 0
    buildVersion: str = ""
    releaseChannel: str = ""
    compatibilityKey: str = ""


class ClientRegisterRequest(BaseModel):
    clientId: str
    clientSecret: str
    friendCode: str
    displayName: str = ""
    playerCard: str = ""


class PresenceHeartbeatRequest(BaseModel):
    clientId: str
    clientSecret: str
    friendCode: str
    displayName: str = ""
    status: str = "menu"
    mode: str = ""
    map: str = ""
    serverName: str = ""
    host: str = ""
    udpPort: int = 0
    webSocketPort: int = 0
    webSocketUrl: str = ""
    joinable: bool = False
    playerCard: str = ""


class PresenceOfflineRequest(BaseModel):
    clientId: str
    clientSecret: str


class FriendRequestCreateRequest(BaseModel):
    clientId: str
    clientSecret: str
    friendCode: str
    displayName: str = ""
    targetFriendCode: str


class FriendRequestsListRequest(BaseModel):
    clientId: str
    clientSecret: str
    friendCode: str
    displayName: str = ""


class FriendRequestRespondRequest(BaseModel):
    clientId: str
    clientSecret: str
    friendCode: str
    displayName: str = ""
    requestId: int
    accept: bool


class DirectMessageSendRequest(BaseModel):
    clientId: str
    clientSecret: str
    friendCode: str
    displayName: str = ""
    targetFriendCode: str
    text: str


class DirectMessagesPollRequest(BaseModel):
    clientId: str
    clientSecret: str
    friendCode: str
    displayName: str = ""
    afterId: int = 0


app = FastAPI(title="OpenGarrison API", version="0.1.0")

cors_origins = [
    origin.strip()
    for origin in os.environ.get("OPENGARRISON_API_CORS_ORIGINS", "https://superganggarrison.com,https://www.superganggarrison.com,https://play.superganggarrison.com,https://unkind-dev.com,https://www.unkind-dev.com,http://localhost:5000,http://localhost:5173").split(",")
    if origin.strip()
]
app.add_middleware(
    CORSMiddleware,
    allow_origins=cors_origins,
    allow_credentials=False,
    allow_methods=["GET", "POST", "OPTIONS"],
    allow_headers=["*"],
)


@app.on_event("startup")
def on_startup() -> None:
    initialize_db()


@app.get("/healthz")
def healthz() -> dict[str, str]:
    return {"status": "ok"}


@app.get("/api/servers")
@app.get("/API/og2servers.php")
def get_servers(
    protocolVersion: int | None = None,
    buildVersion: str = "",
    releaseChannel: str = "",
    channel: str = "",
    compatibilityKey: str = "",
) -> dict[str, Any]:
    requested_channel = clean_text(releaseChannel or channel, 32).lower() or "stable"
    requested_build_version = clean_text(buildVersion, 64)
    requested_compatibility_key = clean_text(compatibilityKey, 128)
    with connect_db() as db:
        prune_expired(db)
        where_clauses = ["last_seen >= ?", "release_channel = ?"]
        parameters: list[Any] = [now_seconds() - SERVER_TTL_SECONDS, requested_channel]
        if protocolVersion is not None:
            where_clauses.append("protocol_version = ?")
            parameters.append(clamp_int(protocolVersion, 0, 999999))
        if requested_compatibility_key:
            where_clauses.append("compatibility_key = ?")
            parameters.append(requested_compatibility_key)
        elif requested_build_version:
            where_clauses.append("(build_version = ? OR build_version = '')")
            parameters.append(requested_build_version)

        rows = db.execute(
            f"""
            SELECT * FROM servers
            WHERE {" AND ".join(where_clauses)}
            ORDER BY players DESC, last_seen DESC, name COLLATE NOCASE
            """,
            parameters,
        ).fetchall()

    return {
        "servers": [
            {
                "serverId": row["server_id"],
                "name": row["name"],
                "host": row["host"],
                "udpPort": row["udp_port"],
                "webSocketPort": row["websocket_port"],
                "webSocketUrl": row["websocket_url"],
                "private": bool(row["private"]),
                "map": row["map"],
                "mode": row["mode"],
                "players": row["players"],
                "maxPlayers": row["max_players"],
                "spectators": row["spectators"],
                "protocolVersion": row["protocol_version"],
                "buildVersion": row["build_version"],
                "releaseChannel": row["release_channel"],
                "compatibilityKey": row["compatibility_key"],
                "lastSeenIso": iso_from_seconds(row["last_seen"]),
            }
            for row in rows
        ],
        "generatedAt": iso_from_seconds(now_seconds()),
    }


@app.post("/api/servers")
@app.post("/API/og2servers.php")
def post_server_registry(payload: ServerRegistryRequest, request: Request) -> dict[str, str]:
    ip = request_ip(request)
    admin_token = os.environ.get("OPENGARRISON_REGISTRY_TOKEN", "")
    action = clean_text(payload.action, 32).lower() or "heartbeat"

    with connect_db() as db:
        prune_expired(db)
        if action == "remove":
            if not payload.serverId:
                return {"serverId": ""}
            if admin_token and payload.token == admin_token:
                db.execute("DELETE FROM servers WHERE server_id = ?", (payload.serverId,))
            else:
                db.execute("DELETE FROM servers WHERE server_id = ? AND request_ip = ?", (payload.serverId, ip))
            return {"serverId": payload.serverId}

        active_for_ip = db.execute(
            "SELECT COUNT(*) AS count FROM servers WHERE request_ip = ? AND last_seen >= ?",
            (ip, now_seconds() - SERVER_TTL_SECONDS),
        ).fetchone()["count"]
        if active_for_ip >= 8 and not (admin_token and payload.token == admin_token):
            raise HTTPException(status_code=429, detail="too many active servers from this address")

        host = clean_text(payload.host, 255) or ip
        udp_port = clamp_int(payload.udpPort, 0, 65535)
        websocket_port = clamp_int(payload.webSocketPort, 0, 65535)
        websocket_url = clean_text(payload.webSocketUrl, 512)
        build_version = clean_text(payload.buildVersion, 64)
        release_channel = clean_text(payload.releaseChannel, 32).lower() or "stable"
        compatibility_key = clean_text(payload.compatibilityKey, 128)
        server_id = clean_text(payload.serverId, 512) or f"og2:{host.lower()}:{udp_port}:{websocket_port}:{websocket_url}"
        current = now_seconds()
        db.execute(
            """
            INSERT INTO servers (
                server_id, name, host, udp_port, websocket_port, websocket_url, private,
                map, mode, players, max_players, spectators, protocol_version,
                build_version, release_channel, compatibility_key, request_ip, last_seen
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(server_id) DO UPDATE SET
                name = excluded.name,
                host = excluded.host,
                udp_port = excluded.udp_port,
                websocket_port = excluded.websocket_port,
                websocket_url = excluded.websocket_url,
                private = excluded.private,
                map = excluded.map,
                mode = excluded.mode,
                players = excluded.players,
                max_players = excluded.max_players,
                spectators = excluded.spectators,
                protocol_version = excluded.protocol_version,
                build_version = excluded.build_version,
                release_channel = excluded.release_channel,
                compatibility_key = excluded.compatibility_key,
                request_ip = excluded.request_ip,
                last_seen = excluded.last_seen
            """,
            (
                server_id,
                clean_text(payload.name, 128),
                host,
                udp_port,
                websocket_port,
                websocket_url,
                1 if payload.private else 0,
                clean_text(payload.map, 128),
                clean_text(payload.mode, 64),
                clamp_int(payload.players, 0, 255),
                clamp_int(payload.maxPlayers, 0, 255),
                clamp_int(payload.spectators, 0, 255),
                clamp_int(payload.protocolVersion, 0, 999999),
                build_version,
                release_channel,
                compatibility_key,
                ip,
                current,
            ),
        )
        return {"serverId": server_id}


@app.post("/api/client/register")
def register_client(payload: ClientRegisterRequest) -> dict[str, str]:
    friend_code = normalize_friend_code(payload.friendCode)
    if not friend_code:
        raise HTTPException(status_code=400, detail="invalid friend code")

    with connect_db() as db:
        verify_client(
            db,
            clean_text(payload.clientId, 64),
            friend_code,
            payload.clientSecret,
            clean_text(payload.displayName, 64),
            clean_json_text(payload.playerCard),
        )

    return {"clientId": payload.clientId, "friendCode": friend_code}


@app.post("/api/friends/request")
def create_friend_request(payload: FriendRequestCreateRequest) -> dict[str, Any]:
    own_code = normalize_friend_code(payload.friendCode)
    target_code = normalize_friend_code(payload.targetFriendCode)
    if not own_code or not target_code:
        raise HTTPException(status_code=400, detail="invalid friend code")
    if own_code == target_code:
        raise HTTPException(status_code=400, detail="cannot request yourself")

    client_id = clean_text(payload.clientId, 64)
    display_name = clean_text(payload.displayName, 64) or "Player"
    current = now_seconds()
    with connect_db() as db:
        verify_client(db, client_id, own_code, payload.clientSecret, display_name)

        reverse = db.execute(
            """
            SELECT * FROM friend_requests
            WHERE from_friend_code = ? AND to_friend_code = ? AND status = 'pending'
            """,
            (target_code, own_code),
        ).fetchone()
        if reverse is not None:
            db.execute(
                "UPDATE friend_requests SET status = 'accepted', updated_at = ? WHERE request_id = ?",
                (current, reverse["request_id"]),
            )
            row = db.execute("SELECT * FROM friend_requests WHERE request_id = ?", (reverse["request_id"],)).fetchone()
            return serialize_friend_request(db, row, own_code)

        db.execute(
            """
            INSERT INTO friend_requests (
                from_client_id, from_friend_code, to_friend_code, status, created_at, updated_at
            )
            VALUES (?, ?, ?, 'pending', ?, ?)
            ON CONFLICT(from_friend_code, to_friend_code) DO UPDATE SET
                from_client_id = excluded.from_client_id,
                status = 'pending',
                updated_at = excluded.updated_at
            """,
            (client_id, own_code, target_code, current, current),
        )
        row = db.execute(
            "SELECT * FROM friend_requests WHERE from_friend_code = ? AND to_friend_code = ?",
            (own_code, target_code),
        ).fetchone()
        return serialize_friend_request(db, row, own_code)


@app.post("/api/friends/requests")
def list_friend_requests(payload: FriendRequestsListRequest) -> dict[str, Any]:
    own_code = normalize_friend_code(payload.friendCode)
    if not own_code:
        raise HTTPException(status_code=400, detail="invalid friend code")

    client_id = clean_text(payload.clientId, 64)
    display_name = clean_text(payload.displayName, 64) or "Player"
    with connect_db() as db:
        verify_client(db, client_id, own_code, payload.clientSecret, display_name)
        rows = db.execute(
            """
            SELECT * FROM friend_requests
            WHERE (to_friend_code = ? AND status = 'pending')
               OR (from_friend_code = ? AND status IN ('pending', 'accepted', 'denied'))
            ORDER BY updated_at DESC, request_id DESC
            LIMIT 50
            """,
            (own_code, own_code),
        ).fetchall()
        return {
            "requests": [serialize_friend_request(db, row, own_code) for row in rows],
            "generatedAt": iso_from_seconds(now_seconds()),
        }


@app.post("/api/friends/respond")
def respond_friend_request(payload: FriendRequestRespondRequest) -> dict[str, Any]:
    own_code = normalize_friend_code(payload.friendCode)
    if not own_code:
        raise HTTPException(status_code=400, detail="invalid friend code")

    client_id = clean_text(payload.clientId, 64)
    display_name = clean_text(payload.displayName, 64) or "Player"
    current = now_seconds()
    with connect_db() as db:
        verify_client(db, client_id, own_code, payload.clientSecret, display_name)
        row = db.execute(
            """
            SELECT * FROM friend_requests
            WHERE request_id = ? AND to_friend_code = ? AND status = 'pending'
            """,
            (payload.requestId, own_code),
        ).fetchone()
        if row is None:
            raise HTTPException(status_code=404, detail="friend request not found")

        status = "accepted" if payload.accept else "denied"
        db.execute(
            "UPDATE friend_requests SET status = ?, updated_at = ? WHERE request_id = ?",
            (status, current, payload.requestId),
        )
        updated = db.execute("SELECT * FROM friend_requests WHERE request_id = ?", (payload.requestId,)).fetchone()
        return serialize_friend_request(db, updated, own_code)


@app.post("/api/messages/send")
def send_direct_message(payload: DirectMessageSendRequest) -> dict[str, Any]:
    own_code = normalize_friend_code(payload.friendCode)
    target_code = normalize_friend_code(payload.targetFriendCode)
    text = clean_text(payload.text, 500)
    if not own_code or not target_code:
        raise HTTPException(status_code=400, detail="invalid friend code")
    if own_code == target_code:
        raise HTTPException(status_code=400, detail="cannot message yourself")
    if not text:
        raise HTTPException(status_code=400, detail="message is required")

    client_id = clean_text(payload.clientId, 64)
    display_name = clean_text(payload.displayName, 64) or "Player"
    current = now_seconds()
    with connect_db() as db:
        verify_client(db, client_id, own_code, payload.clientSecret, display_name)
        cursor = db.execute(
            """
            INSERT INTO direct_messages (
                sender_client_id, sender_friend_code, recipient_friend_code, sender_display_name, text, created_at
            )
            VALUES (?, ?, ?, ?, ?, ?)
            """,
            (client_id, own_code, target_code, display_name, text, current),
        )
        row = db.execute("SELECT * FROM direct_messages WHERE message_id = ?", (cursor.lastrowid,)).fetchone()
        return serialize_direct_message(row, own_code)


@app.post("/api/messages/poll")
def poll_direct_messages(payload: DirectMessagesPollRequest) -> dict[str, Any]:
    own_code = normalize_friend_code(payload.friendCode)
    if not own_code:
        raise HTTPException(status_code=400, detail="invalid friend code")

    client_id = clean_text(payload.clientId, 64)
    display_name = clean_text(payload.displayName, 64) or "Player"
    after_id = max(0, int(payload.afterId))
    with connect_db() as db:
        verify_client(db, client_id, own_code, payload.clientSecret, display_name)
        rows = db.execute(
            """
            SELECT * FROM direct_messages
            WHERE recipient_friend_code = ? AND message_id > ?
            ORDER BY message_id ASC
            LIMIT 50
            """,
            (own_code, after_id),
        ).fetchall()
        return {
            "messages": [serialize_direct_message(row, own_code) for row in rows],
            "generatedAt": iso_from_seconds(now_seconds()),
        }


@app.post("/api/presence/heartbeat")
def heartbeat_presence(payload: PresenceHeartbeatRequest) -> dict[str, str]:
    friend_code = normalize_friend_code(payload.friendCode)
    if not friend_code:
        raise HTTPException(status_code=400, detail="invalid friend code")

    client_id = clean_text(payload.clientId, 64)
    display_name = clean_text(payload.displayName, 64) or "Player"
    status = clean_text(payload.status, 32) or "menu"
    current = now_seconds()
    with connect_db() as db:
        prune_expired(db)
        player_card_json = clean_json_text(payload.playerCard)
        verify_client(db, client_id, friend_code, payload.clientSecret, display_name, player_card_json)
        db.execute(
            """
            INSERT INTO presence (
                client_id, friend_code, display_name, status, mode, map, server_name,
                host, udp_port, websocket_port, websocket_url, joinable, player_card_json, updated_at
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(client_id) DO UPDATE SET
                friend_code = excluded.friend_code,
                display_name = excluded.display_name,
                status = excluded.status,
                mode = excluded.mode,
                map = excluded.map,
                server_name = excluded.server_name,
                host = excluded.host,
                udp_port = excluded.udp_port,
                websocket_port = excluded.websocket_port,
                websocket_url = excluded.websocket_url,
                joinable = excluded.joinable,
                player_card_json = excluded.player_card_json,
                updated_at = excluded.updated_at
            """,
            (
                client_id,
                friend_code,
                display_name,
                status,
                clean_text(payload.mode, 64),
                clean_text(payload.map, 128),
                clean_text(payload.serverName, 128),
                clean_text(payload.host, 255),
                clamp_int(payload.udpPort, 0, 65535),
                clamp_int(payload.webSocketPort, 0, 65535),
                clean_text(payload.webSocketUrl, 512),
                1 if payload.joinable else 0,
                player_card_json,
                current,
            ),
        )

    return {"status": "ok"}


@app.post("/api/presence/offline")
def offline_presence(payload: PresenceOfflineRequest) -> dict[str, str]:
    client_id = clean_text(payload.clientId, 64)
    with connect_db() as db:
        existing = db.execute("SELECT secret_hash FROM clients WHERE client_id = ?", (client_id,)).fetchone()
        if existing is not None and existing["secret_hash"] != secret_hash(payload.clientSecret):
            raise HTTPException(status_code=403, detail="client secret mismatch")
        db.execute("DELETE FROM presence WHERE client_id = ?", (client_id,))

    return {"status": "ok"}


@app.get("/api/presence")
def get_presence(codes: str = "") -> dict[str, Any]:
    requested = []
    seen = set()
    for raw in codes.split(","):
        code = normalize_friend_code(raw)
        if code and code not in seen:
            requested.append(code)
            seen.add(code)

    if not requested:
        return {"friends": [], "generatedAt": iso_from_seconds(now_seconds())}

    current = now_seconds()
    with connect_db() as db:
        prune_expired(db)
        placeholders = ",".join("?" for _ in requested)
        clients = {
            row["friend_code"]: row
            for row in db.execute(f"SELECT * FROM clients WHERE friend_code IN ({placeholders})", requested).fetchall()
        }
        presence = {
            row["friend_code"]: row
            for row in db.execute(f"SELECT * FROM presence WHERE friend_code IN ({placeholders})", requested).fetchall()
        }

    friends = []
    for code in requested:
        client = clients.get(code)
        row = presence.get(code)
        online = row is not None and row["updated_at"] >= current - PRESENCE_TTL_SECONDS
        friends.append(
            {
                "friendCode": code,
                "displayName": (row["display_name"] if row is not None else (client["display_name"] if client is not None else "")),
                "online": online,
                "status": row["status"] if online else "offline",
                "mode": row["mode"] if online else "",
                "map": row["map"] if online else "",
                "serverName": row["server_name"] if online else "",
                "host": row["host"] if online else "",
                "udpPort": row["udp_port"] if online else 0,
                "webSocketPort": row["websocket_port"] if online else 0,
                "webSocketUrl": row["websocket_url"] if online else "",
                "joinable": bool(row["joinable"]) if online else False,
                "playerCard": (row["player_card_json"] if row is not None else (client["player_card_json"] if client is not None else "")),
                "lastSeenIso": iso_from_seconds(row["updated_at"]) if row is not None else "",
            }
        )

    return {"friends": friends, "generatedAt": iso_from_seconds(current)}
