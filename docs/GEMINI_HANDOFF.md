# Gemini handoff: remaining LogTailPro features

Prepared 2026-11-06. This is a local handoff document, not a request to rewrite history
or recreate completed features. Read the code and confirm current state before editing.

## User objective

Implement the remaining five phases from `prompts/ROADMAP.md`, in order. The user
approved the roadmap and implementation of its first feature; that feature is now
implemented. The user now wants a handoff for the remaining feature implementations.
Keep each phase independently reviewable, built and tested. Do not claim a feature is
complete based only on adding UI controls or passing compilation.

## Workspace and repository

- Windows / PowerShell, workspace `C:\LogTailPro`.
- Private repository: https://github.com/lakshamanr/LogTailPro
- Branch: `master`, tracking `origin/master`.
- Last pushed commit when this document was prepared: `0f981da`.
- Working tree was clean before creating this document. This handoff is intentionally
  local/uncommitted; committing it would trigger another build and release.
- Read `CHANGELOG.md`, `prompts/ROADMAP.md`, `CLAUDE.md` and
  `SKILL/technical skill/SKILL.md` for project context. Some older documentation says
  .NET 10; the actual project currently targets `net9.0-windows7.0`, C# 12, WPF.
- MVVM with CommunityToolkit.Mvvm; Newtonsoft.Json; AvalonDock docking.
- Do not edit generated `bin`, `obj`, `publish` or installer output. Some historical
  `obj` files are tracked despite ignore rules; avoid committing build-generated changes.

## Completed roadmap Phase 1: combined live timeline

This is distinct from the original `prompts/PHASE_1.md` core-viewer phase.

- App project version: `1.4.0`. Published release `v1.4.0` contains its MSI.
- Open two file tabs, choose **Edit > Combined Timeline**, select sources, create view.
- Dedicated source and UTC timestamp columns; original line identity/path preserved.
- Reuses source tabs/watchers. Source filters do not affect the timeline.
- Live changes are coalesced; snapshots merged off-thread; retention/truncation reconciled.
- Unknown/time-only timestamps sort last; offset-free full dates use local timezone.
- Right-click selected row > Open original line. Closed/trimmed originals report unavailable;
  hidden originals require clearing source filters.
- Pause freezes the timeline; Resume catches up. Source closure removes that source;
  timeline closure detaches subscriptions without stopping sources.

Important files:

- `Services/TimelineService.cs`: snapshot merge, UTC parsing, stable tie-breaking.
- `ViewModels/LogTabViewModel.Timeline.cs`: subscriptions, timer, lifecycle, navigation.
- `Models/LogLine.cs`: source identity, original-line reference, timeline timestamp.
- `ViewModels/MainViewModel.cs`: MergeTabs command and original navigation.
- `Views/TimelineSourcesDialog.xaml(.cs)` and `ViewModels/TimelineSourceChoice.cs`: picker.
- `Views/LogTabView.xaml(.cs)`: source/time columns, context action, sizing.

Local Release build passed with zero warnings/errors. Both test suites passed locally
and in GitHub Actions. MSI artifact build for commit `179c15d` passed:
https://github.com/lakshamanr/LogTailPro/actions/runs/42202471393

Phase 1 review items to consider while extending it:

- Manual desktop visual review and large-file performance benchmarking remain outstanding.
- Merges currently clone and sort the loaded snapshot for each coalesced update. Benchmark
  before choosing caching/indexing or incremental merge algorithms for later phases.
- Timeline bookmark behavior deserves a regression check: rows are cloned on refresh, so
  bookmark state changed only on a timeline clone may not survive the next rebuild.
- `GridViewColumn` is outside the visual tree: ancestor-based width bindings failed in
  testing. Column sizing is intentionally handled by view-only code in `UpdateWrapLayout`.

## Roadmap Phase 2: structured JSON logs — next implementation

Outcome: JSON records become browsable/filterable events while raw text is always available.

1. Inspect existing `LogParserService`, `LogParserViewModel`, parser configuration,
   `TimestampParser`, `LogLine` and the current search/filter pipeline. Extend reusable
   behavior rather than creating a second inconsistent parser.
2. Add an event model preserving source/path, original line or range, raw text,
   timestamp including offset, severity, message, exception and custom properties.
