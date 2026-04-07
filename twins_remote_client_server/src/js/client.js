let pc;
let dc;
let gamepadIndex = null;

let remoteAudioStream = new MediaStream();

let statsIntervalId = null;
let rtcSummaryIntervalId = null;

let audioEl = null;

// latest-frame rendering
let videoProcessor = null;
let videoReader = null;
let renderCanvas = null;
let renderCtx = null;
let renderLoopStarted = false;
let latestFrame = null;
let receivedFrames = 0;
let droppedFrames = 0;
let renderedFrames = 0;
let processorStopped = false;

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
    const video = document.getElementById("video");
    const fullscreenBtn = document.getElementById("fullscreenBtn");
    const audioBtn = document.getElementById("audioBtn");
    const player = document.getElementById("player");

    // 既存 video は使わないので隠す
    video.style.display = "none";

    // canvas を作る
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

async function connect() {
    cleanupPeerConnection();

    remoteVideoStream = new MediaStream();
    remoteAudioStream = new MediaStream();

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

    video.onloadedmetadata = () => {
        console.log("loadedmetadata", video.videoWidth, video.videoHeight);
    };

    video.onplaying = () => {
        console.log("video playing");
    };

    video.onwaiting = () => {
        console.warn("video waiting");
    };

    video.onstalled = () => {
        console.warn("video stalled");
    };

    pc.ontrack = async (event) => {
        console.log("ontrack", event.track.kind, event.streams);

        if (event.track.kind === "video") {
            if (event.receiver) {
                try {
                    if ("playoutDelayHint" in event.receiver) {
                        event.receiver.playoutDelayHint = 0.10;
                        console.log("video playoutDelayHint set to 0.02");
                    }
                    if ("jitterBufferTarget" in event.receiver) {
                        // event.receiver.jitterBufferTarget = 0;
                        console.log("video jitterBufferTarget set to 0");
                    }
                } catch (e) {
                    console.warn("video receiver tuning failed", e);
                }
            }

            await startVideoTrackProcessor(event.track);
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
                    if ("jitterBufferTarget" in event.receiver) {
                        event.receiver.jitterBufferTarget = 50;
                    }
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

    // DataChannelでUnreliableを明示
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

    if (pc) {
        try {
            pc.close();
        } catch (_) {}
        pc = null;
    }
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
    const video = document.getElementById("video");

    try {
        video.muted = false;
        video.volume = 1.0;

        if (audioEl) {
            audioEl.muted = false;
            audioEl.volume = 1.0;
            audioEl.play().catch((e) => console.warn("audio.play rejected", e));
        }

        const playPromise = video.play();
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
            muted: video.muted,
            volume: video.volume,
            paused: video.paused,
            readyState: video.readyState
        });
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

        dc.send(buf);

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
        totalFreezesDuration: inboundVideo?.totalFreezesDuration,
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

    if (!renderLoopStarted) {
        renderLoopStarted = true;
        requestAnimationFrame(renderLatestFrameLoop);
    }

    console.log("startVideoTrackProcessor");

    (async () => {
        try {
            while (!processorStopped) {
                const { value: frame, done } = await videoReader.read();
                if (done || !frame) break;

                receivedFrames++;

                // 最新1枚だけ保持
                if (latestFrame) {
                    latestFrame.close();
                    droppedFrames++;
                }
                latestFrame = frame;
            }
        } catch (e) {
            if (!processorStopped) {
                console.error("videoReader.read failed", e);
            }
        }
    })();

    setInterval(() => {
        console.log("[CLIENT RENDER]", {
            receivedFrames,
            droppedFrames,
            renderedFrames,
            hasLatestFrame: !!latestFrame
        });
        receivedFrames = 0;
        droppedFrames = 0;
        renderedFrames = 0;
    }, 1000);
}

function stopVideoTrackProcessor() {
    processorStopped = true;

    if (videoReader) {
        try {
            videoReader.releaseLock();
        } catch (_) {}
        videoReader = null;
    }

    videoProcessor = null;

    if (latestFrame) {
        try {
            latestFrame.close();
        } catch (_) {}
        latestFrame = null;
    }
}

function renderLatestFrameLoop() {
    if (latestFrame && renderCanvas && renderCtx) {
        const frame = latestFrame;
        latestFrame = null;

        try {
            if (
                renderCanvas.width !== frame.displayWidth ||
                renderCanvas.height !== frame.displayHeight
            ) {
                renderCanvas.width = frame.displayWidth;
                renderCanvas.height = frame.displayHeight;
            }

            renderCtx.drawImage(frame, 0, 0, renderCanvas.width, renderCanvas.height);
            renderedFrames++;
        } catch (e) {
            console.error("drawImage(frame) failed", e);
        } finally {
            frame.close();
        }
    }

    requestAnimationFrame(renderLatestFrameLoop);
}