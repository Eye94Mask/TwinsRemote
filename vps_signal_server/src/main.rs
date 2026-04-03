mod env;

use axum::{
    extract::State,
    http::StatusCode,
    response::IntoResponse,
    routing::{get, post},
    Json, Router,
};
use serde::{Deserialize, Serialize};
use std::{net::SocketAddr, sync::Arc};
use tokio::sync::Mutex;
use tower_http::services::{ServeDir, ServeFile};

use crate::env::AppConfig;

#[derive(Clone)]
struct AppState {
    config: AppConfig,
    signaling: Arc<Mutex<SignalingStore>>,
}

#[derive(Default, Debug)]
struct SignalingStore {
    offer: Option<SessionDescription>,
    answer: Option<SessionDescription>,
    host_candidates: Vec<IceCandidate>,
    client_candidates: Vec<IceCandidate>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
struct SessionDescription {
    sdp: String,
    #[serde(rename = "type")]
    kind: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
struct IceCandidate {
    candidate: String,
    #[serde(rename = "sdpMid")]
    sdp_mid: Option<String>,
    #[serde(rename = "sdpMLineIndex")]
    sdp_mline_index: Option<u16>,
    #[serde(rename = "usernameFragment")]
    username_fragment: Option<String>,
}

#[derive(Debug, Serialize)]
struct WebRtcConfigResponse {
    #[serde(rename = "iceServers")]
    ice_servers: Vec<IceServerResponse>,
}

#[derive(Debug, Serialize)]
struct IceServerResponse {
    urls: Vec<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    username: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    credential: Option<String>,
}

#[derive(Debug, Serialize)]
struct PollCandidatesResponse {
    candidates: Vec<IceCandidate>,
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    println!("Starting VPS Signal Server");

    let config = AppConfig::load();

    let state = Arc::new(AppState {
        config: config.clone(),
        signaling: Arc::new(Mutex::new(SignalingStore::default())),
    });

    let app = Router::new()
        .route("/webrtc-config", get(get_webrtc_config))
        .route("/offer", get(get_offer).post(post_offer))
        .route("/answer", get(get_answer).post(post_answer))
        .route("/host-candidate", get(get_host_candidates).post(post_host_candidate))
        .route("/client-candidate", get(get_client_candidates).post(post_client_candidate))
        .route("/reset", post(reset_signaling))
        .fallback_service(
            ServeDir::new("web").fallback(ServeFile::new("web/index.html"))
        )
        .with_state(state);

    let addr: SocketAddr = config.host_bind.parse()?;
    let listener = tokio::net::TcpListener::bind(addr).await?;

    println!("listening on http://{}", addr);

    axum::serve(listener, app).await?;

    Ok(())
}

async fn get_webrtc_config(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    let response = WebRtcConfigResponse {
        ice_servers: vec![
            IceServerResponse {
                urls: vec![state.config.stun_url.clone()],
                username: None,
                credential: None,
            },
            IceServerResponse {
                urls: vec![state.config.turn_url.clone()],
                username: Some(state.config.turn_username.clone()),
                credential: Some(state.config.turn_password.clone()),
            },
        ],
    };

    Json(response)
}

async fn post_offer(
    State(state): State<Arc<AppState>>,
    Json(offer): Json<SessionDescription>,
) -> impl IntoResponse {
    let mut store = state.signaling.lock().await;
    store.offer = Some(offer);
    store.answer = None;
    store.host_candidates.clear();
    store.client_candidates.clear();

    StatusCode::OK
}

async fn get_offer(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    let store = state.signaling.lock().await;
    match &store.offer {
        Some(offer) => (StatusCode::OK, Json(offer.clone())).into_response(),
        None => StatusCode::NO_CONTENT.into_response(),
    }
}

async fn post_answer(
    State(state): State<Arc<AppState>>,
    Json(answer): Json<SessionDescription>,
) -> impl IntoResponse {
    let mut store = state.signaling.lock().await;
    store.answer = Some(answer);
    StatusCode::OK
}

async fn get_answer(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    let store = state.signaling.lock().await;
    match &store.answer {
        Some(answer) => (StatusCode::OK, Json(answer.clone())).into_response(),
        None => StatusCode::NO_CONTENT.into_response(),
    }
}

async fn post_host_candidate(
    State(state): State<Arc<AppState>>,
    Json(candidate): Json<IceCandidate>,
) -> impl IntoResponse {
    let mut store = state.signaling.lock().await;
    store.host_candidates.push(candidate);
    StatusCode::OK
}

async fn get_host_candidates(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    let mut store = state.signaling.lock().await;
    let candidates = std::mem::take(&mut store.host_candidates);

    (
        StatusCode::OK,
        Json(PollCandidatesResponse { candidates }),
    )
}

async fn post_client_candidate(
    State(state): State<Arc<AppState>>,
    Json(candidate): Json<IceCandidate>,
) -> impl IntoResponse {
    let mut store = state.signaling.lock().await;
    store.client_candidates.push(candidate);
    StatusCode::OK
}

async fn get_client_candidates(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    let mut store = state.signaling.lock().await;
    let candidates = std::mem::take(&mut store.client_candidates);

    (
        StatusCode::OK,
        Json(PollCandidatesResponse { candidates }),
    )
}

async fn reset_signaling(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    let mut store = state.signaling.lock().await;
    *store = SignalingStore::default();
    StatusCode::OK
}