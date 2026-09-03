<div align="center">

# Nekoframe

[![Downloads](https://img.shields.io/github/downloads/nubsuki/Nekoframe/total?style=for-the-badge&logo=github&color=347d39)](https://github.com/nubsuki/Nekoframe/releases)
[![Latest Release](https://img.shields.io/github/v/release/nubsuki/Nekoframe?style=for-the-badge&logo=github&color=238636)](https://github.com/nubsuki/Nekoframe/releases/latest)
[![Steam Workshop](https://img.shields.io/badge/Steam%20Workshop-Wallux-1b2838?style=for-the-badge&logo=steam&logoColor=white)](https://steamcommunity.com/sharedfiles/filedetails/?id=3791578910)

<br/>

![Nekoframe Preview](new_preview.png)

  <a href="https://youtu.be/lnRToLpockw">
    <img src="https://img.youtube.com/vi/lnRToLpockw/maxresdefault.jpg" alt="Wallux Video Showcase" width="100%" />
  </a>
  <p>
    <a href="https://youtu.be/lnRToLpockw">▶️ <b>Watch Video Showcase</b></a> &nbsp;|&nbsp; 
    <a href="https://steamcommunity.com/sharedfiles/filedetails/?id=3791578910">🎮 <b>Wallux on Steam Workshop</b></a> &nbsp;|&nbsp;
    <a href="https://buymeacoffee.com/nubsuki">☕ <b>Buy Me a Coffee</b></a>
  </p>
</div>

---

**Nekoframe** is a lightweight, background Windows service that collects real-time hardware telemetry (CPU, GPU, RAM, Disks, Network, and Fans) and broadcasts it locally over a WebSocket connection.

It's designed to act as a silent, ultra-efficient data provider for external dashboards, hardware monitoring displays, stream overlays, or local web apps such as the [Wallux Wallpaper Engine HUD](https://steamcommunity.com/sharedfiles/filedetails/?id=3791578910).

---

## Features

- **Zero UI Overhead:** Runs entirely in the Windows System Tray with minimal CPU and RAM usage.
- **Deep Sensor Support:** Powered by [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor), reading core temps, hotspot/junction temps, clock speeds, power draw, VRAM usage, and fan RPMs.
- **Admin-Ready & UAC Bypass:** Installs with a Windows Scheduled Task that automatically starts with highest privileges on logon without prompt interruptions.
- **Multi-GPU & NIC Selector:** Easily pick which GPU or network adapter to track right from the system tray menu.
- **Auto-Update Checker:** Alerts you in the system tray whenever a new release is available on GitHub.

---

## Installation & Quick Start

1. **Download:** Grab the latest `Nekoframe_Setup_v*.exe` from the [Releases](https://github.com/nubsuki/Nekoframe/releases) page.
2. **Run Installer:** Follow the setup wizard. It will configure the automatic startup task and launch Nekoframe in your system tray.
3. **Verify:** Check your system tray (near your Windows clock) for the **Nekoframe** icon.
4. **Connect:** Any WebSocket client can connect to `ws://localhost:3069` to start receiving live hardware telemetry.

---

## System Tray Controls

Right-click the Nekoframe icon in the system tray to access quick controls:

- **Run on Windows Startup:** Toggle automatic launch at logon.
- **Select Primary GPU:** Switch telemetry tracking between multiple GPUs (e.g. dedicated NVIDIA/AMD vs integrated graphics).
- **Select Network Adapter:** Pin the active Ethernet or Wi-Fi interface for bandwidth monitoring.
- **Hide System Processes:** Toggle whether background system tasks appear in the top-process monitor.
- **Check for Updates:** Instantly check GitHub for new versions.
- **View Logs:** Open diagnostic logs for troubleshooting.

---

## WebSocket API

Once running, Nekoframe broadcasts a complete system snapshot every **1000ms** to any connected WebSocket client.

- **Default Address:** `ws://localhost:3069`
- **Data Format:** JSON

---

## Testing & Development

You can preview the live stream immediately without any frontend setup:

- Open [`test_websocket.html`](test_websocket.html) directly in any modern browser (Chrome, Edge, Firefox) while Nekoframe is running to view raw telemetry in real time.

---

## Configuration

You can customize the WebSocket port:

1. Navigate to your Nekoframe directory (default: `C:\Program Files\Nekoframe`).
2. Open `config.json`.
3. Change `"WebSocketPort"` from `3069` to your preferred port:
   ```json
   {
     "WebSocketPort": 3069
   }
   ```
4. Restart Nekoframe from the system tray.

---

## Support & Donations

If you enjoy **Nekoframe** or the **Wallux Wallpaper Engine HUD** and want to support ongoing development, consider buying me a coffee:

<div align="center">
  <a href="https://buymeacoffee.com/nubsuki">
    <img src="https://img.shields.io/badge/Buy%20Me%20A%20Coffee-nubsuki-FFDD00?style=for-the-badge&logo=buy-me-a-coffee&logoColor=black" alt="Buy Me A Coffee" height="40" />
  </a>
</div>

---

## License

This project is for personal use and is distributed "as-is". Use at your own risk.
