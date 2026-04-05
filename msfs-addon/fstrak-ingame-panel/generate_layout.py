import os
import json

addon_dir = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fstrak-ingame-panel")
content = []

SKIP_DIRS = {"Build", "_Temp", "PackageSources", "PackageDefinitions", "Packages"}

for root, dirs, files in os.walk(addon_dir):
    # Skip Build and temp directories — not part of the MSFS runtime package
    dirs[:] = [d for d in dirs if d not in SKIP_DIRS]
    for fname in files:
        if fname in ("layout.json", "README.txt", "package.xml"):
            continue
        fpath = os.path.join(root, fname)
        rel = os.path.relpath(fpath, addon_dir).replace("\\", "/")
        # Convert Unix timestamp to Windows FILETIME (100-nanosecond intervals since 1601-01-01)
        mtime_filetime = int(os.path.getmtime(fpath) * 10000000) + 116444736000000000
        content.append({
            "path": rel,
            "size": os.path.getsize(fpath),
            "date": mtime_filetime
        })

layout = {"content": content}
layout_path = os.path.join(addon_dir, "layout.json")
with open(layout_path, "w") as f:
    json.dump(layout, f, indent=2)
print(f"layout.json written with {len(content)} entries.")
