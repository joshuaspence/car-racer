#!/usr/bin/env python3
"""Create missing Unity .meta files under Unity/Assets with deterministic GUIDs.

Unity creates .meta files (holding each asset's GUID) when the editor imports a new file.
Code written outside the editor (e.g. by CI-only contributors) would otherwise get random
GUIDs on each machine's first import, causing churn and broken references. This script
derives the GUID from the asset path, so everyone gets the same one.

Usage: tools/generate_unity_meta.py [--check]
  --check  exit non-zero if any .meta file is missing or orphaned (for CI)
"""

import hashlib
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
ASSETS = ROOT / "Unity" / "Assets"

FOLDER = """fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

MONO_SCRIPT = """fileFormatVersion: 2
guid: {guid}
MonoImporter:
  externalObjects: {{}}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {{instanceID: 0}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

ASSEMBLY_DEFINITION = """fileFormatVersion: 2
guid: {guid}
AssemblyDefinitionImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEXT_SCRIPT = """fileFormatVersion: 2
guid: {guid}
TextScriptImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

DEFAULT = """fileFormatVersion: 2
guid: {guid}
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

TEMPLATES = {
    ".cs": MONO_SCRIPT,
    ".asmdef": ASSEMBLY_DEFINITION,
    ".json": TEXT_SCRIPT,
    ".txt": TEXT_SCRIPT,
    ".md": TEXT_SCRIPT,
}


def guid_for(path: Path) -> str:
    relative = path.relative_to(ROOT).as_posix()
    return hashlib.md5(f"car-racer:{relative}".encode("utf-8")).hexdigest()


def assets():
    for path in sorted(ASSETS.rglob("*")):
        if path.suffix == ".meta" or path.name.startswith("."):
            continue
        yield path


def main() -> int:
    check = "--check" in sys.argv[1:]
    missing = []
    for path in assets():
        meta = path.with_name(path.name + ".meta")
        if meta.exists():
            continue
        missing.append(meta)
        if not check:
            template = FOLDER if path.is_dir() else TEMPLATES.get(path.suffix, DEFAULT)
            meta.write_text(template.format(guid=guid_for(path)), encoding="utf-8")

    orphaned = [
        meta
        for meta in sorted(ASSETS.rglob("*.meta"))
        if not meta.with_name(meta.name[: -len(".meta")]).exists()
    ]

    for meta in missing:
        print(f"{'missing' if check else 'created'}: {meta.relative_to(ROOT)}")
    for meta in orphaned:
        print(f"orphaned: {meta.relative_to(ROOT)}")

    if check and (missing or orphaned):
        print("Run tools/generate_unity_meta.py (and delete orphaned .meta files).", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
