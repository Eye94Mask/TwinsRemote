const pc = new RTCPeerConnection({
    iceServers: [
        { urls: "stun:stun.l.google.com:19302" }
    ]
})

pc.ontrack = e => {
    document.getElementById("video").srcObject = e.streams[0];
}

async function start() {
    const offer = await pc.createOffer();
    await pc.setLocalDescription(offer);

    await new Promise(resolve => {
        if (pc.iceGetheringState === "complete") {
            resolve()
        } else {
            pc.onicegatheringstatechange = () => {
                if (pc.iceGatheringState === "complete") {
                    resolve()
                }
            }
        }
    });

    const res = await fetch("http://localhost:3030/offer", {
        method: "POST",
        body: JSON.stringify(pc.localDescription),
        headers: { "Content-Type": "application/json" }
    });

    const answer = await res.json();

    await pc.setRemoteDescription(answer)
}