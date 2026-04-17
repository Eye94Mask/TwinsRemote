let pc;
let dc;
let inputDc = null;
let gamepadIndex = null;

let remoteAudioStream = new MediaStream();

let statsIntervalId = null;
let rtcSummaryIntervalId = null;
let videoStatsMonitorAbort = null;
let videoWatchdogIntervalId = null;
let hostCandidatePollIntervalId = null;
let answerPollIntervalId = null;

let audioEl = null;
let videoEl = null;
let canvasEl = null;
let canvasCtx = null;

let forceKeyframeCooldownUntil = 0;
const FORCE_KEYFRAME_COOLDOWN_MS = 2000;

// ---- video processor state ----
let videoProcessor = null;
let videoReader = null;
let processorAbort = null;
let renderLoopActive = false;

let latestFrame = null;
let lastFrameArrivedAt = 0;
let firstVideoFrameArrived = false;

let receivedFrames = 0;
let droppedFrames = 0;
let renderedFrames = 0;

let ttlSeconds = 600 * 1000;
let tokenTimeoutMessage = null;

let connectStatus = null;
let seenRemoteCandidates = new Set();

let sessionId = null;
let copySessionResetTimer = null;
let pendingRemoteCandidates = [];

const VIDEO_STALL_MS = 300;
const RENDER_IDLE_WAIT_MS = 8;

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

window.addEventListener("DOMContentLoaded", async () => {
    await fetchTtlSeconds();
    tokenTimeoutMessage = window.setTimeout(tokenTimeout, ttlSeconds);

    connectStatus = document.getElementById("status");

    videoEl = document.getElementById("video");
    const fullscreenBtn = document.getElementById("fullscreenBtn");
    const audioSwitch = document.getElementById("audio");

    setAudioUiState("off");

    if (videoEl) {
        videoEl.style.display = "none";
        videoEl.playsInline = true;
        videoEl.autoplay = false;
        videoEl.muted = true;
        videoEl.controls = false;
        videoEl.addEventListener("dblclick", () => {
            toggleFullscreen();
        });
    }

    setupCanvas();

    if (fullscreenBtn) {
        fullscreenBtn.addEventListener("click", () => {
            toggleFullscreen();
        });
    }

    audioSwitch.addEventListener("change", onSwitchAudio);

    audioEl = document.createElement("audio");
    audioEl.autoplay = true;
    audioEl.playsInline = true;
    audioEl.controls = false;
    audioEl.style.display = "none";
    audioEl.muted = true;
    audioEl.volume = 1.0;
    document.body.appendChild(audioEl);

    const sessionIdEl = document.getElementById("sessionId");
    if (sessionIdEl && !sessionIdEl.value) {
        sessionIdEl.value = generateSessionId();
    }

    try {
        await connect();
    } catch (e) {
        console.error("initial connect failed", e);
    }
});

function tokenTimeout() {
    if (!alert("トークンの有効期限が切れました\nページの再読み込みをします")) {
        location.reload();
    }
    window.clearTimeout(tokenTimeoutMessage);
}

function setupCanvas() {
    const player = document.getElementById("player");

    canvasEl = document.createElement("canvas");
    canvasEl.id = "videoCanvas";
    canvasEl.style.display = "block";
    canvasEl.style.width = "100%";
    canvasEl.style.height = "100%";
    canvasEl.style.background = "black";
    canvasEl.style.objectFit = "contain";
    canvasEl.tabIndex = 0;

    canvasEl.addEventListener("dblclick", () => {
        toggleFullscreen();
    });

    if (player) {
        player.appendChild(canvasEl);
    } else {
        document.body.appendChild(canvasEl);
    }

    canvasCtx = canvasEl.getContext("2d", {
        alpha: false,
        desynchronized: true,
    });

    clearCanvas();
}

function clearCanvas() {
    if (!canvasCtx || !canvasEl) return;
    canvasCtx.save();
    canvasCtx.setTransform(1, 0, 0, 1, 0, 0);
    canvasCtx.clearRect(0, 0, canvasEl.width || 1, canvasEl.height || 1);
    canvasCtx.fillStyle = "black";
    canvasCtx.fillRect(0, 0, canvasEl.width || 1, canvasEl.height || 1);
    canvasCtx.restore();
}

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

function generateSessionId() {
    if (window.crypto?.randomUUID) {
        return window.crypto.randomUUID();
    }
    return Math.random().toString(36).slice(2, 10);
}

