# Log investigation roadmap

Six new roadmap phases follow the existing Phases 1-7. Roadmap Phase 1 below means
the combined timeline, not the original core-viewer Phase 1. Later phases are planned.

| Phase | Feature | Scope and acceptance criteria |
|---|---|---|
| 1 | Combined log timeline | Select open file sources; merge loaded lines chronologically; show timestamp/source/original line; live updates; navigate to originals; deterministic ordering; handle truncation, retention and source closure. |
| 2 | Structured JSON logs | Detect JSONL and Serilog-style events; timestamp/level/message/exception columns; nested property inspector; typed field filters; preserve raw and malformed input. Depends on Phase 1 source identity. |
| 3 | Request/Trace ID correlation | Right-click parsed or selected IDs; exact-value matching across chosen sources; chronological results and original navigation; cancellation and bounded results. Depends on Phases 1-2. |
| 4 | Saved workspaces | Extend existing workspace commands with timeline sources, highlights, filters, layout, active tab and scroll positions; versioned, atomic saves; backward compatibility and missing-file recovery. Depends on Phases 1-3. |
| 5 | Multiline exceptions | Expandable stack traces; original line ranges; search/copy/export include continuations; handle partial live events and rotation. Depends on Phases 1-2. |
| 6 | Error timeline chart | Severity counts in time buckets; click a range to filter; original navigation; unknown levels separate; bounded incremental aggregation. Depends on Phases 1-2 and 5 for final event-count semantics. |

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

### Verification checklist

- Interleaved timestamps, equal timestamps, duplicate filenames and explicit offsets.
- Missing/time-only timestamps, malformed dates and fractional seconds.
- Late arrivals, append batches, truncation, retention and source closure.
- Independent timeline search/filter/highlighting and original-line navigation.
- Closing during background merges; bounded/coalesced work; virtualized rendering.
- Release build and MSI build workflow pass. Record any manual UI checks still outstanding.
