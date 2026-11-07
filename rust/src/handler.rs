use axum::extract::{Multipart, State};
use axum::http::{HeaderMap, StatusCode};
use axum::response::Response;

use crate::backend::{Backend, ChunkRequest, CompletedUpload, Progress};
use crate::protocol::*;
use crate::staging::{self, IncomingFile};
use crate::Ingest;

// POST /upload-chunk
pub(crate) async fn upload_chunk<B: Backend>(State(ingest): State<Ingest<B>>, headers: HeaderMap, mut multipart: Multipart) -> Response {
    let config = ingest.config();
    let staging_dir = &config.staging_dir;

    // Read the parts. The body goes straight to a scratch file (it can be tens of MB), deleted unless it's stored.
    let mut metadata: Option<ChunkMetadata> = None;
    let mut incoming: Option<IncomingFile> = None;
    let mut chunk_size: u64 = 0;

    loop {
        let mut field = match multipart.next_field().await {
            Ok(Some(field)) => field,
            Ok(None) => break,
            Err(e) => return error_response(StatusCode::BAD_REQUEST, &format!("Invalid upload: {e}")),
        };
        match field.name() {
            Some("metadata") => {
                let text = match field.text().await {
                    Ok(text) => text,
                    Err(e) => return error_response(StatusCode::BAD_REQUEST, &format!("Couldn't read metadata: {e}")),
                };
                match serde_json::from_str::<ChunkMetadata>(&text) {
                    Ok(parsed) => metadata = Some(parsed),
                    Err(e) => return error_response(StatusCode::BAD_REQUEST, &format!("metadata is not valid: {e}")),
                }
            }
            Some("file") => {
                let path = match staging::incoming_path(staging_dir).await {
                    Ok(path) => path,
                    Err(e) => return internal_error("couldn't create a scratch file", e),
                };
                let guard = IncomingFile::new(path);
                let mut file = match staging::create_incoming(&guard.path).await {
                    Ok(file) => file,
                    Err(e) => return internal_error("couldn't create a scratch file", e),
                };
                loop {
                    match field.chunk().await {
                        Ok(Some(bytes)) => {
                            chunk_size += bytes.len() as u64;
                            if chunk_size > config.max_chunk_bytes as u64 {
                                return error_response(
                                    StatusCode::PAYLOAD_TOO_LARGE,
                                    &format!("Chunks can't be larger than {} bytes.", config.max_chunk_bytes),
                                );
                            }
                            if let Err(e) = staging::write_all(&mut file, &bytes).await {
                                return internal_error("couldn't write the chunk", e);
                            }
                        }
                        Ok(None) => break,
                        Err(e) => return error_response(StatusCode::BAD_REQUEST, &format!("The chunk was interrupted: {e}")),
                    }
                }
                if let Err(e) = staging::finish(file).await {
                    return internal_error("couldn't write the chunk", e);
                }
                incoming = Some(guard);
            }
            _ => {}
        }
    }

    let Some(metadata) = metadata else {
        return error_response(StatusCode::BAD_REQUEST, "The upload is missing its metadata part.");
    };
    let Some(mut incoming) = incoming else {
        return error_response(StatusCode::BAD_REQUEST, "The upload is missing its file part.");
    };
    if !valid_asset_id(&metadata.asset_id) {
        return error_response(StatusCode::BAD_REQUEST, "metadata.asset_id must be letters, numbers, _ and - only.");
    }
    if metadata.chunk < 0 || metadata.chunk > MAX_CHUNK_INDEX {
        return error_response(StatusCode::BAD_REQUEST, "metadata.chunk is out of range.");
    }
    if chunk_size == 0 {
        return error_response(StatusCode::BAD_REQUEST, "You cannot upload a file that is 0 bytes.");
    }
    let file_type = normalise_file_type(&metadata.file_type, config);
    if file_type.is_empty() {
        return error_response(StatusCode::BAD_REQUEST, "metadata.file_type is missing.");
    }
    if !file_type_allowed(&file_type, config) {
        // Only what the client claims; nothing trusts the bytes on the strength of it.
        return error_response(StatusCode::BAD_REQUEST, &format!("Unsupported file_type, gave .{file_type}."));
    }

    let asset_id = metadata.asset_id;
    let chunk = metadata.chunk as u32;
    let request = ChunkRequest { asset_id: asset_id.clone(), chunk, chunk_size, file_type: file_type.clone(), mode: metadata.mode.clone(), headers };

    let authorization = match ingest.backend().authorize(&request).await {
        Ok(authorization) => authorization,
        Err(rejection) => return error_response(rejection.status, &rejection.message),
    };
    let total = authorization.total_size;

    // One chunk of an asset at a time from here: check the total, store, re-total. Chunks of other assets (and the
    // body uploads above) still run in parallel.
    let lock = ingest.asset_lock(&asset_id);
    let guard = lock.lock().await;

    if staging::is_complete(staging_dir, &asset_id).await {
        drop(guard);
        return error_response(StatusCode::CONFLICT, "This upload is already complete.");
    }
    let others = match staging::uploaded_except(staging_dir, &asset_id, chunk).await {
        Ok(others) => others,
        Err(e) => return internal_error("couldn't read the upload's chunks", e),
    };
    if others + chunk_size > total {
        return error_response(StatusCode::BAD_REQUEST, &format!("You cannot exceed authorised size of {total}"));
    }
    if let Err(e) = staging::store_chunk(staging_dir, &asset_id, chunk, &mut incoming).await {
        return internal_error("couldn't store the chunk", e);
    }
    let uploaded = others + chunk_size;

    let finishing = if uploaded == total {
        let completion = staging::Completion { file_type: file_type.clone(), mode: metadata.mode.clone(), total };
        match staging::claim_completion(staging_dir, &asset_id, &completion).await {
            Ok(claimed) => claimed,
            Err(e) => return internal_error("couldn't mark the upload complete", e),
        }
    } else {
        false
    };
    drop(guard);

    ingest.backend().progress(Progress { asset_id: asset_id.clone(), uploaded, total_size: total }).await;

    if uploaded < total {
        return chunk_response(ChunkStatus::ChunkCompleted, uploaded, total);
    }
    if finishing {
        let ingest = ingest.clone();
        let mode = metadata.mode;
        tokio::spawn(async move { ingest.finish(asset_id, file_type, mode, total).await });
    }
    chunk_response(ChunkStatus::Processing, uploaded, total)
}

