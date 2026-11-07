// On-disk staging. Each asset gets a folder:
//
//   <staging_dir>/<asset_id>/chunks/<index>.chunk   one file per chunk, replaced if re-sent
//   <staging_dir>/<asset_id>/.complete              created (exclusively) by whichever chunk completed the upload
//   <staging_dir>/<asset_id>/<asset_id>.<type>      the assembled file, handed to Backend::completed
//   <staging_dir>/.incoming/<n>                     chunk bodies while they're being received
//
// The chunk files themselves are the bookkeeping: an asset's uploaded total is the sum of their sizes, so there's no
// database to keep in step and a resent chunk replaces rather than double-counts.

use std::io;
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use tokio::io::AsyncWriteExt;

const INCOMING: &str = ".incoming";
const COMPLETE_MARKER: &str = ".complete";

pub(crate) fn asset_dir(staging: &Path, asset_id: &str) -> PathBuf {
    staging.join(asset_id)
}

fn chunks_dir(staging: &Path, asset_id: &str) -> PathBuf {
    asset_dir(staging, asset_id).join("chunks")
}

/// A fresh path to receive a chunk body into, before we know (or have checked) which asset it's for.
pub(crate) async fn incoming_path(staging: &Path) -> io::Result<PathBuf> {
    static COUNTER: AtomicU64 = AtomicU64::new(0);
    let dir = staging.join(INCOMING);
    tokio::fs::create_dir_all(&dir).await?;
    let nanos = SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_nanos()).unwrap_or(0);
    Ok(dir.join(format!("{}-{}-{}", std::process::id(), nanos, COUNTER.fetch_add(1, Ordering::Relaxed))))
}

/// Deletes a received chunk body unless it was moved into place.
pub(crate) struct IncomingFile {
    pub path: PathBuf,
    keep: bool,
}

impl IncomingFile {
    pub fn new(path: PathBuf) -> Self {
        IncomingFile { path, keep: false }
    }

    fn kept(&mut self) {
        self.keep = true;
    }
}

impl Drop for IncomingFile {
    fn drop(&mut self) {
        if !self.keep {
            let _ = std::fs::remove_file(&self.path);
        }
    }
}

/// Appends to a received chunk body.
pub(crate) async fn create_incoming(path: &Path) -> io::Result<tokio::fs::File> {
    tokio::fs::File::create(path).await
}

pub(crate) async fn write_all(file: &mut tokio::fs::File, bytes: &[u8]) -> io::Result<()> {
    file.write_all(bytes).await
}

pub(crate) async fn finish(mut file: tokio::fs::File) -> io::Result<()> {
    file.flush().await?;
    file.sync_all().await
}

pub(crate) async fn is_complete(staging: &Path, asset_id: &str) -> bool {
    tokio::fs::try_exists(asset_dir(staging, asset_id).join(COMPLETE_MARKER)).await.unwrap_or(false)
}

/// Sizes of the asset's stored chunks, by index.
async fn chunk_sizes(staging: &Path, asset_id: &str) -> io::Result<Vec<(u32, u64)>> {
    let dir = chunks_dir(staging, asset_id);
    let mut entries = match tokio::fs::read_dir(&dir).await {
        Ok(entries) => entries,
        Err(e) if e.kind() == io::ErrorKind::NotFound => return Ok(Vec::new()),
        Err(e) => return Err(e),
    };
    let mut sizes = Vec::new();
    while let Some(entry) = entries.next_entry().await? {
        let name = entry.file_name();
        let Some(index) = name.to_str().and_then(|n| n.strip_suffix(".chunk")).and_then(|n| n.parse::<u32>().ok()) else {
            continue;
        };
        sizes.push((index, entry.metadata().await?.len()));
    }
    Ok(sizes)
}

/// Bytes stored for the asset, not counting chunk `except` (which is about to be replaced).
pub(crate) async fn uploaded_except(staging: &Path, asset_id: &str, except: u32) -> io::Result<u64> {
    Ok(chunk_sizes(staging, asset_id).await?.into_iter().filter(|(index, _)| *index != except).map(|(_, size)| size).sum())
}

/// Moves a received chunk body into place as chunk `index`, replacing any earlier copy.
pub(crate) async fn store_chunk(staging: &Path, asset_id: &str, index: u32, incoming: &mut IncomingFile) -> io::Result<()> {
    let dir = chunks_dir(staging, asset_id);
    tokio::fs::create_dir_all(&dir).await?;
    tokio::fs::rename(&incoming.path, dir.join(format!("{index}.chunk"))).await?;
    incoming.kept();
    Ok(())
}

/// What finishing an upload needs, kept in its completion marker so an interrupted finish can be resumed.
#[derive(Debug, Clone, serde::Serialize, serde::Deserialize)]
pub(crate) struct Completion {
    pub file_type: String,
    pub mode: Option<String>,
    pub total: u64,
}

