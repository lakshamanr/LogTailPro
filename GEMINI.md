# LogTailPro — Project Rules & Architectural Decisions

This document records the foundational architectural decisions, performance guidelines, and build/release rules for the LogTailPro codebase. All agents and developers must adhere to these decisions.

---

## 1. Version Management & Single Source of Truth

- **Single Source of Truth:** The authoritative project version is defined in [`src/LogTailPro/LogTailPro.csproj`](file:///c:/LogTailPro/src/LogTailPro/LogTailPro.csproj) (`<Version>2.10.0</Version>`).
- **Dynamic UI Versioning:** NEVER hardcode version strings in XAML. [`AboutDialog`](file:///c:/LogTailPro/src/LogTailPro/Views/Dialogs/AboutDialog.xaml.cs) and any UI surfaces must dynamically resolve the version at runtime via:
  ```csharp
  var asm = Assembly.GetExecutingAssembly();
  var infoVer = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
  var versionString = !string.IsNullOrWhiteSpace(infoVer) 
      ? infoVer.Split('+')[0] 
      : asm.GetName().Version?.ToString(3);
  ```
- **Manifest Synchronization:** The Win32 [`app.manifest`](file:///c:/LogTailPro/src/LogTailPro/app.manifest) identity version must be kept synchronized with the project major/minor/build version.
- **Release Tag Alignment:** Release tags MUST match the `<Version>` in `.csproj` (e.g., tag `v2.10.0` requires `<Version>2.10.0</Version>`). The CI pipeline enforces this check and aborts if they differ.
- **CI / Master Builds:** Pushes to `master` automatically trigger GitHub Actions to run unit & integration tests, compile with WiX Toolset v5, stamp the build number (`2.10.<run_number>`), and publish the MSI installer release.

---

## 2. Performance & Low-Allocation Ingestion Rules

- **Background Ingestion:** All file tailing parsing, span slicing, and line generation (`BuildLogLines`) must execute on threadpool background workers (`Task.Run()`) — NEVER on the WPF UI thread.
- **Fast-Path Pattern Screening:**
  - Never execute regex matching across every line blindly.
  - Always use fast SIMD `IndexOf` or first-character prefix checks before invoking compiled regexes (e.g. in `TimestampParser`, `MultilineGroupingService`, `CorrelationService`, `ErrorTimelineChartService`).
- **Zero-Allocation File Streaming:**
  - Rent buffers from `ArrayPool<byte>.Shared` in `FileWatcherService`. Never allocate multi-megabyte byte arrays on the Large Object Heap (LOH).
  - Use `ReadOnlySpan<char>` slicing for line splitting instead of `string.Split()`.
- **$O(1)$ Line Trimming:**
  - When trimming historical buffer lines, never perform $O(N)$ containment checks (`_allLines.Contains(...)`).
  - Use index/line-number bounds checking (`parent.LineNumber < minLineNo`) for $O(1)$ pruning.
- **UI Virtualization:**
  - All ListViews, DataGrids, and ItemsControls displaying log entries or bookmarks must have `VirtualizingPanel.IsVirtualizing="True"` and `VirtualizationMode="Recycling"`.
- **Lazy String & Resource Caching:**
  - Cache formatted timestamp strings (`TimelineTimeDisplay`, `StructuredTimeDisplay`) and `Typeface` instances. Avoid per-frame allocations during rendering.

---

## 3. Tooling & SDK Environment

- **Target Framework:** `net9.0-windows7.0`, C# 12, WPF.
- **Local .NET 9 SDK:** Located at `installer\out\dotnet\dotnet.exe`.
- **Local Build Script:** Always use `.\scripts\build-run.ps1 -BuildOnly` to ensure the portable .NET 9 SDK is used rather than system fallback SDKs.
- **Test Suites:** Always run both `tools\Timeline.Tests\Timeline.Tests.csproj` and `tools\Timeline.IntegrationTests\Timeline.IntegrationTests.csproj` before pushing.
