#!/usr/bin/env python3
# ─────────────────────────────────────────────────────────────────────────────
# ingest.py
#
# Reads the local FocusLens SQLite database and loads it into a persistent
# ChromaDB instance (one collection per data source) for fast semantic search.
#
# Everything runs locally: embeddings are computed on-device, the Chroma store
# is a folder on disk, and the SQLite DB is opened read-only.
#
# Examples:
#   python ingest.py                          # all sources, default paths
#   python ingest.py --collections activity   # just one source
#   python ingest.py --reset                  # rebuild collections from scratch
#   python ingest.py --limit 200              # quick test on recent rows
#   python ingest.py --db /path/to/focuslens.db --out /path/to/chroma
# ─────────────────────────────────────────────────────────────────────────────

import argparse
import os
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import db
import documents as docs
import store


def parse_args():
    p = argparse.ArgumentParser(
        description="Ingest FocusLens SQLite data into ChromaDB for semantic search."
    )
    p.add_argument("--db", default=db.default_db_path(),
                   help="Path to focuslens.db (read-only). Default: app data dir.")
    p.add_argument("--out", default=store.default_chroma_path(),
                   help="Chroma persistence directory. Default: app data dir/chroma.")
    p.add_argument("--collections", nargs="+", choices=store.SOURCES,
                   default=store.SOURCES, help="Which sources to ingest.")
    p.add_argument("--limit", type=int, default=None,
                   help="Cap records per source to the most recent N (for testing).")
    p.add_argument("--reset", action="store_true",
                   help="Delete and recreate each collection before ingesting.")
    p.add_argument("--max-gap", type=int, default=120,
                   help="Max seconds between activity events to merge into one session.")
    p.add_argument("--include-idle", action="store_true",
                   help="Include idle activity events (excluded by default).")
    p.add_argument("--embedding", default="default", choices=["default", "hash"],
                   help="Embedding function. 'default' = local MiniLM; 'hash' = test only.")
    p.add_argument("--skip-existing", action="store_true",
                   help="Only embed records not already indexed (summaries are always refreshed).")
    p.add_argument("--batch-size", type=int, default=128,
                   help="Upsert batch size.")
    return p.parse_args()


def build_records(conn, source, args):
    if source == "activity":
        categories = db.read_categories(conn)
        sessions = db.read_activity_sessions(
            conn, categories, limit=args.limit,
            max_gap_s=args.max_gap, include_idle=args.include_idle,
        )
        return docs.activity_documents(sessions)
    if source == "screenshots":
        return docs.screenshot_documents(db.read_screenshots(conn, args.limit))
    if source == "summaries":
        return docs.summary_documents(db.read_summaries(conn, args.limit))
    if source == "conversations":
        return docs.conversation_documents(db.read_messages(conn, args.limit))
    return []


def main():
    args = parse_args()

    try:
        conn = db.connect(args.db)
    except FileNotFoundError as e:
        print(f"error: {e}", file=sys.stderr)
        print("Pass --db with the path to your focuslens.db.", file=sys.stderr)
        return 1

    print(f"Reading:  {args.db}")
    print(f"Writing:  {args.out}")
    print(f"Embedding: {args.embedding} (local){' [TEST]' if args.embedding == 'hash' else ''}")
    print(f"Sources:  {', '.join(args.collections)}\n")

    ef = store.make_embedding_function(args.embedding)
    client = store.open_client(args.out)

    grand_total = 0
    for source in args.collections:
        t0 = time.time()
        records = build_records(conn, source, args)
        if not records:
            print(f"  {source:<14} no records, skipped")
            continue
        collection = store.get_collection(client, source, ef, reset=args.reset)
        if args.skip_existing and source != "summaries" and not args.reset:
            records = store.filter_new(collection, records)
        n = store.upsert_records(collection, records, batch_size=args.batch_size)
        grand_total += n
        print(f"  {source:<14} {n:>6} docs  ({collection.count()} total in collection)"
              f"  [{time.time() - t0:.1f}s]")

    conn.close()
    print(f"\nDone. {grand_total} documents embedded into {args.out}")
    print("Try a search:  python query.py \"what did I work on in xcode\" -c activity")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
