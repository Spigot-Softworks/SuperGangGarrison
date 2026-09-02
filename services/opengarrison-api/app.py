from __future__ import annotations

import hashlib
import asyncio
import os
import re
import secrets
import sqlite3
import time
from contextlib import contextmanager
from pathlib import Path
from typing import Any
from urllib.parse import quote, urlsplit

from fastapi import FastAPI, HTTPException, Request, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from fastapi.openapi.utils import get_openapi
from pydantic import BaseModel


DEFAULT_DB_PATH = "/var/lib/opengarrison-api/opengarrison.db"
PRESENCE_TTL_SECONDS = 120
SERVER_TTL_SECONDS = 120
RELAY_SESSION_TTL_SECONDS = 43200
RELAY_MAX_MESSAGE_BYTES = 4 * 1024 * 1024
RELAY_MAX_PENDING_MESSAGES = 128
RELAY_MAX_PENDING_BYTES = 4 * 1024 * 1024
RELAY_ROOM_CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
RELAY_ROOM_CODE_LENGTH = 4
RELAY_ROOM_LOOKUP_WINDOW_SECONDS = 60
RELAY_ROOM_LOOKUP_MAX_ATTEMPTS = 30
FRIEND_CODE_RE = re.compile(r"^OG2-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}(?:-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4})?(?:-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4})?$")
RELAY_ROOM_CODE_RE = re.compile(r"^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$")


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


# Normalize after clamp_int is defined so an invalid deployment value cannot
# stop the API process.
try:
    RELAY_SESSION_TTL_SECONDS = clamp_int(
        int(os.environ.get("OPENGARRISON_RELAY_SESSION_TTL_SECONDS", "43200")),
        300,
        86400,
    )
except ValueError:
    RELAY_SESSION_TTL_SECONDS = 43200


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


def normalize_relay_room_code(value: str | None) -> str:
    if not value:
        return ""
    compact = "".join(ch for ch in value.upper() if ch.isalnum())
    return compact if RELAY_ROOM_CODE_RE.fullmatch(compact) else ""


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
                quic_port INTEGER NOT NULL DEFAULT 0,
                quic_url TEXT NOT NULL DEFAULT '',
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
        ensure_column(db, "servers", "quic_port", "INTEGER NOT NULL DEFAULT 0")
        ensure_column(db, "servers", "quic_url", "TEXT NOT NULL DEFAULT ''")


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


def relay_public_origin(request: Request) -> tuple[str, str]:
    configured = os.environ.get("OPENGARRISON_RELAY_PUBLIC_BASE_URL", "").strip()
    if configured:
        parsed = urlsplit(configured)
        if parsed.scheme in ("http", "https", "ws", "wss") and parsed.netloc:
            secure = parsed.scheme in ("https", "wss")
            return ("wss" if secure else "ws", parsed.netloc)

    forwarded_proto = request.headers.get("x-forwarded-proto", "").split(",", 1)[0].strip().lower()
    secure = forwarded_proto in ("https", "wss") or request.url.scheme in ("https", "wss")
    host = request.headers.get("x-forwarded-host", "").split(",", 1)[0].strip()
    if not host:
        host = request.headers.get("host", "").strip()
    if not host:
        raise HTTPException(status_code=503, detail="relay public host is unavailable")
    return ("wss" if secure else "ws", host)


def relay_url(scheme: str, authority: str, session_id: str, role: str, token: str, protocol64: bool) -> str:
    advertised_scheme = f"{scheme}64" if protocol64 else scheme
    return (
        f"{advertised_scheme}://{authority}/api/relay/ws/"
        f"{quote(session_id, safe='')}/{role}?token={quote(token, safe='')}"
    )


def prune_relay_sessions_locked(current: int) -> list[WebSocket]:
    stale_sockets: list[WebSocket] = []
    stale_ids = [
        session_id
        for session_id, session in relay_sessions.items()
        if session.expires_at <= current
    ]
    for session_id in stale_ids:
        session = relay_sessions.pop(session_id)
        if relay_session_ids_by_room_code.get(session.room_code) == session_id:
            relay_session_ids_by_room_code.pop(session.room_code, None)
        if session.host is not None:
            stale_sockets.append(session.host)
        if session.guest is not None:
            stale_sockets.append(session.guest)
    return stale_sockets


def create_relay_room_code_locked() -> str:
    for _ in range(128):
        room_code = "".join(
            secrets.choice(RELAY_ROOM_CODE_ALPHABET)
            for _ in range(RELAY_ROOM_CODE_LENGTH)
        )
        if room_code not in relay_session_ids_by_room_code:
            return room_code
    raise HTTPException(status_code=503, detail="relay room codes are temporarily unavailable")


