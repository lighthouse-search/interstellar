use std::env;
use serde::{Serialize, Deserialize};

#[derive(Debug, Clone, Deserialize, Serialize)]
pub struct Command_argument {
    pub value: String
}

fn main() {
    let args: Vec<String> = env::args().collect();

    let mut arguments: Dictionary<String, Command_argument> = Vec::new();
    let mut args_iter = args.iter();
    args_iter.next();
    while let Some(arg) = args_iter.next() {
        if arg.starts_with("--") {
            if let Some(value) = args_iter.next() {
                arguments.push(arg.replace("--", ""), Command_argument {
                    value: value.clone()
                });
            } else {
                eprintln!("Error: -- requires a value.");
                return;
            }
        } else {
            println!("arg COOL: {}", arg);
        }
    }

    println!("arguments: {:?}", arguments);
}
