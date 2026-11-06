# Log investigation roadmap

Six new roadmap phases follow the existing Phases 1-7. Roadmap Phase 1 below means
the combined timeline, not the original core-viewer Phase 1. Later phases are planned.

| Phase | Feature | Status | Scope and acceptance criteria |
|---|---|---|---|
| 1 | Combined log timeline | Completed | Select open file sources; merge loaded lines chronologically; show timestamp/source/original line; live updates; navigate to originals; deterministic ordering; handle truncation, retention and source closure. |
| 2 | Structured JSON logs | Completed | Detect JSONL and Serilog-style events; timestamp/level/message/exception columns; nested property inspector; typed field filters; preserve raw and malformed input. Depends on Phase 1 source identity. |
| 3 | Request/Trace ID correlation | Completed | Right-click parsed or selected IDs; exact-value matching across chosen sources; chronological results and original navigation; cancellation and bounded results. Depends on Phases 1-2. |
| 4 | Saved workspaces | Completed | Extend existing workspace commands with timeline sources, highlights, filters, layout, active tab and scroll positions; versioned, atomic saves; backward compatibility and missing-file recovery. Depends on Phases 1-3. |
| 5 | Multiline exceptions | Planned | Expandable stack traces; original line ranges; search/copy/export include continuations; handle partial live events and rotation. Depends on Phases 1-2. |
| 6 | Error timeline chart | Planned | Severity counts in time buckets; click a range to filter; original navigation; unknown levels separate; bounded incremental aggregation. Depends on Phases 1-2 and 5 for final event-count semantics. |

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
