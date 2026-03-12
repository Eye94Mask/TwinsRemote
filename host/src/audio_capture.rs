use anyhow::Result;
use windows::{
    Win32::{
        Media::Audio::*,
        System::Com::*,
        Foundation::*
    },
    core::*
};
use std::ptr::null_mut;

pub struct AudioCapture {
    client: IAudioClient,
    capture: IAudioCaptureClient,
    channels: usize,
    sample_rate: u32
}

impl AudioCapture {
    pub fn new() -> Result<Self> {
        unsafe {
            CoInitializeEx(None, COINIT_MULTITHREADED).ok()?;

            let enumerator: IMMDeviceEnumerator =
                CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_ALL)?;

            let device =
                enumerator.GetDefaultAudioEndpoint(eRender, eConsole)?;

            let client: IAudioClient =
                device.Activate(CLSCTX_ALL, None)?;

            let format_ptr = client.GetMixFormat()?;
            let format = *format_ptr;

            let channels = format.nChannels as usize;
            let sample_rate = format.nSamplesPerSec;
            let bits = format.wBitsPerSample;
            println!(
                "Audio Format: {}ch {}Hz {}bit",
                channels,
                sample_rate,
                bits
            );

            client.Initialize(
                AUDCLNT_SHAREMODE_SHARED,
                AUDCLNT_STREAMFLAGS_LOOPBACK,
                0,
                0,
                format_ptr,
                None
            )?;

            let capture: IAudioCaptureClient = client.GetService()?;

            client.Start()?;

            Ok(Self {
                client,
                capture,
                channels,
                sample_rate
            })
        }
    }

    pub fn capture(&mut self) -> Option<Vec<i16>> {
        unsafe {
            let packet_length = match self.capture.GetNextPacketSize() {
                Ok(v) => v,
                Err(_) => return None
            };

            if packet_length == 0 {
                return None;
            }

            let mut data_ptr = null_mut();
            let mut frames = 0;
            let mut flags = 0;

            if self.capture.GetBuffer(
                &mut data_ptr,
                &mut frames,
                &mut flags,
                None,
                None
            ).is_err() {
                return None;
            }

            let total_samples = frames as usize * self.channels;
            let float_samples = std::slice::from_raw_parts(
                data_ptr as *const f32,
                total_samples
            );

            // float -> i16 + downsample
            let step = self.sample_rate / 48000;
            let mut pcm = Vec::with_capacity(total_samples / step as usize);

            let mut i = 0;

            // float32 -> i16
            while i < float_samples.len() {
                let s = float_samples[i];

                let v = (s * 32767.0).clamp(-32768.0, 32767.0) as i16;

                pcm.push(v);

                i += step as usize;
            }

            let _ = self.capture.ReleaseBuffer(frames).ok()?;

            Some(pcm)
        }
    }
}