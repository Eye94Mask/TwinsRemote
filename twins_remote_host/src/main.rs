mod dxgi_capture;
mod webrtc_sender;
mod audio_encoder;
mod controller;
mod consts;
mod env;

use anyhow::{anyhow, Result};
use std::io::{Read, Write};
use std::process::{Child, ChildStdout, Command, Stdio};
use std::sync::{
    atomic::{AtomicBool, AtomicU32, AtomicU64, Ordering},
    Arc, Mutex,
};
use std::time::{SystemTime, UNIX_EPOCH};

use serde::Deserialize;
use serde_json::json;

use clap::Parser;
use bytes::Bytes;
use rustls::crypto::ring::default_provider;
use tokio::io::BufReader;
use tokio::sync::{broadcast, mpsc};
use tokio::time::{sleep, Duration};

use webrtc::data_channel::data_channel_message::DataChannelMessage;
use webrtc::data_channel::data_channel_state::RTCDataChannelState;
use webrtc::ice_transport::ice_connection_state::RTCIceConnectionState;
use webrtc::media::Sample;

use crate::audio_encoder::AudioEncoder;
use crate::consts::{VIDEO_FRAME_DURATION, CAP_THRESHOLD};
use crate::controller::{Controller, GamepadState, RumbleState, VirtualPadType};
use crate::env::IceConfig;
use crate::webrtc_sender::{WebRtcSender, StreamPolicy};

#[derive(Parser, Debug)]
#[command(author, version, about)]
struct Args {
    #[arg(long, default_value = "balanced")]
    mode: String,

    #[arg(long)]
    session: String
}

#[derive(Debug, Clone)]
enum NvencCommand {
    ForceIdr,
    ApplyStreamPolicy(StreamPolicy),
    RestorePreset
}

struct AudioHelper {
    child: Child,
    stdout: ChildStdout
}

enum AudioCommand {
    UsePid(u32),
    UseSystemMix,
    Stop
}

