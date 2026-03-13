pub const FPS_MILLIS: u64 = 16;     // 33 = 30fps, 16 = 60fps
pub const TIMEOUT_MILLIS: u32 = 16;
pub const LOOP_MILLIS: u64 = 33;    // For WebRTC

pub const FPS: u64 = 60;
pub const FRAME_DURATION_MS: u64 = 1000 / FPS;

pub const MTU: usize = 1200;
pub const AUDIO_FRAME: usize = 960 * 2;

pub const VIRTUAL_DESKTOP_COORD: i32 = 65535;

pub const MY_MONITOR_WIDTH:  u32 = 3840;
pub const MY_MONITOR_HEIGHT: u32 = 2160;

pub const FULL_HD_WIDTH:     u32 = 1920;
pub const FULL_HD_HEIGHT:    u32 = 1080;

// -------------------------------
// Input Types
// -------------------------------
pub const GAMEPAD: u8             = 0;
pub const KEYBOARD: u8            = 1;
pub const MOUSE_MOVE: u8          = 2;
pub const MOUSE_BUTTON: u8        = 3;
pub const MOUSE_WHEEL: u8         = 4;
pub const MOUSE_MOVE_ABSOLUTE: u8 = 5;

// -------------------------------
// Mouse Movement
// -------------------------------
pub const THRESHOLD_OF_MOUSE_MOVEMENT: i32    = 50;
pub const THRESHOLD_OF_MOUSE_TIME_MILLIS: u64 = 500;

// -------------------------------
// Controller Buttons
// -------------------------------
pub const BUTTON_A: u16     = 0x1000;
pub const BUTTON_B: u16     = 0x2000;
pub const BUTTON_X: u16     = 0x4000;
pub const BUTTON_Y: u16     = 0x8000;

pub const BUTTON_UP: u16    = 0x0001;
pub const BUTTON_DOWN: u16  = 0x0002;
pub const BUTTON_LEFT: u16  = 0x0004;
pub const BUTTON_RIGHT: u16 = 0x0008;

pub const BUTTON_START: u16 = 0x0010;
pub const BUTTON_BACK: u16  = 0x0020;
pub const BUTTON_LS: u16    = 0x0040;
pub const BUTTON_RS: u16    = 0x0080;
pub const BUTTON_LB: u16    = 0x0100;
pub const BUTTON_RB: u16    = 0x0200;
