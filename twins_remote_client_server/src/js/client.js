import japanese from "./locales/ja-JP.json" with {type: 'json'};;
import english from "./locales/en-US.json" with {type: 'json'};;

let pc;
let inputDc = null;
let controllerDc = null;
let gamepadIndex = null;

let remoteAudioStream = new MediaStream();
let sessionEnded = false;

let statsIntervalId = null;
let rtcSummaryIntervalId = null;
let videoStatsMonitorAbort = null;
let hostCandidatePollIntervalId = null;
let answerPollIntervalId = null;

let audioEl = null;
let videoEl = null;
let canvasEl = null;
let canvasCtx = null;
let streamSource = null;

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
let lateDroppedFrames = 0;

let ttlSeconds = 600 * 1000;
let tokenTimeoutMessage = null;

let status = Object.freeze({
    notConnected: "notConnected",
    preparingConnection: "preparingConnection",
    connecting: "connecting",
    waitingForHost: "waitingForHost",
    connected: "connected",
    disconnected: "disconnected",
    connectionFailure: "connectionFailure",
    capViolationWarning: "capViolationWarning"
});
let currentStatus = status.notConnected;
let connectStatus = null;
let seenRemoteCandidates = new Set();
let pendingRemoteCandidates = [];

let sessionId = null;
let copySessionResetTimer = null;

let turnEnabled = false;

const splashSubtitles = [
    "Two PCs, one feeling.",
    "Connecting comfort and speed.",
    "Twins in sync.",

    "Two worlds, one connection.",
    "Play together. Stay close.",
    "Low latency, high comfort.",
    "Smooth play, shared moments.",
    "Close enough to play together.",
    "Feel every frame together.",
    "One stream, two smiles.",
    "Fast enough to forget the distance.",
    "Together, beyond the screen.",
    "Shared moments, seamless connection.",

    "Perfectly in sync.",
    "Twin PCs, one experience.",
    "Two systems, one rhythm.",
    "Connected like twins.",
    "Sync beyond distance.",
    "Two hearts, one session.",
    "Built for perfect sync.",
    "Where two PCs become one.",
    "Stay synchronized.",
    "Twin-speed connection.",

    "Direct connection. Direct feeling.",
    "Latency kept out of the way.",
    "Speed that feels local.",
    "Feels like you're here.",
    "Instant response, shared experience.",
    "Built for real-time moments.",
    "Fast enough for every frame.",
    "High quality, low delay.",
    "Pure connection, minimal latency.",
    "Closer than remote.",
    "Responsive by design.",

    "Play it together.",
    "Watch together. Play together.",
    "Every moment, shared live.",
    "Distance disappears here.",
    "Where distance disappears.",
    "One experience, two screens.",
    "Share the moment instantly.",
    "Connected for every experience.",
    "Play. Watch. Share.",
    "More than remote play.",

    "Because distance should feel smaller.",
    "Closer through every frame.",
    "The feeling of being there.",
    "Built for people who play together.",
    "Connection you can feel.",
    "Shared time, uninterrupted.",
    "The closest thing to being there.",
    "Remote, without the distance.",
    "Streaming comfort between friends.",
    "Together, frame by frame."
];

let videoWatchdogIntervalId = null;
let lastVideoStats = {
    checkedAt: 0,
    framesDecoded: 0,
    bytesReceived: 0,
    packetsReceived: 0,
    freezeCount: 0,
    totalFreezesDuration: 0,
};
let lastForceKeyframeAt = 0;
let forceKeyframeCooldownUntil = 0;

const VIDEO_WATCHDOG_INTERVAL_MS = 500;
const CORRECT_PLAYBACK_DELAY_MS = 1000;
const VIDEO_STALL_MS = 5000;
const FORCE_KEYFRAME_COOLDOWN_MS = 3000;
const RENDER_IDLE_WAIT_MS = 8;
const MAX_FRAME_AGE_MS = 150;
const MAX_RENDER_BACKLOG = 1;

// =======================================================================================
// "relay-test" | "normal" | "stun-first"
// !!!! リリース前には絶対に const ICE_MODE = "normal"; に変更すること !!!!
// =======================================================================================
const ICE_MODE = "normal";

let badRttCount = 0;
let lastIceRestartAt = 0;

const RTT_WARN_SEC = 0.12;  // 120ms
const RTT_BAD_SEC = 0.20;   // 200ms
const BAD_RTT_LIMIT = 3;
const ICE_RESTART_COOLDOWN_MS = 15000;

// ==========================
// Cap 外し予防
// ==========================
const RELAY_CAP_WIDTH = 1200;
const RELAY_CAP_HEIGHT = 720;
const RELAY_CAP_FPS = 30;
const RELAY_CAP_BITRATE_BPS = 5_000_000;

const FPS_MARGIN = 5;           // 35fps 超えで違反
const BITRATE_MARGIN = 1.4;     // 7Mbps 超えで違反
const HARD_BITRATE_MARGIN = 2.0 // 10Mbps 超えで強違反

let capViolationScore = 0;
let lastVideoBytes = null;
let lastVideoBytesAt = null;

let relayMonitorIntervalId = null;
let lastPostedNetworkMode = null;
// ==========================
// Cap 外し予防
// ==========================

let dcKeepaliveTimer = null;
let dcLastPongAt = 0;
let dcLastPingAt = 0;
let reconnecting = false;
let disconnectedTimer = null;

let rumbleTimer = null;
let currentRumble = {
    large: 0,
    small: 0
};

let noticesJa = null;
let noticesEn = null;

// ==================================
// コントローラー制御 Start
// ==================================
let lastGamepadPacket = null;
let lastGamepadSendAt = 0;
let gamepadSeq = 0;
let lastGamepadSendLogAt = 0;

const GAMEPAD_SEND_INTERVAL_MS = 16;    // 最大60Hz
const GAMEPAD_KEEPALIVE_MS = 500;       // 入力がない場合は0.5秒に1回
const GAMEPAD_BUFFER_LIMIT = 16 * 1024; // 詰まり防止
const AXIS_DEADZONE = 0.05;
const GAMEPAD_PACKET_SIZE = 12;

window.addEventListener("gamepadconnected", (e) => {
    gamepadIndex = e.gamepad.index;
    log("gamepad connected:", e.gamepad.id, "index=", gamepadIndex);
});

window.addEventListener("gamepaddisconnected", (e) => {
    log("gamepad disconnected:", e.gamepad.id, "index=", e.gamepad.index);
    if (gamepadIndex === e.gamepad.index) {
        gamepadIndex = null;
    }
});

function axisToI16(v, invert = false) {
    if (!Number.isFinite(v)) v = 0;
    if (Math.abs(v) < AXIS_DEADZONE) v = 0;
    if (invert) v = -v;

    v = Math.max(-1, Math.min(1, v));
    return Math.round(v * 32767);
}

function isXboxController(gamepad) {
    if (
        gamepad.id.includes("DualSense")
        || gamepad.id.includes("Vendor: 054c")
        || gamepad.id.includes("Product: 09cc")
        || gamepad.id.includes("Product: 0ce6")
    ) {
        return 0;
    }

    return 1;
}

