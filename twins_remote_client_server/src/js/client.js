let pc;
let dc;
let inputDc = null;
let gamepadIndex = null;

let remoteAudioStream = new MediaStream();

let statsIntervalId = null;
let rtcSummaryIntervalId = null;
let videoStatsMonitorAbort = null;
let renderStatsIntervalId = null;

let audioEl = null;
let videoEl = null;
let renderCanvas = null;
let renderCtx = null;

// latest-frame rendering
let videoProcessor = null;
let videoReader = null;
let renderLoopStarted = false;
let processorStopped = false;

let latestFrame = null;
let lastGoodFrame = null;

let receivedFrames = 0;
let droppedFrames = 0;
let renderedFrames = 0;

let lastFrameArrivedAt = 0;
let lastRenderedAt = 0;

let videoDecoder = null;

let lastDecodedAt = 0;
let lastRenderedTs = null;

let lastPacketsLost = 0;
let lastNackCount = 0;
let lastFreezeCount = 0;
let lastPacketsDiscarded = 0;

let forceKeyframeCooldownUntil = 0;
const FORCE_KEYFRAME_COOLDOWN_MS = 5000;

window.addEventListener("gamepadconnected", (e) => {
    gamepadIndex = e.gamepad.index;
    console.log("gamepad connected:", e.gamepad.id, "index=", gamepadIndex);
});

window.addEventListener("gamepaddisconnected", (e) => {
    console.log("gamepad disconnected:", e.gamepad.id, "index=", e.gamepad.index);
    if (gamepadIndex === e.gamepad.index) {
        gamepadIndex = null;
    }
});

window.addEventListener("DOMContentLoaded", () => {
    const videoEl = document.getElementById("video");
    const fullscreenBtn = document.getElementById("fullscreenBtn");
    const audioBtn = document.getElementById("audioBtn");
    const player = document.getElementById("player");

    if (videoEl) { videoEl.style.display = "none"; }

    renderCanvas = document.createElement("canvas");
    renderCanvas.id = "renderCanvas";
    renderCanvas.style.width = "100%";
    renderCanvas.style.height = "100%";
    renderCanvas.style.display = "block";
    renderCanvas.style.background = "black";
    player.appendChild(renderCanvas);

    renderCtx = renderCanvas.getContext("2d", {
        alpha: false,
        desynchronized: true,
        willReadFrequently: false
    });

    renderCanvas.addEventListener("dblclick", () => {
        toggleFullscreen();
    });

    fullscreenBtn.addEventListener("click", () => {
        toggleFullscreen();
    });

    audioBtn.addEventListener("click", () => {
        onEnableAudio();
    });

    audioEl = document.createElement("audio");
    audioEl.autoplay = true;
    audioEl.playsInline = true;
    audioEl.controls = false;
    audioEl.style.display = "none";
    document.body.appendChild(audioEl);
});


function nowMs() {
    return performance.now();
}

function log(...args) {
    console.log(...args);
    const el = document.getElementById("log");
    if (!el) return;
    el.textContent += args.map(v => {
        if (typeof v === "string") return v;
        try {
            return JSON.stringify(v);
        } catch {
            return String(v);
        }
    }).join(" ") + "\n";

    el.scrollTop = el.scrollHeight;
}

function closeFrameSafe(frame) {
    if (!frame) return;
    try {
        frame.close();
    } catch (_) {}
}

function sendForceKeyframe(reason) {
    const now = nowMs();
    if (!inputDc || inputDc.readyState !== "open") return;
    if (now < forceKeyframeCooldownUntil) return;

    forceKeyframeCooldownUntil = now + FORCE_KEYFRAME_COOLDOWN_MS;

    const msg = JSON.stringify({
        type: "force_keyframe",
        reason,
        ts: Date.now(),
    });

    try {
        inputDc.send(msg);
        log("[CLIENT] sent force_keyframe:", reason);
    } catch (e) {
        console.log("[CLIENT] force_keyframe send failed", e);
    }
}

function replaceFrame(slotName, newFrame) {
    if (slotName === "latest") {
        const old = latestFrame;
        latestFrame = newFrame;
        closeFrameSafe(old);
        return;
    }

    if (slotName === "lastGood") {
        const old = lastGoodFrame;
        lastGoodFrame = newFrame;
        closeFrameSafe(old);
    }
}

