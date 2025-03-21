// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use sysinfo::System;
use warp::ws::{Message, WebSocket};
use warp::Filter;
use futures::StreamExt;
use futures::SinkExt;
use serde::Serialize;
use nvml_wrapper::Nvml;

#[derive(Serialize)]
struct SystemStats {
    cpu_usage: f32,
    ram_usage: f32,
    gpu_usage: f32,
    gpu_name: String,
    os_name: String,
    cpu_name: String,
    ram_amount: String,
}


async fn handle_socket(ws: WebSocket) {
    let (mut ws_tx, _ws_rx) = ws.split();
    
    let mut sys = System::new_all();
    sys.refresh_all();
    
    // Get OS name
    let os_name = System::name().unwrap_or_else(|| String::from("Unknown OS"));
    
    // Get CPU name
    let cpu_name = sys.cpus().first()
        .map(|cpu| cpu.brand().to_string())
        .unwrap_or_else(|| String::from("Unknown CPU"));
    
    // Get total RAM
    let total_ram_gb = sys.total_memory() as f64 / (1024.0 * 1024.0 * 1024.0);
    let ram_amount = format!("{:.1} GB", total_ram_gb);
    
    // Initialize NVIDIA GPU 
    let nvml = Nvml::init().ok();
    let gpu_device = nvml.as_ref().and_then(|nvml| nvml.device_by_index(0).ok());
    let gpu_name = match &gpu_device {
        Some(device) => device.name().unwrap_or_default(),
        None => String::from("GPU not found"),
    };
    
    loop {
        sys.refresh_all();
        
        // Collect CPU usage (average usage of all cores)
        let cpu_usage: f32 = (sys.cpus().iter().map(|cpu| cpu.cpu_usage()).sum::<f32>() / sys.cpus().len() as f32).round();
        
        // Collect RAM usage
        let ram_usage: f32 = (sys.used_memory() as f32 / sys.total_memory() as f32 * 100.0).round();
        
        // Get GPU usage using the existing device handle
        let gpu_usage = gpu_device.as_ref()
            .and_then(|device| device.utilization_rates().ok())
            .map(|utilization| utilization.gpu as f32)
            .unwrap_or(0.0);

        let stats = SystemStats {
            cpu_usage,
            ram_usage,
            gpu_usage,
            gpu_name: gpu_name.clone(),
            os_name: os_name.clone(),
            cpu_name: cpu_name.clone(),
            ram_amount: ram_amount.clone(),
        };

        // Send stats via WebSocket
        let message = serde_json::to_string(&stats).unwrap();
        if ws_tx.send(Message::text(message)).await.is_err() {
            break;
        }

        tokio::time::sleep(tokio::time::Duration::from_secs(1)).await;
    }
}

#[tokio::main]
async fn main() {
    let ws_route = warp::path("ws")
        .and(warp::ws())
        .map(|ws: warp::ws::Ws| {
            ws.on_upgrade(handle_socket)
        });
    
    // Spawn the WebSocket server in a separate task
    tokio::spawn(warp::serve(ws_route).run(([127, 0, 0, 1], 3069)));
    
    // Run the Tauri application
    nekoframe_lib::run();
}
