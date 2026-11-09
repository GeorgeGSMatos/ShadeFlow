"""Package a published Windows release without user settings, logs or test binaries."""
import hashlib
from pathlib import Path
import shutil
import zipfile
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[1]
version = ET.parse(root / "ShadeFlow.csproj").findtext("./PropertyGroup/Version")
release = root / "artifacts" / f"release-{version}"
published = release / "portable"
for required in ("ShadeFlow.exe", "ShadeFlow.dll", "coreclr.dll", "hostfxr.dll",
                 "PresentationFramework.dll", "Assets/ShadeFlow.ico"):
    if not (published / required).is_file():
        raise SystemExit(f"Missing self-contained release file: {required}")
shutil.copy2(root / "packaging/LEIA-ME.txt", published / "LEIA-ME.txt")
shutil.copy2(root / "CHANGELOG.md", published / "CHANGELOG.md")
shutil.copy2(root / "docs/validacao.md", published / "VALIDACAO.md")
for updater in ("Atualizar-ShadeFlow.cmd", "Atualizar-ShadeFlow.ps1"):
    shutil.copy2(root / "packaging" / updater, published / updater)
for legal_document in ("EULA.txt", "LICENSE.txt", "NOTICE.md"):
    shutil.copy2(root / legal_document, published / legal_document)
archive = release / f"ShadeFlow-{version}-win-x64-portable.zip"
with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as output:
    for source in sorted(published.rglob("*")):
        if source.is_file() and source.suffix.lower() != ".pdb":
            output.write(source, Path(f"ShadeFlow-{version}") / source.relative_to(published))
with zipfile.ZipFile(archive) as output:
    if output.testzip() is not None:
        raise SystemExit("ZIP integrity check failed")

# GitHub generates source archives directly from the versioned tag.
(release / "SHA256SUMS.txt").write_text(
    f"{hashlib.sha256(archive.read_bytes()).hexdigest()}  {archive.name}\n", encoding="utf-8")
print(f"{archive.name}: {archive.stat().st_size / 1024 / 1024:.1f} MiB")
