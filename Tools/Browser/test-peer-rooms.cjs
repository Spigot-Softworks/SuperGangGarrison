// Local browser regression checks. Supply NODE_PATH with Playwright and run against a development or AOT build.
const { chromium } = require('playwright');
const fs = require('node:fs');
const assert = require('node:assert/strict');
const path = require('node:path');
const http = require('node:http');
const { spawn } = require('node:child_process');
const origin = process.env.OG_BROWSER_TEST_URL || 'http://127.0.0.1:18766';
const output = process.env.OG_BROWSER_TEST_OUTPUT || 'F:/OpenGarrisonBuild/peer-rooms/qa';
fs.mkdirSync(output, { recursive: true });
const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const publicApi = 'https://api.superganggarrison.com', localApi = 'http://127.0.0.1:18765';
const allPages = [];
const nativeCount = process.env.OG_NATIVE_GUEST_DLL ? Number(process.env.OG_NATIVE_GUEST_COUNT || 1) : 0;
async function prepare(context) {
    await context.route('**/*', async route => {
        const url = new URL(route.request().url());
        if (url.origin === publicApi) {
            try {
                const response = await route.fetch({ url: localApi + url.pathname + url.search, headers: { ...route.request().headers(), connection: 'close' } });
                await route.fulfill({ response, headers: { ...response.headers(), 'access-control-allow-origin': origin },
                    body: (await response.text()).replaceAll('ws://127.0.0.1:18765', 'wss://api.superganggarrison.com') });
            } catch { await route.abort().catch(() => {}); }
        } else if (['127.0.0.1', 'localhost'].includes(url.hostname)) await route.continue();
        else await route.abort();
    });
    await context.addInitScript(({ fallback }) => {
        const NativeSocket = window.WebSocket, NativePeer = window.RTCPeerConnection;
        window.__peerSockets = []; window.__peerConnections = []; window.__relayPackets = 0;
        window.WebSocket = class extends NativeSocket {
            constructor(url, protocols) {
                super(String(url).replace('wss://api.superganggarrison.com', 'ws://127.0.0.1:18765'), protocols);
                window.__peerSockets.push(this);
            }
            send(data) { if (typeof data !== 'string') window.__relayPackets++; super.send(data); }
        };
        window.RTCPeerConnection = class extends NativePeer {
            constructor(config) {
                if (fallback) throw Error('Direct connection disabled by local regression test');
                super(config); window.__peerConnections.push(this);
            }
        };
        const start = AudioBufferSourceNode.prototype.start;
        window.__stereoMusicAudible = false;
        AudioBufferSourceNode.prototype.start = function(...args) {
            if (this.buffer?.numberOfChannels === 2 && this.buffer.length === 960 && this.buffer.sampleRate === 48000)
                window.__stereoMusicAudible ||= [0, 1].every(c => this.buffer.getChannelData(c).some(v => Math.abs(v) > .0001));
            return start.apply(this, args);
        };
    }, { fallback: process.env.OG_FORCE_RELAY === '1' });
}
async function state(page) {
    let timer;
    try { return await Promise.race([page.evaluate(() => window.OpenGarrisonBrowserHost?.getAutomationState()),
        new Promise((_, reject) => { timer = setTimeout(() => reject(new Error('Browser stopped responding for 120 seconds')), 120000); })]); }
    finally { clearTimeout(timer); }
}
async function until(page, test, label, timeout = 90000) {
    const deadline = Date.now() + timeout;
    let latest;
    while (Date.now() < deadline) {
        latest = await state(page);
        if (latest && test(latest)) return latest;
        await sleep(150);
    }
    await page.screenshot({ path: output + '/failure.png' });
    for (const [index, other] of allPages.entries()) {
        try { fs.writeFileSync(output + '/failure-player-' + index + '.json', JSON.stringify(await state(other), null, 2)); } catch {}
    }
    throw new Error(label + ': ' + JSON.stringify(latest));
}
async function action(page, set, label) {
    assert.equal(await page.evaluate(([set, label]) => window.OpenGarrisonBrowserHost.invokeAutomationAction(set, label), [set, label]), true, set + ': ' + label);
    await sleep(150);
}
async function value(page, field, value) {
    assert.equal(await page.evaluate(([field, value]) => window.OpenGarrisonBrowserHost.setAutomationValue(field, value), [field, value]), true, field);
}
(async () => {
    let server;
    let nativeGuest;
    const nativeGuests = [];
    let nativeOutput = '';
    if (process.env.OG_BROWSER_ROOT) {
        const root = path.resolve(process.env.OG_BROWSER_ROOT);
        const types = { '.html': 'text/html', '.js': 'text/javascript', '.json': 'application/json', '.wasm': 'application/wasm', '.css': 'text/css', '.png': 'image/png' };
        server = http.createServer((req, res) => {
            const name = new URL(req.url, origin).pathname;
            const file = path.resolve(root, '.' + decodeURIComponent(name === '/' ? '/index.html' : name));
            if (!file.startsWith(root + path.sep)) { res.writeHead(403).end(); return; }
            fs.stat(file, (error, stat) => {
                if (error || !stat.isFile()) { res.writeHead(404).end(); return; }
                res.writeHead(200, { 'Content-Type': types[path.extname(file)] || 'application/octet-stream', 'Content-Length': stat.size });
                fs.createReadStream(file).pipe(res);
            });
        });
        await new Promise(resolve => server.listen(new URL(origin).port, '127.0.0.1', resolve));
    }
    const graphics = process.env.OG_BROWSER_GPU === 'd3d11' ? ['--use-angle=d3d11', '--enable-gpu'] : ['--enable-unsafe-swiftshader'];
    const browser = await chromium.launch({ headless: true, args: ['--no-sandbox', '--autoplay-policy=no-user-gesture-required', '--disable-background-timer-throttling', '--disable-renderer-backgrounding', ...graphics] });
    try {
        const context = await browser.newContext({ viewport: { width: 960, height: 720 } });
        await prepare(context);
        const page = await context.newPage();
        allPages.push(page);
        const errors = [];
        page.on('pageerror', error => { errors.push(error.message); console.log('PAGE ERROR', error.message); });
        page.on('console', message => {
            if (message.type() === 'error') console.log('CONSOLE ERROR', message.text().slice(0, 1200));
            if (/\[(server|ltd)\]|room stopped|disconnect/i.test(message.text()))
                fs.appendFileSync(output + '/host-console.log', message.text() + '\n');
        });
        await page.goto(origin);
        console.log('BOOT', (await page.locator('body').innerText()).slice(0, 800));
        await until(page, s => s.mainMenuOpen && !s.startupSplashOpen && s.menuButtons?.length, 'Main menu', 180000);
        console.log('MENU', JSON.stringify(await state(page)));
        if (process.env.OG_SKIP_SOLO !== '1') {
        await action(page, 'menu', 'Last to Die');
        await action(page, 'ltd', 'Play Solo');
        await action(page, 'ltd', 'Standard');
        await until(page, s => s.lastToDie?.phase === 'SurvivorChoice', 'Local solo survivor choice');
        assert.equal((await state(page)).joiningOverlayVisible, false);
        console.log('SOLO SURVIVOR CHOICE PASS');
        await page.screenshot({ path: output + '/solo.png' });
        await value(page, 'ltd_survivor', 'ltd.survivor.spy');
        await until(page, s => s.lastToDie?.phase === 'RewardChoice', 'Solo reward');
        await value(page, 'ltd_reward', (await state(page)).lastToDie.offerChoices[0]);
        await until(page, s => s.lastToDie?.phase === 'Playing' && s.survivorBuffActive, 'Solo gameplay');
        console.log('SOLO GAMEPLAY PASS');
        assert.equal(await page.evaluate(() => window.OpenGarrisonBrowserHost.runAutomationConsoleCommand('disconnect')), true);
        await until(page, s => s.mainMenuOpen && !s.networkConnected, 'Exit solo');
        }
        const guests = [];
        let players;
        if (process.env.OG_PRACTICE_ONLY !== '1') {
        await action(page, 'menu', 'Last to Die');
        await action(page, 'ltd', 'Play Co-Op');
        await action(page, 'ltd', 'Create');
        const lobby = await until(page, s => s.lastToDie?.menuPage === 'PeerLobby' && s.lastToDie.roomCode, 'Create player-hosted room');
        console.log('ROOM CREATED', lobby.lastToDie.roomCode);
        await page.screenshot({ path: output + '/lobby.png' });
        for (const slot of [2, 3, 4].slice(0, 3 - nativeCount)) {
            const context = await browser.newContext({ viewport: { width: 800, height: 600 } });
            await prepare(context);
            const guest = await context.newPage();
            allPages.push(guest);
            guest.on('pageerror', error => errors.push(error.message));
            await guest.goto(origin);
            await until(guest, s => s.mainMenuOpen && !s.startupSplashOpen && s.canEnterGameplaySession, 'Guest ' + slot + ' boot', 300000);
            await action(guest, 'menu', 'Last to Die');
            await action(guest, 'ltd', 'Play Co-Op');
            await action(guest, 'ltd', 'Join');
            await value(guest, 'ltd_code', lobby.lastToDie.roomCode);
            await action(guest, 'ltd', 'Join');
            await until(guest, s => s.lastToDie?.menuPage === 'PeerLobby', 'Guest ' + slot + ' join');
            guests.push(guest);
            console.log('GUEST JOINED', slot);
        }
        for (let index = 0; index < nativeCount; index++) {
            assert(process.env.OG_CONTENT_ID, 'Supply the browser build content ID for the native guest');
            nativeGuest = spawn('dotnet', [process.env.OG_NATIVE_GUEST_DLL, lobby.lastToDie.roomCode, process.env.OG_CONTENT_ID], { windowsHide: true });
            nativeGuests.push(nativeGuest);
            nativeGuest.stdout.on('data', data => { nativeOutput += data.toString(); });
            nativeGuest.stderr.on('data', data => { nativeOutput += data.toString(); });
            const joined = nativeGuest;
            await new Promise((resolve, reject) => {
                const timer = setTimeout(() => reject(Error('Native guest admission timed out: ' + nativeOutput)), 30000);
                joined.stdout.on('data', data => { if (data.toString().includes('NATIVE ROOM JOINED')) { clearTimeout(timer); resolve(); } });
                joined.once('exit', code => { clearTimeout(timer); reject(Error('Native guest exited (' + code + '): ' + nativeOutput)); });
            });
        }
        players = [page, ...guests];
        await until(page, s => s.lastToDie?.playerCount === 4, 'All four in lobby');
        await page.screenshot({ path: output + '/four-player-lobby.png' });
        for (const player of players) await action(player, 'ltd', 'Ready Up');
        await sleep(800);
        await action(page, 'ltd', 'Start');
        await Promise.all(players.map(player => until(player, s => s.lastToDie?.phase === 'SurvivorChoice', 'Co-op survivor choice', 150000)));
        console.log('FOUR PLAYER CONNECTION PASS');
        for (const player of players) await value(player, 'ltd_survivor', 'ltd.survivor.spy');
        await Promise.all(players.map(player => until(player, s => s.lastToDie?.phase === 'RewardChoice', 'Co-op reward')));
        for (const player of players) await value(player, 'ltd_reward', (await state(player)).lastToDie.offerChoices[0]);
        await Promise.all(players.map(player => until(player, s => s.lastToDie?.phase === 'Playing' && s.survivorBuffActive, 'Co-op gameplay', 150000)));
        const slots = await Promise.all(players.map(async player => (await state(player)).lastToDie.localSlot));
        assert.equal(new Set(slots).size, players.length);
        if (nativeGuest) {
            const deadline = Date.now() + 30000;
            while (nativeOutput.split('NATIVE GAMEPLAY PASS').length - 1 < nativeCount && Date.now() < deadline) await sleep(100);
            assert.equal(nativeOutput.split('NATIVE GAMEPLAY PASS').length - 1, nativeCount, nativeOutput);
            console.log('MIXED NATIVE/BROWSER GAMEPLAY PASS');
        }
        console.log('FOUR PLAYER GAMEPLAY PASS', slots);
        const direct = await Promise.all(players.map(player => player.evaluate(() => window.__peerConnections.filter(p => p.connectionState === 'connected').length)));
        const relayed = await Promise.all(players.map(player => player.evaluate(() => window.__relayPackets)));
        if (process.env.OG_FORCE_RELAY === '1') assert((await page.evaluate(() => window.__relayPackets)) > 0);
        else {
            assert.deepEqual(direct, [3, ...guests.map(() => 1)], 'All guests should use direct channels on the local network');
            assert(relayed.every(count => count === 0), 'Healthy direct sessions should not send gameplay through the API relay: ' + relayed);
        }
        console.log('CONNECTION ROUTES PASS', { direct, relayed });
        if (process.env.OG_LTD_ONLY === '1') {
            assert.deepEqual(errors, []);
            fs.writeFileSync(output + '/result.json', JSON.stringify({ passed: true, scenario: 'four-player-ltd', nativeGuests: nativeCount, direct, relayed, errors }, null, 2));
            return;
        }

        await action(page, 'ingame', 'Open');
        assert(!(await state(page)).inGameMenuLabels.includes('Call Vote'));
        assert((await state(page)).inGameMenuLabels.includes('Jukebox'));
        await action(page, 'ingame', 'Return to Lobby');
        await Promise.all(players.map(p => until(p, s => s.roomPhase === 'Lobby' && s.lastToDie?.menuPage === 'PeerLobby', 'Return all players to lobby')));
        // A transient guest signaling disconnect retains the owned seat.
        const socketCount = await guests[0].evaluate(() => { const count = window.__peerSockets.length; window.__peerSockets.at(-1).close(); return count; });
        await guests[0].waitForFunction(count => window.__peerSockets.length > count && window.__peerSockets.at(-1).readyState === WebSocket.OPEN, socketCount, { timeout: 60000 });
        await until(guests[0], s => s.lastToDie?.menuPage === 'PeerLobby' && !s.statusMessage, 'Guest reconnect');
        console.log('RETURN TO LOBBY PASS');
        for (const p of [...guests, page]) {
            await p.evaluate(() => window.OpenGarrisonBrowserHost.runAutomationConsoleCommand('disconnect'));
            await until(p, s => s.mainMenuOpen && s.mainMenuOverlay === 'None', 'Exit room');
        }
        } else {
            for (const slot of [2, 3, 4].slice(0, Number(process.env.OG_PRACTICE_PLAYERS || 4) - 1)) {
                const guestContext = await browser.newContext({ viewport: { width: 800, height: 600 } });
                await prepare(guestContext);
                const guest = await guestContext.newPage();
                allPages.push(guest);
                guest.on('pageerror', error => errors.push(error.message));
                await guest.goto(origin);
                await until(guest, s => s.mainMenuOpen && !s.startupSplashOpen && s.canEnterGameplaySession, 'Practice guest ' + slot + ' boot', 300000);
                guests.push(guest);
            }
            players = [page, ...guests];
        }

        await action(page, 'menu', 'Practice');
        await value(page, 'practice_enemy_bots', '2');
        await action(page, 'practice', 'Co-Op Lobby');
        const practice = await until(page, s => s.roomPhase === 'Lobby' && s.lastToDie.roomCode, 'Create Practice lobby');
        await action(page, 'ltd', 'Edit Settings');
        await value(page, 'practice_enemy_bots', '4');
        await action(page, 'practice', 'Cancel');
        assert((await state(page)).roomSettings.includes('BlueBots = 2'));
        await action(page, 'ltd', 'Edit Settings');
        await value(page, 'practice_enemy_bots', '1');
        await action(page, 'practice', 'Apply');
        await until(page, s => s.roomSettings.includes('BlueBots = 1'), 'Apply Practice settings');
        const joinPractice = async guest => {
            await action(guest, 'menu', 'Practice');
            await action(guest, 'practice', 'Join Co-Op');
            await value(guest, 'ltd_code', practice.lastToDie.roomCode);
            await action(guest, 'ltd', 'Join');
        };
        await joinPractice(guests[0]);
        await until(guests[0], s => s.roomPhase === 'Lobby', 'Practice guest lobby');
        await action(guests[0], 'ltd', 'Team: Red');
        await until(guests[0], s => s.lastToDie.menuLabels.includes('Team: Blue'), 'Practice team choice');
        for (const p of [page, guests[0]]) await action(p, 'ltd', 'Ready Up');
        await sleep(500);
        await action(page, 'ltd', 'Start');
        const chooseClass = async p => {
            await until(p, s => s.classSelectOpen && s.networkConnected, 'Practice class selection');
            await action(p, 'classselect', (await state(p)).classSelectButtons[0].label);
            await until(p, s => !s.classSelectOpen && !s.mainMenuOpen && !s.joiningOverlayVisible && s.localPlayerAlive, 'Practice gameplay');
            await sleep(1500);
            assert(!(await state(p)).classSelectOpen, 'A delayed welcome must not reopen class selection');
        };
        for (const p of [page, guests[0]]) await chooseClass(p);
        for (const guest of guests.slice(1)) { await joinPractice(guest); await chooseClass(guest); }
        console.log('PRACTICE SETTINGS AND TEAMS PASS', players.length);
        if (guests.length > 1) console.log('PRACTICE LATE JOIN PASS', guests.length - 1);
        await page.screenshot({ path: output + '/practice-gameplay.png' });

        await action(page, 'ingame', 'Open');
        await action(page, 'ingame', 'Call Vote');
        await until(page, s => s.voteMenuLabels.includes('Change Map Now'), 'Practice map vote menu');
        const previousMap = (await state(page)).currentMap;
        await action(page, 'vote', 'Change Map Now');
        const mapChoice = (await state(page)).voteMenuLabels.find(label => !label.includes(previousMap) && label !== 'Back');
        await action(page, 'vote', mapChoice);
        await until(page, s => s.voteActive, 'Map vote starts');
        for (const p of guests.slice(0, 2)) {
            await action(p, 'ingame', 'Open'); await action(p, 'ingame', 'Call Vote');
            await until(p, s => s.voteMenuLabels.includes('Vote Yes'), 'Guest vote menu');
            await action(p, 'vote', 'Vote Yes');
        }
        await until(page, s => !s.voteActive && s.currentMap !== previousMap, 'Map vote takes effect');
        console.log('PRACTICE MAP VOTE PASS');

        await page.evaluate(() => window.OpenGarrisonJukebox.manage());
        const samples = 48000 * 20, wave = Buffer.alloc(44 + samples * 4);
        wave.write('RIFF', 0); wave.writeUInt32LE(wave.length - 8, 4); wave.write('WAVEfmt ', 8); wave.writeUInt32LE(16, 16);
        wave.writeUInt16LE(1, 20); wave.writeUInt16LE(2, 22); wave.writeUInt32LE(48000, 24); wave.writeUInt32LE(192000, 28);
        wave.writeUInt16LE(4, 32); wave.writeUInt16LE(16, 34); wave.write('data', 36); wave.writeUInt32LE(samples * 4, 40);
        for (let i = 0; i < samples; i++) { wave.writeInt16LE(Math.round(Math.sin(i * 440 * 2 * Math.PI / 48000) * 3000), 44 + i * 4); wave.writeInt16LE(Math.round(Math.sin(i * 660 * 2 * Math.PI / 48000) * 3000), 46 + i * 4); }
        await page.getByLabel('Import music tracks').setInputFiles({ name: 'Browser stereo test.wav', mimeType: 'audio/wav', buffer: wave });
        await page.getByRole('status').filter({ hasText: 'Saved.' }).waitFor();
        await page.getByRole('button', { name: 'Done', exact: true }).click();
        await action(page, 'ingame', 'Open'); await action(page, 'ingame', 'Jukebox');
        await until(page, s => s.inGameMenuLabels.some(label => label.includes('Browser stereo test')), 'Browser library restored');
        // Menu automation bypasses real pointer/keyboard gestures. Give every
        // listener the gesture an actual class-selection click would provide.
        for (const p of players) await p.keyboard.press('Control');
        await Promise.all(players.map(p => p.evaluate(() => { window.__stereoMusicAudible = false; })));
        await action(page, 'ingame', 'Play Selected');
        await Promise.all(players.map(p => until(p, s => s.musicPlaying, 'Jukebox reaches room')));
        await Promise.all(players.map(p => p.waitForFunction(() => window.__stereoMusicAudible, null, { timeout: 15000 })));
        assert(await page.evaluate(() => window.__stereoMusicAudible), 'Host receives audible stereo music');
        assert(await guests[0].evaluate(() => window.__stereoMusicAudible), 'Guest receives audible stereo music');
        await action(page, 'ingame', 'Pause Music'); await until(page, s => s.musicPaused, 'Pause jukebox');
        await action(page, 'ingame', 'Resume Music'); await until(page, s => !s.musicPaused, 'Resume jukebox');
        await action(page, 'ingame', 'Back');
        await action(page, 'ingame', 'Jukebox');
        assert((await state(page)).musicPlaying, 'Reopening jukebox controls preserves playback');
        await action(page, 'ingame', 'Stop Music'); await until(page, s => !s.musicPlaying, 'Stop jukebox');
        console.log('BROWSER IMPORT AND ROOM STEREO JUKEBOX PASS');

        await page.reload();
        await until(page, s => s.mainMenuOpen && !s.startupSplashOpen && s.canEnterGameplaySession, 'Reload browser', 180000);
        await page.evaluate(() => window.OpenGarrisonJukebox.manage());
        await page.getByText('Browser stereo test.wav', { exact: false }).waitFor();
        await page.screenshot({ path: output + '/persistent-library.png' });
        console.log('BROWSER LIBRARY PERSISTENCE PASS');
        fs.writeFileSync(output + '/browser-errors.json', JSON.stringify(errors, null, 2));
        assert.deepEqual(errors, []);
        fs.writeFileSync(output + '/result.json', JSON.stringify({ passed: true, scenario: process.env.OG_PRACTICE_ONLY === '1' ? 'practice' : 'full', players: players.length, errors }, null, 2));
    } finally {
        fs.writeFileSync(output + '/native-guests.log', nativeOutput);
        for (const guest of nativeGuests) guest.kill();
        await browser.close(); if (server) await new Promise(resolve => server.close(resolve));
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
