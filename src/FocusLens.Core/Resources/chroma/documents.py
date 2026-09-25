# ─────────────────────────────────────────────────────────────────────────────
# documents.py
#
# Turns DB rows into embeddable records: {id, document, metadata}.
#   • `document` is the natural-language text that gets embedded + searched.
#   • `metadata` is structured fields for filtering and for showing results.
#   • `id` is stable across runs so re-ingesting upserts instead of duplicating.
# ─────────────────────────────────────────────────────────────────────────────

from __future__ import annotations  # allow "X | None" hints on Python 3.7-3.9

import hashlib
import json
from datetime import datetime

from db import Session

_META_MAX = 500       # cap stored metadata strings
_OCR_MAX = 4000       # cap OCR text in a document (the model truncates anyway)


# ── small helpers ────────────────────────────────────────────────────────────

def _date(epoch: float) -> str:
    return datetime.fromtimestamp(epoch).strftime("%Y-%m-%d")


def _time(epoch: float) -> str:
    return datetime.fromtimestamp(epoch).strftime("%H:%M")


def _short(key) -> str:
    return hashlib.sha1(repr(key).encode()).hexdigest()[:8]


def _cap(s: str, n: int) -> str:
    s = s or ""
    return s if len(s) <= n else s[:n] + "…"


def human_duration(seconds: float) -> str:
    s = int(seconds)
    if s >= 3600:
        return f"{s / 3600:.1f} h"
    if s >= 60:
        return f"{s // 60} min"
    return f"{s} s"


def _meta(d: dict) -> dict:
    """Chroma metadata must be str/int/float/bool and non-null."""
    out: dict = {}
    for k, v in d.items():
        if v is None:
            continue
        if isinstance(v, bool) or isinstance(v, (int, float)):
            out[k] = v
        else:
            out[k] = _cap(str(v), _META_MAX)
    return out


# ── builders ─────────────────────────────────────────────────────────────────

def activity_documents(sessions: list[Session]) -> list[dict]:
    recs = []
    for s in sessions:
        head_parts = [s.app_name]
        if s.window_title:
            head_parts.append(s.window_title)
        if s.url:
            head_parts.append(s.url)
        head = " — ".join(p for p in head_parts if p)
        duration = int(round(s.end - s.start)) + 1
        cat = s.category or "Uncategorized"
        text = (
            f"{head}. Category: {cat}. "
            f"Active {human_duration(duration)} on {_date(s.start)} at {_time(s.start)}."
        )
        recs.append({
            "id": f"act-{int(s.start)}-{_short(s.key)}",
            "document": _cap(text, 2000),
            "metadata": _meta({
                "source": "activity",
                "app_bundle_id": s.app_bundle_id,
                "app_name": s.app_name,
                "window_title": s.window_title,
                "url": s.url,
                "category": cat,
                "start_ts": int(s.start),
                "end_ts": int(s.end),
                "duration_s": duration,
                "event_count": s.count,
                "date": _date(s.start),
            }),
        })
    return recs


def screenshot_documents(rows: list[dict]) -> list[dict]:
    from db import parse_grdb_datetime
    recs = []
    for r in rows:
        ocr = (r.get("ocr_text") or "").strip()
        if not ocr:
            continue
        epoch = parse_grdb_datetime(r.get("timestamp"))
        win = r.get("window_title") or "window"
        text = f"[{r.get('app_name', '')} — {win}] {ocr}"
        meta = {
            "source": "screenshot",
            "app_bundle_id": r.get("app_bundle_id"),
            "app_name": r.get("app_name"),
            "window_title": r.get("window_title"),
            "thumb_path": r.get("thumb_path"),
        }
        if epoch is not None:
            meta["timestamp"] = int(epoch)
            meta["date"] = _date(epoch)
        recs.append({
            "id": f"shot-{r.get('id')}",
            "document": _cap(text, _OCR_MAX),
            "metadata": _meta(meta),
        })
    return recs


def _parse_totals(raw, name_key="name", sec_key="seconds") -> list[tuple[str, int]]:
    """Parse category_json / top_apps_json (arrays of objects) defensively."""
    try:
        data = json.loads(raw) if raw else []
    except (TypeError, json.JSONDecodeError):
        return []
    items: list[tuple[str, int]] = []
    if isinstance(data, dict):  # tolerate {name: seconds}
        items = [(str(k), int(v)) for k, v in data.items()]
    elif isinstance(data, list):
        for it in data:
            if isinstance(it, dict):
                name = it.get(name_key) or it.get("id") or "?"
                secs = it.get(sec_key) or it.get("seconds") or 0
                items.append((str(name), int(secs)))
    items.sort(key=lambda x: x[1], reverse=True)
    return items


def summary_documents(rows: list[dict]) -> list[dict]:
    recs = []
    for r in rows:
        date = r.get("date", "")
        active = int(r.get("total_active_s") or 0)
        idle = int(r.get("total_idle_s") or 0)
        score = float(r.get("focus_score") or 0.0)
        cats = _parse_totals(r.get("category_json"))
        apps = _parse_totals(r.get("top_apps_json"))
        cat_str = ", ".join(f"{n} {human_duration(s)}" for n, s in cats[:6]) or "none"
        app_str = ", ".join(f"{n} {human_duration(s)}" for n, s in apps[:6]) or "none"
        text = (
            f"On {date}: {human_duration(active)} active, {human_duration(idle)} idle, "
            f"focus score {score:.0f}%. Top categories: {cat_str}. Top apps: {app_str}."
        )
        recs.append({
            "id": f"sum-{date}",
            "document": _cap(text, 2000),
            "metadata": _meta({
                "source": "summary",
                "date": date,
                "total_active_s": active,
                "total_idle_s": idle,
                "focus_score": score,
            }),
        })
    return recs


def conversation_documents(rows: list[dict]) -> list[dict]:
    recs = []
    for r in rows:
        content = (r.get("content_md") or "").strip()
        if not content:
            continue
        role = r.get("role", "")
        created = r.get("created_at")
        text = f"[{role}] {content}"
        meta = {
            "source": "conversation",
            "conversation_id": r.get("conversation_id"),
            "conversation_title": r.get("conversation_title"),
            "role": role,
        }
        if isinstance(created, (int, float)):
            meta["created_at"] = int(created)
            meta["date"] = _date(float(created))
        recs.append({
            "id": f"msg-{r.get('id')}",
            "document": _cap(text, _OCR_MAX),
            "metadata": _meta(meta),
        })
    return recs
