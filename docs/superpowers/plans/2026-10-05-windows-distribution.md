# Windows Distribution Implementation Plan

**Goal:** Build a distributable, per-user one-click installer with custom startup images.

**Architecture:** Keep the existing iframe animation and verified MSIX/CDP launch. Add a small .NET Framework installer with an embedded runtime ZIP, transactional file installation, shortcuts and uninstall support. Retain preferences in a separate IndexedDB namespace.

**Tech Stack:** HTML/CSS/Canvas, Node shipped by official Codex, Windows PowerShell 5.1, .NET Framework C# compiler and ZIP APIs. Development tests use Node 22+.

- [x] Prepare generic defaults, icon, metadata, attribution and separate preferences namespace.
- [x] Write failing tests for opening settings on a fresh and an existing verified animation session, then implement `--settings`, `-OpenSettings` and session reuse.
- [x] Write C# installation tests for traversal rejection, first install, updates, rollback, unowned files and uninstall. Run red, implement `InstallCore.cs`, run green.
- [x] Implement embedded-resource `Setup.cs`, installation feedback, desktop/start-menu shortcuts, HKCU uninstall registration and safe uninstall.
- [x] Add release build script, UTF-8 PowerShell launch, portable ZIP, setup EXE and manifest. Build in a temporary release directory.
- [x] Add README, FAQ, architecture, release workflow and upstream notices. Do not assert an upstream license that is absent.
- [x] Run `node --test tools/*.test.mjs`, `powershell -File tools/test-installer.ps1`, and `powershell -File tools/build-release.ps1`.
- [x] Verify actual official window using a distinct diagnostic browser profile; check default settings, custom static image, custom GIF, text persistence, skip and restore. Never close the user's active main window.
- [x] Request static review; fix material issues; verify final archive entries and file hashes; create clean source Git history and source ZIP.

No publishing or registry/shortcut changes on this machine are needed for build and core tests. Installer shell integration is tested using explicitly isolated desktop/start-menu/registry locations.
