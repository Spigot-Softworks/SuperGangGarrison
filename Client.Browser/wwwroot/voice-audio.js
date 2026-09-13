(() => {
    "use strict";
    let context, bridge, capture, generation = 0, requested = false, disposed = false;
    let devices = [], workletReady;
    const players = new Map();
    const invoke = (method, ...args) => bridge?.invokeMethodAsync(method, ...args).catch(() => {});

    function audioContext() {
        if (!context || context.state === "closed") {
            context = new AudioContext({ sampleRate: 48000, latencyHint: "interactive" });
            workletReady = null;
        }
        return context;
    }
    function unlock() {
        if (disposed) return;
        try { audioContext().resume().catch(() => {}); } catch { }
    }
    async function refreshDevices() {
        try { devices = (await navigator.mediaDevices.enumerateDevices()).filter(device => device.kind === "audioinput"); }
        catch { devices = []; }
    }
    function stopCapture() {
        if (!capture) return;
        capture.node.port.onmessage = null;
        capture.source.disconnect();
        capture.node.disconnect();
        capture.silent.disconnect();
        capture.stream.getTracks().forEach(track => track.stop());
        capture = null;
    }
    function releaseCapture() {
        requested = false;
        stopCapture();
        invoke("VoiceCaptureState", generation, false, "");
    }
    function visibilityChanged() { if (document.hidden) releaseCapture(); }

    window.OpenGarrisonVoice = {
        async init(reference) {
            bridge = reference;
            disposed = false;
            window.addEventListener("blur", releaseCapture);
            window.addEventListener("pagehide", releaseCapture);
            document.addEventListener("visibilitychange", visibilityChanged);
            document.addEventListener("pointerdown", unlock, true);
            document.addEventListener("keydown", unlock, true);
            await refreshDevices();
        },
        microphoneNames() { return devices.map(device => device.label).filter(Boolean); },
        setCapture(active, name, requestGeneration) {
            generation = requestGeneration;
            requested = active;
            stopCapture();
            if (!active || disposed) return;
            if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
                invoke("VoiceCaptureState", generation, false, "Microphone requires HTTPS or localhost.");
                return;
            }
            const request = generation;
            invoke("VoiceCaptureState", request, false, "Allow microphone access in your browser.");
            (async () => {
                let stream;
                try {
                    const device = devices.find(device => device.label === name);
                    if (name && !device) throw new Error("Selected microphone is unavailable. Choose Default in Audio settings.");
                    stream = await navigator.mediaDevices.getUserMedia({ audio: {
                        channelCount: 1, echoCancellation: true, noiseSuppression: true, autoGainControl: false,
                        ...(device ? { deviceId: { exact: device.deviceId } } : {})
                    } });
                    if (!requested || request !== generation || disposed || document.hidden) {
                        stream.getTracks().forEach(track => track.stop());
                        return;
                    }
                    const ctx = audioContext();
                    await ctx.resume();
                    workletReady ??= ctx.audioWorklet.addModule(new URL("voice-capture-worklet.js", document.baseURI).href);
                    await workletReady;
                    if (!requested || request !== generation || disposed || document.hidden) {
                        stream.getTracks().forEach(track => track.stop());
                        return;
                    }
                    const source = ctx.createMediaStreamSource(stream);
                    const node = new AudioWorkletNode(ctx, "opengarrison-voice-capture");
                    const silent = ctx.createGain();
                    silent.gain.value = 0;
                    capture = { source, node, silent, stream };
                    node.port.onmessage = event => {
                        if (requested && request === generation && !disposed)
                            invoke("VoiceCapture", request, new Uint8Array(event.data), ctx.sampleRate);
                    };
                    source.connect(node).connect(silent).connect(ctx.destination);
                    stream.getAudioTracks().forEach(track => track.addEventListener("ended", () => {
                        if (requested && request === generation) {
                            stopCapture();
                            invoke("VoiceCaptureState", request, false, "Microphone disconnected. Release and press push to talk to retry.");
                        }
                    }));
                    invoke("VoiceCaptureState", request, true, "");
                    await refreshDevices();
                } catch (error) {
                    stream?.getTracks().forEach(track => track.stop());
                    if (request === generation && requested && !disposed)
                        invoke("VoiceCaptureState", request, false, error.name === "NotAllowedError"
                            ? "Microphone access denied. Allow it in browser site settings." : "Microphone: " + error.message);
                }
            })();
        },
        play(slot, pcm, channels, volume, pan) {
            if (disposed || !context || context.state !== "running" || volume <= 0) return;
            const ctx = context;
            let player = players.get(slot);
            if (player && player.nextAt > ctx.currentTime + 0.18) {
                this.stopPlayback(slot);
                player = null;
            }
            if (!player) {
                const gain = ctx.createGain();
                const panner = ctx.createStereoPanner();
                gain.connect(panner).connect(ctx.destination);
                player = { gain, panner, nextAt: 0, sources: new Set() };
                players.set(slot, player);
            }
            player.gain.gain.value = Math.max(0, Math.min(1, volume));
            player.panner.pan.value = Math.max(-1, Math.min(1, pan || 0));
            const data = new DataView(pcm.buffer, pcm.byteOffset, pcm.byteLength);
            const sampleCount = pcm.byteLength / (2 * channels);
            const buffer = ctx.createBuffer(channels, sampleCount, 48000);
            for (let channel = 0; channel < channels; channel++) {
                const output = buffer.getChannelData(channel);
                for (let i = 0; i < sampleCount; i++) output[i] = data.getInt16((i * channels + channel) * 2, true) / 32768;
            }
            const source = ctx.createBufferSource();
            source.buffer = buffer;
            source.connect(player.gain);
            player.sources.add(source);
            source.onended = () => { player.sources.delete(source); source.disconnect(); };
            const start = Math.max(ctx.currentTime + 0.015, player.nextAt);
            source.start(start);
            player.nextAt = start + buffer.duration;
        },
        setVolume(slot, volume) { const player = players.get(slot); if (player) player.gain.gain.value = Math.max(0, Math.min(1, volume)); },
        setPan(slot, pan) { const player = players.get(slot); if (player) player.panner.pan.value = Math.max(-1, Math.min(1, pan || 0)); },
        stopPlayback(slot) {
            const player = players.get(slot);
            if (!player) return;
            for (const source of player.sources) { try { source.stop(); source.disconnect(); } catch { } }
            player.gain.disconnect();
            player.panner.disconnect();
            players.delete(slot);
        },
        dispose() {
            disposed = true;
            requested = false;
            generation++;
            stopCapture();
            for (const slot of players.keys()) this.stopPlayback(slot);
            window.removeEventListener("blur", releaseCapture);
            window.removeEventListener("pagehide", releaseCapture);
            document.removeEventListener("visibilitychange", visibilityChanged);
            document.removeEventListener("pointerdown", unlock, true);
            document.removeEventListener("keydown", unlock, true);
            context?.close().catch(() => {});
            bridge = null;
        }
    };
})();
