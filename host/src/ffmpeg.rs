use std::io::{Read, Write, Result};
use std::time::Instant;
use std::sync::Arc;
use std::process::Stdio;
use tokio::io::{AsyncReadExt, AsyncWriteExt, AsyncBufReadExt, BufReader};
use tokio::process::{Command, Child, ChildStdin, ChildStdout};
use bytes::Bytes;

use webrtc::rtp::{packet::Packet, header::Header};
use webrtc::track::track_local::TrackLocalWriter;
use webrtc::track::track_local::track_local_static_rtp::TrackLocalStaticRTP;

use crate::webrtc_sender::WebRtcSender;
use crate::consts::{FPS, MTU};

pub struct FfmpegEncoder {
    _child: Child,
    pub stdin: ChildStdin,
    pub stdout: Option<ChildStdout>
}

impl FfmpegEncoder {
    pub fn new(width: u32, height: u32) -> Result<Self> {
        let mut child = Command::new("ffmpeg")
            .args([
                "-loglevel", "error",
                
                // input raw frames
                "-f", "rawvideo",
                "-pix_fmt", "bgra",
                "-video_size", &format!("{}x{}", width, height),
                "-framerate", &FPS.to_string(),
                "-i", "-",
                "-an",
                "-vf", "scale=1920:1080",

                // NVENC encoder
                "-c:v", "h264_nvenc",
                "-bsf:v", "h264_mp4toannexb",

                // low latency flags
                "-preset", "p1",
                "-rc", "cbr_ld_hq",
                "-b:v", "4M",
                "-maxrate", "4M",
                "-rtbufsize", "512k",
                "-r", "45",
                "-g", "60",
                "-bf", "0",
                "-tune", "ll",
                "-delay", "0",
                "-forced-idr", "1",
                "-no-scenecut", "1",
                "-zerolatency", "1",
                "-rc-lookahead", "0",
                "-spatial-aq", "0",
                "-temporal-aq", "0",
                "-fflags", "nobuffer",
                "-flush_packets", "1",
                "-flags", "low_delay",
                // "-vsync", "0",
                "-max_delay", "0",

                // output raw h264 stream
                "-f", "h264",
                "-"
            ])
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .spawn()
            .expect("Failed to start ffmpeg");
        
        let stdin = child.stdin.take().unwrap();
        let stdout = child.stdout.take();

        let stderr = child.stderr.take().unwrap();

        tokio::spawn(async move {
            let reader = BufReader::new(stderr);
            let mut lines = reader.lines();

            while let Ok(Some(line)) = lines.next_line().await {
                println!("ffmpeg: {}", line);
            }
        });
        
        Ok(Self {
            _child: child,
            stdin,
            stdout
        })
    }

    pub async fn encode(&mut self, frame: &[u8]) -> std::io::Result<()> {
        self.stdin.write_all(frame).await?;
        self.stdin.flush().await?;
        Ok(())
    }

    pub async fn send_nal(&mut self, webrtc_clone: Arc<WebRtcSender>) -> Result<()> {
        let mut stdout = self.stdout.take().unwrap();

        tokio::spawn(async move {
            let mut buffer = Vec::<u8>::new();
            let mut seq: u16 = 0;
            let mut timestamp: u32 = 0;
            let start = Instant::now();

            let mut sps: Option<Vec<u8>> = None;
            let mut pps: Option<Vec<u8>> = None;

            loop {
                let mut temp = [0u8; 65536];

                let size = match stdout.read(&mut temp).await {
                    Ok(s) => s,
                    Err(_) => break
                };

                if size == 0 {
                    continue;
                }

                buffer.extend_from_slice(&temp[..size]);

                while let Some(nal) = Self::extract_nal(&mut buffer) {
                    if nal.is_empty() {
                        continue;
                    }

                    let nal_type = nal[0] & 0x1F;
                    // println!("NAL type: {}", nal_type);

                    match nal_type {
                        12 => {
                            continue;
                        }
                        // ----------
                        // SPS
                        // ----------
                        7 => {
                            sps = Some(nal);
                            continue;
                        }

                        // ----------
                        // PPS
                        // ----------
                        8 => {
                            pps = Some(nal);
                            continue;
                        }

                        // ----------
                        // SEI SKIP
                        // ----------
                        6 => {
                            continue;
                        }

                        // ----------
                        // IDR FRAME
                        // ----------
                        5 => {
                            if let (Some(sps), Some(pps)) = (&sps, &pps) {
                                if let Err(e) = Self::send_stap_a(
                                    &webrtc_clone.video_track,
                                    sps,
                                    pps,
                                    &mut seq,
                                    timestamp
                                ).await {
                                    println!("rtp error {}", e);
                                };
                            }

                            if let Err(e) = Self::send_h264_rtp(
                                &webrtc_clone.video_track,
                                &nal,
                                &mut seq,
                                timestamp
                            ).await {
                                println!("rtp error {}", e);
                            };

                            // timestamp = timestamp.wrapping_add(90000 / FPS as u32);
                            timestamp = (start.elapsed().as_secs_f64() * 90000.0) as u32;

                            continue;
                        }

                        // ----------
                        // P FRAME
                        // ----------
                        1 => {
                            if let Err(e) = Self::send_h264_rtp(
                                &webrtc_clone.video_track,
                                &nal,
                                &mut seq,
                                timestamp
                            ).await {
                                println!("rtp error {}", e);
                            };

                            // timestamp = timestamp.wrapping_add(90000 / FPS as u32);
                            timestamp = (start.elapsed().as_secs_f64() * 90000.0) as u32;

                            continue;
                        }
                        _ => {}
                    }
                }
            }
        });

        Ok(())
    }

