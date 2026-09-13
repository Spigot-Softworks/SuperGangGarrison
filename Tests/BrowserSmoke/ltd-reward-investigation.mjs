import { chromium } from 'playwright';
import { mkdir, writeFile } from 'node:fs/promises';
import { join, resolve, relative, isAbsolute, extname } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import assert from 'node:assert/strict';

const output = process.env.OG_BROWSER_QA_DIR;
assert(output);
await mkdir(output, { recursive: true });
const browser = await chromium.launch({ headless: true, args: ['--enable-gpu', '--use-angle=d3d11', '--disable-background-timer-throttling', '--disable-renderer-backgrounding'] });
const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
if (process.env.OG_BROWSER_LOCAL_ROOT) {
  const root = resolve(process.env.OG_BROWSER_LOCAL_ROOT);
  const types = { '.html':'text/html', '.js':'text/javascript', '.css':'text/css', '.json':'application/json', '.wasm':'application/wasm', '.png':'image/png' };
  await context.route('https://superganggarrison.com/**', async route => {
    const pathname = new URL(route.request().url()).pathname;
    const path = resolve(root, '.' + decodeURIComponent(pathname === '/' ? '/index.html' : pathname));
    const within = relative(root, path);
    if (within.startsWith('..') || isAbsolute(within)) return route.abort();
    try { await route.fulfill({ path, contentType: types[extname(path)] ?? 'application/octet-stream', headers: { 'Cache-Control':'no-store' } }); }
    catch (error) { if (error.code === 'ENOENT') await route.fulfill({ status:404, body:'Not found' }); else throw error; }
  });
}
await context.addInitScript(() => localStorage.setItem('opengarrison:first-play-hints-v1', JSON.stringify({ HasShown: true })));
const page = await context.newPage();
const errors = [], findings = [];
page.on('pageerror', error => errors.push(error.message));
const state = () => page.evaluate(() => OpenGarrisonBrowserHost.getAutomationState());
async function wait(predicate, label) {
  console.log(label);
  for (const end = Date.now() + 180000; Date.now() < end;) {
    const s = await state();
    await writeFile(join(output, 'latest-state.json'), JSON.stringify({label, state:s}, null, 2));
    if (s && predicate(s)) return s;
    await delay(50);
  }
  throw Error(label);
}
const action = (group, label) => page.evaluate(([g,l]) => OpenGarrisonBrowserHost.invokeAutomationAction(g,l), [group,label]);
const value = (key, val) => page.evaluate(([k,v]) => OpenGarrisonBrowserHost.setAutomationValue(k,v), [key,val]);
const win = () => page.evaluate(() => OpenGarrisonBrowserHost.runAutomationConsoleCommand('ltd_win'));
const playing = () => wait(s => s.lastToDie?.phase === 'Playing' && s.localPlayerAlive, 'Playing');
async function choose(s) {
  assert(await value('ltd_reward', s.lastToDie.offerChoices[0]));
  await playing();
  await delay(500);
}
let release, passed = false;
try {
  await page.goto('https://superganggarrison.com/', { waitUntil: 'domcontentloaded', timeout: 120000 });
  release = await page.evaluate(async () => (await fetch('release.json')).json());
  await wait(s => s.mainMenuOpen && !s.startupSplashOpen && s.canEnterGameplaySession, 'Menu');
  await page.evaluate(() => OpenGarrisonBrowserHost.focusCanvas());
  assert(await action('menu','Last to Die'));
  assert(await action('ltd','Play Solo'));
  assert(await action('ltd','Standard'));
  await wait(s => s.lastToDie?.phase === 'SurvivorChoice', 'Survivor');
  assert(await value('ltd_survivor','ltd.survivor.soldier'));
  await choose(await wait(s => s.lastToDie?.offerChoices?.length, 'Opening reward'));

  await win();
  let offered = await wait(s => s.lastToDie?.phase === 'RewardChoice', 'No-input reward');
  assert.equal(offered.lastToDie.offerChoices.length, 3);
  await delay(5000);
  let after = await state();
  assert.equal(after.lastToDie.phase, 'RewardChoice');
  assert.deepEqual(after.lastToDie.offerChoices, offered.lastToDie.offerChoices);
  findings.push({ scenario: 'no input for 5 seconds', phase: after.lastToDie.phase, choices: after.lastToDie.offerChoices });
  await page.screenshot({ path: join(output, 'reward-visible.png') });
  const canvas = await page.locator('canvas').first().boundingBox();
  const panelWidth = Math.min(offered.viewportWidth - 48, 980);
  const panelHeight = Math.min(offered.viewportHeight - 40, 440);
  const x = (offered.viewportWidth - panelWidth) / 2 + 100;
  const y = (offered.viewportHeight - panelHeight) / 2 + 200;
  await page.mouse.move(canvas.x + x * canvas.width / offered.viewportWidth, canvas.y + y * canvas.height / offered.viewportHeight);
  await choose(offered);

  await page.mouse.down();
  await delay(250);
  await win();
  offered = await wait(s => s.lastToDie?.phase === 'RewardChoice', 'Held-fire reward');
  await delay(3000);
  after = await state();
  assert.equal(after.lastToDie.phase, 'RewardChoice');
  findings.push({ scenario: 'M1 held across transition for 3 seconds', phase: after.lastToDie.phase, choices: after.lastToDie.offerChoices });
  await page.mouse.up();
  await choose(after);

  // Keep clicking the same aiming location across stage completion. These are
  // real mouse events; automation selects no reward during this scenario.
  const began = Date.now(), samples = [];
  await win();
  for (let i = 0; i < 30; i++) {
    const s = await state();
    samples.push({ elapsedMs: Date.now() - began, phase: s.lastToDie?.phase, choices: s.lastToDie?.offerChoices });
    await page.mouse.down();
    await delay(35);
    await page.mouse.up();
    await delay(65);
  }
  after = await state();
  findings.push({ scenario: 'repeated firing clicks across transition', samples, finalPhase: after.lastToDie.phase });
  await page.screenshot({ path: join(output, 'after-repeated-clicks.png') });
  if (process.env.OG_REWARD_EXPECT_CONFIRM === '1') {
    assert.equal(after.lastToDie.phase, 'RewardChoice', 'Rapid clicks must not commit a reward');
    assert.equal(after.lastToDie.offerChoices.length, 3);
    const confirmX = (offered.viewportWidth + panelWidth) / 2 - 108;
    const confirmY = (offered.viewportHeight - panelHeight) / 2 + 106;
    await page.mouse.click(canvas.x + confirmX * canvas.width / offered.viewportWidth, canvas.y + confirmY * canvas.height / offered.viewportHeight, { delay: 120 });
    await playing();
    findings.push({ scenario: 'separate Confirm click', phase: 'Playing' });

    await delay(500);
    await win();
    await wait(s => s.lastToDie?.phase === 'RewardChoice', 'Fresh offer keyboard test');
    await delay(900);
    await page.keyboard.press('Enter', { delay: 120 });
    await delay(300);
    assert.equal((await state()).lastToDie.phase, 'RewardChoice', 'Old choice must not carry to next offer');
    await page.keyboard.press('2', { delay: 120 });
    await delay(300);
    assert.equal((await state()).lastToDie.phase, 'RewardChoice', 'Number key should only select');
    await page.screenshot({ path: join(output, 'keyboard-selected.png') });
    await page.keyboard.press('Enter', { delay: 120 });
    await playing();
    findings.push({ scenario: 'new offer reset, number selects, Enter confirms', phase: 'Playing' });
  }
  if (process.env.OG_LTD_TEST_EXIT_PRACTICE === '1') {
    const beforeExit = await state();
    assert.equal(beforeExit.automaticRespawnSuppressed, true);
    assert.equal(beforeExit.droppedWeaponPickupsEnabled, true);
    await page.evaluate(() => OpenGarrisonBrowserHost.runAutomationConsoleCommand('disconnect'));
    await wait(s => s.mainMenuOpen, 'Returned from LTD');
    assert(await action('menu', 'Practice'));
    await page.evaluate(() => OpenGarrisonBrowserHost.startAutomationPractice(0, 0));
    const joinState = await wait(s => s.teamSelectOpen || s.classSelectOpen, 'Practice join after LTD');
    if (joinState.teamSelectOpen) assert(await action('teamselect', 'RED'));
    await wait(s => s.classSelectOpen, 'Practice class');
    assert(await action('classselect', 'Soldier'));
    const practice = await wait(s => s.practiceSessionActive && s.localPlayerAlive, 'Practice alive');
    assert.equal(practice.automaticRespawnSuppressed, false);
    assert.equal(practice.droppedWeaponPickupsEnabled, false);
    assert.equal(practice.survivorBuffActive, false);
    // Let the class-selection respawn finish before forcing a separate death.
    await delay(500);
    const killed = await page.evaluate(async () => {
      const accepted = await OpenGarrisonBrowserHost.runAutomationConsoleCommand('killme');
      return { accepted, state: await OpenGarrisonBrowserHost.getAutomationState() };
    });
    assert(killed.accepted, 'Practice kill command was not accepted');
    assert.equal(killed.state.localPlayerAlive, false, 'Practice death did not occur');
    await wait(s => s.localPlayerAlive, 'Practice automatic respawn');
    findings.push({ scenario: 'LTD exit into Practice clears pickups and automatically respawns', beforeExit, practice });
  }
  assert.deepEqual(errors, []);
  passed = true;
  console.log(JSON.stringify(findings));
} finally {
  await writeFile(join(output, 'result.json'), JSON.stringify({ passed, release, findings, errors }, null, 2));
  try { await action('ltd', 'Leave Room'); } catch {}
  await browser.close();
}
