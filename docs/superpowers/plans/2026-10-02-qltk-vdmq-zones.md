# QLTK VDMQ Zones Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** QLTK reads and changes each selected game's Auto Vùng đất ma quái zone list without starting Auto, changing map, or disturbing other account settings.

**Architecture:** A small C# model validates zone lists and XML. A dedicated Java controller in the existing game observer reads the per-tab RMS key and active instance, applies per-session commands on the MicroEmulator event thread, verifies persistence, and acknowledges each tab. A compact QLTK dialog and column present current, mixed, pending, and confirmed states.

**Tech Stack:** .NET Framework WinForms/C#, Java 8/MicroEmulator, existing PowerShell build/test scripts, atomic XML files.

**Spec:** `docs/superpowers/specs/2026-10-02-qltk-vdmq-zones-design.md`

## Global Constraints

- Scope is only `AutoVdmqZones`: empty means all zones; a nonempty list contains unique decimal IDs 0–9999, at most 64, in input order. Input accepts commas/whitespace and saves single-space-separated IDs.
- Do not call `NSOT_MOB.startAutoVungDatMaQuai`, select a map, or call `Service.requestChangeZone` from the bridge. An already-running Auto VDMQ may react on its next game update.
- Actual game source is read-only outside this workspace. Reflect against the loaded game's `NSOT_MOB.autoVdmq`, `AutoVungDatMaQuai.configuredZones`/`zoneDataMap`, and `mResources.a(String,String)`/`c(String)`; use the existing event thread and existing `QLTK_AUTO_SESSION`.
- Store `vdmq-state.xml`, `vdmq-command.xml`, and `vdmq-result.xml` only under each tab's `data/accounts/tab_N`; no credentials, no changes to `accounts.txt`, `tabs.xml`, history, or legacy layout.
- XML contract: `<VdmqState session="…" state="READY|WAITING|UNSUPPORTED" zones="0 2"/>`, `<VdmqCommand session="…" id="1" zones="0 2"/>`, `<VdmqResult session="…" id="1" state="OK|ERROR" message="…"/>`. Empty `zones` denotes all zones. Use bounded secure parsing and atomic changed-only state writes.
- The workspace contains substantial pre-existing uncommitted source, build outputs, and live account data. Preserve those files; if a task can be committed cleanly, stage only its named files. Do not reset or clean the checkout.
- Build: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build-accounts.ps1`. Full tests: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./tests/run-tests.ps1`.

## Review Focus

- A user types `0, 2 2`: normalize to `0 2`; reject negatives, nonnumbers, >9999, or >64 IDs before any tab receives a command (Task 1).
- A tab has changed its zones in the game menu while QLTK is open: next snapshot reflects the game value; an untouched mixed dialog sends no command (Tasks 2–3).
- A selected tab is asleep for over 30 seconds: command waits, does not wake it, and still has time to receive acknowledgment after waking (Task 3).
- A missing VDMQ class or a silent RMS write failure: report that tab's error, preserve its previous setting, and continue account/Auto observation (Task 2).
- A result-file write is temporarily blocked or the tab restarts: retry acknowledgment without applying twice, and ignore the previous session's command (Task 2).

---

### Task 1: Zone model and wire contract

**Files:** Create `src/VdmqZones.cs` and `tests/VdmqZoneTests.cs`; register both in `build-accounts.ps1` and `tests/run-tests.ps1`.

**Interfaces:** `VdmqZoneList.Normalize(string input, bool allZones) : string` returns canonical space-separated IDs or `""`; `VdmqSnapshot.Parse(string xml) : VdmqSnapshot` exposes `Session`, `State`, and `Zones`; `VdmqSelection.Merge(IEnumerable<VdmqSnapshot>) : string` returns canonical zones or `null` for mixed; `VdmqEdit.Build(string session, long id, string zones) : string` emits the command XML.

- [ ] Write model tests for all-zones empty value; `"0, 2 2" → "0 2"`; ID 0; invalid `-1`, `10000`, letters, and 65 IDs; mixed merge; secure XML rejects DTD, oversized input, wrong root, duplicate/missing attributes; command contains only session/id/zones.
- [ ] Run `tests/run-tests.ps1`; confirm the new model target fails for the missing API.
- [ ] Implement the model and compile registrations, reusing the Auto XML safety conventions without coupling VDMQ to the 32 Auto flags.
- [ ] Run `tests/run-tests.ps1`; confirm new model tests and existing suite pass. Commit only these task files if they can be staged without unrelated changes.

### Task 2: Game-side read, apply, persist, and acknowledge

