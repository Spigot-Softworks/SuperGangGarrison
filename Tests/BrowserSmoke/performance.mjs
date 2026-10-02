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
const requestedTargetFps = Number(process.env.OG_PERF_TARGET_FPS ?? 60);
const fpsTolerance = Number(process.env.OG_PERF_FPS_TOLERANCE ?? 3);
const configuredRefreshRateHz = process.env.OG_PERF_REFRESH_RATE_HZ === undefined
  ? null
  : Number(process.env.OG_PERF_REFRESH_RATE_HZ);
const configuredP95FrameMs = process.env.OG_PERF_P95_FRAME_MS === undefined
  ? null
  : Number(process.env.OG_PERF_P95_FRAME_MS);
const configuredP99FrameMs = process.env.OG_PERF_P99_FRAME_MS === undefined
  ? null
  : Number(process.env.OG_PERF_P99_FRAME_MS);
const configuredMaxFrameMs = process.env.OG_PERF_MAX_FRAME_MS === undefined
  ? null
  : Number(process.env.OG_PERF_MAX_FRAME_MS);
const maps = (process.env.OG_PERF_MAPS ?? '').split(',');
const profile = process.env.OG_PERF_PROFILE === '1';
const gpuMode = process.env.OG_PERF_GPU ?? 'default';
assert(enemies >= 0 && enemies <= 9 && friends >= 0 && friends <= 9, 'Each team supports 0–9 bots.');
assert(Number.isFinite(requestedTargetFps) && requestedTargetFps > 0, 'OG_PERF_TARGET_FPS must be positive.');
assert(Number.isFinite(fpsTolerance) && fpsTolerance >= 0, 'OG_PERF_FPS_TOLERANCE must be nonnegative.');
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
let displayRefreshHz = null;
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
async function sampleRefreshRateHz(page) {
  const hz = await page.evaluate(async () => await new Promise(resolvePromise => {
    const intervals = [];
    let previous = null;
    const startedAt = performance.now();
    function frame(timestamp) {
      if (previous !== null) intervals.push(timestamp - previous);
      previous = timestamp;
      if (performance.now() - startedAt >= 1200) {
        intervals.sort((left, right) => left - right);
        const fastRepresentative = intervals[Math.floor((intervals.length - 1) * 0.1)];
        resolvePromise(fastRepresentative > 0 ? Math.round(1000 / fastRepresentative) : 60);
        return;
      }

      requestAnimationFrame(frame);
    }

    requestAnimationFrame(frame);
  }));
  assert(Number.isFinite(hz) && hz > 0, 'Could not estimate the browser refresh rate.');
  return hz;
}
async function waitForFirstGameplayWorldDraw(page) {
  const deadline = Date.now() + 45_000;
  let latest = null;
  while (Date.now() < deadline) {
    latest = await page.evaluate(() => OpenGarrisonBrowserHost.getPerformanceSnapshot());
    const milliseconds = latest?.startupToGameplayWorldDrawMilliseconds;
    if (typeof milliseconds === 'number' && Number.isFinite(milliseconds) && milliseconds >= 0) {
      return milliseconds;
    }

    await delay(100);
  }

  throw Error(`First gameplay world draw was not observed: ${JSON.stringify(latest)}`);
}
function summarizeIntervals(intervals) {
  const sorted = intervals.filter(value => Number.isFinite(value) && value >= 0).toSorted((left, right) => left - right);
  const percentile = p => sorted.length === 0 ? 0 : sorted[Math.max(0, Math.ceil(p * sorted.length) - 1)];
  return {
    sampleCount: sorted.length,
    p50: percentile(0.50),
    p95: percentile(0.95),
    p99: percentile(0.99),
    max: sorted.at(-1) ?? 0
  };
}
try {
  await page.goto(base, {waitUntil: 'domcontentloaded', timeout: 120000});
  await wait(s => s.mainMenuOpen && !s.startupSplashOpen && s.canEnterGameplaySession, 'ready menu');
  await page.evaluate(() => OpenGarrisonBrowserHost.focusCanvas());
  displayRefreshHz = configuredRefreshRateHz ?? await sampleRefreshRateHz(page);
  const targetFps = Math.min(requestedTargetFps, displayRefreshHz);
  const targetFrameMs = 1000 / targetFps;
  const frameLimits = {
    minimumFps: Math.max(0, targetFps - fpsTolerance),
    p95Ms: configuredP95FrameMs ?? targetFrameMs * 1.2,
    p99Ms: configuredP99FrameMs ?? targetFrameMs * 2,
    maxMs: configuredMaxFrameMs ?? targetFrameMs * 3
  };
  assert(Object.values(frameLimits).every(Number.isFinite), 'Frame timing gates must be finite numbers.');
  for (const map of maps) for (let repeat = 0; repeat < repeats; repeat++) {
    await action('menu', 'Practice');
    if (map) assert(await page.evaluate(m => OpenGarrisonBrowserHost.setAutomationValue('practice_map', m), map), map);
    assert(await page.evaluate(([e,f]) => OpenGarrisonBrowserHost.startAutomationPractice(e,f), [enemies,friends]));
    await wait(s => s.teamSelectOpen, 'team select'); await action('teamselect', 'RED');
    await wait(s => s.classSelectOpen, 'class select'); await action('classselect', 'Soldier');
    await wait(s => s.practiceSessionActive && s.localPlayerAlive, 'spawn');
    const startupToGameplayWorldDrawMs = await waitForFirstGameplayWorldDraw(page);
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
      const probe = window.__performanceProbe = {
        start: performance.now(),
        previousRaf: null,
        rafIntervals: [],
        completionTimesMs: [],
        pumpDurationsMs: [],
        running: true
      };
      host.metrics.performanceFrameProbe = probe;
      function frame(t) {
        if (!probe.running) return;
        if (probe.previousRaf !== null) probe.rafIntervals.push(t - probe.previousRaf);
        probe.previousRaf = t;
        requestAnimationFrame(frame);
      }
      requestAnimationFrame(frame);
    });
    await delay(seconds * 1000);
    const timing = await page.evaluate(() => {
      const p = window.__performanceProbe; p.running = false;
      delete OpenGarrisonBrowserHost.metrics.performanceFrameProbe;
      return {
        elapsedMs: performance.now() - p.start,
        rafIntervals: p.rafIntervals,
        completionTimesMs: p.completionTimesMs,
        pumpDurationsMs: p.pumpDurationsMs,
        pumps: OpenGarrisonBrowserHost.metrics.completedPumps
      };
    });
    const after = await page.evaluate(() => OpenGarrisonBrowserHost.getPerformanceSnapshot());
    if (profile) await writeFile(join(output, `${id}.cpuprofile.json`), JSON.stringify(await cdp.send('Profiler.stop')));
    await cdp.detach();
    const delta = {};
    for (const key of Object.keys(after)) if (key.startsWith('average')) delta[key] = (after[key] * after.samples - before[key] * before.samples) / (after.samples - before.samples);
    const frameIntervals = timing.completionTimesMs.slice(1).map((time, index) => time - timing.completionTimesMs[index]);
    const frameSummary = summarizeIntervals(frameIntervals);
    const rafSummary = summarizeIntervals(timing.rafIntervals);
    const pumpCpuSummary = summarizeIntervals(timing.pumpDurationsMs);
    const measuredPumpFps = timing.completionTimesMs.length > 1
      ? (timing.completionTimesMs.length - 1) * 1000
        / (timing.completionTimesMs.at(-1) - timing.completionTimesMs[0])
      : 0;
    const passed = measuredPumpFps >= frameLimits.minimumFps
      && frameSummary.p95 <= frameLimits.p95Ms
      && frameSummary.p99 <= frameLimits.p99Ms
      && frameSummary.max <= frameLimits.maxMs;
    const result = {map: initial.currentMap, enemies, friends, particles, repeat, startedAt, measuredSeconds: timing.elapsedMs / 1000,
      displayRefreshHz, targetFps, measuredPumpFps, passed, frameLimits,
      frameMs: frameSummary,
      hitches: {
        over16Point67: frameIntervals.filter(value => value > 16.67).length,
        over33Point34: frameIntervals.filter(value => value > 33.34).length,
        over50: frameIntervals.filter(value => value > 50).length
      },
      updateCpuMs: delta.averageUpdateMs,
      drawCpuMs: delta.averageDrawMs,
      gamePumpCpuMs: pumpCpuSummary,
      rafMs: rafSummary,
      startupToGameplayWorldDrawMs,
      delta,
      simulationMsPerTick: delta.averageSimulationMs / delta.averageSimulationTicks,
      names: initial.practiceBotNames, final: await state()};
    samples.push(result);
    console.log(JSON.stringify({...result, final: undefined, names: undefined}));
    assert(passed,
      `Completed game-pump timing missed ${targetFps}Hz gate: ${JSON.stringify({measuredPumpFps, frameMs: frameSummary, frameLimits, displayRefreshHz})}`);
    assert(await page.evaluate(() => OpenGarrisonBrowserHost.runAutomationConsoleCommand('disconnect')));
    await wait(s => s.mainMenuOpen, 'return to menu');
  }
  assert.deepEqual(errors, []);
} finally {
  await writeFile(join(output, 'result.json'), JSON.stringify({base, gpuMode, gpu: system.gpu, profile, requestedTargetFps, displayRefreshHz: typeof displayRefreshHz === 'undefined' ? null : displayRefreshHz, samples, errors}, null, 2));
  await browser.close();
}
