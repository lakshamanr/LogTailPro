# Plan Changelog

Claude Code must read this file BEFORE starting any phase.
If the version here is newer than what you last built, re-read the affected phase files.

---

## v1.5 - 2026-05-18 (CURRENT) — FINAL RELEASE

### Changes
- **Logo:** New SVG logo (`Assets/logo.svg`) — dark navy background, cyan log-lines, amber tail arrow
- **Icon:** Regenerated `Assets/app.ico` — 16/32/48/256 px, PNG-in-ICO, matches design system colors
- **UI fix (v1.4):** Menu text color bugs resolved — no hardcoded colors on `<Menu>`, both themes have full `ControlTemplate` with `TextElement.Foreground="{TemplateBinding Foreground}"`
- **Build:** 0 errors, 0 warnings (`dotnet build -c Release`)
- **Publish:** Single self-contained EXE → `publish/LogTailPro.exe` (67.6 MB, win-x64)

### Build commands used
```
dotnet build src/LogTailPro/LogTailPro.csproj -c Release
dotnet publish src/LogTailPro/LogTailPro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish
```

---

## v1.4 - 2026-05-18
**Bug Report: Menu text color wrong in both themes**
**Affects:** `Themes/Dark.xaml`, `Themes/Light.xaml`, `MainWindow.xaml`

### Bug Description
- **Dark theme:** menu text shows as black/near-black — should be `#D4D4D4` (light gray / white)
- **Light theme:** menu text shows as wrong color — should be `#1A1A1A` (near-black / dark)

### Root Causes (3 issues, all theme/UI code only)

**Cause 1 — `MainWindow.xaml` line 147–149:**
The `<Menu>` element has hardcoded local values:
```xml
Background="#252535"  Foreground="#D4D4D4"  BorderBrush="#444455"
```
Local values on an element always win over `Style` setters. This permanently locks
the menu bar to dark-theme colors regardless of which theme is active. Hardcoded
`Foreground="#D4D4D4"` flows into `MenuItem` children via WPF's inherited property system.

**Cause 2 — `Dark.xaml` `MenuItem` template (line ~97):**
The custom `ControlTemplate` uses `ContentPresenter ContentSource="Header"` without
an explicit `TextElement.Foreground="{TemplateBinding Foreground}"` binding. WPF's
`ContentPresenter` does not reliably inherit foreground from the template host in this
context; an explicit `TemplateBinding` is required to guarantee the text color.

**Cause 3 — `Light.xaml` has no `MenuItem` custom template:**
`Light.xaml` sets `MenuItem.Foreground` via a style setter but has no `ControlTemplate`.
WPF falls back to the system Aero2 template which internally uses `SystemColors.MenuTextBrush`
and ignores the `Foreground` style setter entirely. Menu text color is therefore
determined by Windows system settings, not by the theme.

### Fix Plan (theme/UI code only — 3 steps)

| Step | File | Change |
|---|---|---|
| 1 | `MainWindow.xaml` | Remove hardcoded `Background`, `Foreground`, `BorderBrush` from `<Menu>`; let the theme `Style TargetType="Menu"` control all three via named tokens |
| 2 | `Dark.xaml` | Add `TextElement.Foreground="{TemplateBinding Foreground}"` to the `ContentPresenter` inside the `MenuItem` `ControlTemplate` |
| 3 | `Light.xaml` | Add the same full `ControlTemplate` to the `MenuItem` style (mirrors Dark.xaml); `Foreground` is already set to `TextPrimary=#1A1A1A` — the template just needs to bind to it |

No ViewModels, Services, Models, or business logic are touched.

---

## v1.3 - 2025-05-18
**Affects:** UI_AGENT.md (new file), prompts/UI_FIX.md (new file), CLAUDE.md

New files added — copy them into your project folder:
- UI_AGENT.md: full WPF design system with colour tokens, component specs,
  anti-patterns list, and a 30-item visual QA checklist
- prompts/UI_FIX.md: step-by-step UI overhaul agent prompt

Changes to CLAUDE.md:
- Added Special Commands table (start / fix the UI / continue / status / the plan has changed)
- Added git workflow with exact commit messages per phase
- Updated Golden Rules: rule 6 now requires git commit after every phase

To fix UI: open Claude Code, type "fix the UI"
Claude will read UI_AGENT.md + prompts/UI_FIX.md and overhaul all XAML files.

---

## v1.2 - 2025-05-17
**Affects:** PHASE_2, PHASE_4, PHASE_5, PHASE_6, SKILL.md

Changes made after reviewing the official BareTailPro website:

- PHASE_2: Added search results TABLE (Timestamp | Line# | Text | Capturing Groups columns, sortable)
- PHASE_2: Added regex capturing groups as individual columns in results table
- PHASE_2: Added ArrivedAt timestamp on each log line (when it arrived from watcher)
- PHASE_2: Added saved/named search patterns (save, name, load, delete)
- PHASE_4: Added tab strip positioning (top/bottom/left/right)
- PHASE_4: Added full command line argument support (-wp, -ws, -tc, -ti, file args)
- PHASE_4: Added window tiling support (--tile-window-count / --tile-window-index)
- PHASE_5: Added TAB expansion (configurable tab width, default 8 spaces)
- PHASE_5: Added IIS log format support (null-terminated lines)
- PHASE_5: Added network file support (UNC paths) with disconnect/retry
- PHASE_5: Added font line spacing and character offset settings
- PHASE_5: Added preferences storage mode (file / registry / do not save)
- PHASE_5: Added load/save preferences on demand (share config with teammates)
- PHASE_6: Replaced simple export with full column-configurable export dialog
           (per-column: prefix, suffix, width, alignment, truncation, decimal align)

---

## v1.1 - 2025-05-17
**Affects:** start.ps1, CLAUDE.md, SKILL.md, PHASE_1.md

- Fixed .NET version: upgraded from net8.0-windows to net10.0-windows
- Fixed start.ps1: version check now accepts .NET 8 or higher

---

## v1.0 - 2025-05-17
Initial plan created.
