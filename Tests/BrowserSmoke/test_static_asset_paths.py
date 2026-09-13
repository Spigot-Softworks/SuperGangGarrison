"""Exercise the actual browser project's generated-asset mapping with MSBuild."""
from pathlib import Path
import json
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET

REPO = Path(__file__).resolve().parents[2]


class StaticAssetPathsTests(unittest.TestCase):
    def test_generated_assets_keep_individual_relative_paths(self):
        source = ET.parse(REPO / "Client.Browser/OpenGarrison.Client.Browser.csproj")
        staging = source.find(".//Target[@Name='SyncCoreContentToBrowserWwwroot']")
        mapping = list(staging.findall("ItemGroup"))[-1]
        with tempfile.TemporaryDirectory(prefix="og-browser-paths-") as temporary:
            root = Path(temporary)
            generated = root / "generated"
            for name in ("Content/Backgrounds/background0.png", "Content/Backgrounds/background0.xml",
                         "Content/StockMaps/Classic/cp/map.json", "Plugins/Wheel/plugin.json"):
                path = generated / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("fixture")
            authored = root / "wwwroot/css/app.css"
            authored.parent.mkdir(parents=True)
            authored.write_text("body {}")
            project = ET.Element("Project")
            properties = ET.SubElement(project, "PropertyGroup")
            ET.SubElement(properties, "OpenGarrisonBrowserGeneratedWwwRoot").text = str(generated)
            items = ET.SubElement(project, "ItemGroup")
            ET.SubElement(items, "Content", Include="wwwroot/css/app.css")
            target = ET.SubElement(project, "Target", Name="StageAssets")
            target.append(mapping)
            project_path = root / "paths.proj"
            ET.ElementTree(project).write(project_path, encoding="unicode")
            result = subprocess.run(["dotnet", "msbuild", str(project_path), "-nologo",
                                     "-t:StageAssets", "-getItem:Content"], cwd=root,
                                    check=True, capture_output=True, text=True)
            assets = json.loads(result.stdout)["Items"]["Content"]
            actual = {Path(item["Identity"]).relative_to(generated).as_posix(): item["Link"].replace("\\", "/")
                      for item in assets if Path(item["Identity"]).is_absolute()}
            self.assertEqual(len(actual), 4)
            self.assertEqual(actual, {name: "wwwroot/" + name for name in actual})
            self.assertEqual(len(set(actual.values())), len(actual))


if __name__ == "__main__":
    unittest.main()
