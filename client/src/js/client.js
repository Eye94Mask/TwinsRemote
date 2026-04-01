let pc;
let dc;
let gamepadIndex;
let remoteStream = new MediaStream();

window.addEventListener("gamepadconnected", (e) => {
    gamepadIndex = e.gamepad.index;
    // console.log(
    //     "Gamepad connected at index %d: %s. %d buttons, %d axes.",
    //     e.gamepad.index,
    //     e.gamepad.id,
    //     e.gamepad.buttons.length,
    //     e.gamepad.axes.length
    // );
});

/*
Connectボタン押下時の挙動
*/
async function connect() {
    pc = new RTCPeerConnection({
        iceServers: [
            { urls: "stun:stun.l.google.com:19302" },
            {
                urls: [
                    "turn:43.207.155.19:3478?transport=udp",
                    "turn:43.207.155.19:3478?transport=tcp",
                    "turn:43.207.155.19:5349?transport=tcp"
                ],
                username: "test",
                credential: "password",
                credentialType: "password"
            }
        ],
        iceTransportPolicy: "all"
    });

    pc.getStats().then(r => {
        r.forEach(report => {
            if (report.type === "inbound.rtp" && report.kind === "video") {
                console.log("jitterBufferDelay:", report.jitterBufferDelay);
                console.log("framesDecoded:", report.framesDecoded);
            }
        });
    });

    const video = document.getElementById("video");
    video.srcObject = remoteStream;
    video.autoplay = true;
    video.playsInline = true;
    video.muted = true;

    video.onloadedmetadata = () => {
        console.log("loadedmetadata", video.videoWidth, video.videoHeight);
    };

    video.onplaying = () => {
        console.log("video playing");
    };

    pc.ontrack = (event) => {
        console.log("ontrack", event.track.kind, event.streams);

        remoteStream.addTrack(event.track);

        event.track.onunmute = () => {
            console.log(`${event.track.kind} track unmuted`);
        };

        event.track.onended = () => {
            console.log(`${event.track.kind} track ended`);
        };

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
    };

    setInterval(() => {
        console.log({
            tracks: remoteStream.getTracks().map(t => ({
                kind: t.kind,
                enabled: t.enabled,
                muted: t.muted,
                readyState: t.readyState
            })),
            videoPaused: video.paused,
            readyState: video.readyState,
            currentTime: video.currentTime,
            videoWidth: video.videoWidth,
            videoHeight: video.videoHeight
        });
    }, 1000);

    pc.ondatachannel = (e) => {
        dc = e.channel;
        dc.ordered = false;
        dc.maxRetransmits = 0;

        dc.onopen = () => {
            // ストリーミング映像の拡大
            // const video = document.getElementById("video");
            // video.classList.add("streaming");

            console.log("DataChannel open");
            startGamepadLoop();
        }
    }

    pc.onicecandidate = e => {
        console.log(e.candidate);
        const ice = document.getElementById("ice");
        ice.value = JSON.stringify(e.candidate);
        if (e.candidate) {
            console.log("CLIENT ICE CANDIDATE:");
            console.log(JSON.stringify(e.candidate));
        }
    }
    pc.oniceconnectionstatechange = e => {
        console.log("ICE CANDIDATE STATUS CHANGED");
        console.log(e);
    }

    const offer = JSON.parse(document.getElementById("offer").value);
    await pc.setRemoteDescription(offer);

    const answer = await pc.createAnswer();;
    await pc.setLocalDescription(answer);

    document.getElementById("answer").value = JSON.stringify(pc.localDescription);
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
    const host = document.getElementById("host").value;
    addHostCandidate(host);
}

// HostのICE候補の受け取り
async function addHostCandidate() {
    const json = document.getElementById("host").value;
    if (!json) return;

    const candidate = new RTCIceCandidate(JSON.parse(json));
    await pc.addIceCandidate(candidate);

    console.log("Host Ice candidate added");
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
        const gamepad = navigator.getGamepads()[gamepadIndex];
        if (!gamepad) {
            requestAnimationFrame(startGamepadLoop)
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

        if (dc && dc.readyState === "open") {
            dc.send(buf)
        }

        requestAnimationFrame(loop);
    }

    requestAnimationFrame(loop);
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