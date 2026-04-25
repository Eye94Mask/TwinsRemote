use anyhow::{anyhow, Result};
use serde::{Deserialize, Serialize};
use std::env;

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct IceServer {
    #[serde(rename = "urls")]
    pub urls: Vec<String>,

    #[serde(rename = "username", default)]
    pub username: Option<String>,

    #[serde(rename = "credential", default)]
    pub credential: Option<String>
}

#[derive(Clone, Debug, Serialize, Deserialize)]
pub struct IceConfig {
    #[serde(rename = "iceServers")]
    pub ice_servers: Vec<IceServer>,

    #[serde(rename = "ttlSeconds", default)]
    pub ttl_seconds: Option<u64>
}

#[derive(Debug, Serialize)]
struct WebRtcConfigRequest<'a> {
    #[serde(rename = "sessionId")]
    session_id: &'a str,

    #[serde(rename = "token", skip_serializing_if = "Option::is_none")]
    token: Option<&'a str>
}

impl IceConfig {
    pub async fn fetch_from_server(session_id: &str) -> Result<Self> {
        let base_url = env::var("SIGNAL_BASE_URL")
            .map_err(|e| anyhow!("SIGNAL_BASE_URL is not set: {:?}", e))?;

        let token = env::var("WEBRTC_CONFIG_TOKEN").ok();

        let url = format!("{}/webrtc-config", base_url.trim_end_matches('/'));

        let client = reqwest::Client::builder()
            .use_rustls_tls()
            .build()
            .map_err(|e| anyhow!("failed to build reqwest client: {:?}", e))?;

        let req_body = WebRtcConfigRequest {
            session_id,
            token: token.as_deref(),
        };

        let resp = client
            .post(&url)
            .json(&req_body)
            .send()
            .await
            .map_err(|e| anyhow!("failed to fetch webrtc-config: {:?}", e))?;

        let status = resp.status();
        let body = resp
            .text()
            .await
            .map_err(|e| anyhow!("failed to read webrtc-config response body: {:?}", e))?;

        if !status.is_success() {
            return Err(anyhow!(
                "webrtc-config failed: status={} body={}",
                status,
                body
            ));
        }

        let cfg: IceConfig = serde_json::from_str(&body)
            .map_err(|e| anyhow!("invalid webrtc-config json: {:?}, body={}", e, body))?;

        if cfg.ice_servers.is_empty() {
            return Err(anyhow!("webrtc-config returned empty iceServers"));
        }

        Ok(cfg)
    }
}