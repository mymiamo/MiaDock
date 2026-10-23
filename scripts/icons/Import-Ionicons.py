"""Import the exact dock subset from a pinned official Ionicons npm tarball."""
import hashlib
import io
import json
from pathlib import Path
import re
import tarfile
import urllib.request

root = Path(__file__).resolve().parents[2]
catalog = (root / "src/MiaDock.UI/Icons/DockIconCatalog.cs").read_text(encoding="utf-8")
version = re.search(r'Version = "([\d.]+)"', catalog)[1]
keys = set(re.findall(r'\] = "([a-z-]+)"', catalog))
keys.add("help-circle")
url = f"https://registry.npmjs.org/ionicons/-/ionicons-{version}.tgz"
archive = root / "artifacts" / f"ionicons-{version}.tgz"
data = archive.read_bytes() if archive.exists() else urllib.request.urlopen(url).read()
destination = root / "src/MiaDock.UI/Assets/Ionicons"
destination.mkdir(parents=True, exist_ok=True)
manifest = {"package": "ionicons", "version": version, "source": url,
            "archiveSha256": hashlib.sha256(data).hexdigest(), "assets": {}}
with tarfile.open(fileobj=io.BytesIO(data), mode="r:gz") as package:
    for key in sorted(keys):
        for suffix in ["-outline", ""]:
            name = f"{key}{suffix}.svg"
            content = package.extractfile(f"package/dist/svg/{name}").read()
            (destination / name).write_bytes(content)
            manifest["assets"][name] = hashlib.sha256(content).hexdigest()
    (destination / "LICENSE").write_bytes(package.extractfile("package/LICENSE").read())
(destination / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
print(f"Imported {len(manifest['assets'])} Ionicons {version} SVGs and MIT license")
