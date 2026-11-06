# Log investigation roadmap

Six new roadmap phases follow the existing Phases 1-7. Roadmap Phase 1 below means
the combined timeline, not the original core-viewer Phase 1. Later phases are planned.

| Phase | Feature | Status | Scope and acceptance criteria |
|---|---|---|---|
| 1 | Combined log timeline | Completed | Select open file sources; merge loaded lines chronologically; show timestamp/source/original line; live updates; navigate to originals; deterministic ordering; handle truncation, retention and source closure. |
| 2 | Structured JSON logs | Completed | Detect JSONL and Serilog-style events; timestamp/level/message/exception columns; nested property inspector; typed field filters; preserve raw and malformed input. Depends on Phase 1 source identity. |
| 3 | Request/Trace ID correlation | Completed | Right-click parsed or selected IDs; exact-value matching across chosen sources; chronological results and original navigation; cancellation and bounded results. Depends on Phases 1-2. |
| 4 | Saved workspaces | Completed | Extend existing workspace commands with timeline sources, highlights, filters, layout, active tab and scroll positions; versioned, atomic saves; backward compatibility and missing-file recovery. Depends on Phases 1-3. |
| 5 | Multiline exceptions | Completed | Expandable stack traces; original line ranges; search/copy/export include continuations; handle partial live events and rotation. Depends on Phases 1-2. |
| 6 | Error timeline chart | Completed | Severity counts in time buckets; click a range to filter; original navigation; unknown levels separate; bounded incremental aggregation. Depends on Phases 1-2 and 5 for final event-count semantics. |

## Shared rules

Use existing MVVM, text viewer, file watchers, search and highlights. Keep merging in
services, use background work/cancellation and virtualized views. Preserve source identity
independent of display names. Build and verify each phase before marking it complete.
Roadmap versions are independent of the MSI/application version.

## Phase 1 detailed design

Implemented in application version 1.4.0. Local Release build: zero warnings/errors.
Service checks cover ordering, offsets, unknown dates, duplicate filenames, reconciliation
and cancellation. Dispatcher-based integration checks use real files to verify live
appends, late timestamps, filters, pause/resume, retention, truncation, original navigation,
source closure and disposal. Actual XAML views are loaded and laid out in the check.
Manual desktop visual review and a large-file performance benchmark remain follow-ups.
The MSI workflow runs both check suites before packaging. Phases 2-6 remain planned.

Open at least two files, choose **Edit > Combined Timeline**, select sources, then view
a live merged tab. **Open original line** returns to the source tab and line.

- Parse full dates, fractional seconds and explicit UTC offsets. Normalize offsets to UTC;
  offset-free timestamps use the machine local zone. Never invent dates for time-only input.
- Unknown/time-only timestamps sort after dated records. Ties use source selection order
  and original line number. Explain this policy in the timeline banner.
- Snapshot loaded source lines on the UI thread; merge off-thread; publish batches.
  Coalesce live changes. Source filters must not affect the timeline.
- Reconcile truncation and retention, including removed records. Do not create duplicate watchers.
- Closing a source detaches it; closing the timeline cancels work and removes subscriptions.
- Include only loaded portions of files and surface source warnings. Preserve original raw text.
- Navigation must handle closed sources and lines removed by rotation without jumping elsewhere.

## Phase 2 detailed design

Implemented in Phase 2. Local Release build: zero warnings/errors.
Service checks in `Timeline.Tests` verify Serilog compact CLEF parsing (`@t`, `@l`, `@m`, `@mt`, `@x`),
default level "Information" when `@l` is missing in CLEF, rendered message vs template distinction,
nested object and array property preservation, malformed JSON handling, mixed plain text lines,
UTC offset ordering in combined timeline, and typed field filters (`==`, `!=`, `>`, `<`, `>=`, `<=`,
`exists`, `!exists`, nested dot-notation paths, and type-mismatch/missing semantics).
Dispatcher-based integration checks in `Timeline.IntegrationTests` verify real-file JSON loading,
structured Level/Timestamp columns layout, Property Inspector hierarchical tree, typed field filtering,
toggle between Structured View and Raw View, and live appends of JSON records.

- Parse line-delimited JSON (JSONL) and Serilog CLEF records off-thread in batches.
- Structured event model (`StructuredLogEvent`) preserves raw text, parsed timestamps with timezone offsets,
  normalized severity level, rendered message, template, exception, and native primitive types.
