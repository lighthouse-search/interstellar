use std::collections::HashMap;
use std::path::PathBuf;

/// How an [`Ingest`](crate::Ingest) stages and accepts uploads.
#[derive(Debug, Clone)]
pub struct Config {
    /// Where chunks are written while an upload is in progress, one folder per asset. Each folder is removed once
    /// its upload has been handed to [`Backend::completed`](crate::Backend::completed). Defaults to
    /// `<temp dir>/interstellar`.
    pub staging_dir: PathBuf,
    /// The largest chunk accepted, in bytes. Requests over this (plus a little multipart overhead) are rejected
    /// before they're read. Defaults to 64 MiB.
    pub max_chunk_bytes: usize,
    /// File types accepted, without any `video/` prefix, after [`file_type_aliases`](Self::file_type_aliases) are
    /// applied. Empty accepts anything. Defaults to common video containers.
    pub allowed_file_types: Vec<String>,
    /// Rewrites for the file types clients send, e.g. `xmatroska` (from `video/x-matroska`) to `mkv`. Keys and
    /// values are the sanitised form: lowercase letters and digits only.
    pub file_type_aliases: HashMap<String, String>,
}

impl Default for Config {
    fn default() -> Self {
        let allowed = ["mp4", "m4v", "mov", "webm", "mkv", "avi", "wmv", "flv", "f4v", "mpeg", "mpg", "avchd", "3gp"];
        let aliases = [
            ("xmatroska", "mkv"),
            ("matroska", "mkv"),
            ("quicktime", "mov"),
            ("xmsvideo", "avi"),
            ("xmswmv", "wmv"),
            ("xflv", "flv"),
            ("xm4v", "m4v"),
            ("3gpp", "3gp"),
        ];
        Config {
            staging_dir: std::env::temp_dir().join("interstellar"),
            max_chunk_bytes: 64 * 1024 * 1024,
            allowed_file_types: allowed.iter().map(|s| s.to_string()).collect(),
            file_type_aliases: aliases.iter().map(|(from, to)| (from.to_string(), to.to_string())).collect(),
        }
    }
}

impl Config {
    pub fn staging_dir(mut self, dir: impl Into<PathBuf>) -> Self {
        self.staging_dir = dir.into();
        self
    }

    pub fn max_chunk_bytes(mut self, bytes: usize) -> Self {
        self.max_chunk_bytes = bytes;
        self
    }

    /// Accept any file type.
    pub fn any_file_type(mut self) -> Self {
        self.allowed_file_types.clear();
        self
    }

    pub fn allowed_file_types<I, T>(mut self, types: I) -> Self
    where
        I: IntoIterator<Item = T>,
        T: Into<String>,
    {
        self.allowed_file_types = types.into_iter().map(Into::into).collect();
        self
    }
}