fn internal_error(what: &str, error: std::io::Error) -> Response {
    tracing::error!("interstellar: {what}: {error}");
    error_response(StatusCode::INTERNAL_SERVER_ERROR, "Something went wrong storing the chunk.")
}

impl<B: Backend> Ingest<B> {
    /// Joins the chunks, hands the file to the backend, then clears the asset's staging folder either way.
    pub(crate) async fn finish(&self, asset_id: String, file_type: String, mode: Option<String>, total: u64) {
        // resume_pending can race a finish already running in this process; only one runs.
        if !self.start_finishing(&asset_id) {
            return;
        }
        let staging_dir = &self.config().staging_dir;
        let path = staging::asset_dir(staging_dir, &asset_id).join(format!("{asset_id}.{file_type}"));

        let result = match staging::assemble(staging_dir, &asset_id, &path).await {
            Ok(size) if size == total => {
                self.backend().completed(CompletedUpload { asset_id: asset_id.clone(), path, size, file_type, mode }).await
            }
            Ok(size) => Err(format!("assembled {size} bytes, expected {total}")),
            Err(e) => Err(format!("couldn't assemble the chunks: {e}")),
        };

        if let Err(error) = &result {
            tracing::error!("interstellar: finishing {asset_id} failed: {error}");
            self.backend().failed(&asset_id, error).await;
        }
        staging::remove_asset(staging_dir, &asset_id).await;
        self.forget_lock(&asset_id);
        self.stop_finishing(&asset_id);
    }
}