function getOrCreateSessionId() {
    const el = document.getElementById("sessionId");
    if (el) {
        const v = (el.value || "").trim();
        if (v) return v;

        const newId = generateSessionId();
        el.value = newId;
        return newId;
    }

    if (!sessionId) {
        sessionId = generateSessionId();
    }
    return sessionId;
}

function buildSessionUrl(path) {
    if (!sessionId) {
        throw new Error("sessionId is not set");
    }
    return `${path}?sessionId=${encodeURIComponent(sessionId)}`;
}

async function copySessionId() {
    const btn = document.getElementById("copySessionIdBtn");
    const text = document.getElementById("sessionId")?.value || sessionId || "";

    if (!btn) {
        try {
            await navigator.clipboard.writeText(text);
        } catch (e) {
            console.warn("failed to copy sessionId", e);
        }
        return;
    }

    const originalText = btn.dataset.originalText || btn.textContent || "Copy";
    btn.dataset.originalText = originalText;

    clearTimeout(copySessionResetTimer);
    btn.classList.remove("copy-success", "copy-error");

    try {
        await navigator.clipboard.writeText(text);

        btn.textContent = "✔";
        btn.classList.add("copy-success");

        copySessionResetTimer = setTimeout(() => {
            btn.classList.remove("copy-success");
            btn.textContent = originalText;
        }, 1600);
    } catch (e) {
        console.warn("failed to copy sessionId", e);

        btn.textContent = "Failed";
        btn.classList.add("copy-error");

        copySessionResetTimer = setTimeout(() => {
            btn.classList.remove("copy-error");
            btn.textContent = originalText;
        }, 1400);
    }
}

window.copySessionId = copySessionId;

function sendForceKeyframe(reason) {
    const now = performance.now();

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
        console.log("[CLIENT] sent force_keyframe:", reason);
    } catch (e) {
        console.warn("[CLIENT] force_keyframe send failed:", e);
    }
}

async function fetchWebRtcConfig() {
    const token = window.__WEBRTC_CONFIG_TOKEN__;
    if (!token) {
        throw new Error("webrtc config token is missing");
    }

    const res = await fetch("/webrtc-config", {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify({ token })
    });

    if (!res.ok) {
        const text = await res.text();
        throw new Error("failed to fetch webrtc config: " + text);
    }

    return await res.json();
}

async function fetchTtlSeconds() {
    const res = await fetch("/ttl-seconds");

    if (!res.ok) {
        return;
    }

    const json = await res.json();
    ttlSeconds = json.ttlSeconds * 1000;
}

async function postJson(url, body) {
    const res = await fetch(url, {
        method: "POST",
        headers: {
            "Content-Type": "application/json"
        },
        body: JSON.stringify(body)
    });

    if (!res.ok) {
        const text = await res.text().catch(() => "");
        throw new Error(`POST ${url} failed: ${res.status} ${text}`);
    }
}

async function fetchJson(url) {
    const res = await fetch(url, {
        method: "GET",
        headers: {
            "Accept": "application/json"
        }
    });

    if (res.status === 204) {
        return null;
    }

    if (!res.ok) {
        const text = await res.text().catch(() => "");
        throw new Error(`GET ${url} failed: ${res.status} ${text}`);
    }

    return await res.json();
}

function candidateKey(c) {
    return JSON.stringify({
        candidate: c.candidate ?? "",
        sdpMid: c.sdpMid ?? null,
        sdpMLineIndex: c.sdpMLineIndex ?? null,
        usernameFragment: c.usernameFragment ?? null
    });
}

async function postOffer(offer) {
    await postJson(buildSessionUrl("/offer"), offer);
}

async function pollAnswerOnce() {
    const json = await fetchJson(buildSessionUrl("/answer"));
    if (!json) return false;
    if (!pc) return false;
    if (pc.remoteDescription) return true;

    await pc.setRemoteDescription(json);
    console.log("remote answer set");

    await flushPendingRemoteCandidates();

    return true;
}

function startAnswerPolling() {
    if (answerPollIntervalId) {
        clearInterval(answerPollIntervalId);
    }

    answerPollIntervalId = setInterval(async () => {
        if (!pc) return;
        if (pc.remoteDescription) return;

        try {
            const ok = await pollAnswerOnce();
            if (ok) {
                clearInterval(answerPollIntervalId);
                answerPollIntervalId = null;
            }
        } catch (e) {
            console.warn("pollAnswer failed", e);
        }
    }, 500);
}

