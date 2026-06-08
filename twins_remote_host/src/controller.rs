use anyhow::{anyhow, Result};

use vigem_rust::{
    target::{Xbox360},
    Client, TargetHandle,
    X360Button, X360Notification, X360Report,
    Ds4Button, Ds4Notification, Ds4Report
};

use std::sync::mpsc;
use std::thread;
use serde::{Serialize, Deserialize};

use crate::consts::{
    SONY_VENDOR_ID, DS4_PRODUCT_ID, DSENSE_PRODUCT_ID,
    BUTTON_A, BUTTON_B, BUTTON_X, BUTTON_Y,
    BUTTON_UP, BUTTON_DOWN, BUTTON_LEFT, BUTTON_RIGHT,
    BUTTON_START, BUTTON_BACK, BUTTON_LS, BUTTON_RS, BUTTON_LB, BUTTON_RB, BUTTON_GUIDE
};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum VirtualPadType {
    Xbox360,
    DualShock4,
    DualSenceCompat
}

#[derive(Debug, Deserialize)]
pub struct GamepadState {
    pub buttons: u16,
    pub lt: u8,
    pub rt: u8,
    pub lx: i16,
    pub ly: i16,
    pub rx: i16,
    pub ry: i16
}

#[derive(Debug, Clone, Copy)]
pub struct RumbleState {
    pub large: u8,
    pub small: u8,
    pub led: u8
}

#[derive(Debug, Clone, Default)]
pub struct PadState {
    pub dpad_up:    bool,
    pub dpad_down:  bool,
    pub dpad_left:  bool,
    pub dpad_right: bool,

    pub south:  bool, // A / Cross
    pub east:   bool, // B / Circle
    pub west:   bool, // X / Square
    pub north:  bool, // Y / Triangle

    pub l1: bool,
    pub r1: bool,
    pub l2: u8,
    pub r2: u8,

    pub l3: bool,
    pub r3: bool,

    pub select: bool, // Back / Share
    pub start:  bool, // Start / Options
    pub guide:  bool, // Xbox / PS

    pub lx: i16,
    pub ly: i16,
    pub rx: i16,
    pub ry: i16
}

pub enum ControllerKind {
    Xbox(TargetHandle<Xbox360>)
}

pub struct Controller {
    pub client: Client,
    pub pad_type: VirtualPadType,
    pub kind: ControllerKind
}

impl Controller {
    pub fn new(
        pad_type: VirtualPadType,
        rumble_tx: std::sync::mpsc::Sender<RumbleState>
    ) -> Result<Self> {
        let client = Client::connect()
            .map_err(|e| anyhow!("failed to connect ViGEm client: {:?}", e))?;

        match pad_type {
            VirtualPadType::Xbox360 => {
                let gamepad = client
                    .new_x360_target()
                    .plugin()
                    .map_err(|e| anyhow!("failed to plugin X360 target: {:?}", e))?;

                gamepad
                    .wait_for_ready()
                    .map_err(|e| anyhow!("X360 target not ready: {:?}", e))?;

                let notification_rx = gamepad
                    .register_notification()
                    .map_err(|e| anyhow!("failed to register X360 notification: {:?}", e))?;

                println!("[HOST] X360 notification registered");

                std::thread::spawn(move || {
                    println!("[HOST] rumble notification thread started");

                    while let Ok(result) = notification_rx.recv() {
                        // println!("[HOST] raw ViGEm notification received: {:?}", result);

                        let Ok(n) = result else {
                            continue;
                        };

                        let _ = rumble_tx.send(RumbleState {
                            large: n.large_motor,
                            small: n.small_motor,
                            led: n.led_number,
                        });
                    }

                    eprintln!("[HOST] rumble notification thread ended");
                });

                Ok(Self {
                    client,
                    pad_type,
                    kind: ControllerKind::Xbox(gamepad),
                })
            }

            VirtualPadType::DualShock4 | VirtualPadType::DualSenceCompat => {
                Err(anyhow!("DS4/DualSense compat is not implemented yet with vigem-rust"))
            }
        }
    }