function buildGamepadPacket(gamepad, seq) {
    const buttons = encodeButtons(gamepad);

    const isXbox = isXboxController(gamepad);

    const buf = new ArrayBuffer(17);
    const view = new DataView(buf);

    view.setUint16(0, buttons, true);
    view.setUint8(2, Math.round((gamepad.buttons[6]?.value || 0)* 255));
    view.setUint8(3, Math.round((gamepad.buttons[7]?.value || 0)* 255));
    if (isXbox) {
        view.setInt16(4, axisToI16(gamepad.axes[0]), true);
        view.setInt16(6, axisToI16(gamepad.axes[1], true), true);
        view.setInt16(8, axisToI16(gamepad.axes[2]), true);
        view.setInt16(10, axisToI16(gamepad.axes[3], true), true);
    } else {
        view.setInt16(4, axisToI16(gamepad.axes[0]), true);
        view.setInt16(6, axisToI16(gamepad.axes[1]), true);
        view.setInt16(8, axisToI16(gamepad.axes[2]), true);
        view.setInt16(10, axisToI16(gamepad.axes[3]), true);
    }

    view.setUint32(12, seq, true);

    view.setUint8(16, isXbox);
    return buf;
}

function samePacket(a, b) {
    if (!a || !b) return false;

    const aa = new Uint8Array(a);
    const bb = new Uint8Array(b);

    for (let i = 0; i < GAMEPAD_PACKET_SIZE; i++) {
        if (aa[i] !== bb[i]) return false;
    }

    return true;
}

function startGamepadLoop() {
    console.log("startGamepadLoop started");

    function loop() {
        requestAnimationFrame(loop);

        if (!controllerDc || controllerDc.readyState !== "open") { return; }
        if (gamepadIndex == null) { return; }

        const gamepads = navigator.getGamepads();
        const gamepad = gamepads[gamepadIndex];

        if (!gamepad) { return; }

        const now = performance.now();
        if (now - lastGamepadSendAt < GAMEPAD_SEND_INTERVAL_MS) { return; }

        if (controllerDc.bufferedAmount > GAMEPAD_BUFFER_LIMIT) {
            console.warn("[GAMEPAD] skip send: bufferedAmount = ", dcKeepaliveTimer.bufferedAmount);
            log("[WARN] [GAMEPAD] skip send: bufferedAmount = ", dcKeepaliveTimer.bufferedAmount);
            return;
        }

        const seq = (gamepadSeq + 1) >>> 0;
        const packet = buildGamepadPacket(gamepad, seq);
        const changed = !samePacket(packet, lastGamepadPacket);
        const keepalive = now - lastGamepadSendAt >= GAMEPAD_KEEPALIVE_MS;
        if (!changed && !keepalive) { return; }

        try {
            controllerDc.send(packet);

            const nowLog = performance.now();
            if (nowLog - lastGamepadSendLogAt > 1000) {
                console.log("[GAMEPAD SEND]", {
                    seq,
                    bufferedAmount: controllerDc.bufferedAmount,
                    pcConnectionState: pc?.connectionState,
                    iceConnectionState: pc?.iceConnectionState,
                    dcReadyState: controllerDc?.readyState,
                });
                lastGamepadSendLogAt = nowLog;
            }
            gamepadSeq = seq;
            lastGamepadPacket = packet.slice(0);
            lastGamepadSendAt = now;
        } catch (e) {
            console.error("gamepad send failed", e);
            log("[ERR] gamepad send failed", e);
        }
    }

    requestAnimationFrame(loop);
}

function getActiveGamepad() {
    const pads = navigator.getGamepads ? navigator.getGamepads() : [];

    for (const pad of pads) {
        if (pad && pad.connected) {
            return pad;
        }
    }

    return null;
}

async function handleRumbleMessage(msg) {
    currentRumble.large = msg.large ?? 0;
    currentRumble.small = msg.small ?? 0;

    if (rumbleTimer) {
        clearTimeout(rumbleTimer);
        rumbleTimer = null;
    }

    await applyRumbleState();

    // ホストから振動停止通知が来ない / 落ちた場合の保険
    rumbleTimer = setTimeout(async () => {
        currentRumble.large = 0;
        currentRumble.small = 0;
        await stopRumble();
    }, 600);
}

async function applyRumbleState() {
    const pad = getActiveGamepad();
    if (!pad) return;

    const actuator = pad.vibrationActuator || pad.hapticActuators?.[0];
    if (!actuator) return;

    const large = currentRumble.large;
    const small = currentRumble.small;

    if (large === 0 && small === 0) {
        await stopRumble();
        return;
    }

    const strongMagnitude = clamp01(large / 255);
    const weakMagnitude = clamp01(small / 255);

    try {
        if (typeof actuator.playEffect === "function") {
            await actuator.playEffect("dual-rumble", {
                startDelay: 0,
                duration: 1000,
                strongMagnitude,
                weakMagnitude
            });
        } else if (typeof actuator.pulse === "function") {
            await actuator.pulse(Math.max(strongMagnitude, weakMagnitude), 1000);
        }
    } catch (e) {
        console.warn("[RUMBLE] apply failed", e);
        log("[RUMBLE] apply failed", e);
    }
}

async function stopRumble() {
    const pad = getActiveGamepad();
    if (!pad) return;

    const actuator = pad.vibrationActuator || pad.hapticActuators?.[0];
    if (!actuator) return;

    try {
        if (typeof actuator.playEffect === "function") {
            await actuator.playEffect("dual-rumble", {
                startDelay: 0,
                duration: 1,
                strongMagnitude: 0,
                weakMagnitude: 0
            });
        } else if (typeof actuator.pulse === "function") {
            await actuator.pulse(0, 1);
        }
    } catch (e) {
        console.warn("[RUMBLE] stop failed", e);
        log("[WARN] [RUMBLE] stop failed", e);
    }
}

function clamp01(v) {
    if (!Number.isFinite(v)) return 0;
    return Math.max(0, Math.min(1, v));
}

function encodeButtons(gamepad) {
    let b = 0;

    if (gamepad.buttons[0]?.pressed) b |= 1 << 12;
    if (gamepad.buttons[1]?.pressed) b |= 1 << 13;
    if (gamepad.buttons[2]?.pressed) b |= 1 << 14;
    if (gamepad.buttons[3]?.pressed) b |= 1 << 15;

    if (gamepad.buttons[4]?.pressed) b |= 1 << 8;
    if (gamepad.buttons[5]?.pressed) b |= 1 << 9;

    if (gamepad.buttons[8]?.pressed) b |= 1 << 5;
    if (gamepad.buttons[9]?.pressed) b |= 1 << 4;

    if (gamepad.buttons[10]?.pressed) b |= 1 << 6;
    if (gamepad.buttons[11]?.pressed) b |= 1 << 7;

    if (gamepad.buttons[12]?.pressed) b |= 1 << 0;
    if (gamepad.buttons[13]?.pressed) b |= 1 << 1;
    if (gamepad.buttons[14]?.pressed) b |= 1 << 2;
    if (gamepad.buttons[15]?.pressed) b |= 1 << 3;

    // PS / Xbox ボタンはクライアント・ホストどちらでも作動し、誤操作の原因になるのでホストには送らない無効
    // if (gamepad.buttons[16]?.pressed) b |= 1 << 10;

    if (gamepad.buttons[17]?.pressed) b |= 1 << 11;

    return b;
}

