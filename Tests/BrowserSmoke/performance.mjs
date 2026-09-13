import assert from 'node:assert/strict';
import {chromium} from 'playwright';
import {mkdir, writeFile} from 'node:fs/promises';
import {resolve, join} from 'node:path';
import {setTimeout as delay} from 'node:timers/promises';

const base = process.env.OG_BROWSER_URL ?? 'http://127.0.0.1:5078';
const output = resolve(process.env.OG_BROWSER_QA_DIR ?? 'artifacts/browser-performance');
const enemies = Number(process.env.OG_PERF_ENEMIES ?? 5);
const friends = Number(process.env.OG_PERF_FRIENDS ?? 5);
const seconds = Number(process.env.OG_PERF_SECONDS ?? 40);
const particles = Number(process.env.OG_PERF_PARTICLES ?? 2);
const repeats = Number(process.env.OG_PERF_REPEATS ?? 1);
const maps = (process.env.OG_PERF_MAPS ?? '').split(',');
const profile = process.env.OG_PERF_PROFILE === '1';
const gpuMode = process.env.OG_PERF_GPU ?? 'default';
assert(enemies >= 0 && enemies <= 9 && friends >= 0 && friends <= 9, 'Each team supports 0–9 bots.');
await mkdir(output, {recursive: true});
const browser = await chromium.launch({headless: true,
  args: gpuMode === 'hardware' ? ['--enable-gpu', '--use-angle=d3d11'] : []});
const browserCdp = await browser.newBrowserCDPSession();
const system = await browserCdp.send('SystemInfo.getInfo');
const context = await browser.newContext({viewport: {width: 1280, height: 720}});
await context.addInitScript(mode => {
  localStorage.setItem('opengarrison:first-play-hints-v1', JSON.stringify({HasShown: true}));
  localStorage.setItem('opengarrison:settings-v1', JSON.stringify({ParticleMode: mode}));
}, particles);
const page = await context.newPage();
const errors = [], samples = [];
page.on('pageerror', error => errors.push(error.message));
page.on('console', message => {if (message.type() === 'error') errors.push(message.text());});
async function state() {return page.evaluate(() => OpenGarrisonBrowserHost.getAutomationState());}
async function wait(predicate, label) {
  let latest;
  for (const end = Date.now() + 180000; Date.now() < end;) {
    latest = await state();
    if (latest && predicate(latest)) return latest;
    await delay(250);
  }
  throw Error(`${label}: ${JSON.stringify(latest)}`);
}
async function action(group, label) {
  assert(await page.evaluate(([g,l]) => OpenGarrisonBrowserHost.invokeAutomationAction(g,l), [group,label]), label);
}
try {
  await page.goto(base, {waitUntil: 'domcontentloaded', timeout: 120000});
  await wait(s => s.mainMenuOpen && !s.startupSplashOpen && s.canEnterGameplaySession, 'ready menu');
  await page.evaluate(() => OpenGarrisonBrowserHost.focusCanvas());
  for (const map of maps) for (let repeat = 0; repeat < repeats; repeat++) {
    await action('menu', 'Practice');
    if (map) assert(await page.evaluate(m => OpenGarrisonBrowserHost.setAutomationValue('practice_map', m), map), map);
    assert(await page.evaluate(([e,f]) => OpenGarrisonBrowserHost.startAutomationPractice(e,f), [enemies,friends]));
    await wait(s => s.teamSelectOpen, 'team select'); await action('teamselect', 'RED');
    await wait(s => s.classSelectOpen, 'class select'); await action('classselect', 'Soldier');
    await wait(s => s.practiceSessionActive && s.localPlayerAlive, 'spawn');
    await delay(16000);
    const initial = await state();
    assert.equal(initial.practiceEnemyBotCount, enemies);
    assert.equal(initial.practiceFriendlyBotCount, friends);
    if (initial.practiceBotNames) assert.equal(initial.practiceBotNames.length, enemies + friends);
    if (initial.particleMode !== undefined) assert.equal(initial.particleMode, particles);
    const id = `${initial.currentMap}-${particles}-${repeat}`;
    await page.screenshot({path: join(output, `${id}.png`)});
    const cdp = await context.newCDPSession(page);
    if (profile) {await cdp.send('Profiler.enable'); await cdp.send('Profiler.start');}
    const before = await page.evaluate(() => OpenGarrisonBrowserHost.getPerformanceSnapshot());
    const startedAt = new Date().toISOString();
    await page.evaluate(() => {
      const host = OpenGarrisonBrowserHost; host.resetMetrics();
      const probe = window.__performanceProbe = {start: performance.now(), previous: 0, intervals: [], bins: [], priorCount: 0, binStart: performance.now(), running: true};
      function frame(t) {
        if (!probe.running) return;
        if (probe.previous) probe.intervals.push(t - probe.previous);
        probe.previous = t;
        if (t - probe.binStart >= 1000) {
          const count = host.metrics.completedPumps;
          probe.bins.push((count - probe.priorCount) * 1000 / (t - probe.binStart));
          probe.priorCount = count; probe.binStart = t;
        }
        requestAnimationFrame(frame);
      }
      requestAnimationFrame(frame);
    });
    await delay(seconds * 1000);
    const timing = await page.evaluate(() => {
      const p = window.__performanceProbe; p.running = false;
      return {elapsedMs: performance.now() - p.start, intervals: p.intervals, fpsBins: p.bins, pumps: OpenGarrisonBrowserHost.metrics.completedPumps};
    });
    const after = await page.evaluate(() => OpenGarrisonBrowserHost.getPerformanceSnapshot());
    if (profile) await writeFile(join(output, `${id}.cpuprofile.json`), JSON.stringify(await cdp.send('Profiler.stop')));
    await cdp.detach();
    const delta = {};
    for (const key of Object.keys(after)) if (key.startsWith('average')) delta[key] = (after[key] * after.samples - before[key] * before.samples) / (after.samples - before.samples);
    const sorted = timing.intervals.toSorted((a,b) => a-b);
    const percentile = p => sorted[Math.floor((sorted.length - 1) * p)];
    const result = {map: initial.currentMap, enemies, friends, particles, repeat, startedAt, measuredSeconds: timing.elapsedMs / 1000,
      fps: timing.pumps * 1000 / timing.elapsedMs, minimumOneSecondFps: Math.min(...timing.fpsBins), fpsBins: timing.fpsBins,
      frameMs: {p50: percentile(.5), p95: percentile(.95), p99: percentile(.99)}, delta,
      simulationMsPerTick: delta.averageSimulationMs / delta.averageSimulationTicks,
      names: initial.practiceBotNames, final: await state()};
    samples.push(result);
    console.log(JSON.stringify({...result, final: undefined, fpsBins: undefined, names: undefined}));
    assert(await page.evaluate(() => OpenGarrisonBrowserHost.runAutomationConsoleCommand('disconnect')));
    await wait(s => s.mainMenuOpen, 'return to menu');
  }
  assert.deepEqual(errors, []);
} finally {
  await writeFile(join(output, 'result.json'), JSON.stringify({base, gpuMode, gpu: system.gpu, profile, samples, errors}, null, 2));
  await browser.close();
}
