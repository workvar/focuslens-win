#!/usr/bin/env python3
# ─────────────────────────────────────────────────────────────────────────────
# search.py
#
# Machine-readable semantic search for the FocusLens apps. Prints one JSON
# object to stdout: {"hits": [...], "counts": {...}}. Errors go to stderr with
# a non-zero exit code. Human-friendly output lives in query.py.
#
#   python search.py "what did I read about tokens" -n 6
#   python search.py --stats
# ─────────────────────────────────────────────────────────────────────────────

import argparse
import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import store


def parse_args():
    p = argparse.ArgumentParser(description="JSON semantic search over FocusLens Chroma collections.")
    p.add_argument("query", nargs="?", default="", help="Natural-language query.")
    p.add_argument("-c", "--collections", nargs="+", choices=store.SOURCES, default=store.SOURCES)
    p.add_argument("-n", "--n-results", type=int, default=6, help="Results per collection.")
    p.add_argument("--min-score", type=float, default=0.25, help="Drop hits scoring below this (1 - cosine distance).")
    p.add_argument("--max-chars", type=int, default=600, help="Truncate each document to this many characters.")
    p.add_argument("--out", default=store.default_chroma_path(), help="Chroma persistence directory.")
    p.add_argument("--embedding", default="default", choices=["default", "hash"])
    p.add_argument("--stats", action="store_true", help="Print collection sizes only.")
    return p.parse_args()


def counts(client, ef) -> dict:
    return {s: store.get_collection(client, s, ef).count() for s in store.SOURCES}


def search(client, ef, args) -> list[dict]:
    hits = []
    for source in args.collections:
        col = store.get_collection(client, source, ef)
        if col.count() == 0:
            continue
        res = col.query(query_texts=[args.query], n_results=min(args.n_results, col.count()))
        for doc, meta, dist in zip(res["documents"][0], res["metadatas"][0], res["distances"][0]):
            score = 1 - dist
            if score < args.min_score:
                continue
            hits.append({"source": source, "score": round(score, 4),
                         "text": (doc or "")[:args.max_chars], "meta": meta or {}})
    hits.sort(key=lambda h: h["score"], reverse=True)
    return hits


def main() -> int:
    args = parse_args()
    if not args.stats and not args.query.strip():
        print("error: a query or --stats is required", file=sys.stderr)
        return 2
    ef = store.make_embedding_function(args.embedding)
    client = store.open_client(args.out)
    out = {"counts": counts(client, ef), "hits": [] if args.stats else search(client, ef, args)}
    print(json.dumps(out, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
