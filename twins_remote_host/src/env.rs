use dotenvy::dotenv;
use std::env;

#[derive(Clone, Debug)]
pub struct IceConfig {
    pub stun_url: String,
    pub turn_url: String,
    pub turn_shared_secret: String,
    pub turn_ttl_seconds: u64,
    pub turn_user_id: String,
    pub signal_base_url: String
}

impl IceConfig {
    pub fn load() -> Self {
        dotenv().ok();

        Self {
            stun_url: env::var("STUN_URL").expect("STUN_URL is not set"),
            turn_url: env::var("TURN_URL").expect("TURN_URL is not set"),
            turn_shared_secret: env::var("TURN_SHARED_SECRET").expect("TURN_SHARED_SECRET is not set"),
            turn_ttl_seconds: env::var("TURN_TTL_SECONDS").ok().and_then(|v| v.parse::<u64>().ok()).unwrap_or(600),
            turn_user_id: env::var("TURN_USER_ID").unwrap_or_else(|_| "host".to_string()),
            signal_base_url: env::var("SIGNAL_BASE_URL").expect("SIGNAL_BASE_URL is not set")
        }
    }
}