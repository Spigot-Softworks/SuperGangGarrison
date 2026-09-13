import { chromium } from 'playwright';
import { readFile, mkdir, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { resolve, join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import assert from 'node:assert/strict';

const root = fileURLToPath(new URL('../../', import.meta.url));
const output = resolve(process.env.OG_BROWSER_QA_DIR ?? 'artifacts/loading-progress');
await mkdir(output, { recursive: true });
const css = await readFile(join(root, 'Client.Browser/wwwroot/css/app.css'), 'utf8');
const script = await readFile(join(root, 'Client.Browser/wwwroot/loading-progress.js'), 'utf8');
const browser = await chromium.launch({ headless: true, args: ['--enable-gpu', '--use-angle=d3d11'] });
try {
    const page = await browser.newPage({ viewport: { width: 800, height: 600 } });
    await page.setContent(`<style>${css}</style><div class="game-shell"></div>`);
    await page.addScriptTag({ content: script });
    await page.evaluate(() => OpenGarrisonLoadingProgress.show(.2, .3, .5, .04));
    await page.waitForTimeout(200);
    const bounds = await page.locator('.game-loading-progress').boundingBox();
    assert.deepEqual(bounds, { x: 160, y: 180, width: 400, height: 24 });
    assert.equal(await page.locator('.game-loading-progress').getAttribute('aria-valuenow'), null);
    // Trace screenshots are produced by Chrome's compositor; ordinary
    // screenshot commands wait for the busy main thread and cannot prove this.
    const cdp = await page.context().newCDPSession(page);
    const trace = [];
    cdp.on('Tracing.dataCollected', event => trace.push(...event.value));
    await cdp.send('Tracing.start', { categories: 'disabled-by-default-devtools.screenshot,blink.user_timing', options: 'recordAsMuchAsPossible' });
    await page.evaluate(() => {
        performance.mark('loading-busy-start');
        const end = performance.now() + 2200;
        while (performance.now() < end) { /* synchronous WASM/map-work analogue */ }
        performance.mark('loading-busy-end');
    });
    await delay(100);
    const traceComplete = new Promise(resolve => cdp.once('Tracing.tracingComplete', resolve));
    await cdp.send('Tracing.end');
    await traceComplete;
    const start = trace.find(event => event.name === 'loading-busy-start').ts;
    const end = trace.find(event => event.name === 'loading-busy-end').ts;
    const frames = trace.filter(event => event.name === 'Screenshot' && event.ts > start && event.ts < end);
    await writeFile(join(output, 'trace-summary.json'), JSON.stringify({ start, end, frames: frames.map(event => ({ ts: event.ts, bytes: event.args.snapshot.length })) }, null, 2));
    assert(frames.length > 1, 'No compositor frames captured during blocked main-thread work');
    const first = frames[0].args.snapshot;
    const second = frames.find(event => event.args.snapshot !== first)?.args.snapshot;
    assert(second, 'Loading bar froze with the main thread');
    await writeFile(join(output, 'busy-first.jpg'), Buffer.from(first, 'base64'));
    await writeFile(join(output, 'busy-second.jpg'), Buffer.from(second, 'base64'));
    await page.setViewportSize({ width: 1200, height: 720 });
    const resized = await page.locator('.game-loading-progress').boundingBox();
    for (const [key, expected] of Object.entries({ x: 240, y: 216, width: 600, height: 28.8 }))
        assert(Math.abs(resized[key] - expected) < .05, `Resized ${key} does not match canvas`);
    await page.evaluate(() => {
        OpenGarrisonLoadingProgress.hide();
        OpenGarrisonLoadingProgress.hide();
    });
    assert.equal(await page.locator('.game-loading-progress').isVisible(), false);
    await page.evaluate(() => OpenGarrisonLoadingProgress.show(.1, .2, .6, .05));
    assert.equal(await page.locator('.game-loading-progress').count(), 1);
    assert.equal(await page.locator('.game-loading-progress').isVisible(), true);
    console.log('Loading bar stays animated during blocked main-thread work; resizing and hide/reopen pass.');
} finally {
    await browser.close();
}
