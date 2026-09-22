# Third-party notices

LuminaVault's original code is licensed under the [MIT License](LICENSE).
Dependencies, bundled data, fonts and native libraries retain their original
copyrights and licenses. The project license does not relicense those materials.

## Distributed notices

| Distribution | Included notices |
| --- | --- |
| Frontend source and static builds | [Full dependency notices](LuminaVault-Frontend/public/THIRD_PARTY_NOTICES.txt) and [LuminaVault MIT license](LuminaVault-Frontend/public/LICENSE.txt), served at `/THIRD_PARTY_NOTICES.txt` and `/LICENSE.txt` |
| OCR English language model | [Apache-2.0 license](LuminaVault-Frontend/public/tesseract/lang/LICENSE.txt) and [provenance notice](LuminaVault-Frontend/public/tesseract/lang/NOTICE.txt), alongside the unchanged model |
| Backend build/publish output | [Full dependency notices](LuminaVault-Backend/THIRD_PARTY_NOTICES.txt), [LuminaVault MIT license](LuminaVault-Backend/LICENSE), and original rich-text notices in [licenses/](LuminaVault-Backend/licenses/) |
| Frontend production Docker image | The above frontend files plus Angular's extracted `3rdpartylicenses.txt` |

The frontend notice covers the locked production dependency graph, including
transitive dependencies and the Tesseract worker/WASM files copied directly into
the build. It also includes upstream notices for native OCR libraries and the
Emscripten runtime. Some listed dependencies are removed by bundling or unused in
particular runtime variants; their inclusion does not change their license.

The backend notice covers the restored API dependency graph, including design
tools. Original vendor `.rtf` notices are copied unchanged beside the text notice.
Neither notice claims to inventory every OS package in a container base image.
Retain the base-image and .NET runtime notices and satisfy their redistribution
terms if redistributing images.

## GSAP

The frontend uses GSAP 3.15.0, copyright 2008-2026 GreenSock. It is governed by the
[GSAP Standard No Charge license](https://gsap.com/community/standard-license/),
not MIT. A [snapshot of its terms](licenses/upstream/gsap-standard-license.txt)
is included in the frontend's full notices. Its terms include restrictions on
certain competing visual animation tools and require preserving its proprietary
notices. Commercial use being free of charge does not make GSAP open source.
Forks that retain GSAP must comply with its license independently of LuminaVault's
MIT license.

## Other components

- Angular, PrimeNG community 21.1.6, PrimeIcons, PrimeUIX, Three.js, QRCode, most
  Microsoft application libraries, and BCrypt.Net-Next use MIT terms.
- D3 components use BSD-3-Clause; tslib uses 0BSD.
- RxJS, Tesseract.js/core, the English OCR model, Serilog, and SQLitePCLRaw
  package declarations use Apache-2.0. The native SQLite engine is public domain.
- OCR native/runtime notices include Leptonica, the Independent JPEG Group,
  libpng, libtiff, libwebp, giflib, zlib, OpenLibm, Emscripten, musl and LLVM runtimes.
  This software is based in part on the work of the Independent JPEG Group.
- PrimeNG's supplied license file also contains commercial LTS terms. Those apply
  to `-lts` releases; the locked community release uses its MIT section.
- Development-only tools and datasets retain their own licenses, including
  Lightning CSS (MPL-2.0), caniuse-lite (CC-BY-4.0), and spdx-exceptions (CC-BY-3.0).
  They are not included as tools/datasets in the runtime distribution. If you
  redistribute development environments or `node_modules`, preserve their own
  notices and satisfy any applicable source-availability requirements too.

The optional proprietary `Microsoft.VisualStudio.Azure.Containers.Tools.Targets`
package has been removed. Building through the checked-in Dockerfiles and Docker
Compose does not require that Visual Studio integration.

Finnhub and CoinGecko are external services, not bundled libraries. Their data,
API access and trademarks remain subject to the providers' terms. User-uploaded
models, photographs and documents are not relicensed by this repository.

## Maintaining the notices

The notice snapshots are committed so regular builds and the existing per-project
Docker contexts include them without fetching legal documents or requiring Python.
After dependency changes, install/restore dependencies and regenerate from the
repository root (Python 3.10 or newer):

```sh
npm ci --prefix LuminaVault-Frontend
dotnet restore LuminaVault-Backend/LuminaVault-Backend.csproj
python scripts/update-license-notices.py
python scripts/update-license-notices.py --check
```

Review and commit the generated differences. CI checks the snapshots against the
locked npm graph and restored NuGet graph. The generator fails for missing license
texts, changed package versions, changed OCR model bytes, or a changed OCR core
version needing renewed native-library review. It verifies notice consistency;
it does not automatically determine legal compatibility of dependency upgrades.

The [upstream source manifest](licenses/upstream/sources.json) records source URLs,
revisions and SHA-256 hashes for supplemental texts missing from package archives.
The OCR native revisions come from Tesseract.js core 7.0.0's submodules; runtime
notices come from its Emscripten 4.0.15 build configuration. To deliberately refresh
these supplemental texts, review the URLs/revisions in
`scripts/vendor-license-sources.py`, run it with network access, then regenerate
the notices. The GSAP page is a dated website snapshot, not an immutable Git source.

This notice is attribution and distribution information; it does not grant rights
beyond those granted by each applicable license.
