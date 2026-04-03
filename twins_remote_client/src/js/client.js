let pc;
let dc;
let gamepadIndex = null;
let remoteStream = new MediaStream();

let statsIntervalId = null;
let freezeWatchdogId = null;
let lastVideoCurrentTime = 0;
let lastFrameCheckAt = 0;

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
    video.addEventListener("dblclick", () => {
        toggleFullscreen();
    });
});

/*
Connectボタン押下時の挙動
*/
async function connect() {
    cleanupPeerConnection();

    pc = new RTCPeerConnection({
        iceServers: [
            { urls: "stun:stun.l.google.com:19302" },
            {
                urls: [
                    "turn:54.199.209.243:3478?transport=udp"
                ],
                username: "test",
                credential: "password",
                credentialType: "password"
            }
        ],
        iceTransportPolicy: "relay",

        // bundle / rtcp mux を明示
        bundlePolicy: "max-bundle",
        rtcpMuxPolicy: "require"
    });

    const video = document.getElementById("video");

    // video要素の余計な遅延要因を減らす
    video.srcObject = remoteStream;
    video.autoplay = true;
    video.playsInline = true;
    video.muted = true;

    video.controls = false;
    video.disablePictureInPicture = true;
    video.style.transform = "translateZ(0)";
    video.style.backfaceVisibility = "hidden";

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

        if (!remoteStream.getTracks().some(t => t.id === event.track.id)) {
            remoteStream.addTrack(event.track);
        }

        event.track.onunmute = () => {
            console.log(`${event.track.kind} track unmuted`);
        };

        event.track.onended = () => {
            console.log(`${event.track.kind} track ended`);
        };

        // ブラウザ対応があれば低遅延寄りにする
        if (event.receiver) {
            try {
                if ("playoutDelayHint" in event.receiver) {
                    event.receiver.playoutDelayHint = 0.05; // 50ms目安
                    console.log("playoutDelayHint set to 0.05");
                }

                // 対応ブラウザのみ
                if ("jitterBufferTarget" in event.receiver) {
                    event.receiver.jitterBufferTarget = 50; // ms
                    console.log("jitterBufferTarget set to 50");
                }
            } catch (e) {
                console.warn("receiver tuning failed", e);
            }
        }

        try {
            await video.play();
            console.log("video.play resolved");
        } catch (e) {
            console.error("video.play rejected", e);
        }

        startVideoFreezeWatchdog(video);
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

    pc.oniceconnectionstatechange = () => {
        console.log("iceConnectionState =", pc.iceConnectionState);
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

    if (freezeWatchdogId) {
        clearInterval(freezeWatchdogId);
        freezeWatchdogId = null;
    }

    if (remoteStream) {
        for (const track of remoteStream.getTracks()) {
            remoteStream.removeTrack(track);
        }
    }

    if (dc) {
        try {
            dc.close();
        } catch (_) { }
        dc = null;
    }

    if (pc) {
        try {
            pc.close();
        } catch (_) { }
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

/*
Host ICEの入力
*/
async function onHostIce() {
    await addHostCandidate();
}

// HostのICE候補の受け取り
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

        // Binary
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

    if (gamepad.buttons[0].pressed) b |= 1 << 12;    // A
    if (gamepad.buttons[1].pressed) b |= 1 << 13;    // B
    if (gamepad.buttons[2].pressed) b |= 1 << 14;    // X
    if (gamepad.buttons[3].pressed) b |= 1 << 15;    // Y

    if (gamepad.buttons[4].pressed) b |= 1 << 8;     // LB
    if (gamepad.buttons[5].pressed) b |= 1 << 9;     // RB

    if (gamepad.buttons[8].pressed) b |= 1 << 5;     // BACK
    if (gamepad.buttons[9].pressed) b |= 1 << 4;     // START

    if (gamepad.buttons[10].pressed) b |= 1 << 6;    // LS
    if (gamepad.buttons[11].pressed) b |= 1 << 7;    // RS

    if (gamepad.buttons[12].pressed) b |= 1 << 0;    // UP
    if (gamepad.buttons[13].pressed) b |= 1 << 1;    // DOWN
    if (gamepad.buttons[14].pressed) b |= 1 << 2;    // LEFT
    if (gamepad.buttons[15].pressed) b |= 1 << 3;    // RIGHT

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

function startVideoFreezeWatchdog(video) {
    if (freezeWatchdogId) {
        clearInterval(freezeWatchdogId);
    }

    lastVideoCurrentTime = video.currentTime;
    lastFrameCheckAt = performance.now();

    freezeWatchdogId = setInterval(() => {
        const now = performance.now();
        const dt = now - lastFrameCheckAt;
        const dVideo = video.currentTime - lastVideoCurrentTime;

        // 2秒以上 currentTime がほぼ進んでいないのに paused ではない
        if (!video.paused && dt >= 2000 && dVideo < 0.05) {
            console.warn("possible video freeze detected", {
                currentTime: video.currentTime,
                readyState: video.readyState,
                networkState: video.networkState
            });
        }

        lastVideoCurrentTime = video.currentTime;
        lastFrameCheckAt = now;
    }, 1000);
}