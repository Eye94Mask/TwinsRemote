mod dxgi_capture;
mod webrtc_sender;
mod audio_encoder;
mod controller;
mod consts;
mod env;

use anyhow::{anyhow, Result};
use serde::Serialize;
use std::sync::{Arc, Mutex, atomic::{AtomicBool, AtomicU32, AtomicU64, Ordering}};
use std::process::{Command, Stdio, ChildStdout, Child};
use std::io::{BufRead, Read, Write};
use std::time::{SystemTime, UNIX_EPOCH};

use tokio::time::{sleep, Duration};
use tokio::sync::mpsc;

use bytes::Bytes;

use rustls::crypto::ring::default_provider;

use webrtc::data_channel::data_channel_message::DataChannelMessage;
use webrtc::data_channel::RTCDataChannel;
use webrtc::data_channel::data_channel_init::RTCDataChannelInit;
use webrtc::media::Sample;
use webrtc::track::track_local::track_local_static_sample::TrackLocalStaticSample;

use windows::Win32::System::Com::{CoInitializeEx, COINIT_MULTITHREADED};

use crate::dxgi_capture::DxgiCapture;
use crate::webrtc_sender::WebRtcSender;
use crate::audio_encoder::AudioEncoder;
use crate::controller::{Controller, GamepadState};
use crate::consts::{FPS_MILLIS, AUDIO_FRAME, VIDEO_FRAME_DURATION};
use crate::env::IceConfig;

