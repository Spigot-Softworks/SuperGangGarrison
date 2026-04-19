import { createReadStream } from "node:fs";
import { mkdir, readFile, stat, writeFile } from "node:fs/promises";
import { createServer } from "node:http";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { setTimeout as delay } from "node:timers/promises";
import { spawn } from "node:child_process";
import { chromium } from "playwright";

const __dirname = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(__dirname, "..", "..");
const browserPublishScript = join(repoRoot, "Tools", "Browser", "publish-browser.ps1");
const serverMode = (process.env.OG_BROWSER_SMOKE_SERVER_MODE ?? "aot").trim().toLowerCase();
if (serverMode !== "aot") {
  throw new Error(`Browser smoke is AOT-only. Remove OG_BROWSER_SMOKE_SERVER_MODE=${serverMode} and use the AOT publish path.`);
}
const defaultPort = 5014;
const explicitBaseUrl = process.env.OG_BROWSER_SMOKE_URL ?? "";
let baseUrl = explicitBaseUrl || `http://127.0.0.1:${defaultPort}`;
const skipManagedServer = process.env.OG_BROWSER_SMOKE_SKIP_SERVER === "1";
const skipPublish = process.env.OG_BROWSER_SMOKE_SKIP_PUBLISH === "1";
const dotnetConfiguration = process.env.OG_BROWSER_DOTNET_CONFIGURATION ?? "Release";
const artifactsDir = join(__dirname, "artifacts");
const publishOutputDir = process.env.OG_BROWSER_SMOKE_PUBLISH_OUTPUT
  ? resolve(repoRoot, process.env.OG_BROWSER_SMOKE_PUBLISH_OUTPUT)
  : join(
    repoRoot,
    "artifacts",
    "browser-publish-aot");

const startupTimeoutMs = Number.parseInt(process.env.OG_BROWSER_STARTUP_TIMEOUT_MS ?? "120000", 10);
const hostSettleTimeoutMs = Number.parseInt(process.env.OG_BROWSER_HOST_SETTLE_TIMEOUT_MS ?? "45000", 10);
const practiceLaunchSettleTimeoutMs = Number.parseInt(process.env.OG_BROWSER_PRACTICE_LAUNCH_SETTLE_TIMEOUT_MS ?? "15000", 10);
const gameplayShellTimeoutMs = Number.parseInt(process.env.OG_BROWSER_GAMEPLAY_SHELL_TIMEOUT_MS ?? "20000", 10);
const practiceSpawnSettleTimeoutMs = Number.parseInt(process.env.OG_BROWSER_PRACTICE_SPAWN_SETTLE_TIMEOUT_MS ?? "15000", 10);
const gameplayFpsSampleMs = Number.parseInt(process.env.OG_BROWSER_FPS_SAMPLE_MS ?? "5000", 10);
const minGameplayFps = Number.parseFloat(process.env.OG_BROWSER_MIN_FPS ?? "30");
const maxPumpDurationMs = Number.parseFloat(process.env.OG_BROWSER_MAX_PUMP_MS ?? "250");
const maxPracticeShellReadyMs = Number.parseInt(process.env.OG_BROWSER_MAX_SHELL_READY_MS ?? "45000", 10);
const practiceEnemyBots = clampPracticeBotCount(process.env.OG_BROWSER_PRACTICE_ENEMY_BOTS ?? "0");
const practiceFriendlyBots = clampPracticeBotCount(process.env.OG_BROWSER_PRACTICE_FRIENDLY_BOTS ?? "0");
const postJoinCommands = parseCommandList(process.env.OG_BROWSER_POST_JOIN_COMMANDS ?? "");
const postJoinMoveKey = process.env.OG_BROWSER_POST_JOIN_MOVE_KEY ?? "";
const postJoinMoveDurationMs = Number.parseInt(process.env.OG_BROWSER_POST_JOIN_MOVE_MS ?? "0", 10);
const maxLooseSheetVisuals = Number.parseInt(process.env.OG_BROWSER_MAX_LOOSE_SHEETS ?? "12", 10);

