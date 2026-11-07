//! Interstellar chunked uploads for [axum].
//!
//! Interstellar clients split a file into chunks and POST each one (in any order, several at once) to
//! `/upload-chunk`. This crate is that endpoint, folded into your own server: no separate ingest service and no
//! authorisation callbacks over HTTP. You implement [`Backend`] with two hooks:
//!
//! - [`Backend::authorize`]: may this request upload to this asset, and how big is the whole file?
//! - [`Backend::completed`]: every byte is in; here's the assembled file.
//!
//! and mount the router:
//!
//! ```no_run
//! use axum::Router;
//! use interstellar::{Authorization, Backend, ChunkRequest, CompletedUpload, Config, Ingest, Rejection};
//!
//! struct Videos;
//!
//! impl Backend for Videos {
//!     async fn authorize(&self, request: &ChunkRequest) -> Result<Authorization, Rejection> {
//!         // Look the asset up, check request.headers belongs to its owner, return the size it was created with.
//!         let _ = request;
//!         Ok(Authorization::total_size(1024))
//!     }
//!
//!     async fn completed(&self, upload: CompletedUpload) -> Result<(), String> {
//!         // Move upload.path to storage, start processing, mark the asset uploaded...
//!         tokio::fs::copy(&upload.path, format!("/srv/videos/{}.{}", upload.asset_id, upload.file_type))
//!             .await
//!             .map(|_| ())
//!             .map_err(|e| e.to_string())
//!     }
//! }
//!
//! # async fn run() {
//! let ingest = Ingest::new(Videos, Config::default());
//! let app: Router = Router::new().nest("/ingest", ingest.router());
//! # let _ = app;
//! # }
//! ```
//!
//! The wire format matches Interstellar's C# server, so existing clients work unchanged:
//!
//! ```text
//! POST /upload-chunk   multipart/form-data
//!   metadata  {"asset_id": "...", "chunk": 0, "file_type": "video/mp4", "mode": "..."?}
//!   file      the chunk's bytes
//!
//! 200 {"ok": true, "error": false, "type": "chunk_completed" | "processing", "uploaded": n, "max_size": n}
//! 4xx {"ok": false, "error": true, "message": "..."}
//! ```
//!
//! Chunks are staged on disk under [`Config::staging_dir`] and joined in index order once they add up to the size
//! [`Backend::authorize`] returned. Re-sending a chunk replaces it. Several server instances can share one staging
//! folder (a shared volume): completion is claimed with an exclusive file create, so only one of them finishes an
//! upload.

mod backend;
mod config;
mod handler;
mod protocol;
mod staging;

use std::collections::{HashMap, HashSet};
use std::sync::{Arc, Mutex};
use std::time::Duration;

use axum::Router;
use axum::extract::DefaultBodyLimit;
use axum::routing::post;

pub use backend::{Authorization, Backend, ChunkRequest, CompletedUpload, Progress, Rejection};
pub use config::Config;

// Room for the multipart boundaries and the metadata part on top of a full-size chunk.
const MULTIPART_OVERHEAD: usize = 64 * 1024;

/// Interstellar uploads for one [`Backend`]. Cheap to clone (it's an `Arc` inside).
pub struct Ingest<B: Backend> {
    inner: Arc<Inner<B>>,
}

struct Inner<B> {
    backend: B,
    config: Config,
    // Serialises the store-and-total step per asset. Entries are dropped when an upload finishes.
    locks: Mutex<HashMap<String, Arc<tokio::sync::Mutex<()>>>>,
    // Uploads being handed to the backend by this process right now.
    finishing: Mutex<HashSet<String>>,
}

impl<B: Backend> Clone for Ingest<B> {
    fn clone(&self) -> Self {
        Ingest { inner: self.inner.clone() }
    }
}

impl<B: Backend> Ingest<B> {
    pub fn new(backend: B, config: Config) -> Self {
        Ingest { inner: Arc::new(Inner { backend, config, locks: Mutex::new(HashMap::new()), finishing: Mutex::new(HashSet::new()) }) }
    }