async function fetchWebRtcConfig() {
    const res = await fetch("/webrtc-config", {
        method: "GET",
        cache: "no-store",
    });

    if (!res.ok) {
        throw new Error(`failed to fetch /webrtc-config: ${res.status}`);
    }

    return await res.json();
}

//----------------------------------------------------------------------------------------------------
// Keep rendering lastGoodFrame if client do not have the latest frame
//----------------------------------------------------------------------------------------------------
function startRenderLoop(canvas) {
    if (renderLoopStarted) return;
    renderLoopStarted = true;

    function render() {
        // Give priority to a new frame
        const frameToDraw = latestFrame || lastGoodFrame;

        if (frameToDraw && renderCanvas && renderCtx) {
            try {
                if (
                    renderCanvas.width  !== frameToDraw.displayWidth ||
                    renderCanvas.height !== frameToDraw.displayHeight
                ) {
                    renderCanvas.width  = frameToDraw.displayWidth;
                    renderCanvas.height = frameToDraw.displayHeight;
                }

                renderCtx.drawImage(
                    frameToDraw,
                    0,
                    0,
                    renderCanvas.width,
                    renderCanvas.height
                );

                renderedFrames++;
                lastRenderedAt = nowMs();
            } catch (e) {
                console.error("[RENDER ERROR]", e);
            }

            if (latestFrame) {
                closeFrameSafe(latestFrame);
                latestFrame = null;
            }
        }

        const sinceFrameArrived = lastFrameArrivedAt > 0 ? nowMs() - lastFrameArrivedAt : 0;
        if (lastFrameArrivedAt > 0 && sinceFrameArrived > 1500) {
            sendForceKeyframe(`frame_stall_${Math.floor(sinceFrameArrived)}ms`);
        }

        requestAnimationFrame(render);
    }

    requestAnimationFrame(render);
}

function startRenderStatsLog() {
    if (renderStatsIntervalId) clearInterval(renderStatsIntervalId);

    renderStatsIntervalId = setInterval(() => {
        log("[CLIENT RENDER", {
            receivedFrames,
            droppedFrames,
            renderedFrames,
            hasLatestFrame: !!latestFrame,
            hasLastGoodFrame: !!lastGoodFrame
        });

        receivedFrames = 0;
        droppedFrames  = 0;
        renderedFrames = 0;
    }, 1000);
}

//--------------------------------------------------
// WebRTC stats monitoring
//--------------------------------------------------
async function monitorVideoStats(videoReceiver, abortSignal) {
    while (!abortSignal.aborted) {
        try {
            const stats = await videoReceiver.getStats();

            for(const report of stats.values()) {
                if (report.type !== "inbound-rtp" || report.kind !== "video") continue;

                const packetsLost = report.packetsLost ?? 0;
                const framesDecoded = report.framesDecoded ?? 0;
                const freezeCount = report.freezeCount ?? 0;
                const nackCount = report.nackCount ?? 0;
                const packetsDiscarded = report.packetsDiscarded ?? 0;
                const jitter = report.jitter ?? 0;
                const totalAssemblyTime = report.totalAssemblyTime ?? 0;

                log(
                    `[STATS] lost=${packetsLost} nack=${nackCount} freeze=${freezeCount} discarded=${packetsDiscarded} decoded=${framesDecoded} jitter=${jitter} asm=${totalAssemblyTime}`
                );

                if (freezeCount > lastFreezeCount) {
                    sendForceKeyframe(`freeze_${lastFreezeCount}_to_${freezeCount}`);
                }

                lastPacketsLost = packetsLost;
                lastNackCount = nackCount;
                lastFreezeCount = freezeCount;
                lastPacketsDiscarded = packetsDiscarded;
            }
        } catch (e) {
            if (!abortSignal.aborted) {
                console.error("[monitorVideoStats error]", e);
            }
        }

        await new Promise(r => setTimeout(r, 300));
    }
}

