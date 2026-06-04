# Twins Remote

日本語版ReadMeは[こちら](https://github.com/MeuShirokuma/TwinsRemote/blob/master/README.md)

Low-latency peer-to-peer remote play system for Windows.

This is a version of a system I originally created to play games with my twin brother, customized for public use.

There are two reasons behind the name:

* It started as a system for me to play with my twin brother.
* I wanted the two devices to operate with low latency, as if they were a single device.

---

# For Those Who Want to Support Us
TwinsRemote incurs ongoing maintenance costs.

Server fees (production + TURN + staging) + domain fees = 2,300 yen/month (14.38 USD/month)

If you like TwinsRemote and want it to continue for a long time, or if you’d like us to keep working hard to develop new features, please consider supporting us.

*[OFUSE](https://ofuse.me/eye94mask)

OFUSE

# Table of Contents
- [Features](#features)
  - [Low Latency Streaming](#low-latency-streaming)
  - [Browser-Based Client](#browser-based-client)
- [Supported Features](#supported-features)
  - [Video Streaming](#video-streaming)
  - [Audio Sharing](#audio-sharing)
  - [Controller Input](#controller-input)
- [Architecture](#architecture)
  - [Host Side](#host-side)
    - [Rust](#rust)
    - [C++](#c)
    - [C#](#c-1)
  - [Client Side](#client-side)
    - [Vanilla JavaScript](#vanilla-javascript)
- [How To Use](#how-to-use)
  - [Host](#host)
  - [Client](#client)
- [Troubleshooting](#troubleshooting)
- [Requirements](#requirements)
  - [Host PC](#host-pc)
  - [Client](#client-1)
- [Current Limitation](#current-limitations)
  - [Not Yet Implemented](#not-yet-implemented)
  - [Current Restrictions](#current-restrictions)
- [Networking](#networking)
- [Development](#development)
  - [Technologies Used](#technologies-used)
    - [Host](#host-1)
    - [Client](#client-2)
- [Building](#building)
- [Future Plans](#future-plans)
- [Report]
  - [For Developers](#for-developers)
  - [Feature Request](#feature-request)
  - [Bug Report](#bug-report)
  - [Other Questions or Reports](#other-questions-or-reports)
- [FAQ](#faq)
  - [Does this only work with Steam games?](#does-this-only-work-with-steam-games)
  - [Does the client need to install anything?](#does-the-client-need-to-install-anything)
  - [Why is ViGEm required?](#why-is-vigem-required)
- [Project Background](#project-background)
- [Status](#status)
- [Disclaimer](#disclaimer)

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

> [ViGEm](https://vigembusdriver.com/download/) installation is required on the host PC to allow remote controller input.

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

## Troubleshooting
If you try to run the host on a gaming laptop, it may not work properly.

You may need to review your PC settings.
Click [here](https://github.com/Eye94Mask/TwinsRemote/issues/31#issuecomment-4591706536) for more details

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
The following are examples.

It is not yet determined whether these will be implemented in the future.

* Dynamic resolution switching during streaming
* Multi-monitor selection
* CPU encoding

## Current Restrictions

* Windows host only
* Main monitor capture only
* NVIDIA NVENC required

---

# Networking

Twins Remote supports:

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
* [ViGEm](https://vigembusdriver.com/download/)

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
Please feel free to submit any feature requests or bug reports.

Please submit your reports via GitHub Issues or in the comments section of the X post below.

We plan to actively incorporate any suggestions that do not stray too far from the core philosophy of Twins Remote.

Here is some examples:
* Keyboard and mouse input support
* Host-side input permission settings
* Dynamic resolution switching
* Additional streaming optimizations
* More flexible display selection

---

# Report
## For Developers
I also welcome GitHub issues

## Feature Request
Please reply to [This Post](https://x.com/Eye94MaskTech/status/2061413873991946547) on X

## Bug Report
Please reply to [This Post](https://x.com/Eye94MaskTech/status/2061414643021824433) on X

## Other Questions or Reports
Please reply to [This Post](https://x.com/Eye94MaskTech/status/2062145070296662024) on X

# FAQ

## Does this only work with Steam games?

No.

Twins Remote is not tied to Steam and can be used with:

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

[ViGEm](https://vigembusdriver.com/download/) is used to emulate virtual game controllers on the host PC so that remote controller input can be recognized by games.

---

# Project Background

This project originally started as a system built for the developer and their twin brother to play games remotely together.

The name "Twins Remote" comes from:

* The original use case between twins
* The goal of making two distant PCs feel as synchronized as possible

---

# Status

This project is currently under active development.

Features and behavior may change significantly over time.

---

# Disclaimer

This project is provided as-is without warranty.

Use at your own risk.
