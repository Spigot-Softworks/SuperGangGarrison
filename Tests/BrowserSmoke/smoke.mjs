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
const practiceClass = (process.env.OG_BROWSER_PRACTICE_CLASS ?? "Pyro").trim();
const scenario = (process.env.OG_BROWSER_SCENARIO ?? "practice").trim().toLowerCase();
if (!["practice", "dedicated"].includes(scenario)) {
  throw new Error(`Unsupported OG_BROWSER_SCENARIO=${scenario}. Expected 'practice' or 'dedicated'.`);
}
const remoteHost = (process.env.OG_BROWSER_REMOTE_HOST ?? "").trim();
const remotePort = Number.parseInt(process.env.OG_BROWSER_REMOTE_PORT ?? "8190", 10);
const remoteClass = (process.env.OG_BROWSER_REMOTE_CLASS ?? "Pyro").trim();
const remoteSessionSampleMs = Number.parseInt(process.env.OG_BROWSER_REMOTE_SAMPLE_MS ?? `${gameplayFpsSampleMs}`, 10);
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
  const scenarioResult = scenario === "dedicated"
    ? await runDedicatedScenario(page)
    : await runPracticeScenario(page);

  const gameplayMetrics = await scenarioResult.gameplayMetrics;
  if (!gameplayMetrics) {
    throw new Error("Browser host did not expose frame-pump metrics.");
  }

  if (gameplayMetrics.recentFps < minGameplayFps) {
    throw new Error(
      `Browser gameplay FPS below smoke threshold: ${gameplayMetrics.recentFps.toFixed(1)}fps over ` +
      `${scenarioResult.sampleDurationMs}ms, expected at least ${minGameplayFps}fps. Metrics: ${JSON.stringify(gameplayMetrics)}`);
  }

  if (gameplayMetrics.maxPumpDurationMs > maxPumpDurationMs) {
    throw new Error(
      `Browser gameplay frame pump exceeded the long-frame threshold: ${gameplayMetrics.maxPumpDurationMs.toFixed(1)}ms, ` +
      `expected at most ${maxPumpDurationMs}ms. Metrics: ${JSON.stringify(gameplayMetrics)}`);
  }

  if (scenarioResult.performanceSnapshot) {
    console.log(`Performance snapshot: ${JSON.stringify(scenarioResult.performanceSnapshot)}`);
  }

  if (scenarioResult.sessionHealth) {
    console.log(`Session health: ${JSON.stringify(scenarioResult.sessionHealth)}`);
  }

  await page.screenshot({ path: scenarioResult.screenshotPath, timeout: 60_000 });

  const consoleErrors = consoleEvents.filter((entry) =>
    (entry.startsWith("[error]") || entry.startsWith("pageerror:"))
    && !entry.includes("devmessages.txt")
    && !entry.includes("Access to fetch at 'https://www.ganggarrison.com/devmessages.txt'")
    && !entry.includes("ERR_FAILED"));
  if (consoleErrors.length > 0) {
    throw new Error(`Browser console reported errors:\n${consoleErrors.join("\n")}`);
  }

  console.log("Browser smoke test passed.");
  console.log(`Scenario: ${scenario}`);
  console.log(`KNI host started: ${scenarioResult.finalSummary.kniHostStarted}`);
  console.log(`Host status: ${scenarioResult.finalSummary.hostStatus}`);
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

async function runPracticeScenario(page) {
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
  await joinPracticeAndExerciseRapidFire(page, practiceClass);
  timings.practiceJoinAndExerciseMs = Date.now() - joinPracticeStartedAt;

  await runPostJoinCommands(page);
  await runPostJoinMovement(page);
  await verifyResizeRecovery(page);

  const finalSummary = await waitForStableHost(page, practiceSpawnSettleTimeoutMs);
  if (finalSummary.hostFailed) {
    throw new Error(`Browser host failed after joining Practice. Status: ${finalSummary.hostStatus}`);
  }

  const finalAutomationState = await readAutomationState(page);
  validateFinalAutomationState(finalAutomationState);

  return {
    finalSummary,
    gameplayMetrics: await sampleBrowserMetrics(page, gameplayFpsSampleMs),
    performanceSnapshot: await samplePerformanceSnapshot(page),
    sessionHealth: null,
    screenshotPath: join(artifactsDir, "browser-smoke-practice.png"),
    sampleDurationMs: gameplayFpsSampleMs
  };
}

