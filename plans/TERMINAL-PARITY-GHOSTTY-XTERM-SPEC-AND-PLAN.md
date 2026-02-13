# Terminal Parity Spec And Execution Plan (Ghostty + xterm.js)

## Scope

This document defines parity targets for terminal behavior in:

- `src/XamlVisualEditor.Terminal`
- `src/XamlVisualEditor.Terminal.Avalonia`
- `src/XamlVisualEditor.Shell.ViewModels`

The focus is: window sizing, scroll behavior, viewport/scrollback behavior, and VT command handling for modern TUIs.

## Upstream References (Analyzed)

Cloned to local temp workspace:

- Ghostty: `/tmp/terminal-parity/ghostty` @ `c846174`
- xterm.js: `/tmp/terminal-parity/xterm.js` @ `2f66b5f`

Key source references used:

- Ghostty
  - `src/termio/stream_handler.zig`
  - `src/terminal/stream.zig`
  - `src/terminal/Terminal.zig`
  - `src/terminal/Screen.zig`
  - `src/terminal/modes.zig`
- xterm.js
  - `src/common/InputHandler.ts`
  - `src/common/buffer/Buffer.ts`
  - `src/common/buffer/BufferSet.ts`

## Behavioral Baseline (Required Parity)

### 1. Buffer and Resize Model

- Normal and alternate buffers are separate.
- Alternate buffer has no scrollback and no resize reflow.
- Normal buffer may reflow on resize.
- Resize must keep cursor/selection stable and clamp into legal bounds.

### 2. VT Parser Fidelity

- CSI parser must handle:
  - private prefixes (`?`, `>`, `<`, `=`, legacy `!`)
  - intermediates (e.g. `SP`, `'`)
  - sub-parameter separators (`:`), especially for SGR extended color sequences

### 3. Scroll/Region Operations

- DECSTBM and DECSLRM semantics must be respected.
- Region-scoped ops must not corrupt outside-region cells:
  - IL/DL, SU/SD, ICH/DCH/ECH
  - SL/SR (CSI Ps SP @ / CSI Ps SP A)
  - DECIC/DECDC (CSI Ps ' } / CSI Ps ' ~)

### 4. Cursor/DSR/Mode Semantics

- CPR should respect origin mode when reporting coordinates.
- DEC DSR (`CSI ? 6 n`) should emit `CSI ? r ; c R`.
- DECSCUSR (`CSI Ps SP q`) should control cursor shape/blink.
- DECCOLM should be gated by mode 40 (`allow 80/132`) and then resize+clear+home.

### 5. Window Ops

- Support common xterm window ops:
  - `CSI 14 t` pixel report
  - `CSI 16 t` cell pixel report
  - `CSI 18 t` rows/cols report
  - `CSI 21 t` title report
  - `CSI 22/23 t` title push/pop stack behavior

## Gaps Found Before This Pass

P0

- Parser did not support CSI intermediates, causing misinterpretation of SL/SR/DECSCUSR/DECIC/DECDC.
- Parser did not support `:` in CSI params, breaking colon-form SGR extended color sequences.
- DEC DSR and ANSI DSR were conflated (`CSI ? n` handled as ANSI form).
- CPR did not report origin-relative coordinates in origin mode.

P1

- Missing DECCOLM (`?3`) behavior and mode-40 gating (`?40`).
- Missing window title report/push/pop via `CSI t` window ops.

## Executed Work (Implemented)

### Parser and Dispatch

- Added CSI intermediate tracking and dispatch.
- Added `:` as CSI parameter separator.
- Extended CSI dispatch signature to include intermediate.

Files:

- `src/XamlVisualEditor.Terminal/TerminalParser.cs`
- `src/XamlVisualEditor.Terminal/TerminalEmulator.cs`

### VT Features Added

- Added `CSI Ps SP @` (SL) and `CSI Ps SP A` (SR).
- Added `CSI Ps SP q` (DECSCUSR cursor shape/blink).
- Added `CSI Ps ' }` (DECIC) and `CSI Ps ' ~` (DECDC).
- Added DECSTR (`CSI ! p`) soft-reset handling.

Files:

- `src/XamlVisualEditor.Terminal/TerminalEmulator.cs`

### DSR Fixes

- Split ANSI DSR and DEC DSR behavior.
- Implemented `CSI ? r;cR` formatting for DEC CPR response.
- Made CPR origin-relative in origin mode (including left margin offset when DECSLRM is active).

Files:

- `src/XamlVisualEditor.Terminal/TerminalEmulator.cs`

### Window Ops and Title Stack

- Added `CSI 21 t` title report (`OSC l ... ST`).
- Added `CSI 22/23 t` title push/pop stack behavior with bounded stack.

Files:

- `src/XamlVisualEditor.Terminal/TerminalEmulator.cs`

### DECCOLM + Mode 40

- Added mode 40 support (`allow 80/132`).
- Added mode 3 behavior (`80/132 column mode`) gated by mode 40.
- On DECCOLM apply: resize to target columns, clear display, home cursor, reset scroll region.
- Added state fields for linefeed/newline mode and 80/132 mode state.

Files:

- `src/XamlVisualEditor.Terminal/TerminalState.cs`
- `src/XamlVisualEditor.Terminal/TerminalEmulator.cs`

### SGR Colon-Form Color Compatibility

- Extended RGB parser to accept normalized colon-form payloads like `38:2::R:G:B`.

Files:

- `src/XamlVisualEditor.Terminal/TerminalEmulator.cs`

## Tests Added

New regression tests were added in:

- `tests/XamlVisualEditor.Tests.Unit/TerminalEmulatorTests.cs`

Coverage added for:

- window title report (`21t`)
- window title push/pop (`22/23t`)
- DECSCUSR via CSI intermediate-space
- SL/SR (space intermediates)
- DECIC/DECDC (`'` intermediates)
- origin-relative CPR and DEC CPR prefix
- colon-form SGR RGB
- DECCOLM gating and resize behavior
- DECSTR soft reset behavior

## Validation Run

Executed and passing:

- `dotnet test tests/XamlVisualEditor.Tests.Unit/XamlVisualEditor.Tests.Unit.csproj --filter "FullyQualifiedName~TerminalEmulatorTests"`
- `dotnet test tests/XamlVisualEditor.Tests.Unit/XamlVisualEditor.Tests.Unit.csproj --filter "FullyQualifiedName~Terminal"`
- `dotnet test tests/XamlVisualEditor.Tests.UI/XamlVisualEditor.Tests.UI.csproj --filter "FullyQualifiedName~TerminalControlTests"`

## Remaining Parity Backlog

### Phase 2 (High)

- Full DECRQM/DECRPM parity matrix for implemented modes.
- Additional DEC private save/restore nuance for newly added modes.
- More complete DECSTR compatibility matrix vs Ghostty/xterm.

### Phase 3 (High)

- Replay-driven parity from real TUI captures (vim/tmux/htop/mc) across resize loops.
- Golden rendering tests for resize stress, split panes, and cursor-style updates.

### Phase 4 (Medium)

- Wider window ops parity (guarded by safe integration policy).
- Expanded OSC/DSR compatibility matrix and capability toggles.

### Phase 5 (Medium)

- Performance pass on parser hot path and buffer mutations under sustained output.
- Additional allocation trimming and profiling of resize/reflow paths.

## Acceptance Criteria For Next Iteration

- No regressions in terminal unit/UI suites.
- Real capture replays for at least `mc`, `vim`, and `tmux` without pane corruption.
- Stable behavior across repeated window resize operations (grow/shrink loops).
- Explicit parity matrix maintained against Ghostty/xterm.js for all implemented sequences.