// ==================================
// コントローラー制御 End
// ==================================

function setupDataChannel(channel, controller) {
    inputDc = channel;
    controllerDc = controller;

    inputDc.onopen = () => {
        console.log("Input DataChannel open");
        log("Input DataChannel open");
    };

    inputDc.onclose = () => {
        console.log("Input DataChannel closed");
        log("Input DataChannel closed");
    };

    inputDc.onerror = (err) => {
        console.error("Input DataChannel error", err);
        log("Input DataChannel error", err);
    };

    inputDc.onmessage = async (ev) => {
        console.log("Input DataChannel message:", ev.data);

        if (typeof ev.data !== "string") { return; }

        let msg;
        try {
            msg = JSON.parse(ev.data);
        } catch {
            return;
        }

        if (msg.type === "dc_ping") {
            if (inputDc?.readyState === "open") {
                inputDc.send(JSON.stringify({
                    type: "dc_pong",
                    t: msg.t,
                    receivedAt: Date.now()
                }));
            }
            return;
        }

        if (msg.type === "dc_pong") {
            dcLastPongAt = Date.now();
            console.log("[dc.keepalive] pong", {
                rtt: dcLastPongAt - msg.t,
                dcBufferedAmount: inputDc.bufferedAmount,
            });
            return;
        }
    };

    controllerDc.onopen = () => {
        console.log("Controller DataChannel open");
        log("Controller DataChannel open");
        startGamepadLoop();
    };

    controllerDc.onclose = () => {
        console.log("Controller DataChannel close");
        log("Controller DataChannel close")
    }

    controllerDc.onerror = (err) => {
        console.error("Controller DataChannel error", err);
        log("[ERR] Controller DataChannel error", err);
    }

    controllerDc.onmessage = async (ev) => {
        console.log("Controller DataChannel message:", ev.data);

        if (typeof ev.data !== "string") { return; }

        let msg;
        try {
            msg = JSON.parse(ev.data);
        } catch {
            return;
        }

        if (msg.type === "rumble") {
            console.log("[RUMBLE] received from host", msg);
            await handleRumbleMessage(msg);
            return;
        }
    }
}

function getNoticesByLanguage() {
    const selectedLanguage = document.getElementById("language-select").value;
    switch (selectedLanguage) {
        case "ja-JP": return noticesJa;
        case "en-US": return noticesEn;
        default: return noticesJa;
    }
}

function setHorizontalScrollAnimation(container, element) {
    const overflow = element.scrollWidth - container.clientWidth;

    if (overflow > 0) {
        element.style.setProperty("--overflow", `${overflow + 50}px`);
        element.classList.add("notices-overflow");

        setTimeout(() => {
            element.style.setProperty("text-overflow", "clip");
            element.style.setProperty("overflow", "visible");
        }, 4000);
    }
}

function setNotices() {
    const notices = getNoticesByLanguage();

    if (notices) {
        const noticesList = document.getElementById("noticeList");
        if (noticesList == null) return;

        noticesList.innerHTML = "";

        let first = true;
        notices.forEach(notice => {
            const noticeContent = notice.split("@")[0];
            const div = document.createElement("div");
            const span = document.createElement("span");

            if (first) { div.classList.add("active"); first = false; }
            else div.classList.add("inactive");
            
            span.textContent = noticeContent;
            div.appendChild(span)
            noticesList.appendChild(div);

            setHorizontalScrollAnimation(div, span);
        });

        if (notices.length > 1) {
            const Items = document.getElementById("noticeList").children;
            let currentIndex = 0;
            
            setInterval(() => {
                const currentSpan = Items[currentIndex].children[0];
                Items[currentIndex].classList.remove("active");
                currentSpan.classList.remove("notices-overflow");
                currentSpan.style = null;
                Items[currentIndex].classList.add("inactive");
                currentIndex = (currentIndex + 1) % Items.length;

                const nextItem = Items[currentIndex];
                const nextSpan = nextItem.children[0];
                nextItem.classList.remove("inactive");
                nextItem.classList.add("active");
                setHorizontalScrollAnimation(nextItem, nextSpan);
            }, 10000);
        } else if (notices < 1) {
            const ticker = document.getElementById("ticker");
            ticker.remove();
        }
    }
}