async function runDedicatedScenario(page) {
  if (!remoteHost) {
    throw new Error("Dedicated browser smoke requires OG_BROWSER_REMOTE_HOST.");
  }

  const connectStartedAt = Date.now();
  await connectToDedicatedServer(page, remoteHost, remotePort);
  timings.dedicatedConnectMs = Date.now() - connectStartedAt;

  const joinStartedAt = Date.now();
  await page.evaluate(() => globalThis.OpenGarrisonBrowserHost?.resetMetrics?.());
  await joinDedicatedSessionAndExerciseMovement(page, remoteClass);
  timings.dedicatedJoinAndExerciseMs = Date.now() - joinStartedAt;

  await runPostJoinCommands(page);
  await runPostJoinMovement(page);

  const finalSummary = await waitForStableHost(page, practiceSpawnSettleTimeoutMs);
  if (finalSummary.hostFailed) {
    throw new Error(`Browser host failed after joining dedicated server. Status: ${finalSummary.hostStatus}`);
  }

  const finalAutomationState = await readAutomationState(page);
  validateFinalAutomationState(finalAutomationState);

  return {
    finalSummary,
    gameplayMetrics: await sampleBrowserMetrics(page, remoteSessionSampleMs),
    performanceSnapshot: await samplePerformanceSnapshot(page),
    sessionHealth: await sampleOnlineSessionHealth(page, remoteSessionSampleMs),
    screenshotPath: join(artifactsDir, "browser-smoke-dedicated.png"),
    sampleDurationMs: remoteSessionSampleMs
  };
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
  const started = await page.evaluate(
    async ({ enemyBots, friendlyBots }) =>
      await globalThis.OpenGarrisonBrowserHost?.startAutomationPractice?.(enemyBots, friendlyBots) ?? false,
    { enemyBots: practiceEnemyBots, friendlyBots: practiceFriendlyBots });
  if (!started) {
    throw new Error(`Browser automation practice start failed. State: ${JSON.stringify(await readAutomationState(page))}`);
  }
  timings.launchClickedStartPracticeMs = Date.now() - launchStartedAt;
}

async function connectToDedicatedServer(page, host, port) {
  const rootState = await waitForAutomationState(
    page,
    (state) => state && state.mainMenuOpen && state.mainMenuOverlay === "None" && state.mainMenuPage === "Root",
    hostSettleTimeoutMs,
    "root main menu"
  );
  await clickAutomationAction(page, rootState.menuButtons, "Play Online");

  const onlineState = await waitForAutomationState(
    page,
    (state) => state && state.mainMenuOpen && state.mainMenuOverlay === "None" && state.mainMenuPage === "PlayOnline",
    15_000,
    "online menu"
  );
  await clickAutomationAction(page, onlineState.menuButtons, "Join (IP)");

  const manualConnectState = await waitForAutomationState(
    page,
    (state) => state && state.manualConnectOpen && state.mainMenuOverlay === "ManualConnect",
    15_000,
    "manual connect menu"
  );

  if (!await setAutomationValue(page, "manualconnect_host", host)) {
    throw new Error(`Failed to set manual connect host to '${host}'. State: ${JSON.stringify(manualConnectState)}`);
  }

  if (!await setAutomationValue(page, "manualconnect_port", `${port}`)) {
    throw new Error(`Failed to set manual connect port to '${port}'. State: ${JSON.stringify(manualConnectState)}`);
  }

  const connectStarted = await page.evaluate(
    async ({ hostName, portText }) =>
      await globalThis.OpenGarrisonBrowserHost?.connectAutomationTarget?.(hostName, portText) ?? false,
    { hostName: host, portText: `${port}` });
  if (!connectStarted) {
    throw new Error(`Dedicated connect automation did not start for ${host}:${port}. State: ${JSON.stringify(await readAutomationState(page))}`);
  }

  await waitForDedicatedJoinState(page, 20_000);
}