/// Claims the right to finish the upload. Only the first caller (across processes sharing the staging folder, too)
/// gets `true`.
pub(crate) async fn claim_completion(staging: &Path, asset_id: &str, completion: &Completion) -> io::Result<bool> {
    let marker = asset_dir(staging, asset_id).join(COMPLETE_MARKER);
    match tokio::fs::OpenOptions::new().write(true).create_new(true).open(&marker).await {
        Ok(mut file) => {
            let json = serde_json::to_vec(completion).map_err(io::Error::other)?;
            file.write_all(&json).await?;
            file.sync_all().await?;
            Ok(true)
        }
        Err(e) if e.kind() == io::ErrorKind::AlreadyExists => Ok(false),
        Err(e) => Err(e),
    }
}

/// Uploads that were claimed for finishing but whose folders are still here: the process stopped (crashed, was
/// restarted) before handing them over. Markers from before they held their details come back as `None`.
pub(crate) async fn pending_completions(staging: &Path) -> io::Result<Vec<(String, Option<Completion>)>> {
    let mut pending = Vec::new();
    let mut entries = match tokio::fs::read_dir(staging).await {
        Ok(entries) => entries,
        Err(e) if e.kind() == io::ErrorKind::NotFound => return Ok(pending),
        Err(e) => return Err(e),
    };
    while let Some(entry) = entries.next_entry().await? {
        let Some(asset_id) = entry.file_name().to_str().map(str::to_string) else { continue };
        if asset_id == INCOMING || !entry.file_type().await?.is_dir() {
            continue;
        }
        let marker = entry.path().join(COMPLETE_MARKER);
        let Ok(contents) = tokio::fs::read(&marker).await else { continue };
        pending.push((asset_id, serde_json::from_slice(&contents).ok()));
    }
    Ok(pending)
}

/// For a marker without details: the file type from a previously assembled `<asset_id>.<type>`, and the total from
/// the stored chunks.
pub(crate) async fn infer_completion(staging: &Path, asset_id: &str) -> io::Result<Option<Completion>> {
    let total = uploaded_except(staging, asset_id, u32::MAX).await?;
    let prefix = format!("{asset_id}.");
    let mut entries = tokio::fs::read_dir(asset_dir(staging, asset_id)).await?;
    while let Some(entry) = entries.next_entry().await? {
        if let Some(file_type) = entry.file_name().to_str().and_then(|name| name.strip_prefix(&prefix)) {
            if !file_type.is_empty() && total > 0 {
                return Ok(Some(Completion { file_type: file_type.to_string(), mode: None, total }));
            }
        }
    }
    Ok(None)
}

/// Joins `<index>.chunk` files in numeric index order (not arrival or name order: "10" sorts before "2"),
/// returning the bytes written.
pub(crate) async fn assemble(staging: &Path, asset_id: &str, output: &Path) -> io::Result<u64> {
    let mut indexes: Vec<u32> = chunk_sizes(staging, asset_id).await?.into_iter().map(|(index, _)| index).collect();
    if indexes.is_empty() {
        return Err(io::Error::new(io::ErrorKind::NotFound, "no chunks to assemble"));
    }
    indexes.sort_unstable();

    let dir = chunks_dir(staging, asset_id);
    let mut out = tokio::fs::File::create(output).await?;
    let mut written = 0u64;
    for index in indexes {
        let mut part = tokio::fs::File::open(dir.join(format!("{index}.chunk"))).await?;
        written += tokio::io::copy(&mut part, &mut out).await?;
    }
    out.flush().await?;
    out.sync_all().await?;
    Ok(written)
}

pub(crate) async fn remove_asset(staging: &Path, asset_id: &str) {
    let dir = asset_dir(staging, asset_id);
    if let Err(e) = tokio::fs::remove_dir_all(&dir).await {
        if e.kind() != io::ErrorKind::NotFound {
            tracing::warn!("interstellar: couldn't remove {}: {e}", dir.display());
        }
    }
}

/// Removes staging folders (and half-received bodies) untouched for longer than `older_than`, skipping uploads that
/// are being finished. Returns how many asset folders were removed.
pub(crate) async fn remove_stale(staging: &Path, older_than: Duration) -> io::Result<usize> {
    let cutoff = SystemTime::now().checked_sub(older_than).unwrap_or(UNIX_EPOCH);
    let mut removed = 0;
    let mut entries = match tokio::fs::read_dir(staging).await {
        Ok(entries) => entries,
        Err(e) if e.kind() == io::ErrorKind::NotFound => return Ok(0),
        Err(e) => return Err(e),
    };
    while let Some(entry) = entries.next_entry().await? {
        let path = entry.path();
        let name = entry.file_name();
        if name == INCOMING {
            let mut incoming = tokio::fs::read_dir(&path).await?;
            while let Some(file) = incoming.next_entry().await? {
                if last_modified(&file.path()).await.is_some_and(|m| m < cutoff) {
                    let _ = tokio::fs::remove_file(file.path()).await;
                }
            }
            continue;
        }
        if !entry.file_type().await?.is_dir() || tokio::fs::try_exists(path.join(COMPLETE_MARKER)).await.unwrap_or(false) {
            continue;
        }
        // An asset's last activity is its newest chunk.
        let newest = newest_modified(&path.join("chunks")).await.or(last_modified(&path).await);
        if newest.is_some_and(|m| m < cutoff) {
            tokio::fs::remove_dir_all(&path).await?;
            removed += 1;
        }
    }
    Ok(removed)
}

