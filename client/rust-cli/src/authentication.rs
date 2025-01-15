use crate::structs::Get_asset_id;

use std::env;
use std::collections::HashMap;

use serde::{Serialize, Deserialize};
use serde_json::json;

use reqwest::header::{HeaderMap, HeaderValue, AUTHORIZATION};
use url::Url;

pub async fn get_asset_id(url: String, size: u64) -> Option<Get_asset_id> {
    let object = json!({
        "size": size
    });

    let mut headers = HeaderMap::new();
    headers.insert(AUTHORIZATION, HeaderValue::from_static("EXAMPLE_BEARER_TOKEN"));

    let mut url_href = Url::parse(&url).unwrap();
    url_href.set_path(&format!("{}get-asset-id", url_href.path()));

    // Send POST request
    let client = reqwest::Client::new();
    let response = client
        .post(url_href)
        .headers(headers)
        .json(&object)
        .send()
        .await.expect("Failed to send request.");

    // Handle the response
    if response.status().is_success() {
        let response_json: serde_json::Value = response.json().await.expect("Failed to parse response.");
        let asset_id: Get_asset_id = serde_json::from_value(response_json).expect("Failed to parse asset_id.");
        println!("Response JSON: {:?}", asset_id);
        return Some(asset_id);
    } else {
        eprintln!("Request failed with status: {}", response.status());
        return None;
    }
}