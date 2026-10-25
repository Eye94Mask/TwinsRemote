# TwinsRemote
<img width="1617" height="506" alt="スクリーンショット 2026-10-21 223731" src="https://github.com/user-attachments/assets/76685a1f-5e6b-440a-b4fb-fce190e9d741" />

日本語版ReadMeは[こちら](https://github.com/MeuShirokuma/TwinsRemote/blob/master/README.md)

TwinsRemote is a low-latency P2P remote play system for gamers.

In TwinsRemote, users are divided into

<b> “Hosts” — who stream contents —</b> and <b> “Clients” — who receive the contents —</b>.

Please refer to [here](#System-Requirements) for the system requirements for Host and Client.

## How to Use TwinsRemote
* [Host](#getting-started-as-a-host)
* [Client](#getting-started-as-a-client)

---

# For Those Who Want to Support Me
TwinsRemote incurs ongoing maintenance costs.

(as of November 2026)

Server fees = (TwinsRemote + TURN + staging) + domain fees = 2,300 yen/month (14.38 USD/month).

If you like TwinsRemote and want it to continue for a long time, or if you’d like ms to keep working hard to develop new features, please consider supporting me.

* [OFUSE](https://ofuse.me/eye94mask) 

(Although the articles are written in Japanese, I also post articles about behind-the-scenes development stories, so please check them out if you're interested.)

# Table of Contents

# Table of Contents
- [TwinsRemote](#twinsremote)
- [How to Use TwinsRemote](#how-to-use-twinsremote)
- [For Those Who Want to Support Me](#for-those-who-want-to-support-me)
- [Features](#features)
  - [Low-Latency Streaming](#low-latency-streaming)
  - [Client users need only a supported browser](#client-users-need-only-a-supported-browser)
- [System Requirements](#system-requirements)
  - [Host](#host)
  - [Client](#client)
    - [Controller Support](#controller-support)
- [Getting Started with TwinsRemote](#getting-started-with-twinsremote)
  - [Getting Started as a Host](#getting-started-as-a-host)
    - [Install the App](#install-the-app)
    - [Connect to a Client](#connect-to-a-client)
  - [Getting Started as a Client](#getting-started-as-a-client)
- [Report Issues](#report-issues)
  - [For Developers](#for-developers)
  - [Feature Requests](#feature-requests)
  - [Bug Reports](#bug-reports)
  - [Other Reports or Questions](#other-reports-or-questions)
- [FAQ](#faq)
  - [Is there a dependency on Steam?](#is-there-a-dependency-on-steam)
  - [Do I need to install anything on the client side?](#do-i-need-to-install-anything-on-the-client-side)
  - [Why is ViGEm necessary?](#why-is-vigem-necessary)
  - [Controller input isn’t working on the client side](#controller-input-isnt-working-on-the-client-side)
  - [Video is choppy or lagging](#the-video-is-choppy-or-lagging)
- [Project Background](#project-background)
- [Development Status](#development-status)
- [Disclaimer](#disclaimer)

---

# Features

## Low Latency Streaming

* P2P communication using WebRTC.
* Low latency of a few milliseconds to tens of milliseconds in a stable network environment.
  * We offer the following features for users with limited internet connections.
    * Automatic streaming settings adjustment (for beginners).
    * Ability to finely adjust streaming settings manually (resolution, fps, data transmission bitrate, etc.) (for advanced users).
* Designed with gameplay in mind.

## Client users need only a supported browser

* Client users can join as long as they meet the requirements for [supported browsers](#client).
  * No device restrictions—works on PCs, smartphones, tablets, and more.
* No need to install apps or tools, or register for an account.
  * All you need is a controller to play the game.

---

# System Requirements

## Host

* Windows OS (No need to be Windows 11).
* NVIDIA GPU compatible with NVENC.
  * For NVIDIA's official compatibility chart, please refer to “NVENC - Encoding” [here](https://developer.nvidia.com/video-encode-decode-support-matrix).
    * If <b>“H.264 (AVC) YUV 4:2:0”</b>, <b>“H.264 (AVC) YUV 4:4:4”</b> and <b>“H.264 (AVC) Lossless”</b> are set to “Yes”, then it is supported.
    * <b>Some GPUs may work even if they aren’t listed in the official compatibility list</b>, so I recommend that you check to see if TwinsRemote is working.

<b>Note: If you’re using a laptop, the built-in display may cause TwinsRemote to malfunction.</b>

<b>For more details, see [here](#the-video-is-choppy-or-lagging).</b>

## Client

It generally works on any Chromium-based browser.

There are no specific hardware restrictions.

No app or tool downloads are required.

It generally works on any device capable of running a supported browser, such as PCs, smartphones, and tablets.

| Browser | Supported |
| ---- | ---- |
| Chrome | ✓ |
| Microsoft Edge | ✓ |
| Brave | ✓ |
| Firefox | × |

### Controller Support

| Browser | Compatibility | Vibration |
| ---- | ---- | ---- |
| Xbox Controller | ✓ | ✓ |
| DualShock 4 | ✓ | ✓ |
| DualSense | ✓ | × |

Latency is extremely low, and the system is designed with actual gameplay in mind.

---

# Getting Started with TwinsRemote

## Getting Started as a Host

### Introduction

I recommend installing the latest drivers from NVIDIA App.

TwinsRemote may not work if your drivers are out of date.

### Install the App

Please follow the instructions in the [video](https://youtu.be/T8Qm4MANf10) to complete the installation.

<div>
  <iframe src="https://youtu.be/T8Qm4MANf10"></iframe>
</div>

### Connect to a Client

Please follow the instructions in the [video](https://youtu.be/FHvVxVqWshA) to connect to the client.


<div>
  <iframe src="https://youtu.be/FHvVxVqWshA"></iframe>
</div>

## Getting Started as a Client

Please follow the instructions in the [video](https://youtu.be/3Vvxg5Bhuf0) to connect to the host.

<div>
  <iframe src="https://youtu.be/3Vvxg5Bhuf0"></iframe>
</div>

---

# Report Issues
## For Developers
I also welcome GitHub issues

## Feature Requests
Please reply to [This Post](https://x.com/Eye94MaskTech/status/2061413873991946547) on X

## Bug Reports
Please reply to [This Post](https://x.com/Eye94MaskTech/status/2061414643021824433) on X

## Other Reports or Questions
Please reply to [This Post](https://x.com/Eye94MaskTech/status/2062145070296662024) on X

# FAQ

## Is there a dependency on Steam?

No.

TwinsRemote does not rely on Steam.

It can also be used for the following purposes

* Games other than Steam
* Emulators
* Desktop apps

---

## Do I need to install anything on the client side?

No.

The client only needs a supported browser.

---

## Why is ViGEm necessary?

This is because [ViGEm](https://vigembusdriver.com/download/#:~:text=11%20November%202023-,Download,-Changelog) is used to generate a virtual controller on the host side and pass client input to the game.

### Controller input isn’t working on the client side

If the client-side log shows “gamepad connected: ~”, the controller is recognized.

If input still doesn’t work, please check the following
* Is the client PC’s focus on the TwinsRemote Client tab?
  * Did you just use a different window or application on the client PC?
* Is the host PC’s focus on the running game?
  * Did you just use a different window or application on the host PC?
  * I (めくしめん) have repeatedly made the mistake of not clicking the game window after sharing “Audio Sharing - Share App -”.
* Please check which controllers are supported by the game.
  * Supported controllers may vary by game—for example, some games may only support Xbox controllers.
* Please disconnect and reconnect the host and client.
  * If the connection is still not recognized, please report it to me.

---

### The video is choppy or lagging

* Is the streaming mode set to a high setting?
  * For example, streaming in <b>“4K”</b> while using a <b>shared apartment internet connection</b> may mean your internet connection can’t keep up with the streaming mode settings.
    * Try switching to a lower setting or switching to "Auto Adjust" mode.
* Are you sharing the video from your laptop’s built-in display?
  * Due to hardware limitations, it’s difficult for TwinsRemote to perform optimally on a laptop’s built-in display.
  * Connecting to an external display and sharing the video from there will resolve this issue.

Please also take a look at this [issue](https://github.com/Eye94Mask/TwinsRemote/issues/31#issuecomment-4591706536).

---

# Project Background

This project began as a system the developer created to play games remotely with his twin brother.

The name “TwinsRemote” embodies the following meanings.

* That the project began so the developer could play with his twin brother.
* The desire to make distant PCs feel as close as twins.

---

# Development Status

Development is currently ongoing.

Specifications and features are subject to significant changes in the future.

---

# Disclaimer

This project is provided as-is without warranty.

Please use it at your own risk.