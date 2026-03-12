use anyhow::{Result, anyhow};
use webrtc::ice::stats;
use windows::{
    core::*,
    Win32:: {
        Foundation::*,
        Graphics::{
            Direct3D::*,
            Direct3D11::*,
            Dxgi::*,
            Dxgi::Common::*
        }
    }
};

use crate::consts::TIMEOUT_MILLIS;

pub struct DxgiCapture {
    device: ID3D11Device,
    context:ID3D11DeviceContext,
    duplication: IDXGIOutputDuplication,
    width: u32,
    height: u32
}

impl DxgiCapture {
    pub fn new() -> Result<Self> {
        unsafe {
            // -------------------------------
            // Create D3D11 Device
            // -------------------------------
            let mut device: Option<ID3D11Device> = None;
            let mut context = None;

            D3D11CreateDevice(
                None,
                D3D_DRIVER_TYPE_HARDWARE,
                None,
                D3D11_CREATE_DEVICE_BGRA_SUPPORT,
                None,
                D3D11_SDK_VERSION,
                Some(&mut device as *mut _),
                None,
                Some(&mut context as *mut _),
            )?;
            let device = device.unwrap();
            let context = context.unwrap();

            let dxgi_device: IDXGIDevice = device.cast()?;
            let adapter = dxgi_device.GetAdapter()?;

            let output = adapter.EnumOutputs(0)?;
            let output1: IDXGIOutput1 = output.cast()?;

            let duplication = output1.DuplicateOutput(&device)?;

            let mut desc = DXGI_OUTDUPL_DESC::default();
            duplication.GetDesc(&mut desc);

            let width = desc.ModeDesc.Width;
            let height = desc.ModeDesc.Height;

            Ok(Self {
                device,
                context,
                duplication,
                width,
                height
            })
        }
    }
    
    pub fn capture_frame(&mut self) -> Result<Option<Vec<u8>>> {
        unsafe {
            let mut frame_info = DXGI_OUTDUPL_FRAME_INFO::default();
            let mut resource = None;

            match self.duplication.AcquireNextFrame(16, &mut frame_info, &mut resource) {
                Ok(_) => {}
                Err(e) => {
                    if e.code() == DXGI_ERROR_WAIT_TIMEOUT {
                        return Ok(None);
                    } 
                    else {
                        return Err(anyhow!("frame acquire failed"));
                    }
                }
            }

            let resource = resource.ok_or(anyhow!("No Resource"))?;
            let texture: ID3D11Texture2D = resource.cast()?;
            
            let mut desc = D3D11_TEXTURE2D_DESC::default();
            texture.GetDesc(&mut desc);

            desc.Usage = D3D11_USAGE_STAGING;
            desc.BindFlags = 0;
            desc.CPUAccessFlags = D3D11_CPU_ACCESS_READ.0 as u32;
            desc.MiscFlags = 0;

            let mut staging: Option<ID3D11Texture2D> = None;
            self.device.CreateTexture2D(&desc, None, Some(&mut staging))?;

            let staging = staging.unwrap();

            self.context.CopyResource(&staging, &texture);

            let mut mapped = D3D11_MAPPED_SUBRESOURCE::default();

            self.context.Map(
                &staging,
                0,
                D3D11_MAP_READ,
                0,
                Some(&mut mapped)
            )?;

            let row_pitch = mapped.RowPitch as usize;
            // My Desktop Capture Frame Format (BGRA: width x height x 4)
            let mut frame = vec![0u8; (self.width * self.height * 4) as usize];

            for y in 0..self.height as usize {
                let src = std::slice::from_raw_parts(
                    (mapped.pData as *const u8).add(y * row_pitch),
                    (self.width * 4) as usize
                );

                let dst_offset = y * (self.width * 4) as usize;

                frame[dst_offset..dst_offset + (self.width * 4) as usize].copy_from_slice(src);
            }

            self.context.Unmap(&staging, 0);

            self.duplication.ReleaseFrame()?;

            Ok(Some(frame))
        }
    }

    pub fn resolution(&self) -> (u32, u32) {
        (self.width, self.height)
    }
}