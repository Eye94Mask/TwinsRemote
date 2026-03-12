use opus::{Encoder, Application};

pub struct AudioEncoder {
    encoder: Encoder
}

impl AudioEncoder {
    pub fn new() -> Self {
        let encoder = Encoder::new(
            48000,
            opus::Channels::Stereo,
            Application::LowDelay
        ).unwrap();

        Self { encoder }
    }

    pub fn encode(&mut self, pcm: &[i16]) -> Vec<u8> {
        let mut buf = vec![0u8; 4000];

        let size = self.encoder.encode(pcm, &mut buf).unwrap();
        buf.truncate(size);

        buf
    }
}