async function connect() {
    cleanupPeerConnection();

    remoteVideoStream = new MediaStream();

    const config = await fetchWebRtcConfig();
    log("ICE CONFIG:", config);

    pc = new RTCPeerConnection({
        iceServers: config.iceServers,
        iceTransportPolicy: "relay",
        bundlePolicy: "max-bundle",
        rtcpMuxPolicy: "require",
    });

    if (audioEl) {
        audioEl.srcObject = remoteAudioStream;
        audioEl.muted = true;
        audioEl.volume = 1.0;
    }

    if (videoEl) {
        videoEl.onloadedmetadata = () => {
            console.log("loadedmetadata", videoEl.videoWidth, videoEl.videoHeight);
        };

        videoEl.onplaying = () => {
            console.log("video playing");
        }

        videoEl.onwaiting = () => {
            console.warn("video waiting");
        }

        videoEl.onstalled = () => {
            console.warn("video stalled");
        }
    }

    pc.ontrack = async (event) => {
        console.log("ontrack", event.track.kind, event.streams);

        if (event.track.kind === "video") {
            if (event.receiver) {
                try {
                    if ("playoutDelayHint" in event.receiver) {
                        event.receiver.playoutDelayHint = 0.10;
                        console.log("video playoutDelayHint set to 0.02");
                    }
                    // if ("jitterBufferTarget" in event.receiver) {
                    //     event.receiver.jitterBufferTarget = 0;
                    //     console.log("video jitterBufferTarget set to 0");
                    // }
                } catch (e) {
                    console.warn("video receiver tuning failed", e);
                }

                if (videoStatsMonitorAbort) {
                    videoStatsMonitorAbort.aborted = true;
                }
                videoStatsMonitorAbort = { aborted: false };
                monitorVideoStats(event.receiver, videoStatsMonitorAbort);
            }

            await startVideoTrackProcessor(event.track);

            setTimeout(() => {
                sendForceKeyframe("initial_start");
            }, 300);

            return;
        }

        if (event.track.kind === "audio") {
            if (!remoteAudioStream.getTracks().some(t => t.id === event.track.id)) {
                remoteAudioStream.addTrack(event.track);
            }

            if (event.receiver) {
                try {
                    if ("playoutDelayHint" in event.receiver) {
                        event.receiver.playoutDelayHint = 0.05;
                    }
                    // if ("jitterBufferTarget" in event.receiver) {
                    //     event.receiver.jitterBufferTarget = 50;
                    // }
                } catch (e) {
                    console.warn("audio receiver tuning failed", e);
                }
            }

            if (audioEl) {
                audioEl.srcObject = remoteAudioStream;
                await audioEl.play().catch((e) => {
                    console.warn("audio.play rejected", e);
                });
            }
        }
    };

    pc.ondatachannel = (e) => {
        dc = e.channel;
        inputDc = dc;

        console.log("DataChannel received:", dc.label);

        dc.onopen = () => {
            console.log("DataChannel open");
            startGamepadLoop();
        };

        dc.onclose = () => {
            console.log("DataChannel closed");
        };

        dc.onerror = (err) => {
            console.error("DataChannel error", err);
        };

        dc.onmessage = (ev) => {
            console.log("DataChannel message:", ev.data);
        };
    };

    pc.onicecandidate = (e) => {
        console.log("local candidate:", e.candidate);
        const ice = document.getElementById("ice");
        if (e.candidate) {
            ice.value = JSON.stringify(e.candidate);
            console.log("CLIENT ICE CANDIDATE:");
            console.log(JSON.stringify(e.candidate));
        }
    };

    pc.onicecandidateerror = (e) => {
        console.error("ICE candidate error", e);
    };

    pc.oniceconnectionstatechange = async () => {
        console.log("iceConnectionState =", pc.iceConnectionState);

        if (pc.iceConnectionState === "connected" || pc.iceConnectionState === "completed") {
            if (!rtcSummaryIntervalId) {
                rtcSummaryIntervalId = setInterval(() => {
                    logRtcSummary(pc).catch(console.error);
                }, 5000);
            }

            await logSelectedCandidatePair(pc);
        }
    };

    pc.onconnectionstatechange = () => {
        console.log("connectionState =", pc.connectionState);
    };

    pc.onicegatheringstatechange = () => {
        console.log("iceGatheringState =", pc.iceGatheringState);
    };

    pc.onsignalingstatechange = () => {
        console.log("signalingState =", pc.signalingState);
    };

    pc.createDataChannel("input", {
        ordered: false,
        maxRetransmits: 0
    });

    const offer = JSON.parse(document.getElementById("offer").value);
    await pc.setRemoteDescription(offer);

    const answer = await pc.createAnswer();
    await pc.setLocalDescription(answer);

    document.getElementById("answer").value = JSON.stringify(pc.localDescription);

    startStatsMonitor();
    startRenderLoop();
    startRenderStatsLog();
}

