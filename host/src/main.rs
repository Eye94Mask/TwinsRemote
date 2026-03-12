mod dxgi_capture;
mod ffmpeg;
mod webrtc_sender;
mod consts;

use std::sync::Arc;
use std::process::ChildStdin;
use std::io::Read;

use anyhow::Result;

use tokio::time::{sleep, Duration};

use bytes::Bytes;

use webrtc::api::APIBuilder;
use webrtc::peer_connection::configuration::RTCConfiguration;
use webrtc::rtp_transceiver::rtp_codec::RTCRtpCodecCapability;
use webrtc::track::track_local::track_local_static_rtp::TrackLocalStaticRTP;
use webrtc::track::track_local::TrackLocalWriter;

use rustls::crypto::ring::default_provider;

use dxgi_capture::DxgiCapture;
use ffmpeg::FfmpegEncoder;
use webrtc_sender::WebRtcSender;
use crate::consts::{FPS_MILLIS, MY_MONITOR_WIDTH, MY_MONITOR_HEIGHT};

#[tokio::main]
async fn main() -> Result<()> {
    default_provider().install_default().expect("install rustls crypto provider");
    println!("Starting Remote Play Host");
    
    let mut capture = DxgiCapture::new(MY_MONITOR_WIDTH, MY_MONITOR_HEIGHT)?;
    let mut encoder = FfmpegEncoder::new(MY_MONITOR_WIDTH, MY_MONITOR_HEIGHT)?;
    
    let webrtc = Arc::new(WebRtcSender::new().await?);
    let webrtc_clone = webrtc.clone();
    
    encoder.send_nal(webrtc_clone).await?;
    let (width, height) = capture.resolution();
    
    tokio::spawn(async move {
        loop {
            if let Ok((data, row_pitch)) = capture.capture_frame() {
                let mut packet = Vec::with_capacity((width * height * 4) as usize);

                for y in 0..height as usize {
                    let start = y * row_pitch as usize;
                    let end = start + (width as usize * 4);

                    packet.extend_from_slice(&data[start..end]);

                }
                
                if encoder.encode(&packet).await.is_err() {
                    eprintln!("stdin write failed");
                    break;
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