3. Support line-delimited JSON and common Serilog-style fields, including compact
   `@t`, `@l`, `@m`, `@mt`, `@x` as appropriate. Clearly distinguish rendered message
   from message template. Do not silently substitute one for the other.
4. Preserve property types and nested arrays/objects. Add a property inspector and
   timestamp/level/message columns with an easy raw-text view.
5. Add typed field filters (exact string, numeric comparisons, existence); define
   missing/null/type-mismatch semantics and keep ordinary text/regex search working.
6. Handle malformed JSON per record without dropping it or stopping live tailing.
   Protect against excessive nesting/size; handle unfinished live lines safely.
7. Parse off-thread in batches; avoid reparsing unchanged records on every render or search.

Acceptance: valid JSONL, compact/regular Serilog-style records, nested/custom values,
malformed records and mixed plain text; correct timezone ordering in combined timeline;
typed filtering independent from raw search; live appends without duplicates; source
navigation still points at the original record. No cloud service is required.

## Roadmap Phase 3: Request/Trace ID correlation

Depends on Phase 2 event/property model and Phase 1 source identity.

- Right-click a parsed ID or selected text and choose a correlation search action.
- Offer scope selection among open sources. Explain that only loaded records are searched.
- Recognize configured/common request, trace and correlation fields; preserve exact values.
  Avoid substring matches (`abc` must not match `abcdef`) and regex interpretation by default.
- Search plain-text records too, with an explicit boundary/extraction policy.
- Show matching events chronologically, with source and original navigation.
- Cancel stale searches, bound result rendering and show truncation/limit notices.
- Reuse parsed data and consider an index based on profiling, not a duplicate full-file load.

Acceptance: same ID across multiple sources; similar-but-different IDs excluded; nested
field values; missing IDs; repeated events; cancellation; rotation/closure; source navigation.
Do not require a tracing backend or send log content to external services.

## Roadmap Phase 4: complete saved workspaces

Workspace support ALREADY EXISTS in `MainViewModel.SaveWorkspaceAsync` /
`LoadWorkspaceAsync`, with `.ltpw` models. AvalonDock layout persistence also exists.

- Inspect existing workspace models and `DockLayoutService` first.
- Extend format with schema version, timeline definitions/source identities, tab order,
  active document, filters/search options, workspace-specific highlights, scroll positions,
  follow-tail state, bookmarks as appropriate, and docking layout.
- Save source descriptions, not entire logs or runtime object references.
- Use asynchronous, atomic saves through a service. Preserve old workspace compatibility.
- Restore file sources before dependent timelines/correlation views. Avoid index-based
  active-tab mistakes when missing/unrestorable sources are skipped.
- Recover from missing files, invalid layouts and partially invalid entries with a clear
  report and a usable workspace. Do not persist or relaunch process commands implicitly.

Acceptance: round-trip a multi-file timeline workspace; retain filters/highlights/layout;
restore active tab and scroll when not following; load old format; missing files and corrupt
JSON do not destroy current work; interrupted save leaves the previous file intact.

## Roadmap Phase 5: multiline exception grouping

Depends on Phase 2 event model; update Phase 1 timeline without losing original ranges.

- Add configurable/common event-start detection and continuation grouping for .NET and
  other stack traces. Do not classify every indented message as an exception automatically.
- Represent each event with start/end source positions and all raw continuation lines.
- Expand/collapse events in a virtualized view; preserve a plain line view.
- Search, correlation, copy and export must include hidden continuation content.
- Handle a partially written event: finalize or extend it consistently as new lines arrive.
- Reset grouping at source rotation/truncation; never merge records from different files.
- Reconcile retention boundaries and show when the beginning of an event was not loaded.

Acceptance: nested exceptions, blank lines, stack traces without timestamps, final event
without a successor, mixed ordinary records, append-in-progress, truncation/rotation,
collapsed-content search, source-range navigation and complete copy/export.

## Roadmap Phase 6: interactive error timeline chart

Depends on timestamps/severity from Phase 2 and final event-count semantics from Phase 5.

- Aggregate events into adaptive time buckets, with Error/Warning/Info/other/unknown
  handled explicitly. Count grouped events rather than their stack-trace lines.
- Show source scope, timezone, time range and bucket size. Do not invent timestamps
  for undated records; display their separate count.
- Click/drag a time range to filter the event view; offer clear/reset. State whether this
  combines with existing filters; prevent feedback loops between chart and list.
