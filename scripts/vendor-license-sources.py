"""Fetch the pinned upstream texts used by update-license-notices.py.

Run deliberately when maintaining notices; normal builds never need network
access to these sources. Copyright notices and license texts are not rewritten.
"""

import concurrent.futures
import hashlib
from html.parser import HTMLParser
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "licenses" / "upstream"
MODEL_REV = "806cd9adc8c6e8abc11c782db1818c990576bebc"
CORE_REV = "acffef2b66eb44a31df297e11d905f4b39001068"


def github(repo, revision, path):
    return f"https://raw.githubusercontent.com/{repo}/{revision}/{path}"


# (filename, description, destination group, authoritative source)
SOURCES = [
    ("bcrypt-4.0.3.txt", "BCrypt.Net-Next 4.0.3", "backend", github("BcryptNet/bcrypt.net", "4.0.3", "licence.txt")),
    ("gsap-standard-license.txt", "GSAP Standard No Charge license (snapshot for GSAP 3.15.0)", "frontend", "https://gsap.com/community/standard-license/"),
    ("tessdata-LICENSE.txt", "English Tesseract language data", "model", github("naptha/tessdata", MODEL_REV, "LICENSE")),
    ("giflib-COPYING.txt", "giflib fa376720", "ocr", github("mirrorer/giflib", "fa37672085ce4b3d62c51627ab3c8cf2dda8009a", "COPYING")),
    ("leptonica-LICENSE.txt", "Leptonica 4af068b5", "ocr", github("DanBloomberg/leptonica", "4af068b56a9674da915debea4ed7e1b9885b17e8", "leptonica-license.txt")),
    ("libjpeg-README.txt", "Independent JPEG Group libjpeg 6c0fcb8d", "ocr", github("LuaDist/libjpeg", "6c0fcb8ddee365e7abc4d332662b06900612e923", "README")),
    ("libpng-LICENSE.txt", "libpng a37d4836", "ocr", github("glennrp/libpng", "a37d4836519517bdce6cb9d956092321eca3e73b", "LICENSE")),
    ("libtiff-COPYRIGHT.txt", "libtiff b51bb157", "ocr", "https://gitlab.com/libtiff/libtiff/-/raw/b51bb157123264e26d34c09cc673d213aea61fc7/COPYRIGHT"),
    ("libwebp-COPYING.txt", "libwebp 20ef03ee", "ocr", github("webmproject/libwebp", "20ef03ee351d4ff03fc5ff3ec4804a879d1b9d5c", "COPYING")),
    ("libwebp-PATENTS.txt", "libwebp additional patent grant", "ocr", github("webmproject/libwebp", "20ef03ee351d4ff03fc5ff3ec4804a879d1b9d5c", "PATENTS")),
    ("openlibm-LICENSE.md", "OpenLibm ae2d9169", "ocr", github("JuliaMath/openlibm", "ae2d91698508701c83cab83714d42a1146dccf85", "LICENSE.md")),
    ("tesseract-LICENSE.txt", "Tesseract 2a9c1c49", "ocr", github("Balearica/tesseract", "2a9c1c49c360462733c386d2a44fcd22c4e21411", "LICENSE")),
    ("zlib-README.txt", "zlib 21767c65", "ocr", github("madler/zlib", "21767c654d31d2dccdde4330529775c6c5fd5389", "README")),
    ("emscripten-LICENSE.txt", "Emscripten 4.0.15 runtime", "ocr", github("emscripten-core/emscripten", "4.0.15", "LICENSE")),
    ("musl-COPYRIGHT.txt", "musl runtime from Emscripten 4.0.15", "ocr", github("emscripten-core/emscripten", "4.0.15", "system/lib/libc/musl/COPYRIGHT")),
    ("llvm-runtime-LICENSE.txt", "LLVM compiler-rt runtime from Emscripten 4.0.15", "ocr", github("emscripten-core/emscripten", "4.0.15", "system/lib/compiler-rt/LICENSE.TXT")),
    ("libcxx-LICENSE.txt", "LLVM libc++ runtime from Emscripten 4.0.15", "ocr", github("emscripten-core/emscripten", "4.0.15", "system/lib/libcxx/LICENSE.TXT")),
    ("libcxxabi-LICENSE.txt", "LLVM libc++abi runtime from Emscripten 4.0.15", "ocr", github("emscripten-core/emscripten", "4.0.15", "system/lib/libcxxabi/LICENSE.TXT")),
]


class LicensePageText(HTMLParser):
    """Preserve paragraph boundaries and link destinations in the GSAP page."""
    def __init__(self):
        super().__init__()
        self.parts = []
        self.link = None

    def handle_starttag(self, tag, attrs):
        if tag in ("p", "li", "h1", "h2", "h3", "h4", "br", "div"):
            self.parts.append("\n")
        if tag == "a":
            self.link = dict(attrs).get("href")

    def handle_endtag(self, tag):
        if tag == "a" and self.link:
            self.parts.append(f" ({self.link})")
            self.link = None
        if tag in ("p", "li", "h1", "h2", "h3", "h4", "div"):
            self.parts.append("\n")

    def handle_data(self, data):
        self.parts.append(data)


def fetch(source):
    filename, description, group, url = source
    request = urllib.request.Request(url, headers={"User-Agent": "LuminaVault-license-notices"})
    with urllib.request.urlopen(request, timeout=30) as response:
        data = response.read()
    if filename == "gsap-standard-license.txt":
        parser = LicensePageText()
        parser.feed(data.decode("utf-8"))
        page = "".join(parser.parts)
        start = page.index('Standard "No Charge" GSAP License')
        end = page.index("Copyright (©) 2025, Webflow", start) + len("Copyright (©) 2025, Webflow")
        text = "\n".join(line.strip() for line in page[start:end].splitlines() if line.strip())
        data = (text + "\n").encode("utf-8")
    if not data or b"<!doctype html" in data[:100].lower():
        raise ValueError(f"Expected a license text at {url}")
    (DEST / filename).write_bytes(data)
    return {"file": filename, "description": description, "group": group,
            "url": url, "sha256": hashlib.sha256(data).hexdigest()}


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        results = list(pool.map(fetch, SOURCES))
    manifest = {"tesseractCoreRevision": CORE_REV, "languageModelRevision": MODEL_REV,
                "languageModelSha256": "45b4cb346724ac1774f1c36f42f182b887bcdb28ebe63e6fff90ac41f3fcff91",
                "sources": results}
    (DEST / "sources.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Saved {len(results)} upstream license texts and their SHA-256 provenance.")


if __name__ == "__main__":
    main()
