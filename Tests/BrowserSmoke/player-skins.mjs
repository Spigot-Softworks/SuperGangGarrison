import assert from 'node:assert/strict';
import {mkdir, writeFile} from 'node:fs/promises';
import {resolve, join} from 'node:path';
import {setTimeout as delay} from 'node:timers/promises';
import {chromium} from 'playwright';

const output = resolve(process.env.OG_BROWSER_QA_DIR ?? 'artifacts/browser-player-skins');
await mkdir(output, {recursive: true});
const browser = await chromium.launch({headless: true});
const context = await browser.newContext({viewport: {width: 1280, height: 720}});
await context.addInitScript(() => {
    localStorage.setItem('opengarrison:first-play-hints-v1', JSON.stringify({HasShown: true}));
    localStorage.setItem('opengarrison:settings-v1', JSON.stringify({PlayerName: 'Sprite QA', ParticleMode: 0}));
});
const page = await context.newPage();
const errors = [], checks = [];
let passed = false;
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => { if (message.type() === 'error') console.error(message.text()); });
async function state() {
    let timeout;
    try {
        return await Promise.race([
            page.evaluate(() => globalThis.OpenGarrisonBrowserHost?.getAutomationState()),
            new Promise((_, reject) => { timeout = setTimeout(() => reject(Error('Browser stopped answering automation requests.')), 10000); }),
        ]);
    } finally { clearTimeout(timeout); }
}
async function wait(predicate, label, timeout = 180000) {
    let snapshot;
    for (const end = Date.now() + timeout; Date.now() < end;) {
        snapshot = await state();
        const host = await page.evaluate(() => globalThis.OpenGarrisonBrowserHost?.hostState);
        if (host?.failed) throw Error(`Browser startup failed: ${JSON.stringify(host)}`);
        if (snapshot && predicate(snapshot)) {
            console.log(`Passed: ${label}`);
            return snapshot;
        }
        await delay(80);
    }
    throw Error(`${label}: ${JSON.stringify(snapshot)}`);
}
async function action(group, label) {
    assert(await page.evaluate(([g, l]) => OpenGarrisonBrowserHost.invokeAutomationAction(g, l), [group, label]), label);
}
async function command(text) {
    assert(await page.evaluate(t => OpenGarrisonBrowserHost.runAutomationConsoleCommand(t), text), text);
}
async function key(code, down) {
    await page.evaluate(([c, d]) => OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync('HandleBrowserKey', c, d), [code, down]);
}
try {
    await page.goto(process.env.OG_BROWSER_URL ?? 'http://127.0.0.1:5079', {waitUntil: 'domcontentloaded', timeout: 120000});
    await wait(s => s.menuBootstrapComplete && s.canEnterGameplaySession, 'content ready');
    await page.evaluate(() => OpenGarrisonBrowserHost.focusCanvas());
    for (let attempt = 0; attempt < 3 && (await state()).startupSplashOpen; attempt++) {
        await key('Enter', true);
        await delay(150);
        await key('Enter', false);
        await delay(500);
    }
    await wait(s => !s.startupSplashOpen && s.mainMenuOpen && s.canEnterGameplaySession, 'main menu');
    for (const [label, team] of [['RED', 'Red'], ['BLU', 'Blue']]) {
        await page.evaluate(() => OpenGarrisonBrowserHost.focusCanvas());
        const offlineLabel = (await state()).menuButtons.some(button => button.label === 'Play Offline') ? 'Play Offline' : 'Practice';
        await action('menu', offlineLabel);
        if (offlineLabel === 'Play Offline') await action('menu', 'Practice');
        assert(await page.evaluate(() => OpenGarrisonBrowserHost.startAutomationPractice(0, 0)));
        await wait(s => s.teamSelectOpen, 'team selection');
        await action('teamselect', label);
        await wait(s => s.classSelectOpen, 'class selection');
        await action('classselect', 'Soldier');
        for (const [classId, skin, count] of [['soldier', 'Rocketman', 17], ['medic', 'Healer', 9], ['spy', 'Infiltrator', 17]]) {
            await command(`set_class ${classId}`);
            const snapshot = await wait(s => s.localPlayerAlive && s.playerSkin?.bodySprite === `${skin}${team}BodyS`
                && s.playerSkin.bodyFrameCount === count && s.playerSkin.clip === 'idle', `${skin} ${team} art`);
            assert.equal(snapshot.playerSkin.weaponSprite, `${skin}${team}WeaponS`);
            assert.equal(snapshot.playerSkin.weaponFrameCount, count);
            await page.mouse.move(1000, 350);
            await delay(150);
            await page.screenshot({path: join(output, `${skin}-${team}-idle.png`)});
            checks.push(snapshot.playerSkin);

            const moveKey = team === 'Blue' ? 'KeyA' : 'KeyD';
            await key(moveKey, true);
            try {
                const moving = await wait(s => ['run', 'runBackward'].includes(s.playerSkin?.clip), `${skin} running`, 10000);
                assert.equal(moving.playerSkin.pose, moving.playerSkin.weaponPose);
            } finally { await key(moveKey, false); }
            await key('KeyW', true);
            try {
                await wait(s => ['jumpStart', 'rise', 'fall'].includes(s.playerSkin?.clip), `${skin} airborne`, 10000);
                await page.screenshot({path: join(output, `${skin}-${team}-jump.png`)});
            } finally { await key('KeyW', false); }
            await wait(s => s.playerSkin?.clip === 'idle', 'landed', 10000);
            if (classId === 'soldier') {
                // Leave spawn resupply so it cannot refill the launcher before it reloads.
                const position = await state();
                await command(`teleport ${position.localPlayerX + (team === 'Blue' ? -500 : 500)} ${position.localPlayerY}`);
                await delay(400);
                await command('+fire');
                try {
                    await wait(s => s.playerSkin?.weaponAnimation === 'Recoil', 'Rocketman fire', 10000);
                    await page.screenshot({path: join(output, `${skin}-${team}-fire.png`)});
                }
                finally { await command('-fire'); }
                await wait(s => s.playerSkin?.weaponAnimation === 'Reload', 'Rocketman reload', 10000);
                await page.screenshot({path: join(output, `${skin}-${team}-reload.png`)});
            }
        }
        await command('disconnect');
        await wait(s => s.mainMenuOpen, 'return to menu');
    }
    assert.deepEqual(errors, []);
    passed = true;
    console.log('Both teams: default skins, loaded frames, running, jumping, firing and reload passed.');
} finally {
    await page.screenshot({path: join(output, 'last-state.png')}).catch(() => {});
    await writeFile(join(output, 'results.json'), JSON.stringify({passed, checks, errors}, null, 2));
    await browser.close();
}
