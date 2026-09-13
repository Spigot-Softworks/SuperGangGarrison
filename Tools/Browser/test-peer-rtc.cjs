const { chromium } = require('playwright');
const { spawn } = require('node:child_process');
const { readFileSync } = require('node:fs');
const assert = require('node:assert/strict');
(async () => {
    const browser = await chromium.launch({ headless: true });
    const native = spawn('dotnet', [process.env.OG_NATIVE_GUEST_DLL, 'RTC'], { windowsHide: true });
    let output = '', pending = '', page;
    native.stdout.on('data', data => {
        output += data; pending += data;
        while (pending.includes('\n')) {
            const index = pending.indexOf('\n'), line = pending.slice(0, index).trim(); pending = pending.slice(index + 1);
            if (line.startsWith('SIGNAL:')) page.evaluate(signal => window.OpenGarrisonPeers.signal('native', signal), line.slice(7)).catch(() => {});
        }
    });
    native.stderr.on('data', data => { output += data; });
    try {
        page = await browser.newPage();
        await page.exposeFunction('nativeSignal', signal => native.stdin.write(signal + '\n'));
        await page.setContent('<title>Local WebRTC interoperability fixture</title>');
        await page.addScriptTag({ content: readFileSync('Client.Browser/wwwroot/peer-rooms.js', 'utf8') });
        await page.evaluate(() => {
            window.results = []; window.peerOpen = false;
            let assembling, count = 0, id;
            window.OpenGarrisonPeers.create('native', true, [], { invokeMethodAsync: async (method, ...args) => {
                if (method === 'Signal') await window.nativeSignal(args[0]);
                if (method === 'State') window.peerOpen = args[0];
                if (method === 'Receive') {
                    const chunk = args[0], view = new DataView(chunk.buffer, chunk.byteOffset, chunk.byteLength);
                    if (view.getUint32(0, true) !== 0x3150474f) throw Error('Bad packet header');
                    const nextId = view.getUint32(4, true), length = view.getInt32(8, true), offset = view.getInt32(12, true);
                    if (offset === 0) { assembling = new Uint8Array(length); count = 0; id = nextId; }
                    if (id !== nextId || offset !== count) throw Error('Out-of-order fragment');
                    assembling.set(chunk.subarray(16), offset); count += chunk.length - 16;
                    if (count === length) window.results.push({ length, valid: assembling.every((b, i) => b === ((i % 251) ^ (length & 255))) });
                }
            }});
        });
        await page.waitForFunction(() => window.peerOpen, { timeout: 20000 });
        await page.evaluate(() => {
            for (const [index, length] of [1, 16384, 100000].entries()) {
                for (let offset = 0; offset < length; offset += 16000) {
                    const count = Math.min(16000, length - offset), chunk = new Uint8Array(16 + count), view = new DataView(chunk.buffer);
                    view.setUint32(0, 0x3150474f, true); view.setUint32(4, index + 1, true); view.setInt32(8, length, true); view.setInt32(12, offset, true);
                    for (let i = 0; i < count; i++) chunk[16 + i] = ((offset + i) % 251) ^ (length & 255);
                    window.OpenGarrisonPeers.send('native', chunk);
                }
            }
        });
        await page.waitForFunction(() => window.results.length === 3, { timeout: 20000 });
        assert.deepEqual(await page.evaluate(() => window.results), [1, 16384, 100000].map(length => ({ length, valid: true })));
        console.log('BROWSER/NATIVE DIRECT DATA CHANNEL PASS: bidirectional ordered packets through 100 KB.');
    } catch (error) { console.error(output); throw error; }
    finally { native.stdin.end(); native.kill(); await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
