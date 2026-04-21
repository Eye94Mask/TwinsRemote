use std::time::Duration;

// 60fps
pub const VIDEO_FRAME_DURATION: Duration = Duration::from_micros(16_667);

// PS Controller
pub const SONY_VENDOR_ID: u16    = 0x054C;
pub const DS4_PRODUCT_ID: u16    = 0x05C4;
pub const DSENSE_PRODUCT_ID: u16 = 0x09CC;

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
pub const BUTTON_GUIDE: u16 = 0x0400;

pub const BUTTON_A: u16     = 0x1000;
pub const BUTTON_B: u16     = 0x2000;
pub const BUTTON_X: u16     = 0x4000;
pub const BUTTON_Y: u16     = 0x8000;

