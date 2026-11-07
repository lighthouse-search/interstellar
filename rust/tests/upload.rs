use std::collections::HashMap;
use std::path::PathBuf;
use std::sync::{Arc, Mutex};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use axum::Router;
use axum::body::Body;
use axum::http::{Request, StatusCode};
use http_body_util::BodyExt;
use interstellar::{Authorization, Backend, ChunkRequest, CompletedUpload, Config, Ingest, Rejection};
use serde_json::Value;
use tower::ServiceExt;

/// Assets and their sizes; records finished uploads.
#[derive(Default)]
struct TestBackend {
    sizes: HashMap<String, u64>,
    completed: Mutex<Vec<(String, Vec<u8>, String)>>,
    failed: Mutex<Vec<String>>,
    finished: tokio::sync::Notify,
}

impl Backend for TestBackend {
    async fn authorize(&self, request: &ChunkRequest) -> Result<Authorization, Rejection> {
        if request.headers.get("authorization").and_then(|v| v.to_str().ok()) != Some("Bearer ok") {
            return Err(Rejection::unauthorized());
        }
        self.sizes.get(&request.asset_id).map(|size| Authorization::total_size(*size)).ok_or_else(Rejection::not_found)
    }

    async fn completed(&self, upload: CompletedUpload) -> Result<(), String> {
        let bytes = tokio::fs::read(&upload.path).await.map_err(|e| e.to_string())?;
        self.completed.lock().unwrap().push((upload.asset_id, bytes, upload.file_type));
        self.finished.notify_one();
        Ok(())
    }

    async fn failed(&self, asset_id: &str, _error: &str) {
        self.failed.lock().unwrap().push(asset_id.to_string());
        self.finished.notify_one();
    }
}

fn staging(name: &str) -> PathBuf {
    let nanos = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos();
    std::env::temp_dir().join(format!("interstellar-it-{name}-{nanos}"))
}

fn setup(name: &str, sizes: &[(&str, u64)], config: Config) -> (Router, Arc<TestBackend>, PathBuf) {
    let dir = staging(name);
    let backend = Arc::new(TestBackend { sizes: sizes.iter().map(|(id, size)| (id.to_string(), *size)).collect(), ..Default::default() });
    let ingest = Ingest::new(backend.clone(), config.staging_dir(&dir));
    (Router::new().merge(ingest.router()), backend, dir)
}

