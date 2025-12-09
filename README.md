# 📺 DVBSharp  
*A modern, high-performance DVB backend written in C#.*

<img src="docs/logo.png" width="150" />

DVBSharp is a next-generation digital TV backend designed as a clean, modular, and performant alternative to legacy systems like TVHeadend.  
It is built in **C# / .NET 8**, designed to integrate with **Next.js frontends**, and engineered to support **DVB-T/T2 tuners**, **IPTV sources**, **SAT>IP**, and **NVIDIA NVENC transcoding**.

---

## ✨ Features

### 🎛️ Tuner Management
- Multiple tuner support  
- Hot-plug architecture  
- Native Linux DVB discovery (/dev/dvb) with capability reporting  
- Dependency-injected tuners (DVB, IPTV, SAT>IP, virtual)
- Development-only Cambridge/Sandy Heath virtual tuner that emits realistic mux + channel data
- Persistent tuner pinning so DVB frontends stay locked on dedicated muxes

### 📡 DVB Infrastructure (Work in Progress)
- Tuning via Linux DVB API (`FE_SET_FRONTEND`)  
- `/dev/dvb/adapterX` device handling  
- Blind scanning  
- Lock monitoring (SNR, BER, strength)  
- Frequency plan presets (UK, EU, US…)

### 🎞️ MPEG-TS Processing
- Full TS reader (188-byte packets)  
- PAT / PMT / SDT parsing  
- PID demuxing  
- Continuity counter validation  
- Service + Channel model generation  
- Logical channel metadata (LCNs, call signs, categories) for clean lineups  

### 🔥 Streaming
- Live MPEG-TS over HTTP  
- Chunked transfer  
- Low latency  
- Multiple clients  
- Planned: WebSocket stats + HLS output

### 📡 HDHomeRun Emulation
- `/discover.json`, `/lineup_status.json`, `/lineup.json`, `/lineup.post` endpoints
- `/device.xml`, `/ConnectionManager.xml`, `/ContentDirectory.xml` for UPnP discovery parity (per antennas reference)
- Dynamically generated lineups from the mux/channel database
- Works with any registered tuner or the automatic `/api/stream/any` endpoint
- Client-friendly channel metadata (LCN, name, category)
- Optional dev transport override – set `Streaming:TestTransportPath` (defaults to `test.ts` at the repo root) to loop a canned MPEG-TS over every channel/stream for testing

### ⚡ NVIDIA NVENC Support
- GPU-accelerated transcoding via FFmpeg NVENC  
- H.264 / H.265 streaming  
- Live re-encoding pipeline  
- Lightweight Docker builds with GPU support  

### 🌐 Modern API
- Clean REST API (JSON)  
- Compatible with Next.js / React / Vue / etc.  
- Consistent model representations  
- Easy to integrate with home automation or monitoring

---

## 🧱 Architecture Overview

## 📻 HDHomeRun Quickstart
1. Ensure you have at least one tuner registered (`/api/tuners`).
2. Populate your mux/channel database via scans (`/api/muxes/scan`) so the lineup has services to expose.
3. Point HDHomeRun-compatible clients (Plex, Channels, Jellyfin, etc.) at the DVBSharp base URL. They will call:
   - `GET /discover.json` to detect the virtual device
   - `GET /lineup_status.json` to determine scan state
   - `GET /lineup.json` to fetch channels (each entry links to `/api/stream/{tunerId}?frequency=...` or `/api/stream/any`)

### Optional configuration
You can customise the emulated device identity via configuration:

```json
"HdHomeRun": {
  "FriendlyName": "DVBSharp Lab",
  "DeviceId": "D0D0CAFE",
  "DeviceAuth": "sharp-secret",
  "ModelNumber": "HDHR5-4DT",
  "FirmwareName": "dvbsharp_atsc",
  "FirmwareVersion": "2024.02.0",
  "Manufacturer": "DVBSharp",
  "SourceType": "Antenna"
}
```

Place the section in `appsettings.json` (or user secrets) inside `DVBSharp.Web`. Defaults are supplied if the section is omitted.

## 🧪 Development Emulation
- When running the backend with `ASPNETCORE_ENVIRONMENT=Development`, DVBSharp registers a **Cambridgeshire virtual tuner** that emulates the Sandy Heath transmitter.
- Tune it by hitting `/api/stream/{id}?frequency=474000000` (or 498e6, 522e6, 514e6, 530e6, 562e6) and it will automatically seed mux/channel data for realistic Freeview lineups.
- The fake tuner also produces MPEG-TS-like packets so client players can open the stream endpoints without needing physical hardware.
- Plex / Jellyfin setup tip: point the client at `http(s)://<host>:<port>` and it will fetch `/discover.json`, `/device.xml`, `/lineup_status.json`, and `/lineup.json` just like the [antennas HDHomeRun emulator](https://github.com/jfarseneau/antennas/). Use `/api/integrations/hdhomerun` (UI panel) to copy-paste the exact URLs.

## 🔍 Discovery & Pinning
- Visit the new **Discovery** page in the Next.js UI to pin any registered tuner to a specific mux frequency.
- Pins are persisted (`data/tuner_assignments.json`) and replayed on startup, ensuring tuners never hop to the wrong mux during HDHomeRun requests.
- The `/api/stream` endpoints honor pins—attempting to tune a pinned tuner to a different mux returns a descriptive error.