const serverLogs = [];
const consoleEvents = [];
const timings = {};
let server;
let browser;
let staticServer;

process.on("SIGINT", async () => {
  await cleanup();
  process.exit(130);
});

process.on("SIGTERM", async () => {
  await cleanup();
  process.exit(143);
});

try {
  const smokeStartedAt = Date.now();
  await mkdir(artifactsDir, { recursive: true });

  if (!skipManagedServer) {
    server = await startServer();
  }
  await waitForServer(baseUrl, startupTimeoutMs);
  timings.serverReadyMs = Date.now() - smokeStartedAt;

  browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();

  page.on("console", (message) => {
    consoleEvents.push(`[${message.type()}] ${message.text()}`);
  });
  page.on("pageerror", (error) => {
    consoleEvents.push(`pageerror: ${error.stack ?? error.message}`);
  });

  const navigationStartedAt = Date.now();
  await page.goto(baseUrl, { waitUntil: "networkidle", timeout: startupTimeoutMs });
  timings.pageGotoMs = Date.now() - navigationStartedAt;

  const hostStartedAt = Date.now();
  const summary = await waitForHostState(page, hostSettleTimeoutMs);
  timings.hostReadyMs = Date.now() - hostStartedAt;

  if (summary.hostFailed) {
    throw new Error(`Browser host reported failure. Status: ${summary.hostStatus}`);
  }

  if (!summary.kniHostStarted) {
    throw new Error(`Browser host never started. Status: ${summary.hostStatus}`);
  }

  await waitForAutomationState(
    page,
    (state) => state && !state.startupSplashOpen && state.mainMenuOpen && state.mainMenuOverlay === "None" && state.menuButtons.length > 0,
    hostSettleTimeoutMs,
    "main menu"
  );
  const practiceLaunchStartedAt = Date.now();
  await launchPracticeSession(page);

  const postPracticeSummary = await waitForStableHost(page, practiceLaunchSettleTimeoutMs);
  if (postPracticeSummary.hostFailed) {
    throw new Error(`Browser host failed after launching Practice. Status: ${postPracticeSummary.hostStatus}`);
  }

  await delay(1_000);
  const immediateGameplayShellState = await readAutomationState(page);
  if (!isGameplayShellReady(immediateGameplayShellState)) {
    await waitForGameplayShell(page, gameplayShellTimeoutMs);
  }
  timings.practiceShellReadyMs = Date.now() - practiceLaunchStartedAt;
  if (timings.practiceShellReadyMs > maxPracticeShellReadyMs) {
    throw new Error(
      `Browser practice shell readiness exceeded the load threshold: ${timings.practiceShellReadyMs}ms, ` +
      `expected at most ${maxPracticeShellReadyMs}ms. Timings: ${JSON.stringify(timings)}`
    );
  }

  const joinPracticeStartedAt = Date.now();
  await page.evaluate(() => globalThis.OpenGarrisonBrowserHost?.resetMetrics?.());
  await joinPracticeAndExerciseRapidFire(page);
  timings.practiceJoinAndExerciseMs = Date.now() - joinPracticeStartedAt;

  await runPostJoinCommands(page);
  await runPostJoinMovement(page);
  await verifyChatInput(page);
  await verifyResizeRecovery(page);

  const finalSummary = await waitForStableHost(page, practiceSpawnSettleTimeoutMs);
  if (finalSummary.hostFailed) {
    throw new Error(`Browser host failed after joining Practice. Status: ${finalSummary.hostStatus}`);
  }

  const finalAutomationState = await readAutomationState(page);
  if (finalAutomationState && finalAutomationState.audioAvailable === false) {
    throw new Error(`Browser audio disabled during smoke. Automation state: ${JSON.stringify(finalAutomationState)}`);
  }

  if (finalAutomationState && finalAutomationState.looseSheetVisualCount > maxLooseSheetVisuals) {
    throw new Error(
      `Browser loose-sheet visual count exceeded the cap: ${finalAutomationState.looseSheetVisualCount}, ` +
      `expected at most ${maxLooseSheetVisuals}. Automation state: ${JSON.stringify(finalAutomationState)}`);
  }

  const gameplayMetrics = await sampleBrowserMetrics(page, gameplayFpsSampleMs);
  if (!gameplayMetrics) {
    throw new Error("Browser host did not expose frame-pump metrics.");
  }

  if (gameplayMetrics.recentFps < minGameplayFps) {
    throw new Error(
      `Browser gameplay FPS below smoke threshold: ${gameplayMetrics.recentFps.toFixed(1)}fps over ` +
      `${gameplayFpsSampleMs}ms, expected at least ${minGameplayFps}fps. Metrics: ${JSON.stringify(gameplayMetrics)}`);
  }

  if (gameplayMetrics.maxPumpDurationMs > maxPumpDurationMs) {
    throw new Error(
      `Browser gameplay frame pump exceeded the long-frame threshold: ${gameplayMetrics.maxPumpDurationMs.toFixed(1)}ms, ` +
      `expected at most ${maxPumpDurationMs}ms. Metrics: ${JSON.stringify(gameplayMetrics)}`);
  }

  const practiceScreenshotPath = join(artifactsDir, "browser-smoke-practice.png");
  await page.screenshot({ path: practiceScreenshotPath, timeout: 60_000 });

  const consoleErrors = consoleEvents.filter((entry) =>
    (entry.startsWith("[error]") || entry.startsWith("pageerror:"))
    && !entry.includes("devmessages.txt")
    && !entry.includes("Access to fetch at 'https://www.ganggarrison.com/devmessages.txt'")
    && !entry.includes("ERR_FAILED"));
  if (consoleErrors.length > 0) {
    throw new Error(`Browser console reported errors:\n${consoleErrors.join("\n")}`);
  }

  console.log("Browser smoke test passed.");
  console.log(`KNI host started: ${finalSummary.kniHostStarted}`);
  console.log(`Host status: ${finalSummary.hostStatus}`);
  console.log(`Timings: ${JSON.stringify(timings)}`);
  console.log(`Gameplay metrics: ${JSON.stringify(gameplayMetrics)}`);
  const readPixelsWarnings = consoleEvents.filter((entry) => entry.includes("ReadPixels")).length;
  if (readPixelsWarnings > 0) {
    console.log(`ReadPixels warnings observed: ${readPixelsWarnings}`);
  }
  if (consoleEvents.length > 0) {
    console.log(consoleEvents.join("\n"));
  }
} catch (error) {
  const browserPage = browser?.contexts()?.[0]?.pages()?.[0];
  if (browserPage) {
    const screenshotPath = join(artifactsDir, "browser-smoke-failure.png");
    await browserPage.screenshot({ path: screenshotPath, fullPage: true }).catch(() => {});
  }

  const logPath = join(artifactsDir, "browser-smoke-server.log");
  await writeFile(logPath, serverLogs.join(""), "utf8");

  if (browser?.contexts()?.[0]?.pages()?.[0]) {
    const latestSummary = await readSummary(browser.contexts()[0].pages()[0]).catch(() => null);
    if (latestSummary) {
      console.error(`Latest summary: ${JSON.stringify(latestSummary)}`);
    }
  }

  if (consoleEvents.length > 0) {
    console.error(`Console events:\n${consoleEvents.join("\n")}`);
  }

  console.error(error instanceof Error ? error.stack ?? error.message : String(error));
  console.error(`Server log: ${logPath}`);
  process.exitCode = 1;
} finally {
  await cleanup();
}