#[tokio::main]
async fn main() -> Result<()> {
    default_provider()
        .install_default()
        .expect("install rustls crypto provider");
    
    let args = Args::parse();
    
    let preset = args.mode;
    let session_id = args.session;

    let preset_for_nvenc_thread = preset.clone();

    println!("[STATE] HOST_STARTING");
    println!("[INFO] Starting Remote Play Host");
    println!("[INFO] mode={}", preset);
    println!("[INFO] session={}", session_id);

    let (audio_cmd_tx, audio_cmd_rx) = std::sync::mpsc::channel::<AudioCommand>();
    let (quit_tx, quit_rx) = std::sync::mpsc::channel::<()>();
    spawn_stdin_router(audio_cmd_tx.clone(), quit_tx);

    println!("[INFO] fetching IceConfig from server");
    let ice = IceConfig::fetch_from_server(&session_id).await?;
    println!("[INFO] IceConfig fetched");

    let webrtc = WebRtcSender::new(ice.clone(), &session_id).await?;
    let webrtc_clone = webrtc.clone();
    // -------------------------------
    // Controller / Rumble
    // -------------------------------
    let (rumble_rx_tx, rumble_rx_std) = std::sync::mpsc::channel::<RumbleState>();
    let controller_raw = Controller::new(
        VirtualPadType::Xbox360,
        rumble_rx_tx
    )?;
    let controller = Arc::new(Mutex::new(controller_raw));

    // broadcast で DataChannel 再接続時にも subscribe しなおす
    let (rumble_tx, _) = broadcast::channel::<RumbleState>(32);
    let rumble_tx_bridge = rumble_tx.clone();

    std::thread::spawn(move || {
        while let Ok(rumble) = rumble_rx_std.recv() {
            println!(
                "[HOST] rumble received from ViGEm: large={} small={} led={}",
                rumble.large, rumble.small, rumble.led
            );

            let _ = rumble_tx_bridge.send(rumble);
        }
    });

    println!("[INFO] launching NvEnc.exe preset={}", preset_for_nvenc_thread);
    let mut child = Command::new("NvEnc.exe")
        .arg(&preset_for_nvenc_thread)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::inherit())
        .spawn()
        .expect("failed to start nvenc");

    let mut nvenc_stdin = child.stdin.take().expect("failed to take nvenc stdin");
    let nvenc_stdout = child.stdout.take().expect("failed to take nvenc stdout");

    let last_nvenc_packet_at = Arc::new(AtomicU64::new(now_millis()));
    let last_video_sample_at = Arc::new(AtomicU64::new(now_millis()));
    let video_watchdog_fired = Arc::new(AtomicBool::new(false));

    // NvEnc command thread
    let (nvenc_cmd_tx, nvenc_cmd_rx) = std::sync::mpsc::channel::<NvencCommand>();
    std::thread::spawn(move || {
        while let Ok(cmd) = nvenc_cmd_rx.recv() {
            let line = match cmd {
                NvencCommand::ForceIdr => "force_idr\n".to_string(),

                NvencCommand::RestorePreset => format!("set_preset {}\n", preset_for_nvenc_thread),

                NvencCommand::ApplyStreamPolicy(policy) => {
                    match policy.cap {
                        Some(cap) => format!(
                            "set_config {} {} {} {} {} {} {} {} {} {} {} {} {} {} {} {} {} {} {}\n",
                            cap.width,
                            cap.height,
                            cap.fps,
                            cap.average_bitrate,
                            cap.max_bitrate,
                            cap.vbv_buffer_size,
                            cap.vbv_initial_delay,
                            cap.gop_length,
                            cap.idr_period,
                            cap.repeat_sps_pps as u32,
                            cap.output_aud as u32,
                            cap.max_ref_frames,
                            cap.profile_guid,
                            cap.preset_guid,
                            cap.tuning_info,
                            cap.enable_lookahead as u32,
                            cap.lookahead_depth,
                            cap.disable_iadapt as u32,
                            cap.disable_badapt as u32
                        ),
                        None => {
                            format!("set_preset {}\n", preset)
                        }
                    }
                }
            };

            if let Err(e) = nvenc_stdin.write_all(line.as_bytes()) {
                eprintln!("[HOST] nvenc stdin write failed: {:?}", e);
                break;
            }
            if let Err(e) = nvenc_stdin.flush() {
                eprintln!("[HOST] nvenc stdin flush failed: {:?}", e);
                break;
            }
        }
    });

    // -------------------------------
    // AUDIO THREAD
    // -------------------------------
    let audio_track = webrtc.audio_track.clone();

    let (tx_audio, mut rx_audio) = tokio::sync::mpsc::channel::<Sample>(3);
    let tx_audio_clone = tx_audio.clone();

    std::thread::spawn(move || {
        let mut helper: Option<AudioHelper> = None;
        let mut audio_encoder = AudioEncoder::new();

        loop {
            while let Ok(cmd) = audio_cmd_rx.try_recv() {
                if let Some(mut old) = helper.take() {
                    let _ = old.child.kill();
                    let _ = old.child.wait();
                }

                helper = match cmd {
                    AudioCommand::UsePid(pid) => match spawn_audio_helper(pid) {
                        Ok(h) => {
                            println!("[AUDIO] helper started for pid={}", pid);
                            println!("[STATE] AUDIO_PID");
                            Some(h)
                        }
                        Err(e) => {
                            eprintln!("[AUDIO] failed to start helper: {:?}", e);
                            println!("[STATE] AUDIO_ERROR");
                            None
                        }
                    },
                    AudioCommand::UseSystemMix => match spawn_system_mix_helper() {
                        Ok(h) => {
                            println!("[AUDIO] system mix helper started.");
                            println!("[STATE] AUDIO_SYSTEM");
                            Some(h)
                        }
                        Err(e) => {
                            eprintln!("[AUDIO] failed to start system mix helper: {:?}", e);
                            println!("[STATE] AUDIO_ERROR");
                            None
                        }
                    },
                    AudioCommand::Stop => {
                        println!("[STATE] AUDIO_OFF");
                        None
                    }
                };
            }

            let Some(h) = helper.as_mut() else {
                std::thread::sleep(std::time::Duration::from_millis(10));
                continue;
            };

            match read_exact_pcm_20ms(&mut h.stdout) {
                Ok(pcm) => {
                    let opus = audio_encoder.encode(&pcm);

                    let sample = Sample {
                        data: Bytes::from(opus),
                        duration: Duration::from_millis(20),
                        ..Default::default()
                    };

                    if tx_audio_clone.blocking_send(sample).is_err() {
                        return;
                    }
                }
                Err(e) => {
                    eprintln!("[AUDIO] helper read failed: {:?}", e);

                    if let Some(mut old) = helper.take() {
                        let _ = old.child.kill();
                        let _ = old.child.wait();
                    }

                    println!("[STATE] AUDIO_ERROR");
                    std::thread::sleep(std::time::Duration::from_millis(100));
                }
            }
        }
    });

    let audio_track_clone = audio_track.clone();
    tokio::spawn(async move {
        while let Some(sample) = rx_audio.recv().await {
            if audio_track_clone.write_sample(&sample).await.is_err() {
                // drop frame
            }
        }
    });

    // -------------------------------
    // DataChannel
    // -------------------------------
    let controller_clone = controller.clone();
    let nvenc_cmd_tx_clone = nvenc_cmd_tx.clone();
    let rumble_tx_for_dc = rumble_tx.clone();

    let last_force_keyframe_at = Arc::new(AtomicU64::new(0));
    let last_force_keyframe_at_clone = last_force_keyframe_at.clone();

    let pc = webrtc.peer.clone();
    let last_gamepad_seq = Arc::new(AtomicU32::new(0));

    webrtc.peer.on_data_channel(Box::new(move |dc| {
        println!("DataChannel accepted from client: {}", dc.label());

        let controller_clone = controller_clone.clone();
        let nvenc_cmd_tx_clone = nvenc_cmd_tx_clone.clone();
        let last_force_keyframe_at_clone = last_force_keyframe_at_clone.clone();
        let rumble_tx_for_dc = rumble_tx_for_dc.clone();
        let last_gamepad_seq_clone = last_gamepad_seq.clone();

        let pc_clone = pc.clone();
        let dc_clone = dc.clone();
        Box::pin(async move {
            let last_gamepad_seq = last_gamepad_seq_clone.clone();
            let pc_clone_clone = pc_clone.clone();
            let dc_clone_clone = dc_clone.clone();
            dc.on_open(Box::new(move || {
                let pc_clone_1 = pc_clone_clone.clone();
                let dc_clone_1 = dc_clone_clone.clone();
                let rumble_tx_for_open = rumble_tx_for_dc.clone();

                println!("[HOST] DataChannel OPEN");
                pc_clone.on_peer_connection_state_change(Box::new(move |_| {
                    let pc_clone_clone_clone = pc_clone_1.clone();
                    let dc_clone_clone_clone = dc_clone_1.clone();
                    Box::pin(async move {
                        let my_pc = pc_clone_clone_clone.clone();
                        let my_dc = dc_clone_clone_clone.clone();
                        log_pc_state("[HOST] pc.connection_state_change", &my_pc, &my_dc).await;
                    })
                }));

                let pc_clone_2 = pc_clone_clone.clone();
                let dc_clone_2 = dc_clone_clone.clone();
                pc_clone.on_ice_connection_state_change(Box::new(move |_| {
                    let pc_clone_clone_clone = pc_clone_2.clone();
                    let dc_clone_clone_clone = dc_clone_2.clone();
                    Box::pin(async move {
                        let my_pc = pc_clone_clone_clone.clone();
                        let my_dc = dc_clone_clone_clone.clone();
                        log_pc_state("[HOST] pc.ice_connection_state_change", &my_pc, &my_dc).await;
                    })
                }));

                let pc_clone_3 = pc_clone_clone.clone();
                let dc_clone_3 = dc_clone_clone.clone();
                pc_clone.on_signaling_state_change(Box::new(move |_| {
                    let pc_clone_clone_clone = pc_clone_3.clone();
                    let dc_clone_clone_clone = dc_clone_3.clone();
                    Box::pin(async move {
                        let my_pc = pc_clone_clone_clone.clone();
                        let my_dc = dc_clone_clone_clone.clone();
                        log_pc_state("[HOST] pc.on_signaling_state_change", &my_pc, &my_dc).await;
                    })
                }));

                let pc_clone_4 = pc_clone_clone.clone();
                let dc_clone_4 = dc_clone_clone.clone();
                dc_clone.on_close(Box::new(move || {
                    let pc_clone_clone_clone = pc_clone_4.clone();
                    let dc_clone_clone_clone = dc_clone_4.clone();
                    Box::pin(async move {
                        let my_pc = pc_clone_clone_clone.clone();
                        let my_dc = dc_clone_clone_clone.clone();
                        log_pc_state("[HOST] pc.on_close", &my_pc, &my_dc).await;
                    })
                }));

                let pc_clone_5 = pc_clone_clone.clone();
                let dc_clone_5 = dc_clone_clone.clone();
                dc_clone.on_error(Box::new(move |e| {
                    let pc_clone_clone_clone = pc_clone_5.clone();
                    let dc_clone_clone_clone = dc_clone_5.clone();
                    Box::pin(async move {
                        let my_pc = pc_clone_clone_clone.clone();
                        let my_dc = dc_clone_clone_clone.clone();
                        println!("[HOST] dc.error: {}", e);
                        log_pc_state("[HOST] dc.error", &my_pc, &my_dc);
                    })
                }));

                let dc_ping = dc_clone_clone.clone();
                tokio::spawn(async move {
                    loop {
                        sleep(Duration::from_millis(10000)).await;

                        if dc_ping.ready_state() != webrtc::data_channel::data_channel_state::RTCDataChannelState::Open {
                            break;
                        }

                        let ping = json!({
                            "type": "dc_ping",
                            "t": now_millis(),
                            "from": "host"
                        }).to_string();

                        if let Err(e) = dc_ping.send_text(ping).await {
                            eprintln!("[HOST] failed to send dc_ping: {:?}", e);
                            break;
                        }
                    }
                });
                
                // -------------------------------
                // Rumble sender task
                // -------------------------------
                let dc_rumble = dc_clone_clone.clone();
                let mut rumble_rx = rumble_tx_for_open.subscribe();

                tokio::spawn(async move {
                    loop {
                        if dc_rumble.label() != "controller" { break; }
                        let rumble = match rumble_rx.recv().await {
                            Ok(v) => v,
                            Err(broadcast::error::RecvError::Lagged(n)) => {
                                eprintln!("[HOST] rumble broadcast lagged: {}", n);
                                continue;
                            }
                            Err(broadcast::error::RecvError::Closed) => break,
                        };

                        if dc_rumble.ready_state() != RTCDataChannelState::Open {
                            eprintln!("[HOST] dc_rumble is not ready");
                            break;
                        }

                        let large = if rumble.large > 0 {
                            rumble.large
                        } else {
                            0
                        };

                        let small = if rumble.small > 0 {
                            rumble.small.max(120)
                        } else {
                            0
                        };

                        let msg = json!({
                            "type": "rumble",
                            "large": large,
                            "small": small,
                            "duration_ms": 120
                        }).to_string();

                        if let Err(e) = dc_rumble.send_text(msg).await {
                            eprintln!("[HOST] failed to send rumble: {:?}", e);
                            break;
                        }
                    }
                });

                Box::pin(async {})
            }));

            let dc_for_message = dc.clone();
            dc.on_message(Box::new(move |msg: DataChannelMessage| {
                let controller = controller_clone.clone();
                let nvenc_cmd_tx = nvenc_cmd_tx_clone.clone();
                let last_force_keyframe_at = last_force_keyframe_at_clone.clone();
                let dc = dc_for_message.clone();
                let last_gamepad_seq_clone = last_gamepad_seq.clone();

                Box::pin(async move {
                    let data = &msg.data;

                    if data.len() == 12 || data.len() == 16 {
                        let buttons = u16::from_le_bytes([data[0], data[1]]);
                        let lt = data[2];
                        let rt = data[3];
                        let lx = i16::from_le_bytes([data[4], data[5]]);
                        let ly = i16::from_le_bytes([data[6], data[7]]);
                        let rx = i16::from_le_bytes([data[8], data[9]]);
                        let ry = i16::from_le_bytes([data[10], data[11]]);

                        let seq = if data.len() >= 16 {
                            Some(u32::from_le_bytes([data[12], data[13], data[14], data[15]]))
                        } else {
                            None
                        };

                        if let Ok(mut ctrl) = controller.lock() {
                            let report = GamepadState {
                                buttons,
                                lt,
                                rt,
                                lx,
                                ly,
                                rx,
                                ry,
                            };

                            let result = ctrl.update(report);
                            
                            if let Some(seq) = seq {
                                let prev = last_gamepad_seq_clone.swap(seq, Ordering::Relaxed);

                                if prev != 0 {
                                    let expected = prev.wrapping_add(1);
                                    if seq != expected {
                                        println!(
                                            "[HOST GAMEPAD GAP] prev={} current={} lost_or_reordered={}",
                                            prev,
                                            seq,
                                            seq.wrapping_sub(expected)
                                        );
                                    }
                                }

                                if seq % 60 == 0 {
                                    println!(
                                        "[HOST GAMEPAD RECV] seq={} update={:?} buttons={} lt={} rt={} lx={} ly={} rx={} ry={}",
                                        seq, result, buttons, lt, rt, lx, ly, rx, ry
                                    );
                                }
                            }
                        }

                        return;
                    }

                    if let Ok(text) = std::str::from_utf8(data) {
                        if let Ok(v) = serde_json::from_str::<serde_json::Value>(text) {
                            if v.get("type").and_then(|x| x.as_str()) == Some("dc_ping") {
                                let t = v.get("t").and_then(|x| x.as_u64()).unwrap_or(0);

                                let pong = json!({
                                    "type": "dc_pong",
                                    "t": t,
                                    "receivedAt": now_millis(),
                                    "from": "host"
                                })
                                .to_string();

                                if let Err(e) = dc.send_text(pong).await {
                                    eprintln!("[HOST] failed to send dc_pong: {:?}", e);
                                }

                                return;
                            }

                            if v.get("type").and_then(|x| x.as_str()) == Some("dc_pong") {
                                return;
                            }
                        }

                        if is_force_keyframe_message(text) {
                            let now = now_millis();
                            let prev = last_force_keyframe_at.load(Ordering::Relaxed);

                            if now.saturating_sub(prev) >= 1000 {
                                last_force_keyframe_at.store(now, Ordering::Relaxed);
                                let _ = nvenc_cmd_tx.send(NvencCommand::ForceIdr);
                                println!("[HOST] force_keyframe requested from client: {}", text);
                            }

                            return;
                        }

                        println!("[HOST] text message on input dc: {}", text);
                    }
                })
            }));
        })
    }));

    // -------------------------------
    // VIDEO READER THREAD
    // -------------------------------
    let (tx_video, mut rx_video) = mpsc::channel::<Vec<u8>>(16);
    let last_nvenc_packet_at_reader = last_nvenc_packet_at.clone();

    std::thread::spawn(move || {
        let mut stdout = nvenc_stdout;

        loop {
            match read_packet(&mut stdout) {
                Ok(packet) => {
                    last_nvenc_packet_at_reader.store(now_millis(), Ordering::Relaxed);

                    if tx_video.blocking_send(packet).is_err() {
                        break;
                    }
                }
                Err(e) => {
                    eprintln!("video pipe closed/read error: {:?}", e);
                    break;
                }
            }
        }
    });

    let video_track = webrtc_clone.video_track.clone();
    let last_video_sample_at_writer = last_video_sample_at.clone();

    tokio::spawn(async move {
        while let Some(frame) = rx_video.recv().await {
            let filtered = rebuild_annexb_without_aud(&frame);

            let sample = Sample {
                data: Bytes::from(filtered),
                duration: VIDEO_FRAME_DURATION,
                ..Default::default()
            };

            if let Err(e) = video_track.write_sample(&sample).await {
                eprintln!("video write_sample failed: {:?}", e);
                tokio::time::sleep(Duration::from_millis(10)).await;
                continue;
            }

            last_video_sample_at_writer.store(now_millis(), Ordering::Relaxed);
        }
    });

    // -------------------------------
    // WATCHDOG TASK
    // -------------------------------
    let last_nvenc_packet_at_wd = last_nvenc_packet_at.clone();
    let last_video_sample_at_wd = last_video_sample_at.clone();
    let video_watchdog_fired_wd = video_watchdog_fired.clone();
    let nvenc_cmd_tx_wd = nvenc_cmd_tx.clone();

    tokio::spawn(async move {
        loop {
            sleep(Duration::from_millis(1000)).await;

            let now = now_millis();
            let packet_age = now.saturating_sub(last_nvenc_packet_at_wd.load(Ordering::Relaxed));
            let sample_age = now.saturating_sub(last_video_sample_at_wd.load(Ordering::Relaxed));

            if (packet_age > 3000 || sample_age > 3000)
                && !video_watchdog_fired_wd.swap(true, Ordering::SeqCst)
            {
                let _ = nvenc_cmd_tx_wd.send(NvencCommand::ForceIdr);
                eprintln!(
                    "[VIDEO WATCHDOG] STALL DETECTED packet_age={}ms sample_age={}ms",
                    packet_age, sample_age
                );
            }

            if packet_age < 500 && sample_age < 500 {
                video_watchdog_fired_wd.store(false, Ordering::SeqCst);
            }
        }
    });

    let ice_connected = Arc::new(AtomicBool::new(false));
    let ice_connected_for_cb = ice_connected.clone();

    webrtc.peer.on_ice_connection_state_change(Box::new(move |s| {
        println!("ICE: {:?}", s);

        if s == RTCIceConnectionState::Connected || s == RTCIceConnectionState::Completed {
            ice_connected_for_cb.store(true, Ordering::Release);

            println!("[STATE] ICE_CONNECTED");
            eprintln!("[HOST] audio command input enabled");
            eprintln!("[HOST] commands: pid <number> / audio_stop / system");
        }

        if s == RTCIceConnectionState::Disconnected
            || s == RTCIceConnectionState::Failed
            || s == RTCIceConnectionState::Closed
        {
            println!("[STATE] ICE_DISCONNECTED");
        }

        Box::pin(async {})
    }));
   
    // Offer / Answer Auto Exchange
    println!("[HOST] WAITING_FOR_OFFER");
    let offer_json = loop {
        match webrtc.fetch_offer().await {
            Ok(Some(offer)) => break offer,
            Ok(None) => {
                tokio::time::sleep(Duration::from_millis(500)).await;
            }
            Err(e) => {
                eprintln!("[HOST] fetch_offer error: {:?}", e);
                tokio::time::sleep(Duration::from_millis(1000)).await;
            }
        }
    };
    let initial_offer_json = offer_json.clone();

    println!("[STATE] OFFER_RECEIVED");

    // Apply Offer
    webrtc.set_remote_offer(&offer_json).await?;
    println!("[STATE] REMOTE_OFFER_SET");

    // Generate Answer
    let answer = webrtc.create_and_set_local_answer().await?;
    println!("[STATE] LOCAL_ANSWER_CREATED");

    // Send Answer
    webrtc.post_answer(&answer).await?;
    println!("[STATE] ANSWER_POSTED");

    // Start ICE Polling
    webrtc.start_client_candidate_polling().await;

    println!("[STATE] ICE_WAITING");

    while !ice_connected.load(Ordering::Acquire) {
        if quit_rx.try_recv().is_ok() {
            println!("[STATE] EXITING");
            return Ok(())
        }
        sleep(Duration::from_millis(100)).await;
    }

    println!("[STATE] HOST_READY");

    // -------------------------------
    // Check Offer Constantly
    // -------------------------------
    let webrtc_renegotiate = webrtc.clone();
    tokio::spawn(async move {
        let mut last_offer: Option<String> = Some(initial_offer_json);

        loop {
            match webrtc_renegotiate.fetch_offer().await {
                Ok(Some(offer_json)) => {
                    if last_offer.as_ref() == Some(&offer_json) {
                        sleep(Duration::from_millis(500)).await;
                        continue;
                    }

                    println!("[HOST] RENEGOTIATION_OFFER_RECEIVED");

                    match async {
                        webrtc_renegotiate.set_remote_offer(&offer_json).await?;

                        let answer = webrtc_renegotiate
                            .create_and_set_local_answer()
                            .await?;

                        webrtc_renegotiate.post_answer(&answer).await?;

                        Ok::<(), anyhow::Error>(())
                    }
                    .await
                    {
                        Ok(()) => {
                            println!("[HOST] RENEGOTIATION_ANSWER_POSTED");
                            last_offer = Some(offer_json);
                        }
                        Err(e) => {
                            eprintln!("[HOST] renegotiation failed: {:?}", e);
                        }
                    }
                }

                Ok(None) => {}

                Err(e) => {
                    eprintln!("[HOST] renegotiation fetch_offer error: {:?}", e);
                }
            }

            sleep(Duration::from_millis(500)).await;
        }
    });

    // -------------------------------
    // Stream Policy
    // -------------------------------
    let nvenc_cmd_tx_policy = nvenc_cmd_tx.clone();
    let webrtc_policy = webrtc.clone();
    
    tokio::spawn(async move {
        let mut capped = false;
        let mut direct_streak = 0u32;

        loop {
            match webrtc_policy.fetch_stream_policy().await {
                Ok(Some(policy)) => {
                    match policy.mode.as_str() {
                        "relay" => {
                            direct_streak = 0;

                            if !capped {
                                println!("[STREAM_POLICY] relay detected: apply cap");
                                let _ = nvenc_cmd_tx_policy
                                    .send(NvencCommand::ApplyStreamPolicy(policy));
                                capped = true;
                            }
                        }

                        "direct" => {
                            if capped {
                                direct_streak += 1;

                                if direct_streak >= CAP_THRESHOLD {
                                    println!("[STREAM_POLICY] direct stable: restore preset");
                                    let _ = nvenc_cmd_tx_policy
                                        .send(NvencCommand::RestorePreset);

                                    capped = false;
                                    direct_streak = 0;
                                }
                            }
                        }

                        other => {
                            eprintln!("[STREAM_POLICY] unknown mode: {}", other);
                        }
                    }
                }

                Ok(None) => {}

                Err(e) => {
                    eprintln!("[STREAM_POLICY] fetch failed: {:?}", e);
                }
            }

            sleep(Duration::from_secs(1)).await;
        }
    });

    loop {
        if quit_rx.try_recv().is_ok() {
            println!("[STATE] EXITING");
            break;
        }
        sleep(Duration::from_secs(200)).await;
    }

    Ok(())
}