- Drill into source events. Live append, late timestamps, retention and rotation update
  the chart without recomputing/rendering every point on the UI thread.
- Keep bucket count bounded, preserve keyboard accessibility and theme resources.
- Choose a chart dependency only after inspecting existing dependencies and licensing;
  simple native WPF drawing may be sufficient. No cloud dashboard is necessary.

Acceptance: known bucket counts; severity normalization; time-range boundary semantics;
timezone handling; undated events; late arrivals; source filters; reset; large data bounds;
original navigation; grouping does not inflate counts.

## Local verification commands

The machine's default dotnet was SDK 8. An isolated SDK 9 was installed under ignored
`installer/out/dotnet`. It may not exist on another machine; install an appropriate SDK.

```powershell
dotnet run --project tools/Timeline.Tests/Timeline.Tests.csproj -c Release
& .\installer\out\dotnet\dotnet.exe build src/LogTailPro/LogTailPro.csproj -c Release -r win-x64
& .\installer\out\dotnet\dotnet.exe run --project tools/Timeline.IntegrationTests/Timeline.IntegrationTests.csproj -c Release -r win-x64 --self-contained true
```

The service suite currently targets net8 and links the pure models/service. The integration
suite targets net9 Windows/WPF and references the application, so Release runs need
`--self-contained true`. It exercises real file appends, source filters, timeline filters,
pause/resume, late arrivals, retention, truncation, navigation, closing and disposal.
It loads/layouts the actual XAML views and verifies timeline column visibility across tab switches.
Extend tests for meaningful new behaviors rather than only mirroring implementation.

## Automatic build/release: latest change needs final verification

User explicitly wants a build AND a release after pushes to `master`.

- `.github/workflows/build-msi.yml` runs tests, installs pinned WiX 5.0.2/extensions,
  builds the MSI, uploads an artifact and publishes a release for `master`.
- Other branches build artifacts without releases. Explicit version tags are still supported.
- Master MSI version is project major/minor + workflow run number. Project `1.4.0`,
  run 8 => release `v1.4.8`, installer `LogTailPro-1.4.8-x64.msi`.
- `installer/build-msi.ps1` accepts optional `-Version`; CI passes it into both EXE
  and MSI version. The project file itself is not automatically edited.
- Reruns reuse their release/asset and check that an existing tag points to the same commit.
- An older run finishing after master advanced is not marked Latest.
- GITHUB_TOKEN-created release tags do not recursively trigger another workflow.
- A local commit must be pushed to GitHub to trigger this. One multi-commit push builds
  its final head, not an independent release for every intermediate commit in that push.

At handoff, the FIRST master-auto-release run was still in progress:

- Commit: `0f981daf5ccc220445072a679643bb7764968a0e`
- Run: https://github.com/lakshamanr/LogTailPro/actions/runs/42205436250
- Run number: 8; expected release `v1.4.8`.
- Tests, SDK/WiX setup and version selection passed; MSI build was running.
- **First follow-up:** verify Publish release succeeded and the release is non-draft
  with a nonempty uploaded MSI asset and the correct commit tag. Do not claim this
  latest automation is verified until checking the actual outcome.

Previously verified published release:
https://github.com/lakshamanr/LogTailPro/releases/tag/v1.4.0

The Packages sidebar is unrelated: MSI downloads belong under Releases > Assets.
The user has repeatedly asked to see the installer there, so verify release assets,
not just an Actions artifact or a successful compile.

## Other session context

The old `lakshamanrokade1991` author attribution was already corrected to `lakshamanr`
with explicit user authorization and backups. GitHub's contributor API returned only
`lakshamanr` with 43 commits at that time. A cached sidebar remained. The user said to
ignore it. Do not rewrite history again. Claude co-author trailers remain intentionally.
Backups are in ignored `installer/out/*.bundle`; do not upload these as product assets.

## Suggested execution sequence

1. Verify current git status and the in-flight release result.
2. Read the current roadmap and inspect existing parser/event/search abstractions.
3. Implement Phase 2 end to end, preserving Phase 1 behavior and existing raw logs.
4. Run relevant tests, build Release, inspect actual UI, then commit/push authorized work.
5. Confirm the master workflow publishes an installer under Releases.
6. Update roadmap/changelog with evidence and outstanding limits. Continue to later phases
   when authorized by the user's instructions; do not silently expand scope beyond them.