**Files:** Create `src/GameVdmqController.java`; modify `src/GameAccountObserver.java` and `build-accounts.ps1`; extend `tests/fixture/mResources.java`; create `tests/fixture/NSOT_MOB.java` and `tests/fixture/AutoVungDatMaQuai.java`; extend `tests/HeadlessSmoke.java`.

**Interfaces:** `GameVdmqController(File tabHome, String session)` with `tick(ClassLoader loader) : void` when observer sees a ready character and `waiting() : void` on non-game screens. Uses the three `vdmq-*.xml` files from Global Constraints, independent of Auto's command/result files. Reads the RMS key `AutoVdmqZones` through the game's `mResources` and, when initialized, the active object's private `configuredZones`.

- [ ] Extend the fixture test to verify missing RMS means all zones; manual menu-like RMS/runtime change appears in snapshot; command `0 2` changes both RMS and initialized runtime; `zoneDataMap` invalidates; no call starts Auto or changes map; malformed/oversized command does not stop character snapshots.
- [ ] Add fixture cases for unsupported class/field, stale session, duplicate request, invalid zones, silent RMS save/readback failure with rollback, and result-write retry without duplicate apply.
- [ ] Run `tests/run-tests.ps1`; confirm the new fixture assertions fail for the absent controller/hook.
- [ ] Implement capability checks, secure/bounded command parsing, session/ID deduplication, canonical zone validation, RMS readback, runtime update and rollback, changed-only state, and retryable result delivery. Wire it next to `GameAutoController.tick` and call `waiting()` in the observer's non-ready path. Keep failures local to VDMQ so existing character/Auto ticks still run.
- [ ] Run build and full tests; compare reflected method/field signatures with `C:/Users/Admin/Desktop/game/dapdonso/nso2-main/src/AutoVungDatMaQuai.java`, `NSOT_MOB.java`, and `mResources.java`. Commit only task-owned files if clean staging is possible.

### Task 3: QLTK controls and per-tab commands

**Files:** Create `src/VdmqZonesDialog.cs` and `tests/VdmqUiTests.cs`; modify `src/AccountManager.cs`, `build-accounts.ps1`, and `tests/run-tests.ps1`.

**Interfaces:** `VdmqZonesDialog(IEnumerable<VdmqSnapshot>)` exposes `Changed` and `Zones` after Apply. `AccountManager` stores `VdmqSnapshot`, state file stamp, command sequence, pending ID/time, and VDMQ status per `RunningTab`, reusing the tab's `AutoSession`. `ApplyVdmq(List<Account> selected, string zones)` writes one atomic command per ready selected tab and matches acknowledgments by session and ID.

- [ ] Write UI tests for the **Khu up VDMQ** button beside Auto, **Khu VDMQ** column, all/mixed values, no command on untouched dialog, chosen-only targeting, unready/unsupported tab skipped, correct success/error acknowledgment, sleeping request retained after >30 seconds and applied after wake.
- [ ] Run `tests/run-tests.ps1`; confirm UI tests fail for the missing controls/behavior.
- [ ] Implement the dialog and manager polling/sending, with labels `Tất cả khu`, `Khu: 0 2`, `Khác nhau — giữ nguyên`, `Chờ thức`, and explicit per-tab errors. Keep the existing configuration row count/height and account grid usable at 900×480.
- [ ] Run build and full tests; inspect the compact row/dialog visually and commit only task-owned files if clean staging is possible.

### Task 4: Offline flow and guidance

**Files:** Extend `tests/HeadlessSmoke.java` and `tests/VdmqUiTests.cs`; update `HUONG_DAN_QLTK_ACCOUNTS.md`; change production files only for failing behavior revealed here.

**Interfaces:** No new API. The managed fake-process fixture proves per-account targeting/status; the offline Java fixture proves event-thread application, persistence, readback, and acknowledgment with the same XML contract.

- [ ] Add managed fixture cases for two selected tabs with different old lists and one unselected tab; change selected tabs only and leave the unselected tab unchanged. Add Java fixture cases that reload the saved RMS list and expose a menu-side edit in the next snapshot. Include a sleeping tab and a game version without VDMQ.
- [ ] Run the new cases; confirm any failure identifies an actual missing behavior rather than reused fixture RMS or setup artifacts. Use a fresh temporary smoke home per test run.
- [ ] Make only corrections required by failures. Document ID numbering from 0, empty/all semantics, multi-select mixed state, apply/acknowledge timing, sleep behavior, per-tab persistence, and unsupported-JAR limits in the user guide.
- [ ] Run build and the complete `tests/run-tests.ps1`, inspect exit codes and built EXE/JAR, and run `git diff --check` on touched tracked files. Request a focused review of the VDMQ diff; resolve Important/Critical findings and rerun affected tests. Do not launch real accounts as part of verification.