fn is_force_keyframe_message(text: &str) -> bool {
    let t = text.trim();

    t == "force_keyframe"
        || t.contains("\"type\":\"force_keyframe\"")
        || t.contains("\"type\": \"force_keyframe\"")
}

fn spawn_stdin_router(
    audio_cmd_tx: std::sync::mpsc::Sender<AudioCommand>,
    quit_tx: std::sync::mpsc::Sender<()>
) {
    std::thread::spawn(move || {
        use std::io::{self, BufRead};

        let stdin = io::stdin();
        for line in stdin.lock().lines() {
            let Ok(line) = line else { break };
            let line = line.trim();

            if let Some(rest) = line.strip_prefix("pid ") {
                match rest.trim().parse::<u32>() {
                    Ok(pid) => {
                        let _ = audio_cmd_tx.send(AudioCommand::UsePid(pid));
                        eprintln!("[HOST] requested audio switch to pid={}", pid);
                    }
                    Err(e) => {
                        eprintln!("[HOST] invalid pid: {:?} ({})", e, line);
                    }
                }
            } else if line.eq_ignore_ascii_case("audio_stop") {
                let _ = audio_cmd_tx.send(AudioCommand::Stop);
                eprintln!("[HOST] requested audio stop");
            } else if line.eq_ignore_ascii_case("system") {
                let _ = audio_cmd_tx.send(AudioCommand::UseSystemMix);
                eprintln!("[HOST] requested audio switch to system mix");
            } else if line.eq_ignore_ascii_case("quit") {
                let _ = quit_tx.send(());
                std::process::exit(0);
            } else {
                eprintln!("[HOST] commands: pid <number> / audio_stop / system / quit");
            }
        }
    });
}