async function postClientCandidate(candidate) {
    await postJson(buildSessionUrl("/client-candidate"), candidate);
}

async function pollHostCandidates() {
    if (!pc) return;

    try {
        const json = await fetchJson(buildSessionUrl("/host-candidate"));
        if (!json || !Array.isArray(json.candidates)) return;

        for (const c of json.candidates) {
            if (!c || !c.candidate) continue;

            const key = candidateKey(c);
            if (seenRemoteCandidates.has(key)) continue;
            seenRemoteCandidates.add(key);

            if (!pc.remoteDescription) {
                pendingRemoteCandidates.push(c);
                console.log("Host ICE candidate queued (remoteDescription not set yet):", c);
                continue;
            }

            await pc.addIceCandidate(new RTCIceCandidate(c));
            console.log("Host ICE candidate added:", c);
        }
    } catch (e) {
        console.warn("pollHostCandidates failed", e);
    }
}

async function flushPendingRemoteCandidates() {
    if (!pc || !pc.remoteDescription) return;

    while (pendingRemoteCandidates.length > 0) {
        const c = pendingRemoteCandidates.shift();
        try {
            await pc.addIceCandidate(new RTCIceCandidate(c));
            console.log("Queued host ICE candidate added:", c);
        } catch (e) {
            console.warn("failed to add queued host candidate", e, c);
        }
    }
}

function startHostCandidatePolling() {
    if (hostCandidatePollIntervalId) {
        clearInterval(hostCandidatePollIntervalId);
    }

    hostCandidatePollIntervalId = setInterval(() => {
        pollHostCandidates();
    }, 300);
}

async function resetSession() {
    try {
        await postJson(buildSessionUrl("/reset"), {});
    } catch (e) {
        console.warn("resetSession failed", e);
    }
}

//--------------------------------------------------
// WebRTC stats monitoring
//--------------------------------------------------
async function monitorVideoStats(videoReceiver, abortSignal) {
    while (!abortSignal.aborted) {
        try {
            await videoReceiver.getStats();
        } catch (e) {
            if (!abortSignal.aborted) {
                console.error("[monitorVideoStats error]", e);
            }
        }

        await sleep(1000);
    }
}

function sleep(ms) {
    return new Promise(r => setTimeout(r, ms));
}

function connectStatusReset() {
    if (!connectStatus) return;
    connectStatus.classList.forEach((name) => {
        connectStatus.classList.remove(name);
    });
}

