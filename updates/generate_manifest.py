import hashlib
import json
import os

REPO_RAW_BASE = "https://raw.githubusercontent.com/abdifaizal/PlayBrwsr/main"

COMPONENTS = {
    "browsec_xpi": {
        "version": "4.1.1",
        "path": "extensions/browsec@browsec.com.xpi",
    },
    "user_chrome": {
        "version": "1.0.1",
        "path": "data/profile/chrome/userChrome.css",
    },
    "user_content": {
        "version": "1.0.1",
        "path": "data/profile/chrome/userContent.css",
    },
    "user_prefs": {
        "version": "1.0.1",
        "path": "data/profile/user.js",
    },
    "hub_html": {
        "version": "1.0.1",
        "path": "app/hub.html",
    },
}


def sha256_file(path: str) -> str:
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def main():
    manifest = {"manifest_version": 1, "components": {}}
    for key, info in COMPONENTS.items():
        rel = info["path"]
        manifest["components"][key] = {
            "version": info["version"],
            "url": f"{REPO_RAW_BASE}/{rel}",
            "target": rel,
            "sha256": sha256_file(rel),
        }

    os.makedirs("updates", exist_ok=True)
    out = os.path.join("updates", "manifest.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")
    print(f"Generated {out}")


if __name__ == "__main__":
    main()
