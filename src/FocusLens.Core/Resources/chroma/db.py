# ─────────────────────────────────────────────────────────────────────────────
# db.py
#
# Read-only access to the FocusLens SQLite database, plus the timestamp parsing
# and activity "sessionization" needed before the rows can be embedded.
#
# Timestamp formats in the DB:
#   • activity_events / screenshots / daily_summaries -> GRDB datetime TEXT,
#     stored UTC as "YYYY-MM-DD HH:MM:SS.SSS".
#   • conversations / messages -> INTEGER epoch seconds.
# Both are normalised to epoch seconds (float) by parse_grdb_datetime().
# ─────────────────────────────────────────────────────────────────────────────

from __future__ import annotations  # allow "X | None" hints on Python 3.7-3.9

import os
import sqlite3
from dataclasses import dataclass
from datetime import datetime, timezone


def default_db_path() -> str:
    """The dev-fallback DB path the app uses when no App Group is configured."""
    return os.path.expanduser(
        "~/Library/Application Support/FocusLens/focuslens.db"
    )


def connect(path: str) -> sqlite3.Connection:
    """Open the database read-only so ingestion can never mutate user data."""
    if not os.path.exists(path):
        raise FileNotFoundError(f"FocusLens database not found at: {path}")
    conn = sqlite3.connect(f"file:{path}?mode=ro", uri=True)
    conn.row_factory = sqlite3.Row
    return conn


def table_exists(conn: sqlite3.Connection, name: str) -> bool:
    row = conn.execute(
        "SELECT 1 FROM sqlite_master WHERE type='table' AND name=?", (name,)
    ).fetchone()
    return row is not None


# ── Timestamp helpers ────────────────────────────────────────────────────────

_DT_FORMATS = (
    "%Y-%m-%d %H:%M:%S.%f",
    "%Y-%m-%d %H:%M:%S",
    "%Y-%m-%dT%H:%M:%S.%fZ",
    "%Y-%m-%dT%H:%M:%SZ",
    "%Y-%m-%dT%H:%M:%S",
)


def parse_grdb_datetime(value) -> float | None:
    """Normalise a stored timestamp (UTC text or epoch int) to epoch seconds."""
    if value is None:
        return None
    if isinstance(value, (int, float)):
        return float(value) / 1000.0 if value > 1e12 else float(value)
    s = str(value).strip()
    if not s:
        return None
    try:  # numeric string (epoch seconds or millis)
        f = float(s)
        return f / 1000.0 if f > 1e12 else f
    except ValueError:
        pass
    for fmt in _DT_FORMATS:
        try:
            return datetime.strptime(s, fmt).replace(tzinfo=timezone.utc).timestamp()
        except ValueError:
            continue
    return None


# ── Row readers ──────────────────────────────────────────────────────────────

def read_categories(conn: sqlite3.Connection) -> dict[int, str]:
    if not table_exists(conn, "categories"):
        return {}
    return {r["id"]: r["name"] for r in conn.execute("SELECT id, name FROM categories")}


@dataclass
class Session:
    key: tuple
    app_bundle_id: str
    app_name: str
    window_title: str | None
    url: str | None
    category: str
    start: float
    end: float
    count: int


def read_activity_sessions(
    conn: sqlite3.Connection,
    categories: dict[int, str],
    limit: int | None = None,
    max_gap_s: int = 120,
    include_idle: bool = False,
) -> list[Session]:
    """Collapse the 1-second activity stream into contiguous sessions.

    Consecutive rows sharing (app, window title, url) within `max_gap_s` are
    merged into one session, so we embed a handful of meaningful records per
    hour instead of thousands of near-identical one-second rows.
    """
    if not table_exists(conn, "activity_events"):
        return []

    sql = (
        "SELECT timestamp, app_bundle_id, app_name, window_title, url, "
        "is_idle, category_id FROM activity_events ORDER BY timestamp ASC"
    )
    sessions: list[Session] = []
    current: Session | None = None

    for r in conn.execute(sql):
        if not include_idle and r["is_idle"]:
            if current:
                sessions.append(current)
                current = None
            continue
        ts = parse_grdb_datetime(r["timestamp"])
        if ts is None:
            continue
        key = (r["app_bundle_id"], r["window_title"] or "", r["url"] or "")
        if current and current.key == key and ts - current.end <= max_gap_s:
            current.end = ts
            current.count += 1
        else:
            if current:
                sessions.append(current)
            current = Session(
                key=key,
                app_bundle_id=r["app_bundle_id"],
                app_name=r["app_name"],
                window_title=r["window_title"],
                url=r["url"],
                category=categories.get(r["category_id"], ""),
                start=ts,
                end=ts,
                count=1,
            )
    if current:
        sessions.append(current)

    # `limit` keeps the most recent N sessions (useful for quick test runs).
    if limit is not None and limit > 0:
        sessions = sessions[-limit:]
    return sessions


def read_screenshots(conn: sqlite3.Connection, limit: int | None = None) -> list[dict]:
    if not table_exists(conn, "screenshots"):
        return []
    sql = (
        "SELECT id, timestamp, app_bundle_id, app_name, window_title, "
        "ocr_text, thumb_path FROM screenshots "
        "WHERE ocr_text IS NOT NULL AND TRIM(ocr_text) != '' "
        "ORDER BY timestamp DESC"
    )
    if limit is not None and limit > 0:
        sql += f" LIMIT {int(limit)}"
    return [dict(r) for r in conn.execute(sql)]


def read_summaries(conn: sqlite3.Connection, limit: int | None = None) -> list[dict]:
    if not table_exists(conn, "daily_summaries"):
        return []
    sql = (
        "SELECT date, total_active_s, total_idle_s, focus_score, "
        "category_json, top_apps_json FROM daily_summaries ORDER BY date DESC"
    )
    if limit is not None and limit > 0:
        sql += f" LIMIT {int(limit)}"
    return [dict(r) for r in conn.execute(sql)]


def read_messages(conn: sqlite3.Connection, limit: int | None = None) -> list[dict]:
    if not table_exists(conn, "messages"):
        return []
    sql = (
        "SELECT m.id, m.conversation_id, m.role, m.content_md, m.created_at, "
        "c.title AS conversation_title FROM messages m "
        "LEFT JOIN conversations c ON c.id = m.conversation_id "
        "WHERE m.role IN ('user', 'assistant') AND TRIM(m.content_md) != '' "
        "ORDER BY m.created_at DESC"
    )
    if limit is not None and limit > 0:
        sql += f" LIMIT {int(limit)}"
    return [dict(r) for r in conn.execute(sql)]
