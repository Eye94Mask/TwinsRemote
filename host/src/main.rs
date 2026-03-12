mod dxgi_capture;
mod ffmpeg;
mod webrtc_sender;
mod audio_capture;
mod audio_encoder;
mod consts;

use std::sync::Arc;

use anyhow::Result;

use tokio::time::{sleep, Duration};

use bytes::Bytes;

use rustls::crypto::ring::default_provider;

use webrtc::media::Sample;

use dxgi_capture::DxgiCapture;
use ffmpeg::FfmpegEncoder;
use webrtc_sender::WebRtcSender;
use audio_capture::AudioCapture;
use audio_encoder::AudioEncoder;
use crate::consts::{FPS_MILLIS, AUDIO_FRAME};

#[tokio::main]
async fn main() -> Result<()> {
    default_provider().install_default().expect("install rustls crypto provider");
    println!("Starting Remote Play Host");
    
    let mut video_capture = DxgiCapture::new()?;
    let (width, height) = video_capture.resolution();

    let mut encoder = FfmpegEncoder::new(width, height)?;
    
    let webrtc = Arc::new(WebRtcSender::new().await?);
    let webrtc_clone = webrtc.clone();
    
    encoder.send_nal(webrtc_clone).await?;
    let audio_track = webrtc.audio_track.clone();
    
    // ---------------
    // VIDEO
    // ---------------
    tokio::spawn(async move {
        loop {
            match video_capture.capture_frame() {
                Ok(Some(frame)) => {
                    if encoder.encode(&frame).await.is_err() {
                        eprintln!("encoder stdin closed");
                        break;
                    }
                }
                Ok(None) => {}
                Err(e) => {
                    eprintln!("capture error: {:?}", e);
                }
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