use std::future::Future;
use std::path::PathBuf;

use axum::http::{HeaderMap, StatusCode};

/// Your side of an upload. Implement it on whatever holds your app's state (a database pool, config, ...).
///
/// Interstellar handles the protocol, staging, size accounting and reassembly; the backend only answers whether a
/// request may upload to an asset, and takes the finished file.
pub trait Backend: Send + Sync + 'static {
    /// Called before every chunk is stored. Check the request (`request.headers` carries its cookies and
    /// `Authorization`) may upload to `request.asset_id`, and return the asset's total size in bytes: the size the
    /// client declared when you created the asset. The upload completes once its chunks add up to exactly that.
    fn authorize(&self, request: &ChunkRequest) -> impl Future<Output = Result<Authorization, Rejection>> + Send;

    /// Called once, in the background, when every byte is in and the chunks have been joined in order.
    ///
    /// `upload.path` is the assembled file. Interstellar deletes it (and the asset's staging folder) when this
    /// returns, so move or copy it somewhere permanent: storage, a transcoder, ... Returning an error reports the
    /// upload as failed through [`failed`](Self::failed).
    fn completed(&self, upload: CompletedUpload) -> impl Future<Output = Result<(), String>> + Send;

    /// Called after each chunk is stored, with the running total. Optional; use it to show progress elsewhere.
    fn progress(&self, progress: Progress) -> impl Future<Output = ()> + Send {
        let _ = progress;
        async {}
    }

    /// Called if an upload that had every byte couldn't be assembled, or [`completed`](Self::completed) returned an
    /// error. Its chunks have been discarded; the client has to upload it again. Optional.
    fn failed(&self, asset_id: &str, error: &str) -> impl Future<Output = ()> + Send {
        let _ = (asset_id, error);
        async {}
    }
}

/// One incoming chunk, as passed to [`Backend::authorize`].
#[derive(Debug, Clone)]
pub struct ChunkRequest {
    /// The asset the chunk belongs to. Only `A-Z a-z 0-9 _ -` are accepted, so it's safe in paths and keys.
    pub asset_id: String,
    /// The chunk's position in the file, from 0. Chunks can arrive in any order.
    pub chunk: u32,
    /// This chunk's size in bytes.
    pub chunk_size: u64,
    /// The file type, normalised (e.g. `mp4`, `mkv`), and already checked against
    /// [`Config::allowed_file_types`](crate::Config::allowed_file_types).
    pub file_type: String,
    /// The client's `mode`, if it sent one (Interstellar's C# server picks a configuration by it).
    pub mode: Option<String>,
    /// The request's headers, for authentication.
    pub headers: HeaderMap,
}

/// The answer to [`Backend::authorize`] when a chunk may be stored.
#[derive(Debug, Clone, Copy)]
pub struct Authorization {
    /// The whole file's size in bytes.
    pub total_size: u64,
}

impl Authorization {
    pub fn total_size(total_size: u64) -> Self {
        Authorization { total_size }
    }
}

/// The answer to [`Backend::authorize`] when a chunk may not be stored. Sent to the client as
/// `{"ok": false, "error": true, "message": ...}` with `status`.
#[derive(Debug, Clone)]
pub struct Rejection {
    pub status: StatusCode,
    pub message: String,
}

impl Rejection {
    pub fn new(status: StatusCode, message: impl Into<String>) -> Self {
        Rejection { status, message: message.into() }
    }

    pub fn unauthorized() -> Self {
        Self::new(StatusCode::UNAUTHORIZED, "Authentication failed (you must authenticate).")
    }

    pub fn forbidden(message: impl Into<String>) -> Self {
        Self::new(StatusCode::FORBIDDEN, message)
    }

    pub fn not_found() -> Self {
        Self::new(StatusCode::NOT_FOUND, "Asset not found.")
    }

    pub fn bad_request(message: impl Into<String>) -> Self {
        Self::new(StatusCode::BAD_REQUEST, message)
    }

    pub fn internal(message: impl Into<String>) -> Self {
        Self::new(StatusCode::INTERNAL_SERVER_ERROR, message)
    }
}

/// A finished upload, as passed to [`Backend::completed`].
#[derive(Debug, Clone)]
pub struct CompletedUpload {
    pub asset_id: String,
    /// The assembled file. Deleted once `completed` returns.
    pub path: PathBuf,
    /// Its size in bytes (equal to the authorised total).
    pub size: u64,
    /// The file type of the chunk that completed the upload, normalised (e.g. `mp4`).
    pub file_type: String,
    pub mode: Option<String>,
}

/// Running totals, as passed to [`Backend::progress`].
#[derive(Debug, Clone)]
pub struct Progress {
    pub asset_id: String,
    pub uploaded: u64,
    pub total_size: u64,
}

/// So a backend can be shared: `Ingest::new(Arc::new(MyBackend), ...)` while keeping a clone of the `Arc` yourself.
impl<B: Backend> Backend for std::sync::Arc<B> {
    fn authorize(&self, request: &ChunkRequest) -> impl Future<Output = Result<Authorization, Rejection>> + Send {
        (**self).authorize(request)
    }

    fn completed(&self, upload: CompletedUpload) -> impl Future<Output = Result<(), String>> + Send {
        (**self).completed(upload)
    }

    fn progress(&self, progress: Progress) -> impl Future<Output = ()> + Send {
        (**self).progress(progress)
    }

    fn failed(&self, asset_id: &str, error: &str) -> impl Future<Output = ()> + Send {
        (**self).failed(asset_id, error)
    }
}
