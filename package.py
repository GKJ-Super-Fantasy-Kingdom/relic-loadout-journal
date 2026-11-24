"""Create the Thunderstore ZIP from the current Release build and listing files."""
import json
from pathlib import Path
from tempfile import TemporaryDirectory
from xml.etree import ElementTree
from zipfile import ZIP_DEFLATED, ZipFile

root = Path(__file__).resolve().parent
listing = root / "thunderstore"
manifest = json.loads((listing / "manifest.json").read_text(encoding="utf-8"))
project = ElementTree.parse(root / "RelicLoadoutJournal.csproj")
version = manifest["version_number"]
if version != project.findtext(".//Version"):
    raise SystemExit("Match the project and manifest versions before packaging.")

framework = project.findtext(".//TargetFramework")
files = {
    "LICENSE": root / "LICENSE",
    "plugins/RelicLoadoutJournal.dll": root / "bin" / "Release" / framework / "RelicLoadoutJournal.dll",
    "manifest.json": listing / "manifest.json",
    "README.md": listing / "README.md",
    "CHANGELOG.md": root / "CHANGELOG.md",
    "icon.png": listing / "icon.png",
}
output = root / "dist" / f"RelicLoadoutJournal-{version}.zip"
output.parent.mkdir(exist_ok=True)
with TemporaryDirectory(dir=output.parent) as scratch:
    staged = Path(scratch) / output.name
    with ZipFile(staged, "w", ZIP_DEFLATED) as archive:
        for name, source in files.items():
            archive.write(source, name)
    with ZipFile(staged) as archive:
        if archive.testzip() is not None:
            raise SystemExit("ZIP verification failed.")
    staged.replace(output)
print(output)
