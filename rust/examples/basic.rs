//! A minimal server: assets are created with `POST /assets?size=<bytes>`, uploaded to `POST /upload-chunk`, and
//! saved to ./uploads when complete.
//!
//!   cargo run --example basic
//!   curl -X POST 'localhost:3000/assets?size=11'          # -> {"asset_id":"asset1"}
//!   curl -F 'metadata={"asset_id":"asset1","chunk":0,"file_type":"video/mp4"}' -F file=@<(printf 'hello world') \
//!        -H 'Authorization: Bearer letmein' localhost:3000/upload-chunk

use std::collections::HashMap;
use std::sync::Mutex;
use std::sync::atomic::{AtomicU64, Ordering};

use axum::extract::{Query, State};
use axum::routing::post;
use axum::{Json, Router};
use interstellar::{Authorization, Backend, ChunkRequest, CompletedUpload, Config, Ingest, Progress, Rejection};
use serde::Deserialize;
use serde_json::{Value, json};

/// Stands in for your database: asset id -> declared size.
#[derive(Default)]
struct Assets {
    sizes: Mutex<HashMap<String, u64>>,
    next: AtomicU64,
}

impl Backend for Assets {
    async fn authorize(&self, request: &ChunkRequest) -> Result<Authorization, Rejection> {
        // Real code checks a session cookie or token belongs to the asset's owner.
        if request.headers.get("authorization").and_then(|v| v.to_str().ok()) != Some("Bearer letmein") {
            return Err(Rejection::unauthorized());
        }
        let size = self.sizes.lock().unwrap().get(&request.asset_id).copied();
        size.map(Authorization::total_size).ok_or_else(Rejection::not_found)
    }

    async fn completed(&self, upload: CompletedUpload) -> Result<(), String> {
        tokio::fs::create_dir_all("uploads").await.map_err(|e| e.to_string())?;
        let destination = format!("uploads/{}.{}", upload.asset_id, upload.file_type);
        tokio::fs::copy(&upload.path, &destination).await.map_err(|e| e.to_string())?;
        // Done with this asset: further chunks for it will be refused by authorize.
        self.sizes.lock().unwrap().remove(&upload.asset_id);
        println!("saved {destination} ({} bytes)", upload.size);
        Ok(())
    }

    async fn progress(&self, progress: Progress) {
        println!("{}: {}/{} bytes", progress.asset_id, progress.uploaded, progress.total_size);
    }
}

#[derive(Deserialize)]
struct CreateAsset {
    size: u64,
}

async fn create_asset(State(ingest): State<Ingest<Assets>>, Query(query): Query<CreateAsset>) -> Json<Value> {
    let assets = ingest.backend();
    let id = format!("asset{}", assets.next.fetch_add(1, Ordering::Relaxed) + 1);
    assets.sizes.lock().unwrap().insert(id.clone(), query.size);
    Json(json!({ "asset_id": id }))
}

#[tokio::main]
async fn main() {
    let ingest = Ingest::new(Assets::default(), Config::default().max_chunk_bytes(32 * 1024 * 1024));
    // Clear out uploads abandoned for a day, hourly.
    ingest.spawn_cleanup(std::time::Duration::from_secs(3600), std::time::Duration::from_secs(86_400));

    let app = Router::new()
        .route("/assets", post(create_asset))
        .with_state(ingest.clone())
        .merge(ingest.router());

    let listener = tokio::net::TcpListener::bind("127.0.0.1:3000").await.unwrap();
    println!("listening on http://127.0.0.1:3000");
    axum::serve(listener, app).await.unwrap();
}
