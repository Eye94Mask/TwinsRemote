use anyhow::{anyhow, Result};
use std::collections::HashSet;
use std::sync::Arc;
use std::time::{SystemTime, UNIX_EPOCH};
use std::env;

use base64::{engine::general_purpose::STANDARD, Engine as _};
use hmac::{Hmac, KeyInit, Mac};
use reqwest::Client;
use serde::Deserialize;
use sha1::Sha1;
use tokio::sync::Mutex;
use tokio::time::{sleep, Duration};

use webrtc::api::interceptor_registry::register_default_interceptors;
use webrtc::api::media_engine::MediaEngine;
use webrtc::api::APIBuilder;
use webrtc::ice_transport::ice_candidate::RTCIceCandidateInit;
use webrtc::ice_transport::ice_credential_type::RTCIceCredentialType;
use webrtc::ice_transport::ice_server::RTCIceServer;
use webrtc::ice_transport::ice_connection_state::RTCIceConnectionState;
use webrtc::interceptor::registry::Registry;
use webrtc::peer_connection::configuration::RTCConfiguration;
use webrtc::peer_connection::policy::ice_transport_policy::RTCIceTransportPolicy;
use webrtc::peer_connection::sdp::session_description::RTCSessionDescription;
use webrtc::rtp_transceiver::rtp_codec::RTCRtpCodecCapability;
use webrtc::track::track_local::track_local_static_sample::TrackLocalStaticSample;

use crate::env::IceConfig;

type HmacSha1 = Hmac<Sha1>;

#[derive(Clone)]
pub struct WebRtcSender {
    pub video_track: Arc<TrackLocalStaticSample>,
    pub audio_track: Arc<TrackLocalStaticSample>,
    pub peer: Arc<webrtc::peer_connection::RTCPeerConnection>,
    signal_base_url: String,
    session_id: String,
    http: Client,
}

#[derive(Debug, Deserialize)]
struct CandidatePollResponse {
    candidates: Vec<RTCIceCandidateInit>,
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

fn with_session_id(base: &str, path: &str, session_id: &str) -> String {
    format!(
        "{}/{}?sessionId={}",
        base.trim_end_matches('/'),
        path.trim_start_matches('/'),
        urlencoding::encode(session_id)
    )
}

fn candidate_dedup_key(c: &RTCIceCandidateInit) -> String {
    format!(
        "{}|{:?}|{:?}|{:?}",
        c.candidate,
        c.sdp_mid,
        c.sdp_mline_index,
        c.username_fragment
    )
}

impl WebRtcSender {
    pub async fn new(ice: IceConfig, session_id: &str) -> Result<Self> {
        let mut m = MediaEngine::default();
        m.register_default_codecs()?;

        let mut registry = Registry::new();
        registry = register_default_interceptors(registry, &mut m)?;

        let api = APIBuilder::new()
            .with_media_engine(m)
            .with_interceptor_registry(registry)
            .build();

        let turn_username = ice.ice_servers[1].username.clone().expect("turn_username not found");
        let turn_credential = ice.ice_servers[1].credential.clone().expect("turn_credential not found");

        let config = RTCConfiguration {
            ice_servers: vec![
                RTCIceServer {
                    urls: ice.ice_servers[0].urls.clone(),
                    ..Default::default()
                },
                RTCIceServer {
                    urls: ice.ice_servers[1].urls.clone(),
                    username: turn_username,
                    credential: turn_credential,
                    credential_type: RTCIceCredentialType::Password,
                    ..Default::default()
                },
            ],
            ice_transport_policy: RTCIceTransportPolicy::All,
            ..Default::default()
        };

        let peer = Arc::new(api.new_peer_connection(config).await?);

        peer.on_ice_gathering_state_change(Box::new(move |s| {
            println!("ICE gathering state: {:?}", s);
            Box::pin(async {})
        }));

        peer.on_ice_connection_state_change(Box::new(move |s: RTCIceConnectionState| {
            println!("[HOST] ICE state changed: {:?}", s);
            Box::pin(async {})
        }));

        let http = Client::new();
        let signal_base_url = env::var("SIGNAL_BASE_URL").map_err(|e| anyhow!("SIGNAL_BASE_URL is not set: {:?}", e))?;

        let post_url = with_session_id(&signal_base_url, "/host-candidate", session_id);
        let http_for_candidate = http.clone();

        peer.on_ice_candidate(Box::new(move |c| {
            let post_url = post_url.clone();
            let http = http_for_candidate.clone();

            Box::pin(async move {
                if let Some(c) = c {
                    match c.to_json() {
                        Ok(json) => {
                            println!(
                                "HOST ICE CANDIDATE: {}",
                                serde_json::to_string(&json).unwrap_or_default()
                            );

                            if let Err(e) = http
                                .post(&post_url)
                                .header("content-type", "application/json")
                                .json(&json)
                                .send()
                                .await
                            {
                                eprintln!("failed to post host candidate: {:?}", e);
                            }
                        }
                        Err(e) => {
                            eprintln!("failed to convert host candidate to json: {:?}", e);
                        }
                    }
                } else {
                    println!("HOST ICE gathering finished");
                }
            })
        }));

        let video_track = Arc::new(TrackLocalStaticSample::new(
            RTCRtpCodecCapability {
                mime_type: "video/H264".to_string(),
                clock_rate: 90000,
                channels: 0,
                sdp_fmtp_line:
                    "level-asymmetry-allowed=1;packetization-mode=1;profile-level-id=42e01f"
                        .to_string(),
                rtcp_feedback: vec![],
            },
            "video".to_string(),
            "nvenc".to_string(),
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
                sdp_fmtp_line: "minptime=10;useinbandfec=1".to_string(),
                rtcp_feedback: vec![],
            },
            "audio".to_string(),
            "webrtc-rs".to_string(),
        ));
        let audio_sender = peer.add_track(audio_track.clone()).await?;

