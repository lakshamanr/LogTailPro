# Plan Changelog

Claude Code must read this file BEFORE starting any phase.
If the version here is newer than what you last built, re-read the affected phase files.

---

## v1.2 - 2025-05-17 (CURRENT)
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
