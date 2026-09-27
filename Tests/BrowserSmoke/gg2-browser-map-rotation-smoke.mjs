import { chromium } from "playwright";
import { spawn } from "node:child_process";
import { readFile } from "node:fs/promises";
import { createHash } from "node:crypto";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { setTimeout as delay } from "node:timers/promises";

const directory = dirname(fileURLToPath(import.meta.url));
const root = resolve(directory, "../..");
const browserUrl = process.env.OG_GG2_BROWSER_URL ?? "http://127.0.0.1:5014";
const browserOrigin = new URL(browserUrl).origin;
const custom = process.argv.includes("--custom");
const png = custom ? await readFile(join(root, "Core/Content/StockMaps/Gg2/koth_harvest.png")) : null;
const hash = png ? createHash("md5").update(png).digest("hex") : "";
const nextMap = custom ? `gg2_${hash}` : "gg2_stock_koth_harvest";
const fake = spawn("python", [join(directory, "gg2-map-rotation-fake.py"), ...(custom ? ["--custom"] : [])],
  { cwd: root, stdio: ["ignore", "pipe", "pipe"] });
let fakeErrors = "";
fake.stderr.on("data", chunk => { fakeErrors += chunk.toString(); });
const port = await new Promise((resolvePort, rejectPort) => {
  let output = "";
  fake.stdout.on("data", chunk => {
    output += chunk.toString();
    if (output.includes("\n")) resolvePort(Number.parseInt(output, 10));
  });
  fake.once("error", rejectPort);
  fake.once("exit", code => rejectPort(new Error(`Fake GG2 server exited: ${code}`)));
});
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
const errors = [];
page.on("pageerror", error => errors.push(error.stack ?? error.message));
page.on("console", message => { if (message.type() === "error") errors.push(message.text()); });
await page.route("https://api.superganggarrison.com/api/gg2/servers", route => route.fulfill({
  status: 200,
  contentType: "application/json",
  headers: { "access-control-allow-origin": browserOrigin },
  body: JSON.stringify({ servers: [{ host: "127.0.0.1", port, name: "Map rotation test",
    map: "koth_corinth", game: "gg2", version: "2.9.2", players: 0, bots: 0,
    slots: 10, isPrivate: false, isCompatible: true }] }),
}));
if (custom) {
  await page.route(new RegExp(`^https://api\\.superganggarrison\\.com/api/gg2/maps/${hash}\\.png`),
    route => { console.log(`custom map fetch: ${route.request().url()}`); return route.fulfill({ status: 200, contentType: "image/png", body: png,
      headers: { "access-control-allow-origin": browserOrigin } });
    });
}
await page.routeWebSocket(/^wss:\/\/api\.superganggarrison\.com\/api\/gg2\/ws\//, async route => {
  const target = new URL(route.url());
  const local = new WebSocket(`ws://127.0.0.1:8768${target.pathname}`);
  local.binaryType = "arraybuffer";
  await new Promise((resolveOpen, rejectOpen) => {
    local.addEventListener("open", resolveOpen, { once: true });
    local.addEventListener("error", rejectOpen, { once: true });
  });
  route.onMessage(message => local.send(message));
  local.addEventListener("message", event => route.send(Buffer.from(event.data)));
  local.addEventListener("close", event => console.log(`local gateway websocket closed: ${event.code} ${event.reason}`));
  route.onClose(() => local.close());
});

async function state() {
  return page.evaluate(async () => await globalThis.OpenGarrisonBrowserHost?.getAutomationState?.() ?? null);
}
async function waitFor(predicate, label, timeout = 45000) {
  const end = Date.now() + timeout;
  let last;
  const observations = [];
  let previous = "";
  while (Date.now() < end) {
    last = await state();
    if (predicate(last)) return last;
    const summary = JSON.stringify({ shell: last?.shell, map: last?.currentMap,
      connected: last?.networkConnected, status: last?.statusMessage,
      loading: last?.loadingOverlayVisible, team: last?.teamSelectOpen,
      frame: last?.lastAppliedSnapshotFrame });
    if (summary !== previous) observations.push(summary);
    previous = summary;
    await delay(200);
  }
  throw new Error(`${label} timed out: ${JSON.stringify(last)}\nTransitions:\n${observations.join("\n")}`);
}
async function key(code) {
  await page.evaluate(async value => await globalThis.OpenGarrisonBrowserHost.inputBridgeHost
    .invokeMethodAsync("HandleBrowserKey", value, true), code);
  await delay(100);
  await page.evaluate(async value => await globalThis.OpenGarrisonBrowserHost.inputBridgeHost
    .invokeMethodAsync("HandleBrowserKey", value, false), code);
}

try {
  await page.goto(browserUrl,
    { waitUntil: "networkidle", timeout: 120000 });
  await waitFor(s => s?.mainMenuOpen && !s.startupSplashOpen, "browser startup");
  if ((await state()).mainMenuOverlay === "NamePrompt") {
    await page.keyboard.type("RotationSmoke");
    await page.keyboard.press("Enter");
  }
  const menu = await waitFor(s => s?.mainMenuOverlay === "None"
    && s.menuButtons?.[0]?.label === "Servers", "Servers as first menu button");
  if (menu.menuButtons.some(button => button.label === "Play GG2")) {
    throw new Error("Old Play GG2 button is still visible");
  }
  if (!await page.evaluate(() => globalThis.OpenGarrisonBrowserHost.invokeAutomationAction("menu", "Servers"))) {
    throw new Error("Servers action failed");
  }
  await waitFor(s => s?.mainMenuOverlay === "LobbyBrowser" && !s.statusMessage, "GG2 server list");
  await key("Enter");
  await waitFor(s => s?.networkConnected && s.currentMap === "gg2_stock_koth_corinth",
    "first GG2 map");
  const changed = await waitFor(s => s?.networkConnected && s.currentMap === nextMap,
    "rotated GG2 map", 30000);
  const settled = await waitFor(s => s?.networkConnected
    && s.currentMap === nextMap
    && !s.loadingOverlayVisible && s.teamSelectOpen,
    "map rotation loading release and team menu", 12000);
  await page.screenshot({ path: join(directory, "artifacts/gg2-browser-map-rotation.png") });
  if (errors.length) throw new Error(`Browser errors:\n${errors.join("\n")}`);
  console.log(`GG2 browser map rotation passed: ${changed.currentMap}, frame ${settled.lastAppliedSnapshotFrame}`);
} catch (error) {
  console.error(error.stack ?? String(error));
  console.error(`browser state: ${JSON.stringify(await state().catch(() => null))}`);
  console.error(`console errors: ${errors.join("\n")}`);
  console.error(`fake server errors: ${fakeErrors}`);
  await page.screenshot({ path: join(directory, "artifacts/gg2-browser-map-rotation-failure.png") }).catch(() => {});
  process.exitCode = 1;
} finally {
  await browser.close();
  fake.kill();
}
