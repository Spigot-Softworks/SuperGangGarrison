import assert from "node:assert/strict";
import { readFile, readdir } from "node:fs/promises";
import { resolve, join } from "node:path";
import { brotliDecompressSync, gunzipSync } from "node:zlib";

const root = resolve(process.argv[2]);
let checked = 0;
async function visit(directory) {
  for (const entry of await readdir(directory, { withFileTypes: true })) {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) await visit(path);
    else if (entry.name.endsWith(".br") || entry.name.endsWith(".gz")) {
      const original = path.slice(0, -3);
      let source;
      try { source = await readFile(original); } catch (error) {
        // Authored compressed bot JSON assets need not have an uncompressed twin.
        if (error.code === "ENOENT" && path.includes("BotBrain")) continue;
        throw error;
      }
      const compressed = await readFile(path);
      const decoded = path.endsWith(".br") ? brotliDecompressSync(compressed) : gunzipSync(compressed);
      assert.ok(decoded.equals(source), `Stale compression: ${path}`);
      checked++;
    }
  }
}
await visit(root);
console.log(`Verified ${checked} compressed files against their originals.`);
