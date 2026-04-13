use anyhow::Result;
use std::sync::Arc;
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use serde::{Deserialize, Serialize};
use base64::{engine::general_purpose::STANDARD, Engine as _};
use hmac::{Hmac, Mac, KeyInit};
use sha1::Sha1;

use webrtc::api::APIBuilder;
use webrtc::api::media_engine::MediaEngine;
use webrtc::api::interceptor_registry::register_default_interceptors;
use webrtc::ice_transport::ice_candidate::RTCIceCandidateInit;
use webrtc::interceptor::registry::Registry;
use webrtc::ice_transport::ice_server::RTCIceServer;
use webrtc::ice_transport::ice_credential_type::RTCIceCredentialType;
use webrtc::peer_connection::policy::ice_transport_policy::RTCIceTransportPolicy;
use webrtc::peer_connection::configuration::RTCConfiguration;
use webrtc::rtp_transceiver::rtp_codec::RTCRtpCodecCapability;
use webrtc::track::track_local::track_local_static_sample::TrackLocalStaticSample;
use webrtc::peer_connection::sdp::session_description::RTCSessionDescription;

use crate::env::IceConfig;

type HmacSha1 = Hmac<Sha1>;

#[derive(Clone)]
pub struct WebRtcSender {
    pub video_track: Arc<TrackLocalStaticSample>,
    pub audio_track: Arc<TrackLocalStaticSample>,
    pub peer: Arc<webrtc::peer_connection::RTCPeerConnection>
}

#[derive(Deserialize)]
pub struct Offer {
    pub sdp: String,
    pub r#type: String
}

#[derive(Serialize)]
pub struct Answer {
    pub sdp: String,
    pub r#type: String
}

fn current_unix_time() -> u64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .expect("clock error")
        .as_secs()
}

fn create_turn_credentials(
    secret: &str,
    ttl_seconds: u64,
    user_id: &str,
) -> anyhow::Result<(String, String)> {
    let expiry = current_unix_time() + ttl_seconds;
    let username = format!("{}:{}", expiry, user_id);

    let mut mac = HmacSha1::new_from_slice(secret.as_bytes())?;
    mac.update(username.as_bytes());
    let result = mac.finalize().into_bytes();
    let credential = STANDARD.encode(result);

    Ok((username, credential))
}

impl WebRtcSender {
    pub async fn new(ice: IceConfig) -> Result<Self> {
        let mut m = MediaEngine::default();
        m.register_default_codecs()?;

        let mut registry = Registry::new();
        registry = register_default_interceptors(registry, &mut m)?;

        let api = APIBuilder::new()
            .with_media_engine(m)
            .with_interceptor_registry(registry)
            .build();

        let (turn_username, turn_credential) = create_turn_credentials(
            &ice.turn_shared_secret,
            ice.turn_ttl_seconds,
            &ice.turn_user_id
        )?;

        let config = RTCConfiguration {
            ice_servers: vec![
                RTCIceServer {
                    urls: vec![ice.stun_url],
                    ..Default::default()
                },
                RTCIceServer {
                    urls: vec![ice.turn_url],
                    username: turn_username,
                    credential: turn_credential,
                    credential_type: RTCIceCredentialType::Password,
                    ..Default::default()
                },
            ],
            ice_transport_policy: RTCIceTransportPolicy::Relay,
            ..Default::default()
        };

        let peer = Arc::new(api.new_peer_connection(config).await?);

        let video_track = Arc::new(TrackLocalStaticSample::new(
            RTCRtpCodecCapability {
                mime_type: "video/H264".to_string(),
                clock_rate: 90000,
                channels: 0,
                sdp_fmtp_line: "".to_string(),
                rtcp_feedback: vec![]
            },
            "video".to_string(),
            "nvenc".to_string()
        ));
        let video_sender = peer.add_track(video_track.clone()).await?;

        tokio::spawn(async move {
            let mut rtcp_buf = vec![0u8; 1500];
            loop {
                match video_sender.read(&mut rtcp_buf).await {
                    Ok((_n, _)) => {}
                    Err(e) => {
                        eprintln!("video RTCP read failed: {:?}", e);
                        break;
                    }
                }
            }
        });

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

        Ok(Self {
            video_track,
            audio_track,
            peer
        })
    }

    pub async fn generate_offer(&self) -> Result<()> {
        let offer = self.peer.create_offer(None).await?;
        self.peer.set_local_description(offer).await?;

        println!("=== OFFER ===");
        println!(
            "{}",
            serde_json::to_string_pretty(&self.peer.local_description().await.unwrap())?
        );

        Ok(())
    }

    pub async fn set_answer_from_json(&self, input: &str) -> Result<()> {
        let answer: RTCSessionDescription = serde_json::from_str(input)?;
        self.peer.set_remote_description(answer).await?;

        if let Some(local) = self.peer.local_description().await {
            eprintln!("LOCAL SDP:\n{}", local.sdp);
        }
        if let Some(remote) = self.peer.remote_description().await {
            eprintln!("REMOTE SDP:\n{}", remote.sdp);
        }
        Ok(())
    }

    pub async fn get_host_ice_candidate(&self) -> Result<()> {
        self.peer.on_ice_candidate(Box::new(|c| {
            if let Some(c) = c {
                println!(
                    "HOST ICE CANDIDATE: \n{}",
                    serde_json::to_string(&c.to_json().unwrap()).unwrap()
                );
            }

            Box::pin(async {})
        }));

        Ok(())
    }

    pub async fn add_client_candidate_from_json(&self, input: &str) -> Result<()> {
        if input.trim().is_empty() {
            return Ok(());
        }

        let candidate: RTCIceCandidateInit = serde_json::from_str(input)?;

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