async function startServer() {
  return startPublishedStaticServer();
}

async function startPublishedStaticServer() {
  if (!skipPublish) {
    await publishBrowserArtifacts();
  }

  const contentRoot = join(
    publishOutputDir,
    "wwwroot");
  const basePath = await readPublishedBasePath(contentRoot);
  if (!explicitBaseUrl && basePath !== "/") {
    baseUrl = `http://127.0.0.1:${defaultPort}${basePath}`;
  }

  const requestedUrl = new URL(baseUrl);
  const servedBasePath = normalizeBasePath(requestedUrl.pathname);
  staticServer = createStaticFileServer(contentRoot, servedBasePath);
  await listenStaticServer(staticServer, requestedUrl.hostname, Number(requestedUrl.port));

  const address = staticServer.address();
  if (address && typeof address === "object") {
    baseUrl = `${requestedUrl.protocol}//${address.address}:${address.port}${servedBasePath}`;
  }

  serverLogs.push(`[static server] serving ${contentRoot} at ${baseUrl}\n`);
  return staticServer;
}

async function listenStaticServer(serverInstance, hostname, preferredPort) {
  try {
    await listenStaticServerOnce(serverInstance, hostname, preferredPort);
  } catch (error) {
    const code = error?.code;
    const canFallbackToRandomPort = !explicitBaseUrl
      && hostname === "127.0.0.1"
      && (code === "EADDRINUSE" || code === "EACCES");
    if (!canFallbackToRandomPort) {
      throw error;
    }

    await listenStaticServerOnce(serverInstance, hostname, 0);
  }
}