async function connect() {
    cleanupPeerConnection();
    window.clearTimeout(tokenTimeoutMessage);

    sessionId = getOrCreateSessionId();
    log("SESSION ID:", sessionId);

    const config = await fetchWebRtcConfig();
    log("ICE CONFIG:", config);

    pc = new RTCPeerConnection({
        iceServers: config.iceServers,
        iceTransportPolicy: "all",
        bundlePolicy: "max-bundle",
        rtcpMuxPolicy: "require",
    });

    if (audioEl) {
        audioEl.srcObject = remoteAudioStream;
        audioEl.muted = true;
        audioEl.volume = 1.0;
    }

    pc.ontrack = async (event) => {
        console.log("ontrack", event.track.kind, event.streams);

        if (event.track.kind === "video") {
            console.log("[VIDEO] track received:", event.track.id);

            if (event.receiver) {
                try {
                    if ("playoutDelayHint" in event.receiver) {
                        event.receiver.playoutDelayHint = 0.0;
                    }
                    if ("jitterBufferTarget" in event.receiver) {
                        event.receiver.jitterBufferTarget = 0.02;
                    }
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
                        event.receiver.playoutDelayHint = 0.0;
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

    pc.onicecandidate = async (e) => {
        console.log("local candidate:", e.candidate);

        if (!e.candidate) {
            console.log("local ICE gathering finished");
            return;
        }

        try {
            await postClientCandidate(e.candidate.toJSON());
            console.log("client candidate posted");
        } catch (err) {
            console.warn("failed to post client candidate", err);
        }
    };

    pc.onicecandidateerror = (e) => {
        console.warn("ICE candidate warning", e);
    };

    pc.oniceconnectionstatechange = async () => {
        console.log("iceConnectionState =", pc.iceConnectionState);

        if (pc.iceConnectionState === "checking") {
            if (connectStatus) {
                connectStatus.innerText = "接続待機中";
                connectStatusReset();
                connectStatus.classList.add("connecting");
            }
        }

        if (pc.iceConnectionState === "connected" || pc.iceConnectionState === "completed") {
            if (connectStatus) {
                connectStatus.innerText = "接続完了";
                connectStatusReset();
                connectStatus.classList.add("connected");
            }

            if (!rtcSummaryIntervalId) {
                rtcSummaryIntervalId = setInterval(() => {
                    logRtcSummary(pc).catch(console.error);
                }, 5000);
            }

            await logSelectedCandidatePair(pc);
        }

        if (pc.iceConnectionState === "disconnected") {
            if (connectStatus) {
                connectStatus.innerText = "接続終了";
                connectStatusReset();
                connectStatus.classList.add("disconnected");
            }
        }

        if (pc.iceConnectionState === "failed") {
            if (connectStatus) {
                connectStatus.innerText = "接続エラー";
                connectStatusReset();
                connectStatus.classList.add("failed");
            }
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

    // 受信用Tranceiver
    pc.addTransceiver("video", { direction: "recvonly"});
    pc.addTransceiver("audio", { direction: "recvonly"});

    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);

    await resetSession();
    await postOffer(pc.localDescription?.toJSON ? pc.localDescription.toJSON() : pc.localDescription);

    seenRemoteCandidates.clear();
    startHostCandidatePolling();
    startAnswerPolling();

    startStatsMonitor();
    startVideoWatchdog();
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

    if (videoWatchdogIntervalId) {
        clearInterval(videoWatchdogIntervalId);
        videoWatchdogIntervalId = null;
    }

    if (hostCandidatePollIntervalId) {
        clearInterval(hostCandidatePollIntervalId);
        hostCandidatePollIntervalId = null;
    }

    if (answerPollIntervalId) {
        clearInterval(answerPollIntervalId);
        answerPollIntervalId = null;
    }

    if (videoStatsMonitorAbort) {
        videoStatsMonitorAbort.aborted = true;
        videoStatsMonitorAbort = null;
    }

    seenRemoteCandidates.clear();

    stopVideoTrackProcessor();

    if (remoteAudioStream) {
        for (const track of remoteAudioStream.getTracks()) {
            remoteAudioStream.removeTrack(track);
        }
    }

    if (audioEl) {
        audioEl.srcObject = null;
    }

    if (videoEl) {
        videoEl.srcObject = null;
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

    firstVideoFrameArrived = false;
    lastFrameArrivedAt = 0;
    receivedFrames = 0;
    droppedFrames = 0;
    renderedFrames = 0;
    forceKeyframeCooldownUntil = 0;

    pendingRemoteCandidates = [];

    clearCanvas();
}

function setAudioUiState(state) {
    const area = document.getElementById("makeImg");
    if (!area) return;
    area.dataset.audioState = state;
}

function onSwitchAudio() {
    const audioOn = document.getElementById("audioOn");
    const audioOff = document.getElementById("audioOff");

    if (audioOn) {
        audioOn.addEventListener("change", async () => {
            if (!audioOn.checked) return;

            try {
                setAudioUiState("pending");

                if (audioEl) {
                    audioEl.muted = false;
                    audioEl.volume = 1.0;
                    await audioEl.play();
                }

                setAudioUiState("on");
            } catch (e) {
                console.error("failed to enable audio", e);
                audioOff.checked = true;
                setAudioUiState("off");
            }
        });
    }

    if (audioOff) {
        audioOff.addEventListener("change", () => {
            if (!audioOff.checked) return;

            setAudioUiState("pending-off");

            if (audioEl) {
                audioEl.muted = true;
            }

            setTimeout(() => {
                setAudioUiState("off");
            }, 120);
        });
    }

    if (audioEl.muted) {
        log("audio disabled");
    } else {
        log("audio enabled");
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
    const player = document.getElementById("player") || canvasEl;

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

function closeFrameSafe(frame) {
    if (!frame) return;
    try {
        frame.close();
    } catch (_) {}
}

function replaceLatestFrame(frame) {
    if (latestFrame) {
        closeFrameSafe(latestFrame);
        droppedFrames++;
    }
    latestFrame = frame;
}

async function startVideoTrackProcessor(track) {
    stopVideoTrackProcessor();

    if (typeof MediaStreamTrackProcessor === "undefined") {
        console.warn("MediaStreamTrackProcessor is not available in this browser/context");
        console.warn("falling back to direct video element playback");

        if (videoEl) {
            videoEl.style.display = "block";
            videoEl.srcObject = new MediaStream([track]);
            await videoEl.play().catch((e) => {
                console.warn("video.play rejected", e);
            });
        }
        return;
    }

    if (videoEl) {
        videoEl.style.display = "none";
        videoEl.srcObject = null;
    }

    processorAbort = { aborted: false };
    videoProcessor = new MediaStreamTrackProcessor({ track });
    videoReader = videoProcessor.readable.getReader();

    firstVideoFrameArrived = false;
    lastFrameArrivedAt = 0;
    receivedFrames = 0;
    droppedFrames = 0;
    renderedFrames = 0;

    console.log("[VIDEO] startVideoTrackProcessor");

    startRenderLoop();

    (async () => {
        try {
            while (!processorAbort.aborted) {
                const { value: frame, done } = await videoReader.read();
                if (done || !frame) break;

                receivedFrames++;
                firstVideoFrameArrived = true;
                lastFrameArrivedAt = nowMs();

                replaceLatestFrame(frame);
            }
        } catch (e) {
            if (!processorAbort?.aborted) {
                console.error("videoReader.read failed", e);
                sendForceKeyframe("video_reader_error");
            }
        } finally {
            console.log("[VIDEO] processor loop ended");
        }
    })();
}

function stopVideoTrackProcessor() {
    if (processorAbort) {
        processorAbort.aborted = true;
    }
    processorAbort = null;

    if (videoReader) {
        try {
            videoReader.cancel();
        } catch (_) {}
        try {
            videoReader.releaseLock();
        } catch (_) {}
        videoReader = null;
    }

    videoProcessor = null;
    renderLoopActive = false;

    closeFrameSafe(latestFrame);
    latestFrame = null;
}

function resizeCanvasToFrame(frame) {
    if (!canvasEl) return;

    const w = frame.displayWidth || frame.codedWidth || 1280;
    const h = frame.displayHeight || frame.codedHeight || 720;

    if (canvasEl.width !== w || canvasEl.height !== h) {
        canvasEl.width = w;
        canvasEl.height = h;
        console.log("[VIDEO] canvas resized:", w, h);
    }
}

function drawVideoFrame(frame) {
    if (!canvasCtx || !canvasEl || !frame) return;

    resizeCanvasToFrame(frame);

    try {
        canvasCtx.drawImage(frame, 0, 0, canvasEl.width, canvasEl.height);
        renderedFrames++;
    } catch (e) {
        console.error("drawImage failed", e);
        sendForceKeyframe("video_draw_error");
    }
}

function startRenderLoop() {
    if (renderLoopActive) return;
    renderLoopActive = true;

    const loop = async () => {
        while (renderLoopActive) {
            if (latestFrame) {
                const frame = latestFrame;
                latestFrame = null;
                drawVideoFrame(frame);
                closeFrameSafe(frame);
                continue;
            }

            await sleep(RENDER_IDLE_WAIT_MS);
        }
    };

    loop().catch((e) => {
        renderLoopActive = false;
        console.error("[VIDEO] render loop failed", e);
        sendForceKeyframe("render_loop_error");
    });
}

function startVideoWatchdog() {
    if (videoWatchdogIntervalId) {
        clearInterval(videoWatchdogIntervalId);
    }

    videoWatchdogIntervalId = setInterval(() => {
        if (!pc) return;
        if (pc.connectionState !== "connected" && pc.connectionState !== "connecting") return;
        if (!firstVideoFrameArrived) return;

        const age = nowMs() - lastFrameArrivedAt;

        if (age > VIDEO_STALL_MS) {
            console.warn("[VIDEO WATCHDOG] stall detected age=", Math.floor(age), "ms");
            sendForceKeyframe("video_stall");
        }
    }, 250);
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
                        packetsLost: report.packetsLost,
                        processorReceivedFrames: receivedFrames,
                        processorDroppedFrames: droppedFrames,
                        processorRenderedFrames: renderedFrames,
                        latestFrameAgeMs: firstVideoFrameArrived ? Math.floor(nowMs() - lastFrameArrivedAt) : null,
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
        jitterBufferEmittedCount: inboundVideo?.jitterBufferEmittedCount,
        processorReceivedFrames: receivedFrames,
        processorDroppedFrames: droppedFrames,
        processorRenderedFrames: renderedFrames,
        latestFrameAgeMs: firstVideoFrameArrived ? Math.floor(nowMs() - lastFrameArrivedAt) : null,
    });
}