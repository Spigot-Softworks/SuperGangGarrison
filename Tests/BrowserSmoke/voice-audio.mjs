import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "playwright";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const server = createServer(async (request, response) => {
    if (request.url === "/") {
        response.setHeader("Content-Type", "text/html");
        response.end('<!doctype html><html><body><button>Start</button><script src="voice-audio.js"></script></body></html>');
        return;
    }
    const name = request.url?.slice(1);
    if (!["voice-audio.js", "voice-capture-worklet.js"].includes(name)) { response.writeHead(404).end(); return; }
    response.setHeader("Content-Type", "text/javascript");
    response.end(await readFile(resolve(root, "Client.Browser/wwwroot", name)));
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
let browser;
try {
    browser = await chromium.launch({ headless: true, args: ["--use-fake-device-for-media-stream", "--use-fake-ui-for-media-stream", "--autoplay-policy=no-user-gesture-required"] });
    const page = await browser.newPage();
    const errors = [];
    page.on("pageerror", error => errors.push(error.message));
    await page.goto(`http://127.0.0.1:${server.address().port}/`);
    await page.evaluate(async () => {
        window.voiceEvents = [];
        window.audioStarts = [];
        window.audioGains = [];
        window.audioPanners = [];
        window.audioStops = 0;
        const prototype = AudioContext.prototype;
        const createBufferSource = prototype.createBufferSource;
        prototype.createBufferSource = function () {
            const source = createBufferSource.call(this);
            const start = source.start.bind(source);
            const stop = source.stop.bind(source);
            source.stop = (...args) => { window.audioStops++; stop(...args); };
            source.start = (...args) => { window.audioStarts.push({ channels: source.buffer.numberOfChannels, length: source.buffer.length }); start(...args); };
            return source;
        };
        const createGain = prototype.createGain;
        prototype.createGain = function () { const gain = createGain.call(this); window.audioGains.push(gain); return gain; };
        const createStereoPanner = prototype.createStereoPanner;
        prototype.createStereoPanner = function () { const panner = createStereoPanner.call(this); window.audioPanners.push(panner); return panner; };
        await OpenGarrisonVoice.init({ invokeMethodAsync: (method, ...args) => { voiceEvents.push({ method, args }); return Promise.resolve(); } });
    });
    await page.getByRole("button").click();
    await page.evaluate(() => OpenGarrisonVoice.setCapture(true, "", 1));
    await page.waitForFunction(() => voiceEvents.filter(event => event.method === "VoiceCapture").length >= 4);
    const capture = await page.evaluate(() => voiceEvents.filter(event => event.method === "VoiceCapture").map(event => ({ generation: event.args[0], bytes: event.args[1].length, rate: event.args[2] })));
    assert(capture.every(frame => frame.generation === 1 && frame.bytes === 1920 && frame.rate >= 8000));
    await page.evaluate(() => OpenGarrisonVoice.setCapture(false, "", 2));
    const count = await page.evaluate(() => voiceEvents.filter(event => event.method === "VoiceCapture").length);
    await page.waitForTimeout(150);
    assert.equal(await page.evaluate(() => voiceEvents.filter(event => event.method === "VoiceCapture").length), count);

    // Playback uses the same path for a voice participant and the stereo Jukebox.
    await page.evaluate(() => {
        OpenGarrisonVoice.play(1, new Uint8Array(1920), 1, 0.8, -0.75);
        OpenGarrisonVoice.play(0, new Uint8Array(3840), 2, 0.4);
        OpenGarrisonVoice.setVolume(0, 0.1);
    });
    assert.deepEqual(await page.evaluate(() => audioStarts), [{ channels: 1, length: 960 }, { channels: 2, length: 960 }]);
    assert(Math.abs(await page.evaluate(() => audioGains.at(-1).gain.value) - 0.1) < 0.001);
    assert.deepEqual(await page.evaluate(() => audioPanners.map(panner => panner.pan.value)), [-0.75, 0]);
    await page.evaluate(() => OpenGarrisonVoice.play(1, new Uint8Array(1920), 1, 0.8, 0));
    assert.equal(await page.evaluate(() => audioPanners[0].pan.value), 0);
    await page.evaluate(() => { OpenGarrisonVoice.stopPlayback(0); OpenGarrisonVoice.stopPlayback(1); });
    assert(await page.evaluate(() => audioStops) > 0);

    // A permission grant arriving after the PTT key was released must close its tracks.
    await page.evaluate(() => {
        window.getUserMediaOriginal = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
        navigator.mediaDevices.getUserMedia = async constraints => {
            const stream = await getUserMediaOriginal(constraints);
            window.delayedStream = stream;
            await new Promise(resolve => { window.releaseGrant = resolve; });
            return stream;
        };
        OpenGarrisonVoice.setCapture(true, "", 3);
    });
    await page.waitForFunction(() => typeof releaseGrant === "function");
    await page.evaluate(() => { OpenGarrisonVoice.setCapture(false, "", 4); releaseGrant(); });
    await page.waitForFunction(() => delayedStream.getTracks().every(track => track.readyState === "ended"));

    await page.evaluate(() => {
        navigator.mediaDevices.getUserMedia = async () => { throw new DOMException("Denied", "NotAllowedError"); };
        OpenGarrisonVoice.setCapture(true, "", 5);
    });
    await page.waitForFunction(() => voiceEvents.some(event => event.method === "VoiceCaptureState" && event.args[0] === 5 && event.args[2].includes("denied")));
    await page.evaluate(() => OpenGarrisonVoice.dispose());
    assert.deepEqual(errors, []);
    console.log("PASS: Web Audio capture, 20ms frames, PTT release, mono/stereo playback, volume, delayed permissions, denial and disposal.");
} finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
}
