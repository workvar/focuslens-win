# ─────────────────────────────────────────────────────────────────────────────
# store.py
#
# Thin wrapper around a persistent ChromaDB client: collection naming, the
# local embedding function, and batched upserts.
#
# Embeddings are computed locally by Chroma's bundled all-MiniLM-L6-v2 (ONNX),
# so window titles / OCR text never leave the machine. A test embedding hook is
# provided so the pipeline can be verified without the model download.
# ─────────────────────────────────────────────────────────────────────────────

from __future__ import annotations  # allow "X | None" hints on Python 3.7-3.9

import os

# Maps the short CLI name -> the actual Chroma collection name (3+ chars,
# [a-zA-Z0-9._-], which Chroma requires).
COLLECTIONS = {
    "activity": "focuslens_activity",
    "screenshots": "focuslens_screenshots",
    "summaries": "focuslens_summaries",
    "conversations": "focuslens_conversations",
}
SOURCES = list(COLLECTIONS.keys())


def default_chroma_path() -> str:
    return os.path.expanduser("~/Library/Application Support/FocusLens/chroma")


def make_embedding_function(name: str = "default"):
    """Return a Chroma embedding function.

    "default" -> local all-MiniLM-L6-v2 (downloaded once, then cached).
    "hash"    -> deterministic offline embedder for tests only (not semantic).
    """
    if name == "hash":
        from chromadb.api.types import Documents, Embeddings, EmbeddingFunction
        import hashlib

        class HashEmbeddingFunction(EmbeddingFunction):
            def __init__(self, dims: int = 32):
                self.dims = dims

            def __call__(self, input: "Documents") -> "Embeddings":
                out = []
                for doc in input:
                    h = hashlib.sha256((doc or "").encode()).digest()
                    vec = [(h[i % len(h)]) / 255.0 for i in range(self.dims)]
                    out.append(vec)
                return out

            @staticmethod
            def name() -> str:
                return "focuslens-hash-test"

        return HashEmbeddingFunction()

    from chromadb.utils import embedding_functions
    return embedding_functions.DefaultEmbeddingFunction()


def open_client(path: str):
    import chromadb
    os.makedirs(path, exist_ok=True)
    return chromadb.PersistentClient(path=path)


def get_collection(client, short_name: str, embedding_function, reset: bool = False):
    full = COLLECTIONS[short_name]
    if reset:
        try:
            client.delete_collection(full)
        except Exception:
            pass  # didn't exist yet
    return client.get_or_create_collection(
        name=full,
        embedding_function=embedding_function,
        metadata={"hnsw:space": "cosine"},  # cosine suits sentence embeddings
    )


def upsert_records(collection, records: list[dict], batch_size: int = 128) -> int:
    """Upsert records in batches. De-duplicates ids within this run (last wins)."""
    seen: dict[str, dict] = {}
    for r in records:
        if r["document"]:
            seen[r["id"]] = r
    unique = list(seen.values())

    total = 0
    for i in range(0, len(unique), batch_size):
        chunk = unique[i:i + batch_size]
        collection.upsert(
            ids=[r["id"] for r in chunk],
            documents=[r["document"] for r in chunk],
            metadatas=[r["metadata"] for r in chunk],
        )
        total += len(chunk)
    return total


def filter_new(collection, records: list[dict]) -> list[dict]:
    """Drop records whose id is already indexed, so repeat runs only embed new rows."""
    if not records:
        return records
    have = set(collection.get(include=[])["ids"])
    return [r for r in records if r["id"] not in have]
