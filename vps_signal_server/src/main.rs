mod env;

use axum::{
    extract::{Query, State},
    http::{HeaderMap, HeaderValue, StatusCode},
    response::{Html, IntoResponse, Response},
    routing::{get, post},
    Json, Router
};
use base64::{engine::general_purpose::STANDARD, Engine as _};
use hmac::{Hmac, Mac, KeyInit};
use rand::{distr::Alphanumeric, RngExt};
use serde::{Deserialize, Serialize};
use sha1::Sha1;
use std::{
    collections::HashMap,
    net::SocketAddr,
    path::Path,
    sync::Arc,
    time::{SystemTime, UNIX_EPOCH}
};
use tokio::sync::Mutex;
use tower_http::services::ServeDir;

use crate::env::AppConfig;

type HmacSha1 = Hmac<Sha1>;

#[derive(Clone)]
struct AppState {
    config: AppConfig,
    sessions: Arc<Mutex<HashMap<String, SessionState>>>,
    web_tokens: Arc<Mutex<HashMap<String, u64>>>
}

#[derive(Debug, Default)]
struct SessionState {
    offer: Option<SessionDescription>,
    answer: Option<SessionDescription>,
    host_candidates: Vec<IceCandidate>,
    client_candidates: Vec<IceCandidate>,
    updated_at: u64
}

#[derive(Debug, Clone, Serialize, Deserialize)]
struct SessionDescription {
    sdp: String,
    #[serde(rename = "type")]
    kind: String
}

#[derive(Debug, Clone, Serialize, Deserialize)]
struct IceCandidate {
    candidate: String,
    #[serde(rename = "sdpMid")]
    sdp_mid: Option<String>,
    #[serde(rename = "sdpMLineIndex")]
    sdp_mline_index: Option<u16>,
    #[serde(rename = "usernameFragment")]
    username_fragment: Option<String>
}

#[derive(Debug, Serialize)]
struct WebRtcConfigResponse {
    #[serde(rename = "iceServers")]
    ice_servers: Vec<IceServerResponse>,
    #[serde(rename = "ttlSeconds")]
    ttl_seconds: u64
}

#[derive(Debug, Serialize)]
struct IceServerResponse {
    urls: Vec<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    username: Option<String>,
    #[serde(skip_serializing_if = "Option::is_none")]
    credential: Option<String>
}

#[derive(Debug, Serialize)]
struct PollCandidatesResponse {
    candidates: Vec<IceCandidate>
}

#[derive(Debug, Deserialize)]
struct WebRtcConfigRequest {
    token: String
}

#[derive(Debug, Serialize)]
struct TtlSecondsResponse {
    #[serde(rename = "ttlSeconds")]
    ttl_seconds: u64
}

#[derive(Debug, Deserialize)]
struct SessionQuery {
    #[serde(rename = "sessionId")]
    session_id: String
}

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    println!("Starting VPS Signal Server");

    let config = AppConfig::load();

    let state = Arc::new(AppState {
        config: config.clone(),
        sessions: Arc::new(Mutex::new(HashMap::new())),
        web_tokens: Arc::new(Mutex::new(HashMap::new()))
    });

    let app = Router::new()
        .route("/", get(get_index))
        .route("/index.html", get(get_index))
        .route("/webrtc-config", post(post_webrtc_config))
        .route("/offer", get(get_offer).post(post_offer))
        .route("/answer", get(get_answer).post(post_answer))
        .route("/host-candidate", get(get_host_candidates).post(post_host_candidate))
        .route("/client-candidate", get(get_client_candidates).post(post_client_candidate))
        .route("/ttl-seconds", get(get_ttl_seconds))
        .route("/reset", post(reset_signaling))
        .fallback_service(ServeDir::new("web"))
        .with_state(state);

    let addr: SocketAddr = config.host_bind.parse()?;
    let listener = tokio::net::TcpListener::bind(addr).await?;

    println!("listening on http://{}", addr);

    axum::serve(listener, app).await?;

    Ok(())
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
    user_id: &str
) -> anyhow::Result<(String, String)> {
    let expiry = current_unix_time() + ttl_seconds;
    let username = format!("{}:{}", expiry, user_id);

    let mut mac = HmacSha1::new_from_slice(secret.as_bytes())?;
    mac.update(username.as_bytes());
    let result = mac.finalize().into_bytes();
    let credential = STANDARD.encode(result);

    Ok((username, credential))
}

fn normalize_origin_like(value: &str) -> String {
    value.trim_end_matches('/').to_string()
}

