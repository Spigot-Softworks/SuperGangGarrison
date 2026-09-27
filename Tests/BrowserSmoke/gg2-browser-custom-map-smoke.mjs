import { chromium } from "playwright";
import { spawn } from "node:child_process";
import { readFile } from "node:fs/promises";
import { createHash } from "node:crypto";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { setTimeout as delay } from "node:timers/promises";

const directory = dirname(fileURLToPath(import.meta.url));
const root = resolve(directory, "../..");
const png = await readFile(join(root, "Core/Content/StockMaps/Gg2/koth_corinth.png"));
const hash = createHash("md5").update(png).digest("hex");
const fake = spawn("python", [join(directory, "gg2-gateway-custom-map-smoke.py"), "--serve"],
  { cwd: root, stdio: ["ignore", "pipe", "pipe"] });
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
await page.route("http://127.0.0.1:8768/api/gg2/servers", route => route.fulfill({
  status: 200,
  contentType: "application/json",
  headers: { "access-control-allow-origin": "http://127.0.0.1:5014" },
  body: JSON.stringify({ servers: [{ host: "127.0.0.1", port, name: "Custom map test",
    map: "koth_corinth", game: "gg2", version: "2.9.2", players: 0, bots: 0,
    slots: 10, isPrivate: false, isCompatible: true }] }),
}));

async function state() {
  return page.evaluate(async () => await globalThis.OpenGarrisonBrowserHost?.getAutomationState?.() ?? null);
}
async function waitFor(predicate, label, timeout = 90000) {
  const end = Date.now() + timeout;
  let last;
  while (Date.now() < end) {
    last = await state();
    if (predicate(last)) return last;
    await delay(250);
  }
  throw new Error(`${label} timed out: ${JSON.stringify(last)}`);
}
async function key(code) {
  await page.evaluate(async value => await globalThis.OpenGarrisonBrowserHost.inputBridgeHost
    .invokeMethodAsync("HandleBrowserKey", value, true), code);
  await delay(120);
  await page.evaluate(async value => await globalThis.OpenGarrisonBrowserHost.inputBridgeHost
    .invokeMethodAsync("HandleBrowserKey", value, false), code);
}

try {
  await page.goto(process.env.OG_GG2_BROWSER_URL ?? "http://127.0.0.1:5014",
    { waitUntil: "networkidle", timeout: 120000 });
  await waitFor(s => s?.mainMenuOpen && !s.startupSplashOpen, "browser startup");
  await delay(750);
  if ((await state()).mainMenuOverlay === "NamePrompt") {
    await page.keyboard.type("GatewayMapTest");
    await page.keyboard.press("Enter");
  }
  await waitFor(s => s?.mainMenuOverlay === "None"
    && s.menuButtons?.some(button => button.label === "Servers"), "Servers menu");
  if (!await page.evaluate(() => globalThis.OpenGarrisonBrowserHost.invokeAutomationAction("menu", "Servers"))) {
    throw new Error("Servers action failed");
  }
  await waitFor(s => s?.mainMenuOverlay === "LobbyBrowser" && !s.statusMessage,
    "custom GG2 server list", 30000);
  await key("Enter");
  const observations = [];
  let lastObservation = "";
  let joined = null;
  for (let index = 0; index < 180; index += 1) {
    const current = await state();
    const observation = JSON.stringify({ shell: current?.shell, overlay: current?.mainMenuOverlay,
      connected: current?.networkConnected, map: current?.currentMap,
      status: current?.statusMessage, loading: current?.loadingOverlayVisible });
    if (observation !== lastObservation) observations.push(observation);
    lastObservation = observation;
    if (current?.networkConnected && current.shell === "Gameplay"
      && current.currentMap === `gg2_${hash}`) {
      joined = current;
      break;
    }
    await delay(250);
  }
  if (!joined) throw new Error(`Browser custom map did not load: ${observations.join("\n")}`);
  await page.screenshot({ path: join(directory, "artifacts/gg2-browser-custom-map.png") });
  if (errors.length) throw new Error(`Browser errors:\n${errors.join("\n")}`);
  console.log(`GG2 browser custom map passed: ${joined.currentMap}`);
} catch (error) {
  console.error(error.stack ?? String(error));
  console.error(`browser state: ${JSON.stringify(await state().catch(() => null))}`);
  console.error(`console errors: ${errors.join("\n")}`);
  await page.screenshot({ path: join(directory, "artifacts/gg2-browser-custom-map-failure.png") }).catch(() => {});
  process.exitCode = 1;
} finally {
  await browser.close();
  fake.kill();
}