    pub fn register_rumble_channel(&mut self) -> Result<mpsc::Receiver<RumbleState>> {
        match &self.kind {
            ControllerKind::Xbox(gamepad) => {
                let notification_rx = gamepad
                    .register_notification()
                    .map_err(|e| anyhow!("failed to register X360 notification: {:?}", e))?;

                println!("[HOST] X360 notification registered");

                let (tx, rx) = mpsc::channel::<RumbleState>();

                thread::spawn(move || {
                    println!("[HOST] rumble notification thread started");

                    let mut last_large = 0u8;
                    let mut last_small = 0u8;
                    let mut last_led = 0u8;

                    while let Ok(result) = notification_rx.recv() {
                        // println!("[HOST] raw ViGEm notification received: {:?}", result);

                        let Ok(notification) = result else {
                            eprintln!("[HOST] ViGEm notification error: {:?}", result);
                            continue;
                        };

                        let X360Notification {
                            large_motor,
                            small_motor,
                            led_number,
                        } = notification;

                        if large_motor == last_large
                            && small_motor == last_small
                            && led_number == last_led
                        {
                            continue;
                        }

                        last_large = large_motor;
                        last_small = small_motor;
                        last_led = led_number;

                        let _ = tx.send(RumbleState {
                            large: large_motor,
                            small: small_motor,
                            led: led_number,
                        });
                    }

                    eprintln!("[HOST] rumble notification thread ended");
                });

                Ok(rx)
            }
        }
    }

    pub fn update(&mut self, state: GamepadState) -> Result<()> {
        let pad = sanitize_pad_state(state.into_pad_state());

        match &mut self.kind {
            ControllerKind::Xbox(gamepad) => {
                let mut report = X360Report::default();
                report = X360Report {
                    buttons: xbox_buttons_from_pad(&pad),
                    left_trigger: pad.l2,
                    right_trigger: pad.r2,
                    thumb_lx: pad.lx,
                    thumb_ly: pad.ly,
                    thumb_rx: pad.rx,
                    thumb_ry: pad.ry,
                };

                gamepad
                    .update(&report)
                    .map_err(|e| anyhow!("failed to update X360 report: {:?}", e))?;
            }
        }

        Ok(())
    }
}

impl GamepadState {
    pub fn into_pad_state(self) -> PadState {
        PadState {
            dpad_up:    has_button(self.buttons, BUTTON_UP),
            dpad_down:  has_button(self.buttons, BUTTON_DOWN),
            dpad_left:  has_button(self.buttons, BUTTON_LEFT),
            dpad_right: has_button(self.buttons, BUTTON_RIGHT),

            start:      has_button(self.buttons, BUTTON_START),
            select:     has_button(self.buttons, BUTTON_BACK),

            l3:         has_button(self.buttons, BUTTON_LS),
            r3:         has_button(self.buttons, BUTTON_RS),

            l1:         has_button(self.buttons, BUTTON_LB),
            r1:         has_button(self.buttons, BUTTON_RB),

            guide:      has_button(self.buttons, BUTTON_GUIDE),

            south:      has_button(self.buttons, BUTTON_A),
            east:       has_button(self.buttons, BUTTON_B),
            west:       has_button(self.buttons, BUTTON_X),
            north:      has_button(self.buttons, BUTTON_Y),

            l2: self.lt,
            r2: self.rt,
            lx: self.lx,
            ly: self.ly,
            rx: self.rx,
            ry: self.ry
        }
    }
}

fn has_button(raw: u16, mask: u16) -> bool {
    (raw & mask) != 0
}

fn apply_deadzone(v: i16, dz: i16) -> i16 {
    if v.abs() < dz { 0 }
    else { v }
}

fn normalize_trigger(v: u8, min_on: u8) -> u8 {
    if v < min_on { 0 }
    else { v }
}

fn sanitize_pad_state(mut s: PadState) -> PadState {
    s.lx = apply_deadzone(s.lx, 2500);
    s.ly = apply_deadzone(s.ly, 2500);
    s.rx = apply_deadzone(s.rx, 2500);
    s.ry = apply_deadzone(s.ry, 2500);

    s.l2 = normalize_trigger(s.l2, 8);
    s.r2 = normalize_trigger(s.r2, 8);

    s
}

fn xbox_buttons_from_pad(pad: &PadState) -> X360Button {
    let mut buttons = X360Button::empty();

    buttons.set(X360Button::DPAD_UP, pad.dpad_up);
    buttons.set(X360Button::DPAD_DOWN, pad.dpad_down);
    buttons.set(X360Button::DPAD_LEFT, pad.dpad_left);
    buttons.set(X360Button::DPAD_RIGHT, pad.dpad_right);

    buttons.set(X360Button::START, pad.start);
    buttons.set(X360Button::BACK, pad.select);

    buttons.set(X360Button::LEFT_THUMB, pad.l3);
    buttons.set(X360Button::RIGHT_THUMB, pad.r3);

    buttons.set(X360Button::LEFT_SHOULDER, pad.l1);
    buttons.set(X360Button::RIGHT_SHOULDER, pad.r1);

    buttons.set(X360Button::GUIDE, pad.guide);

    buttons.set(X360Button::A, pad.south);
    buttons.set(X360Button::B, pad.east);
    buttons.set(X360Button::X, pad.west);
    buttons.set(X360Button::Y, pad.north);

    buttons
}