fn chunk_request(asset: &str, chunk: i64, file_type: &str, bytes: &[u8], auth: &str) -> Request<Body> {
    let boundary = "XinterstellarX";
    let mut body = Vec::new();
    body.extend_from_slice(format!("--{boundary}\r\nContent-Disposition: form-data; name=\"metadata\"\r\n\r\n").as_bytes());
    body.extend_from_slice(format!(r#"{{"asset_id":"{asset}","chunk":{chunk},"file_type":"{file_type}","mode":"default"}}"#).as_bytes());
    body.extend_from_slice(format!("\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"blob\"\r\nContent-Type: application/octet-stream\r\n\r\n").as_bytes());
    body.extend_from_slice(bytes);
    body.extend_from_slice(format!("\r\n--{boundary}--\r\n").as_bytes());
    Request::post("/upload-chunk")
        .header("content-type", format!("multipart/form-data; boundary={boundary}"))
        .header("authorization", auth)
        .body(Body::from(body))
        .unwrap()
}

async fn send(app: &Router, request: Request<Body>) -> (StatusCode, Value) {
    let response = app.clone().oneshot(request).await.unwrap();
    let status = response.status();
    let bytes = response.into_body().collect().await.unwrap().to_bytes();
    (status, serde_json::from_slice(&bytes).unwrap_or(Value::Null))
}

async fn wait_finished(backend: &TestBackend) {
    tokio::time::timeout(Duration::from_secs(5), backend.finished.notified()).await.expect("upload never finished");
}

#[tokio::test]
async fn out_of_order_chunks_assemble_and_complete_once() {
    let (app, backend, dir) = setup("order", &[("vid", 10)], Config::default());

    let (status, body) = send(&app, chunk_request("vid", 2, "video/mp4", b"IJ", "Bearer ok")).await;
    assert_eq!(status, StatusCode::OK);
    assert_eq!((body["error"].as_bool(), body["type"].as_str(), body["uploaded"].as_u64()), (Some(false), Some("chunk_completed"), Some(2)));

    let (_, body) = send(&app, chunk_request("vid", 0, "video/mp4", b"ABCD", "Bearer ok")).await;
    assert_eq!(body["type"], "chunk_completed");

    let (_, body) = send(&app, chunk_request("vid", 1, "video/mp4", b"EFGH", "Bearer ok")).await;
    assert_eq!((body["type"].as_str(), body["uploaded"].as_u64(), body["max_size"].as_u64()), (Some("processing"), Some(10), Some(10)));

    wait_finished(&backend).await;
    let completed = backend.completed.lock().unwrap().clone();
    assert_eq!(completed, vec![("vid".to_string(), b"ABCDEFGHIJ".to_vec(), "mp4".to_string())]);

    // Staging is cleared once the backend has the file.
    tokio::time::sleep(Duration::from_millis(50)).await;
    assert!(!dir.join("vid").exists());
    let _ = tokio::fs::remove_dir_all(&dir).await;
}

#[tokio::test]
async fn parallel_chunks_complete_exactly_once() {
    let chunks: Vec<Vec<u8>> = (0..8u8).map(|i| vec![b'a' + i; 1000]).collect();
    let (app, backend, dir) = setup("parallel", &[("vid", 8000)], Config::default());

    let mut tasks = Vec::new();
    for (index, bytes) in chunks.iter().enumerate().rev() {
        let app = app.clone();
        let request = chunk_request("vid", index as i64, "video/webm", bytes, "Bearer ok");
        tasks.push(tokio::spawn(async move { send(&app, request).await }));
    }
    let mut processing = 0;
    for task in tasks {
        let (status, body) = task.await.unwrap();
        assert_eq!(status, StatusCode::OK);
        if body["type"] == "processing" {
            processing += 1;
        }
    }
    assert_eq!(processing, 1);

    wait_finished(&backend).await;
    let completed = backend.completed.lock().unwrap().clone();
    assert_eq!(completed.len(), 1);
    assert_eq!(completed[0].1, chunks.concat());
    let _ = tokio::fs::remove_dir_all(&dir).await;
}

#[tokio::test]
async fn resent_chunk_replaces_and_oversize_is_refused() {
    let (app, backend, dir) = setup("limits", &[("vid", 6)], Config::default());

    send(&app, chunk_request("vid", 0, "mp4", b"AAA", "Bearer ok")).await;
    // Re-sending chunk 0 replaces it rather than counting twice.
    let (_, body) = send(&app, chunk_request("vid", 0, "mp4", b"BBB", "Bearer ok")).await;
    assert_eq!(body["uploaded"].as_u64(), Some(3));

    let (status, body) = send(&app, chunk_request("vid", 1, "mp4", b"CCCC", "Bearer ok")).await;
    assert_eq!(status, StatusCode::BAD_REQUEST);
    assert_eq!(body["error"], true);
    assert_eq!(body["message"], "You cannot exceed authorised size of 6");

    let (_, body) = send(&app, chunk_request("vid", 1, "mp4", b"CCC", "Bearer ok")).await;
    assert_eq!(body["type"], "processing");
    wait_finished(&backend).await;
    assert_eq!(backend.completed.lock().unwrap()[0].1, b"BBBCCC");
    let _ = tokio::fs::remove_dir_all(&dir).await;
}

#[tokio::test]
async fn the_backend_decides_who_may_upload() {
    let (app, _backend, dir) = setup("auth", &[("vid", 4)], Config::default());

    let (status, body) = send(&app, chunk_request("vid", 0, "mp4", b"AB", "Bearer nope")).await;
    assert_eq!(status, StatusCode::UNAUTHORIZED);
    assert_eq!(body["error"], true);

    let (status, _) = send(&app, chunk_request("missing", 0, "mp4", b"AB", "Bearer ok")).await;
    assert_eq!(status, StatusCode::NOT_FOUND);

    // Nothing is left behind from refused chunks.
    assert!(!dir.join("vid").exists());
    let _ = tokio::fs::remove_dir_all(&dir).await;
}

#[tokio::test]
async fn bad_requests_are_refused_before_anything_is_stored() {
    let (app, _backend, dir) = setup("bad", &[("vid", 4)], Config::default().max_chunk_bytes(8));

    let (status, body) = send(&app, chunk_request("../etc", 0, "mp4", b"AB", "Bearer ok")).await;
    assert_eq!(status, StatusCode::BAD_REQUEST);
    assert!(body["message"].as_str().unwrap().contains("asset_id"));

    let (status, body) = send(&app, chunk_request("vid", 0, "application/x-msdownload", b"AB", "Bearer ok")).await;
    assert_eq!(status, StatusCode::BAD_REQUEST);
    assert!(body["message"].as_str().unwrap().starts_with("Unsupported file_type"));

    let (status, _) = send(&app, chunk_request("vid", -1, "mp4", b"AB", "Bearer ok")).await;
    assert_eq!(status, StatusCode::BAD_REQUEST);

    let (status, _) = send(&app, chunk_request("vid", 0, "mp4", b"", "Bearer ok")).await;
    assert_eq!(status, StatusCode::BAD_REQUEST);

    let (status, _) = send(&app, chunk_request("vid", 0, "mp4", b"0123456789", "Bearer ok")).await;
    assert_eq!(status, StatusCode::PAYLOAD_TOO_LARGE);

    assert!(!dir.join("vid").exists());
    let incoming = dir.join(".incoming");
    if incoming.exists() {
        assert_eq!(std::fs::read_dir(&incoming).unwrap().count(), 0, "scratch files were left behind");
    }
    let _ = tokio::fs::remove_dir_all(&dir).await;
}

/// Holds the hand-off open until released, so a late chunk arrives while the upload is still being finished.
struct SlowBackend {
    release: tokio::sync::Notify,
    done: tokio::sync::Notify,
}

impl Backend for SlowBackend {
    async fn authorize(&self, _request: &ChunkRequest) -> Result<Authorization, Rejection> {
        Ok(Authorization::total_size(2))
    }

    async fn completed(&self, _upload: CompletedUpload) -> Result<(), String> {
        self.release.notified().await;
        self.done.notify_one();
        Ok(())
    }
}

#[tokio::test]
async fn chunks_after_completion_are_refused() {
    let dir = staging("late");
    let backend = Arc::new(SlowBackend { release: tokio::sync::Notify::new(), done: tokio::sync::Notify::new() });
    let app: Router = Router::new().merge(Ingest::new(backend.clone(), Config::default().staging_dir(&dir)).router());

    let (_, body) = send(&app, chunk_request("vid", 0, "mp4", b"AB", "Bearer ok")).await;
    assert_eq!(body["type"], "processing");

    let (status, body) = send(&app, chunk_request("vid", 1, "mp4", b"C", "Bearer ok")).await;
    assert_eq!(status, StatusCode::CONFLICT);
    assert_eq!(body["message"], "This upload is already complete.");

    backend.release.notify_one();
    tokio::time::timeout(Duration::from_secs(5), backend.done.notified()).await.expect("upload never finished");
    let _ = tokio::fs::remove_dir_all(&dir).await;
}

#[tokio::test]
async fn interrupted_finishes_are_resumed_at_startup() {
    let dir = staging("resume");
    // An upload that had every byte in when the process stopped: chunks staged, completion claimed.
    let chunks = dir.join("vid").join("chunks");
    tokio::fs::create_dir_all(&chunks).await.unwrap();
    tokio::fs::write(chunks.join("1.chunk"), b"DEF").await.unwrap();
    tokio::fs::write(chunks.join("0.chunk"), b"ABC").await.unwrap();
    tokio::fs::write(dir.join("vid").join(".complete"), br#"{"file_type":"mkv","mode":null,"total":6}"#).await.unwrap();

    // One from before markers held details: empty marker, a half-written assembled file to infer the type from.
    let old_chunks = dir.join("old").join("chunks");
    tokio::fs::create_dir_all(&old_chunks).await.unwrap();
    tokio::fs::write(old_chunks.join("0.chunk"), b"XY").await.unwrap();
    tokio::fs::write(dir.join("old").join(".complete"), b"").await.unwrap();
    tokio::fs::write(dir.join("old").join("old.mov"), b"partial").await.unwrap();

    let backend = Arc::new(TestBackend::default());
    let ingest = Ingest::new(backend.clone(), Config::default().staging_dir(&dir));
    assert_eq!(ingest.resume_pending().await.unwrap(), 2);
    wait_finished(&backend).await;
    wait_finished(&backend).await;

    let mut completed = backend.completed.lock().unwrap().clone();
    completed.sort();
    assert_eq!(completed, vec![
        ("old".to_string(), b"XY".to_vec(), "mov".to_string()),
        ("vid".to_string(), b"ABCDEF".to_vec(), "mkv".to_string()),
    ]);
    tokio::time::sleep(Duration::from_millis(50)).await;
    assert!(!dir.join("vid").exists() && !dir.join("old").exists());
    // Nothing left to resume.
    assert_eq!(ingest.resume_pending().await.unwrap(), 0);
    let _ = tokio::fs::remove_dir_all(&dir).await;
}
