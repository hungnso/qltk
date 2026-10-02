# Full Auto Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** QLTK reads and edits every setting shown in the game's Tự động menu for chosen running accounts, with per-tab confirmation and persistence.

**Architecture:** A scrollable WinForms dialog reads `auto-state.xml` files written by each game's existing event-thread observer. QLTK writes a per-session, per-tab change command; the observer validates, applies, saves through the game's existing RMS methods, and acknowledges each command. A separate C# model owns option definitions, mixed-tab merging, value validation, and XML parsing.

**Tech Stack:** .NET Framework WinForms/C# (`csc.exe`), Java 8/MicroEmulator, XML files with atomic replacement, the existing PowerShell build/test scripts.

**Spec:** `docs/superpowers/specs/2026-10-02-qltk-full-auto-menu-design.md`

## Global Constraints

- Exactly 32 visible boolean options: the 31 entries drawn by `GameScr` plus `AutoDailyPanel.weaponOnlyPickup`; seven numeric values: `Char.ek`, `el`, `em`, `en`, `eo`, `ep`, `eq`.
- The checkbox field order is `timeStartBlink,isAHP,isAMP,isAFood,isABuff,isAResuscitate,isAPickYen,isAPickYHM,isAPickYHMS,dm,dn,doa,dp,dq,dr,ds,dt,du,dv,dw,dx,dy,dz,ea,eb,ec,ed,ee,ef,eg,eh,weaponOnlyPickup`. Match labels to the visible `GameScr` draw calls; `mResources.ri` has unused entries.
- Exact displayed field→label mapping, in order: `timeStartBlink`→Dùng HP khi còn dưới; `isAHP`→Dùng MP khi còn dưới; `isAMP`→Dùng thức ăn cấp; `isAFood`→Dùng chiêu hỗ trợ; `isABuff`→Dùng khiên mana; `isAResuscitate`→Dùng đốt quái & ẩn thân; `isAPickYen`→Dùng phân thân; `isAPickYHM`→Nhặt yên; `isAPickYHMS`→Nhặt HP, MP; `dm`→Nhặt N.Liệu(Đá); `dn`→Luyện đá Max; `doa`→Nhặt Trang Bị; `dp`→Nhặt VP Nhiệm Vụ; `dq`→Nhặt VP Sự Kiện; `dr`→Nhặt All; `ds`→Nhặt SVC; `dt`→Không nhặt gì cả; `du`→ReMap; `dv`→Tàn sát map trống; `dw`→Auto Mua Thức Ăn; `dx`→TS khi hết MP; `dy`→Auto Reconnect; `dz`→Chuyển Map Hết Boss; `ea`→Săn TATL; `eb`→Đánh Quái Thường; `ec`→Đánh Tinh Anh; `ed`→Đánh Thủ Lĩnh; `ee`→Cộng tiềm năng; `ef`→Cộng kĩ năng; `eg`→Đánh theo nhóm; `eh`→Né PK; `weaponOnlyPickup`→Chỉ nhặt vũ khí.
- Numeric input bounds matching the menu: `ek` HP 1–99, `el` MP 10–90, `em` food 1 or 10–70 by tens, `en` pickup level 1 or 10–70 by tens, `eo` 1–7, `ep` 1–12, `eq` 1 or 10–70 by tens.
- `Char.b()` saves the ordinary settings to `V7LCSetting`; `AutoDailyPanel` has its own save operation. Reflect into the loaded game classloader on the MicroEmulator event thread. Game source outside this workspace is read-only.
- Use the current account/server key and per-account data directory. No credentials in Auto files, command arguments, UI, or logs. Preserve the existing `accounts.txt`, `tabs.xml`, character history, legacy `layout.xml`, and non-Auto features.
- VPS light mode uses the current observer cadence (5 seconds in game); write snapshots only when changed. No live account login in tests.
- Build with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./build-accounts.ps1`; run the full suite with `powershell.exe -NoProfile -ExecutionPolicy Bypass -File ./tests/run-tests.ps1`.
- The repository already has unrelated uncommitted user/workspace changes. If committing implementation tasks, stage only named source/test/doc files for that task; never sweep account data or generated binaries into a commit.

## Review Focus

- Two selected tabs disagree: editing one checkbox must preserve every untouched flag and number on both tabs (Task 1 + Task 3 tests).
- A selected tab is asleep: its command waits for wake, does not wake the game, and reports pending rather than success (Task 2 + Task 4 tests).
- The tab closes or restarts with an old command present: the new session must never replay that command (Task 2 test).
- An unsupported game JAR or rejected value: report the specific tab/field and keep login, snapshots, and other tabs functioning (Task 2 + Task 4 tests).
- `Không nhặt gì cả`, `Nhặt Trang Bị`, and `Chỉ nhặt vũ khí` conflict: mirror the game's normalization without leaving a partially saved command (Task 1 + Task 2 tests).

---

### Task 1: Auto catalog, snapshot, and edit model

**Files:** Create `src/AutoSettings.cs`; create `tests/AutoSettingsTests.cs`; modify `tests/run-tests.ps1` to compile and run this test and include the new source when compiling the manager.

**Interfaces:** Produce `AutoCatalog.Options : AutoOption[]` (key, Vietnamese label, optional numeric field/range); `AutoSnapshot.Parse(string xml) : AutoSnapshot`; `AutoSelection.Merge(IEnumerable<AutoSnapshot>) : AutoSelection` with mixed values; `AutoEdit.Build(string session, long requestId, IDictionary<string,string> changedFields) : string` for a command containing only changed fields.

- [ ] Write `AutoSettingsTests.cs` with literal expected keys/labels for all 32 visible rows, seven numeric ranges matching the game, mixed-tab merge, untouched fields omitted, invalid value rejection, pickup-option conflict normalization, and bounded XML parsing.
- [ ] Run the new test target through `tests/run-tests.ps1`; confirm it fails because the Auto model/API is missing.
- [ ] Implement `src/AutoSettings.cs` and register it in both `build-accounts.ps1` and `tests/run-tests.ps1`; keep the model independent of WinForms.
- [ ] Run `tests/run-tests.ps1`; confirm the new tests and existing suite pass.
- [ ] Review the option-to-game-field table against `GameScr.java` and `Char.java` in the configured game source; stage/commit only this task's sources/scripts if the working tree allows a clean isolated commit.

### Task 2: Game-side Auto read, apply, save, and acknowledge

**Files:** Create `src/GameAutoController.java`; modify `src/GameAccountObserver.java`, `src/AccountBootstrap.java`, `build-accounts.ps1`; extend `tests/fixture/Char.java`, create `tests/fixture/AutoDailyPanel.java`, and extend `tests/HeadlessSmoke.java`/`tests/BridgeTests.java` as needed.

**Interfaces:** `GameAutoController(File tabHome, String sessionId)` with `tick(ClassLoader loader) : void` called by the existing observer on the event thread. Files: `<AutoState session="…"><Option key="…" enabled="…" value="…"/></AutoState>`, `<AutoCommand session="…" id="…"><Set key="…" enabled="…" value="…"/></AutoCommand>`, and `<AutoResult session="…" id="…" state="OK|ERROR" message="…"/>`; a missing enabled/value attribute means leave that field untouched. C# sets `QLTK_AUTO_SESSION` once per process launch; command/result session and id must match.

- [ ] Extend the offline fixture test to assert all 32 live values and seven numbers, one changed flag without touching another, real `Char.b()` save call, weapon-only save, duplicate-id idempotence, stale-session rejection, invalid range rollback, and unsupported-field error without affecting character snapshots.
- [ ] Run `tests/run-tests.ps1`; confirm the fixture tests fail for the missing game-side controller/observer hook.
- [ ] Implement `GameAutoController.tick`, atomic changed-only state/result writes, command validation before mutation, rollback on reflection/save failure, RMS readback before a success acknowledgment, and nonfatal Auto capability checks. Hook it after game readiness in `GameAccountObserver`; pass the session token from `AccountBootstrap` without changing the existing required login contract.
- [ ] Run `build-accounts.ps1` then `tests/run-tests.ps1`; confirm offline fixture and earlier game tests pass.
- [ ] Review RMS saving and the game menu's exclusion rules; stage/commit only this task's bridge, fixture, build, and tests if possible.

### Task 3: QLTK Auto row, dialog, and per-tab requests

**Files:** Create `src/AutoSettingsDialog.cs`; modify `src/AccountManager.cs`, `build-accounts.ps1`, `tests/run-tests.ps1`; create `tests/AutoUiTests.cs` using managed fake game processes and Auto XML fixtures.

**Interfaces:** `AutoSettingsDialog` accepts selected account keys and `AutoSnapshot`s, exposes `IDictionary<string,string> ChangedFields` only after Apply; `AccountManager` owns per-running-tab `AutoSession` and request sequence and writes commands using `AutoEdit.Build`; result polling matches session/id and updates per-tab status.

- [ ] Write UI tests for the Auto row, 32 scrollable controls, mixed checkbox/number display, selected-account targeting, untouched-field preservation, invalid input that sends no command, per-tab acknowledgment, closed/unready tab, and a sleeping tab that stays asleep while queued.
- [ ] Run `tests/run-tests.ps1`; confirm those tests fail for the absent UI and request wiring.
- [ ] Implement the compact Auto row under Hiệu năng, scrollable grouped dialog, touched-field tracking, tri-state merge, atomic command writes, and asynchronous status polling. The tab key and session token are generated/kept inside QLTK; never expose passwords.
- [ ] Run `build-accounts.ps1` then `tests/run-tests.ps1`; confirm new UI tests and existing launch/history/sleep/arrangement tests pass.
- [ ] Inspect the dialog at 900×480 and normal desktop size to ensure the account grid remains usable; stage/commit only this task's files if possible.

### Task 4: Offline end-to-end verification and user guide

**Files:** Extend `tests/HeadlessSmoke.java`, `tests/AutoUiTests.cs`, `HUONG_DAN_QLTK_ACCOUNTS.md`; update a task-owned source file only if a new failing integration test shows a defect.

**Interfaces:** No new production API; validate the command, game event-thread update, RMS persistence, acknowledgment, and manager display as one flow.

- [ ] Add a failing offline flow that applies different settings to two chosen tabs, keeps an unchosen tab unchanged, waits through sleep/wake, and reloads persisted Auto values. Also test mismatched session IDs, bad XML/oversized input, and game version missing Auto fields.
- [ ] Run `tests/run-tests.ps1`; confirm each new failing case fails for the intended missing behavior or defect, not a fixture setup error.
- [ ] Make the smallest source corrections the failures require; document controls, mixed values, pending/success/error status, and game-version limits in `HUONG_DAN_QLTK_ACCOUNTS.md`.
- [ ] Run `build-accounts.ps1` and the complete `tests/run-tests.ps1`; inspect outputs and exit codes before claiming completion. Verify the built EXE and bridge JAR are the ones in the workspace.
- [ ] Request a focused review of the resulting diff; resolve Important/Critical findings, rerun affected tests, then stage/commit only implementation source/tests/docs if a clean isolated commit is possible.
