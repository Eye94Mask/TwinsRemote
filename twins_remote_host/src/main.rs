mod dxgi_capture;
mod webrtc_sender;
mod audio_encoder;
mod controller;
mod consts;
mod env;

use anyhow::{anyhow, Result};
use std::io::{BufRead, Read, Write};
use std::process::{Child, ChildStdout, Command, Stdio};
use std::sync::{
    atomic::{AtomicBool, AtomicU32, AtomicU64, Ordering},
    Arc, Mutex,
};
use std::time::{SystemTime, UNIX_EPOCH};

use bytes::Bytes;
use rustls::crypto::ring::default_provider;
use tokio::sync::mpsc;
use tokio::time::{sleep, Duration};

use webrtc::data_channel::data_channel_message::DataChannelMessage;
use webrtc::ice_transport::ice_connection_state::RTCIceConnectionState;
use webrtc::media::Sample;

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

    println!("Please input session ID");

    let input_mode = Arc::new(AtomicU32::new(2)); // 0=answer, 1=audio, 2=session_id

    let (audio_cmd_tx, audio_cmd_rx) = std::sync::mpsc::channel::<AudioCommand>();

    let (answer_tx, answer_rx) = std::sync::mpsc::channel::<String>();

    let (session_id_tx, session_id_rx) = std::sync::mpsc::channel::<String>();

    spawn_stdin_router(
        input_mode.clone(),
        answer_tx,
        audio_cmd_tx.clone(),
        session_id_tx
    );
    
    let session_id = session_id_rx
        .recv()
        .map_err(|e| anyhow!("failed to receive session_id from stdin router: {:?}", e))?;
    
    let ice = IceConfig::load(&session_id);

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

    // ---------------
    // AUDIO THREAD
    // ---------------
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
                    AudioCommand::UseSystemMix => {
                        match spawn_system_mix_helper() {
                            Ok(h) => {
                                println!("[AUDIO] system mix helper started.");
                                Some(h)
                            }
                            Err(e) => {
                                eprintln!("[AUDIO] failed to start system mix helper: {:?}", e);
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

            // println!(
            //     "[VIDEO WATCHDOG] packet_age={}ms sample_age={}ms fired={}",
            //     packet_age, sample_age, video_watchdog_fired_wd.load(Ordering::Relaxed)
            // );

            if packet_age > 3000 || sample_age > 3000 && !video_watchdog_fired_wd.swap(true, Ordering::SeqCst) {
                // eprintln!(
                //     "[VIDEO WATCHDOG] STALL DETECTED packet_age={}ms sample_age={}ms",
                //     packet_age, sample_age
                // );

                if let Err(e) = nvenc_cmd_tx_wd.send("force_idr\n".to_string()) {
                    // eprintln!("[VIDEO WATCHDOG] failed to send_force_idr: {:?}", e);
                } else {
                    // eprintln!("[VIDEO WATCHDOG] force_idr sent");
                }
            }

            // 復帰したら再び発火可能に戻す
            if packet_age < 500 && sample_age < 500 {
                video_watchdog_fired_wd.store(false, Ordering::SeqCst);
            }
        }
    });

    let ice_connected = Arc::new(AtomicBool::new(false));
    let ice_connected_for_cb = ice_connected.clone();
    let input_mode_for_ice = input_mode.clone();

    webrtc.peer.on_ice_connection_state_change(Box::new(move |s| {
        println!("ICE: {:?}", s);

        if s == RTCIceConnectionState::Connected || s == RTCIceConnectionState::Completed {
            ice_connected_for_cb.store(true, Ordering::Release);
            input_mode_for_ice.store(1, Ordering::Release);

            eprintln!("[HOST] audio command input enabled");
            eprintln!("[HOST] commands: pid <number> / audio_stop / system");
        }

        Box::pin(async {})
    }));

   
    // Poll Offer
    let offer_json = loop {
        match webrtc.fetch_offer().await {
            Ok(Some(o)) => break o,
            Ok(None) => {
                tokio::time::sleep(Duration::from_millis(500)).await;
            }
            Err(e) => {
                eprintln!("fetch_offer error: {:?}", e);
                tokio::time::sleep(Duration::from_millis(1000)).await;
            }
        }
    };

    println!("offer received");

    // Apply Offer
    webrtc.set_remote_offer(&offer_json).await?;

    // Generate Answer
    let answer = webrtc.create_and_set_local_answer().await?;

    // Send Answer
    webrtc.start_client_candidate_polling().await;

    println!("waiting ICE...");

    while !ice_connected.load(Ordering::Acquire) {
        sleep(Duration::from_millis(100)).await;
    }

    println!("ICE finished, switching stdin to audio command");

    loop {
        sleep(Duration::from_secs(3600)).await;
    }

    Ok(())
}

struct AudioHelper {
    child: Child,
    stdout: ChildStdout,
}

enum AudioCommand {
    UsePid(u32),
    UseSystemMix,
    Stop,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum InputMode {
    WaitAnswer,
    WaitIce,
    AudioCommand
}

fn spawn_stdin_router(
    input_mode: Arc<AtomicU32>,
    answer_tx: std::sync::mpsc::Sender<String>,
    audio_cmd_tx: std::sync::mpsc::Sender<AudioCommand>,
    session_id_tx: std::sync::mpsc::Sender<String>
) {
    std::thread::spawn(move || {
        use std::io::{self, BufRead};

        let stdin = io::stdin();
        for line in stdin.lock().lines() {
            let Ok(line) = line else { break };
            let line = line.trim().to_string();

            match input_mode.load(Ordering::Acquire) {
                0 => {
                    let _ = answer_tx.send(line);
                }
                1 => {
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
                    } else {
                        eprintln!("[HOST] commands: pid <number> / audio_stop / system");
                    }
                }
                2 => {
                    let _ = session_id_tx.send(line);
                }
                _ => {}
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