- Distinguishes rendered message `@m` from message template `@mt`. Does not substitute one for the other.
- Gracefully handles malformed JSON per line with error message, keeping tailing active without dropping records.
- Seamlessly integrates with the combined timeline (`TimelineService.ParseTimestamp`), ensuring accurate UTC chronological order across sources.
- Typed field filters (`TypedFieldFilter`) support syntax expressions (e.g. `StatusCode >= 400`, `exists(UserId)`, `Level == Error`) directly in the search bar or filter chain.
- Line Details / Property Inspector panel (`DetailsPanel.xaml`) provides header summary, hierarchical property tree (`LogPropertyNode`), exception viewer with copy button, and raw/pretty-printed text tab.
- Structured columns (`StructuredLevelColumn`, `StructuredTimeColumn`) and rendered message display toggleable via Toolbar toggle and context menu.

## Phase 3 detailed design

Implemented in Phase 3. Local Release build: zero warnings/errors.
Service checks in `Timeline.Tests` verify `MatchesCorrelationId` exact-value matching, case-insensitivity,
exclusion of substrings (e.g. `req-123` never matches `req-12345` or `pre-req-123`; `abc` never matches `abcdef`),
nested dictionary and list property paths, strict token boundary regex (`(?<![\w\-])` and `(?![\w\-])`),
suggested correlation ID extraction (`@tr`, `TraceId`, `RequestId`, key-value pairs, GUIDs, W3C trace IDs, prefixed tokens),
multi-source chronological merging in UTC with deterministic tie-breaking, bounding limits (default 50,000 matches with truncation notice),
and cancellation.
Dispatcher-based integration checks in `Timeline.IntegrationTests` verify multi-source correlation across real files (mixed raw and JSONL),
suggested ID extraction on selection, Correlate ID dialog workflow, correlation tab with dedicated source and UTC timestamp columns,
navigation to original source lines, live tail appends matching correlation IDs, and unmatching appends ignored.

- Correlation service (`CorrelationService.cs`) checks common trace/request fields (`TraceId`, `@tr`, `RequestId`, `CorrelationId`, `SpanId`, `TransactionId`), custom nested properties, and plain text with strict token boundaries.
- Never matches substring prefixes or suffixes; strips enclosing quotes when pasted.
- Scope selection dialog (`CorrelateIdDialog.xaml(.cs)`) lets users inspect and select from open log sources, with Select/Deselect All buttons and explicit notice that only loaded records are searched.
- Live correlation tab (`LogTabViewModel.Timeline.cs`) merges matching records chronologically in UTC, displays source and UTC timestamp columns, and notifies truncation limits in the tab banner.
- Right-click context menu in log tabs (`Correlate Request / Trace ID…`), Property Inspector header "Correlate ID" button on detected IDs, and Property Inspector tree item context menu ("Correlate by Value").
- Seamless navigation back to original line (`OpenOriginalLineCommand`), with fallback warnings if records rotated or trimmed.


## Phase 4 detailed design

Implemented in Phase 4. Local Release build: zero warnings/errors.
Service checks in Timeline.Tests verify WorkspaceService atomic saving (interrupted saves leave previous content completely intact),
schema versioning (v2 format with backward-compatible v1 upgrade), multi-tab round-trip (file tabs, timeline definitions, correlation views,
search patterns, filter modes, bookmarks, follow-tail, scroll positions, custom highlight rules, and AvalonDock layout XML),
missing files detection, and corrupt JSON error reporting.
Dispatcher-based integration checks in Timeline.IntegrationTests verify saving and loading full workspaces with active file tabs,
combined timeline, correlation view, active tab preservation by identity, restored scroll offsets and bookmarks, layout XML round-trip,
and non-destructive loading (corrupt workspaces do not clear or destroy open tabs).

- Workspace model (Workspace.cs, WorkspaceTab.cs) supports WorkspaceTabType (File, Timeline, Correlation), schema versioning, active document by path/name identity (ActiveTabId), source paths for dependent views, and AvalonDock layout XML.
- WorkspaceService handles atomic saves via safe temp-file replacement, preventing workspace corruption if interrupted.
- Backward compatibility: legacy v1 workspaces load seamlessly, automatically upgrading to schema 2.
- Restoration order: restores underlying file sources first, then resolves and builds dependent timelines and correlation tabs without creating duplicate file watchers.
- Active tab selection prioritizes document identity rather than naive index to prevent misselection when sources are missing or reordered.
- Missing file recovery: missing files are identified and surfaced in StatusMessage while valid files and remaining tabs load cleanly.
- Non-destructive: corrupt JSON or completely missing workspaces cancel loading without wiping out current user work.


## Phase 5 detailed design

