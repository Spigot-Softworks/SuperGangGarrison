const { chromium } = require('playwright');
const { readFileSync } = require('node:fs');
const { createServer } = require('node:http');
const assert = require('node:assert/strict');
(async () => {
    const script = readFileSync('Client.Browser/wwwroot/jukebox-library.js');
    const server = createServer((req, res) => {
        res.setHeader('Content-Type', req.url === '/library.js' ? 'text/javascript' : 'text/html');
        res.end(req.url === '/library.js' ? script : '<title>Local music storage fixture</title><script src="/library.js"></script>');
    });
    await new Promise(done => server.listen(0, '127.0.0.1', done));
    const origin = 'http://127.0.0.1:' + server.address().port;
    const browser = await chromium.launch({ headless: true });
    try {
        const page = await browser.newPage();
        const data = Buffer.alloc(44 + 960 * 2);
        data.write('RIFF'); data.writeUInt32LE(data.length - 8, 4); data.write('WAVEfmt ', 8); data.writeUInt32LE(16, 16);
        data.writeUInt16LE(1, 20); data.writeUInt16LE(1, 22); data.writeUInt32LE(48000, 24); data.writeUInt32LE(96000, 28);
        data.writeUInt16LE(2, 32); data.writeUInt16LE(16, 34); data.write('data', 36); data.writeUInt32LE(data.length - 44, 40);
        await page.goto(origin);
        await page.evaluate(() => window.OpenGarrisonJukebox.manage());
        await page.getByLabel('Import music tracks').setInputFiles({ name: 'Persistent test.wav', mimeType: 'audio/wav', buffer: data });
        await page.getByRole('status').filter({ hasText: 'Saved.' }).waitFor();
        await page.getByRole('button', { name: 'Done', exact: true }).click();
        await page.reload();
        const restored = await page.evaluate(async () => {
            const tracks = [];
            const names = await window.OpenGarrisonJukebox.restore({ invokeMethodAsync: async (_, name, data) => tracks.push({ name, bytes: Array.from(data) }) });
            return { names, tracks };
        });
        assert.deepEqual(restored.names, ['Persistent test.wav']);
        assert(Buffer.from(restored.tracks[0].bytes).equals(data));
        await page.evaluate(() => window.OpenGarrisonJukebox.manage());
        await page.getByRole('button', { name: 'Remove', exact: true }).click();
        await page.getByText('Persistent test.wav', { exact: false }).waitFor({ state: 'detached' });
        assert.deepEqual(await page.evaluate(() => window.OpenGarrisonJukebox.restore({ invokeMethodAsync: async () => {} })), []);
        console.log('BROWSER MUSIC STORAGE PASS: import, exact-byte restoration after reload, and removal.');
    } finally { await browser.close(); await new Promise(done => server.close(done)); }
})().catch(error => { console.error(error); process.exitCode = 1; });
