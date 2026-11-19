# LogTailPro — Architectural Decisions & Performance Record

Document Created: 2026-11-19

## Summary of Decisions

### 1. Performance Engineering Overhaul
* **Thread Offloading:** Ingestion shifted from UI dispatcher to `Task.Run()` background pool.
* **Line Trimming Complexity:** Reduced from $O(N)$ containment search to $O(1)$ line-number threshold test (`parent.LineNumber < minLineNo`).
* **Memory Management:** Replaced continuous LOH allocations with `ArrayPool<byte>.Shared.Rent()` and zero-allocation `ReadOnlySpan<char>` splitting in `FileWatcherService`.
* **Heuristic Regex Screening:** Added substring `IndexOf` and prefix gating before regex executions in `TimestampParser` (722k lines/s), `MultilineGroupingService` (2.89M lines/s), `CorrelationService` (8.6M lines/s), and `ErrorTimelineChartService` (516k lines/s).
* **Virtualization & Caching:** Enforced `VirtualizingPanel.IsVirtualizing="True"` on all log and bookmark lists; cached UI strings and chart typefaces lazily.

### 2. Versioning & Single Source of Truth
* **Single Source:** `<Version>` in `src/LogTailPro/LogTailPro.csproj` is the single source of truth.
* **UI Dynamic Reflection:** `AboutDialog` no longer hardcodes version text. It queries `Assembly.GetExecutingAssembly()` / `AssemblyInformationalVersionAttribute` at runtime so CI builds and package updates are instantly reflected.
* **Manifest Alignment:** `src/LogTailPro/app.manifest` assembly identity version is kept in sync with the project version.
* **Release Automation:** CI on `refs/heads/master` computes upgradeable versions (`2.10.<run_number>`), builds WiX MSI packages, and publishes GitHub releases automatically. Pushed tags must strictly match the project version.

### 3. Build & Test Conventions
* **Target:** .NET 9.0 (`net9.0-windows7.0`), C# 12, WPF.
* **Portable SDK:** `installer\out\dotnet\dotnet.exe` (.NET SDK 9.0.320).
* **Scripts:** `.\scripts\build-run.ps1` for building; verification must pass `Timeline.Tests` and `Timeline.IntegrationTests`.
