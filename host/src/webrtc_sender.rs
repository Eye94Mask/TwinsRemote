use anyhow::Result;
use std::sync::Arc;
use std::io::Read;
use std::process::ChildStdout;
use std::time::Duration;

use serde::{Deserialize, Serialize};
use bytes::Bytes;

use webrtc::api::APIBuilder;
use webrtc::api::media_engine::MediaEngine;
use webrtc::api::interceptor_registry::register_default_interceptors;
use webrtc::data_channel::data_channel_message::DataChannelMessage;
use webrtc::ice_transport::ice_candidate::RTCIceCandidateInit;
use webrtc::interceptor::registry::Registry;
use webrtc::media::Sample;
use webrtc::rtp::{packet::Packet, header::Header};
use webrtc::ice_transport::ice_server::RTCIceServer;
use webrtc::ice_transport::ice_credential_type::RTCIceCredentialType;
use webrtc::peer_connection::policy::ice_transport_policy::RTCIceTransportPolicy;
use webrtc::peer_connection::configuration::RTCConfiguration;
use webrtc::rtp_transceiver::rtp_codec::{RTCRtpCodecParameters, RTCRtpCodecCapability};
use webrtc::track::track_local::track_local_static_rtp::TrackLocalStaticRTP;
use webrtc::track::track_local::track_local_static_sample::TrackLocalStaticSample;
use webrtc::track::track_local::TrackLocalWriter;
use webrtc::peer_connection::sdp::session_description::RTCSessionDescription;

use crate::consts::{FPS_MILLIS, MTU};

#[derive(Clone)]
pub struct WebRtcSender {
    pub video_track: Arc<TrackLocalStaticRTP>,
    pub audio_track: Arc<TrackLocalStaticSample>,
    pub peer: Arc<webrtc::peer_connection::RTCPeerConnection>
}

#[derive(Deserialize)]
pub struct Offer {
    pub sdp: String,
    pub r#type: String,
}

#[derive(Serialize)]
pub struct Answer {
    pub sdp: String,
    pub r#type: String,
}

impl WebRtcSender {
    pub async fn new() -> Result<Self> {
        let mut m = MediaEngine::default();
        m.register_default_codecs()?;

        let mut registry = Registry::new();
        registry = register_default_interceptors(registry, &mut m)?;

        let api = APIBuilder::new()
            .with_media_engine(m)
            .with_interceptor_registry(registry)
            .build();
        
        let config = RTCConfiguration {
            ice_servers: vec![
                RTCIceServer {
                    urls: vec!["stun:stun.l.google.com:19302".to_string()],
                    ..Default::default()
                },
                RTCIceServer {
                    urls: vec![
                        "turn:43.207.155.19:3478?transport=udp".to_string()
                    ],
                    username: "test".to_string(),
                    credential: "password".to_string(),
                    credential_type: RTCIceCredentialType::Password,
                    ..Default::default()
                }
            ],
            ice_transport_policy: RTCIceTransportPolicy::Relay,
            ..Default::default()
        };
        let peer = Arc::new(api.new_peer_connection(config).await?);

        let video_track = Arc::new(TrackLocalStaticRTP::new(
            RTCRtpCodecCapability {
                mime_type: "video/H264".to_string(),
                clock_rate: 90000,
                channels: 0,
                sdp_fmtp_line: "level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=42e01f".to_string(),
                rtcp_feedback: vec![]
            },
            "video".to_string(),
            "webrtc-rs".to_string()
        ));
        peer.add_track(video_track.clone()).await?;

        let audio_track = Arc::new(TrackLocalStaticSample::new(
            RTCRtpCodecCapability {
                mime_type: "audio/opus".to_string(),
                clock_rate: 48000,
                channels: 2,
                sdp_fmtp_line: "".to_string(),
                rtcp_feedback: vec![]
            },
            "audio".to_string(),
            "webrtc-rs".to_string()
        ));
        peer.add_track(audio_track.clone()).await?;

        Ok(Self { video_track, audio_track, peer })
    }

    pub async fn generate_offer(&self) -> Result<()> {
        let offer = self.peer.create_offer(None).await?;
        self.peer.set_local_description(offer).await?;

        println!("=== OFFER ===");
        println!("{}", serde_json::to_string_pretty(&self.peer.local_description().await.unwrap())?);

        Ok(())
    }

    pub async fn set_answer(&self) -> Result<()> {
        println!("Paste ANSWER:");
        let mut input = String::new();
        std::io::stdin().read_line(&mut input)?;
        
        let answer: RTCSessionDescription = serde_json::from_str(&input)?;
        self.peer.set_remote_description(answer).await?;

        Ok(())
    }

    pub async fn get_host_ice_candidate(&self) -> Result<()> {
        self.peer.on_ice_candidate(Box::new(|c| {
            if let Some(c) = c {
                println!("HOST ICE CANDIDATE: \n{}", serde_json::to_string(&c.to_json().unwrap()).unwrap());
            }

            Box::pin(async {})
        }));

        Ok(())
    }

    pub async fn add_client_candidate(&self) -> Result<()> {
        println!("Paste CLIENT ICE candidate JSON(empty line to skip):");

        let mut input = String::new();
        std::io::stdin().read_line(&mut input)?;

        if input.trim().is_empty() {
            return Ok(());
        }

        let candidate: RTCIceCandidateInit = serde_json::from_str(&input)?;

        if candidate.candidate.is_empty() {
            println!("ICE gathering finished");
            return Ok(());
        }

        self.peer.add_ice_candidate(candidate).await?;

        println!("Client ICE candidate added.");
        Ok(())
    }

    pub async fn handle_offer(&self, offer: Offer) -> Result<Answer> {
        let offer = RTCSessionDescription::offer(offer.sdp)?;
        self.peer.set_remote_description(offer).await?;

        let answer = self.peer.create_answer(None).await?;
        self.peer.set_local_description(answer.clone()).await?;

        Ok(Answer {
            sdp: answer.sdp,
            r#type: "answer".to_string()
        })
    }
}