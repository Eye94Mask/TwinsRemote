use anyhow::{anyhow, Result};
use std::ptr::null_mut;
use std::sync::{Arc, Condvar, Mutex};

use windows::{
    core::{implement, Interface, HRESULT},
    Win32::{
        Foundation::{E_FAIL, E_POINTER},
        Media::Audio::*,
        System::{
            Com::*,
            Variant::{PROPVARIANT, VT_BLOB},
        },
    },
};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CaptureTarget {
    SystemMix,
    ProcessInclude(u32),
}

pub struct AudioCapture {
    client: IAudioClient,
    capture: IAudioCaptureClient,
    channels: usize,
    sample_rate: u32,
    bits_per_sample: u16,
}

#[implement(IActivateAudioInterfaceCompletionHandler)]
struct ActivateHandler {
    result: Arc<(Mutex<Option<windows::core::Result<IAudioClient>>>, Condvar)>,
}

#[allow(non_snake_case)]
impl IActivateAudioInterfaceCompletionHandler_Impl for ActivateHandler_Impl {
    fn ActivateCompleted(
        &self,
        operation: windows::core::Ref<'_, IActivateAudioInterfaceAsyncOperation>,
    ) -> windows::core::Result<()> {
        let r = (|| -> windows::core::Result<IAudioClient> {
            let operation = operation.ok_or_else(|| windows::core::Error::from(E_POINTER))?;

            let mut activate_hr = HRESULT(0);
            let mut activated_interface = None;

            unsafe {
                operation.GetActivateResult(&mut activate_hr, &mut activated_interface)?;
            }

            activate_hr.ok()?;

            let unk = activated_interface.ok_or_else(|| windows::core::Error::from(E_FAIL))?;
            unk.cast::<IAudioClient>()
        })();

        let (lock, cv) = &*self.result;
        *lock.lock().unwrap() = Some(r);
        cv.notify_one();

        Ok(())
    }
}

impl AudioCapture {
    pub fn new(target: CaptureTarget) -> Result<Self> {
        match target {
            CaptureTarget::SystemMix => Self::new_system_mix(),
            CaptureTarget::ProcessInclude(pid) => Self::new_process_include(pid),
        }
    }

    fn new_system_mix() -> Result<Self> {
        unsafe {
            let enumerator: IMMDeviceEnumerator =
                CoCreateInstance(&MMDeviceEnumerator, None, CLSCTX_ALL)?;

            let device = enumerator.GetDefaultAudioEndpoint(eRender, eConsole)?;
            let client: IAudioClient = device.Activate(CLSCTX_ALL, None)?;

            let format_ptr = client.GetMixFormat()?;
            let format = *format_ptr;

            let channels = format.nChannels as usize;
            let sample_rate = format.nSamplesPerSec;
            let bits_per_sample = format.wBitsPerSample;

            println!(
                "[AUDIO] SystemMix format: {}ch {}Hz {}bit",
                channels, sample_rate, bits_per_sample
            );

            client.Initialize(
                AUDCLNT_SHAREMODE_SHARED,
                AUDCLNT_STREAMFLAGS_LOOPBACK,
                0,
                0,
                format_ptr,
                None,
            )?;

            let capture: IAudioCaptureClient = client.GetService()?;
            client.Start()?;

            Ok(Self {
                client,
                capture,
                channels,
                sample_rate,
                bits_per_sample,
            })
        }
    }

