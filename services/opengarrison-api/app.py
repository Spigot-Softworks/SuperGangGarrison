from __future__ import annotations

import hashlib
import hmac
import asyncio
import os
import re
import secrets
import sqlite3
import time
import uuid
from contextlib import contextmanager
from pathlib import Path
from typing import Any
from urllib.parse import quote, urlsplit

from fastapi import Depends, FastAPI, HTTPException, Request, WebSocket, WebSocketDisconnect
from fastapi.middleware.cors import CORSMiddleware
from fastapi.openapi.utils import get_openapi
from pydantic import BaseModel
from reward_authority import require_reward_authority
from run_verification import install_routes as install_run_verification_routes


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
ACCOUNT_CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
ACCOUNT_CODE_LENGTH = 8
RECOVERY_KEY_LENGTH = 8
LOGIN_FAILURE_WINDOW_SECONDS = 15 * 60
LOGIN_FAILURE_BLOCK_SECONDS = 5 * 60
LOGIN_FAILURE_LIMIT = 5
GAMEPLAY_SESSION_TTL_SECONDS = 6 * 60 * 60
MAX_STAT_EVENT_POINTS = 100_000
MAX_STAT_EVENT_CREDITS = MAX_STAT_EVENT_POINTS
FRIEND_CODE_RE = re.compile(r"^OG2-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}(?:-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4})?(?:-[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4})?$")
RELAY_ROOM_CODE_RE = re.compile(r"^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{4}$")
RECOVERY_KEY_RE = re.compile(r"^[ABCDEFGHJKLMNPQRSTUVWXYZ23456789]{8}$")
LAST_TO_DIE_SURVIVOR_IDS = {
    "ltd.survivor.soldier",
    "ltd.survivor.demoknight",
    "ltd.survivor.engineer",
    "ltd.survivor.spy",
    "ltd.survivor.medic",
    "ltd.survivor.sniper",
}


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


def normalize_client_id(value: str | None) -> str:
    client_id = clean_text(value, 64)
    if not client_id:
        return ""
    try:
        return uuid.UUID(client_id).hex
    except ValueError:
        return client_id


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


def normalize_recovery_key(value: str | None) -> str:
    if not value:
        return ""
    compact = "".join(ch for ch in value.upper() if ch.isalnum())
    return compact if RECOVERY_KEY_RE.fullmatch(compact) else ""


def format_recovery_key(value: str) -> str:
    normalized = normalize_recovery_key(value)
    return f"{normalized[:4]}-{normalized[4:]}" if normalized else ""


def secret_hash(secret: str) -> str:
    return hashlib.sha256(secret.encode("utf-8")).hexdigest()


def recovery_key_hash(recovery_key: str, salt: bytes) -> str:
    return hashlib.scrypt(
        recovery_key.encode("ascii"),
        salt=salt,
        n=16384,
        r=8,
        p=1,
        dklen=32,
    ).hex()


def create_account_id() -> str:
    return secrets.token_hex(16)


def create_unique_friend_code(db: sqlite3.Connection) -> str:
    for _ in range(128):
        compact = "".join(secrets.choice(ACCOUNT_CODE_ALPHABET) for _ in range(ACCOUNT_CODE_LENGTH))
        friend_code = normalize_friend_code(compact)
        existing = db.execute(
            "SELECT 1 FROM account_friend_codes WHERE friend_code = ?",
            (friend_code,),
        ).fetchone()
        if existing is None:
            return friend_code
    raise HTTPException(status_code=503, detail="friend codes are temporarily unavailable")