async function listenStaticServerOnce(serverInstance, hostname, port) {
  await new Promise((resolvePromise, rejectPromise) => {
    const handleError = (error) => {
      serverInstance.off("listening", handleListening);
      rejectPromise(error);
    };
    const handleListening = () => {
      serverInstance.off("error", handleError);
      resolvePromise();
    };

    serverInstance.once("error", handleError);
    serverInstance.once("listening", handleListening);
    serverInstance.listen(port, hostname);
  });
}

async function publishBrowserArtifacts() {
  const args = [
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    browserPublishScript,
    "-Configuration",
    dotnetConfiguration
  ];
  const child = spawn(
    "powershell",
    args,
    {
      cwd: repoRoot,
      stdio: ["ignore", "pipe", "pipe"],
      shell: false
    });

  child.stdout.on("data", (chunk) => {
    serverLogs.push(chunk.toString());
  });
  child.stderr.on("data", (chunk) => {
    serverLogs.push(chunk.toString());
  });

  const exitCode = await new Promise((resolvePromise, rejectPromise) => {
    child.on("error", rejectPromise);
    child.on("exit", resolvePromise);
  });

  if (exitCode !== 0) {
    throw new Error(`Browser publish failed with exit code ${exitCode}.`);
  }
}

async function readPublishedBasePath(rootDirectory) {
  const indexHtml = await readFile(join(rootDirectory, "index.html"), "utf8");
  const match = indexHtml.match(/<base\s+href=["']([^"']+)["']/i);
  return normalizeBasePath(match?.[1] ?? "/");
}

function normalizeBasePath(pathname) {
  if (!pathname || pathname === "/") {
    return "/";
  }

  const normalized = pathname.startsWith("/") ? pathname : `/${pathname}`;
  return normalized.endsWith("/") ? normalized : `${normalized}/`;
}

function createStaticFileServer(rootDirectory, basePath) {
  return createServer(async (request, response) => {
    try {
      const requestUrl = new URL(request.url ?? "/", "http://127.0.0.1");
      let relativePath = decodeURIComponent(requestUrl.pathname);
      if (basePath !== "/" && relativePath.startsWith(basePath)) {
        relativePath = `/${relativePath.slice(basePath.length)}`;
      }

      if (relativePath === "/" || relativePath.length === 0) {
        relativePath = "/index.html";
      }

      const normalizedSegments = relativePath
        .split("/")
        .filter((segment) => segment.length > 0 && segment !== "." && segment !== "..");
      const filePath = join(rootDirectory, ...normalizedSegments);
      const fileInfo = await stat(filePath);
      if (!fileInfo.isFile()) {
        response.writeHead(404);
        response.end();
        return;
      }

      response.writeHead(200, {
        "Content-Type": getContentType(filePath),
        "Cache-Control": "no-store"
      });
      createReadStream(filePath).pipe(response);
    } catch (error) {
      if (String(error?.code) === "ENOENT") {
        response.writeHead(404);
        response.end();
        return;
      }

      response.writeHead(500, { "Content-Type": "text/plain; charset=utf-8" });
      response.end(error instanceof Error ? error.message : String(error));
    }
  });
}