function cleanupPeerConnection() {
    if (statsIntervalId) {
        clearInterval(statsIntervalId);
        statsIntervalId = null;
    }

    if (rtcSummaryIntervalId) {
        clearInterval(rtcSummaryIntervalId);
        rtcSummaryIntervalId = null;
    }

    if (renderStatsIntervalId) {
        clearInterval(renderStatsIntervalId);
        renderStatsIntervalId = null;
    }

    if (videoStatsMonitorAbort) {
        videoStatsMonitorAbort.aborted = true;
        videoStatsMonitorAbort = null
    }

    stopVideoTrackProcessor();

    if (remoteAudioStream) {
        for (const track of remoteAudioStream.getTracks()) {
            remoteAudioStream.removeTrack(track);
        }
    }

    if (audioEl) {
        audioEl.srcObject = null;
    }

    if (dc) {
        try {
            dc.close();
        } catch (_) {}
        dc = null;
    }

    inputDc = null;

    if (pc) {
        try {
            pc.close();
        } catch (_) {}
        pc = null;
    }

    closeFrameSafe(latestFrame);
    latestFrame = null;

    closeFrameSafe(lastGoodFrame);
    lastGoodFrame = null;

    lastFrameArrivedAt = 0;
    lastRenderedAt = 0;

    lastPacketsLost = 0;
    lastNackCount = 0;
    lastFreezeCount = 0;
    lastPacketsDiscarded = 0;

    forceKeyframeCooldownUntil = 0;
}

function copyIceCandidate() {
    const iceCandidate = document.getElementById("ice").value;
    navigator.clipboard.writeText(iceCandidate);
}

function copyAnswer() {
    const answer = document.getElementById("answer").value;
    navigator.clipboard.writeText(answer);
}

async function onHostIce() {
    await addHostCandidate();
}

async function addHostCandidate() {
    const json = document.getElementById("host").value;
    if (!json) return;
    if (!pc) {
        console.warn("pc is not ready");
        return;
    }

    const candidate = new RTCIceCandidate(JSON.parse(json));
    await pc.addIceCandidate(candidate);

    console.log("Host ICE candidate added");
}

function onEnableAudio() {
    try {
        if (videoEl) {
            videoEl.muted = false;
            videoEl.volume = 1.0;
        }

        if (audioEl) {
            audioEl.muted = false;
            audioEl.volume = 1.0;
            audioEl.play().catch((e) => console.warn("audio.play rejected", e));
        }

        if (videoEl){
            const playPromise = videoEl.play();
            if (playPromise !== undefined) {
                playPromise
                    .then(() => {
                        console.log("video.play resolved");
                    })
                    .catch((e) => {
                        console.error("video.play rejected", e);
                    });
            }

            console.log("audio enabled");
            console.log({
                muted: videoEl.muted,
                volume: videoEl.volume,
                paused: videoEl.paused,
                readyState: videoEl.readyState
            });
        }
    } catch (e) {
        console.error("failed to enable audio", e);
    }
}

function startGamepadLoop() {
    console.log("startGamepadLoop started");

    function loop() {
        if (!dc || dc.readyState !== "open") {
            requestAnimationFrame(loop);
            return;
        }

        if (gamepadIndex == null) {
            requestAnimationFrame(loop);
            return;
        }

        const gamepads = navigator.getGamepads();
        const gamepad = gamepads[gamepadIndex];

        if (!gamepad) {
            requestAnimationFrame(loop);
            return;
        }

        const buttons = encodeButtons(gamepad);

        const buf = new ArrayBuffer(12);
        const view = new DataView(buf);

        view.setUint16(0, buttons, true);
        view.setUint8(2, Math.floor(gamepad.buttons[6].value * 255));
        view.setUint8(3, Math.floor(gamepad.buttons[7].value * 255));
        view.setInt16(4, Math.floor(gamepad.axes[0] * 32767), true);
        view.setInt16(6, Math.floor(gamepad.axes[1] * -32767), true);
        view.setInt16(8, Math.floor(gamepad.axes[2] * 32767), true);
        view.setInt16(10, Math.floor(gamepad.axes[3] * -32767), true);

        try {
            dc.send(buf);
        } catch (e) {
            console.error("gamepad send failed", e);
        }

        requestAnimationFrame(loop);
    }

    requestAnimationFrame(loop);
}

async function toggleFullscreen() {
    const player = document.getElementById("player");

    try {
        if (!document.fullscreenElement) {
            await player.requestFullscreen();
        } else {
            await document.exitFullscreen();
        }
    } catch (e) {
        console.error("fullscreen failed", e);
    }
}

