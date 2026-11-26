# Continue the missing pointer-message investigation

**The cause remains unknown.** The [thread-stage observers](../architecture/technology-stack.md#the-trace-follows-a-move-through-each-stage-on-the-test-thread) ran in the latest failure and recorded no input reaching the test thread. The user approved a [temporary low-level observer](../architecture/technology-stack.md#a-temporary-low-level-hook-follows-only-our-marked-move) to look before thread delivery. Neither extension changes input delivery or claims to fix the failure.

## The new thread-stage trace also saw no input

[Run 45746343858](https://github.com/RazMake/Memoria-ToDo/actions/runs/45746343858), [job 139663228483](https://github.com/RazMake/Memoria-ToDo/actions/runs/45746343858/job/139663228483), tested commit `06a3079b3d6fc5956cf857de89d4827b9bab61e6`: 7,378 passed, 1 failed, and 0 skipped. The same `pointerStartsOnPart: True` case failed at initial positioning, before the hover card was constructed.

The packet was accepted and the cursor reached its target. No input appeared at retrieval, the mouse hook, WPF's loop, or the target window. The dispatcher retrieved eight other messages and fired its timer. The four hit tests bracket the send and the timeout; the helper's own window queries cause hit tests, so these entries do not establish delivery of the injected move.

The observer's integration test and the `pointerStartsOnPart: False` case passed in this run. [Run 45764678558](https://github.com/RazMake/Memoria-ToDo/actions/runs/45764678558) later passed all 7,598 tests with the pointer helper unchanged. It used runner image `20261116.315.1`, while the failing run used `20261123.321.1`; however, earlier identical failures also used `20261116.315.1`. Pinning that older image is not an evidence-backed fix.

The latest trace is retained here, without the surrounding runner timestamps:

```text
Pointer trace of this test's real moves; 0 oldest entries dropped.
Latest move, kept apart from the entries:
  call 1 target hit test: count 4, first +23 ms, last +3052 ms; WM_NCHITTEST window 2401CA at (130, 182), result 1
  call 1 target at send: cursor (512, 384); window at the point 2401CA (target); foreground window 2401CA (target); foreground window thread 6816; target window thread 6816; test thread 6816; WPF capture none; test thread native capture none; test thread GUI info thread 6816 active 2401CA (target), focus 2401CA (target), capture none, menu owner none, move/size none, flags 0x0; foreground thread GUI info thread 6816 active 2401CA (target), focus 2401CA (target), capture none, menu owner none, move/size none, flags 0x0
  call 1 target other retrievals: 8 removed, 0 peeked
  call 1 target timed out: no routed move was accepted before the wait ended
  call 1 target at failure: cursor (130, 182); window at the point 2401CA (target); foreground window 2401CA (target); foreground window thread 6816; target window thread 6816; test thread 6816; WPF capture none; test thread native capture none; test thread GUI info thread 6816 active 2401CA (target), focus 2401CA (target), capture none, menu owner none, move/size none, flags 0x0; foreground thread GUI info thread 6816 active 2401CA (target), focus 2401CA (target), capture none, menu owner none, move/size none, flags 0x0
  call 1 target queue status: pending bits 0x0008, current only, read once at the timeout
Entries, oldest first:
  [+20 ms] call 1 target: send check passed for (130, 182)
  [+21 ms] call 1 target: marker 1, cursor start (512, 384), target (130, 182)
  [+23 ms] call 1 target: hit test: WM_NCHITTEST window 2401CA at (130, 182), result 1
  [+25 ms] call 1 target: hit test: WM_NCHITTEST window 2401CA at (130, 182), result 1
  [+25 ms] call 1 target: send check passed for (130, 182)
  [+26 ms] call 1 target: calling SendInput
  [+27 ms] call 1 target: SendInput inserted 1 of 1
  [+3051 ms] call 1 target: wait timer fired
  [+3051 ms] call 1 target: wait ended; 8 other filtered messages counted, their content not recorded
  [+3052 ms] call 1 target: hit test: WM_NCHITTEST window 2401CA at (130, 182), result 1
  [+3052 ms] call 1 target: hit test: WM_NCHITTEST window 2401CA at (130, 182), result 1
Not shown: mouse capture held by an unobserved thread, windows of other threads or processes that came and went between readings, and whether Windows dropped or delayed a move. A missing message is evidence, not proof, that Windows did not deliver it. This trace names no cause.
```

## The first pointer move failed again at commit 1f59bd8

[Run 45695729810](https://github.com/RazMake/Memoria-ToDo/actions/runs/45695729810), [job 139492496062](https://github.com/RazMake/Memoria-ToDo/actions/runs/45695729810/job/139492496062), ran commit `1f59bd8c1b59874143704b5384172ea2a041ca39`. Its tests finished with 7,331 passed, 1 failed, and 0 skipped.

The failure was `HoverCardRealPointerIntegrationTests.A_real_pointer_move_onto_a_part_opens_its_card_and_asks_for_the_part_where_the_pointer_is(pointerStartsOnPart: True)`. It happened at the [initial pointer move, before the hover-card component was constructed](https://github.com/RazMake/Memoria-ToDo/blob/1f59bd8c1b59874143704b5384172ea2a041ca39/tests/Desktop/Tests/Platform/HoverCard/HoverCardRealPointerIntegrationTests.cs#L33).

That commit only added the separate [real click report](../architecture/technology-stack.md#a-real-click-the-window-never-sees-released-reports-what-it-saw). The pointer extension wasn't built, so this trace has the same gaps as the one before it.

[Run 45540807230](https://github.com/RazMake/Memoria-ToDo/actions/runs/45540807230), at commit `c8bf08795fa282529b1988e5b144987999adc24a`, failed the same test the same way. Its trace differs only in its window handle, thread ID, and times.

The [saved trace below](#the-failing-moves-trace-is-kept-here) comes from the job's log. Continuing this investigation doesn't need the original session directory or an unexpired Actions artifact.

## The trace narrows the failure but does not name its cause

In both failures, the packet was accepted and the cursor moved to the requested point. The target, hit, and foreground window handles matched at the readings, as did their thread IDs. Neither reading showed capture. The dispatcher processed other messages and its timer fired.

No processed-mouse hook callback or WPF routed move was recorded. This does **not** prove Windows dropped the input. The observer has gaps, and the readings can't exclude transient routing changes between them.

The queue value `0x0008` means posted messages, not pending mouse input. It's only a point-in-time hint: [GetQueueStatus](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getqueuestatus) doesn't reconstruct what Windows generated or filtered earlier.

An [earlier failure, run 45488260474](https://github.com/RazMake/Memoria-ToDo/actions/runs/45488260474), used marker 2 and failed at `(133, 182)` on the three-pixel step away, after the first positioning move passed. Removing that step or adding a first-move warm-up wouldn't explain both kinds of failure.

## Do not restart the investigation from unsupported guesses

These checks didn't establish a cause:

- [NativeInput.Move](../../tests/Desktop/Tests/Platform/NativeInput/NativeInput.cs) already sends an absolute, virtual-desktop move with `MOVE_NOCOALESCE`. Native structure alignment and callback lifetime were inspected without finding a defect.
- The [receipt](../../tests/Desktop/Tests/Platform/NativeInput/InjectedMoveReceipt.cs) already accepts the current marker or zero. That can't repair an attempt with no observed move.
- A repository audit found no established competing input consumer. Screen tests share the [screen collection](../../tests/Desktop/Tests/ScreenTests.cs); serial tests can leave the cursor elsewhere, but the failing trace explicitly records a nonzero move.
- Public Windows and WPF research found no matching documented first-move suppression bug. WPF's own first-move filtering happens after native retrieval, so it alone doesn't explain a silent mouse hook.
- [AfterFirstDraw](../../tests/Desktop/Tests/Platform/AfterFirstDraw.cs) uses a Background-priority barrier. WPF queues `ContentRendered` at Input priority; that event isn't a compositor presentation acknowledgment. No evidence connects replacing the barrier with this event to the missing native message.

Don't claim that extra waits, Topmost, warm-up moves, or a different marker rule fix the failure. Its exact cause is still unknown.

## Resume with the native-stage extension

The user approved the [native-stage design](../architecture/technology-stack.md#a-temporary-low-level-hook-follows-only-our-marked-move), including a test-only hook that rejects only its own injected marked move. This is diagnostic work, not a repair. Implementation, independent review, closing failure proof, and final affected validation completed on 2026-11-26.

- The review found two failure-path defects: observer-only failures lost their trace, and failed dialog-watcher startup missed worker cleanup. Both were reproduced with unit tests and fixed.
- Native validation exposed a marker-width problem in the new code. The [owner design](../architecture/technology-stack.md#a-temporary-low-level-hook-follows-only-our-marked-move) records its evidence and the narrower allocation range. No observer filter was broadened.
- Both new native cases failed at the expected empty-observation assertion when the hook forwarded input without recording it. Earlier sentinel timeouts did not count as proof. The temporary break was restored.
- The final affected run selected Platform and its test-code users, App and TaskEditor.AddWindow: 810 unit, 31 UX, and 281 integration cases passed, with zero failures and zero skips. Both native pass-through/rejection cases and both original hover-card pointer cases passed. Unrelated TaskList work was excluded; the remaining suite runs only in the cloud.

After the user's next push, compare the low-level callback's entry and downstream result with the thread stages in the same naturally failing attempt. A pending downstream result is different from zero. A nonzero result identifies downstream rejection; zero does not promise delivery to WPF. Do not retry input or dispatch a workflow merely to obtain another failure.

## Earlier local validation stopped before closing proof

The earlier thread-stage observer and its tests now exist. The [owner section](../architecture/technology-stack.md#the-trace-follows-a-move-through-each-stage-on-the-test-thread) links them and explains what the tests prove.

Local validation on 2026-11-25 is incomplete:

- The recorder's 24 unit cases passed. Red stopped at the missing observer APIs, before executing tests.
- All 11 new cases still need closing failure proof. The attempted missing-report break could not build because another session's desktop test run locked the executable. No proof tests ran, and the break was restored.
- The new integration test and final affected run have not run. In particular, the observer's ordering on the pinned WPF runtime is not yet verified by a test.
- The reviewer refused the unverified-model waiver without reviewing files. The user then approved non-independent self-review; it found no actionable defect.
- The user could not grant exclusive desktop validation while the other session was running, so validation paused without stopping its processes.

The later cloud runs above executed this observer, including its integration test. They do not supply the earlier session's missing failure proof.

## The failing move's trace is kept here

This is the failure message from the log of [job 139492496062](https://github.com/RazMake/Memoria-ToDo/actions/runs/45695729810/job/139492496062). Its statement that a message was "never fetched" describes the current observer's result, not proof that Windows never delivered one.

```text
System.TimeoutException : WPF did not observe the pointer on Border: SendInput accepted the packet; its marked WM_MOUSEMOVE was never fetched (0 mouse messages fetched while waiting), requested screen (130, 182), cursor (130, 182), last observed event none, expected marker 1, last fetched mouse message none, directly over none, captured by none; expected window AC0210 (target), hit window AC0210 (target), foreground window AC0210 (target).
Pointer trace of this test's real moves; 0 oldest entries dropped.
Latest move, kept apart from the entries:
  call 1 target at send: cursor (512, 384); window at the point AC0210 (target); foreground window AC0210 (target); foreground window thread 6304; target window thread 6304; test thread 6304; WPF capture none; test thread native capture none; test thread GUI info thread 6304 active AC0210 (target), focus AC0210 (target), capture none, menu owner none, move/size none, flags 0x0; foreground thread GUI info thread 6304 active AC0210 (target), focus AC0210 (target), capture none, menu owner none, move/size none, flags 0x0
  call 1 target timed out: no routed move was accepted before the wait ended
  call 1 target at failure: cursor (130, 182); window at the point AC0210 (target); foreground window AC0210 (target); foreground window thread 6304; target window thread 6304; test thread 6304; WPF capture none; test thread native capture none; test thread GUI info thread 6304 active AC0210 (target), focus AC0210 (target), capture none, menu owner none, move/size none, flags 0x0; foreground thread GUI info thread 6304 active AC0210 (target), focus AC0210 (target), capture none, menu owner none, move/size none, flags 0x0
  call 1 target queue status: pending bits 0x0008, current only, read once at the timeout
Entries, oldest first:
  [+14 ms] call 1 target: send check passed for (130, 182)
  [+15 ms] call 1 target: marker 1, cursor start (512, 384), target (130, 182)
  [+23 ms] call 1 target: send check passed for (130, 182)
  [+23 ms] call 1 target: calling SendInput
  [+24 ms] call 1 target: SendInput inserted 1 of 1
  [+3036 ms] call 1 target: wait timer fired
  [+3037 ms] call 1 target: wait ended; 10 other messages counted, their content not recorded
Not shown: mouse capture held by an unobserved thread, windows of other threads or processes that came and went between readings, and whether Windows dropped or delayed a move. A missing message is evidence, not proof, that Windows did not deliver it. This trace names no cause.
```