async function load() {
    loadLanguage();
    await startWakeLock();
    await fetchTtlSeconds();
    await fetchNotifications();
    tokenTimeoutMessage = setTimeout(await tokenTimeout, ttlSeconds);
    
    setNotices();

    const selectedSubtitle = splashSubtitles[getRandomInt(splashSubtitles.length)];
    const subtitle = document.getElementById("splashSubtitle");
    subtitle.textContent = selectedSubtitle;

    connectStatus = document.getElementById("status");

    videoEl = document.getElementById("video");
    const fullscreenBtn = document.getElementById("fullscreenBtn");
    const audioOn = document.getElementById("audioOn");
    const audioOff = document.getElementById("audioOff");

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

    audioEl = document.createElement("audio");
    audioEl.autoplay = true;
    audioEl.playsInline = true;
    audioEl.controls = false;
    audioEl.style.display = "none";
    audioEl.muted = true;
    audioEl.volume = 1.0;
    document.body.appendChild(audioEl);

    if (audioOn) {
        audioOn.addEventListener("change", async () => {
            if (!audioOn.checked) return;
            await onEnableAudio();
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

    setAudioUiState("off");

    const sessionIdEl = document.getElementById("sessionId");
    if (sessionIdEl && !sessionIdEl.value) {
        sessionIdEl.value = generateSessionId();
    }

    document.addEventListener("fullscreenchange", () => {
        const btn = document.getElementById("fullscreenBtn");
        if (!btn) return;
        btn.textContent = document.fullscreenElement ? locale.exitFullScreen : locale.fullScreen;
    });

    try {
        await connect();
    } catch (e) {
        console.error("initial connect failed", e);
        log("[ERR] Initial connect failed", e);
    }

    startSplash();
};

function getRandomInt(max) {
    return Math.floor(Math.random() * max);
}

function startSplash() {
    const splash = document.getElementById("splash");
    if (!splash) return;
    splash.classList.add("show");

    setTimeout(() => {
        splash.classList.add("hide");
        splash.classList.remove("show");
    }, 1000);

    setTimeout(() => {
        splash.remove();
    }, 2400);
}

async function tokenTimeout() {
    await releaseWakeLock();
    if (!alert(locale.invalidToken)) {
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

function showToast(message, type = "info") {
    let toast = document.getElementById("toast");
    if (!toast) {
        toast = document.createElement("div");
        toast.id = "toast";
        document.body.appendChild(toast);
    }

    toast.textContent = message;
    toast.dataset.type = type;
    toast.classList.add("show");

    clearTimeout(showToast._timer);
    showToast._timer = setTimeout(() => {
        toast.classList.remove("show");
    }, 1800);
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

function setAudioUiState(state) {
    const area = document.getElementById("makeImg");
    if (!area) return;
    area.dataset.audioState = state;
}

async function copySessionId() {
    const btn = document.getElementById("copySessionIdBtn");
    const text = document.getElementById("sessionId")?.value || sessionId || "";

    if (!btn) {
        try {
            await navigator.clipboard.writeText(text);
            showToast(locale.copiedSessionId);
        } catch (e) {
            console.warn("failed to copy sessionId", e);
            log("[WARN] failed to copy sessionId", e);
            showToast(locale.failedToCopySessionId, "error");
        }
        return;
    }

    const originalText = btn.dataset.originalText || btn.textContent || "Copy";
    btn.dataset.originalText = originalText;

    clearTimeout(copySessionResetTimer);
    btn.classList.remove("copy-success", "copy-error");

    try {
        await navigator.clipboard.writeText(text);

        btn.textContent = "✓";
        btn.classList.add("copy-success");
        showToast(locale.copiedSessionId);

        copySessionResetTimer = setTimeout(() => {
            btn.classList.remove("copy-success");
            btn.textContent = originalText;
        }, 1600);
    } catch (e) {
        console.warn("failed to copy sessionId", e);
        log("[WARN] failed to copy sessionId", e);

        btn.textContent = "Failed";
        btn.classList.add("copy-error");
        showToast(locale.failedToCopySessionId, "error");

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
        log("[CLIENT] sent force_keyframe:", reason);
    } catch (e) {
        console.warn("[CLIENT] force_keyframe send failed:", e);
        log("[WARN] [CLIENT] force_keyframe send failed:", e);
    }
}

window.onunload = async function () {
  if (!sessionEnded) {
    await fetchSessionEnd();
    sessionEnded = true;
  }
};

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

async function fetchSessionStart() {
    const res = await fetch(buildSessionUrl("/start-twins-remote"), {
        method: "POST"
    });

    if (!res.ok) {
        const text = await res.text();
        throw new Error("failed to fetch start twins remote: " + text);
    }
}
async function fetchSessionEnd() {
    const res = await fetch(buildSessionUrl("/end-twins-remote"), {
        method: "POST"
    });

    if (!res.ok) {
        const text = await res.text();
        throw new Error("failed to fetch end twins remote: " + text);
    }
}

async function fetchTtlSeconds() {
    const res = await fetch("/ttl-seconds");

    if (!res.ok) { return; }

    const json = await res.json();
    ttlSeconds = json.ttlSeconds * 1000;
}

async function fetchNotifications() {
    const res = await fetch("/notifications")

    if (!res.ok) { return; }

    const json = await res.json();
    console.log(json);
    noticesJa = json.japanese;
    noticesEn = json.english;
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

    if (pc.remoteDescription &&
        pc.remoteDescription.type === json.type &&
        pc.remoteDescription.sdp === json.sdp
    ) {
        return true;
    }


    await pc.setRemoteDescription(json);
    console.log("remote answer set / updated");
    await flushPendingRemoteCandidates();
    return true;
}

function startAnswerPolling() {
    if (answerPollIntervalId) {
        clearInterval(answerPollIntervalId);
    }

    answerPollIntervalId = setInterval(async () => {
        if (!pc) return;

        try {
            const ok = await pollAnswerOnce();

            if (
                ok &&
                pc.signalingState === "stable"
            ) {
                clearInterval(answerPollIntervalId);
                answerPollIntervalId = null;
            }
        } catch (e) {
            console.warn("pollAnswer failed", e);
            log("[WARN] pollAnswer failed", e);
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
        log("[WARN] pollHostCandidates failed", e)
    }
}

async function flushPendingRemoteCandidates() {
    if (!pc || !pc.remoteDescription) return;

    while (pendingRemoteCandidates.length > 0) {
        const c = pendingRemoteCandidates.shift();
        try {
            await pc.addIceCandidate(new RTCIceCandidate(c));
            console.log("Queued host ICE candidate added:", c);
            log("Queued host ICE candidate added:", c);
        } catch (e) {
            console.warn("failed to add queued host candidate", e, c);
            log("[WARN] failed to add queued host candidate", e, c)
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
        log("[WARN] resetSession failed", e);
    }
}

async function postClientNetworkState(mode) {
    await postJson(buildSessionUrl("/client-network-state"), {
        mode,
        ts: Date.now()
    });
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
                log("[ERR] [monitorVideoStats error]", e);
            }
        }

        await sleep(1000);
    }
}

function sleep(ms) {
    return new Promise(r => setTimeout(r, ms));
}

function setStatus(state, text) {
    const el = document.getElementById("status");
    if (!el) return;

    el.className = "";
    el.id = "status";
    el.classList.add(state);
    el.textContent = text;
}

async function connect() {
    cleanupPeerConnection();

    sessionId = getOrCreateSessionId();
    log("SESSION ID:", sessionId);

    setStatus("connecting", locale.preparingConnection);
    currentStatus = status.preparingConnection;

    await fetchSessionStart();

    const config = await fetchWebRtcConfig();
    log("ICE CONFIG:", config);

    pc = new RTCPeerConnection(buildRtcConfig(config));

    if (audioEl) {
        audioEl.srcObject = remoteAudioStream;
        audioEl.muted = true;
        audioEl.volume = 1.0;
    }

    pc.addTransceiver("video", { direction: "recvonly" });
    pc.addTransceiver("audio", { direction: "recvonly" });

    pc.ontrack = async (event) => {
        console.log("ontrack", event.track.kind, event.streams);

        if (event.track.kind === "video") {
            console.log("[VIDEO] track received:", event.track.id);

            if (event.receiver) {
                try {
                    if ("playoutDelayHint" in event.receiver) {
                        event.receiver.playoutDelayHint = 0.00;
                    }
                    if ("jitterBufferTarget" in event.receiver) {
                        event.receiver.jitterBufferTarget = 0.00;
                    }
                } catch (e) {
                    console.warn("video receiver tuning failed", e);
                    log("[WARN] video receiver tuning failed", e);
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
            }, 250);

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
                    log("[WARN] audio receiver tuning failed", e);
                }
            }

            if (audioEl) {
                audioEl.srcObject = remoteAudioStream;
                await audioEl.play().catch((e) => {
                    console.warn("audio.play rejected", e);
                    log("[WARN] audio.play rejected", e);
                });
            }
        }
    };

    pc.ondatachannel = (e) => {
        console.log("DataChannel received:", e.channel.label);
        setupDataChannel(e.channel);
    };

    pc.onicecandidate = async (e) => {
        console.log("local candidate:", e.candidate);

        if (!e.candidate) {
            console.log("local ICE gathering finished");
            return;
        }

        const cand = e.candidate.candidate;
        log("CANDIDATE: ", e.candidate.candidate);
        console.log("CANDIDATE: ", cand);

        if (cand.includes("typ relay")) {
            log("✅ relay candidate generated");
        }

        try {
            await postClientCandidate(e.candidate.toJSON());
            log("client candidate posted");
        } catch (err) {
            console.warn("failed to post client candidate", err);
            log("[WARN] failed to post client candidate", err);
        }
    };

    pc.onicecandidateerror = (e) => {
        console.warn("ICE candidate warning", e);
        log("[WARN] ICE candidate warning", e);
    };

    pc.oniceconnectionstatechange = async () => {
        console.log("iceConnectionState =", pc.iceConnectionState);

        if (pc && inputDc && controllerDc) {
            logPcState("pc.iceconnectionstatechange", pc, inputDc, controllerDc);
        }

        if (pc.iceConnectionState === "checking") {
            setStatus("connecting", locale.connecting);
            currentStatus = status.connecting;
        }

        if (pc.iceConnectionState === "connected" || pc.iceConnectionState === "completed") {
            setStatus("connected", locale.connected);
            currentStatus = status.connected;
            clearTimeout(tokenTimeoutMessage);

            if (!rtcSummaryIntervalId) {
                rtcSummaryIntervalId = setInterval(() => {
                    logRtcSummary(pc).catch(console.error);
                }, 5000);
            }

            await logSelectedCandidatePair(pc);
        }

        if (pc.iceConnectionState === "disconnected") {
            setStatus("disconnected", locale.disconnected);
            currentStatus = status.disconnected;

            if (!sessionEnded) {
                await fetchSessionEnd();
                sessionEnded = true;
            }
        }

        if (pc.iceConnectionState === "failed") {
            setStatus("failed", locale.connectionFailure);
            currentStatus = status.connectionFailure;

            if (!sessionEnded) {
                await fetchSessionEnd();
                sessionEnded = true;
            }
        }
    };

    pc.onconnectionstatechange = () => {
        if (pc && inputDc && controllerDc) {
            logPcState("pc.connectionstatechange", pc, inputDc, controllerDc);

            if (pc.connectionState === "connected") {
                if (disconnectedTimer) {
                    clearTimeout(disconnectedTimer);
                    disconnectedTimer = null;
                }

                return;
            }

            if (pc.connectionState === "disconnected") {
                if (disconnectedTimer) return;

                disconnectedTimer = setTimeout(() => {
                    if (pc.connectionState === "disconnected") {
                        handleDataChannelDead("pc disconnected timeout");
                    }
                }, 15000);

                return;
            }

            if (pc.connectionState === "failed") {
                handleDataChannelDead("pc failed");
            }
        }
    };

    pc.onicegatheringstatechange = () => {
        if (pc && inputDc && controllerDc) {
            logPcState("pc.connectionstatechange", pc, inputDc, controllerDc);
        }
    };

    pc.onsignalingstatechange = () => {
        if (pc && inputDc && controllerDc) {
            logPcState("pc.connectionstatechange", pc, inputDc, controllerDc);
        }
    };

    const originalPcClose = pc.close.bind(pc);
    pc.close = () => {
        console.warn("pc.close called", new Error().stack);
        log("[WARN] pc.close called", new Error().stack);
        originalPcClose();
    };

    const inputChannel = pc.createDataChannel("input", {
        ordered: false
    });

    const controllerChannel = pc.createDataChannel("controller", {
        ordered: false
    });
    setupDataChannel(inputChannel, controllerChannel);

    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);

    await resetSession();
    await postOffer(pc.localDescription?.toJSON ? pc.localDescription.toJSON() : pc.localDescription);

    setStatus("waiting", locale.waitingForHost);
    currentStatus = status.waitingForHost;

    seenRemoteCandidates.clear();
    pendingRemoteCandidates = [];
    startHostCandidatePolling();
    startAnswerPolling();

    startStatsMonitor();
    startVideoWatchdog();
    startRelayMonitoring();
}

async function maybeRestartIceByRtt(selectedPair) {
    if (!selectedPair || !pc) return;
    if (
        pc.iceConnectionState !== "connected" &&
        pc.iceConnectionState !== "completed"
    ) {
        return;
    }

    const stats = await pc.getStats();

    if (isRelayConnection(stats, selectedPair)) {
        return;
    }

    const rtt = selectedPair.currentRoundTripTime;
    if (typeof rtt !== "number") return;

    if (rtt >= RTT_BAD_SEC) {
        badRttCount++;
    } else {
        badRttCount = 0;
    }

    if (badRttCount < BAD_RTT_LIMIT) return;

    const now = Date.now();
    if (now - lastIceRestartAt < ICE_RESTART_COOLDOWN_MS) return;

    badRttCount = 0;
    lastIceRestartAt = now;

    console.warn("[ICE] RTT degraded, restarting ICE", rtt);
    log("[WARN] [ICE] RTT degraded, restarting ICE", rtt);

    const offer = await pc.createOffer({ iceRestart: true });
    await pc.setLocalDescription(offer);

    seenRemoteCandidates.clear();
    pendingRemoteCandidates = [];

    await postOffer(pc.localDescription.toJSON());
    startAnswerPolling();

    setTimeout(() => {
        sendForceKeyframe("after_ice_restart");
    }, 800);
}

function urlsOf(server) {
    return Array.isArray(server.urls) ? server.urls : [server.urls];
}

function isTurnServer(server) {
    return urlsOf(server).some(u => u.startsWith("turn:") || u.startsWith("turns:"));
}

function isStunServer(server) {
    return urlsOf(server).some(u => u.startsWith("stun:"));
}

function buildRtcConfig(config) {
    switch (ICE_MODE) {
        case "relay-test":
            return {
                iceServers: config.iceServers.filter(isTurnServer),
                iceTransportPolicy: "relay",
                bundlePolicy: "max-bundle",
                rtcpMuxPolicy: "require"
            }
        
        case "stun-first":
            return {
                iceServers: config.iceServers.filter(isStunServer),
                iceTransportPolicy: "all",
                bundlePolicy: "max-bundle",
                rtcpMuxPolicy: "require"
            };

        default:
            return {
                iceServers: config.iceServers,
                iceTransportPolicy: "all",
                bundlePolicy: "max-bundle",
                rtcpMuxPolicy: "require"
            }
    }
}

function isRelayConnection(stats, selectedPair) {
    const local = stats.get(selectedPair.localCandidateId);
    const remote = stats.get(selectedPair.remoteCandidateId);

    return local?.candidateType === "relay" || remote?.candidateType == "relay";
}

function startRelayMonitoring() {
    const candidateTypeMessenger = document.getElementById("candidateTypeMessenger");
    const limitation = document.getElementById("limitation");

    if (relayMonitorIntervalId) {
        clearInterval(relayMonitorIntervalId);
    }

    relayMonitorIntervalId = setInterval(async () => {
        if (!pc) return;

        try {
            const stats = await pc.getStats(null);
            let selectedPair = null;

            stats.forEach((report) => {
                if (report.type === "transport" && report.selectedCandidatePairId) {
                    selectedPair = stats.get(report.selectedCandidatePairId);
                }
            });

            if (!selectedPair) return;

            const relay = isRelayConnection(stats, selectedPair);
            const mode = relay ? "relay" : "direct";

            if (relay) {
                if (candidateTypeMessenger) {
                    candidateTypeMessenger.textContent = "🟡 TURN Relay";
                }

                if (limitation) {
                    limitation.textContent = "720p30 limited";
                }
            } else {
                if (candidateTypeMessenger) {
                    candidateTypeMessenger.textContent = "🟢 Direct P2P";
                }

                if (limitation) {
                    limitation.textContent = "";
                }
            }

            if (lastPostedNetworkMode !== mode) {
                lastPostedNetworkMode = mode;

                await postClientNetworkState(mode);

                console.log("[CLIENT NETWORK STATE] posted:", mode);
            }
        } catch (e) {
            console.warn("relay monitoring failed", e);
            log("[WARN] relay monitoring failed", e);
        }
    }, 1000);
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

    if (relayMonitorIntervalId) {
        clearInterval(relayMonitorIntervalId);
        relayMonitorIntervalId = null;
    }

    seenRemoteCandidates.clear();
    pendingRemoteCandidates = [];
    lastPostedNetworkMode = null;

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

    if (streamSource) {
        streamSource = null;
    }

    if (inputDc) {
        try {
            inputDc.close();
        } catch (_) {}
        inputDc = null;
    }

    if (controllerDc) {
        try {
            controllerDc.close();
        } catch (_) {}
        controllerDc = null;
    }

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
    lateDroppedFrames = 0;
    forceKeyframeCooldownUntil = 0;

    clearCanvas();
}

async function onEnableAudio() {
    try {
        setAudioUiState("pending");

        if (audioEl) {
            audioEl.muted = false;
            audioEl.volume = 1.0;
            await audioEl.play();
        }

        const on = document.getElementById("audioOn");
        if (on) on.checked = true;

        setAudioUiState("on");
        showToast(locale.validateAudio);
    } catch (e) {
        console.error("failed to enable audio", e);
        log("[ERR] failed to enable audio", e);

        const off = document.getElementById("audioOff");
        if (off) off.checked = true;

        setAudioUiState("off");
        showToast(locale.failedToValidateAudio, "error");
    }
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
        log("[ERR] fullscreen failed", e);
    }
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
        log("[WARN] MediaStreamTrackProcessor is not available in this browser/context");
        log("[WARN] falling back to direct video element playback");

        if (videoEl) {
            videoEl.style.display = "block";
            streamSource = new MediaStream([track]);
            videoEl.srcObject = streamSource;
            await videoEl.play().catch((e) => {
                console.warn("video.play rejected", e);
                log("[WARN] video.play rejected", e);
            });
        }
        return;
    }

    if (videoEl) {
        videoEl.style.display = "none";
        videoEl.srcObject = null;
        streamSource = null;
    }

    processorAbort = { aborted: false };
    videoProcessor = new MediaStreamTrackProcessor({ track });
    videoReader = videoProcessor.readable.getReader();

    firstVideoFrameArrived = false;
    lastFrameArrivedAt = 0;
    receivedFrames = 0;
    droppedFrames = 0;
    renderedFrames = 0;
    lateDroppedFrames = 0;

    console.log("[VIDEO] startVideoTrackProcessor");
    log("[VIDEO] startVideoTrackProcessor");

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
                log("[ERR] videoReader.read failed", e);
                sendForceKeyframe("video_reader_error");
            }
        } finally {
            log("[VIDEO] processor loop ended");
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
        log("[ERR] drawImage failed", e);
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

                const age = nowMs() - lastFrameArrivedAt;
                if (age > MAX_FRAME_AGE_MS) {
                    lateDroppedFrames++;
                    closeFrameSafe(frame);
                    continue;
                }

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
        log("[ERR] [VIDEO] render loop failed", e);
        sendForceKeyframe("render_loop_error");
    });
}