    async fn send_stap_a(
        track: &TrackLocalStaticRTP,
        sps: &[u8],
        pps: &[u8],
        seq: &mut u16,
        timestamp: u32
    ) -> anyhow::Result<()> {
        let mut payload = Vec::new();

        // STAP-A header
        let nri = sps[0] & 0x60;
        payload.push(nri | 24);

        payload.extend_from_slice(&(sps.len() as u16).to_be_bytes());
        payload.extend_from_slice(sps);

        payload.extend_from_slice(&(pps.len() as u16).to_be_bytes());
        payload.extend_from_slice(pps);

        let pkt = Packet {
            header: Header {
                version: 2,
                payload_type: 96,
                sequence_number: *seq,
                timestamp,
                ssrc: 1234,
                marker: false,
                ..Default::default()
            },
            payload: payload.into()
        };

        track.write_rtp(&pkt).await?;
        tokio::task::yield_now().await;

        *seq = seq.wrapping_add(1);

        Ok(())
    }

    fn extract_nal(buffer: &mut Vec<u8>) -> Option<Vec<u8>> {
        if buffer.len() < 4 {
            return None;
        }

        for i in 0..buffer.len() - 3 {
            if buffer[i..].starts_with(&[0, 0, 1]) {
                for j in (i + 3)..(buffer.len() - 3) {
                    if buffer[j..].starts_with(&[0, 0, 1]) ||
                       buffer[j..].starts_with(&[0, 0, 0, 1]) {
                        let nal = buffer[(i + 3)..j].to_vec();
                        buffer.drain(..j);
                        return Some(nal);
                    }
                }
            }

            if buffer[i..].starts_with(&[0, 0, 0, 1]) {
                for j in (i + 4)..(buffer.len() - 3) {
                    if buffer[j..].starts_with(&[0, 0, 1]) ||
                       buffer[j..].starts_with(&[0, 0, 0, 1]) {
                        let nal = buffer[(i + 4)..j].to_vec();
                        buffer.drain(..j);
                        return Some(nal);
                    }
                }
            }
        }

        None
    }

    async fn send_h264_rtp(
        track: &TrackLocalStaticRTP,
        nal: &[u8],
        seq: &mut u16,
        timestamp: u32
    ) -> anyhow::Result<()> {
        if nal.len() <= MTU {
            let pkt = Packet {
                // --- Single NAL Packet --- //
                header: Header {
                    version: 2,
                    payload_type: 96,
                    sequence_number: *seq,
                    timestamp,
                    ssrc: 1234,
                    marker: true,
                    ..Default::default()
                },
                payload: nal.to_vec().into()
            };

            track.write_rtp(&pkt).await?;

            tokio::task::yield_now().await;
            *seq = seq.wrapping_add(1);

            return Ok(());
        }

        // ---------------------------
        // FU-A Fragmentation
        // ---------------------------
        let nal_header = nal[0];
        let nal_type = nal_header & 0x1F;
        let nri = nal_header & 0x60;

        let fu_indicator = nri | 28;

        let mut offset = 1;
        let payload_size = MTU - 2;

        let mut first = true;

        while offset < nal.len() {
            let remaining = nal.len() - offset;
            let size = remaining.min(payload_size);

            let end = offset + size >= nal.len();

            let mut payload = Vec::with_capacity(size + 2);

            payload.push(fu_indicator);

            let mut fu_header = nal_type;

            if first {
                fu_header |= 0x80;
            }

            if end {
                fu_header |= 0x40;
            }

            payload.push(fu_header);

            payload.extend_from_slice(&nal[offset..(offset + size)]);

            let pkt = Packet {
                header: Header {
                    version: 2,
                    payload_type: 96,
                    sequence_number: *seq,
                    timestamp,
                    ssrc: 1234,
                    marker: end,
                    ..Default::default()
                },
                payload: payload.into()
            };

            track.write_rtp(&pkt).await?;
            tokio::task::yield_now().await;

            *seq = seq.wrapping_add(1);

            offset += size;
            first = false;
        }

        Ok(())
    }
}

impl Drop for FfmpegEncoder {
    fn drop(&mut self) {
        let _ = self._child.kill();
    }
}