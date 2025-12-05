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
- Fake tuners for development (no hardware needed)  
- Dependency-injected tuners (DVB, IPTV, SAT>IP, virtual)

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

### 🔥 Streaming
- Live MPEG-TS over HTTP  
- Chunked transfer  
- Low latency  
- Multiple clients  
- Planned: WebSocket stats + HLS output

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

