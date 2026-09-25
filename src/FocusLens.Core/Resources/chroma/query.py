#!/usr/bin/env python3
# ─────────────────────────────────────────────────────────────────────────────
# query.py
#
# Run a semantic search against a Chroma collection built by ingest.py.
# Handy for sanity-checking ingestion and for prototyping retrieval before
# wiring it into the app.
#
# Examples:
#   python query.py "time spent coding" -c activity
#   python query.py "error message I saw" -c screenshots -n 3
#   python query.py "what did we discuss about focus score" -c conversations
# ─────────────────────────────────────────────────────────────────────────────

import argparse
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import store


def parse_args():
    p = argparse.ArgumentParser(description="Semantic search over a FocusLens Chroma collection.")
    p.add_argument("query", help="Natural-language search query.")
    p.add_argument("-c", "--collection", default="activity", choices=store.SOURCES,
                   help="Which collection to search. Default: activity.")
    p.add_argument("-n", "--n-results", type=int, default=5, help="Number of results.")
    p.add_argument("--out", default=store.default_chroma_path(),
                   help="Chroma persistence directory.")
    p.add_argument("--embedding", default="default", choices=["default", "hash"],
                   help="Must match what ingest.py used.")
    return p.parse_args()


def main():
    args = parse_args()
    ef = store.make_embedding_function(args.embedding)
    client = store.open_client(args.out)
    collection = store.get_collection(client, args.collection, ef, reset=False)

    if collection.count() == 0:
        print(f"Collection '{args.collection}' is empty. Run ingest.py first.")
        return 1

    res = collection.query(query_texts=[args.query], n_results=args.n_results)
    ids = res["ids"][0]
    docs = res["documents"][0]
    metas = res["metadatas"][0]
    dists = res.get("distances", [[None] * len(ids)])[0]

    print(f"\nTop {len(ids)} results for: {args.query!r}  (collection: {args.collection})\n")
    for rank, (doc, meta, dist) in enumerate(zip(docs, metas, dists), start=1):
        score = f"{1 - dist:.3f}" if dist is not None else "n/a"
        when = meta.get("date") or meta.get("start_ts") or ""
        label = meta.get("app_name") or meta.get("conversation_title") or meta.get("source")
        print(f"{rank}. score={score}  {label}  {when}")
        snippet = doc.replace("\n", " ")
        print(f"   {snippet[:200]}\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