fn spawn_audio_helper(pid: u32) -> Result<AudioHelper> {
    let mut child = Command::new("ProcessAudioCapture.exe")
        .arg(pid.to_string())
        .stdout(Stdio::piped())
        .stderr(Stdio::inherit())
        .spawn()
        .map_err(|e| anyhow!("failed to spawn ProcessAudioCapture.exe: {:?}", e))?;

    let stdout = child
        .stdout
        .take()
        .ok_or_else(|| anyhow!("failed to take helper stdout"))?;

    Ok(AudioHelper { child, stdout })
}

fn spawn_system_mix_helper() -> Result<AudioHelper> {
    let mut child = Command::new("SystemMixCapture.exe")
        .stdout(Stdio::piped())
        .stderr(Stdio::inherit())
        .spawn()
        .map_err(|e| anyhow!("failed to spawn SystemMixCapture.exe: {:?}", e))?;

    let stdout = child
        .stdout
        .take()
        .ok_or_else(|| anyhow!("failed to take helper stdout"))?;

    Ok(AudioHelper { child, stdout })
}

fn read_exact_pcm_20ms(stdout: &mut ChildStdout) -> std::io::Result<Vec<i16>> {
    // 48kHz, stereo, 16bit, 20ms
    // 48000 * 0.02 = 960 frames
    // 960 * 2ch * 2bytes = 3840 bytes
    let mut buf = [0u8; 3840];
    stdout.read_exact(&mut buf)?;

    let mut pcm = Vec::with_capacity(1920);
    for chunk in buf.chunks_exact(2) {
        pcm.push(i16::from_le_bytes([chunk[0], chunk[1]]));
    }

    Ok(pcm)
}

