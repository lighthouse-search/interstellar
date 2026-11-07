# Interstellar for Rust (axum)

Interstellar chunked uploads, as a library you mount in your own [axum](https://github.com/tokio-rs/axum) server.
No separate ingest service, and no authorisation callbacks over HTTP: your app answers two questions in-process.

```rust
use axum::Router;
use interstellar::{Authorization, Backend, ChunkRequest, CompletedUpload, Config, Ingest, Rejection};

struct Videos { /* your db pool, storage client, ... */ }

impl Backend for Videos {
    // Before every chunk: may this request upload to this asset, and how big is the whole file?
    async fn authorize(&self, request: &ChunkRequest) -> Result<Authorization, Rejection> {
        // look up request.asset_id, check request.headers (cookie / Authorization) belongs to its owner,
        // refuse if it's already uploaded, and return the size it was created with:
        Ok(Authorization::total_size(1024))
    }

    // Once, in the background, when every byte is in and the chunks are joined in order.
    async fn completed(&self, upload: CompletedUpload) -> Result<(), String> {
        // upload.path is deleted when this returns: move it to storage / start transcoding / mark the asset uploaded.
        Ok(())
    }
}

let ingest = Ingest::new(Videos { /* ... */ }, Config::default());
let app = Router::new().nest("/ingest", ingest.router());   // POST /ingest/upload-chunk
```

`progress` (after each chunk) and `failed` (assembly or `completed` failed) are optional hooks. Wrap the backend in an
`Arc` if you want to keep a handle to it: `Backend` is implemented for `Arc<B>`.

## Protocol

The same as Interstellar's C# server, so existing clients work unchanged:

```
POST /upload-chunk   multipart/form-data
  metadata  {"asset_id": "...", "chunk": 0, "file_type": "video/mp4", "mode": "..."?}
  file      the chunk's bytes

200 {"ok": true, "error": false, "type": "chunk_completed" | "processing", "uploaded": n, "max_size": n}
4xx {"ok": false, "error": true, "message": "..."}
```

- Chunks may arrive in any order, several at once; they're joined by index (numerically), not arrival order.
- Re-sending a chunk replaces it rather than counting twice, so clients can retry a failed chunk.
- The upload completes when stored chunks add up to exactly the size `authorize` returned. The chunk that gets it
  there gets `"processing"`; going over is refused (`400`). Exactly one request triggers `completed`.
- Chunks for an upload that's being finished are refused with `409`. After `completed` returns, refusing further
  chunks is up to `authorize` (e.g. the asset is no longer waiting for an upload).
- Asset ids must be `A-Z a-z 0-9 _ -` (refused, not rewritten). File types are normalised (`video/x-matroska` →
  `mkv`) and checked against `Config::allowed_file_types`.

## Configuration

```rust
Config::default()
    .staging_dir("/var/lib/myapp/uploads")   // default: <temp dir>/interstellar
    .max_chunk_bytes(32 * 1024 * 1024)       // default: 64 MiB; the router's body limit follows it
    .allowed_file_types(["mp4", "webm"])      // default: common video containers; .any_file_type() for anything
```

Chunks are staged on disk (`<staging_dir>/<asset_id>/chunks/<index>.chunk`) and the staging folder is the only
bookkeeping, so there's nothing to add to your database. Several instances can share one staging folder on a shared
volume: completion is claimed with an exclusive file create.

Abandoned uploads (a client that gave up half way) stay on disk until removed:

```rust
ingest.spawn_cleanup(Duration::from_secs(3600), Duration::from_secs(86_400)); // hourly, anything idle for a day
```

CORS is yours to add if clients upload from another origin (e.g. `tower_http::cors::CorsLayer` on the router).

## Not included (yet)

The C# server's post-processing (FFmpeg validation, DASH transcoding, upload to S3) isn't part of this crate: you get
the assembled file in `completed` and do whatever your app needs with it.

## Example

```
cargo run --example basic
```

A small server with an in-memory asset table; see `examples/basic.rs` for the curl commands.
