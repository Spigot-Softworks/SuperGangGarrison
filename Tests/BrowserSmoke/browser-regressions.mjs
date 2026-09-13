import assert from 'node:assert/strict';
import {mkdir, readFile, writeFile} from 'node:fs/promises';
import {resolve, join} from 'node:path';
import {setTimeout as delay} from 'node:timers/promises';
import {chromium} from 'playwright';

const base = process.env.OG_BROWSER_URL ?? 'http://127.0.0.1:5079';
const output = resolve(process.env.OG_BROWSER_QA_DIR ?? 'artifacts/browser-regressions');
const desktopNames = new Set((await readFile(new URL('../../Client/practice-bot-names.txt', import.meta.url), 'utf8'))
  .split('\n').map(line => line.trim()).filter(line => line && !line.startsWith('#')));
await mkdir(output, {recursive: true});
const browser = await chromium.launch({headless: true});
const context = await browser.newContext({viewport: {width: 1280, height: 720}});
await context.addInitScript(() => {
  localStorage.setItem('opengarrison:first-play-hints-v1', JSON.stringify({HasShown: true}));
  localStorage.setItem('opengarrison:settings-v1', JSON.stringify({ParticleMode: 0}));
});
const page = await context.newPage(), errors = [], checks = {};
page.on('pageerror', e => errors.push(e.message));
page.on('console', m => {if (m.type() === 'error') errors.push(m.text());});
async function state() {return page.evaluate(() => OpenGarrisonBrowserHost.getAutomationState());}
async function wait(predicate, label, timeout = 180000) {
  let s;
  for (const end = Date.now() + timeout; Date.now() < end;) {
    s = await state(); if (s && predicate(s)) return s;
    await delay(100);
  }
  throw Error(`${label}: ${JSON.stringify(s)}`);
}
async function action(group, label) {
  assert(await page.evaluate(([g,l]) => OpenGarrisonBrowserHost.invokeAutomationAction(g,l), [group,label]), label);
}
async function set(key, value) {
  assert(await page.evaluate(([k,v]) => OpenGarrisonBrowserHost.setAutomationValue(k,v), [key,value]), key);
}
async function disconnect() {
  assert(await page.evaluate(() => OpenGarrisonBrowserHost.runAutomationConsoleCommand('disconnect')));
  await wait(s => s.mainMenuOpen, 'return to menu');
}
let passed = false;
try {
  await page.goto(base, {waitUntil: 'domcontentloaded', timeout: 120000});
  const ready = await wait(s => !s.startupSplashOpen && s.mainMenuOpen && s.canEnterGameplaySession, 'main menu');
  assert.equal(ready.loadingTitle, 'Gang Garrison');
  assert.equal(ready.particleMode, 0);
  assert.equal(ready.reducedBrowserEffects, false);
  checks.normalEffects = true; checks.loadingTitle = ready.loadingTitle;
  await page.evaluate(() => OpenGarrisonBrowserHost.focusCanvas());
  await action('menu', 'Practice');
  assert(await page.evaluate(() => OpenGarrisonBrowserHost.startAutomationPractice(5,5)));
  await wait(s => s.teamSelectOpen, 'team'); await action('teamselect', 'RED');
  await wait(s => s.classSelectOpen, 'class'); await action('classselect', 'Soldier');
  const practice = await wait(s => s.localPlayerAlive && s.practiceBotNames?.length === 10, 'named bots');
  assert.equal(new Set(practice.practiceBotNames).size, 10);
  assert(practice.practiceBotNames.every(name => desktopNames.has(name)), JSON.stringify(practice.practiceBotNames));
  checks.names = practice.practiceBotNames;
  async function holdFireUntil(predicate, label) {
    const x = Math.floor(practice.viewportWidth * .75), y = Math.floor(practice.viewportHeight * .5);
    await page.evaluate(([x,y]) => OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync('HandleBrowserMouseButton', 0, true, x, y), [x,y]);
    try {return await wait(predicate, label, 10000);}
    finally {await page.evaluate(([x,y]) => OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync('HandleBrowserMouseButton', 0, false, x, y), [x,y]);}
  }
  const rockets = await holdFireUntil(s => s.rocketSmokeCount > 0, 'normal rocket smoke');
  checks.rocketSmokeCount = rockets.rocketSmokeCount;
  assert(await page.evaluate(() => OpenGarrisonBrowserHost.runAutomationConsoleCommand('set_class pyro')));
  const flames = await holdFireUntil(s => s.flameSmokeCount > 0, 'normal flame smoke');
  checks.flameSmokeCount = flames.flameSmokeCount;
  await page.screenshot({path: join(output, 'practice.png')});
  async function key(code, down) {
    await page.evaluate(([c,d]) => OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync('HandleBrowserKey', c, d), [code,down]);
    await delay(200);
  }
  await key('KeyZ', true);
  assert.notEqual((await state()).bubbleMenu, 'None');
  await page.screenshot({path: join(output, 'lua-bubble-wheel.png')});
  await key('KeyZ', false);
  await key('Backquote', true); await key('Backquote', false);
  await page.screenshot({path: join(output, 'console-recent.png')});
  await key('PageUp', true); await key('PageUp', false);
  await page.screenshot({path: join(output, 'console-earlier.png')});
  await key('Backquote', true); await key('Backquote', false);
  await disconnect();
  await action('menu', 'Last to Die'); await action('ltd', 'Play Solo'); await action('ltd', 'Standard');
  const survivor = await wait(s => s.lastToDie?.phase === 'SurvivorChoice', 'solo survivor selection');
  assert.equal(survivor.hostedLobbyDrawCount, 0);
  await page.screenshot({path: join(output, 'solo-survivor.png')});
  await set('ltd_survivor', 'ltd.survivor.soldier');
  const reward = await wait(s => s.lastToDie?.offerChoices?.length, 'starting reward');
  await set('ltd_reward', reward.lastToDie.offerChoices[0]);
  const playing = await wait(s => s.lastToDie?.phase === 'Playing' && s.localPlayerAlive, 'solo gameplay');
  assert.equal(playing.hostedLobbyDrawCount, 0);
  checks.soloLobbyFrames = playing.hostedLobbyDrawCount;
  // Simulation can enter Playing before the browser presents the first room frame.
  await delay(2000);
  assert.equal((await state()).hostedLobbyDrawCount, 0);
  await page.screenshot({path: join(output, 'solo-playing.png')});
  await disconnect();
  assert.deepEqual(errors, []); passed = true;
} finally {
  await writeFile(join(output, 'result.json'), JSON.stringify({passed, checks, errors}, null, 2));
  await browser.close();
}
