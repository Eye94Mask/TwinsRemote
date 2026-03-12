mod dxgi_capture;
mod ffmpeg;
mod webrtc_sender;
mod consts;

use std::sync::Arc;

use anyhow::Result;

use tokio::time::{sleep, Duration};

use bytes::Bytes;

use rustls::crypto::ring::default_provider;

use dxgi_capture::DxgiCapture;
use ffmpeg::FfmpegEncoder;
use webrtc_sender::WebRtcSender;
use crate::consts::FPS_MILLIS;

#[tokio::main]
async fn main() -> Result<()> {
    default_provider().install_default().expect("install rustls crypto provider");
    println!("Starting Remote Play Host");
    
    let mut capture = DxgiCapture::new()?;
    let (width, height) = capture.resolution();

    let mut encoder = FfmpegEncoder::new(width, height)?;
    
    let webrtc = Arc::new(WebRtcSender::new().await?);
    let webrtc_clone = webrtc.clone();
    
    encoder.send_nal(webrtc_clone).await?;
    
    tokio::spawn(async move {
        loop {
            match capture.capture_frame() {
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