def enforce_relay_room_lookup_rate_limit(request: Request) -> None:
    current = now_seconds()
    cutoff = current - RELAY_ROOM_LOOKUP_WINDOW_SECONDS
    lookup_key = request_ip(request) or "unknown"
    attempts = [
        attempted_at
        for attempted_at in relay_room_lookup_attempts.get(lookup_key, [])
        if attempted_at > cutoff
    ]
    if len(attempts) >= RELAY_ROOM_LOOKUP_MAX_ATTEMPTS:
        relay_room_lookup_attempts[lookup_key] = attempts
        raise HTTPException(status_code=429, detail="too many relay room lookups")
    attempts.append(current)
    relay_room_lookup_attempts[lookup_key] = attempts


def enqueue_relay_payload_locked(session: "RelaySession", target_role: str, payload: bytes) -> bool:
    queue = session.pending_host if target_role == "host" else session.pending_guest
    pending_bytes = session.pending_host_bytes if target_role == "host" else session.pending_guest_bytes
    if len(queue) >= RELAY_MAX_PENDING_MESSAGES or pending_bytes + len(payload) > RELAY_MAX_PENDING_BYTES:
        return False
    queue.append(payload)
    if target_role == "host":
        session.pending_host_bytes += len(payload)
    else:
        session.pending_guest_bytes += len(payload)
    return True


async def send_relay_payload(session: "RelaySession", target_role: str, socket: WebSocket, payload: bytes) -> None:
    send_lock = session.host_send_lock if target_role == "host" else session.guest_send_lock
    async with send_lock:
        await socket.send_bytes(payload)


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
    quicPort: int = 0
    quicUrl: str = ""
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


class RelaySessionCreateRequest(BaseModel):
    clientId: str
    clientSecret: str
    friendCode: str
    displayName: str = ""


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


app = FastAPI(title="OpenGarrison API", version="0.2.0")


def openapi_with_relay_websocket() -> dict[str, Any]:
    """Document the WebSocket relay route, which FastAPI omits by default."""
    if app.openapi_schema:
        return app.openapi_schema

    schema = get_openapi(title=app.title, version=app.version, routes=app.routes)
    schema.setdefault("paths", {})["/api/relay/ws/{session_id}/{role}"] = {
        "summary": "Protocol64 relay WebSocket",
        "description": (
            "Binary relay endpoint. Connect with the bearer token returned by "
            "the relay session endpoint. WebSocket routes are represented as an "
            "OpenAPI path item extension because OpenAPI has no WebSocket operation."
        ),
        "x-websocket": True,
        "x-websocket-protocols": ["wss", "ws64"],
        "parameters": [
            {
                "name": "session_id",
                "in": "path",
                "required": True,
                "schema": {"type": "string"},
            },
            {
                "name": "role",
                "in": "path",
                "required": True,
                "schema": {"type": "string", "enum": ["host", "guest"]},
            },
            {
                "name": "token",
                "in": "query",
                "required": True,
                "schema": {"type": "string"},
            },
        ],
    }
    app.openapi_schema = schema
    return schema


app.openapi = openapi_with_relay_websocket


class RelaySession:
    def __init__(
        self,
        session_id: str,
        owner_client_id: str,
        owner_friend_code: str,
        room_code: str,
        host_token: str,
        guest_token: str,
        expires_at: int,
    ):
        self.session_id = session_id
        self.owner_client_id = owner_client_id
        self.owner_friend_code = owner_friend_code
        self.room_code = room_code
        self.host_token = host_token
        self.guest_token = guest_token
        self.expires_at = expires_at
        self.host: WebSocket | None = None
        self.guest: WebSocket | None = None
        self.host_send_lock = asyncio.Lock()
        self.guest_send_lock = asyncio.Lock()
        self.pending_host: list[bytes] = []
        self.pending_guest: list[bytes] = []
        self.pending_host_bytes = 0
        self.pending_guest_bytes = 0