async function setPracticeBotCount(page, kind, currentCount, targetCount) {
  if (currentCount === targetCount) {
    return;
  }

  const fieldName = kind === "Enemy" ? "practice_enemy_bots" : "practice_friendly_bots";
  const countField = kind === "Enemy" ? "practiceEnemyBotCount" : "practiceFriendlyBotCount";
  const updated = await setAutomationValue(page, fieldName, `${targetCount}`);
  if (!updated) {
    throw new Error(`Failed to set ${kind.toLowerCase()} bot count to ${targetCount}.`);
  }

  const latestState = await waitForAutomationState(
    page,
    (state) => state && state.practiceSetupOpen && state[countField] === targetCount,
    5_000,
    `${kind.toLowerCase()} bot count change`);
  const latestCount = latestState[countField];
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

async function joinPracticeAndExerciseRapidFire(page, preferredClass) {
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
  await clickAutomationAction(page, classSelectState.classSelectButtons, preferredClass);

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

async function joinDedicatedSessionAndExerciseMovement(page, preferredClass) {
  const joinedState = await waitForDedicatedJoinState(page, 20_000);
  if (joinedState.teamSelectOpen) {
    await clickAutomationAction(page, joinedState.teamSelectButtons, "Auto Select");
  }

  const classSelectState = await waitForAutomationState(
    page,
    (state) => state && state.classSelectOpen,
    15_000,
    "dedicated class select"
  );
  await clickAutomationAction(page, classSelectState.classSelectButtons, preferredClass);

  const spawnedState = await waitForAutomationState(
    page,
    (state) => state && state.networkConnected && !state.classSelectOpen && !state.awaitingJoin && state.localPlayerAlive,
    20_000,
    "dedicated spawn completion"
  );

  await setBrowserKey(page, "KeyD", true);
  await delay(750);
  await setBrowserKey(page, "KeyD", false);

  await delay(1_000);
  await page.mouse.move(500, 300);
  await page.mouse.down();
  await delay(1_000);
  await page.mouse.up();
  await delay(1_000);
}

async function waitForDedicatedJoinState(page, timeoutMs) {
  return await waitForAutomationState(
    page,
    (state) => {
      if (!state) {
        return false;
      }

      if (typeof state.statusMessage === "string" && state.statusMessage.toLowerCase().includes("connect failed")) {
        return true;
      }

      return state.networkConnected && (state.teamSelectOpen || state.classSelectOpen || state.shell === "Gameplay");
    },
    timeoutMs,
    "dedicated join state"
  ).then((state) => {
    if (typeof state.statusMessage === "string" && state.statusMessage.toLowerCase().includes("connect failed")) {
      throw new Error(`Dedicated connect failed. State: ${JSON.stringify(state)}`);
    }

    return state;
  });
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

async function setAutomationValue(page, fieldName, value) {
  return await page.evaluate(
    async ({ field, nextValue }) =>
      await globalThis.OpenGarrisonBrowserHost?.setAutomationValue?.(field, nextValue) ?? false,
    { field: fieldName, nextValue: value });
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
  if (labels.has("Play Offline")
    || labels.has("Play Online")
    || labels.has("Settings")
    || labels.has("Practice")
    || labels.has("Minigames")
    || labels.has("Host Match")
    || labels.has("Join (IP)")
    || labels.has("Join (Lobby)")
    || labels.has("Back")) {
    return "menu";
  }

  if (labels.has("Start Practice") || labels.has("Enemy Bots +") || labels.has("Friendly Bots +")) {
    return "practice";
  }

  if (labels.has("Connect") || labels.has("Edit Host") || labels.has("Edit Port")) {
    return "manualconnect";
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

async function samplePerformanceSnapshot(page) {
  return await page.evaluate(async () => await globalThis.OpenGarrisonBrowserHost?.getPerformanceSnapshot?.() ?? null);
}

async function sampleOnlineSessionHealth(page, durationMs) {
  const startedAt = Date.now();
  let previousState = null;
  let previousMetrics = null;
  let currentStallMs = 0;
  let maxSnapshotStallMs = 0;
  let sampleCount = 0;
  while (Date.now() - startedAt < durationMs) {
    const [state, metrics] = await Promise.all([
      readAutomationState(page),
      page.evaluate(() => globalThis.OpenGarrisonBrowserHost?.getMetrics?.() ?? null)
    ]);
    sampleCount += 1;

    const sameSnapshotFrame = previousState
      && state
      && state.networkConnected
      && state.lastAppliedSnapshotFrame === previousState.lastAppliedSnapshotFrame;
    const framePumpAdvanced = previousMetrics
      && metrics
      && metrics.completedPumps > previousMetrics.completedPumps;
    if (sameSnapshotFrame && framePumpAdvanced) {
      currentStallMs += 1000;
      maxSnapshotStallMs = Math.max(maxSnapshotStallMs, currentStallMs);
    } else {
      currentStallMs = 0;
    }

    previousState = state;
    previousMetrics = metrics;
    await delay(1000);
  }

  return {
    samples: sampleCount,
    maxSnapshotStallMs,
    lastAppliedSnapshotFrame: previousState?.lastAppliedSnapshotFrame ?? 0,
    estimatedPingMilliseconds: previousState?.estimatedPingMilliseconds ?? -1,
    queuedAuthoritativeSnapshotCount: previousState?.queuedAuthoritativeSnapshotCount ?? 0
  };
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
  const focused = await page.evaluate(() => globalThis.OpenGarrisonBrowserHost?.focusCanvas?.() ?? false);
  if (!focused) {
    throw new Error("Browser canvas focus helper was unavailable for chat input verification.");
  }

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

function validateFinalAutomationState(finalAutomationState) {
  if (finalAutomationState && finalAutomationState.audioAvailable === false) {
    throw new Error(`Browser audio disabled during smoke. Automation state: ${JSON.stringify(finalAutomationState)}`);
  }

  if (finalAutomationState && finalAutomationState.looseSheetVisualCount > maxLooseSheetVisuals) {
    throw new Error(
      `Browser loose-sheet visual count exceeded the cap: ${finalAutomationState.looseSheetVisualCount}, ` +
      `expected at most ${maxLooseSheetVisuals}. Automation state: ${JSON.stringify(finalAutomationState)}`);
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
