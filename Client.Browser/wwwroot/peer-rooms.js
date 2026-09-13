(() => {
    "use strict";
    const peers = new Map();
    function notify(p, method, ...args) {
        if (!p.closed) p.callback.invokeMethodAsync(method, ...args).catch(() => {});
    }
    function bind(p, channel) {
        if (channel.label !== "og2" || p.channel) { channel.close(); return; }
        p.channel = channel;
        channel.binaryType = "arraybuffer";
        channel.onopen = () => notify(p, "State", true);
        channel.onclose = channel.onerror = () => notify(p, "State", false);
        channel.onmessage = event => {
            if (event.data instanceof ArrayBuffer && event.data.byteLength <= 16016)
                notify(p, "Receive", new Uint8Array(event.data));
        };
    }
    window.OpenGarrisonPeers = {
        create(id, offerer, iceServers, callback) {
            const p = { connection: new RTCPeerConnection({ iceServers }), callback, pending: [], chain: Promise.resolve(), closed: false };
            peers.set(id, p);
            p.connection.onicecandidate = event => {
                if (event.candidate) notify(p, "Signal", JSON.stringify(event.candidate.toJSON()));
            };
            p.connection.ondatachannel = event => bind(p, event.channel);
            if (offerer) {
                bind(p, p.connection.createDataChannel("og2", { ordered: true }));
                p.chain = p.connection.createOffer().then(offer => p.connection.setLocalDescription(offer))
                    .then(() => notify(p, "Signal", JSON.stringify(p.connection.localDescription)))
                    .catch(() => notify(p, "State", false));
            }
        },
        signal(id, json) {
            const p = peers.get(id); if (!p || p.closed) return;
            p.chain = p.chain.then(async () => {
                const signal = JSON.parse(json);
                if (signal.type === "offer" || signal.type === "answer") {
                    await p.connection.setRemoteDescription(signal);
                    for (const candidate of p.pending.splice(0)) await p.connection.addIceCandidate(candidate);
                    if (signal.type === "offer") {
                        await p.connection.setLocalDescription(await p.connection.createAnswer());
                        notify(p, "Signal", JSON.stringify(p.connection.localDescription));
                    }
                } else if (signal.candidate) {
                    if (p.connection.remoteDescription) await p.connection.addIceCandidate(signal);
                    else if (p.pending.length < 128) p.pending.push(signal);
                }
            }).catch(() => notify(p, "State", false));
        },
        canSend(id, count) {
            const channel = peers.get(id)?.channel;
            return channel?.readyState === "open" && channel.bufferedAmount + count * 1.01 < 8 * 1024 * 1024;
        },
        send(id, bytes) { peers.get(id).channel.send(bytes); },
        close(id) {
            const p = peers.get(id); if (!p) return;
            p.closed = true; p.channel?.close(); p.connection.close(); peers.delete(id);
        }
    };
})();