async function getInboundVideoStats() {
    if (!pc) return null;

    const stats = await pc.getStats();

    for (const report of stats.values()) {
        if (
            report.type === "inbound-rtp" &&
            report.kind === "video" &&
            !report.isRemote
        ) {
            return report;
        }
    }

    return null;
}

function startVideoWatchdog() {
    if (videoWatchdogIntervalId) {
        clearInterval(videoWatchdogIntervalId);
    }

    videoWatchdogIntervalId = setInterval(async () => {
        if (!pc) return;
        if (pc.connectionState !== "connected" && pc.connectionState !== "connecting") return;

        const now = nowMs();

        if (!firstVideoFrameArrived) {
            if (now - lastForceKeyframeAt > FORCE_KEYFRAME_COOLDOWN_MS) {
                lastForceKeyframeAt = now;
                sendForceKeyframe("waiting_first_frame");
            }
            return;
        }

        const stats = await getInboundVideoStats();
        if (!stats) return;

        const framesDecoded = stats.framesDecoded ?? 0;
        const bytesReceived = stats.bytesReceived ?? 0;
        const packetsReceived = stats.packetsReceived ?? 0;
        const freezeCount = stats.freezeCount ?? 0;
        const totalFreezesDuration = stats.totalFreezesDuration ?? 0;

        const age = now - lastFrameArrivedAt;

        const framesNotMoving = framesDecoded <= lastVideoStats.framesDecoded;

        const bytesNotMoving = bytesReceived <= lastVideoStats.framesReceived;

        const packetsNotMoving = packetsReceived <= lastVideoStats.packetsReceived;

        const freezeIncreased = 
            freezeCount > lastVideoStats.freezeCount ||
            totalFreezesDuration > lastVideoStats.totalFreezesDuration;
        
        const reallyStalled = 
            age > VIDEO_STALL_MS &&
            framesNotMoving &&
            bytesNotMoving &&
            packetsNotMoving;
        
        const reallyPlaybackDelay = 
            age > CORRECT_PLAYBACK_DELAY_MS &&
            framesNotMoving &&
            bytesNotMoving &&
            packetsNotMoving;
        
        const browserDetectedFreeze =
            age > VIDEO_STALL_MS &&
            freezeIncreased &&
            framesNotMoving;
        
        const browserDetectedSoftFreeze =
            age > CORRECT_PLAYBACK_DELAY_MS &&
            freezeIncreased &&
            framesNotMoving;
        
        if (
            (reallyStalled || browserDetectedFreeze) &&
            now- lastForceKeyframeAt > FORCE_KEYFRAME_COOLDOWN_MS
        ) {
            console.warn("[WARN] [VIDEO WATCHDOG] stall detected", {
                age: Math.floor(age),
                framesDecoded,
                bytesReceived,
                packetsReceived,
                freezeCount,
                totalFreezesDuration,
                reallyStalled,
                browserDetectedFreeze
            });

            log("[WARN] [VIDEO WATCHDOG] stall detected", {
                age: Math.floor(age),
                framesDecoded,
                bytesReceived,
                packetsReceived,
                freezeCount,
                totalFreezesDuration,
                reallyStalled,
                browserDetectedFreeze
            });

            lastForceKeyframeAt = now;
            sendForceKeyframe("video_stall");
        } else if (
            (reallyPlaybackDelay || browserDetectedSoftFreeze) &&
            now- lastForceKeyframeAt > FORCE_KEYFRAME_COOLDOWN_MS
        ) {
            console.warn("[VIDEO WATCHDOG] playback delay detected", {
                age: Math.floor(age),
                framesDecoded,
                bytesReceived,
                packetsReceived,
                freezeCount,
                totalFreezesDuration,
                reallyStalled,
                browserDetectedFreeze
            });

            log("[WARN] [VIDEO WATCHDOG] playback delay detected", {
                age: Math.floor(age),
                framesDecoded,
                bytesReceived,
                packetsReceived,
                freezeCount,
                totalFreezesDuration,
                reallyStalled,
                browserDetectedFreeze
            });

            correctPlaybackDelay(firstVideoFrameArrived ? Math.floor(nowMs() - lastFrameArrivedAt) : null)
        }

        lastVideoStats = {
            checkedAt: now,
            framesDecoded,
            bytesReceived,
            packetsReceived,
            freezeCount,
            totalFreezesDuration
        };
    }, VIDEO_WATCHDOG_INTERVAL_MS);
}

