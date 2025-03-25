// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use futures::SinkExt;
use futures::StreamExt;
use nvml_wrapper::enum_wrappers::device::TemperatureSensor;
use nvml_wrapper::Nvml;
use serde::Serialize;
use sysinfo::{Disks, Networks, System};
use warp::ws::{Message, WebSocket};
use warp::Filter;

#[derive(Serialize)]
struct SystemHealth {
    status: String,
    warnings: Vec<String>,
}

#[derive(Serialize)]
struct SystemStats {
    cpu_usage: f32,
    ram_usage: f32,
    gpu_usage: f32,
    gpu_temp: f32,
    gpu_name: String,
    os_name: String,
    cpu_name: String,
    ram_amount: String,
    network_down: f32,
    network_up: f32,
    disks: Vec<String>,
    process_count: usize,
    top_processes: Vec<ProcessInfo>,
    host_name: String,
    health: SystemHealth,
}

#[derive(Serialize)]
struct ProcessInfo {
    name: String,
    pid: u32,
    cpu_usage: f32,
    memory_usage: u64,
}

async fn handle_socket(ws: WebSocket) {
    let (mut ws_tx, _ws_rx) = ws.split();

    let mut sys = System::new_all();
    let mut networks = Networks::new_with_refreshed_list();
    let disks = Disks::new_with_refreshed_list();

    // Refresh system information
    sys.refresh_all();

    // Get OS name
    let os_name = format!(
        "{} {}",
        System::name().unwrap_or_else(|| String::from("Unknown OS")),
        System::os_version().unwrap_or_else(|| String::from("Unknown Version"))
    );

    let host_name = System::host_name().unwrap_or_else(|| String::from("Unknown Host"));

    // Get CPU name
    let cpu_name = sys
        .cpus()
        .first()
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

    // Get all disks info
    let all_disks: Vec<String> = disks
        .iter()
        .map(|disk| {
            let total_gb = disk.total_space() as f64 / (1024.0 * 1024.0 * 1024.0);
            let used_gb =
                (disk.total_space() - disk.available_space()) as f64 / (1024.0 * 1024.0 * 1024.0);
            let free_gb = disk.available_space() as f64 / (1024.0 * 1024.0 * 1024.0);
            let name = disk.mount_point().to_string_lossy().to_string();
            format!(
                "{}:: {:.1} GB / {:.1} GB ({:.1} GB free)",
                name, used_gb, total_gb, free_gb
            )
        })
        .collect();

    loop {
        sys.refresh_all();
        networks.refresh(false);

        let mut background_processes = 0;
        let mut apps = 0;

        sys.processes().iter().for_each(|(_, process)| {
            let name = process.name().to_string_lossy().to_lowercase();
            
            // Count as an app if it has a window or high memory usage
            if process.memory() > 1024 * 1024 * 20 {
                apps += 1;
            } else if process.cpu_usage() > 0.01 && 
                !name.contains("system") &&
                !name.contains("svc") &&
                !name.contains("service") &&
                !name.contains("runtime") &&
                !name.starts_with("ms") &&
                !name.starts_with("win") &&
                !name.contains("registry") &&
                !name.contains("fontdrvhost") &&
                !name.contains("csrss") &&
                !name.contains("smss") &&
                !name.contains("wininit") &&
                !name.contains("lsass") {
                background_processes += 1;
            }
        });

        let process_count = background_processes + apps;

        // Collect top processes for display
        let mut process_map: std::collections::HashMap<String, ProcessInfo> = std::collections::HashMap::new();
        
        sys.processes()
            .iter()
            .filter(|(_, process)| {
                process.cpu_usage() > 0.01 && 
                !process.name().to_string_lossy().to_lowercase().contains("system")
            })
            .for_each(|(_, process)| {
                let name = process.name().to_string_lossy().to_string();
                let cpu_usage = process.cpu_usage() / sys.cpus().len() as f32;
                
                process_map
                    .entry(name.clone())
                    .and_modify(|e| {
                        e.cpu_usage += cpu_usage;
                        e.memory_usage += process.memory();
                    })
                    .or_insert(ProcessInfo {
                        name,
                        pid: process.pid().as_u32(),
                        cpu_usage,
                        memory_usage: process.memory(),
                    });
            });

        let mut processes: Vec<ProcessInfo> = process_map.into_values().collect();

        // Sort by CPU usage
        processes.sort_by(|a, b| b.cpu_usage.partial_cmp(&a.cpu_usage).unwrap_or(std::cmp::Ordering::Equal));
        // Take top 10
        processes.truncate(10);

        // Collect CPU usage (average usage of all cores)
        let cpu_usage: f32 = (sys.cpus().iter().map(|cpu| cpu.cpu_usage()).sum::<f32>()
            / sys.cpus().len() as f32)
            .round();

        // Collect RAM usage
        let ram_usage: f32 = (sys.used_memory() as f32 / sys.total_memory() as f32 * 100.0).round();

        // Get GPU usage using the existing device handle
        let (gpu_usage, gpu_temp) = gpu_device
            .as_ref()
            .map(|device| {
                let usage = device
                    .utilization_rates()
                    .map(|utilization| utilization.gpu as f32)
                    .unwrap_or(0.0);
                let temp = device
                    .temperature(TemperatureSensor::Gpu)
                    .map(|temp| temp as f32)
                    .unwrap_or(0.0);
                (usage, temp)
            })
            .unwrap_or((0.0, 0.0));

        // Get network usage
        let (total_rx, total_tx) = networks.iter().fold((0, 0), |(rx, tx), (_name, data)| {
            (rx + data.received(), tx + data.transmitted())
        });

        let network_down = (total_rx as f32 / (1024.0 * 1024.0) * 100.0).round() / 100.0;
        let network_up = (total_tx as f32 / (1024.0 * 1024.0) * 100.0).round() / 100.0;


        let mut warnings = Vec::new();
        let mut status = "Healthy".to_string();

        // CPU usage check
        if cpu_usage > 95.0 {
            warnings.push(format!("High CPU usage: {}%", cpu_usage));
            status = "Warning".to_string();
        }

        // GPU checks
        if gpu_usage > 95.0 {
            warnings.push(format!("High GPU usage: {}%", gpu_usage));
            status = "Warning".to_string();
        }
        if gpu_temp > 85.0 {
            warnings.push(format!("High GPU temperature: {}°C", gpu_temp));
            status = "Warning".to_string();
        }

        // RAM usage check
        if ram_usage > 95.0 {
            warnings.push(format!("High RAM usage: {}%", ram_usage));
            status = "Warning".to_string();
        }

        let health = SystemHealth {
            status,
            warnings,
        };

        let stats = SystemStats {
            cpu_usage,
            ram_usage,
            gpu_usage,
            gpu_temp,
            gpu_name: gpu_name.clone(),
            os_name: os_name.clone(),
            cpu_name: cpu_name.clone(),
            ram_amount: ram_amount.clone(),
            network_down,
            network_up,
            disks: all_disks.clone(),
            process_count,
            top_processes: processes,
            host_name: host_name.clone(),
            health,
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
        .map(|ws: warp::ws::Ws| ws.on_upgrade(handle_socket));

    // Spawn the WebSocket server in a separate task
    tokio::spawn(warp::serve(ws_route).run(([127, 0, 0, 1], 3069)));

    // Run the Tauri application
    nekoframe_lib::run();
}