function encodeButtons(gamepad) {
    let b = 0;

    if (gamepad.buttons[0].pressed) b |= 1 << 12;
    if (gamepad.buttons[1].pressed) b |= 1 << 13;
    if (gamepad.buttons[2].pressed) b |= 1 << 14;
    if (gamepad.buttons[3].pressed) b |= 1 << 15;

    if (gamepad.buttons[4].pressed) b |= 1 << 8;
    if (gamepad.buttons[5].pressed) b |= 1 << 9;

    if (gamepad.buttons[8].pressed) b |= 1 << 5;
    if (gamepad.buttons[9].pressed) b |= 1 << 4;

    if (gamepad.buttons[10].pressed) b |= 1 << 6;
    if (gamepad.buttons[11].pressed) b |= 1 << 7;

    if (gamepad.buttons[12].pressed) b |= 1 << 0;
    if (gamepad.buttons[13].pressed) b |= 1 << 1;
    if (gamepad.buttons[14].pressed) b |= 1 << 2;
    if (gamepad.buttons[15].pressed) b |= 1 << 3;

    return b;
}

function startStatsMonitor() {
    if (!pc) return;

    if (statsIntervalId) {
        clearInterval(statsIntervalId);
    }

    statsIntervalId = setInterval(async () => {
        if (!pc) return;

        try {
            const stats = await pc.getStats();

            let selectedCandidatePair = null;
            let remoteCandidateId = null;

            stats.forEach((report) => {
                if (report.type === "candidate-pair" && report.state === "succeeded" && report.nominated) {
                    selectedCandidatePair = report;
                    remoteCandidateId = report.remoteCandidateId;
                }
                if (!selectedCandidatePair && report.type === "transport" && report.selectedCandidatePairId) {
                    selectedCandidatePair = stats.get(report.selectedCandidatePairId);
                    remoteCandidateId = selectedCandidatePair?.remoteCandidateId;
                }
            });

            let remoteCandidate = null;
            if (remoteCandidateId) {
                remoteCandidate = stats.get(remoteCandidateId);
            }

            stats.forEach((report) => {
                if (report.type === "inbound-rtp" && report.kind === "video") {
                    console.log("[video stats]", {
                        jitter: report.jitter,
                        framesDecoded: report.framesDecoded,
                        framesDropped: report.framesDropped,
                        frameWidth: report.frameWidth,
                        frameHeight: report.frameHeight,
                        keyFramesDecoded: report.keyFramesDecoded,
                        totalDecodeTime: report.totalDecodeTime,
                        jitterBufferDelay: report.jitterBufferDelay,
                        jitterBufferEmittedCount: report.jitterBufferEmittedCount,
                        freezeCount: report.freezeCount,
                        totalFreezesDuration: report.totalFreezesDuration,
                        packetsLost: report.packetsLost
                    });
                }

                if (report.type === "inbound-rtp" && report.kind === "audio") {
                    console.log("[audio stats]", {
                        jitter: report.jitter,
                        jitterBufferDelay: report.jitterBufferDelay,
                        packetsLost: report.packetsLost
                    });
                }
            });

            if (selectedCandidatePair) {
                console.log("[candidate pair]", {
                    currentRoundTripTime: selectedCandidatePair.currentRoundTripTime,
                    availableIncomingBitrate: selectedCandidatePair.availableIncomingBitrate,
                    availableOutgoingBitrate: selectedCandidatePair.availableOutgoingBitrate,
                    bytesReceived: selectedCandidatePair.bytesReceived,
                    bytesSent: selectedCandidatePair.bytesSent,
                    localCandidateId: selectedCandidatePair.localCandidateId,
                    remoteCandidateId: selectedCandidatePair.remoteCandidateId,
                    remoteCandidateProtocol: remoteCandidate?.protocol,
                    remoteCandidateType: remoteCandidate?.candidateType,
                    remoteCandidateAddress: remoteCandidate?.address,
                    remoteCandidatePort: remoteCandidate?.port
                });
            }
        } catch (e) {
            console.error("getStats failed", e);
        }
    }, 2000);
}