function correctPlaybackDelay(latestFrameAgeMs) {
    if (!videoEl) return;

    if (latestFrameAgeMs > 2000) {
        videoEl.playbackRate = 1.06;
    } else if (latestFrameAgeMs > 1500) {
        videoEl.playbackRate = 1.04;
    } else if (latestFrameAgeMs > 800) {
        videoEl.playbackRate = 1.03;
    } else {
        videoEl.playbackRate = 1.0;
    }
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
            let localCandidateId = null;
            let remoteCandidateId = null;

            stats.forEach((report) => {
                if (report.type === "candidate-pair" && report.state === "succeeded" && report.nominated) {
                    selectedCandidatePair = report;
                    localCandidateId = report.localCandidateId;
                    remoteCandidateId = report.remoteCandidateId;
                }
                if (!selectedCandidatePair && report.type === "transport" && report.selectedCandidatePairId) {
                    selectedCandidatePair = stats.get(report.selectedCandidatePairId);
                    localCandidateId = selectedCandidatePair?.localCandidateId;
                    remoteCandidateId = selectedCandidatePair?.remoteCandidateId;
                }
            });

            let localCandidate = null;
            let remoteCandidate = null;
            if (localCandidateId) localCandidate = stats.get(localCandidateId);
            if (remoteCandidateId) remoteCandidate = stats.get(remoteCandidateId);

            stats.forEach((report) => {
                if (report.type === "inbound-rtp" && report.kind === "video") {
                    const bitrateBps = estimateVideoBitrateBps(report);
                    const isRelay = selectedCandidatePair
                        ? isRelayConnection(stats, selectedCandidatePair)
                        : false;
                    
                    if (updateCapViolationScore(report, bitrateBps, isRelay)) {
                        disconnectForCapViolation(report, bitrateBps);
                        return;
                    }
                    
                    console.log("[video stats]", {
                        packetsReceived: report.packetsReceived,
                        bytesReceived: report.bytesReceived,
                        framesReceived: report.framesReceived,
                        framesDecoded: report.framesDecoded,
                        framesDropped: report.framesDropped,
                        frameWidth: report.frameWidth,
                        frameHeight: report.frameHeight,
                        keyFramesDecoded: report.keyFramesDecoded,
                        totalDecodeTime: report.totalDecodeTime,
                        jitter: report.jitter,
                        jitterBufferDelay: report.jitterBufferDelay,
                        jitterBufferEmittedCount: report.jitterBufferEmittedCount,
                        freezeCount: report.freezeCount,
                        totalFreezesDuration: report.totalFreezesDuration,
                        packetsLost: report.packetsLost,
                        processorReceivedFrames: receivedFrames,
                        processorDroppedFrames: droppedFrames,
                        processorRenderedFrames: renderedFrames,
                        processorLateDroppedFrames: lateDroppedFrames,
                        latestFrameAgeMs: firstVideoFrameArrived ? Math.floor(nowMs() - lastFrameArrivedAt) : null,
                    });
                }

                if (report.type === "inbound-rtp" && report.kind === "audio") {
                    console.log("[audio stats]", {
                        packetsReceived: report.packetsReceived,
                        bytesReceived: report.bytesReceived,
                        jitter: report.jitter,
                        jitterBufferDelay: report.jitterBufferDelay,
                        packetsLost: report.packetsLost
                    });
                }
            });

            if (selectedCandidatePair) {
                await maybeRestartIceByRtt(selectedCandidatePair);
                console.log("[candidate pair]", {
                    currentRoundTripTime: selectedCandidatePair.currentRoundTripTime,
                    availableIncomingBitrate: selectedCandidatePair.availableIncomingBitrate,
                    availableOutgoingBitrate: selectedCandidatePair.availableOutgoingBitrate,
                    bytesReceived: selectedCandidatePair.bytesReceived,
                    bytesSent: selectedCandidatePair.bytesSent,
                    localCandidateType: localCandidate?.candidateType,
                    localCandidateProtocol: localCandidate?.protocol,
                    localCandidateAddress: localCandidate?.address,
                    localCandidatePort: localCandidate?.port,
                    remoteCandidateType: remoteCandidate?.candidateType,
                    remoteCandidateProtocol: remoteCandidate?.protocol,
                    remoteCandidateAddress: remoteCandidate?.address,
                    remoteCandidatePort: remoteCandidate?.port
                });
            }
        } catch (e) {
            console.error("getStats failed", e);
            log("[ERR] getStats failed", e);
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
        packetsReceived: inboundVideo?.packetsReceived,
        bytesReceived: inboundVideo?.bytesReceived,
        packetsLost: inboundVideo?.packetsLost,
        jitter: inboundVideo?.jitter,
        framesReceived: inboundVideo?.framesReceived,
        framesDecoded: inboundVideo?.framesDecoded,
        framesPerSecond: inboundVideo?.framesPerSecond,
        freezeCount: inboundVideo?.freezeCount,
        totalFreezeDuration: inboundVideo?.totalFreezeDuration,
        jitterBufferDelay: inboundVideo?.jitterBufferDelay,
        jitterBufferEmittedCount: inboundVideo?.jitterBufferEmittedCount,
        processorReceivedFrames: receivedFrames,
        processorDroppedFrames: droppedFrames,
        processorRenderedFrames: renderedFrames,
        processorLateDroppedFrames: lateDroppedFrames,
        latestFrameAgeMs: firstVideoFrameArrived ? Math.floor(nowMs() - lastFrameArrivedAt) : null,
    });
}