relay_sessions: dict[str, RelaySession] = {}
relay_session_ids_by_room_code: dict[str, str] = {}
relay_room_lookup_attempts: dict[str, list[int]] = {}
relay_sessions_lock = asyncio.Lock()

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
@app.get("/servers.json")
@app.get("/api/servers/servers.json")
@app.get("/API/servers.json")
def get_servers(
    protocolVersion: int | None = None,
    buildVersion: str = "",
    releaseChannel: str = "",
    channel: str = "",
    compatibilityKey: str = "",
) -> dict[str, Any]:
    requested_build_version = clean_text(buildVersion, 64)
    requested_compatibility_key = clean_text(compatibilityKey, 128)
    requested_channel_explicit = bool(clean_text(releaseChannel or channel, 32))
    requested_channel = clean_text(releaseChannel or channel, 32).lower()
    if not requested_channel and requested_compatibility_key:
        compatibility_channel = requested_compatibility_key.split(":", 1)[0].strip().lower()
        if compatibility_channel:
            requested_channel = clean_text(compatibility_channel, 32).lower()

    if not requested_channel:
        # Protocol 64 is the alpha/beta transport line. Some 64.0.0 clients were
        # shipped before releaseChannel was added to registry queries, so keep
        # those clients from silently querying stable and seeing only legacy rows.
        normalized_build_version = requested_build_version.strip().lower()
        if protocolVersion == 64 or normalized_build_version.startswith("64."):
            requested_channel = "alpha"
        else:
            requested_channel = "stable"

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
                "quicPort": row["quic_port"],
                "quicUrl": row["quic_url"],
                "private": bool(row["private"]),
                "map": row["map"],
                "mode": row["mode"],
                "players": row["players"],
                "maxPlayers": row["max_players"],
                "spectators": row["spectators"],
                "protocolVersion": row["protocol_version"],
                "buildVersion": row["build_version"],
                "releaseChannel": response_release_channel(row, requested_channel_explicit, protocolVersion, requested_build_version),
                "compatibilityKey": response_compatibility_key(row, requested_channel_explicit, protocolVersion, requested_build_version),
                "lastSeenIso": iso_from_seconds(row["last_seen"]),
            }
            for row in rows
        ],
        "generatedAt": iso_from_seconds(now_seconds()),
    }


def response_release_channel(
    row: sqlite3.Row,
    requested_channel_explicit: bool,
    protocol_version: int | None,
    requested_build_version: str,
) -> str:
    if should_mask_alpha_channel_for_legacy_client(row, requested_channel_explicit, protocol_version, requested_build_version):
        return "stable"

    return row["release_channel"]


def response_compatibility_key(
    row: sqlite3.Row,
    requested_channel_explicit: bool,
    protocol_version: int | None,
    requested_build_version: str,
) -> str:
    if should_mask_alpha_channel_for_legacy_client(row, requested_channel_explicit, protocol_version, requested_build_version):
        build_version = clean_text(row["build_version"], 64) or clean_text(requested_build_version, 64)
        protocol = clamp_int(row["protocol_version"], 0, 999999)
        return f"stable:{build_version}:{protocol}"

    return row["compatibility_key"]