    fn new_process_include(pid: u32) -> Result<Self> {
        unsafe {
            let result_slot: Arc<(Mutex<Option<windows::core::Result<IAudioClient>>>, Condvar)> =
                Arc::new((Mutex::new(None), Condvar::new()));

            let handler = ActivateHandler {
                result: result_slot.clone(),
            };

            let process_params = AUDIOCLIENT_PROCESS_LOOPBACK_PARAMS {
                TargetProcessId: pid,
                ProcessLoopbackMode: PROCESS_LOOPBACK_MODE_INCLUDE_TARGET_PROCESS_TREE,
            };

            let mut activation_params = AUDIOCLIENT_ACTIVATION_PARAMS {
                ActivationType: AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK,
                Anonymous: AUDIOCLIENT_ACTIVATION_PARAMS_0 {
                    ProcessLoopbackParams: process_params,
                },
            };

            let mut prop = PROPVARIANT::default();

            // windows crate の版によって Anonymous の段数が微妙に違うことがあります
            // ここで field 名エラーが出たら、その1〜2行だけ調整してください
            prop.Anonymous.Anonymous.vt = VT_BLOB.0 as u16;
            prop.Anonymous.Anonymous.Anonymous.blob.cbSize =
                std::mem::size_of::<AUDIOCLIENT_ACTIVATION_PARAMS>() as u32;
            prop.Anonymous.Anonymous.Anonymous.blob.pBlobData =
                (&mut activation_params as *mut AUDIOCLIENT_ACTIVATION_PARAMS).cast::<u8>();

            let _op = ActivateAudioInterfaceAsync(
                VIRTUAL_AUDIO_DEVICE_PROCESS_LOOPBACK,
                &IAudioClient::IID,
                Some(&prop),
                &handler,
            )?;

            let (lock, cv) = &*result_slot;
            let mut guard = lock.lock().unwrap();
            while guard.is_none() {
                guard = cv.wait(guard).unwrap();
            }

            let client = guard.take().unwrap()?;

            // process loopback 側は 48kHz / stereo / 16-bit に固定して扱う
            // これなら今の Opus 48kHz 系とつなぎやすい
            let format = WAVEFORMATEX {
                wFormatTag: WAVE_FORMAT_PCM as u16,
                nChannels: 2,
                nSamplesPerSec: 48_000,
                wBitsPerSample: 16,
                nBlockAlign: 4,      // 2ch * 16bit / 8
                nAvgBytesPerSec: 48_000 * 4,
                cbSize: 0,
            };

            client.Initialize(
                AUDCLNT_SHAREMODE_SHARED,
                AUDCLNT_STREAMFLAGS_LOOPBACK | AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM,
                0,
                0,
                &format,
                None,
            )?;

            let capture: IAudioCaptureClient = client.GetService()?;
            client.Start()?;

            println!("[AUDIO] ProcessInclude pid={} format: 2ch 48000Hz 16bit", pid);

            Ok(Self {
                client,
                capture,
                channels: 2,
                sample_rate: 48_000,
                bits_per_sample: 16,
            })
        }
    }

    pub fn capture(&mut self) -> Option<Vec<i16>> {
        unsafe {
            let packet_length = match self.capture.GetNextPacketSize() {
                Ok(v) => v,
                Err(_) => return None,
            };

            if packet_length == 0 {
                return None;
            }

            let mut data_ptr = null_mut();
            let mut frames = 0u32;
            let mut flags = 0u32;

            if self
                .capture
                .GetBuffer(&mut data_ptr, &mut frames, &mut flags, None, None)
                .is_err()
            {
                return None;
            }

            let total_samples = frames as usize * self.channels;

            let pcm = if self.bits_per_sample == 16 {
                let src = std::slice::from_raw_parts(data_ptr as *const i16, total_samples);
                src.to_vec()
            } else {
                let src = std::slice::from_raw_parts(data_ptr as *const f32, total_samples);
                let step = (self.sample_rate / 48_000).max(1) as usize;
                let mut out = Vec::with_capacity(total_samples / step.max(1));

                let mut i = 0usize;
                while i < src.len() {
                    let v = (src[i] * 32767.0).clamp(-32768.0, 32767.0) as i16;
                    out.push(v);
                    i += step;
                }
                out
            };

            let _ = self.capture.ReleaseBuffer(frames).ok()?;
            Some(pcm)
        }
    }

    pub fn stop(&self) {
        unsafe {
            let _ = self.client.Stop();
        }
    }
}

impl Drop for AudioCapture {
    fn drop(&mut self) {
        self.stop();
    }
}