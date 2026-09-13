import assert from "node:assert/strict";
import { createReadStream } from "node:fs";
import { appendFile, mkdir, readFile, stat, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, extname, join, resolve, sep } from "node:path";
import { fileURLToPath } from "node:url";
import { setTimeout as delay } from "node:timers/promises";
import { chromium } from "playwright";

const repo = resolve(dirname(fileURLToPath(import.meta.url)), "../..");
const root = resolve(repo, process.env.OG_ROOM_BROWSER_ROOT ?? "artifacts/browser-practice-ltd/browser-publish/wwwroot");
const output = resolve(repo, process.env.OG_ROOM_OUTPUT ?? "Tests/BrowserSmoke/artifacts/private-rooms");
const localApi = process.env.OG_ROOM_API ?? "http://127.0.0.1:8009";
const publicApi = "https://api.superganggarrison.com";
const base = "http://127.0.0.1:5015";
const logs = [], requests = [], sockets = [];
let phase = "startup";
let closingBrowser = false;
let dropNextLeave = false;
let droppedLeaveRequests = 0;
let forcedRoomError = null;
const failedRoomRequests = [];
const observedLtdPhases = new Set();
const release = JSON.parse(await readFile(join(root, "release.json"), "utf8"));
assert.equal(release.edition, "PracticeAndLastToDie");
assert.equal(release.aot, !process.env.OG_ROOM_DIAGNOSTIC);
await mkdir(output, { recursive: true });
await writeFile(join(output, "live-console.log"), "");
const redact = text => String(text).replace(/([?&]token=)[^&\s"']+/g, "$1[redacted]");
const types = { ".html": "text/html", ".css": "text/css", ".js": "text/javascript", ".json": "application/json", ".wasm": "application/wasm", ".png": "image/png", ".wav": "audio/wav", ".ogg": "audio/ogg" };
const server = createServer(async (req, res) => {
  const path = resolve(root, "." + decodeURIComponent(new URL(req.url, base).pathname === "/" ? "/index.html" : new URL(req.url, base).pathname));
  if (!path.startsWith(root + sep)) { res.writeHead(403).end(); return; }
  try {
    const info = await stat(path);
    if (!info.isFile()) throw new Error("not a file");
    res.writeHead(200, { "Content-Type": types[extname(path)] ?? "application/octet-stream", "Content-Length": info.size, "Cache-Control": "no-cache" });
    createReadStream(path).pipe(res);
  } catch { res.writeHead(404).end(); }
});
await new Promise(done => server.listen(5015, "127.0.0.1", done));
const browser = await chromium.launch({ headless: true, args: ["--disable-background-timer-throttling", "--disable-renderer-backgrounding"] });
const pages = [];

async function timed(promise, label) {
  let timer;
  try { return await Promise.race([promise, new Promise((_, reject) => { timer = setTimeout(() => reject(new Error(`Timeout: ${label}`)), 30000); })]); }
  finally { clearTimeout(timer); }
}
async function state(page) { return timed(page.evaluate(() => window.OpenGarrisonBrowserHost?.getAutomationState()), "read browser state"); }
async function wait(page, predicate, label, timeout = 150000) {
  console.log(`Waiting: ${label}`);
  let last;
  for (const started = Date.now(); Date.now() - started < timeout;) {
    const host = await timed(page.evaluate(() => window.OpenGarrisonBrowserHost?.hostState), "browser host status");
    if (host?.failed) throw new Error(`Browser host failed: ${host.status}`);
    last = await state(page);
    if (last?.networkConnected && last.lastToDie?.phase) {
      observedLtdPhases.add(last.lastToDie.phase);
      assert.equal(last.joiningOverlayVisible, false, `joining popup over ${last.lastToDie.phase}`);
    }
    if (last && predicate(last)) return last;
    await writeFile(join(output, "latest-state.json"), JSON.stringify({ label, last }, null, 2));
    await delay(250);
  }
  throw new Error(`${label}: ${JSON.stringify(last)}`);
}
async function action(page, group, label) {
  if (group === "ltdlobby") {
    // Another participant may have joined while this client was recovering its
    // own socket. Wait for this client's lobby snapshot before sending a command.
    await wait(page, s => s.networkConnected && s.lastToDie?.phase === "Lobby", `connected lobby before ${label}`);
  }
  console.log(`Action: ${group}/${label}`);
  assert.equal(await page.evaluate(([group, label]) => window.OpenGarrisonBrowserHost.invokeAutomationAction(group, label), [group, label]), true, `${group}/${label}`);
  await delay(350);
}
async function set(page, key, value) {
  assert.equal(await page.evaluate(([key, value]) => window.OpenGarrisonBrowserHost.setAutomationValue(key, value), [key, value]), true, key);
}
async function key(page, code, down) {
  await page.evaluate(([code, down]) => window.OpenGarrisonBrowserHost.inputBridgeHost.invokeMethodAsync("HandleBrowserKey", code, down), [code, down]);
}
async function press(page, code) { await key(page, code, true); await delay(100); await key(page, code, false); await delay(300); }
async function command(page, value) { console.log(`Command: ${value}`); await timed(page.evaluate(value => window.OpenGarrisonBrowserHost.runAutomationConsoleCommand(value), value), `command ${value}`); }
async function rootMenu(page) { return wait(page, s => !s.startupSplashOpen && s.mainMenuOpen && s.mainMenuOverlay === "None" && s.canEnterGameplaySession, "root menu"); }
async function newPlayer(name) {
  const context = await browser.newContext({ viewport: { width: 1100, height: 800 } });
  // Keep the production-compiled origin and admission validation. Redirect only at
  // the browser test boundary; no request is sent to the production service.
  await context.route("**/*", async route => {
    const url = new URL(route.request().url());
    requests.push({ player: name, phase, host: url.host, path: url.pathname });
    if (url.origin === publicApi) {
      if (dropNextLeave && route.request().method() === "POST" && url.pathname === "/api/private-rooms/leave") {
        dropNextLeave = false;
        droppedLeaveRequests++;
        await route.abort("connectionfailed");
        return;
      }
      if (forcedRoomError && route.request().method() === "POST" && url.pathname === `/api/private-rooms/${forcedRoomError.operation}`) {
        const payload = route.request().postDataJSON();
        failedRoomRequests.push({ difficulty: payload.difficulty, code: payload.code });
        await route.fulfill({ status: forcedRoomError.status, contentType: "application/json", headers: { "Access-Control-Allow-Origin": base },
          body: JSON.stringify({ detail: { code: forcedRoomError.code } }) });
        return;
      }
      try {
        // The local HTTP/1 test proxy can outlive Uvicorn's idle keep-alive
        // timeout between menu actions. Use a fresh fixture connection rather
        // than introducing stale pooled-socket resets into an unrelated test.
        const response = await route.fetch({ url: localApi + url.pathname + url.search, timeout: 30000,
          headers: { ...route.request().headers(), connection: "close" } });
        if (!closingBrowser) await route.fulfill({ response });
      } catch (error) {
        // An in-flight status request may be disposed by intentional teardown.
        // Otherwise expose the network failure to the game as fetch would,
        // rather than crashing Node from an unobserved route-handler promise.
        if (!closingBrowser) {
          logs.push(redact(`LOCAL API transport failure: ${error.message}`));
          await route.abort("connectionfailed").catch(() => {});
        }
      }
    } else if (url.origin === base && url.pathname === "/_framework/dotnet.js" && process.env.OG_ROOM_NAV_TRACE) {
      const response = await route.fetch();
      const body = (await response.text()).replace("/*json-start*/{", '/*json-start*/{"environmentVariables":{"BOTBRAIN_NAV_ALPHA_WARM_TRACE":"1","BOTBRAIN_NAV_ALPHA_CACHE_TRACE":"1"},');
      await route.fulfill({ response, body });
    } else if (url.origin === base) await route.continue();
    else { logs.push(`BLOCKED external ${url.origin}${url.pathname}`); await route.abort(); }
  });
  await context.addInitScript(({ origin, local }) => {
    const NativeWebSocket = window.WebSocket;
    window.__roomSockets = [];
    window.WebSocket = class extends NativeWebSocket {
      constructor(url, protocols) {
        const target = String(url).replace(origin.replace("https:", "wss:"), local.replace("http:", "ws:"));
        super(target, protocols);
        window.__roomSockets.push(this);
        this.addEventListener("close", event => console.log("Managed socket closed", event.code, event.reason));
      }
      send(data) {
        const bytes = data instanceof ArrayBuffer ? new Uint8Array(data)
          : ArrayBuffer.isView(data) ? new Uint8Array(data.buffer, data.byteOffset, data.byteLength) : null;
        if (window.__dropNextStageReady && bytes?.length > 72) {
          const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
          // Protocol-64 header (40 bytes), LTD command schema 40, then
          // command ID + run GUID + structural revision before command kind.
          if (view.getUint32(0, true) === 0x3432474f && view.getUint16(6, true) === 0
              && view.getUint16(8, true) === 40 && bytes[72] === 6) {
            window.__dropNextStageReady = false;
            window.__droppedStageReady = (window.__droppedStageReady ?? 0) + 1;
            this.close();
            return;
          }
        }
        super.send(data);
      }
    };
  }, { origin: publicApi, local: localApi });
  if (process.env.OG_ROOM_STATIC_MENU) await context.addInitScript(() => {
    const key = "opengarrison:settings-v1";
    const settings = JSON.parse(localStorage.getItem(key) ?? "{}");
    settings.MenuBackgroundMode = 0;
    localStorage.setItem(key, JSON.stringify(settings));
  });
  const page = await context.newPage();
  pages.push(page);
  page.on("console", msg => { const line = redact(`${name} ${msg.type()}: ${msg.text()}`); logs.push(line); void appendFile(join(output, "live-console.log"), line + "\n"); });
  page.on("pageerror", error => logs.push(redact(`${name} PAGEERROR: ${error.stack}`)));
  page.on("websocket", socket => {
    const record = { player: name, phase, path: new URL(socket.url()).pathname, sent: 0, received: 0, shortFrames: 0 };
    sockets.push(record);
    socket.on("framesent", frame => {
      record.sent++;
      if (Buffer.isBuffer(frame.payload) && frame.payload.length < 40) {
        record.shortFrames++;
        console.log("Short outbound frame", name, frame.payload.toString("hex"));
      }
    });
    socket.on("framereceived", () => record.received++);
    socket.on("close", () => console.log("Socket closed", JSON.stringify(record)));
  });
  await page.goto(base, { waitUntil: "domcontentloaded", timeout: 180000 });
  const initial = await rootMenu(page);
  assert.deepEqual(initial.menuButtons.map(b => b.label), ["Practice", "Last to Die", "Settings"]);
  await page.locator("canvas").click({ position: { x: 550, y: 20 } });
  return page;
}
async function advanceSelections(players) {
  for (const page of players) {
    const choice = await wait(page, s => s.lastToDie?.phase === "SurvivorChoice", "survivor choice");
    assert.equal(choice.joiningOverlayVisible, false, "survivor choices must not have a joining popup");
    await set(page, "ltd_survivor", "ltd.survivor.spy");
  }
  for (const page of players) {
    const choice = await wait(page, s => s.lastToDie?.offerChoices?.length > 0, "reward choice");
    assert.equal(choice.joiningOverlayVisible, false, "reward choices must not have a joining popup");
    await set(page, "ltd_reward", choice.lastToDie.offerChoices[0]);
  }
  for (const page of players) {
    const playing = await wait(page, s => s.lastToDie?.phase === "Playing" && s.shell === "Gameplay"
      && s.lastAppliedSnapshotFrame > 0 && !s.loadingOverlayVisible, "live Last to Die stage");
    assert.equal(playing.joiningOverlayVisible, false, "playing must not retain a joining popup");
    if (process.env.OG_EXPECT_SURVIVOR) {
      assert.equal(playing.survivorBuffActive, true, "every hosted survivor receives the starting buff");
      const canvas = await page.locator("canvas").boundingBox();
      const scale = Math.min(canvas.width / playing.viewportWidth, canvas.height / playing.viewportHeight);
      const buff = playing.hudBounds['last-to-die.buff-icon'];
      assert(buff, 'the Survivor buff has resolved HUD bounds');
      await page.mouse.move(canvas.x + (canvas.width - playing.viewportWidth * scale) / 2 + (buff.x + buff.width / 2) * scale,
        canvas.y + (canvas.height - playing.viewportHeight * scale) / 2 + (buff.y + buff.height / 2) * scale);
      await delay(300);
      await page.screenshot({ path: join(output, `survivor-${players.indexOf(page)}-${players.length}p.png`) });
    }
  }
}

try {
  assert.equal((await fetch(localApi + "/healthz")).ok, true);
  const owner = await newPlayer("owner");
  await owner.screenshot({ path: join(output, "menu.png") });
  phase = "menu-restrictions";
  assert.equal(await owner.evaluate(() => window.OpenGarrisonBrowserHost.connectAutomationTarget("ws64://127.0.0.1:8190/opengarrison/ws64", "8190")), false);
  await command(owner, "connect 127.0.0.1 8190");
  assert.equal((await state(owner)).networkConnected, false);
  phase = "settings";
  await action(owner, "menu", "Settings");
  await wait(owner, s => s.mainMenuOverlay === "OptionsMenu", "settings menu");
  await owner.screenshot({ path: join(output, "settings.png") });
  await press(owner, "Escape");
  await rootMenu(owner);
  await press(owner, "F12");
  await command(owner, "set_name Browser Smoke");
  const saved = await owner.evaluate(() => localStorage.getItem("opengarrison:settings-v1"));
  assert.equal(JSON.parse(saved).AudioMuted, true);
  await owner.reload({ waitUntil: "domcontentloaded" });
  await rootMenu(owner);
  await press(owner, "F12");
  await command(owner, "set_name Browser Smoke");
  assert.equal(await owner.evaluate(() => JSON.parse(localStorage.getItem("opengarrison:settings-v1")).AudioMuted), false);
  phase = "practice";
  if (!process.env.OG_ROOM_SKIP_PRACTICE) {
  await action(owner, "menu", "Practice");
  await set(owner, "practice_enemy_bots", "1");
  if (process.env.OG_ROOM_CPU_PROFILE) {
    const cdp = await owner.context().newCDPSession(owner);
    await cdp.send("Profiler.enable");
    await cdp.send("Profiler.start");
    setTimeout(async () => {
      try {
        const profile = await cdp.send("Profiler.stop");
        await writeFile(join(output, "practice.cpuprofile"), JSON.stringify(profile.profile));
      } catch (error) { logs.push(`Profile unavailable: ${error.message}`); }
    }, 15000);
  }
  await action(owner, "practice", "Start Practice");
  await wait(owner, s => s.teamSelectOpen, "practice team selection");
  await action(owner, "teamselect", "RED");
  await wait(owner, s => s.classSelectOpen, "practice class selection");
  await action(owner, "classselect", "Pyro");
  await wait(owner, s => s.practiceSessionActive && s.localPlayerAlive, "practice spawn");
  if (process.env.OG_EXPECT_SURVIVOR) assert.equal((await state(owner)).survivorBuffActive, false, "Practice has no LTD starting buff");
  await key(owner, "KeyD", true); await delay(700); await key(owner, "KeyD", false);
  await command(owner, "+fire"); await delay(700); await command(owner, "-fire");
  await owner.screenshot({ path: join(output, "practice.png") });
  assert.equal(requests.filter(r => ["startup", "menu-restrictions", "settings", "practice"].includes(r.phase) && r.host !== new URL(base).host).length, 0, "idle and Practice must not call online services");
  assert.equal(sockets.length, 0);
  await command(owner, "disconnect"); await rootMenu(owner);
  }

  phase = "room-errors";
  forcedRoomError = { operation: "create", status: 503, code: "capacity_full" };
  await action(owner, "menu", "Last to Die");
  await action(owner, "ltd", "Play Solo");
  await action(owner, "ltd", "Hardcore");
  let failed = await wait(owner, s => s.lastToDie?.menuPage === "RoomError", "solo error page");
  assert.deepEqual(failed.lastToDie.menuLabels, ["Retry", "Back"]);
  assert.equal(failed.joiningOverlayVisible, false);
  assert.match(failed.statusMessage, /busy/);
  // Console/status activity must not erase a room error that still needs action.
  await command(owner, "set_name Browser Smoke");
  assert.equal((await state(owner)).statusMessage, failed.statusMessage);
  await owner.screenshot({ path: join(output, "solo-error.png") });
  await action(owner, "ltd", "Retry");
  await wait(owner, s => s.lastToDie?.menuPage === "RoomError", "retry preserves solo difficulty");
  assert.deepEqual(failedRoomRequests.slice(-2).map(r => r.difficulty), ["hardcore", "hardcore"]);
  await action(owner, "ltd", "Back");
  assert.equal((await state(owner)).lastToDie.menuPage, "Difficulty");
  await action(owner, "ltd", "Back");
  await action(owner, "ltd", "Play Co-Op");
  await action(owner, "ltd", "Create");
  await wait(owner, s => s.lastToDie?.menuPage === "RoomError", "co-op create error page");
  await action(owner, "ltd", "Back");
  assert.equal((await state(owner)).lastToDie.menuPage, "CoOp");
  await action(owner, "ltd", "Join");
  await set(owner, "ltd_code", "ABCD");
  forcedRoomError = { operation: "join", status: 404, code: "" };
  await action(owner, "ltd", "Join");
  await wait(owner, s => s.lastToDie?.menuPage === "RoomError", "join error page");
  await action(owner, "ltd", "Back");
  assert.equal((await state(owner)).lastToDie.menuLabels[0], "Code: ABCD");
  forcedRoomError = null;
  await command(owner, "disconnect"); await rootMenu(owner);
  assert.equal((await state(owner)).loadingOverlayVisible, false);

  phase = "solo";
  if (!process.env.OG_ROOM_SKIP_SOLO) {
  await action(owner, "menu", "Last to Die");
  await action(owner, "ltd", "Play Solo");
  await action(owner, "ltd", "Standard");
  await advanceSelections([owner]);
  assert.equal((await state(owner)).lastToDie.localSlot, 1);
  await owner.screenshot({ path: join(output, "solo.png") });
  await press(owner, "Escape");
  await delay(1600);
  const pausedAt = (await state(owner)).lastToDie.serverTick;
  await delay(1800);
  assert.equal((await state(owner)).lastToDie.serverTick, pausedAt, "solo menu pauses the authoritative clock");
  await press(owner, "Escape");
  await wait(owner, s => s.lastToDie.serverTick > pausedAt + 10, "solo resumed");
  await command(owner, "disconnect"); await rootMenu(owner); await delay(2000);
  }

  phase = "coop";
  const guest = await newPlayer("guest");
  await action(owner, "menu", "Last to Die");
  await action(owner, "ltd", "Play Co-Op");
  await action(owner, "ltd", "Create");
  const lobby = await wait(owner, s => s.lastToDie?.phase === "Lobby" && s.lastToDie.roomCode.length === 4, "co-op lobby");
  assert.equal(lobby.joiningOverlayVisible, false, "lobby must not have a joining popup");
  await owner.screenshot({ path: join(output, "coop-lobby.png") });
  await action(guest, "menu", "Last to Die");
  await action(guest, "ltd", "Play Co-Op");
  await action(guest, "ltd", "Join");
  await wait(guest, s => s.lastToDie?.menuPage === "RoomJoin", "join code input");
  await guest.locator("canvas").focus();
  await guest.locator("canvas").evaluate((canvas, code) => {
    const clipboard = new DataTransfer();
    clipboard.setData("text/plain", code.toLowerCase());
    canvas.dispatchEvent(new ClipboardEvent("paste", { clipboardData: clipboard, bubbles: true }));
  }, lobby.lastToDie.roomCode);
  await wait(guest, s => s.lastToDie?.menuLabels?.[0] === `Code: ${lobby.lastToDie.roomCode}`, "pasted room code");
  await action(guest, "ltd", "Join");
  await wait(guest, s => s.lastToDie?.playerCount === 2, "guest lobby");
  assert.equal((await state(guest)).lastToDie.localSlot, 2);
  assert.equal((await state(guest)).lastToDie.isOwner, false);
  await action(owner, "ltdlobby", "Ready");
  await action(guest, "ltdlobby", "Ready");
  await delay(700);
  await action(owner, "ltdlobby", "Start");
  await guest.evaluate(() => { window.__dropNextStageReady = true; });
  await advanceSelections([owner, guest]);
  assert.equal(await guest.evaluate(() => window.__droppedStageReady), 1, "disconnect while acknowledging stage content must recover");
  await guest.screenshot({ path: join(output, "coop-guest.png") });
  phase = "reconnect";
  const guestSocketsBeforeReconnect = sockets.filter(socket => socket.player === "guest").length;
  await guest.evaluate(() => window.__roomSockets.at(-1).close());
  await delay(2500);
  // Idle smoke players can lose the run while the guest reconnects. Prove a
  // new socket restores the same room/seat and world, including its result.
  await wait(guest, s => s.networkConnected && s.shell === "Gameplay"
    && s.lastAppliedSnapshotFrame > 0 && s.lastToDie?.localSlot === 2
    && s.lastToDie.roomCode === lobby.lastToDie.roomCode && s.lastToDie.phase.length > 0
    && sockets.filter(socket => socket.player === "guest").length > guestSocketsBeforeReconnect, "guest reconnect", 35000);
  assert.equal((await state(guest)).lastToDie.localSlot, 2);
  phase = "leave";
  dropNextLeave = true;
  await command(owner, "disconnect"); await rootMenu(owner);
  assert.equal((await state(owner)).loadingOverlayVisible, false);
  // No cooldown: start solo while the old native process is still closing.
  phase = "rapid-restart";
  await action(owner, "menu", "Last to Die");
  await action(owner, "ltd", "Play Solo");
  await action(owner, "ltd", "Standard");
  await advanceSelections([owner]);
  assert.equal(droppedLeaveRequests, 1, "a lost Leave request must be retried before the next run");
  await command(owner, "disconnect"); await rootMenu(owner);
  await wait(guest, s => s.mainMenuOpen && !s.networkConnected && s.lastToDie?.menuPage !== "RoomLoading", "guest sees owner closure", 45000);
  phase = "cancel";
  await action(owner, "menu", "Last to Die");
  await action(owner, "ltd", "Play Solo");
  await action(owner, "ltd", "Hardcore");
  await action(owner, "ltd", "Cancel");
  await wait(owner, s => s.lastToDie?.menuPage !== "RoomLoading" && !s.networkConnected, "cancelled room request");
  assert.equal((await state(owner)).lastToDie.menuPage, "Difficulty", "solo cancel returns to difficulty");
  await delay(2500);
  assert.equal((await state(owner)).networkConnected, false, "cancelled allocation must not reconnect later");
  assert.equal(logs.filter(l => l.includes("BLOCKED external") || l.includes("PAGEERROR")).length, 0);
  assert.equal(sockets.reduce((sum, socket) => sum + socket.shortFrames, 0), 0, "canonical connections must not send legacy frames");
  const partial = Boolean(process.env.OG_ROOM_SKIP_PRACTICE || process.env.OG_ROOM_SKIP_SOLO || process.env.OG_ROOM_STATIC_MENU || process.env.OG_ROOM_DIAGNOSTIC || process.env.OG_ROOM_NAV_TRACE || process.env.OG_ROOM_LEGACY_NAV_DIAGNOSTIC);
  await writeFile(join(output, partial ? "diagnostic-result.json" : "result.json"), JSON.stringify({ passed: true, partial, release, droppedLeaveRequests, observedLtdPhases: [...observedLtdPhases], requests, sockets }, null, 2));
  console.log(partial ? "Partial AOT room diagnostic passed." : "Restricted AOT browser: Practice, persistence, solo pause, co-op and reconnect passed.");
} catch (error) {
  await writeFile(join(output, "failure-error.txt"), redact(error.stack ?? error));
  for (const [i, page] of pages.entries()) {
    await page.screenshot({ path: join(output, `failure-${i}.png`), timeout: 10000 }).catch(() => {});
    await writeFile(join(output, `failure-${i}.json`), JSON.stringify(await state(page).catch(() => null), null, 2));
  }
  throw error;
} finally {
  await Promise.allSettled(pages.map(page => command(page, "disconnect")));
  await delay(2000);
  await writeFile(join(output, "console.log"), logs.join("\n"));
  await writeFile(join(output, "requests.json"), JSON.stringify({ phase, requests, sockets }, null, 2));
  closingBrowser = true;
  await browser.close();
  await new Promise(done => server.close(done));
}
