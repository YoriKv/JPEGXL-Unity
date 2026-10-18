# libjxl native libraries (editor only)

All libraries are libjxl v0.12.0 (BSD-3-Clause; bundled Highway Apache-2.0 and skcms BSD-3 are statically linked; brotli MIT ships as sibling libraries). They are marked editor-only in their .meta files and are never included in player builds.

| Folder | Source | Notes |
|---|---|---|
| `x86_64/` | Official release `jxl-x64-windows.zip` from https://github.com/libjxl/libjxl/releases/tag/v0.12.0 | Windows x64 editor. Depends on the MSVC runtime the editor ships. |
| `OSX/` | Built by `.github/workflows/build-libjxl.yml` (macos-14 arm64 + macos-15-intel x86_64, lipo'd universal) | Install names and rpath rewritten to `@rpath/<plain name>` + `@loader_path`, ad-hoc signed. Untested on a Mac in this repository so far. |
| `Linux/x86_64/` | Built by the same workflow on ubuntu-22.04 with clang | Sonames and NEEDED entries rewritten to plain names, RUNPATH `$ORIGIN`. Needs glibc 2.35+ and libstdc++ 6. Untested on Linux in this repository so far. |

The binding pins the ABI: `JxlDecoder` refuses any library that does not report version 0.12.x. Replace all platforms together.
Native libraries cannot be unloaded, so replacing a file requires an editor restart.
