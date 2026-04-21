use anyhow::Result;

use vigem_client::{Client, TargetId, DualShock4Wired, Xbox360Wired, XButtons, XGamepad};

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

#[derive(Deserialize)]
pub struct GamepadState {
    pub buttons: u16,
    pub lt: u8,
    pub rt: u8,
    pub lx: i16,
    pub ly: i16,
    pub rx: i16,
    pub ry: i16
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
    Xbox(Xbox360Wired<Client>),
    Ds4(DualShock4Wired<Client>)
}

pub struct Controller {
    pub pad_type: VirtualPadType,
    pub kind: ControllerKind
}

impl Controller {
    pub fn new(pad_type: VirtualPadType) -> Result<Self> {
        let client = Client::connect()?;

        let kind = match pad_type {
            VirtualPadType::Xbox360 => {
                let mut gamepad = Xbox360Wired::new(client, TargetId::XBOX360_WIRED);
                gamepad.plugin()?;
                gamepad.wait_ready()?;
                ControllerKind::Xbox(gamepad)
            }
            VirtualPadType::DualShock4 => {
                let mut gamepad = DualShock4Wired::new(
                    client,
                    TargetId {
                        vendor: SONY_VENDOR_ID,
                        product: DS4_PRODUCT_ID,
                    }
                );
                gamepad.plugin()?;
                gamepad.wait_ready()?;
                ControllerKind::Ds4(gamepad)
            }
            VirtualPadType::DualSenceCompat => {
                let mut gamepad = DualShock4Wired::new(
                    client,
                    TargetId {
                        vendor: SONY_VENDOR_ID,
                        product: DSENSE_PRODUCT_ID,
                    }
                );
                gamepad.plugin()?;
                gamepad.wait_ready()?;
                ControllerKind::Ds4(gamepad)
            }
        };

        Ok(Self { pad_type, kind })
    }

    pub fn update(&mut self, state: GamepadState) -> Result<()> {
        let pad = sanitize_pad_state(state.into_pad_state());

        match &mut self.kind {
            ControllerKind::Xbox(gamepad) => {
                let report = XGamepad {
                    buttons: xbox_buttons_from_pad(&pad),
                    left_trigger: pad.l2,
                    right_trigger: pad.r2,
                    thumb_lx: pad.lx,
                    thumb_ly: pad.ly,
                    thumb_rx: pad.rx,
                    thumb_ry: pad.ry
                };
                gamepad.update(&report)?;
            }
            ControllerKind::Ds4(_gamepad) => {

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

fn xbox_buttons_from_pad(pad: &PadState) -> XButtons {
    let mut raw = 0u16;

    if pad.dpad_up      { raw |= BUTTON_UP; }
    if pad.dpad_down    { raw |= BUTTON_DOWN; }
    if pad.dpad_left    { raw |= BUTTON_LEFT; }
    if pad.dpad_right   { raw |= BUTTON_RIGHT; }

    if pad.start        { raw |= BUTTON_START; }
    if pad.select       { raw |= BUTTON_BACK; }

    if pad.l3           { raw |= BUTTON_LS; }
    if pad.r3           { raw |= BUTTON_RS; }

    if pad.l1           { raw |= BUTTON_LB; }
    if pad.r1           { raw |= BUTTON_RB; }

    if pad.guide        { raw |= BUTTON_GUIDE; }

    if pad.south        { raw |= BUTTON_A; }
    if pad.east         { raw |= BUTTON_B; }
    if pad.west         { raw |= BUTTON_X; }
    if pad.north        { raw |= BUTTON_Y; }

    XButtons { raw }
}