function getContentType(path) {
  const lowerPath = path.toLowerCase();
  if (lowerPath.endsWith(".html")) return "text/html; charset=utf-8";
  if (lowerPath.endsWith(".js") || lowerPath.endsWith(".mjs")) return "text/javascript; charset=utf-8";
  if (lowerPath.endsWith(".css")) return "text/css; charset=utf-8";
  if (lowerPath.endsWith(".json")) return "application/json; charset=utf-8";
  if (lowerPath.endsWith(".wasm")) return "application/wasm";
  if (lowerPath.endsWith(".dll")) return "application/octet-stream";
  if (lowerPath.endsWith(".dat")) return "application/octet-stream";
  if (lowerPath.endsWith(".png")) return "image/png";
  if (lowerPath.endsWith(".jpg") || lowerPath.endsWith(".jpeg")) return "image/jpeg";
  if (lowerPath.endsWith(".wav")) return "audio/wav";
  if (lowerPath.endsWith(".ogg")) return "audio/ogg";
  if (lowerPath.endsWith(".txt")) return "text/plain; charset=utf-8";
  return "application/octet-stream";
}

async function waitForServer(url, timeoutMs) {
  const startedAt = Date.now();
  while (Date.now() - startedAt < timeoutMs) {
    if (server && "exitCode" in server && server.exitCode !== null) {
      throw new Error(`Browser host process exited early with code ${server.exitCode}.`);
    }

    try {
      const response = await fetch(url, { method: "GET" });
      if (response.ok) {
        return;
      }
    } catch {
    }

    await delay(1_000);
  }

  throw new Error(`Timed out waiting for browser host at ${url}.`);
}

async function waitForHostState(page, timeoutMs) {
  const startedAt = Date.now();
  let latestSummary = await readSummary(page);

  while (Date.now() - startedAt < timeoutMs) {
    const summary = await readSummary(page);
    latestSummary = summary;
    const status = summary.hostStatus ?? "";

    if (summary.hostFailed) {
      return summary;
    }

    if (summary.kniHostStarted) {
      return summary;
    }

    await delay(1_000);
  }

  throw new Error(`Timed out waiting for browser host status to settle. Latest status: ${latestSummary.hostStatus}`);
}

async function waitForStableHost(page, timeoutMs) {
  const startedAt = Date.now();
  let latestSummary = await readSummary(page);
  if (latestSummary.hostFailed || latestSummary.kniHostStarted) {
    if (latestSummary.kniHostStarted) {
      await delay(1_000);
      return await readSummary(page);
    }

    return latestSummary;
  }

  while (Date.now() - startedAt < timeoutMs) {
    latestSummary = await readSummary(page);
    if (latestSummary.hostFailed) {
      return latestSummary;
    }

    if (latestSummary.kniHostStarted) {
      await delay(1_000);
      return await readSummary(page);
    }

    await delay(500);
  }

  throw new Error(`Timed out waiting for browser host after practice launch. Latest status: ${latestSummary.hostStatus}`);
}

