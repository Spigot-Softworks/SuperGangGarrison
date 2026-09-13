import assert from "node:assert/strict";
import { createReadStream } from "node:fs";
import { mkdir, readFile, stat, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, extname, join, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { setTimeout as delay } from "node:timers/promises";
import { chromium } from "playwright";

const repo = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const root = resolve(repo, process.env.OG_ROOM_BROWSER_ROOT ?? "artifacts/browser-ltd-repair-20260906/browser-publish/wwwroot");
const output = resolve(repo, process.env.OG_RESIZE_OUTPUT ?? "Tests/BrowserSmoke/artifacts/resizing");
const base = "http://127.0.0.1:5018";
const types = { ".html": "text/html", ".css": "text/css", ".js": "text/javascript", ".json": "application/json", ".wasm": "application/wasm", ".png": "image/png" };
const release = JSON.parse(await readFile(join(root, "release.json"), "utf8"));
assert.equal(release.aot, true);
await mkdir(output, { recursive: true });
const server = createServer(async (req, res) => {
  const name = new URL(req.url, base).pathname;
  const path = resolve(root, "." + decodeURIComponent(name === "/" ? "/index.html" : name));
  if (!path.startsWith(root + sep)) { res.writeHead(403).end(); return; }
  try {
    const info = await stat(path);
    if (!info.isFile()) throw Error("Not a file");
    res.writeHead(200, { "Content-Type": types[extname(path)] ?? "application/octet-stream", "Content-Length": info.size });
    createReadStream(path).pipe(res);
  } catch { res.writeHead(404).end(); }
});
await new Promise(done => server.listen(5018, "127.0.0.1", done));
const browser = await chromium.launch({ headless: true, args: process.env.OG_BROWSER_GPU === 'd3d11' ? ['--use-angle=d3d11', '--enable-gpu'] : [] });
const results = [], errors = [];
let activePage;
async function state(page) { return page.evaluate(() => window.OpenGarrisonBrowserHost?.getAutomationState()); }
async function closeSettings(page) {
  // Send the game's Back key without letting browser chrome consume Escape
  // and leave fullscreen before its geometry has been checked.
  await page.evaluate(() => window.OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync("HandleBrowserKey", "Escape", true));
  await delay(90);
  await page.evaluate(() => window.OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync("HandleBrowserKey", "Escape", false));
  await wait(page, s => s.mainMenuOverlay === "None");
}
async function wait(page, predicate, timeout = 30000) {
  for (const start = Date.now(); Date.now() - start < timeout;) {
    const value = await state(page);
    if (value && predicate(value)) return value;
    await delay(200);
  }
  throw Error("Browser state timed out");
}
async function check(page, label) {
  console.log("CHECK", label);
  await delay(250);
  const geometry = await page.evaluate(() => {
    const canvas = document.querySelector("canvas"), box = canvas.getBoundingClientRect();
    return { window: [innerWidth, innerHeight], buffer: [canvas.width, canvas.height],
      display: [box.x, box.y, box.width, box.height], dpr: devicePixelRatio };
  });
  assert.deepEqual(geometry.buffer, geometry.window, label + " drawing buffer");
  assert.deepEqual(geometry.display, [0, 0, ...geometry.window], label + " display rectangle");
  const s = await state(page), bounds = s.menuButtons.find(b => b.label === "Settings").bounds;
  const [width, height] = geometry.window;
  const scale = Math.min(width / s.viewportWidth, height / s.viewportHeight);
  await page.mouse.click((width - s.viewportWidth * scale) / 2 + (bounds.x + bounds.width / 2) * scale,
    (height - s.viewportHeight * scale) / 2 + (bounds.y + bounds.height / 2) * scale, { delay: 90 });
  const clicked = await state(page);
  console.log("CLICK", JSON.stringify({ overlay: clicked.mainMenuOverlay, focused: clicked.browserInputFocused }));
  await wait(page, s => s.mainMenuOverlay === "OptionsMenu");
  await closeSettings(page);
  results.push({ label, aspect: s.ingameResolution, ...geometry, settingsClick: true });
  await page.screenshot({ path: join(output, label + ".png") });
}
try {
  for (const dpr of [1, 2]) for (const [aspect, name] of [[3, "16x9"], [1, "4x3"], [0, "5x4"]]) {
    const context = await browser.newContext({ viewport: { width: 1280, height: 720 }, deviceScaleFactor: dpr });
    await context.route("**/*", route => new URL(route.request().url()).origin === base ? route.continue() : route.abort());
    await context.addInitScript(aspect => localStorage.setItem("opengarrison:settings-v1", JSON.stringify({ IngameResolution: aspect })), aspect);
    const page = await context.newPage();
    activePage = page;
    page.on("pageerror", error => errors.push(String(error)));
    await page.goto(base, { waitUntil: "domcontentloaded", timeout: 180000 });
    const initial = await wait(page, s => s.canEnterGameplaySession && !s.startupSplashOpen && s.mainMenuOpen, 150000);
    assert.equal(initial.ingameResolution, name.replace("x", ":"));
    for (const [width, height] of [[1280, 720], [1920, 1080], [640, 480], [360, 640], [800, 300]]) {
      await page.setViewportSize({ width, height });
      await check(page, `dpr${dpr}-${name}-${width}x${height}`);
    }
    // Exercise the real browser fullscreen API with a user-activated test button.
    await page.setViewportSize({ width: 1280, height: 720 });
    await page.evaluate(() => {
      const button = document.createElement("button");
      button.id = "fullscreen-test"; button.textContent = "Fullscreen";
      button.style.cssText = "position:fixed;top:0;right:0;z-index:1000";
      button.onclick = () => document.querySelector("canvas").requestFullscreen();
      document.body.append(button);
    });
    await page.locator("#fullscreen-test").click();
    await page.waitForFunction(() => !!document.fullscreenElement);
    // The test button takes focus away from the game. Restore it before
    // testing geometry: the game intentionally consumes activation clicks.
    await page.locator("canvas").focus();
    await check(page, `dpr${dpr}-${name}-fullscreen`);
    await page.evaluate(() => document.exitFullscreen());
    await page.locator("#fullscreen-test").evaluate(button => button.remove());
    await check(page, `dpr${dpr}-${name}-after-fullscreen`);
    // Desktop zoom changes both the CSS viewport and the effective device ratio.
    // Emulate that combination at 125% without relying on headless browser chrome.
    const cdp = await context.newCDPSession(page);
    await cdp.send("Emulation.setDeviceMetricsOverride", { width: 1024, height: 576,
      deviceScaleFactor: dpr * 1.25, mobile: false, screenWidth: 1280, screenHeight: 720 });
    await check(page, `dpr${dpr}-${name}-zoom125-emulated`);
    await cdp.send("Emulation.clearDeviceMetricsOverride");
    await page.setViewportSize({ width: 1280, height: 720 });
    // The first browser graphics row must now be the working Aspect Ratio control.
    const before = await state(page);
    await page.evaluate(() => window.OpenGarrisonBrowserHost.invokeAutomationAction("menu", "Settings"));
    await wait(page, s => s.mainMenuOverlay === "OptionsMenu");
    const panelHeight = Math.min(before.viewportHeight - 32, 620), compact = panelHeight < 540;
    const logicalY = (before.viewportHeight - panelHeight) / 2 + (compact ? 100 : 114) + (compact ? 14 : 16);
    const scale = Math.min(1280 / before.viewportWidth, 720 / before.viewportHeight);
    await page.mouse.click(640, (720 - before.viewportHeight * scale) / 2 + logicalY * scale, { delay: 90 });
    await wait(page, s => s.ingameResolution !== before.ingameResolution);
    await closeSettings(page);
    await check(page, `dpr${dpr}-${name}-changed-aspect`);
    await context.close();
    console.log(`PASS: DPR ${dpr}, ${name}, five window sizes and fullscreen`);
  }
  assert.deepEqual(errors, []);
  await writeFile(join(output, "result.json"), JSON.stringify({ passed: true, release, results, errors }, null, 2));
} catch (error) {
  if (activePage && !activePage.isClosed()) {
    await activePage.screenshot({ path: join(output, "failure.png") }).catch(() => {});
    await writeFile(join(output, "failure.json"), JSON.stringify(await state(activePage), null, 2));
  }
  throw error;
} finally {
  await browser.close();
  await new Promise(done => server.close(done));
}
