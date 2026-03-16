mod dxgi_capture;
mod ffmpeg;
mod webrtc_sender;
mod audio_capture;
mod audio_encoder;
mod controller;
mod consts;

use std::sync::{Arc, Mutex};

use anyhow::Result;

use tokio::time::{sleep, Duration};

use bytes::Bytes;

use rustls::crypto::ring::default_provider;

use webrtc::data_channel::data_channel_message::DataChannelMessage;
use webrtc::data_channel::RTCDataChannel;
use webrtc::data_channel::data_channel_init::RTCDataChannelInit;
use webrtc::media::Sample;

use dxgi_capture::DxgiCapture;
use ffmpeg::FfmpegEncoder;
use webrtc_sender::WebRtcSender;
use audio_capture::AudioCapture;
use audio_encoder::AudioEncoder;
use controller::{Controller, GamepadState};
use crate::consts::{FPS_MILLIS, AUDIO_FRAME};

#[tokio::main]
async fn main() -> Result<()> {
    default_provider().install_default().expect("install rustls crypto provider");
    println!("Starting Remote Play Host");
    
    let video_capture = DxgiCapture::new()?;
    let (width, height) = video_capture.resolution();

    let mut encoder = FfmpegEncoder::new(width, height)?;
    
    let webrtc = Arc::new(WebRtcSender::new().await?);
    let webrtc_clone = webrtc.clone();

    let controller = Arc::new(Mutex::new(Controller::new()?));
    
    encoder.send_nal(webrtc_clone).await?;
    let audio_track = webrtc.audio_track.clone();
    
    // ---------------
    // VIDEO
    // ---------------
    let latest_frame: Arc<Mutex<Option<Vec<u8>>>> = Arc::new(Mutex::new(None));
    let capture_slot = latest_frame.clone();
    let encode_slot = latest_frame.clone();

    // ---------------
    // Capture Thread
    // ---------------
    std::thread::spawn(move || {
        let mut video_capture = video_capture;

        loop {
            match video_capture.capture_frame() {
                Ok(Some(frame)) => {
                    let mut slot = capture_slot.lock().unwrap();
                    *slot = Some(frame);
                }

                Ok(None) => {}

                Err(e) => {
                    eprintln!("Capture error {:?}", e);
                }
            }
        }
    });

    // ---------------
    // Encode Thread
    // ---------------
    tokio::spawn(async move {
        loop {
            let frame = {
                let mut slot = encode_slot.lock().unwrap();
                slot.take()
            };
            if let Some(frame) = frame {
                if encoder.encode(&frame).await.is_err() {
                    eprintln!("encoder closed");
                    break;
                }
            } else {
                tokio::time::sleep(Duration::from_millis(1)).await;
            }
        }
    });

    // ---------------
    // AUDIO THREAD
    // ---------------
    tokio::task::spawn_blocking(move || {
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
                    let audio_track_clone = audio_track.clone();

                    tokio::runtime::Handle::current().block_on(async move {
                        let _ = audio_track_clone.write_sample(&sample).await;
                    });
                }
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

    loop {
        webrtc.add_client_candidate().await?;
    }
    Ok(())
}