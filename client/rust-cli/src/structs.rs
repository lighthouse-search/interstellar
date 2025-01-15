use serde::{Serialize, Deserialize};
use std::collections::HashMap;

#[derive(Debug, Clone, Deserialize, Serialize)]
pub struct Command_argument {
    pub value: String
}

#[derive(Clone, Debug, Deserialize)]
pub struct Get_asset_id {
    pub asset_id: String
}