mod dxgi_capture;
// mod ffmpeg;
mod webrtc_sender;
mod audio_capture;
mod audio_encoder;
mod controller;
mod consts;

use std::sync::{Arc, Mutex};
use std::process::{Command, Stdio, ChildStdout};
use std::io::Read;

use anyhow::Result;

use tokio::time::{sleep, Duration};
use tokio::sync::mpsc;

use bytes::Bytes;

use rustls::crypto::ring::default_provider;

use webrtc::data_channel::data_channel_message::DataChannelMessage;
use webrtc::data_channel::RTCDataChannel;
use webrtc::data_channel::data_channel_init::RTCDataChannelInit;
use webrtc::media::Sample;
use webrtc::track::track_local::track_local_static_sample::TrackLocalStaticSample;

use dxgi_capture::DxgiCapture;
// use ffmpeg::FfmpegEncoder;
use webrtc_sender::WebRtcSender;
use audio_capture::AudioCapture;
use audio_encoder::AudioEncoder;
use controller::{Controller, GamepadState};
use crate::consts::{FPS_MILLIS, AUDIO_FRAME, VIDEO_FRAME_DURATION};

#[tokio::main]
async fn main() -> Result<()> {
    default_provider().install_default().expect("install rustls crypto provider");
    println!("Starting Remote Play Host");
    
    // let video_capture = DxgiCapture::new()?;
    // let (width, height) = video_capture.resolution();

    // let mut encoder = FfmpegEncoder::new(width, height)?;
    
    let webrtc = Arc::new(WebRtcSender::new().await?);
    let webrtc_clone = webrtc.clone();

    let controller = Arc::new(Mutex::new(Controller::new()?));

    // !!!New Constructure Start!!!
    
    // encoder.send_nal(webrtc_clone).await?;

    let mut child = Command::new("nvenc_min.exe")
        .stdout(Stdio::piped())
        .stderr(Stdio::inherit())
        .spawn()
        .expect("failed to start nvenc");

    let mut stdout = child.stdout.take().unwrap();

    let audio_track = webrtc.audio_track.clone();

    // ---------------
    // AUDIO THREAD
    // ---------------
    let (tx, mut rx) = tokio::sync::mpsc::channel::<Sample>(3);
    let tx_clone = tx.clone();
    std::thread::spawn(move || {
        let mut audio_capture = AudioCapture::new().unwrap();
        let mut audio_encoder = AudioEncoder::new();

        let mut pcm_buffer: Vec<i16> = Vec::new();

        loop {
            if let Some(pcm) = audio_capture.capture() {
                pcm_buffer.extend_from_slice(&pcm);

                while pcm_buffer.len() >= AUDIO_FRAME {
                    let frame: Vec<i16> = pcm_buffer.drain(..AUDIO_FRAME).collect();
                    let opus = audio_encoder.encode(&frame);

                    let sample = Sample {
                        data: Bytes::from(opus),
                        duration: Duration::from_millis(20),
                        ..Default::default()
                    };

                    if tx_clone.blocking_send(sample).is_err() { break; }
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

    let init = RTCDataChannelInit::default();
    dc.on_message(Box::new(move |msg: DataChannelMessage| {
        let data = &msg.data;

        if data.len() < 12 {
            return Box::pin(async {});
        }
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
                ry
            };
            let _ = ctrl.update(report);
        }

        Box::pin(async {})
    }));

    webrtc.peer.on_ice_connection_state_change(Box::new(|s| {
        println!("ICE: {:?}", s);
        Box::pin(async {})
    }));

    let _ = webrtc.get_host_ice_candidate().await;
    webrtc.generate_offer().await?;
    let _ = webrtc.peer.gathering_complete_promise().await;

    webrtc.set_answer().await?;

    // -------------------------------
    // VIDEO READER THREAD
    // -------------------------------
    let (tx_video, mut rx_video) = mpsc::channel::<Vec<u8>>(8);

    std::thread::spawn(move || {
        let mut stdout = stdout;

        loop {
            match read_packet(&mut stdout) {
                Ok(packet) => {
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
                tokio::time::sleep(Duration::from_millis(100)).await;
                break;
            }
        }
    });

    loop {
        webrtc.add_client_candidate().await?;
    }

    Ok(())
}

fn find_nal_units(buffer: &mut Vec<u8>) -> Vec<Vec<u8>> {
    let mut nal_units = Vec::new();
    let mut i = 0;

    while i + 4 < buffer.len() {
        if &buffer[i..i+4] == [0,0,0,1] {
            let start = i + 4;
            i = start;

            while i + 4 < buffer.len() &&
                &buffer[i..i+4] != [0,0,0,1] {
                i += 1;
            }

            nal_units.push(buffer[start..i].to_vec());
        } else {
            i += 1;
        }
    }

    nal_units
}

async fn send_frame(
    track: &std::sync::Arc<TrackLocalStaticSample>,
    nals: &Vec<Vec<u8>>,
) -> Result<()> {
    let mut data = Vec::new();

    for nal in nals {
        // AnnexB start code
        data.extend_from_slice(&[0, 0, 0, 1]);
        data.extend_from_slice(nal);
    }

    let sample = Sample {
        data: Bytes::from(data),
        duration: Duration::from_millis(33),
        ..Default::default()
    };

    track.write_sample(&sample).await?;
    Ok(())
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