#[tokio::main]
async fn main() -> Result<()> {
    default_provider().install_default().expect("install rustls crypto provider");
    println!("Starting Remote Play Host");
    
    let ice = IceConfig::load();

    let webrtc = WebRtcSender::new(ice.clone()).await?;
    let webrtc_clone = webrtc.clone();

    let controller = Arc::new(Mutex::new(Controller::new()?));
    
    // balanced: バランス型(普段用)
    // stable:   安定重視型(重いゲーム)
    // quality:  画質重視型(軽いゲーム)
    // mobile:   帯域節約型
    let preset = "balanced";

    let mut child = Command::new("NvEnc.exe")
        .arg(preset)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::inherit())
        .spawn()
        .expect("failed to start nvenc");

    let mut stdin = child.stdin.take().expect("failed to take nvenc stdin");
    let mut stdout = child.stdout.take().expect("failed to take nvenc stdout");

    let last_nvenc_packet_at = Arc::new(AtomicU64::new(now_millis()));
    let last_video_sample_at = Arc::new(AtomicU64::new(now_millis()));
    let video_watchdog_fired = Arc::new(AtomicBool::new(false));

    // Thread for sending comands to NvEnc.exe
    let (nvenc_cmd_tx, nvenc_cmd_rx) = std::sync::mpsc::channel::<String>();
    std::thread::spawn(move || {
        while let Ok(cmd) = nvenc_cmd_rx.recv() {
            if let Err(e) = stdin.write_all(cmd.as_bytes()) {
                eprintln!("[HOST] nvenc stdin write failed: {:?}", e);
                break;
            }
            if let Err(e) = stdin.flush() {
                eprintln!("[HOST] nvenc stdin flush failed: {:?}", e);
                break;
            }
        }
    });

    let (audio_cmd_tx, audio_cmd_rx) = std::sync::mpsc::channel::<AudioCommand>();

    std::thread::spawn(move || {
        let stdin = std::io::stdin();

        for line in stdin.lock().lines() {
            let Ok(line) = line else { break };
            let line = line.trim();

            if let Some(rest) = line.strip_prefix("pid ") {
                match rest.trim().parse::<u32>() {
                    Ok(pid) => {
                        let _ = audio_cmd_tx.send(AudioCommand::UsePid(pid));
                        println!("[HOST] requested audio switch to pid={}", pid);
                    }
                    Err(e) => {
                        eprintln!("[HOST] invalid pid: {:?} ({})", e, line);
                    }
                }
                continue;
            }

            if line.eq_ignore_ascii_case("audio_stop") {
                let _ = audio_cmd_tx.send(AudioCommand::Stop);
                println!("[HOST] requested audio stop");
                continue;
            }

            println!("[HOST] commands: pid <number> / audio_stop");
        }
    });

    // ---------------
    // AUDIO THREAD
    // ---------------
    let audio_track = webrtc.audio_track.clone();

    let (tx, mut rx) = tokio::sync::mpsc::channel::<Sample>(3);
    let tx_clone = tx.clone();

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
                    AudioCommand::UsePid(pid) => {
                        match spawn_audio_helper(pid) {
                            Ok(h) => {
                                println!("[AUDIO] helper started for pid={}", pid);
                                Some(h)
                            }
                            Err(e) => {
                                eprintln!("[AUDIO] failed to start helper: {:?}", e);
                                None
                            }
                        }
                    }
                    AudioCommand::Stop => None,
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

                    if tx_clone.blocking_send(sample).is_err() {
                        return;
                    }
                }
                Err(e) => {
                    eprintln!("[AUDIO] helper read failed: {:?}", e);

                    if let Some(mut old) = helper.take() {
                        let _ = old.child.kill();
                        let _ = old.child.wait();
                    }

                    std::thread::sleep(std::time::Duration::from_millis(100));
                }
            }
        }
    });

    let audio_track_clone = audio_track.clone();
    tokio::spawn(async move {
        while let Some(sample) = rx.recv().await {
            if audio_track_clone.write_sample(&sample).await.is_err() {
                // drop frame
            }
        }
    });

    // -------------------------------
    // DataChannel
    // -------------------------------
    let dc = webrtc.peer.create_data_channel("input", None).await?;
    println!("DataChannel label: {}", dc.label());

    let controller_clone = controller.clone();
    let nvenc_cmd_tx_clone = nvenc_cmd_tx.clone();
    let last_force_keyframe_at = Arc::new(AtomicU64::new(0));
    let last_force_keyframe_at_clone = last_force_keyframe_at.clone();
    dc.on_message(Box::new(move |msg: DataChannelMessage| {
        let controller = controller_clone.clone();
        let nvenc_cmd_tx = nvenc_cmd_tx_clone.clone();
        let last_force_keyframe_at = last_force_keyframe_at_clone.clone();
        
        Box::pin(async move {
            let data = &msg.data;

            // Xbox controller input from Client
            if data.len() == 12 {
                let buttons = u16::from_le_bytes([data[0], data[1]]);
                let lt = data[2];
                let rt = data[3];
                let lx = i16::from_le_bytes([data[4], data[5]]);
                let ly = i16::from_le_bytes([data[6], data[7]]);
                let rx = i16::from_le_bytes([data[8], data[9]]);
                let ry = i16::from_le_bytes([data[10], data[11]]);

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
                    let _ = ctrl.update(report);
                }

                return;
            }

            // String message from Client
            if let Ok(text) = std::str::from_utf8(data) {
                if text.contains("\"type\":\"force_keyframe\"")
                    || text.contains("\"type\": \"force_keyframe\"")
                    || text.trim() == "force_keyframe"
                {
                    // ...
                    return;
                }

                println!("[HOST] text message on input dc: {}", text);
            }
        })
    }));

    webrtc.peer.on_ice_connection_state_change(Box::new(|s| {
        println!("ICE: {:?}", s);
        Box::pin(async {})
    }));

    let audio_track = webrtc.audio_track.clone();

    // -------------------------------
    // VIDEO READER THREAD
    // -------------------------------
    let (tx_video, mut rx_video) = mpsc::channel::<Vec<u8>>(16);
    let last_nvenc_packet_at_reader = last_nvenc_packet_at.clone();
    std::thread::spawn(move || {
        let mut stdout = stdout;

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
    // WATCHDOG TASK THREAD
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

            println!(
                "[VIDEO WATCHDOG] packet_age={}ms sample_age={}ms fired={}",
                packet_age, sample_age, video_watchdog_fired_wd.load(Ordering::Relaxed)
            );

            if packet_age > 3000 || sample_age > 3000 && !video_watchdog_fired_wd.swap(true, Ordering::SeqCst) {
                eprintln!(
                    "[VIDEO WATCHDOG] STALL DETECTED packet_age={}ms sample_age={}ms",
                    packet_age, sample_age
                );

                if let Err(e) = nvenc_cmd_tx_wd.send("force_idr\n".to_string()) {
                    eprintln!("[VIDEO WATCHDOG] failed to send_force_idr: {:?}", e);
                } else {
                    eprintln!("[VIDEO WATCHDOG] force_idr sent");
                }
            }

            // 復帰したら再び発火可能に戻す
            if packet_age < 500 && sample_age < 500 {
                video_watchdog_fired_wd.store(false, Ordering::SeqCst);
            }
        }
    });

    webrtc.peer.on_ice_connection_state_change(Box::new(|s| {
        println!("ICE: {:?}", s);
        Box::pin(async {})
    }));

    webrtc.get_host_ice_candidate().await?;
    webrtc.generate_offer().await?;

    println!("waiting for answer...");
    webrtc.set_answer().await?;

    loop {
        webrtc.add_client_candidate().await?;
    }

    Ok(())
}

struct AudioHelper {
    child: Child,
    stdout: ChildStdout,
}

enum AudioCommand {
    UsePid(u32),
    Stop,
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

fn read_exact_pcm_20ms(stdout: &mut ChildStdout) -> std::io::Result<Vec<i16>> {
    // 48kHz, stereo, 16bit, 20ms:
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

        // AUD(9) を除外
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