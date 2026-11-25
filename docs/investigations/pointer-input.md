# Continue the missing pointer-message investigation

The next approved change is to fill gaps in the pointer trace, not to change input delivery. **That change is not implemented yet.** This page keeps the evidence and next steps in the repository so another session or machine can continue.

## The Local API fix passed; the pointer failure remains

[Run 45540807230](https://github.com/RazMake/Memoria-ToDo/actions/runs/45540807230), automatically started by commit `c8bf08795fa282529b1988e5b144987999adc24a`, finished with 6,904 passed, 1 failed, and 0 skipped .NET tests. All 205 Local API tests passed. That separate repair is complete.

The failure was `HoverCardRealPointerIntegrationTests.A_real_pointer_move_onto_a_part_opens_its_card_and_asks_for_the_part_where_the_pointer_is(pointerStartsOnPart: True)`. It happened at the [initial pointer move, before the hover-card component was constructed](https://github.com/RazMake/Memoria-ToDo/blob/c8bf08795fa282529b1988e5b144987999adc24a/tests/Desktop/Tests/Platform/HoverCard/HoverCardRealPointerIntegrationTests.cs#L33).

The [saved trace below](#the-failing-moves-trace-is-kept-here) comes from that run's JUnit failure message. Continuing this investigation does not require the original session directory or an unexpired Actions artifact.

## The trace narrows the failure but does not name its cause

The packet was accepted and the cursor moved to the requested point. The target, hit, and foreground window handles matched at the readings, as did their thread IDs. Neither reading showed capture. The dispatcher processed other messages and its timer fired.

No processed-mouse hook callback or WPF routed move was recorded. This does **not** prove Windows dropped the input. The observer has gaps, and the readings cannot exclude transient routing changes between them.

The queue value `0x0008` means posted messages, not pending mouse input. It is only a point-in-time hint: [GetQueueStatus](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getqueuestatus) does not reconstruct what Windows generated or filtered earlier.

An [earlier failure, run 45488260474](https://github.com/RazMake/Memoria-ToDo/actions/runs/45488260474), used marker 2 and failed at `(133, 182)` on the three-pixel step away, after the first positioning move passed. Removing that step or adding a first-move warm-up would not explain both occurrences.

## Fill the observation gap without changing what passes

The user approved recording the missing stages in the same move attempt:

1. **Thread retrieval:** preserve mouse and pointer message identities and destinations. [PointerMoveRecorder.Fetched](../../tests/Desktop/Tests/Platform/NativeInput/PointerDiagnostics/PointerMoveRecorder.cs) currently ignores mouse-range IDs and collapses other messages into a count.
2. **Mouse-hook calls:** distinguish processed messages from non-removing peeks. [RealPointer](../../tests/Desktop/Tests/Platform/RealPointer.cs) records only `HC_ACTION` (0), not `HC_NOREMOVE` (3); [MouseProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/mouseproc) explains both.
3. **Target-window arrival:** observe relevant messages before WPF's normal handling. Verify the ordering of the chosen hook rather than assuming every window hook precedes WPF's input providers.
4. **Actual hit-test result:** preserve the routing result, not only a separate `WindowFromPoint` reading. Inspect the existing sent-message hook and structure in [Win32](../../tests/Desktop/Tests/Platform/Win32.cs) before adding another observer.

Correlate those stages with the existing routed-move observation. Prefer extending the existing thread-local observers and bounded recorder.

The [diagnostic contract](../architecture/technology-stack.md#a-failed-real-pointer-move-reports-what-each-step-saw) remains the owner of privacy, retention, callback failure handling, and cleanup. Keep input sending, delivery success, the preparatory step, and all waits unchanged. Do not make a new diagnostic observation a requirement for a move to pass.

The extension must not consume messages, change their data, set `handled`, or add global low-level hooks. Message identification does not require reading raw-input payloads. Do not add queue reads casually: they change Windows' new-message bookkeeping.

The user plans to inspect the next natural cloud recurrence. Do not dispatch or retry a run merely to obtain another failure.

## Do not restart the investigation from unsupported guesses

These checks did not establish a cause:

- [NativeInput.Move](../../tests/Desktop/Tests/Platform/NativeInput/NativeInput.cs) already sends an absolute, virtual-desktop move with `MOVE_NOCOALESCE`. Native structure alignment and callback lifetime were inspected without finding a defect.
- The [receipt](../../tests/Desktop/Tests/Platform/NativeInput/InjectedMoveReceipt.cs) already accepts the current marker or zero. That cannot repair an attempt with no observed move.
- A repository audit found no established competing input consumer. Screen tests share the [screen collection](../../tests/Desktop/Tests/ScreenTests.cs); serial tests can leave the cursor elsewhere, but the failing trace explicitly records a nonzero move.
- Public Windows/WPF research found no matching documented first-move suppression bug. WPF's own first-move filtering happens after native retrieval, so it alone does not explain a silent mouse hook.
- [AfterFirstDraw](../../tests/Desktop/Tests/Platform/AfterFirstDraw.cs) uses a Background-priority barrier. WPF queues `ContentRendered` at Input priority; that event is not a compositor presentation acknowledgment. No evidence connects replacing the barrier with this event to the missing native message.

Do not claim that extra waits, Topmost, warm-up moves, or a different marker rule fix the failure. Its exact cause is still unknown.

## Resume with the diagnostic extension

At this handoff, no source changes or new tests for the extension have been made, and no implementation agents have started.

Use the [contribution process](../contributing/process.md) to define the observer ordering and owner-document update, then add focused recorder tests before implementation. Cover message identity and destination, peek versus removal, target-window arrival, hit-test results, privacy exclusions, and cleanup. Use the smallest integration coverage needed to verify real hook wiring.

After independent review and the closing failure proof, use [affected test selection](../architecture/test-selection.md#a-script-turns-the-changed-files-into-the-commands-to-run). The earlier Local API-only selection is not applicable: this change touches shared Platform test support. Use the repository SDK and the [documented test commands and desktop prerequisite](../architecture/technology-stack.md#plain-dotnet-test-runs-only-unit-tests-and-final-validation-runs-every-kind-of-test).

When the failure next occurs, compare the observed stages in that same attempt. A missing hook callback alone is not enough to assign the failure to Windows, WPF, or the helper.

## The failing move's trace is kept here

This is the failure message extracted from the desktop JUnit report for [job 138958562318](https://github.com/RazMake/Memoria-ToDo/actions/runs/45540807230/job/138958562318). Its statement that a message was "never fetched" describes the current observer's result, not proof that Windows never delivered one.

```text
System.TimeoutException : WPF did not observe the pointer on Border: SendInput accepted the packet; its marked WM_MOUSEMOVE was never fetched (0 mouse messages fetched while waiting), requested screen (130, 182), cursor (130, 182), last observed event none, expected marker 1, last fetched mouse message none, directly over none, captured by none; expected window 4301D0 (target), hit window 4301D0 (target), foreground window 4301D0 (target).
Pointer trace of this test's real moves; 0 oldest entries dropped.
Latest move, kept apart from the entries:
  call 1 target at send: cursor (512, 384); window at the point 4301D0 (target); foreground window 4301D0 (target); foreground window thread 2488; target window thread 2488; test thread 2488; WPF capture none; test thread native capture none; test thread GUI info thread 2488 active 4301D0 (target), focus 4301D0 (target), capture none, menu owner none, move/size none, flags 0x0; foreground thread GUI info thread 2488 active 4301D0 (target), focus 4301D0 (target), capture none, menu owner none, move/size none, flags 0x0
  call 1 target timed out: no routed move was accepted before the wait ended
  call 1 target at failure: cursor (130, 182); window at the point 4301D0 (target); foreground window 4301D0 (target); foreground window thread 2488; target window thread 2488; test thread 2488; WPF capture none; test thread native capture none; test thread GUI info thread 2488 active 4301D0 (target), focus 4301D0 (target), capture none, menu owner none, move/size none, flags 0x0; foreground thread GUI info thread 2488 active 4301D0 (target), focus 4301D0 (target), capture none, menu owner none, move/size none, flags 0x0
  call 1 target queue status: pending bits 0x0008, current only, read once at the timeout
Entries, oldest first:
  [+20 ms] call 1 target: send check passed for (130, 182)
  [+24 ms] call 1 target: marker 1, cursor start (512, 384), target (130, 182)
  [+30 ms] call 1 target: send check passed for (130, 182)
  [+31 ms] call 1 target: calling SendInput
  [+32 ms] call 1 target: SendInput inserted 1 of 1
  [+3036 ms] call 1 target: wait timer fired
  [+3036 ms] call 1 target: wait ended; 10 other messages counted, their content not recorded
Not shown: mouse capture held by an unobserved thread, windows of other threads or processes that came and went between readings, and whether Windows dropped or delayed a move. A missing message is evidence, not proof, that Windows did not deliver it. This trace names no cause.
```
