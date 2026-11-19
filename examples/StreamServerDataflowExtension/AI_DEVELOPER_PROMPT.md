# AI Developer Prompt: Build the Stream Server Dataflow Extension

Use the master prompt below in Claude (or any AI assistant) to generate or enhance this independent plugin project.

---

```markdown
### AI INSTRUCTION: BUILD AN INDEPENDENT LOGTAILPRO PLUGIN

You are an expert .NET 9, C# 12, and WPF software architect.
Your task is to build a complete, production-grade independent plugin project for **LogTailPro** named:
**`StreamServerDataflowExtension`** (Stream Server & Telephony Dataflow Studio).

---

### 1. Architectural Rules & Requirements
1. **Target Framework:** `net9.0-windows7.0`, WPF enabled, C# 12, nullable enabled.
2. **Decoupled Architecture:** This project lives in its own repository/directory. It references `LogTailPro.PluginContract.dll`.
3. **Out-of-Process / Modeless UI:** Because analyzing gigabytes of streaming logs is computationally heavy, the plugin must open its visualizer in its own dedicated, modern Dark-Themed WPF Window without blocking LogTailPro's UI thread.
4. **Performance:** Use `ReadOnlySpan<char>` and streaming reader for parsing large log files.

---

### 2. Plugin Manifest (`plugin.json`)
The output directory must contain a `plugin.json` file:
```json
{
  "id": "com.logtailpro.streamserver.dataflow",
  "name": "Stream Server Dataflow",
  "version": "1.0.0",
  "author": "Telephony Systems Team",
  "description": "Visualizes end-to-end SIP signaling, RTP media streaming, and starvation diagnostics.",
  "entryAssembly": "StreamServerDataflowExtension.dll",
  "entryClass": "StreamServerDataflowExtension.StreamDataflowPlugin",
  "minLogTailProVersion": "2.10.0",
  "menu": {
    "header": "Stream Server Dataflow",
    "items": [
      {
        "id": "open_visualizer",
        "header": "Open Stream Dataflow Visualizer…",
        "shortcut": "Ctrl+Shift+D"
      },
      {
        "id": "analyze_current_call",
        "header": "Analyze Selected Call Session"
      },
      {
        "id": "export_report",
        "header": "Export Stream Incident Report…",
        "separatorBefore": true
      }
    ]
  }
}
```

---

### 3. Core Capabilities to Implement

#### A. Multi-Dimensional Log Parser & Session Extractor
The log files have the following format:
`DD/MM/YY HH:mm:ss.fff | LEVEL | THREAD | LOGGER | MESSAGE`
Parse and group lines into **Call Sessions**:
- Extract Call GUIDs: `Call Identifier: <GUID>`
- Extract Session Handles: `SessionHandle: <ID>`
- Extract Paired Media Handles: `handleId = <Rx>` and `handleId = <Tx>`
- Map Logger names (`SipConnLayer`, `SessionHandler`, `RealTimeArpCommunicator`, `CacheDownloadHandler`, `ERSServerBase`, `ERSMedia`, `EngageStorageCenterDb`, `PBAdminServer`).

#### B. 6D Flow Diagnostics Engine
Implement automated detection for:
1. **SIP TLS Handshake Failure:** Status `FP_NOT_SECURED_CONNECTION` in `SipConnLayer`.
2. **Cache Starvation Timeout:** 5,000ms silent freeze between last packet and `The get timed out - waiting on an empty cache` after `DataIsTemporarilyUnavailable`.
3. **Dual-Thread Teardown Race:** Handle finalized twice or teardown on null handle (`status IllegalState`).
4. **Unclosed SIP Sessions:** `FinalizeStackMgr - There are N sessions which haven't closed yet`.
5. **Database Handshake Outage:** `SQL Provider error: 0 - The handle specified is invalid`.
6. **High-Frequency Poll Inundation:** Thousands of `queue isnt empty` warnings.

#### C. WPF Diagnostic Studio Window (`StudioMainWindow.xaml`)
Design a modern Fluent Dark Theme window containing:
1. **Session Picker Toolbar:** Dropdown list of discovered Call GUIDs with health badges (`Healthy`, `Starved`, `TLS Handshake Failed`, `Orphaned`).
2. **Call Flow Sequence Waterfall Canvas:** Interactive visual diagram showing:
   `PBX -> SipConnLayer -> SessionHandler -> ERS -> CacheDownloadHandler -> Client`
   Highlight the exact failure point in red with the log line number.
3. **Dual-Channel Audio Oscilloscope:**
   Side-by-side packet arrival rate curves for `Rx` (Agent) and `Tx` (Customer), highlighting the 5,000ms flatline starvation gap.
4. **Anomaly Radar Cards:**
   Displays Root Cause, Impact, and Remediation advice for each detected issue.
5. **Time Scrubber Bar:**
   Slider to step through the call's timeline millisecond-by-millisecond.
6. **Navigate Back to LogTailPro:** Clicking any step in the visual waterfall calls `host.NavigateToLine(lineNumber)` to jump LogTailPro directly to the corresponding log row.

---

### 4. Required Project Files to Output
Please generate all required source code files:
1. `StreamServerDataflowExtension.csproj`
2. `plugin.json`
3. `StreamDataflowPlugin.cs` (implements `ILogTailPlugin`)
4. Models: `CallSession.cs`, `FlowStep.cs`, `DiagnosticAnomaly.cs`, `CallState.cs`
5. Engine: `StreamLogParser.cs`, `FlowDiagnosticEngine.cs`
6. Views: `StudioMainWindow.xaml` and `StudioMainWindow.xaml.cs`
7. Controls: `FlowWaterfallCanvas.xaml(.cs)` and `AudioOscilloscopeControl.xaml(.cs)`
```
