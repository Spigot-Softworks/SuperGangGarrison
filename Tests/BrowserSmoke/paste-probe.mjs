import { chromium } from "playwright";
import assert from "node:assert/strict";
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { resolve, extname, sep } from "node:path";
import { setTimeout as delay } from "node:timers/promises";
const root = resolve("artifacts/browser-practice-ltd-release/browser-publish/wwwroot");
const base = "http://127.0.0.1:5016";
const types = { ".js": "text/javascript", ".wasm": "application/wasm", ".html": "text/html", ".json": "application/json" };
const server = createServer(async (req, res) => {
  const path = resolve(root, "." + (new URL(req.url, base).pathname === "/" ? "/index.html" : new URL(req.url, base).pathname));
  if (!path.startsWith(root + sep)) return res.writeHead(403).end();
  try { res.writeHead(200, { "Content-Type": types[extname(path)] ?? "application/octet-stream" }).end(await readFile(path)); }
  catch { res.writeHead(404).end(); }
});
await new Promise(done => server.listen(5016, "127.0.0.1", done));
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  await page.route("**/*", route => route.request().url().startsWith(base + "/") ? route.continue() : route.abort());
  page.on("console", message => { if (message.type() === "error") console.log(message.text()); });
  await page.goto(base);
  for (const started = Date.now();;) {
    const s = await page.evaluate(() => window.OpenGarrisonBrowserHost?.getAutomationState());
    if (s?.canEnterGameplaySession && !s.startupSplashOpen && s.mainMenuOpen && s.mainMenuOverlay === "None") break;
    if (Date.now() - started > 120000) throw new Error("Root menu timed out");
    await delay(250);
  }
  await page.locator("canvas").click({ position: { x: 550, y: 20 } });
  for (const [group, label] of [["menu", "Last to Die"], ["ltd", "Play Co-Op"], ["ltd", "Join"]]) {
    const accepted = await page.evaluate(([group, label]) => window.OpenGarrisonBrowserHost.invokeAutomationAction(group, label), [group, label]);
    if (!accepted) throw new Error(`Action rejected: ${group}/${label}`);
    await delay(350);
  }
  await page.locator("canvas").focus();
  const result = await page.evaluate(async () => {
    const host = window.OpenGarrisonBrowserHost;
    const events = [];
    window.addEventListener("paste", e => events.push(["capture", e.clipboardData?.getData("text/plain")]), { capture: true });
    window.addEventListener("paste", e => events.push(["bubble", e.clipboardData?.getData("text/plain")]));
    const data = new DataTransfer(); data.setData("text/plain", "abcd");
    const event = new ClipboardEvent("paste", { clipboardData: data, bubbles: true, cancelable: true });
    document.querySelector("canvas").dispatchEvent(event);
    const afterEvent = await host.getAutomationState();
    await host.inputBridgeHost.invokeMethodAsync("HandleBrowserRoomCodePaste", "EFGH");
    const afterDirect = await host.getAutomationState();
    return { active: document.activeElement?.id, events, prevented: event.defaultPrevented, afterEvent, afterDirect };
  });
  assert.equal(result.afterEvent.lastToDie.menuLabels[0], "Code: ABCD");
  assert.equal(result.afterDirect.lastToDie.menuLabels[0], "Code: EFGH");
  console.log(JSON.stringify(result, null, 2));
} finally {
  await browser.close();
  await new Promise(done => server.close(done));
}