fn origin_allowed(headers: &HeaderMap, allowed_origin: &Option<String>) -> bool {
    let Some(allowed) = allowed_origin.as_ref() else {
        return true;
    };

    let allowed = normalize_origin_like(allowed);

    if let Some(origin) = headers.get("origin").and_then(|v| v.to_str().ok()) {
        if normalize_origin_like(origin) == allowed {
            return true;
        }
    }

    if let Some(referer) = headers.get("referer").and_then(|v| v.to_str().ok()) {
        if referer == allowed || referer.starts_with(&(allowed.clone() + "/")) {
            return true;
        }
    }

    false
}

fn make_random_token(len: usize) -> String {
    rand::rng()
        .sample_iter(&Alphanumeric)
        .take(len)
        .map(char::from)
        .collect()
}

fn cleanup_expired_tokens(tokens: &mut HashMap<String, u64>) {
    let now = current_unix_time();
    tokens.retain(|_, expires_at| *expires_at > now);
}

async fn issue_web_token(state: &Arc<AppState>, ttl_seconds: u64) -> String {
    let token = make_random_token(48);
    let expires_at = current_unix_time() + ttl_seconds;

    let mut tokens = state.web_tokens.lock().await;
    cleanup_expired_tokens(&mut tokens);
    tokens.insert(token.clone(), expires_at);

    token
}

async fn consume_web_token(state: &Arc<AppState>, token: &str) -> bool {
    let mut tokens = state.web_tokens.lock().await;
    cleanup_expired_tokens(&mut tokens);

    match tokens.get(token).copied() {
        Some(expires_at) if expires_at > current_unix_time() => {
            tokens.remove(token);
            true
        }
        _ => false
    }
}

fn touch_session(session: &mut SessionState) {
    session.updated_at = current_unix_time();
}

fn cleanup_expired_sessions(
    sessions: &mut HashMap<String, SessionState>,
    session_ttl_seconds: u64
) {
    let now = current_unix_time();
    sessions.retain(|_, s| now.saturating_sub(s.updated_at) <= session_ttl_seconds);
}

async fn get_index(State(state): State<Arc<AppState>>) -> Response {
    let path = Path::new("web/index.html");

    let html = match tokio::fs::read_to_string(path).await {
        Ok(v) => v,
        Err(e) => {
            eprintln!("failed to read index.html: {e}");
            return (
                StatusCode::INTERNAL_SERVER_ERROR,
                "failed to load index.html"
            )
                .into_response();
        }
    };

    let token = issue_web_token(&state, 300).await;

    let injected = html.replace(
        "</body>",
        &format!(
            r#"<script>
window.__WEBRTC_CONFIG_TOKEN__ = "{}";
</script>
</body>"#,
            token
        )
    );

    let final_html = if injected == html {
        format!(
            r#"{html}
<script>
window.__WEBRTC_CONFIG_TOKEN__ = "{token}";
</script>
"#
        )
    } else {
        injected
    };

    let mut response = Html(final_html).into_response();
    response.headers_mut().insert(
        axum::http::header::CACHE_CONTROL,
        HeaderValue::from_static("no-store")
    );
    response
}

async fn post_webrtc_config(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    Json(req): Json<WebRtcConfigRequest>
) -> Response {
    if !origin_allowed(&headers, &state.config.allowed_origin) {
        return (
            StatusCode::FORBIDDEN,
            [("cache-control", "no-store")],
            "forbidden"
        )
            .into_response();
    }

    if !consume_web_token(&state, &req.token).await {
        return (
            StatusCode::FORBIDDEN,
            [("cache-control", "no-store")],
            "invalid token"
        )
            .into_response();
    }

    let ttl = state.config.turn_ttl_seconds;
    let user_id = "guest";

    let (turn_username, turn_credential) = match create_turn_credentials(
        &state.config.turn_shared_secret,
        ttl,
        user_id
    ) {
        Ok(v) => v,
        Err(e) => {
            eprintln!("failed to create TURN credentials: {e}");
            return (
                StatusCode::INTERNAL_SERVER_ERROR,
                [("cache-control", "no-store")],
                "failed to create TURN credentials"
            )
                .into_response();
        }
    };

    let response = WebRtcConfigResponse {
        ice_servers: vec![
            IceServerResponse {
                urls: vec![state.config.stun_url.clone()],
                username: None,
                credential: None
            },
            IceServerResponse {
                urls: vec![state.config.turn_url.clone()],
                username: Some(turn_username),
                credential: Some(turn_credential)
            },
        ],
        ttl_seconds: ttl
    };

    (
        StatusCode::OK,
        [
            ("cache-control", "no-store"),
            ("pragma", "no-cache")
        ],
        Json(response)
    )
        .into_response()
}

async fn post_offer(
    State(state): State<Arc<AppState>>,
    Query(query): Query<SessionQuery>,
    Json(offer): Json<SessionDescription>
) -> impl IntoResponse {
    let mut sessions = state.sessions.lock().await;
    cleanup_expired_sessions(&mut sessions, state.config.session_ttl_seconds);

    let session = sessions.entry(query.session_id).or_default();
    session.offer = Some(offer);
    session.answer = None;
    session.host_candidates.clear();
    session.client_candidates.clear();
    touch_session(session);

    StatusCode::OK
}