async function launchPracticeSession(page) {
  const launchStartedAt = Date.now();
  const rootState = await waitForAutomationState(
    page,
    (state) => state && state.mainMenuOpen && state.mainMenuOverlay === "None" && state.mainMenuPage === "Root",
    hostSettleTimeoutMs,
    "root main menu"
  );
  timings.launchRootMenuReadyMs = Date.now() - launchStartedAt;
  await clickAutomationAction(page, rootState.menuButtons, "Play Offline");
  timings.launchClickedOfflineMs = Date.now() - launchStartedAt;

  const offlineState = await waitForAutomationState(
    page,
    (state) => state && state.mainMenuOpen && state.mainMenuOverlay === "None" && state.mainMenuPage === "PlayOffline",
    15_000,
    "offline menu"
  );
  timings.launchOfflineMenuReadyMs = Date.now() - launchStartedAt;
  await clickAutomationAction(page, offlineState.menuButtons, "Practice");
  timings.launchClickedPracticeMs = Date.now() - launchStartedAt;

  const practiceState = await waitForAutomationState(
    page,
    (state) => state && state.practiceSetupOpen && state.practiceButtons.length > 0,
    15_000,
    "practice setup"
  );
  timings.launchPracticeSetupReadyMs = Date.now() - launchStartedAt;
  if (!practiceState.canEnterGameplaySession) {
    await waitForAutomationState(
      page,
      (state) => state && state.practiceSetupOpen && state.canEnterGameplaySession,
      hostSettleTimeoutMs,
      "practice bootstrap readiness"
    );
    timings.launchPracticeBootstrapReadyMs = Date.now() - launchStartedAt;
  }

  const readyPracticeState = await readAutomationState(page);
  await setPracticeBotCount(page, "Enemy", readyPracticeState?.practiceEnemyBotCount ?? 0, practiceEnemyBots);
  timings.launchEnemyBotsReadyMs = Date.now() - launchStartedAt;
  await setPracticeBotCount(page, "Friendly", (await readAutomationState(page))?.practiceFriendlyBotCount ?? 0, practiceFriendlyBots);
  timings.launchFriendlyBotsReadyMs = Date.now() - launchStartedAt;
  await clickAutomationAction(page, readyPracticeState?.practiceButtons ?? [], "Start Practice");
  timings.launchClickedStartPracticeMs = Date.now() - launchStartedAt;
}

async function setPracticeBotCount(page, kind, currentCount, targetCount) {
  if (currentCount === targetCount) {
    return;
  }

  const decrementLabel = `${kind} Bots -`;
  const incrementLabel = `${kind} Bots +`;
  const countField = kind === "Enemy" ? "practiceEnemyBotCount" : "practiceFriendlyBotCount";
  let latestState = await readAutomationState(page);
  let latestCount = currentCount;
  for (let attempt = 0; latestCount !== targetCount && attempt < 20; attempt += 1) {
    const label = latestCount < targetCount ? incrementLabel : decrementLabel;
    await clickAutomationAction(page, latestState?.practiceButtons ?? [], label);
    latestState = await waitForAutomationState(
      page,
      (state) => state && state.practiceSetupOpen && state[countField] !== latestCount,
      5_000,
      `${kind.toLowerCase()} bot count change`);
    latestCount = latestState[countField];
  }

  if (latestCount !== targetCount) {
    throw new Error(`${kind} bot count settled at ${latestCount}, expected ${targetCount}. State: ${JSON.stringify(latestState)}`);
  }
}

async function waitForGameplayShell(page, timeoutMs) {
  await waitForAutomationState(
    page,
    isGameplayShellReady,
    timeoutMs,
    "gameplay shell"
  );
}

async function joinPracticeAndExerciseRapidFire(page) {
  const teamSelectState = await waitForAutomationState(
    page,
    (state) => state && state.teamSelectOpen,
    15_000,
    "team select"
  );
  await clickAutomationAction(page, teamSelectState.teamSelectButtons, "Auto Select");

  const classSelectState = await waitForAutomationState(
    page,
    (state) => state && state.classSelectOpen,
    15_000,
    "class select"
  );
  await clickAutomationAction(page, classSelectState.classSelectButtons, "Pyro");

  const spawnedState = await waitForAutomationState(
    page,
    (state) => state && !state.classSelectOpen && !state.awaitingJoin,
    15_000,
    "spawn completion"
  );

  const startingPlayerX = spawnedState.localPlayerX;
  await setBrowserKey(page, "KeyD", true);
  await waitForAutomationState(
    page,
    (state) => state && state.browserInputFocused && state.localPlayerX > startingPlayerX + 1,
    5_000,
    "keyboard movement"
  );
  await setBrowserKey(page, "KeyD", false);

  await delay(2_000);
  await page.mouse.move(500, 300);
  await page.mouse.down();
  await delay(2_000);
  await page.mouse.up();
  await delay(3_000);
}