fn read_packet<R: Read>(r: &mut R) -> std::io::Result<Vec<u8>> {
    let mut len_buf = [0u8; 4];
    r.read_exact(&mut len_buf)?;
    let len = u32::from_le_bytes(len_buf) as usize;

    let mut buf = vec![0u8; len];
    r.read_exact(&mut buf)?;
    Ok(buf)
}

fn split_annexb_nals(data: &[u8]) -> Vec<&[u8]> {
    let mut out = Vec::new();
    let mut i = 0;

    while i + 3 < data.len() {
        let sc_len = if i + 4 <= data.len() && &data[i..i + 4] == [0, 0, 0, 1] {
            4
        } else if &data[i..i + 3] == [0, 0, 1] {
            3
        } else {
            i += 1;
            continue;
        };

        let start = i + sc_len;
        i = start;

        let mut end = data.len();
        let mut j = i;
        while j + 3 < data.len() {
            if (j + 4 <= data.len() && &data[j..j + 4] == [0, 0, 0, 1])
                || &data[j..j + 3] == [0, 0, 1]
            {
                end = j;
                break;
            }
            j += 1;
        }

        if start < end {
            out.push(&data[start..end]);
        }
        i = end;
    }

    out
}

fn rebuild_annexb_without_aud(data: &[u8]) -> Vec<u8> {
    let mut out = Vec::new();
    let nals = split_annexb_nals(data);

    for nal in nals {
        if nal.is_empty() {
            continue;
        }

        let nal_type = nal[0] & 0x1f;

        if nal_type == 9 {
            continue;
        }

        out.extend_from_slice(&[0, 0, 0, 1]);
        out.extend_from_slice(nal);
    }

    out
}

fn now_millis() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis() as u64
}

async fn log_pc_state(
    prefix: &str,
    pc: &Arc<webrtc::peer_connection::RTCPeerConnection>,
    dc: &webrtc::data_channel::RTCDataChannel
) {
    println!("
        [HOST] {}
        time: {}
        pcConnectionState: {:?}
        iceConnectionState: {:?}
        iceGatheringState: {:?}
        signalingState: {:?}
        dcReadyState: {:?}
        dcBufferedAmount: {:?}",
        prefix,
        now_millis(),
        pc.connection_state(),
        pc.ice_connection_state(),
        pc.ice_gathering_state(),
        pc.signaling_state(),
        dc.ready_state(),
        dc.buffered_amount().await
    )
}