async fn get_offer(
    State(state): State<Arc<AppState>>,
    Query(query): Query<SessionQuery>
) -> impl IntoResponse {
    let mut sessions = state.sessions.lock().await;
    cleanup_expired_sessions(&mut sessions, state.config.session_ttl_seconds);

    match sessions.get_mut(&query.session_id) {
        Some(session) => {
            touch_session(session);
            match &session.offer {
                Some(offer) => (StatusCode::OK, Json(offer.clone())).into_response(),
                None => StatusCode::NO_CONTENT.into_response()
            }
        }
        None => StatusCode::NO_CONTENT.into_response()
    }
}

async fn post_answer(
    State(state): State<Arc<AppState>>,
    Query(query): Query<SessionQuery>,
    Json(answer): Json<SessionDescription>
) -> impl IntoResponse {
    let mut sessions = state.sessions.lock().await;
    cleanup_expired_sessions(&mut sessions, state.config.session_ttl_seconds);

    let session = sessions.entry(query.session_id).or_default();
    session.answer = Some(answer);
    touch_session(session);

    StatusCode::OK
}

async fn get_answer(
    State(state): State<Arc<AppState>>,
    Query(query): Query<SessionQuery>
) -> impl IntoResponse {
    let mut sessions = state.sessions.lock().await;
    cleanup_expired_sessions(&mut sessions, state.config.session_ttl_seconds);

    match sessions.get_mut(&query.session_id) {
        Some(session) => {
            touch_session(session);
            match &session.answer {
                Some(answer) => (StatusCode::OK, Json(answer.clone())).into_response(),
                None => StatusCode::NO_CONTENT.into_response()
            }
        }
        None => StatusCode::NO_CONTENT.into_response()
    }
}

async fn post_host_candidate(
    State(state): State<Arc<AppState>>,
    Query(query): Query<SessionQuery>,
    Json(candidate): Json<IceCandidate>
) -> impl IntoResponse {
    let mut sessions = state.sessions.lock().await;
    cleanup_expired_sessions(&mut sessions, state.config.session_ttl_seconds);

    let session = sessions.entry(query.session_id).or_default();
    session.host_candidates.push(candidate);
    touch_session(session);

    StatusCode::OK
}

async fn get_host_candidates(
    State(state): State<Arc<AppState>>,
    Query(query): Query<SessionQuery>
) -> impl IntoResponse {
    let mut sessions = state.sessions.lock().await;
    cleanup_expired_sessions(&mut sessions, state.config.session_ttl_seconds);

    match sessions.get_mut(&query.session_id) {
        Some(session) => {
            touch_session(session);
            let candidates = std::mem::take(&mut session.host_candidates);
            (
                StatusCode::OK,
                Json(PollCandidatesResponse { candidates })
            )
                .into_response()
        }
        None => (
            StatusCode::OK,
            Json(PollCandidatesResponse {
                candidates: Vec::new()
            }),
        )
            .into_response()
    }
}

async fn post_client_candidate(
    State(state): State<Arc<AppState>>,
    Query(query): Query<SessionQuery>,
    Json(candidate): Json<IceCandidate>
) -> impl IntoResponse {
    let mut sessions = state.sessions.lock().await;
    cleanup_expired_sessions(&mut sessions, state.config.session_ttl_seconds);

    let session = sessions.entry(query.session_id).or_default();
    session.client_candidates.push(candidate);
    touch_session(session);

    StatusCode::OK
}

async fn get_client_candidates(
    State(state): State<Arc<AppState>>,
    Query(query): Query<SessionQuery>
) -> impl IntoResponse {
    let mut sessions = state.sessions.lock().await;
    cleanup_expired_sessions(&mut sessions, state.config.session_ttl_seconds);

    match sessions.get_mut(&query.session_id) {
        Some(session) => {
            touch_session(session);
            let candidates = std::mem::take(&mut session.client_candidates);
            (
                StatusCode::OK,
                Json(PollCandidatesResponse { candidates })
            )
                .into_response()
        }
        None => (
            StatusCode::OK,
            Json(PollCandidatesResponse {
                candidates: Vec::new()
            }),
        )
            .into_response()
    }
}

async fn get_ttl_seconds(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    Json(TtlSecondsResponse {
        ttl_seconds: state.config.turn_ttl_seconds
    })
}

async fn reset_signaling(
    State(state): State<Arc<AppState>>,
    Query(query): Query<SessionQuery>
) -> impl IntoResponse {
    let mut sessions = state.sessions.lock().await;
    sessions.remove(&query.session_id);
    StatusCode::OK
}