async function logSelectedCandidatePair(pc) {
    const stats = await pc.getStats();

    let selectedPair = null;
    let localCandidate = null;
    let remoteCandidate = null;

    stats.forEach(report => {
        if (report.type === "candidate-pair" && report.state === "succeeded" && report.nominated) {
            selectedPair = report;
        }
    });

    if (!selectedPair) {
        stats.forEach(report => {
            if (report.type === "transport" && report.selectedCandidatePairId) {
                selectedPair = stats.get(report.selectedCandidatePairId);
            }
        });
    }

    if (selectedPair) {
        if (selectedPair.localCandidateId) {
            localCandidate = stats.get(selectedPair.localCandidateId);
        }
        if (selectedPair.remoteCandidateId) {
            remoteCandidate = stats.get(selectedPair.remoteCandidateId);
        }

        console.log("=== SELECTED CANDIDATE PAIR ===");
        console.log("pair:", selectedPair);
        console.log("local:", localCandidate);
        console.log("remote:", remoteCandidate);

        console.log("summary:", {
            state: selectedPair.state,
            nominated: selectedPair.nominated,
            currentRoundTripTime: selectedPair.currentRoundTripTime,
            availableOutgoingBitrate: selectedPair.availableOutgoingBitrate,
            availableIncomingBitrate: selectedPair.availableIncomingBitrate,
            localCandidateType: localCandidate?.candidateType,
            localProtocol: localCandidate?.protocol,
            remoteCandidateType: remoteCandidate?.candidateType,
            remoteProtocol: remoteCandidate?.protocol,
            localAddress: localCandidate?.address,
            remoteAddress: remoteCandidate?.address
        });
    } else {
        console.log("selected candidate pair not found");
    }
}

async function logRtcSummary(pc) {
    const stats = await pc.getStats();

    let selectedPair = null;
    let inboundVideo = null;

    stats.forEach(report => {
        if (report.type === "candidate-pair" && report.state === "succeeded" && report.nominated) {
            selectedPair = report;
        }
        if (report.type === "transport" && report.selectedCandidatePairId && !selectedPair) {
            selectedPair = stats.get(report.selectedCandidatePairId);
        }
        if (report.type === "inbound-rtp" && report.kind === "video") {
            inboundVideo = report;
        }
    });

    console.log("[RTC SUMMARY]", {
        iceConnectionState: pc.iceConnectionState,
        currentRoundTripTime: selectedPair?.currentRoundTripTime,
        availableOutgoingBitrate: selectedPair?.availableOutgoingBitrate,
        availableIncomingBitrate: selectedPair?.availableIncomingBitrate,
        packetsLost: inboundVideo?.packetsLost,
        jitter: inboundVideo?.jitter,
        framesDecoded: inboundVideo?.framesDecoded,
        framesPerSecond: inboundVideo?.framesPerSecond,
        freezeCount: inboundVideo?.freezeCount,
        totalFreezeDuration: inboundVideo?.totalFreezeDuration,
        jitterBufferDelay: inboundVideo?.jitterBufferDelay,
        jitterBufferEmittedCount: inboundVideo?.jitterBufferEmittedCount
    });
}

async function startVideoTrackProcessor(track) {
    stopVideoTrackProcessor();

    if (typeof MediaStreamTrackProcessor === "undefined") {
        console.warn("MediaStreamTrackProcessor is not available in this browser/context");
        return;
    }

    processorStopped = false;
    receivedFrames = 0;
    droppedFrames = 0;
    renderedFrames = 0;

    videoProcessor = new MediaStreamTrackProcessor({ track });
    videoReader = videoProcessor.readable.getReader();

    console.log("startVideoTrackProcessor");

    (async () => {
        try {
            while (!processorStopped) {
                const { value: frame, done } = await videoReader.read();
                if (done || !frame) break;

                receivedFrames++;
                lastFrameArrivedAt = nowMs();

                if (latestFrame) {
                    droppedFrames++;
                }
                
                replaceFrame("latest", frame.clone());
                replaceFrame("lastGood", frame.clone());

                closeFrameSafe(frame);
            }
        } catch (e) {
            if (!processorStopped) {
                console.error("videoReader.read failed", e);
                sendForceKeyframe("video_render_error");
            }
        }
    })();
}

function stopVideoTrackProcessor() {
    processorStopped = true;

    if (videoReader) {
        try {
            videoReader.releaseLock();
        } catch (_) {}
        
        try {
            videoReader.releaseLock();
        } catch (_) {}

        videoReader = null;
    }

    videoProcessor = null;

    closeFrameSafe(latestFrame);
    latestFrame = null;

    closeFrameSafe(lastGoodFrame);
    lastGoodFrame = null;
}