def should_mask_alpha_channel_for_legacy_client(
    row: sqlite3.Row,
    requested_channel_explicit: bool,
    protocol_version: int | None,
    requested_build_version: str,
) -> bool:
    if requested_channel_explicit:
        return False

    if clean_text(row["release_channel"], 32).lower() != "alpha":
        return False

    normalized_build_version = clean_text(requested_build_version, 64).lower()
    return protocol_version == 64 or normalized_build_version.startswith("64.")


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
        quic_port = clamp_int(payload.quicPort, 0, 65535)
        quic_url = clean_text(payload.quicUrl, 512)
        build_version = clean_text(payload.buildVersion, 64)
        release_channel = clean_text(payload.releaseChannel, 32).lower() or "stable"
        compatibility_key = clean_text(payload.compatibilityKey, 128)
        server_id = clean_text(payload.serverId, 512) or f"og2:{host.lower()}:{udp_port}:{websocket_port}:{websocket_url}:{quic_port}:{quic_url}"
        current = now_seconds()
        db.execute(
            """
            INSERT INTO servers (
                server_id, name, host, udp_port, websocket_port, websocket_url, quic_port, quic_url, private,
                map, mode, players, max_players, spectators, protocol_version,
                build_version, release_channel, compatibility_key, request_ip, last_seen
            )
            VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            ON CONFLICT(server_id) DO UPDATE SET
                name = excluded.name,
                host = excluded.host,
                udp_port = excluded.udp_port,
                websocket_port = excluded.websocket_port,
                websocket_url = excluded.websocket_url,
                quic_port = excluded.quic_port,
                quic_url = excluded.quic_url,
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
                quic_port,
                quic_url,
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


@app.post("/api/relay/session")
async def create_relay_session(payload: RelaySessionCreateRequest, request: Request) -> dict[str, str]:
    friend_code = normalize_friend_code(payload.friendCode)
    if not friend_code:
        raise HTTPException(status_code=400, detail="invalid friend code")

    client_id = clean_text(payload.clientId, 64)
    display_name = clean_text(payload.displayName, 64) or "Player"
    with connect_db() as db:
        verify_client(db, client_id, friend_code, payload.clientSecret, display_name)

    current = now_seconds()
    session_id = secrets.token_urlsafe(18)
    host_token = secrets.token_urlsafe(32)
    guest_token = secrets.token_urlsafe(32)
    async with relay_sessions_lock:
        stale_sockets = prune_relay_sessions_locked(current)
        previous_ids = [
            existing_id
            for existing_id, existing in relay_sessions.items()
            if existing.owner_client_id == client_id
        ]
        for previous_id in previous_ids:
            previous = relay_sessions.pop(previous_id)
            if relay_session_ids_by_room_code.get(previous.room_code) == previous_id:
                relay_session_ids_by_room_code.pop(previous.room_code, None)
            if previous.host is not None:
                stale_sockets.append(previous.host)
            if previous.guest is not None:
                stale_sockets.append(previous.guest)
        room_code = create_relay_room_code_locked()
        session = RelaySession(
            session_id,
            client_id,
            friend_code,
            room_code,
            host_token,
            guest_token,
            current + RELAY_SESSION_TTL_SECONDS,
        )
        relay_sessions[session_id] = session
        relay_session_ids_by_room_code[room_code] = session_id

    for stale_socket in stale_sockets:
        try:
            await stale_socket.close(code=1001, reason="Relay session expired.")
        except Exception:
            pass

    scheme, authority = relay_public_origin(request)
    return {
        "sessionId": session_id,
        "roomCode": room_code,
        "hostWebSocketUrl": relay_url(scheme, authority, session_id, "host", host_token, protocol64=False),
        "guestWebSocketUrl": relay_url(scheme, authority, session_id, "guest", guest_token, protocol64=True),
        "expiresAtIso": iso_from_seconds(session.expires_at),
    }


async def resolve_relay_session(
    request: Request,
    *,
    room_code: str = "",
    friend_code: str = "",
) -> dict[str, str]:
    current = now_seconds()
    async with relay_sessions_lock:
        stale_sockets = prune_relay_sessions_locked(current)
        if room_code:
            session_id = relay_session_ids_by_room_code.get(room_code, "")
            session = relay_sessions.get(session_id)
        else:
            session = next(
                (
                    candidate
                    for candidate in relay_sessions.values()
                    if candidate.owner_friend_code == friend_code
                ),
                None,
            )
        host_connected = session is not None and session.host is not None

    for stale_socket in stale_sockets:
        try:
            await stale_socket.close(code=1001, reason="Relay session expired.")
        except Exception:
            pass

    if session is None or session.expires_at <= current:
        raise HTTPException(status_code=404, detail="relay room not found or expired")
    if not host_connected:
        raise HTTPException(status_code=409, detail="relay room is still starting")

    scheme, authority = relay_public_origin(request)
    return {
        "roomCode": session.room_code,
        "friendCode": session.owner_friend_code,
        "guestWebSocketUrl": relay_url(
            scheme,
            authority,
            session.session_id,
            "guest",
            session.guest_token,
            protocol64=True,
        ),
        "expiresAtIso": iso_from_seconds(session.expires_at),
    }


@app.get("/api/relay/room/{room_code}")
async def resolve_relay_room(room_code: str, request: Request) -> dict[str, str]:
    enforce_relay_room_lookup_rate_limit(request)
    normalized_room_code = normalize_relay_room_code(room_code)
    if not normalized_room_code:
        raise HTTPException(status_code=404, detail="relay room not found or expired")
    return await resolve_relay_session(request, room_code=normalized_room_code)


@app.get("/api/relay/friend/{friend_code}")
async def resolve_relay_room_by_friend_code(friend_code: str, request: Request) -> dict[str, str]:
    enforce_relay_room_lookup_rate_limit(request)
    normalized_friend_code = normalize_friend_code(friend_code)
    if not normalized_friend_code:
        raise HTTPException(status_code=404, detail="relay room not found or expired")
    return await resolve_relay_session(request, friend_code=normalized_friend_code)


@app.websocket("/api/relay/ws/{session_id}/{role}")
async def relay_websocket(websocket: WebSocket, session_id: str, role: str, token: str = "") -> None:
    if role not in ("host", "guest"):
        await websocket.close(code=4404, reason="Unknown relay role.")
        return

    current = now_seconds()
    async with relay_sessions_lock:
        session = relay_sessions.get(session_id)
        expected_token = "" if session is None else (session.host_token if role == "host" else session.guest_token)
        authorized = (
            session is not None
            and session.expires_at > current
            and bool(token)
            and secrets.compare_digest(expected_token, token)
        )
        if not authorized:
            session = None

    if session is None:
        await websocket.close(code=4403, reason="Relay session is invalid or expired.")
        return

    await websocket.accept()
    async with relay_sessions_lock:
        if relay_sessions.get(session_id) is not session or session.expires_at <= now_seconds():
            old_socket = None
            pending: list[bytes] = []
            accepted = False
        else:
            old_socket = session.host if role == "host" else session.guest
            if role == "host":
                session.host = websocket
                pending = session.pending_host
                session.pending_host = []
                session.pending_host_bytes = 0
            else:
                session.guest = websocket
                pending = session.pending_guest
                session.pending_guest = []
                session.pending_guest_bytes = 0
            accepted = True

    if not accepted:
        await websocket.close(code=4403, reason="Relay session expired.")
        return

    if old_socket is not None and old_socket is not websocket:
        try:
            await old_socket.close(code=1012, reason="Relay role reconnected.")
        except Exception:
            pass

    try:
        for queued_payload in pending:
            await send_relay_payload(session, role, websocket, queued_payload)

        while True:
            message = await websocket.receive()
            if message.get("type") == "websocket.disconnect":
                break
            payload_bytes = message.get("bytes")
            if payload_bytes is None:
                await websocket.close(code=1003, reason="Binary protocol messages are required.")
                break
            if len(payload_bytes) == 0:
                continue
            if len(payload_bytes) > RELAY_MAX_MESSAGE_BYTES:
                await websocket.close(code=1009, reason="Relay message exceeded the size limit.")
                break

            target_role = "guest" if role == "host" else "host"
            async with relay_sessions_lock:
                if relay_sessions.get(session_id) is not session or session.expires_at <= now_seconds():
                    target_socket = None
                    queued = False
                else:
                    target_socket = session.guest if target_role == "guest" else session.host
                    queued = target_socket is None and enqueue_relay_payload_locked(session, target_role, payload_bytes)

            if target_socket is None:
                if not queued:
                    await websocket.close(code=1013, reason="Relay peer queue is full or the session expired.")
                    break
                continue

            try:
                await send_relay_payload(session, target_role, target_socket, payload_bytes)
            except Exception:
                async with relay_sessions_lock:
                    if target_role == "host" and session.host is target_socket:
                        session.host = None
                    elif target_role == "guest" and session.guest is target_socket:
                        session.guest = None
                    queued = enqueue_relay_payload_locked(session, target_role, payload_bytes)
                if not queued:
                    await websocket.close(code=1013, reason="Relay peer queue is full.")
                    break
    except WebSocketDisconnect:
        pass
    finally:
        async with relay_sessions_lock:
            if role == "host" and session.host is websocket:
                session.host = None
                counterpart = session.guest
                session.guest = None
            elif role == "guest" and session.guest is websocket:
                session.guest = None
                counterpart = session.host
                session.host = None
            else:
                counterpart = None
            if counterpart is not None:
                session.pending_host = []
                session.pending_guest = []
                session.pending_host_bytes = 0
                session.pending_guest_bytes = 0

        if counterpart is not None:
            try:
                await counterpart.close(code=1012, reason="Relay peer disconnected; reconnecting pair.")
            except Exception:
                pass


@app.post("/api/presence/heartbeat")
def heartbeat_presence(payload: PresenceHeartbeatRequest, request: Request) -> dict[str, str]:
    friend_code = normalize_friend_code(payload.friendCode)
    if not friend_code:
        raise HTTPException(status_code=400, detail="invalid friend code")

    client_id = clean_text(payload.clientId, 64)
    display_name = clean_text(payload.displayName, 64) or "Player"
    status = clean_text(payload.status, 32) or "menu"
    udp_port = clamp_int(payload.udpPort, 0, 65535)
    websocket_port = clamp_int(payload.webSocketPort, 0, 65535)
    websocket_url = clean_text(payload.webSocketUrl, 512)
    host = clean_text(payload.host, 255)
    if payload.joinable and not host and (udp_port > 0 or websocket_port > 0 or websocket_url):
        host = request_ip(request)
    joinable = bool(payload.joinable and host and (udp_port > 0 or websocket_port > 0 or websocket_url))
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
                host,
                udp_port,
                websocket_port,
                websocket_url,
                1 if joinable else 0,
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
