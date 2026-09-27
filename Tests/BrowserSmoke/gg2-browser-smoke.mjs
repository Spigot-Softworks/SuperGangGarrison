import { chromium } from "playwright";
import { setTimeout as delay } from "node:timers/promises";

const url = process.env.OG_GG2_BROWSER_URL ?? "http://127.0.0.1:5014";
const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
const errors = [];
page.on("pageerror", error => errors.push(error.stack ?? error.message));
page.on("console", message => {
  if (message.type() === "error") errors.push(message.text());
});

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
  await page.evaluate(async name => await globalThis.OpenGarrisonBrowserHost.inputBridgeHost
    .invokeMethodAsync("HandleBrowserKey", name, true), code);
  await delay(120);
  await page.evaluate(async name => await globalThis.OpenGarrisonBrowserHost.inputBridgeHost
    .invokeMethodAsync("HandleBrowserKey", name, false), code);
}

try {
  await page.goto(url, { waitUntil: "networkidle", timeout: 120000 });
  await waitFor(s => s?.mainMenuOpen && !s.startupSplashOpen
    && (s.mainMenuOverlay === "NamePrompt" || s.mainMenuOverlay === "None"), "browser startup");
  await delay(750);
  const startup = await state();
  if (startup.mainMenuOverlay === "NamePrompt") {
    await page.keyboard.type("GatewaySmoke");
    await page.keyboard.press("Enter");
  }
  const menu = await waitFor(s => s?.mainMenuOpen && !s.startupSplashOpen
    && s.mainMenuOverlay === "None" && s.menuButtons?.some(b => b.label === "Servers"),
    "limited GG2 menu");
  console.log(`menu: ${menu.menuButtons.map(b => b.label).join(", ")}`);
  if (!await page.evaluate(() => globalThis.OpenGarrisonBrowserHost.invokeAutomationAction("menu", "Servers"))) {
    throw new Error("Servers action failed");
  }
  await waitFor(s => s?.mainMenuOverlay === "LobbyBrowser", "GG2 server browser");
  await waitFor(s => s?.mainMenuOverlay === "LobbyBrowser" && !s.statusMessage, "GG2 server list", 30000);
  await page.screenshot({ path: "Tests/BrowserSmoke/artifacts/gg2-browser-list.png" });
  await key("Enter");
  let joined = await waitFor(s => s?.networkConnected && s.currentMap?.startsWith("gg2_")
    && s.shell === "Gameplay", "GG2 join", 45000);
  console.log(`joined: ${JSON.stringify({ server: joined.networkServerDescription, map: joined.currentMap,
    shell: joined.shell, team: joined.teamSelectOpen, class: joined.classSelectOpen,
    status: joined.statusMessage })}`);
  if (!joined.teamSelectOpen && !joined.classSelectOpen) {
    await key("KeyN");
    joined = await state();
    console.log(`team menu after N: ${JSON.stringify({ team: joined.teamSelectOpen,
      class: joined.classSelectOpen, awaiting: joined.awaitingJoin, status: joined.statusMessage })}`);
  }
  if (joined.teamSelectOpen) {
    if (!await page.evaluate(() => globalThis.OpenGarrisonBrowserHost.invokeAutomationAction("teamselect", "Auto Select"))) {
      throw new Error("Auto Select failed");
    }
  }
  const classes = await waitFor(s => s?.classSelectOpen, "GG2 class select", 20000);
  const scout = classes.classSelectButtons.find(b => b.label === "Scout");
  if (!scout) throw new Error(`Scout class missing: ${JSON.stringify(classes.classSelectButtons)}`);
  if (!await page.evaluate(() => globalThis.OpenGarrisonBrowserHost.invokeAutomationAction("classselect", "Scout"))) {
    throw new Error("Scout selection failed");
  }
  const spawned = await waitFor(s => s?.networkConnected && s.localPlayerAlive && !s.awaitingJoin,
    "GG2 spawn", 30000);
  const startX = spawned.localPlayerX;
  await page.evaluate(async () => await globalThis.OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync("HandleBrowserKey", "KeyD", true));
  await delay(750);
  await page.evaluate(async () => await globalThis.OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync("HandleBrowserKey", "KeyD", false));
  const moved = await waitFor(s => s?.localPlayerX > startX + 1, "GG2 movement", 10000);
  const ammoBeforeFire = moved.primaryAmmo;
  await page.mouse.move(1000, 400);
  await page.mouse.down();
  const fireSamples = [];
  for (let index = 0; index < 15; index += 1) {
    const sample = await state();
    fireSamples.push({ ammo: sample?.primaryAmmo, animation: sample?.weaponAnimation });
    await delay(50);
  }
  await page.mouse.up();
  const fired = await state();
  if (!fireSamples.some(sample => sample.ammo < ammoBeforeFire || sample.animation !== "Idle")) {
    console.log(`firing samples: ${JSON.stringify(fireSamples)}`);
    throw new Error("GG2 firing showed no ammo or animation change");
  }
  const yBeforeJump = fired.localPlayerY;
  await key("KeyW");
  const jumpSamples = [];
  for (let index = 0; index < 12; index += 1) {
    jumpSamples.push((await state())?.localPlayerY);
    await delay(75);
  }
  if (Math.min(...jumpSamples) >= yBeforeJump - 1) {
    throw new Error(`GG2 jump input did not lift the player: before=${yBeforeJump}, samples=${jumpSamples}`);
  }
  await page.screenshot({ path: "Tests/BrowserSmoke/artifacts/gg2-browser-spawn.png" });
  console.log(`spawned: ${JSON.stringify({ map: fired.currentMap, x: fired.localPlayerX,
    frame: fired.lastAppliedSnapshotFrame, audio: fired.audioAvailable,
    ammoBeforeFire, ammoAfterFire: Math.min(...fireSamples.map(sample => sample.ammo)), yBeforeJump,
    minJumpY: Math.min(...jumpSamples) })}`);
  await page.evaluate(() => globalThis.OpenGarrisonBrowserHost?.resetMetrics?.());
  await delay(5000);
  const metrics = await page.evaluate(() => globalThis.OpenGarrisonBrowserHost?.getMetrics?.() ?? null);
  console.log(`metrics: ${JSON.stringify(metrics)}`);
  if (!metrics || metrics.recentFps < 30) {
    throw new Error(`GG2 steady gameplay frame rate is below 30 FPS: ${JSON.stringify(metrics)}`);
  }
  const settled = await state();
  console.log(`network after settling: ${JSON.stringify({ ping: settled.estimatedPingMilliseconds,
    frame: settled.lastAppliedSnapshotFrame, connected: settled.networkConnected,
    map: settled.currentMap })}`);
  const relevantErrors = errors.filter(error => !error.includes("devmessages.txt")
    && !error.includes("ERR_FAILED"));
  if (relevantErrors.length) throw new Error(`Browser errors:\n${relevantErrors.join("\n")}`);
  console.log("GG2 browser smoke passed");
} catch (error) {
  console.error(error.stack ?? String(error));
  console.error(`browser state: ${JSON.stringify(await state().catch(() => null))}`);
  console.error(`console errors: ${errors.join("\n")}`);
  await page.screenshot({ path: "Tests/BrowserSmoke/artifacts/gg2-browser-failure.png" }).catch(() => {});
  process.exitCode = 1;
} finally {
  await browser.close();
}