function logPcState(prefix, pc, inputDc, controllerDc) {
    console.log(`[${prefix}]`, {
        time: new Date().toISOString(),
        pcConnectionState: pc?.connectionState,
        iceConnectionState: pc?.iceConnectionState,
        iceGatheringState: pc?.iceGatheringState,
        signalingState: pc?.signalingState,
        inputDcReadyState: inputDc?.readyState,
        inputDcBufferedAmount: inputDc?.bufferedAmount,
        controllerDcReadyState: controllerDc?.readyState,
        controllerDcBufferedAmount: controllerDc?.bufferedAmount,
    });
}

// ==========================
// Cap 外し予防 Start
// ==========================
function estimateVideoBitrateBps(report) {
    const now = performance.now();

    if (lastVideoBytes == null || lastVideoBytesAt == null) {
        lastVideoBytes = report.bytesReceived;
        lastVideoBytesAt = now;
        return null;
    }

    const deltaBytes = report.bytesReceived - lastVideoBytes;
    const deltaMs = now - lastVideoBytesAt;

    lastVideoBytes = report.bytesReceived;
    lastVideoBytesAt = now;

    if (deltaMs <= 0 || deltaBytes < 0) return null;

    return (deltaBytes * 8 * 1000) / deltaMs;
}

function updateCapViolationScore(report, bitrateBps, isRelay) {
    if (!isRelay) {
        capViolationScore = 0;
        return false;
    }

    const width  = report.frameWidth || 0;
    const height = report.frameHeight || 0;
    const fps    = report.framesPerSecond || 0;

    let scoreAdd = 0;

    if (width > RELAY_CAP_WIDTH || height > RELAY_CAP_HEIGHT) {
        scoreAdd += 4;
    }

    if (fps > RELAY_CAP_FPS + FPS_MARGIN) {
        scoreAdd += 3;
    }

    if (scoreAdd > 0) {
        capViolationScore += scoreAdd;
    } else {
        capViolationScore = Math.max(0, capViolationScore - 1);
    }

    return capViolationScore >= 12
}

