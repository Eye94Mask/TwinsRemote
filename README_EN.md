# Twins Remote Play

Low-latency peer-to-peer remote play system for Windows.

Twins Remote Play allows a host PC to stream video/audio to a remote client through the browser with very low latency.
The client can also send controller input back to the host, allowing remote gameplay similar to Steam Remote Play Together — but without being limited to Steam games.

---

# Features

## Low Latency Streaming

* Peer-to-peer communication using WebRTC
* Very low latency under stable network conditions
* Designed for interactive gameplay, not just video playback

## Browser-Based Client

* No client installation required
* Open the client page in a Chromium-based browser and connect
* Confirmed working on:

  * Google Chrome
  * Brave

Client URL:

```text
https://play.twins-remote.com
```

---

# Supported Features

## Video Streaming

* Hardware encoding using NVIDIA NVENC
* Multiple predefined streaming modes
* User-created custom modes
* Tunable for unstable network environments
* IPv4 / IPv6 support
* TURN relay support for NAT traversal

## Audio Sharing

Three audio sharing modes are available:

1. Full PC system audio
2. Application-specific audio sharing
3. Audio disabled

## Controller Input

Supported and tested controllers:

* Xbox Controllers
* DualShock 4
* DualSense

Controller input is transmitted through WebRTC DataChannels with very low latency.

---

# Architecture

## Host Side

### Rust

Responsible for:

* WebRTC communication
* DataChannel processing
* Controller input handling
* TURN/STUN support
* NAT traversal

Uses:

* WebRTC
* ViGEm for virtual controller emulation

> ViGEm installation is required on the host PC to allow remote controller input.

### C++

Used for:

* Video capture and NVENC encoding
* Audio capture

### C#

Windows Forms application used for:

* Host UI
* Streaming configuration
* Session connection management

The host application supports:

* Japanese
* English

---

## Client Side

### Vanilla JavaScript

* No frontend framework
* Browser-based WebRTC client
* Session ID generation and connection handling

---

# How To Use

## Host

1. Launch `TwinsRemoteHost.exe`
2. Select a streaming mode
3. Enter the session ID shared by the client
4. Press the connect button

Streaming will start automatically after the connection is established.

---

## Client

1. Open:

```text
https://play.twins-remote.com
```

2. A session ID will be generated automatically
3. Send the session ID to the host
4. Wait for the host to connect

After the host finishes processing, the stream will begin automatically.

---

# Requirements

## Host PC

* Windows
* NVIDIA GPU with NVENC support
* Chromium-based browser is NOT required on the host

## Client

* Chromium-based browser

  * Chrome
  * Brave

---

# Current Limitations

## Not Yet Implemented

* Editing/deleting custom streaming modes
* Controller vibration feedback
* Dynamic resolution switching during streaming
* Multi-monitor selection
* CPU encoding

## Current Restrictions

* Windows host only
* Main monitor capture only
* NVIDIA NVENC required

---

# Networking

Twins Remote Play supports:

* P2P WebRTC communication
* IPv4
* IPv6
* TURN relay fallback
* NAT traversal

Currently, a TURN server is deployed in Tokyo.

---

# Development

## Technologies Used

### Host

* Rust
* C++
* C#
* WebRTC
* NVENC
* ViGEm

### Client

* Vanilla JavaScript
* WebRTC

---

# Building

Visual Studio is required for development.

The repository contains multiple projects including:

* `NvEnc`
* `ProcessAudioCapture`
* `SystemMicCapture`
* `TwinsRemoteHost`

These projects are managed through a Visual Studio solution.

---

# Future Plans

Planned features include:

* Keyboard and mouse input support
* Host-side input permission settings
* Dynamic resolution switching
* Additional streaming optimizations
* More flexible display selection

---

# FAQ

## Does this only work with Steam games?

No.

Twins Remote Play is not tied to Steam and can be used with:

* Non-Steam games
* Emulators
* Desktop applications
* Video/movie sharing

---

## Does the client need to install anything?

No.

The client only needs a supported browser.

---

## Why is ViGEm required?

ViGEm is used to emulate virtual game controllers on the host PC so that remote controller input can be recognized by games.

---

# Project Background

This project originally started as a system built for the developer and their twin brother to play games remotely together.

The name "Twins Remote Play" comes from:

* The original use case between twins
* The goal of making two distant PCs feel as synchronized as possible

---

# Status

This project is currently under active development.

Features and behavior may change significantly over time.

---

# Japanese README

日本語版READMEは将来的に追加予定です。

---

# Disclaimer

This project is provided as-is without warranty.

Use at your own risk.
