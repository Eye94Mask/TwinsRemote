use dotenvy::dotenv;
use std::env;

#[derive(Clone, Debug)]
pub struct AppConfig {
    pub host_bind: String,
    pub stun_url: String,
    pub turn_url: String,
    pub turn_username: String,
    pub turn_password: String,
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
            turn_username: env::var("TURN_USERNAME")
                .expect("TURN_USERNAME is not set"),
            turn_password: env::var("TURN_PASSWORD")
                .expect("TURN_PASSWORD is not set"),
        }
    }
}