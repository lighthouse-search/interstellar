pub mod chunk;
pub mod authentication;
pub mod structs;
pub mod file {
    pub mod chunk;
}

use std::fs;
use std::env;
use std::collections::HashMap;
use std::path::Path;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::Arc;

use serde::{Serialize, Deserialize};
use serde_json::json;

use tokio::task;

use crate::structs::*;

#[tokio::main]
async fn main() {
    let args: Vec<String> = env::args().collect();

    // Parse arguments and move into a hashmap.
    let mut arguments: HashMap<String, Command_argument> = HashMap::new();
    let mut args_iter = args.iter();
    args_iter.next();
    while let Some(arg) = args_iter.next() {
        if arg.starts_with("--") {
            if let Some(value) = args_iter.next() {
                arguments.insert(arg.replace("--", ""), Command_argument {
                    value: value.clone()
                });
            } else {
                eprintln!("Error: -- requires a value.");
                return;
            }
        } else {
            // Any argument that doesn't have "--" oin it. e.g. in "interstellar upload --url example.com", "upload" would get caught here.
            println!("arg COOL: {}", arg);
        }
    }

    // Get the specified file path
    let file_path: String = arguments.get("file").unwrap().value.clone();

    // Get the file type, if specified.
    let file_type: String = Path::new(&file_path)
        .extension()
        .and_then(|ext| ext.to_str())
        .map(|s| s.to_owned())  // Convert &str to an owned String
        .expect("Failed to determine file type.");

    // Get the specific host of the Interstellar server. [TODO: Link to docs]
    let host: String = arguments.get("host").unwrap().value.clone();

    // Get the specified Interstellar mode. [TODO: Link to docs]
    let mode: String = arguments.get("mode").unwrap().value.clone();

    // Specified number of threads. [TODO: Link to docs]
    let mut max_threads: u64 = arguments.get("max_threads").unwrap().value.parse().unwrap_or(3);
    
    // Specified chunk size (how much data should be uploaded each request/how big should a chunk be.) [TODO: Link to docs]
    let mut chunk_size_mb: u64 = arguments.get("chunk_size").unwrap().value.parse().unwrap_or(40);

    // Specified asset_id for upload.
    let mut asset_id: String = String::new();
    if (arguments.get("asset_id").is_none() == false) {
        asset_id = arguments.get("asset_id").unwrap().value.clone();
    } else {
        let metadata = fs::metadata(file_path.clone()).expect("Failed to get file metadata.");
        let file_size = metadata.len();

        let management_host: String = arguments.get("management_host").expect("Missing 'management_host' argument.").value.clone();
        
        asset_id = authentication::get_asset_id(management_host, file_size).await.expect("Failed to get_asset_id.").asset_id;
    }

    let active_connections = Arc::new(AtomicU64::new(0));
    let mut connection_offset: u64 = 0;
    let chunk_size = chunk_size_mb * 1024 * 1024;

    let mut offset: u64 = 0;
    loop {
        let active_connections_clone = Arc::clone(&active_connections);
        if active_connections_clone.load(Ordering::SeqCst) < max_threads {
            let output = file::chunk::read(&file_path, offset, chunk_size).await;
            if (output.is_none() == true) {
                // println!("EOF");
                break;
            }

            let connection_id = connection_offset.clone();
            connection_offset = connection_offset+1;

            println!("Starting connection ({})... (active connections: {:?}/{})", connection_id, active_connections_clone.load(Ordering::SeqCst), max_threads);
            
            let host_clone = host.clone();
            let asset_id_clone = asset_id.clone();
            let mode_clone = mode.clone();
            let file_type_clone = file_type.clone();

            task::spawn(async move {
                active_connections_clone.fetch_add(1, Ordering::SeqCst);

                chunk::upload(host_clone.clone(), asset_id_clone.clone(), mode_clone.clone(), file_type_clone.to_string(), 0, output.unwrap()).await;
    
                active_connections_clone.fetch_sub(1, Ordering::SeqCst);
    
                // dbg!("Closed connection ({}) - {}.", connection_id, active_connections_clone.load(Ordering::SeqCst));
            });

            offset += chunk_size;
        }
    }

    let active_connections_clone = Arc::clone(&active_connections);
    println!("Waiting for uploads to finish...");
    // Wait for uploads to finish. The earlier thread will exit when there is no need to watch for new connections, however, we're still in the process of uploading. If we exit the application, those uploads will be aborted. We hold out this thread and wait for active connections to be 0.
    while (active_connections_clone.load(Ordering::SeqCst) > 0) {
    }
    println!("Finished.");
}