Implemented in Phase 5. Local Release build: zero warnings/errors.
Service checks in Timeline.Tests verify MultilineGroupingService (.NET stack frames, nested exceptions --->,
inner exception boundaries --- End of ..., Java exceptions Caused by: / omitted frames, Python tracebacks File "...",
blank line preservation within traces, generic indented lines not misclassified, final event grouping,
mid-stream start partial continuation marking, live appending via AttachLine, and correlation ID extraction across continuation frames).
Dispatcher-based integration checks in Timeline.IntegrationTests verify real-file exception loading, collapsed view hiding
continuation lines by default, expand/collapse toggling (â–¶ / â–¼), badge formatting (+N frames), search and filter
matching inside hidden continuation frames, plain line view toggling, live appending of stack frames to open exception roots,
finalization upon arrival of subsequent normal records, and Property Inspector extraction.

- MultilineGroupingService detects .NET stack frames (^\s+at\s+[\w\.\<\>\$]+\s*\(.*?\)), inner exceptions/boundaries (--- End of, ---> System.*Exception:), Java exception frames (Caused by:, ... common frames omitted), and Python tracebacks (File "...", During handling of..., Traceback), preserving blank lines within traces and avoiding misclassification of generic indented text.
- LogLine model extended with Continuations, HasContinuations, IsExpanded, ExpanderGlyph (â–¶ / â–¼), ContinuationBadge (+N frames), IsContinuation, GroupParent, and IsPartialContinuation.
- Virtualized list view renders collapsed exception roots with interactive expander button and frame count badge.
- View menu and Toolbar toggle allow switching between Group Exceptions view and Plain Line View, with Expand All / Collapse All commands.
- Search and typed filters evaluate continuation frames so searching for terms in hidden stack traces matches and reveals the root event.
- Correlation service inspects continuation frames for suggested trace IDs and matches correlation queries across hidden lines.
- Copy commands ("Copy selected lines" and "Copy with Line Numbers") include all continuation lines when copying collapsed exceptions.
- Line Details / Property Inspector displays complete formatted multiline stack traces in the exception viewer.
- Live appending attaches incoming stack frames to the active group and finalizes the event upon arrival of subsequent normal log records.

## Phase 6 detailed design

Implemented in Phase 6. Local Release build: zero warnings/errors.
Service checks in Timeline.Tests verify ErrorTimelineChartService (severity normalization for both Serilog CLEF and plain text,
multiline grouping semantics preventing inflated event counts, adaptive time bucketing with known event distributions,
separate counting for undated events without invented timestamps, large data duration bounding, and cancellation).
Dispatcher-based integration checks in Timeline.IntegrationTests verify real-file chart loading, adaptive bucket generation,
severity totals matching source events, single bucket click and drag selection activating time-range filters,
time filter restricting visible lines, filter clearing restoring all lines, live append updating error counts in real time,
and error timeline chart view layout and rendering without errors.

- Model and view-model: TimeBucket.cs defines time intervals, normalized severity counts (Error, Warning, Info, Other), proportional bar heights, tooltips, and selection state. ErrorTimelineChartViewModel.cs binds data, supports range selection, filter clearing, drill-down navigation, and keyboard accessibility.
- Aggregation service: ErrorTimelineChartService.cs normalizes severity levels across structured and plain text logs, calculates adaptive time bucket intervals, bounds bucket counts (<= 120) across any time span, and counts distinct root events (!line.IsContinuation) without inflating counts for multiline continuation frames.
- Undated events: records without recognized timestamps are counted separately (UndatedCount, UndatedErrors, UndatedWarnings, etc.) and displayed in an informative badge rather than plotted with invented timestamps.
- Interactive filtering: users can click an individual bucket or click-and-drag across buckets to select a time range. The selected time range filters VisibleLines in combination with existing search and filter rules (AND semantics).
- Anti-feedback loop: the chart aggregates the source stream (_allLines), so filtering VisibleLines highlights the selected time range without shrinking or recalculating the chart itself. Clear / reset button restores the unfiltered view immediately.
- Drill-down: double-clicking or selecting a bucket navigates directly to the first event at or after the bucket timestamp.
- Incremental updates: background processing off the UI thread updates the chart on file loads, live appends, and retention trimming with cancellation tokens.
- Native WPF vector rendering: ErrorTimelineChartCanvas uses high-performance direct drawing with design tokens (StatusRed, StatusYellow, Accent, BgSurface), selection marquee, hover tooltips, and keyboard navigation (Left/Right, Enter/Space, Escape).
- AvalonDock & Toolbar integration: docked in the bottom pane alongside search and results, toggleable via Toolbar button ("ðŸ“Š  Chart") and View â–¸ Panels menu (Ctrl+Shift+T).