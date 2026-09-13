"""Regression checks for stale browser assets and interrupted incremental builds."""
from pathlib import Path
import importlib.util
import os
import tempfile
import unittest
import zipfile

SCRIPT = Path(__file__).resolve().parents[2] / "Tools/Browser/browser_build_cache.py"
spec = importlib.util.spec_from_file_location("browser_build_cache", SCRIPT)
cache = importlib.util.module_from_spec(spec)
spec.loader.exec_module(cache)


class BrowserBuildCacheTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="browser-cache-test-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.repo, self.web, self.cache = (self.root / name for name in ("repo", "site", "cache"))
        self.source = self.repo / "Core/Content/map.txt"
        self.put(self.source, b"first")
        for name in cache.REQUIRED_CONTENT:
            self.put(self.repo / "Client/Content/bin/DesktopGL/Content" / name, b"XNBfixture")
        self.put(self.web / "index.html", b"authored website")
        self.builds = 0

    def put(self, path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def builder(self, repo, staging, _):
        self.builds += 1
        for path in cache.files_under(repo / "Core/Content"):
            self.put(staging / "Content" / path.relative_to(repo / "Core/Content"), path.read_bytes())
        self.put(staging / "Content/bundle.zip", self.source.read_bytes())
        for path in cache.files_under(repo / "Maps"):
            self.put(staging / "Content/StockMaps" / path.relative_to(repo / "Maps"), path.read_bytes())
        for path in cache.files_under(repo / "Docking"):
            self.put(staging / "Content/StockMaps/Docking" / path.relative_to(repo / "Docking"), path.read_bytes())
        for path in cache.files_under(repo / "Plugins/Packaged/Client"):
            self.put(staging / "Plugins" / path.relative_to(repo / "Plugins/Packaged/Client"), path.read_bytes())

    def sync(self, builder=None):
        return cache.sync_assets(self.repo, self.web, self.cache, builder or self.builder)

    def test_unchanged_and_touched_inputs_keep_outputs_and_timestamps(self):
        self.sync()
        before = {name: (self.web / name).stat().st_mtime_ns for name in cache.generated_hashes(self.web)}
        os.utime(self.source, ns=(123000000000, 123000000000))
        result = self.sync()
        self.assertTrue(result["cacheHit"])
        self.assertEqual(result["copied"], 0)
        self.assertEqual(self.builds, 1)
        self.assertEqual(before, {name: (self.web / name).stat().st_mtime_ns for name in before})

    def test_same_size_edit_with_restored_timestamp_invalidates(self):
        self.sync()
        timestamp = self.source.stat().st_mtime_ns
        self.source.write_bytes(b"other")
        os.utime(self.source, ns=(timestamp, timestamp))
        self.assertFalse(self.sync()["cacheHit"])
        self.assertEqual((self.web / "Content/bundle.zip").read_bytes(), b"other")

    def test_add_remove_maps_plugins_and_stale_files(self):
        old_map = self.repo / "Maps/Old/map.json"
        plugin = self.repo / "Plugins/Packaged/Client/music.lua"
        self.put(old_map, b"old map")
        self.put(plugin, b"old plugin")
        self.sync()
        old_map.unlink()
        plugin.unlink()
        self.put(self.repo / "Docking/map.json", b"new map")
        self.put(self.web / "Content/unowned-stale.json", b"stale")
        self.sync()
        self.assertFalse((self.web / "Content/StockMaps/Old/map.json").exists())
        self.assertFalse((self.web / "Plugins/music.lua").exists())
        self.assertFalse((self.web / "Content/unowned-stale.json").exists())
        self.assertEqual((self.web / "Content/StockMaps/Docking/map.json").read_bytes(), b"new map")
        self.assertEqual((self.web / "index.html").read_bytes(), b"authored website")

    def test_missing_or_corrupt_cache_rebuilds_but_output_damage_only_copies(self):
        self.sync()
        (self.web / "Content/bundle.zip").write_bytes(b"damage")
        self.assertTrue(self.sync()["cacheHit"])
        self.assertEqual(self.builds, 1)
        (self.cache / "wwwroot/Content/bundle.zip").unlink()
        self.assertFalse(self.sync()["cacheHit"])
        (self.cache / "assets.json").write_text("broken json")
        self.assertFalse(self.sync()["cacheHit"])

    def test_source_code_and_required_compiled_content_invalidate(self):
        self.sync()
        self.put(self.repo / "Tools/BrowserAssetBuilder/Program.cs", b"changed builder")
        self.assertFalse(self.sync()["cacheHit"])
        font = self.repo / "Client/Content/bin/DesktopGL/Content/MenuFont.xnb"
        self.put(font, b"XNBchanged")
        self.assertFalse(self.sync()["cacheHit"])
        font.unlink()
        with self.assertRaisesRegex(ValueError, "Required compiled"):
            self.sync()

    def test_failed_or_racing_build_preserves_previous_output(self):
        self.sync()
        before = cache.generated_hashes(self.web)
        self.source.write_bytes(b"new input")
        def fail(*args):
            raise RuntimeError("builder failed")
        with self.assertRaisesRegex(RuntimeError, "builder failed"):
            self.sync(fail)
        self.assertEqual(before, cache.generated_hashes(self.web))
        def race(repo, staging, root):
            self.builder(repo, staging, root)
            self.source.write_bytes(b"changed during build")
        with self.assertRaisesRegex(RuntimeError, "changed during generation"):
            self.sync(race)
        self.assertEqual(before, cache.generated_hashes(self.web))
        self.assertFalse(self.sync()["cacheHit"])

    def test_publish_pruning_uses_current_inventory_and_rejects_escape(self):
        for name in ("_framework/current.wasm", "_framework/old.wasm", "old.js.br", "release.json", "LICENSE"):
            self.put(self.web / name, b"fixture")
        inventory = self.root / "inventory.txt"
        inventory.write_text("wwwroot/index.html\nwwwroot/_framework/current.wasm\n")
        self.assertEqual(cache.prune_publish(self.web, inventory), 2)
        self.assertTrue((self.web / "release.json").exists())
        self.assertTrue((self.web / "_framework/current.wasm").exists())
        inventory.write_text("wwwroot/../../outside.txt\n")
        with self.assertRaises(ValueError):
            cache.prune_publish(self.web, inventory)
        inventory.write_text("wwwroot/index.html\n")
        with self.assertRaisesRegex(ValueError, "Incomplete"):
            cache.prune_publish(self.web, inventory)

    def test_cache_output_overlap_and_simultaneous_writer_rejected(self):
        with self.assertRaisesRegex(ValueError, "separate directories"):
            cache.sync_assets(self.repo, self.web, self.web / "cache", self.builder)
        with cache.cache_lock(self.cache):
            with self.assertRaisesRegex(RuntimeError, "Another browser asset build"):
                self.sync()

    def test_archive_reuse_edits_and_corruption(self):
        archive, manifest = self.root / "site.zip", self.root / "archive.json"
        def create(destination):
            with zipfile.ZipFile(destination, "w") as output:
                output.write(self.web / "index.html", "index.html")
        self.assertTrue(cache.cached_archive(self.web, archive, manifest, create))
        timestamp = archive.stat().st_mtime_ns
        self.assertFalse(cache.cached_archive(self.web, archive, manifest, create))
        self.assertEqual(timestamp, archive.stat().st_mtime_ns)
        self.put(self.web / "index.html", b"updated website")
        self.assertTrue(cache.cached_archive(self.web, archive, manifest, create))
        with zipfile.ZipFile(archive) as output:
            self.assertEqual(output.read("index.html"), b"updated website")
        archive.write_bytes(b"damaged")
        self.assertTrue(cache.cached_archive(self.web, archive, manifest, create))


if __name__ == "__main__":
    unittest.main()
