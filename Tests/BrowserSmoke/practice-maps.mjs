import assert from "node:assert/strict";
import { createReadStream } from "node:fs";
import { mkdir, readFile, stat, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, extname, join, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { setTimeout as delay } from "node:timers/promises";
import { chromium } from "playwright";

const repo = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const root = resolve(repo, process.env.OG_ROOM_BROWSER_ROOT ?? "artifacts/browser-practice-ltd-upload/browser-publish/wwwroot");
const output = join(repo, "Tests/BrowserSmoke/artifacts/practice-maps");
const base = process.env.OG_PRACTICE_URL ?? "http://127.0.0.1:5017";
const types = { ".html": "text/html", ".css": "text/css", ".js": "text/javascript", ".json": "application/json", ".wasm": "application/wasm", ".png": "image/png" };
await mkdir(output, { recursive: true });
const server = createServer(async (req, res) => {
  const pathname = new URL(req.url, base).pathname;
  const path = resolve(root, "." + decodeURIComponent(pathname === "/" ? "/index.html" : pathname));
  if (!path.startsWith(root + sep)) { res.writeHead(403).end(); return; }
  try {
    const info = await stat(path);
    if (!info.isFile()) throw new Error("not a file");
    res.writeHead(200, { "Content-Type": types[extname(path)] ?? "application/octet-stream", "Content-Length": info.size, "Cache-Control": "no-cache" });
    createReadStream(path).pipe(res);
  } catch { res.writeHead(404).end(); }
});
if (!process.env.OG_PRACTICE_URL) await new Promise(done => server.listen(5017, "127.0.0.1", done));
const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
const logs = [], external = [], results = [];
await context.route("**/*", async route => {
  const url = new URL(route.request().url());
  if (url.origin === new URL(base).origin) await route.continue();
  else { external.push(url.origin + url.pathname); await route.abort(); }
});
// Keep the shipped menu defaults, including its animated background and runners.
const page = await context.newPage();
page.on("console", message => logs.push(`${message.type()}: ${message.text()}`));
page.on("pageerror", error => logs.push(`PAGEERROR: ${error.stack}`));
page.on("websocket", socket => external.push(new URL(socket.url()).pathname));
async function state() { return page.evaluate(() => window.OpenGarrisonBrowserHost?.getAutomationState()); }
async function wait(predicate, label, timeout = 120000) {
  let current;
  for (const start = Date.now(); Date.now() - start < timeout;) {
    current = await state();
    if (current && predicate(current)) return current;
    if (current?.statusMessage?.includes("Failed to load local map")) throw new Error(current.statusMessage);
    await delay(250);
  }
  throw new Error(`Timeout ${label}: ${JSON.stringify(current)}`);
}
async function action(group, label) {
  assert.equal(await page.evaluate(([g, l]) => window.OpenGarrisonBrowserHost.invokeAutomationAction(g, l), [group, label]), true);
  await delay(150);
}
async function press(code) {
  await page.evaluate(code => window.OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync("HandleBrowserKey", code, true), code);
  await delay(80);
  await page.evaluate(code => window.OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync("HandleBrowserKey", code, false), code);
  await delay(150);
}
async function command(value) {
  await page.evaluate(value => window.OpenGarrisonBrowserHost.runAutomationConsoleCommand(value), value);
}
async function nextMap() {
  const s = await state();
  const right = s.practiceButtons.find(b => b.label === "Enemy Bots +").bounds;
  // The map selector shares the enemy selector's column, five rows above it.
  const rowGap = s.viewportHeight - 24 < 500 ? 6 : 8;
  const x = right.x + right.width / 2;
  const y = right.y - 5 * (right.height + rowGap) + right.height / 2;
  const box = await page.locator("canvas").boundingBox();
  const scale = Math.min(box.width / s.viewportWidth, box.height / s.viewportHeight);
  await page.mouse.click(box.x + (box.width - s.viewportWidth * scale) / 2 + x * scale,
    box.y + (box.height - s.viewportHeight * scale) / 2 + y * scale, { delay: 90 });
  await wait(n => n.selectedPracticeMap !== s.selectedPracticeMap, "next map", 5000);
}
try {
  assert.equal(JSON.parse(await readFile(join(root, "release.json"), "utf8")).aot, true);
  await page.goto(base, { waitUntil: "domcontentloaded", timeout: 180000 });
  await wait(s => !s.startupSplashOpen && s.mainMenuOpen && s.canEnterGameplaySession, "root menu");
  await page.locator("canvas").click({ position: { x: 20, y: 20 } });
  for (let index = 0; index < 27; index++) {
    await action("menu", "Practice");
    await press("Enter"); // Confirm the actual map browser before starting setup.
    if (index > 0) await nextMap();
    const selected = (await state()).selectedPracticeMap;
    assert.equal(results.some(r => r.map === selected), false, `Repeated map ${selected}`);
    console.log(`Practice ${index + 1}/27: ${selected}`);
    assert.equal(await page.evaluate(() => window.OpenGarrisonBrowserHost.setAutomationValue("practice_enemy_bots", "1")), true);
    await press("Enter");
    await wait(s => s.teamSelectOpen, `team selection on ${selected}`);
    await action("teamselect", "RED");
    await wait(s => s.classSelectOpen, "class selection");
    await action("classselect", "Pyro");
    const spawned = await wait(s => s.practiceSessionActive && s.localPlayerAlive, `spawn on ${selected}`);
    await command("+fire"); await delay(500); await command("-fire");
    await page.screenshot({ path: join(output, selected + ".png") });
    results.push({ map: selected, spawned: true, enemyBots: spawned.practiceEnemyBotCount, networkConnected: spawned.networkConnected });
    await command("disconnect");
    await wait(s => s.mainMenuOpen && s.mainMenuOverlay === "None", "return to menu");
  }
  for (const required of ["cp_coldfront_js", "Kulay", "Docking", "Harvest", "Conflict"]) assert(results.some(r => r.map === required));
  assert.equal(external.length, 0, "Practice must remain offline");
  assert.equal(logs.filter(l => l.startsWith("PAGEERROR:")).length, 0);
  console.log("PASS: all 27 Practice maps spawned with one enemy bot and no online requests");
} finally {
  await writeFile(join(output, "result.json"), JSON.stringify({ results, external, logs }, null, 2));
  await browser.close();
  if (server.listening) await new Promise(done => server.close(done));
}
