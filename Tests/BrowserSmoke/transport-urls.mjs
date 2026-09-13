import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";
import vm from "node:vm";

const html = await readFile(new URL("../../Client.Browser/wwwroot/index.html", import.meta.url), "utf8");
const source = html.match(/buildWebSocketUrl: (function \(host, port\) \{[\s\S]*?)\s*,\s*openWebSocketClient:/)?.[1];
assert.ok(source);
for (const pageProtocol of ["http:", "https:"]) {
  const build = vm.runInNewContext(`(${source})`, { URL, window: { location: { protocol: pageProtocol } } });
  assert.equal(build("wss64://api.example.test/api/private-rooms/ws/abc/1?token=a%2Fb%3Dc", 0),
    "wss://api.example.test/api/private-rooms/ws/abc/1?token=a%2Fb%3Dc");
  assert.equal(build("wss64://api.example.test:8443/api/private-rooms/ws/abc/2?token=abc", 0),
    "wss://api.example.test:8443/api/private-rooms/ws/abc/2?token=abc");
  assert.equal(build("WSS64://[::1]:8443/api/private-rooms/ws/abc/1?token=abc", 0),
    "wss://[::1]:8443/api/private-rooms/ws/abc/1?token=abc");
  const scheme = pageProtocol === "https:" ? "wss:" : "ws:";
  assert.equal(build("ws64://localhost", 8190), `${scheme}//localhost:8190/opengarrison/ws64`);
  assert.equal(build("localhost", 8190), `${scheme}//localhost:8190/opengarrison/ws`);
  assert.equal(build("ws://localhost:8190/custom?x=1", 8190), "ws://localhost:8190/custom?x=1");
  assert.throws(() => build("", 0));
}
console.log("Browser transport URL regression checks passed.");
