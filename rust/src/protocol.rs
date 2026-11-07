// The wire format, kept compatible with Interstellar's C# server and its clients:
//
//   POST /upload-chunk   multipart/form-data
//     metadata  JSON text: {"asset_id", "chunk", "file_type", "mode"?, "size"?}
//     file      the chunk's bytes
//
//   200 {"ok": true, "error": false, "type": "chunk_completed" | "processing", "uploaded", "max_size"}
//   4xx {"ok": false, "error": true, "message"}
//
// Clients check `error == false` and stop once `type` is "processing".

use axum::Json;
use axum::http::StatusCode;
use axum::response::{IntoResponse, Response};
use serde::Deserialize;
use serde_json::json;

use crate::config::Config;

#[derive(Debug, Deserialize)]
pub(crate) struct ChunkMetadata {
    pub asset_id: String,
    pub chunk: i64,
    pub file_type: String,
    #[serde(default)]
    pub mode: Option<String>,
}

pub(crate) const MAX_CHUNK_INDEX: i64 = 1_000_000;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum ChunkStatus {
    ChunkCompleted,
    Processing,
}

pub(crate) fn chunk_response(status: ChunkStatus, uploaded: u64, total: u64) -> Response {
    let kind = match status {
        ChunkStatus::ChunkCompleted => "chunk_completed",
        ChunkStatus::Processing => "processing",
    };
    Json(json!({ "ok": true, "error": false, "type": kind, "uploaded": uploaded, "max_size": total })).into_response()
}

pub(crate) fn error_response(status: StatusCode, message: &str) -> Response {
    (status, Json(json!({ "ok": false, "error": true, "message": message }))).into_response()
}

/// Asset ids become folder names, so only `A-Z a-z 0-9 _ -` are allowed. Anything else is refused rather than
/// stripped, so two different ids can't land in the same folder.
pub(crate) fn valid_asset_id(id: &str) -> bool {
    !id.is_empty() && id.len() <= 200 && id.chars().all(|c| c.is_ascii_alphanumeric() || c == '_' || c == '-')
}

/// `video/x-matroska` -> `xmatroska` -> (alias) `mkv`. Lowercase letters and digits only, like the C# server's
/// file_type_sanitize.
pub(crate) fn normalise_file_type(raw: &str, config: &Config) -> String {
    let sanitised: String = raw
        .trim()
        .trim_start_matches("video/")
        .chars()
        .filter(|c| c.is_ascii_alphanumeric())
        .collect::<String>()
        .to_ascii_lowercase();
    config.file_type_aliases.get(&sanitised).cloned().unwrap_or(sanitised)
}

pub(crate) fn file_type_allowed(file_type: &str, config: &Config) -> bool {
    config.allowed_file_types.is_empty() || config.allowed_file_types.iter().any(|allowed| allowed == file_type)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn asset_ids_are_refused_not_rewritten() {
        assert!(valid_asset_id("ABC_123-def"));
        assert!(valid_asset_id("0b8e5c9a-1f2d-4c3b-9a8e-7d6c5b4a3f21"));
        assert!(!valid_asset_id("../../etc/passwd"));
        assert!(!valid_asset_id("a/b"));
        assert!(!valid_asset_id("a.b"));
        assert!(!valid_asset_id(""));
    }

    #[test]
    fn file_types_are_normalised_and_aliased() {
        let config = Config::default();
        assert_eq!(normalise_file_type("video/mp4", &config), "mp4");
        assert_eq!(normalise_file_type(".MKV", &config), "mkv");
        assert_eq!(normalise_file_type("video/x-matroska", &config), "mkv");
        assert_eq!(normalise_file_type("video/quicktime", &config), "mov");
        assert!(file_type_allowed("mp4", &config));
        assert!(!file_type_allowed("exe", &config));
        assert!(file_type_allowed("exe", &Config::default().any_file_type()));
    }
}