async function setBrowserKey(page, code, pressed) {
  const invoked = await page.evaluate(
    async ({ keyCode, isPressed }) => {
      const host = globalThis.OpenGarrisonBrowserHost?.inputBridgeHost;
      if (!host || typeof host.invokeMethodAsync !== "function") {
        return false;
      }

      await host.invokeMethodAsync("HandleBrowserKey", keyCode, isPressed);
      return true;
    },
    { keyCode: code, isPressed: pressed });
  if (!invoked) {
    throw new Error(`Browser input bridge unavailable for ${code}.`);
  }
}

async function readAutomationState(page) {
  return await page.evaluate(async () => await globalThis.OpenGarrisonBrowserHost?.getAutomationState?.() ?? null);
}

async function runAutomationConsoleCommand(page, commandText) {
  const executed = await page.evaluate(async (command) =>
    await globalThis.OpenGarrisonBrowserHost?.runAutomationConsoleCommand?.(command) ?? false,
    commandText);
  if (!executed) {
    throw new Error(`Browser automation command failed: ${commandText}`);
  }
}

async function waitForAutomationState(page, predicate, timeoutMs, description) {
  const startedAt = Date.now();
  let latestState = await readAutomationState(page);
  if (predicate(latestState)) {
    return latestState;
  }

  while (Date.now() - startedAt < timeoutMs) {
    latestState = await readAutomationState(page);
    if (predicate(latestState)) {
      return latestState;
    }

    await delay(250);
  }

  throw new Error(`Timed out waiting for ${description}. Latest automation state: ${JSON.stringify(latestState)}`);
}

async function clickAutomationAction(page, actions, label) {
  const actionSet = inferAutomationActionSet(actions);
  if (actionSet) {
    const invoked = await page.evaluate(
      async ({ setName, actionLabel }) =>
        await globalThis.OpenGarrisonBrowserHost?.invokeAutomationAction?.(setName, actionLabel) ?? false,
      { setName: actionSet, actionLabel: label });
    if (invoked) {
      return;
    }
  }

  const action = actions.find((entry) => entry.label === label);
  if (!action) {
    throw new Error(`Automation action '${label}' was not available. Actions: ${JSON.stringify(actions)}`);
  }

  if (!action.enabled) {
    throw new Error(`Automation action '${label}' was disabled. Action: ${JSON.stringify(action)}`);
  }

  const x = action.bounds.x + Math.floor(action.bounds.width / 2);
  const y = action.bounds.y + Math.floor(action.bounds.height / 2);
  await page.mouse.click(x, y, { delay: 60 });
}

function inferAutomationActionSet(actions) {
  if (!Array.isArray(actions) || actions.length === 0) {
    return null;
  }

  const labels = new Set(actions.map((entry) => entry.label));
  if (labels.has("Play Offline") || labels.has("Play Online") || labels.has("Settings") || labels.has("Practice") || labels.has("Minigames")) {
    return "menu";
  }

  if (labels.has("Start Practice") || labels.has("Enemy Bots +") || labels.has("Friendly Bots +")) {
    return "practice";
  }

  if (labels.has("Auto Select") || labels.has("RED") || labels.has("BLU")) {
    return "teamselect";
  }

  if (labels.has("Scout") || labels.has("Pyro") || labels.has("Random")) {
    return "classselect";
  }

  return null;
}

function isGameplayShellReady(state) {
  return !!state
    && state.shell === "Gameplay"
    && state.practiceSessionActive
    && state.teamSelectOpen;
}