        tokio::spawn(async move {
            let mut rtcp_buf = vec![0u8; 1500];
            loop {
                match audio_sender.read(&mut rtcp_buf).await {
                    Ok((_n, _)) => {}
                    Err(e) => {
                        eprintln!("audio RTCP read failed: {:?}", e);
                        break;
                    }
                }
            }
        });

        Ok(Self {
            video_track,
            audio_track,
            peer,
            signal_base_url,
            session_id: session_id.to_string(),
            http,
        })
    }

    pub async fn fetch_offer(&self) -> Result<Option<String>> {
        let url = with_session_id(
            &self.signal_base_url,
            "/offer",
            &self.session_id
        );

        let resp = self.http.get(&url).send().await?;

        if resp.status() == 204 {
            return Ok(None);
        }

        if !resp.status().is_success() {
            return Err(anyhow::anyhow!("fetch_offer failed: {}", resp.status()));
        }

        let text = resp.text().await?;
        Ok(Some(text))
    }

    pub async fn set_remote_offer(&self, json: &str) -> Result<()> {
        let offer: RTCSessionDescription = serde_json::from_str(json)?;
        self.peer.set_remote_description(offer).await?;
        Ok(())
    }

    pub async fn create_and_set_local_answer(&self) -> Result<String> {
        let answer = self.peer.create_answer(None).await?;
        self.peer.set_local_description(answer.clone()).await?;

        let json = serde_json::to_string(&answer)?;
        Ok(json)
    }

    pub async fn post_answer(&self, answer_json: &str) -> Result<()> {
        let url = with_session_id(&self.signal_base_url, "/answer", &self.session_id);

        let v: serde_json::Value = serde_json::from_str(answer_json)?;

        let resp = self
            .http
            .post(&url)
            .header("content-type", "application/json")
            .json(&v)
            .send()
            .await?;

        if !resp.status().is_success() {
            return Err(anyhow::anyhow!("post_answer failed: {}", resp.status()));
        }

        Ok(())
    }

    pub async fn start_client_candidate_polling(&self) {
        let url = with_session_id(
            &self.signal_base_url,
            "/client-candidate",
            &self.session_id,
        );

        let peer = self.peer.clone();
        let http = self.http.clone();

        let seen = Arc::new(Mutex::new(HashSet::<String>::new()));

        tokio::spawn(async move {
            loop {
                match http.get(&url).send().await {
                    Ok(resp) => {
                        if resp.status() == 204 {
                            sleep(Duration::from_millis(300)).await;
                            continue;
                        }

                        if resp.status().is_success() {
                            match resp.json::<CandidatePollResponse>().await {
                                Ok(payload) => {
                                    for candidate in payload.candidates {
                                        if candidate.candidate.trim().is_empty() {
                                            continue;
                                        }

                                        let key = candidate_dedup_key(&candidate);

                                        {
                                            let mut guard = seen.lock().await;
                                            if guard.contains(&key) {
                                                continue;
                                            }
                                            guard.insert(key);
                                        }

                                        match peer.add_ice_candidate(candidate.clone()).await {
                                            Ok(_) => {
                                                println!(
                                                    "CLIENT ICE added: {}",
                                                    serde_json::to_string(&candidate)
                                                        .unwrap_or_default()
                                                );
                                            }
                                            Err(e) => {
                                                eprintln!("addIceCandidate error: {:?}", e);
                                            }
                                        }
                                    }
                                }
                                Err(e) => {
                                    eprintln!("failed to parse client candidates: {:?}", e);
                                }
                            }
                        } else {
                            eprintln!("client candidate poll failed: {}", resp.status());
                        }
                    }
                    Err(e) => {
                        eprintln!("poll client candidates error: {:?}", e);
                    }
                }

                sleep(Duration::from_millis(300)).await;
            }
        });
    }
}