function disconnectForCapViolation(report, bitrateBps) {
    console.warn("[CAP VIOLATION] disconnecting", {
        width: report.frameWidth,
        height: report.frameHeight,
        fps: report.framesPerSecond,
        bitrateMbps: bitrateBps ? bitrateBps / 1_000_000 : null
    });
    log("[WARN] [CAP VIOLATION] disconnecting", {
        width: report.frameWidth,
        height: report.frameHeight,
        fps: report.framesPerSecond,
        bitrateMbps: bitrateBps ? bitrateBps / 1_000_000 : null
    });

    setStatus("failed", locale.capViolationWarning);
    currentStatus = status.capViolationWarning;

    log("!!! [WARN] Cap Violation is detected !!!");
    log("!!! Disconnected from the host !!!");

    try {
        pc?.close();
    } catch(_) {}

    pc = null;
}
// ==========================
// Cap 外し予防 End
// ==========================

function stopDataChannelKeepalive() {
    if (dcKeepaliveTimer) {
        clearInterval(dcKeepaliveTimer);
        dcKeepaliveTimer = null;
    }
}

function handleDataChannelDead(reason) {
    if (reconnecting) return;
    reconnecting = true;

    console.warn("[dc.dead]", reason);
    log("[WARN] [dc.dead]", reason);

    stopDataChannelKeepalive();

    try {
        if (inputDc && inputDc.readyState !== "closed") {
            inputDc.close();
        }
        if (controllerDc && controllerDc.readyState !== "closed") {
            controllerDc.close();
        }
    } catch {}

    try {
        if (pc && pc.signalingState !== "closed") {
            pc.close();
        }
    } catch {}
}

// ===========================
// 多言語対応 Start
// ===========================
const locales = [
    { name: "日本語", value: "ja-JP"},
    { name: "English", value: "en-US"}
]

let locale = japanese;
let isLanguageInitialized = false;

function getLocale() {
    const selectedLanguage = document.getElementById("language-select").value;
    
    switch (selectedLanguage) {
        case "ja-JP": return japanese;
        case "en-US": return english;
    }

    return japanese;
}

function setLanguageList() {
    const languageSelect = document.getElementById("language-select");
    locales.forEach(locale => {
        const languageOption = document.createElement("option");
        
        languageOption.text = locale.name;
        languageOption.value = locale.value;

        languageSelect.add(languageOption, null);
    });
}

function getLocaleStatus() {
    switch (currentStatus) {
        case status.notConnected: return locale.notConnected;
        case status.preparingConnection: return locale.preparingConnection;
        case status.connecting: return locale.connecting;
        case status.waitingForHost: return locale.waitingForHost;
        case status.connected: return locale.connected;
        case status.disconnected: return locale.disconnected;
        case status.connectionFailure: return locale.connectionFailure;
        case status.capViolationWarning: return locale.capViolationWarning;
    }
}

function applyLanguage(language) {
    locale = language;

    const statusLabel = document.getElementById("status-label");
    const el = document.getElementById("status");
    const fullscreenBtn = document.getElementById("fullscreenBtn");
    const saveLogBtn = document.getElementById("save-log")

    statusLabel.textContent = locale.connectionStatus;
    el.textContent = getLocaleStatus();
    fullscreenBtn.textContent = locale.fullScreen;
    saveLogBtn.textContent = locale.saveLog;

    setNotices();
}

function getLanguageFromStorage() {
    switch (localStorage.getItem("language")) {
        case "ja-JP": return japanese;
        case "en-US": return english;
        default: return japanese;
    }
}

function setSelectElementValue(language) {
    const languageSelect = document.getElementById("language-select");
    const options = languageSelect.children;
    
    for (let i = 0; i < options.length; i++) {
        if (options[i].value === language) {
            languageSelect.selectedIndex = i;
            break;
        }
    }
}

function loadLanguage() {
    setLanguageList();

    const languageSelect = document.getElementById("language-select");
    setSelectElementValue(localStorage.getItem("language"));
    applyLanguage(getLanguageFromStorage());

    languageSelect.addEventListener("change", (event) => {
        if (!isLanguageInitialized) return;
        const languageSelect = document.getElementById("language-select");
        localStorage.setItem("language", languageSelect.value);

        applyLanguage(getLocale());
    });

    isLanguageInitialized = true;
}
// ===========================
// 多言語対応 End
// ===========================

// ===========================
// ログ保存 Start
// ===========================
function downloadLogToFile() {
    const log = document.getElementById("log").textContent;
    if (log == null) { return; }

    const blob = new Blob([log], { type: "text/plain" });

    // ログファイル(例：TwinsRemote_Client_2020_1_1_12_00_00.txt)
    const now = new Date();
    const fileName = "TwinsRemote_Client_" +
        now.getFullYear() + "_" + (now.getMonth() + 1) + "_" + now.getDate() +
        "_" + now.getHours() + "_" + now.getMinutes() + "_" + now.getSeconds() + ".txt";

    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob);
    a.download = fileName;
    a.click();

    a.remove();
}

window.downloadLogToFile = downloadLogToFile;
// ===========================
// ログ保存 End
// ===========================

// ===========================
// スリープ機能ロック Start
// ===========================
let wakeLock = null;

if (navigator.userAgent.match(/(iPhone|iPad|iPod|Android)/i)) {
    // mobile
    document.addEventListener("visibilitychange", async () => {
        if (document.visibilityState === "visible") {
            setTimeout(startWakeLock, 100);
        }

        if (document.visibilityState === "hidden") {
            setTimeout(releaseWakeLock, 100);
        }
    });
} else {
    window.addEventListener("focus", () => {
        setTimeout(startWakeLock, 100);
    });
    window.addEventListener("blur", async () => {
        setTimeout(releaseWakeLock, 100);
    });
}

async function startWakeLock() {
    log("Start ScreenWakeLock");
    wakeLock = await navigator.wakeLock.request("screen");
}

async function releaseWakeLock() {
    if (wakeLock == null) return;
    log("End ScreenWakeLock");
    await wakeLock.release();
}
// ===========================
// スリープ機能ロック End
// ===========================

load();