    pub fn backend(&self) -> &B {
        &self.inner.backend
    }

    pub fn config(&self) -> &Config {
        &self.inner.config
    }

    /// `POST /upload-chunk`, with a body limit sized for [`Config::max_chunk_bytes`]. Mount it wherever you like
    /// (`.merge(...)` for the root, `.nest("/ingest", ...)` under a prefix), and add your CORS layer if clients
    /// upload from another origin.
    pub fn router<S>(&self) -> Router<S>
    where
        S: Clone + Send + Sync + 'static,
    {
        Router::new()
            .route(
                "/upload-chunk",
                post(handler::upload_chunk::<B>).layer(DefaultBodyLimit::max(self.config().max_chunk_bytes + MULTIPART_OVERHEAD)),
            )
            .with_state(self.clone())
    }

    /// Deletes uploads nobody has added to for `older_than` (abandoned mid-way), and half-received chunk bodies.
    /// Uploads being finished are left alone. Returns how many uploads were removed.
    pub async fn remove_stale(&self, older_than: Duration) -> std::io::Result<usize> {
        staging::remove_stale(&self.config().staging_dir, older_than).await
    }

    /// Runs [`remove_stale`](Self::remove_stale) every `every`, in the background, for as long as the runtime runs.
    pub fn spawn_cleanup(&self, every: Duration, older_than: Duration) -> tokio::task::JoinHandle<()> {
        let ingest = self.clone();
        tokio::spawn(async move {
            let mut interval = tokio::time::interval(every);
            loop {
                interval.tick().await;
                match ingest.remove_stale(older_than).await {
                    Ok(0) => {}
                    Ok(removed) => tracing::info!("interstellar: removed {removed} abandoned upload(s)"),
                    Err(e) => tracing::warn!("interstellar: cleanup failed: {e}"),
                }
            }
        })
    }

    /// Finishes uploads that had every byte in but weren't handed to [`Backend::completed`] because the process
    /// stopped (a crash, a restart mid-transcode). Call it once at startup; it runs them in the background and
    /// returns how many it started. Their chunks are still staged, so they're reassembled from scratch.
    pub async fn resume_pending(&self) -> std::io::Result<usize> {
        let staging_dir = &self.config().staging_dir;
        let mut started = 0;
        for (asset_id, completion) in staging::pending_completions(staging_dir).await? {
            let completion = match completion {
                Some(completion) => Some(completion),
                None => staging::infer_completion(staging_dir, &asset_id).await.ok().flatten(),
            };
            let Some(completion) = completion else {
                tracing::warn!("interstellar: can't resume {asset_id}: its completion details are missing");
                continue;
            };
            tracing::info!("interstellar: resuming {asset_id}");
            let ingest = self.clone();
            tokio::spawn(async move { ingest.finish(asset_id, completion.file_type, completion.mode, completion.total).await });
            started += 1;
        }
        Ok(started)
    }

    fn start_finishing(&self, asset_id: &str) -> bool {
        let mut finishing = self.inner.finishing.lock().unwrap_or_else(|poisoned| poisoned.into_inner());
        finishing.insert(asset_id.to_string())
    }

    fn stop_finishing(&self, asset_id: &str) {
        let mut finishing = self.inner.finishing.lock().unwrap_or_else(|poisoned| poisoned.into_inner());
        finishing.remove(asset_id);
    }

    fn asset_lock(&self, asset_id: &str) -> Arc<tokio::sync::Mutex<()>> {
        let mut locks = self.inner.locks.lock().unwrap_or_else(|poisoned| poisoned.into_inner());
        locks.entry(asset_id.to_string()).or_default().clone()
    }

    fn forget_lock(&self, asset_id: &str) {
        let mut locks = self.inner.locks.lock().unwrap_or_else(|poisoned| poisoned.into_inner());
        locks.remove(asset_id);
    }
}
