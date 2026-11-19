# Gemini implementation brief for LogTailPro responsiveness

The user reports that LogTailPro becomes extremely slow during use. A local replay reproduced a UI freeze during file ingestion. The cause of the user's extended-session slowdown still needs a sustained replay. Work in the order below and report the impact of each change. Read the repository's `GEMINI.md` before implementing.

## What the investigation established

The replay used the existing local Windows build, a temporary file, the real file watcher and tab view model, and WPF log and Stats controls laid out without an on-screen window. Each trial began with 1,000 rows and appended 250,000 numbered plain-text INFO records in one write. The two timings in each cell are individual runs. Dispatcher time measures the longest callback during catch-up. Physical input and painting were outside this measurement.

| Stats binding | Longest dispatcher callback | Append to visible collection caught up |
| --- | --- | --- |
| Bound to the tab | 776 ms and 1,118 ms | 1.26 s and 1.69 s |
| Detached from the tab | 225 ms and 226 ms | 0.59 s and 0.63 s |

Each burst raised 250,000 notifications for `StatsInfoCount`. The saved settings used unlimited lines and a 1,024 MB initial load limit. In a separate one-tab catch-up run, live managed memory after forced GC measured 12 MiB at 26,000 retained rows, 54 MiB at 126,000, and 173 MiB at 426,000. These measurements establish history growth. Diagnosing a leak would require evidence of objects surviving after their owning tab or retained rows are released.

## 1. Establish a repeatable baseline

- Start with the WPF dispatcher harness in `tools/Timeline.IntegrationTests/Program.cs`. Use temporary files and test-local settings. Record the revision, build configuration, line format, and input rate for comparisons.
- Repeat the burst above with Stats bound and detached. Record total rows, emitted counter notifications, maximum dispatcher callback, and catch-up time. Also post a periodic input-priority callback from a background timer and measure its queue delay. Keep at most one probe outstanding.
- For the long-session case, replay a steady 1,000 numbered lines per second for 10 minutes. Compare unlimited history with a test-local 50,000-line limit. Sample every minute, stop the writer, then scroll, switch tabs, and search. Record written and displayed row numbers, pending work, input-delay p95 and maximum, CPU, GC pauses, and live heap. Compare early and late minutes at the same input rate. Take forced-GC measurements outside timed sections. Confirm the user-visible result with the window displayed.

## 2. Publish severity counters once per batch

In `src/LogTailPro/ViewModels/LogTabViewModel.cs`, `UpdateStats` increments observable properties for each root event. Accumulate error, warning, and info deltas in locals. Publish each changed counter once per applied batch. Keep line-rate accounting, continuation handling, and chart reconciliation consistent with existing behavior.

Add a deterministic check using a mixed batch containing INFO, WARN, ERROR, and continuation lines. Count notifications during application of that batch and verify both the final totals and at most one change per affected severity. Repeat the burst benchmark after this change alone.

## 3. Bound the work done in one UI turn

- Trace `FileWatcherService.ReadNewContent`, `Resume`, and `LogTabViewModel.OnNewLinesAvailable`. The 16 MiB limit bounds each read. The watcher accumulates the results of multiple reads into one delivery. Each delivery currently starts an independent parsing task and posts its entire batch to the dispatcher.
- Give each tab an ordered ingestion path. Keep file reads, decoding, and row building on a background worker. Size the handoff in bounded chunks and let the UI yield between slices. Start with an 8 ms processing budget per slice and tune using the input-delay measurements. Keep only one scheduled drain per tab. Arrange backpressure so a slow UI cannot accumulate unlimited parsed batches. A file producer can leave unread bytes in the file and resume from its committed offset. Account separately for process output, whose shared consumer cannot reread a file.
- Keep collection changes on the dispatcher. Coalesce status, scroll, and chart refresh requests across slices. Check visible-list capacity growth and collection reset frequency while tuning. Smaller slices can amplify both costs. Move the synchronous catch-up read out of the Resume command. Avoid waiting on the dispatcher while holding the watcher lock.
- Exercise a second append while the first is still parsing, a split line, pause/resume, rotation while work is queued, and closing a busy tab. Verify exact row order and identity, no missing or duplicate rows, correct multiline grouping across slices, plugin append delivery, and follow-tail behavior with filters active. Discard queued work belonging to an obsolete file generation after reload.

## 4. Address the extended-session cause shown by the replay

| Observation at a fixed input rate | Next change to evaluate |
| --- | --- |
| Written-to-displayed lag grows and drains after the writer stops | Profile the ordered ingestion path, including producer, parser, UI drain, and WPF layout. Tune the stage that falls behind. |
| Lag stays low but CPU or input delay increases with retained rows | Time the full chart snapshot and aggregation in `UpdateChartAsync`. Measure active and background tabs, including Stats-only consumers. If chart work dominates, update aggregates from additions and evictions and retain a rebuild path for reloads and bucket-boundary changes. |
| Ingestion drains but typing or searching remains slow | Profile `ApplySearchAsync` on the retained history. Check whether superseded searches continue scanning or mutating match flags. If confirmed, cancel inside long scans and apply results only for the current search and tab generation. |
| A finite limit is configured and rows survive eviction or tab closure | Inspect heap roots for the evicted rows and closed tabs. Check visible rows, search results, continuations, bookmarks, and queued callbacks. Verify intended owners before changing lifetimes. |

Preserve the user's unlimited-history setting during implementation. If sustained measurements point to the cost of keeping all history accessible, present the tradeoff between a bounded in-memory window and disk-backed access before changing retention behavior.

## Verification and handoff

Use these proposed targets for the baseline machine. Report actual results and any missed target. Keep timing thresholds out of ordinary CI assertions.

- For the 250,000-line burst, target a longest ingestion callback below 50 ms and input-probe p95 below 50 ms with Stats bound. Report end-to-end catch-up time alongside those figures so improved input latency cannot hide a growing backlog.
- During steady input, verify the consumer keeps pace in the final minute and reaches the writer's final sequence number after input stops. With finite retention, confirm that live memory stabilizes after the window fills. With unlimited history, report memory per retained row and whether input latency grows as the session ages.
- Build and run both repository test programs from the repository root.

```powershell
.\scripts\build-run.ps1 -BuildOnly
.\installer\out\dotnet\dotnet.exe run --project tools/Timeline.Tests/Timeline.Tests.csproj
.\installer\out\dotnet\dotnet.exe run --project tools/Timeline.IntegrationTests/Timeline.IntegrationTests.csproj
```

Deliver a before/after table for each implemented phase, the relevant code changes and regression checks, and a short account of any remaining slow scenario. Identify whether the result was reproduced in the displayed application, the dispatcher harness, or both.