async fn last_modified(path: &Path) -> Option<SystemTime> {
    tokio::fs::metadata(path).await.ok()?.modified().ok()
}

async fn newest_modified(dir: &Path) -> Option<SystemTime> {
    let mut entries = tokio::fs::read_dir(dir).await.ok()?;
    let mut newest = None;
    while let Ok(Some(entry)) = entries.next_entry().await {
        if let Some(modified) = last_modified(&entry.path()).await {
            newest = Some(newest.map_or(modified, |n: SystemTime| n.max(modified)));
        }
    }
    newest
}

#[cfg(test)]
mod tests {
    use super::*;

    fn test_dir(name: &str) -> PathBuf {
        let nanos = SystemTime::now().duration_since(UNIX_EPOCH).unwrap().as_nanos();
        std::env::temp_dir().join(format!("interstellar-test-{name}-{nanos}"))
    }

    async fn put(staging: &Path, asset: &str, index: u32, bytes: &[u8]) {
        let path = incoming_path(staging).await.unwrap();
        tokio::fs::write(&path, bytes).await.unwrap();
        store_chunk(staging, asset, index, &mut IncomingFile::new(path)).await.unwrap();
    }

    #[tokio::test]
    async fn assembles_in_numeric_index_order() {
        let staging = test_dir("order");
        // Out of order, and with "10" so name order would disagree with numeric order.
        put(&staging, "a", 10, b"K").await;
        put(&staging, "a", 2, b"CD").await;
        put(&staging, "a", 0, b"AB").await;
        put(&staging, "a", 1, b"XY").await;

        let out = staging.join("out");
        assert_eq!(assemble(&staging, "a", &out).await.unwrap(), 7);
        assert_eq!(tokio::fs::read(&out).await.unwrap(), b"ABXYCDK");
        let _ = tokio::fs::remove_dir_all(&staging).await;
    }

    #[tokio::test]
    async fn resent_chunks_replace_rather_than_add() {
        let staging = test_dir("resend");
        put(&staging, "a", 0, b"AAAA").await;
        put(&staging, "a", 1, b"BB").await;
        put(&staging, "a", 1, b"CCC").await;
        assert_eq!(uploaded_except(&staging, "a", u32::MAX).await.unwrap(), 7);
        assert_eq!(uploaded_except(&staging, "a", 1).await.unwrap(), 4);
        let _ = tokio::fs::remove_dir_all(&staging).await;
    }

    #[tokio::test]
    async fn only_one_caller_claims_completion() {
        let staging = test_dir("claim");
        tokio::fs::create_dir_all(asset_dir(&staging, "a")).await.unwrap();
        let completion = Completion { file_type: "mp4".into(), mode: Some("m".into()), total: 3 };
        assert!(claim_completion(&staging, "a", &completion).await.unwrap());
        assert!(!claim_completion(&staging, "a", &completion).await.unwrap());
        assert!(is_complete(&staging, "a").await);
        let pending = pending_completions(&staging).await.unwrap();
        assert_eq!(pending.len(), 1);
        assert_eq!(pending[0].0, "a");
        assert_eq!(pending[0].1.as_ref().map(|c| (c.file_type.as_str(), c.total)), Some(("mp4", 3)));
        let _ = tokio::fs::remove_dir_all(&staging).await;
    }

    #[tokio::test]
    async fn incoming_files_are_removed_unless_stored() {
        let staging = test_dir("incoming");
        let path = incoming_path(&staging).await.unwrap();
        tokio::fs::write(&path, b"x").await.unwrap();
        drop(IncomingFile::new(path.clone()));
        assert!(!path.exists());
        let _ = tokio::fs::remove_dir_all(&staging).await;
    }

    #[tokio::test]
    async fn stale_uploads_are_removed_and_finishing_ones_kept() {
        let staging = test_dir("stale");
        put(&staging, "old", 0, b"x").await;
        put(&staging, "finishing", 0, b"x").await;
        claim_completion(&staging, "finishing", &Completion { file_type: "mp4".into(), mode: None, total: 1 }).await.unwrap();
        assert_eq!(remove_stale(&staging, Duration::ZERO).await.unwrap(), 1);
        assert!(!asset_dir(&staging, "old").exists());
        assert!(asset_dir(&staging, "finishing").exists());
        let _ = tokio::fs::remove_dir_all(&staging).await;
    }
}
