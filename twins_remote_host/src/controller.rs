use anyhow::Result;

use vigem_client::{Client, TargetId, Xbox360Wired, XButtons, XGamepad};

use serde::{Serialize, Deserialize};

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

pub struct Controller {
    pub gamepad: Xbox360Wired<Client>
}

impl Controller {
    pub fn new() -> Result<Self> {
        let client = Client::connect()?;
        let mut gamepad = Xbox360Wired::new(client, TargetId::XBOX360_WIRED);
        gamepad.plugin()?;

        Ok(Self { gamepad })
    }

    pub fn update(&mut self, state: GamepadState) {
        let report = XGamepad {
            buttons: XButtons { raw: state.buttons },
            left_trigger: state.lt,
            right_trigger: state.rt,
            thumb_lx: state.lx,
            thumb_ly: state.ly,
            thumb_rx: state.rx,
            thumb_ry: state.ry,
        };

        let _ = self.gamepad.update(&report);
    }
}