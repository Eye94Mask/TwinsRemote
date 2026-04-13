use dotenvy::dotenv;
use std::env;

#[derive(Clone, Debug)]
pub struct AppConfig {
    pub host_bind: String,
    pub stun_url: String,
    pub turn_url: String,
    pub turn_shared_secret: String,
    pub turn_ttl_seconds: u64,
    pub allowed_origin: Option<String>
}

impl AppConfig {
    pub fn load() -> Self {
        dotenv().ok();

        Self {
            host_bind: env::var("HOST_BIND")
                .unwrap_or_else(|_| "0.0.0.0:8080".to_string()),
            stun_url: env::var("STUN_URL")
                .expect("STUN_URL is not set"),
            turn_url: env::var("TURN_URL")
                .expect("TURN_URL is not set"),
            turn_shared_secret: env::var("TURN_SHARED_SECRET")
                .expect("TURN_SHARED_SECRET is not set"),
            turn_ttl_seconds: env::var("TURN_TTL_SECONDS")
                .ok()
                .and_then(|v| v.parse::<u64>().ok())
                .unwrap_or(600),
            allowed_origin: env::var("ALLOWED_ORIGIN").ok()
        }
    }
}