def create_recovery_key() -> str:
    compact = "".join(secrets.choice(ACCOUNT_CODE_ALPHABET) for _ in range(RECOVERY_KEY_LENGTH))
    return format_recovery_key(compact)


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

            CREATE TABLE IF NOT EXISTS accounts (
                account_id TEXT PRIMARY KEY,
                primary_friend_code TEXT NOT NULL UNIQUE,
                display_name TEXT NOT NULL DEFAULT '',
                player_card_json TEXT NOT NULL DEFAULT '',
                lifetime_points INTEGER NOT NULL DEFAULT 0,
                wallet_balance INTEGER NOT NULL DEFAULT 0,
                profile_revision INTEGER NOT NULL DEFAULT 0,
                created_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS account_friend_codes (
                friend_code TEXT PRIMARY KEY,
                account_id TEXT NOT NULL,
                is_primary INTEGER NOT NULL DEFAULT 0,
                created_at INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_account_friend_codes_account
                ON account_friend_codes(account_id);

            CREATE TABLE IF NOT EXISTS client_devices (
                client_id TEXT PRIMARY KEY,
                account_id TEXT NOT NULL,
                secret_hash TEXT NOT NULL,
                display_name TEXT NOT NULL DEFAULT '',
                player_card_json TEXT NOT NULL DEFAULT '',
                created_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL,
                revoked_at INTEGER
            );

            CREATE INDEX IF NOT EXISTS idx_client_devices_account
                ON client_devices(account_id);

            CREATE TABLE IF NOT EXISTS account_recovery_credentials (
                account_id TEXT PRIMARY KEY,
                recovery_salt TEXT NOT NULL,
                recovery_hash TEXT NOT NULL,
                created_at INTEGER NOT NULL,
                updated_at INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS account_login_attempts (
                attempt_key TEXT PRIMARY KEY,
                window_started_at INTEGER NOT NULL,
                failure_count INTEGER NOT NULL,
                blocked_until INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS gameplay_sessions (
                token_hash TEXT PRIMARY KEY,
                account_id TEXT NOT NULL,
                client_id TEXT NOT NULL,
                issued_at INTEGER NOT NULL,
                expires_at INTEGER NOT NULL,
                revoked_at INTEGER
            );

            CREATE INDEX IF NOT EXISTS idx_gameplay_sessions_account
                ON gameplay_sessions(account_id);

            CREATE TABLE IF NOT EXISTS matches (
                match_id TEXT PRIMARY KEY,
                server_id TEXT NOT NULL DEFAULT '',
                map_name TEXT NOT NULL DEFAULT '',
                mode TEXT NOT NULL DEFAULT '',
                started_at INTEGER NOT NULL,
                ended_at INTEGER,
                winner TEXT NOT NULL DEFAULT ''
            );

            CREATE TABLE IF NOT EXISTS stat_events (
                event_id TEXT PRIMARY KEY,
                account_id TEXT NOT NULL,
                match_id TEXT NOT NULL DEFAULT '',
                source_frame INTEGER NOT NULL DEFAULT 0,
                event_type TEXT NOT NULL,
                raw_value INTEGER NOT NULL DEFAULT 0,
                points_delta INTEGER NOT NULL DEFAULT 0,
                credits_delta INTEGER NOT NULL DEFAULT 0,
                policy_version INTEGER NOT NULL DEFAULT 1,
                created_at INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_stat_events_account
                ON stat_events(account_id, created_at);

            CREATE TABLE IF NOT EXISTS last_to_die_runs (
                submission_id TEXT PRIMARY KEY,
                run_id TEXT NOT NULL,
                account_id TEXT NOT NULL,
                score_units INTEGER NOT NULL DEFAULT 0,
                round_number INTEGER NOT NULL DEFAULT 1,
                difficulty TEXT NOT NULL DEFAULT 'standard',
                survivor_id TEXT NOT NULL DEFAULT '',
                policy_version INTEGER NOT NULL DEFAULT 1,
                created_at INTEGER NOT NULL,
                UNIQUE(run_id, account_id)
            );

            CREATE INDEX IF NOT EXISTS idx_last_to_die_runs_account
                ON last_to_die_runs(account_id, score_units DESC, round_number DESC);

            CREATE INDEX IF NOT EXISTS idx_last_to_die_runs_round
                ON last_to_die_runs(round_number DESC, score_units DESC);

            CREATE TABLE IF NOT EXISTS wallet_transactions (
                transaction_id TEXT PRIMARY KEY,
                account_id TEXT NOT NULL,
                kind TEXT NOT NULL,
                amount INTEGER NOT NULL,
                balance_after INTEGER NOT NULL,
                reference_id TEXT NOT NULL UNIQUE,
                created_at INTEGER NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_wallet_transactions_account
                ON wallet_transactions(account_id, created_at);

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
        ensure_column(db, "last_to_die_runs", "survivor_id", "TEXT NOT NULL DEFAULT ''")
        db.execute(
            "CREATE INDEX IF NOT EXISTS idx_last_to_die_runs_survivor "
            "ON last_to_die_runs(survivor_id, score_units DESC, round_number DESC)"
        )
        migrate_legacy_clients(db)
        migrate_client_id_aliases(db)


def ensure_column(db: sqlite3.Connection, table: str, column: str, definition: str) -> None:
    existing_columns = {
        row["name"]
        for row in db.execute(f"PRAGMA table_info({table})").fetchall()
    }
    if column not in existing_columns:
        db.execute(f"ALTER TABLE {table} ADD COLUMN {column} {definition}")


def migrate_legacy_clients(db: sqlite3.Connection) -> None:
    """Copy pre-account client rows into the account/device model idempotently."""
    rows = db.execute(
        """
        SELECT client_id, friend_code, secret_hash, display_name, player_card_json, created_at, updated_at
        FROM clients
        """
    ).fetchall()
    for row in rows:
        client_id = normalize_client_id(str(row["client_id"]))
        existing_device = db.execute(
            "SELECT account_id FROM client_devices WHERE client_id = ?",
            (client_id,),
        ).fetchone()
        if existing_device is not None:
            continue

        friend_code = normalize_friend_code(row["friend_code"])
        if not friend_code:
            continue

        alias = db.execute(
            "SELECT account_id FROM account_friend_codes WHERE friend_code = ?",
            (friend_code,),
        ).fetchone()
        account_id = alias["account_id"] if alias is not None else (
            "legacy-" + hashlib.sha256(row["client_id"].encode("utf-8")).hexdigest()[:32]
        )
        created_at = int(row["created_at"])
        updated_at = int(row["updated_at"])
        db.execute(
            """
            INSERT OR IGNORE INTO accounts (
                account_id, primary_friend_code, display_name, player_card_json,
                lifetime_points, wallet_balance, profile_revision, created_at, updated_at
            ) VALUES (?, ?, ?, ?, 0, 0, 0, ?, ?)
            """,
            (
                account_id,
                friend_code,
                row["display_name"],
                row["player_card_json"],
                created_at,
                updated_at,
            ),
        )
        db.execute(
            """
            INSERT OR IGNORE INTO account_friend_codes (friend_code, account_id, is_primary, created_at)
            VALUES (?, ?, 1, ?)
            """,
            (friend_code, account_id, created_at),
        )
        db.execute(
            """
            INSERT INTO client_devices (
                client_id, account_id, secret_hash, display_name, player_card_json,
                created_at, updated_at, revoked_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, NULL)
            """,
            (
                client_id,
                account_id,
                row["secret_hash"],
                row["display_name"],
                row["player_card_json"],
                created_at,
                updated_at,
            ),
        )


def migrate_client_id_aliases(db: sqlite3.Connection) -> None:
    """Add canonical UUID spellings for devices migrated by older API builds."""
    rows = db.execute(
        """
        SELECT client_id, account_id, secret_hash, display_name, player_card_json,
               created_at, updated_at, revoked_at
        FROM client_devices
        """
    ).fetchall()
    for row in rows:
        client_id = str(row["client_id"])
        canonical_id = normalize_client_id(client_id)
        if canonical_id == client_id:
            continue
        db.execute(
            """
            INSERT OR IGNORE INTO client_devices (
                client_id, account_id, secret_hash, display_name, player_card_json,
                created_at, updated_at, revoked_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                canonical_id,
                row["account_id"],
                row["secret_hash"],
                row["display_name"],
                row["player_card_json"],
                row["created_at"],
                row["updated_at"],
                row["revoked_at"],
            ),
        )


def get_account_id_for_friend_code(db: sqlite3.Connection, friend_code: str) -> str:
    row = db.execute(
        "SELECT account_id FROM account_friend_codes WHERE friend_code = ?",
        (friend_code,),
    ).fetchone()
    return "" if row is None else str(row["account_id"])


def get_primary_friend_code(db: sqlite3.Connection, account_id: str) -> str:
    row = db.execute(
        "SELECT primary_friend_code FROM accounts WHERE account_id = ?",
        (account_id,),
    ).fetchone()
    return "" if row is None else str(row["primary_friend_code"])


def canonicalize_friend_code(db: sqlite3.Connection, friend_code: str) -> str:
    account_id = get_account_id_for_friend_code(db, friend_code)
    return get_primary_friend_code(db, account_id) if account_id else friend_code


def get_account_friend_codes(db: sqlite3.Connection, account_id: str) -> list[str]:
    return [
        str(row["friend_code"])
        for row in db.execute(
            "SELECT friend_code FROM account_friend_codes WHERE account_id = ?",
            (account_id,),
        ).fetchall()
    ]


def delete_empty_implicit_account(db: sqlite3.Connection, account_id: str) -> None:
    """Remove an abandoned first-run account after its only device is linked elsewhere."""
    if not account_id:
        return
    has_device = db.execute(
        "SELECT 1 FROM client_devices WHERE account_id = ? LIMIT 1",
        (account_id,),
    ).fetchone()
    has_recovery = db.execute(
        "SELECT 1 FROM account_recovery_credentials WHERE account_id = ? LIMIT 1",
        (account_id,),
    ).fetchone()
    has_stats = db.execute(
        "SELECT 1 FROM stat_events WHERE account_id = ? LIMIT 1",
        (account_id,),
    ).fetchone()
    has_wallet = db.execute(
        "SELECT 1 FROM wallet_transactions WHERE account_id = ? LIMIT 1",
        (account_id,),
    ).fetchone()
    has_last_to_die_run = db.execute(
        "SELECT 1 FROM last_to_die_runs WHERE account_id = ? LIMIT 1",
        (account_id,),
    ).fetchone()
    if (
        has_device is not None
        or has_recovery is not None
        or has_stats is not None
        or has_wallet is not None
        or has_last_to_die_run is not None
    ):
        return
    db.execute("DELETE FROM gameplay_sessions WHERE account_id = ?", (account_id,))
    db.execute("DELETE FROM account_friend_codes WHERE account_id = ?", (account_id,))
    db.execute("DELETE FROM accounts WHERE account_id = ?", (account_id,))


def serialize_account_profile(db: sqlite3.Connection, account_id: str) -> dict[str, Any]:
    reconcile_account_totals(db, account_id)
    row = db.execute(
        """
        SELECT account_id, primary_friend_code, display_name, player_card_json,
               lifetime_points, wallet_balance, profile_revision, created_at, updated_at
        FROM accounts WHERE account_id = ?
        """,
        (account_id,),
    ).fetchone()
    if row is None:
        raise HTTPException(status_code=404, detail="account not found")

    recovery = db.execute(
        "SELECT 1 FROM account_recovery_credentials WHERE account_id = ?",
        (account_id,),
    ).fetchone()
    return {
        "accountId": row["account_id"],
        "friendCode": row["primary_friend_code"],
        "displayName": row["display_name"],
        "playerCard": row["player_card_json"],
        "lifetimePoints": max(0, int(row["lifetime_points"])),
        "walletBalance": max(0, int(row["wallet_balance"])),
        "profileRevision": max(0, int(row["profile_revision"])),
        "isProtected": recovery is not None,
        "createdAtIso": iso_from_seconds(int(row["created_at"])),
        "updatedAtIso": iso_from_seconds(int(row["updated_at"])),
    }


def reconcile_account_totals(db: sqlite3.Connection, account_id: str) -> None:
    row = db.execute(
        "SELECT lifetime_points, wallet_balance FROM accounts WHERE account_id = ?",
        (account_id,),
    ).fetchone()
    if row is None:
        return
    points_row = db.execute(
        "SELECT COALESCE(SUM(points_delta), 0) AS total FROM stat_events WHERE account_id = ?",
        (account_id,),
    ).fetchone()
    wallet_row = db.execute(
        "SELECT COALESCE(SUM(amount), 0) AS total FROM wallet_transactions WHERE account_id = ?",
        (account_id,),
    ).fetchone()
    lifetime_points = max(0, int(points_row["total"]))
    wallet_balance = max(0, int(wallet_row["total"]))
    if lifetime_points == int(row["lifetime_points"]) and wallet_balance == int(row["wallet_balance"]):
        return
    current = now_seconds()
    db.execute(
        """
        UPDATE accounts SET lifetime_points = ?, wallet_balance = ?,
            profile_revision = profile_revision + 1, updated_at = ?
        WHERE account_id = ?
        """,
        (lifetime_points, wallet_balance, current, account_id),
    )


def validate_gameplay_session(db: sqlite3.Connection, token: str) -> sqlite3.Row:
    if not token:
        raise HTTPException(status_code=403, detail="invalid gameplay session")
    current = now_seconds()
    db.execute("DELETE FROM gameplay_sessions WHERE expires_at <= ?", (current,))
    row = db.execute(
        """
        SELECT token_hash, account_id, client_id, issued_at, expires_at, revoked_at
        FROM gameplay_sessions WHERE token_hash = ?
        """,
        (secret_hash(token),),
    ).fetchone()
    if row is None or row["revoked_at"] is not None or int(row["expires_at"]) <= current:
        raise HTTPException(status_code=403, detail="invalid gameplay session")
    device = db.execute(
        "SELECT revoked_at FROM client_devices WHERE client_id = ? AND account_id = ?",
        (row["client_id"], row["account_id"]),
    ).fetchone()
    if device is None or device["revoked_at"] is not None:
        raise HTTPException(status_code=403, detail="invalid gameplay session")
    return row


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
) -> str:
    client_id = normalize_client_id(client_id)
    if not client_id or not client_secret or not friend_code:
        raise HTTPException(status_code=400, detail="client identity is required")

    current = now_seconds()
    hashed_secret = secret_hash(client_secret)
    existing = db.execute(
        """
        SELECT client_id, account_id, secret_hash, revoked_at
        FROM client_devices WHERE client_id = ?
        """,
        (client_id,),
    ).fetchone()
    clean_player_card = clean_json_text(player_card_json)

    if existing is None:
        if get_account_id_for_friend_code(db, friend_code):
            raise HTTPException(status_code=409, detail="friend code is already registered")

        account_id = create_account_id()
        try:
            db.execute(
                """
                INSERT INTO accounts (
                    account_id, primary_friend_code, display_name, player_card_json,
                    lifetime_points, wallet_balance, profile_revision, created_at, updated_at
                ) VALUES (?, ?, ?, ?, 0, 0, 0, ?, ?)
                """,
                (account_id, friend_code, display_name, clean_player_card, current, current),
            )
            db.execute(
                """
                INSERT INTO account_friend_codes (friend_code, account_id, is_primary, created_at)
                VALUES (?, ?, 1, ?)
                """,
                (friend_code, account_id, current),
            )
            db.execute(
                """
                INSERT INTO client_devices (
                    client_id, account_id, secret_hash, display_name, player_card_json,
                    created_at, updated_at, revoked_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, NULL)
                """,
                (client_id, account_id, hashed_secret, display_name, clean_player_card, current, current),
            )
        except sqlite3.IntegrityError as exc:
            raise HTTPException(status_code=409, detail="client identity is already registered") from exc
        return account_id

    if existing["revoked_at"] is not None:
        raise HTTPException(status_code=403, detail="client device is revoked")
    if not hmac.compare_digest(str(existing["secret_hash"]), hashed_secret):
        raise HTTPException(status_code=403, detail="client secret mismatch")

    account_id = str(existing["account_id"])
    requested_code_account_id = get_account_id_for_friend_code(db, friend_code)
    if requested_code_account_id and requested_code_account_id != account_id:
        raise HTTPException(status_code=409, detail="friend code is already registered")
    if not requested_code_account_id:
        db.execute(
            """
            INSERT INTO account_friend_codes (friend_code, account_id, is_primary, created_at)
            VALUES (?, ?, 0, ?)
            """,
            (friend_code, account_id, current),
        )

    db.execute(
        """
        UPDATE client_devices SET
            display_name = CASE WHEN ? <> '' THEN ? ELSE display_name END,
            player_card_json = CASE WHEN ? <> '' THEN ? ELSE player_card_json END,
            updated_at = ?
        WHERE client_id = ?
        """,
        (
            display_name,
            display_name,
            clean_player_card,
            clean_player_card,
            current,
            client_id,
        ),
    )

    account = db.execute(
        "SELECT display_name, player_card_json FROM accounts WHERE account_id = ?",
        (account_id,),
    ).fetchone()
    if account is None:
        raise HTTPException(status_code=409, detail="client account is unavailable")
    next_display_name = display_name or str(account["display_name"])
    next_player_card = clean_player_card or str(account["player_card_json"])
    if next_display_name != account["display_name"] or next_player_card != account["player_card_json"]:
        db.execute(
            """
            UPDATE accounts SET display_name = ?, player_card_json = ?,
                profile_revision = profile_revision + 1, updated_at = ?
            WHERE account_id = ?
            """,
            (next_display_name, next_player_card, current, account_id),
        )
    return account_id


def get_friend_display_name(db: sqlite3.Connection, friend_code: str) -> str:
    row = db.execute(
        """
        SELECT a.display_name
        FROM account_friend_codes afc
        JOIN accounts a ON a.account_id = afc.account_id
        WHERE afc.friend_code = ?
        LIMIT 1
        """,
        (friend_code,),
    ).fetchone()
    return clean_text(row["display_name"], 64) if row is not None else ""


def serialize_friend_request(db: sqlite3.Connection, row: sqlite3.Row, own_friend_code: str) -> dict[str, Any]:
    from_code = canonicalize_friend_code(db, str(row["from_friend_code"]))
    to_code = canonicalize_friend_code(db, str(row["to_friend_code"]))
    incoming = to_code == own_friend_code
    other_code = from_code if incoming else to_code
    return {
        "requestId": row["request_id"],
        "direction": "incoming" if incoming else "outgoing",
        "status": row["status"],
        "friendCode": other_code,
        "displayName": get_friend_display_name(db, other_code),
        "createdAtIso": iso_from_seconds(row["created_at"]),
        "updatedAtIso": iso_from_seconds(row["updated_at"]),
    }


def serialize_direct_message(db: sqlite3.Connection, row: sqlite3.Row, own_friend_code: str) -> dict[str, Any]:
    sender_code = canonicalize_friend_code(db, str(row["sender_friend_code"]))
    recipient_code = canonicalize_friend_code(db, str(row["recipient_friend_code"]))
    outgoing = sender_code == own_friend_code
    return {
        "messageId": row["message_id"],
        "direction": "outgoing" if outgoing else "incoming",
        "friendCode": recipient_code if outgoing else sender_code,
        "displayName": row["sender_display_name"],
        "text": row["text"],
        "createdAtIso": iso_from_seconds(row["created_at"]),
    }


def login_attempt_key(request: Request, account_id: str) -> str:
    material = f"{request_ip(request)}\n{account_id}".encode("utf-8")
    return hashlib.sha256(material).hexdigest()


def enforce_account_login_rate_limit(db: sqlite3.Connection, attempt_key: str) -> None:
    current = now_seconds()
    row = db.execute(
        """
        SELECT window_started_at, failure_count, blocked_until
        FROM account_login_attempts WHERE attempt_key = ?
        """,
        (attempt_key,),
    ).fetchone()
    if row is None:
        return
    if int(row["blocked_until"]) > current:
        raise HTTPException(status_code=429, detail="too many account login attempts")
    if int(row["window_started_at"]) <= current - LOGIN_FAILURE_WINDOW_SECONDS:
        db.execute("DELETE FROM account_login_attempts WHERE attempt_key = ?", (attempt_key,))


def record_account_login_failure(db: sqlite3.Connection, attempt_key: str) -> None:
    current = now_seconds()
    row = db.execute(
        "SELECT window_started_at, failure_count FROM account_login_attempts WHERE attempt_key = ?",
        (attempt_key,),
    ).fetchone()
    if row is None or int(row["window_started_at"]) <= current - LOGIN_FAILURE_WINDOW_SECONDS:
        window_started_at = current
        failure_count = 1
    else:
        window_started_at = int(row["window_started_at"])
        failure_count = int(row["failure_count"]) + 1
    blocked_until = current + LOGIN_FAILURE_BLOCK_SECONDS if failure_count >= LOGIN_FAILURE_LIMIT else 0
    db.execute(
        """
        INSERT INTO account_login_attempts (attempt_key, window_started_at, failure_count, blocked_until)
        VALUES (?, ?, ?, ?)
        ON CONFLICT(attempt_key) DO UPDATE SET
            window_started_at = excluded.window_started_at,
            failure_count = excluded.failure_count,
            blocked_until = excluded.blocked_until
        """,
        (attempt_key, window_started_at, failure_count, blocked_until),
    )


def verify_account_recovery_key(
    db: sqlite3.Connection,
    account_id: str,
    recovery_key: str,
    request: Request,
) -> None:
    attempt_key = login_attempt_key(request, account_id)
    enforce_account_login_rate_limit(db, attempt_key)
    credential = db.execute(
        """
        SELECT recovery_salt, recovery_hash
        FROM account_recovery_credentials WHERE account_id = ?
        """,
        (account_id,),
    ).fetchone()
    valid = False
    if credential is not None:
        try:
            salt = bytes.fromhex(str(credential["recovery_salt"]))
            candidate_hash = recovery_key_hash(recovery_key, salt)
            valid = hmac.compare_digest(str(credential["recovery_hash"]), candidate_hash)
        except ValueError:
            valid = False
    if not valid:
        record_account_login_failure(db, attempt_key)
        # The surrounding request transaction is rolled back when HTTPException
        # leaves connect_db(), but failed-login counters must survive that error.
        db.commit()
        raise HTTPException(status_code=403, detail="invalid account credentials")
    db.execute("DELETE FROM account_login_attempts WHERE attempt_key = ?", (attempt_key,))


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


class AccountAuthenticatedRequest(BaseModel):
    clientId: str
    clientSecret: str
    friendCode: str


class AccountLoginRequest(BaseModel):
    clientId: str
    clientSecret: str
    friendCode: str
    recoveryKey: str


class AccountProfileRequest(AccountAuthenticatedRequest):
    pass


class GameplaySessionValidateRequest(BaseModel):
    gameplayToken: str


class StatAwardRequest(BaseModel):
    gameplayToken: str
    eventId: str
    matchId: str = ""
    sourceFrame: int = 0
    eventType: str
    rawValue: int = 0
    pointsDelta: int = 0
    creditsDelta: int = 0
    policyVersion: int = 1


class LastToDieRunRequest(BaseModel):
    gameplayToken: str
    submissionId: str
    runId: str
    scoreUnits: int = 0
    roundNumber: int = 0
    difficulty: str = "standard"
    survivorId: str = ""
    policyVersion: int = 1


class LastToDieRankingsRequest(AccountAuthenticatedRequest):
    limit: int = 3


app = FastAPI(title="OpenGarrison API", version="0.4.0")


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
        account_id = verify_client(
            db,
            normalize_client_id(payload.clientId),
            friend_code,
            payload.clientSecret,
            clean_text(payload.displayName, 64),
            clean_json_text(payload.playerCard),
        )
        canonical_friend_code = get_primary_friend_code(db, account_id)

    return {"clientId": payload.clientId, "friendCode": canonical_friend_code}


@app.post("/api/account/profile")
def get_account_profile(payload: AccountProfileRequest) -> dict[str, Any]:
    friend_code = normalize_friend_code(payload.friendCode)
    if not friend_code:
        raise HTTPException(status_code=400, detail="invalid friend code")
    with connect_db() as db:
        account_id = verify_client(
            db,
            normalize_client_id(payload.clientId),
            friend_code,
            payload.clientSecret,
            "",
        )
        return serialize_account_profile(db, account_id)


@app.post("/api/account/protect")
def protect_account(payload: AccountAuthenticatedRequest) -> dict[str, Any]:
    friend_code = normalize_friend_code(payload.friendCode)
    if not friend_code:
        raise HTTPException(status_code=400, detail="invalid friend code")
    with connect_db() as db:
        account_id = verify_client(
            db,
            normalize_client_id(payload.clientId),
            friend_code,
            payload.clientSecret,
            "",
        )
        recovery_key = create_recovery_key()
        normalized_key = normalize_recovery_key(recovery_key)
        salt = secrets.token_bytes(16)
        key_hash = recovery_key_hash(normalized_key, salt)
        current = now_seconds()
        db.execute(
            """
            INSERT INTO account_recovery_credentials (
                account_id, recovery_salt, recovery_hash, created_at, updated_at
            ) VALUES (?, ?, ?, ?, ?)
            ON CONFLICT(account_id) DO UPDATE SET
                recovery_salt = excluded.recovery_salt,
                recovery_hash = excluded.recovery_hash,
                updated_at = excluded.updated_at
            """,
            (account_id, salt.hex(), key_hash, current, current),
        )
        db.execute(
            """
            UPDATE accounts SET profile_revision = profile_revision + 1, updated_at = ?
            WHERE account_id = ?
            """,
            (current, account_id),
        )
        response = serialize_account_profile(db, account_id)
        response["recoveryKey"] = recovery_key
        return response


@app.post("/api/account/friend-code/shorten")
def shorten_account_friend_code(payload: AccountAuthenticatedRequest) -> dict[str, Any]:
    friend_code = normalize_friend_code(payload.friendCode)
    if not friend_code:
        raise HTTPException(status_code=400, detail="invalid friend code")
    with connect_db() as db:
        account_id = verify_client(
            db,
            normalize_client_id(payload.clientId),
            friend_code,
            payload.clientSecret,
            "",
        )
        primary_friend_code = get_primary_friend_code(db, account_id)
        compact_primary = primary_friend_code.replace("OG2-", "").replace("-", "")
        if len(compact_primary) != ACCOUNT_CODE_LENGTH:
            primary_friend_code = create_unique_friend_code(db)
            current = now_seconds()
            db.execute(
                "UPDATE account_friend_codes SET is_primary = 0 WHERE account_id = ?",
                (account_id,),
            )
            db.execute(
                """
                INSERT INTO account_friend_codes (friend_code, account_id, is_primary, created_at)
                VALUES (?, ?, 1, ?)
                """,
                (primary_friend_code, account_id, current),
            )
            db.execute(
                """
                UPDATE accounts SET primary_friend_code = ?,
                    profile_revision = profile_revision + 1, updated_at = ?
                WHERE account_id = ?
                """,
                (primary_friend_code, current, account_id),
            )
        return serialize_account_profile(db, account_id)


@app.post("/api/account/login")
def login_account(payload: AccountLoginRequest, request: Request) -> dict[str, Any]:
    friend_code = normalize_friend_code(payload.friendCode)
    recovery_key = normalize_recovery_key(payload.recoveryKey)
    client_id = normalize_client_id(payload.clientId)
    client_secret = payload.clientSecret
    if not friend_code or not recovery_key or not client_id or not client_secret:
        raise HTTPException(status_code=403, detail="invalid account credentials")

    with connect_db() as db:
        account_id = get_account_id_for_friend_code(db, friend_code)
        if not account_id:
            raise HTTPException(status_code=403, detail="invalid account credentials")
        verify_account_recovery_key(db, account_id, recovery_key, request)

        current = now_seconds()
        hashed_secret = secret_hash(client_secret)
        target_account = db.execute(
            "SELECT display_name, player_card_json FROM accounts WHERE account_id = ?",
            (account_id,),
        ).fetchone()
        if target_account is None:
            raise HTTPException(status_code=403, detail="invalid account credentials")
        target_display_name = str(target_account["display_name"])
        target_player_card = str(target_account["player_card_json"])
        device = db.execute(
            "SELECT account_id, secret_hash FROM client_devices WHERE client_id = ?",
            (client_id,),
        ).fetchone()
        if device is not None and not hmac.compare_digest(str(device["secret_hash"]), hashed_secret):
            raise HTTPException(status_code=403, detail="invalid account credentials")

        if device is None:
            db.execute(
                """
                INSERT INTO client_devices (
                    client_id, account_id, secret_hash, display_name, player_card_json,
                    created_at, updated_at, revoked_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, NULL)
                """,
                (
                    client_id,
                    account_id,
                    hashed_secret,
                    target_display_name,
                    target_player_card,
                    current,
                    current,
                ),
            )
        else:
            previous_account_id = str(device["account_id"])
            db.execute(
                """
                UPDATE client_devices SET account_id = ?, display_name = ?, player_card_json = ?,
                    updated_at = ?, revoked_at = NULL
                WHERE client_id = ?
                """,
                (account_id, target_display_name, target_player_card, current, client_id),
            )
            if previous_account_id != account_id:
                db.execute("DELETE FROM presence WHERE client_id = ?", (client_id,))
                delete_empty_implicit_account(db, previous_account_id)

        return serialize_account_profile(db, account_id)


@app.post("/api/game-session/create")
def create_gameplay_session(payload: AccountAuthenticatedRequest) -> dict[str, Any]:
    friend_code = normalize_friend_code(payload.friendCode)
    if not friend_code:
        raise HTTPException(status_code=400, detail="invalid friend code")
    client_id = normalize_client_id(payload.clientId)
    with connect_db() as db:
        account_id = verify_client(
            db,
            client_id,
            friend_code,
            payload.clientSecret,
            "",
        )
        current = now_seconds()
        db.execute("DELETE FROM gameplay_sessions WHERE expires_at <= ?", (current,))
        token = secrets.token_urlsafe(32)
        expires_at = current + GAMEPLAY_SESSION_TTL_SECONDS
        db.execute(
            """
            INSERT INTO gameplay_sessions (
                token_hash, account_id, client_id, issued_at, expires_at, revoked_at
            ) VALUES (?, ?, ?, ?, ?, NULL)
            """,
            (secret_hash(token), account_id, client_id, current, expires_at),
        )
        return {
            "gameplayToken": token,
            "expiresAtIso": iso_from_seconds(expires_at),
            "profile": serialize_account_profile(db, account_id),
        }


@app.post("/api/game-session/validate")
def validate_gameplay_session_endpoint(payload: GameplaySessionValidateRequest) -> dict[str, Any]:
    with connect_db() as db:
        session = validate_gameplay_session(db, clean_text(payload.gameplayToken, 256))
        return {
            "valid": True,
            "clientId": session["client_id"],
            "expiresAtIso": iso_from_seconds(int(session["expires_at"])),
            "profile": serialize_account_profile(db, str(session["account_id"])),
        }


@app.post("/api/stats/award", dependencies=[Depends(require_reward_authority)])
def award_stat_event(payload: StatAwardRequest) -> dict[str, Any]:
    token = clean_text(payload.gameplayToken, 256)
    event_id = clean_text(payload.eventId, 128)
    event_type = clean_text(payload.eventType, 64).lower()
    if not event_id or not event_type:
        raise HTTPException(status_code=400, detail="event id and type are required")

    points_delta = clamp_int(payload.pointsDelta, 0, MAX_STAT_EVENT_POINTS)
    credits_delta = clamp_int(payload.creditsDelta, 0, MAX_STAT_EVENT_CREDITS)
    if points_delta != int(payload.pointsDelta) or credits_delta != int(payload.creditsDelta):
        raise HTTPException(status_code=400, detail="stat award is outside allowed bounds")

    with connect_db() as db:
        session = validate_gameplay_session(db, token)
        account_id = str(session["account_id"])
        existing = db.execute(
            """
            SELECT account_id, event_type, points_delta, credits_delta
            FROM stat_events WHERE event_id = ?
            """,
            (event_id,),
        ).fetchone()
        if existing is not None:
            if (
                str(existing["account_id"]) != account_id
                or str(existing["event_type"]) != event_type
                or int(existing["points_delta"]) != points_delta
                or int(existing["credits_delta"]) != credits_delta
            ):
                raise HTTPException(status_code=409, detail="event id conflicts with an existing award")
            return {
                "applied": False,
                "eventId": event_id,
                "profile": serialize_account_profile(db, account_id),
            }

        reconcile_account_totals(db, account_id)
        account = db.execute(
            "SELECT wallet_balance FROM accounts WHERE account_id = ?",
            (account_id,),
        ).fetchone()
        if account is None:
            raise HTTPException(status_code=404, detail="account not found")
        current = now_seconds()
        balance_after = int(account["wallet_balance"]) + credits_delta
        db.execute(
            """
            INSERT INTO stat_events (
                event_id, account_id, match_id, source_frame, event_type, raw_value,
                points_delta, credits_delta, policy_version, created_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                event_id,
                account_id,
                clean_text(payload.matchId, 128),
                clamp_int(payload.sourceFrame, 0, 9_223_372_036_854_775_807),
                event_type,
                clamp_int(payload.rawValue, -2_147_483_648, 2_147_483_647),
                points_delta,
                credits_delta,
                clamp_int(payload.policyVersion, 1, 1_000_000),
                current,
            ),
        )
        if credits_delta > 0:
            db.execute(
                """
                INSERT INTO wallet_transactions (
                    transaction_id, account_id, kind, amount, balance_after, reference_id, created_at
                ) VALUES (?, ?, 'stat_award', ?, ?, ?, ?)
                """,
                (
                    secrets.token_hex(16),
                    account_id,
                    credits_delta,
                    balance_after,
                    f"stat:{account_id}:{event_id}",
                    current,
                ),
            )
        db.execute(
            """
            UPDATE accounts SET lifetime_points = lifetime_points + ?, wallet_balance = ?,
                profile_revision = profile_revision + 1, updated_at = ?
            WHERE account_id = ?
            """,
            (points_delta, balance_after, current, account_id),
        )
        return {
            "applied": True,
            "eventId": event_id,
            "profile": serialize_account_profile(db, account_id),
        }


def get_account_global_rank(db: sqlite3.Connection, account_id: str) -> int:
    row = db.execute(
        """
        SELECT lifetime_points,
               1 + (
                   SELECT COUNT(*) FROM accounts ranked
                   WHERE ranked.lifetime_points > account.lifetime_points
               ) AS global_rank
        FROM accounts account
        WHERE account_id = ?
        """,
        (account_id,),
    ).fetchone()
    if row is None or int(row["lifetime_points"]) <= 0:
        return 0
    return max(1, int(row["global_rank"]))


def serialize_stat_totals(db: sqlite3.Connection, account_id: str) -> dict[str, int]:
    rows = db.execute(
        """
        SELECT event_type, COALESCE(SUM(raw_value), 0) AS total
        FROM stat_events
        WHERE account_id = ?
        GROUP BY event_type
        ORDER BY event_type
        """,
        (account_id,),
    ).fetchall()
    return {str(row["event_type"]): int(row["total"]) for row in rows}


@app.post("/api/stats/points")
def get_player_points(payload: GameplaySessionValidateRequest) -> dict[str, Any]:
    with connect_db() as db:
        session = validate_gameplay_session(db, clean_text(payload.gameplayToken, 256))
        account_id = str(session["account_id"])
        reconcile_account_totals(db, account_id)
        return {
            "profile": serialize_account_profile(db, account_id),
            "globalRank": get_account_global_rank(db, account_id),
            "stats": serialize_stat_totals(db, account_id),
        }


@app.get("/api/stats/leaderboard")
def get_points_leaderboard(limit: int = 10, offset: int = 0) -> dict[str, Any]:
    page_limit = clamp_int(limit, 1, 50)
    page_offset = clamp_int(offset, 0, 1_000_000)
    with connect_db() as db:
        rows = db.execute(
            """
            SELECT account_id, primary_friend_code, display_name, lifetime_points,
                   wallet_balance, profile_revision,
                   RANK() OVER (ORDER BY lifetime_points DESC) AS global_rank
            FROM accounts
            WHERE lifetime_points > 0
            ORDER BY lifetime_points DESC, updated_at ASC, account_id ASC
            LIMIT ? OFFSET ?
            """,
            (page_limit, page_offset),
        ).fetchall()
        total_row = db.execute(
            "SELECT COUNT(*) AS total FROM accounts WHERE lifetime_points > 0"
        ).fetchone()
        entries = []
        for index, row in enumerate(rows):
            entries.append({
                "rank": max(1, int(row["global_rank"])),
                "friendCode": str(row["primary_friend_code"]),
                "displayName": str(row["display_name"]) or "Player",
                "lifetimePoints": max(0, int(row["lifetime_points"])),
                "walletBalance": max(0, int(row["wallet_balance"])),
                "profileRevision": max(0, int(row["profile_revision"])),
            })
        return {
            "entries": entries,
            "offset": page_offset,
            "limit": page_limit,
            "total": int(total_row["total"]) if total_row is not None else 0,
        }


def get_last_to_die_player_stats(db: sqlite3.Connection, account_id: str) -> dict[str, Any]:
    aggregate = db.execute(
        """
        SELECT COUNT(*) AS runs_played,
               COALESCE(MAX(score_units), 0) AS best_score_units,
               COALESCE(MAX(round_number), 0) AS highest_round
        FROM last_to_die_runs
        WHERE account_id = ?
        """,
        (account_id,),
    ).fetchone()
    best_score_units = max(0, int(aggregate["best_score_units"]))
    highest_round = max(0, int(aggregate["highest_round"]))
    score_rank = 0
    round_rank = 0
    if best_score_units > 0 or highest_round > 0:
        score_rank_row = db.execute(
            """
            WITH account_records AS (
                SELECT account_id, MAX(score_units) AS best_score_units
                FROM last_to_die_runs GROUP BY account_id
            )
            SELECT 1 + COUNT(*) AS rank
            FROM account_records WHERE best_score_units > ?
            """,
            (best_score_units,),
        ).fetchone()
        round_rank_row = db.execute(
            """
            WITH account_records AS (
                SELECT account_id, MAX(round_number) AS highest_round
                FROM last_to_die_runs GROUP BY account_id
            )
            SELECT 1 + COUNT(*) AS rank
            FROM account_records WHERE highest_round > ?
            """,
            (highest_round,),
        ).fetchone()
        score_rank = max(1, int(score_rank_row["rank"]))
        round_rank = max(1, int(round_rank_row["rank"]))

    profile = serialize_account_profile(db, account_id)
    return {
        "friendCode": profile["friendCode"],
        "displayName": profile["displayName"] or "Player",
        "runsPlayed": max(0, int(aggregate["runs_played"])),
        "bestScoreUnits": best_score_units,
        "highestRound": highest_round,
        "scoreRank": score_rank,
        "roundRank": round_rank,
    }


def get_last_to_die_leaderboard(
    db: sqlite3.Connection,
    sort: str,
    limit: int,
    offset: int,
    survivor_id: str = "",
) -> dict[str, Any]:
    normalized_sort = clean_text(sort, 16).lower()
    if normalized_sort not in ("score", "round"):
        raise HTTPException(status_code=400, detail="sort must be score or round")
    page_limit = clamp_int(limit, 1, 50)
    page_offset = clamp_int(offset, 0, 1_000_000)
    normalized_survivor_id = clean_text(survivor_id, 96).lower()
    if normalized_survivor_id and normalized_survivor_id not in LAST_TO_DIE_SURVIVOR_IDS:
        raise HTTPException(status_code=400, detail="invalid Last to Die survivor")
    rank_column = "score_units" if normalized_sort == "score" else "round_number"
    secondary_column = "round_number" if normalized_sort == "score" else "score_units"
    rows = db.execute(
        f"""
        WITH candidate_runs AS (
            SELECT account_id, score_units, round_number, survivor_id,
                   COUNT(*) OVER (PARTITION BY account_id) AS runs_played,
                   ROW_NUMBER() OVER (
                       PARTITION BY account_id
                       ORDER BY {rank_column} DESC, {secondary_column} DESC,
                                created_at ASC, submission_id ASC
                   ) AS account_record
            FROM last_to_die_runs
            WHERE (? = '' OR survivor_id = ?)
        ), account_records AS (
            SELECT account_id, score_units, round_number, survivor_id, runs_played
            FROM candidate_runs
            WHERE account_record = 1
        ), ranked AS (
            SELECT account_id, score_units, round_number, survivor_id, runs_played,
                   RANK() OVER (ORDER BY {rank_column} DESC) AS global_rank
            FROM account_records
        )
        SELECT ranked.*, accounts.primary_friend_code, accounts.display_name
        FROM ranked
        JOIN accounts ON accounts.account_id = ranked.account_id
        ORDER BY {rank_column} DESC, {secondary_column} DESC,
                 accounts.updated_at ASC, ranked.account_id ASC
        LIMIT ? OFFSET ?
        """,
        (normalized_survivor_id, normalized_survivor_id, page_limit, page_offset),
    ).fetchall()
    total_row = db.execute(
        "SELECT COUNT(DISTINCT account_id) AS total FROM last_to_die_runs "
        "WHERE (? = '' OR survivor_id = ?)",
        (normalized_survivor_id, normalized_survivor_id),
    ).fetchone()
    entries = [
        {
            "rank": max(1, int(row["global_rank"])),
            "friendCode": str(row["primary_friend_code"]),
            "displayName": str(row["display_name"]) or "Player",
            "survivorId": str(row["survivor_id"]),
            "runsPlayed": max(0, int(row["runs_played"])),
            "bestScoreUnits": max(0, int(row["score_units"])),
            "highestRound": max(0, int(row["round_number"])),
        }
        for row in rows
    ]
    return {
        "sort": normalized_sort,
        "survivorId": normalized_survivor_id,
        "entries": entries,
        "offset": page_offset,
        "limit": page_limit,
        "total": int(total_row["total"]) if total_row is not None else 0,
    }


@app.post("/api/last-to-die/run", dependencies=[Depends(require_reward_authority)])
def record_last_to_die_run(payload: LastToDieRunRequest) -> dict[str, Any]:
    token = clean_text(payload.gameplayToken, 256)
    submission_id = clean_text(payload.submissionId, 128)
    run_id = clean_text(payload.runId, 128)
    score_units = clamp_int(payload.scoreUnits, 0, 2_147_483_647)
    round_number = clamp_int(payload.roundNumber, 0, 1_000_000)
    difficulty = clean_text(payload.difficulty, 16).lower()
    survivor_id = clean_text(payload.survivorId, 96).lower()
    policy_version = clamp_int(payload.policyVersion, 1, 1_000_000)
    if not submission_id or not run_id:
        raise HTTPException(status_code=400, detail="submission id and run id are required")
    if score_units != int(payload.scoreUnits) or round_number != int(payload.roundNumber):
        raise HTTPException(status_code=400, detail="Last to Die result is outside allowed bounds")
    if difficulty not in ("standard", "hardcore"):
        raise HTTPException(status_code=400, detail="invalid Last to Die difficulty")
    if survivor_id and survivor_id not in LAST_TO_DIE_SURVIVOR_IDS:
        raise HTTPException(status_code=400, detail="invalid Last to Die survivor")

    with connect_db() as db:
        session = validate_gameplay_session(db, token)
        account_id = str(session["account_id"])
        existing = db.execute(
            """
            SELECT run_id, account_id, score_units, round_number, difficulty, survivor_id, policy_version
            FROM last_to_die_runs WHERE submission_id = ?
            """,
            (submission_id,),
        ).fetchone()
        if existing is not None:
            if (
                str(existing["run_id"]) != run_id
                or str(existing["account_id"]) != account_id
                or int(existing["score_units"]) != score_units
                or int(existing["round_number"]) != round_number
                or str(existing["difficulty"]) != difficulty
                or str(existing["survivor_id"]) != survivor_id
                or int(existing["policy_version"]) != policy_version
            ):
                raise HTTPException(status_code=409, detail="submission id conflicts with an existing run")
            return {
                "applied": False,
                "submissionId": submission_id,
                "player": get_last_to_die_player_stats(db, account_id),
            }

        duplicate_run = db.execute(
            "SELECT submission_id FROM last_to_die_runs WHERE run_id = ? AND account_id = ?",
            (run_id, account_id),
        ).fetchone()
        if duplicate_run is not None:
            raise HTTPException(status_code=409, detail="run was already recorded for this account")

        db.execute(
            """
            INSERT INTO last_to_die_runs (
                submission_id, run_id, account_id, score_units, round_number,
                difficulty, survivor_id, policy_version, created_at
            ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?)
            """,
            (
                submission_id,
                run_id,
                account_id,
                score_units,
                round_number,
                difficulty,
                survivor_id,
                policy_version,
                now_seconds(),
            ),
        )
        return {
            "applied": True,
            "submissionId": submission_id,
            "player": get_last_to_die_player_stats(db, account_id),
        }


@app.get("/api/last-to-die/leaderboard")
def last_to_die_leaderboard(
    sort: str = "score",
    limit: int = 10,
    offset: int = 0,
    survivor: str = "",
) -> dict[str, Any]:
    with connect_db() as db:
        return get_last_to_die_leaderboard(db, sort, limit, offset, survivor)


@app.post("/api/last-to-die/rankings")
def last_to_die_rankings(payload: LastToDieRankingsRequest) -> dict[str, Any]:
    friend_code = normalize_friend_code(payload.friendCode)
    if not friend_code:
        raise HTTPException(status_code=400, detail="invalid friend code")
    with connect_db() as db:
        account_id = verify_client(
            db,
            normalize_client_id(payload.clientId),
            friend_code,
            payload.clientSecret,
            "",
        )
        page_limit = clamp_int(payload.limit, 1, 10)
        return {
            "player": get_last_to_die_player_stats(db, account_id),
            "scoreRecords": get_last_to_die_leaderboard(db, "score", page_limit, 0)["entries"],
            "roundRecords": get_last_to_die_leaderboard(db, "round", page_limit, 0)["entries"],
        }


@app.post("/api/friends/request")
def create_friend_request(payload: FriendRequestCreateRequest) -> dict[str, Any]:
    own_code = normalize_friend_code(payload.friendCode)
    target_code = normalize_friend_code(payload.targetFriendCode)
    if not own_code or not target_code:
        raise HTTPException(status_code=400, detail="invalid friend code")
    client_id = normalize_client_id(payload.clientId)
    display_name = clean_text(payload.displayName, 64) or "Player"
    current = now_seconds()
    with connect_db() as db:
        account_id = verify_client(db, client_id, own_code, payload.clientSecret, display_name)
        own_code = get_primary_friend_code(db, account_id)
        own_aliases = get_account_friend_codes(db, account_id)
        target_account_id = get_account_id_for_friend_code(db, target_code)
        target_code = get_primary_friend_code(db, target_account_id) if target_account_id else target_code
        target_aliases = get_account_friend_codes(db, target_account_id) if target_account_id else [target_code]
        if own_code == target_code:
            raise HTTPException(status_code=400, detail="cannot request yourself")

        own_placeholders = ",".join("?" for _ in own_aliases)
        target_placeholders = ",".join("?" for _ in target_aliases)
        reverse = db.execute(
            f"""
            SELECT * FROM friend_requests
            WHERE from_friend_code IN ({target_placeholders})
              AND to_friend_code IN ({own_placeholders})
              AND status = 'pending'
            """,
            (*target_aliases, *own_aliases),
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

    client_id = normalize_client_id(payload.clientId)
    display_name = clean_text(payload.displayName, 64) or "Player"
    with connect_db() as db:
        account_id = verify_client(db, client_id, own_code, payload.clientSecret, display_name)
        own_code = get_primary_friend_code(db, account_id)
        own_aliases = get_account_friend_codes(db, account_id)
        placeholders = ",".join("?" for _ in own_aliases)
        rows = db.execute(
            f"""
            SELECT * FROM friend_requests
            WHERE (to_friend_code IN ({placeholders}) AND status = 'pending')
               OR (from_friend_code IN ({placeholders}) AND status IN ('pending', 'accepted', 'denied'))
            ORDER BY updated_at DESC, request_id DESC
            LIMIT 50
            """,
            (*own_aliases, *own_aliases),
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

    client_id = normalize_client_id(payload.clientId)
    display_name = clean_text(payload.displayName, 64) or "Player"
    current = now_seconds()
    with connect_db() as db:
        account_id = verify_client(db, client_id, own_code, payload.clientSecret, display_name)
        own_code = get_primary_friend_code(db, account_id)
        own_aliases = get_account_friend_codes(db, account_id)
        placeholders = ",".join("?" for _ in own_aliases)
        row = db.execute(
            f"""
            SELECT * FROM friend_requests
            WHERE request_id = ? AND to_friend_code IN ({placeholders}) AND status = 'pending'
            """,
            (payload.requestId, *own_aliases),
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
    if not text:
        raise HTTPException(status_code=400, detail="message is required")

    client_id = normalize_client_id(payload.clientId)
    display_name = clean_text(payload.displayName, 64) or "Player"
    current = now_seconds()
    with connect_db() as db:
        account_id = verify_client(db, client_id, own_code, payload.clientSecret, display_name)
        own_code = get_primary_friend_code(db, account_id)
        target_code = canonicalize_friend_code(db, target_code)
        if own_code == target_code:
            raise HTTPException(status_code=400, detail="cannot message yourself")
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
        return serialize_direct_message(db, row, own_code)


@app.post("/api/messages/poll")
def poll_direct_messages(payload: DirectMessagesPollRequest) -> dict[str, Any]:
    own_code = normalize_friend_code(payload.friendCode)
    if not own_code:
        raise HTTPException(status_code=400, detail="invalid friend code")

    client_id = normalize_client_id(payload.clientId)
    display_name = clean_text(payload.displayName, 64) or "Player"
    after_id = max(0, int(payload.afterId))
    with connect_db() as db:
        account_id = verify_client(db, client_id, own_code, payload.clientSecret, display_name)
        own_code = get_primary_friend_code(db, account_id)
        own_aliases = get_account_friend_codes(db, account_id)
        placeholders = ",".join("?" for _ in own_aliases)
        rows = db.execute(
            f"""
            SELECT * FROM direct_messages
            WHERE recipient_friend_code IN ({placeholders}) AND message_id > ?
            ORDER BY message_id ASC
            LIMIT 50
            """,
            (*own_aliases, after_id),
        ).fetchall()
        return {
            "messages": [serialize_direct_message(db, row, own_code) for row in rows],
            "generatedAt": iso_from_seconds(now_seconds()),
        }


@app.post("/api/relay/session")
async def create_relay_session(payload: RelaySessionCreateRequest, request: Request) -> dict[str, str]:
    friend_code = normalize_friend_code(payload.friendCode)
    if not friend_code:
        raise HTTPException(status_code=400, detail="invalid friend code")

    client_id = normalize_client_id(payload.clientId)
    display_name = clean_text(payload.displayName, 64) or "Player"
    with connect_db() as db:
        account_id = verify_client(db, client_id, friend_code, payload.clientSecret, display_name)
        friend_code = get_primary_friend_code(db, account_id)

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
    if friend_code:
        with connect_db() as db:
            friend_code = canonicalize_friend_code(db, friend_code)
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

    client_id = normalize_client_id(payload.clientId)
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
        account_id = verify_client(db, client_id, friend_code, payload.clientSecret, display_name, player_card_json)
        friend_code = get_primary_friend_code(db, account_id)
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

    return {"status": "ok", "friendCode": friend_code}


@app.post("/api/presence/offline")
def offline_presence(payload: PresenceOfflineRequest) -> dict[str, str]:
    client_id = normalize_client_id(payload.clientId)
    with connect_db() as db:
        existing = db.execute(
            "SELECT secret_hash FROM client_devices WHERE client_id = ?",
            (client_id,),
        ).fetchone()
        if existing is not None and not hmac.compare_digest(
            str(existing["secret_hash"]),
            secret_hash(payload.clientSecret),
        ):
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
        alias_rows = db.execute(
            f"""
            SELECT afc.friend_code, a.*
            FROM account_friend_codes afc
            JOIN accounts a ON a.account_id = afc.account_id
            WHERE afc.friend_code IN ({placeholders})
            """,
            requested,
        ).fetchall()
        accounts = {row["friend_code"]: row for row in alias_rows}
        account_ids = list({str(row["account_id"]) for row in alias_rows})
        presence_by_account: dict[str, sqlite3.Row] = {}
        if account_ids:
            account_placeholders = ",".join("?" for _ in account_ids)
            presence_rows = db.execute(
                f"""
                SELECT p.*, d.account_id
                FROM presence p
                JOIN client_devices d ON d.client_id = p.client_id
                WHERE d.account_id IN ({account_placeholders})
                ORDER BY p.updated_at DESC
                """,
                account_ids,
            ).fetchall()
            for row in presence_rows:
                presence_by_account.setdefault(str(row["account_id"]), row)

    friends = []
    for code in requested:
        account = accounts.get(code)
        row = presence_by_account.get(str(account["account_id"])) if account is not None else None
        online = row is not None and row["updated_at"] >= current - PRESENCE_TTL_SECONDS
        friends.append(
            {
                "friendCode": code,
                "displayName": (row["display_name"] if row is not None else (account["display_name"] if account is not None else "")),
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
                "playerCard": (row["player_card_json"] if row is not None else (account["player_card_json"] if account is not None else "")),
                "lastSeenIso": iso_from_seconds(row["updated_at"]) if row is not None else "",
            }
        )

    return {"friends": friends, "generatedAt": iso_from_seconds(current)}


# Managed room control/gateway shares device authentication with LTD's social relay.
import sys as _sys
from private_rooms import install_private_rooms as _install_private_rooms
_install_private_rooms(_sys.modules[__name__])
from peer_rooms import install_peer_rooms as _install_peer_rooms
_install_peer_rooms(_sys.modules[__name__])


install_run_verification_routes(app, connect_db, validate_gameplay_session)