async function sampleBrowserMetrics(page, durationMs) {
  await delay(durationMs);
  return await page.evaluate(() => globalThis.OpenGarrisonBrowserHost?.getMetrics?.() ?? null);
}

async function runPostJoinCommands(page) {
  for (const commandText of postJoinCommands) {
    await runAutomationConsoleCommand(page, commandText);
    await delay(250);
  }
}

async function runPostJoinMovement(page) {
  if (!postJoinMoveKey || postJoinMoveDurationMs <= 0) {
    return;
  }

  await page.keyboard.down(postJoinMoveKey);
  await delay(postJoinMoveDurationMs);
  await page.keyboard.up(postJoinMoveKey);
}

async function verifyChatInput(page) {
  await page.click("#theCanvas");
  await page.keyboard.press("y");
  await waitForAutomationState(
    page,
    (state) => state && state.chatOpen,
    5_000,
    "chat prompt open");

  await page.keyboard.type("browser");
  const chatState = await waitForAutomationState(
    page,
    (state) => state && state.chatOpen && state.chatInput === "browser",
    5_000,
    "browser chat text");

  if (chatState.chatInput !== "browser") {
    throw new Error(`Browser chat text duplicated or diverged. State: ${JSON.stringify(chatState)}`);
  }

  await page.keyboard.press("Escape");
  await waitForAutomationState(
    page,
    (state) => state && !state.chatOpen,
    5_000,
    "chat prompt close");
}

async function verifyResizeRecovery(page) {
  await page.setViewportSize({ width: 1400, height: 900 });
  await delay(750);
  await page.setViewportSize({ width: 960, height: 720 });

  const resizeState = await waitForAutomationState(
    page,
    (state) => state && state.shell === "Gameplay" && state.viewportWidth > 0 && state.viewportHeight > 0,
    10_000,
    "post-resize gameplay shell");
  if (!resizeState.practiceSessionActive) {
    throw new Error(`Browser gameplay session was lost after resize. State: ${JSON.stringify(resizeState)}`);
  }

  const resizeSummary = await waitForStableHost(page, 10_000);
  if (resizeSummary.hostFailed) {
    throw new Error(`Browser host failed after resize. Status: ${resizeSummary.hostStatus}`);
  }

  const resizeMetrics = await sampleBrowserMetrics(page, 2_000);
  if (!resizeMetrics || resizeMetrics.recentCompletedPumps <= 0) {
    throw new Error(`Browser frame pump did not recover after resize. Metrics: ${JSON.stringify(resizeMetrics)}`);
  }
}

async function readSummary(page) {
  return await page.evaluate(() => {
    const managedState = globalThis.OpenGarrisonBrowserHost?.getManagedState?.() ?? null;
    const text = document.body.innerText;
    const read = (label) => {
      const pattern = new RegExp(`${label}:\\s*(.+)`);
      const match = text.match(pattern);
      return match ? match[1].trim() : "";
    };

      return {
      kniHostStarted: managedState ? !!managedState.started : read("KNI host started").toLowerCase() === "true",
      hostStatus: managedState?.status ?? read("Host status"),
      hostFailed: managedState ? !!managedState.failed : read("Host failed").toLowerCase() === "true"
    };
  });
}

function clampPracticeBotCount(value) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed)) {
    return 0;
  }

  return Math.max(0, Math.min(9, parsed));
}

function parseCommandList(value) {
  return value
    .split(";")
    .map((entry) => entry.trim())
    .filter((entry) => entry.length > 0);
}

async function cleanup() {
  if (browser) {
    await browser.close().catch(() => {});
    browser = undefined;
  }

  if (staticServer) {
    await new Promise((resolvePromise) => staticServer.close(() => resolvePromise()));
    staticServer = undefined;
  }

  if (server && "exitCode" in server && server.exitCode === null) {
    server.kill();
    await delay(500);
    if (server.exitCode === null) {
      server.kill("SIGKILL");
    }
  }
}
