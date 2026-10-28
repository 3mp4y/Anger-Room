# A reusable timer, illustrated with the shooting minigame

These are study files, not a game update. They are outside `Assets`, are not
compiled by Unity automatically, and are not attached to any scene object.
The existing minigames and their timers are unchanged.

Read `CountdownTimer.cs` first, then `ShootingMinigameTimerExample.cs`.

## 1. The timer class

`CountdownTimer` is an ordinary C# class, not a MonoBehaviour. It has no
GameObject, Inspector, Update method, text field, audio reference or coroutine.
It does nothing until another object calls its methods.

| Member | Meaning |
| --- | --- |
| `Start(seconds)` | Replace any previous countdown with a new running countdown. Duration must be finite and greater than zero. |
| `Tick(elapsedSeconds)` | Subtract elapsed time when running. Return true only on the call that reaches zero. |
| `RemainingSeconds` | Read the remaining time. The caller cannot assign it. |
| `IsRunning` / `IsPaused` | Read the current state. |
| `Pause()` | Keep the remaining time and ignore subsequent ticks. |
| `Resume()` | Continue a paused countdown. Does not restart expired or cancelled countdowns. |
| `Stop()` | Cancel without reporting expiry and set remaining time to zero. |

The private enum makes the four states mutually exclusive. At expiry, Tick
sets the state to Expired before returning true. A later Tick therefore cannot
report the same expiry again. Zero remaining time alone does not mean an
expiry should be reported: a cancelled timer also has zero remaining time.

The bool return is a deliberate choice here. There is one owner that needs the
result, so a return value is enough; an event with subscription/unsubscription
would add work without helping this example.

`public float RemainingSeconds { get; private set; }` means everyone can read
the value but only CountdownTimer can change it. This prevents another script
from changing the time without also maintaining the timer's state correctly.

## 2. The shooting controller owns an instance

The example controller contains:

```csharp
private readonly CountdownTimer roundTimer = new CountdownTimer();
```

Each controller instance gets its own timer. `readonly` prevents replacing
the reference with a different timer; it does not prevent calling Start or
changing the timer object's internal state. The timer is not static and is
not shared between minigames.

The example is based on the current shooting controller and retains its
target movement, gun visibility, localization, scoring calls and level
transition sequence. Its timer-related connections are:

| Shooting operation | Timer action |
| --- | --- |
| `StartGun()` | `roundTimer.Start(roundDurationSeconds)` |
| `Update()` | `roundTimer.Tick(Time.deltaTime)` |
| Timed attempt expires | Call `HandleTimeExpired()` once |
| Required hits reached | Stop the timer before the transition or victory |
| Next level starts | Start a new 21-second timer, matching the current rule |
| Tutorial starts/ends | Stop the timer; tutorial practice is untimed |

The first-level duration deliberately has no invented default: it must be
copied from the live controller's Inspector configuration before testing an
integrated version. Start rejects zero/negative/NaN/infinite durations.

## 3. Update supplies time; it does not implement countdown rules

```csharp
bool expiredThisFrame = roundTimer.Tick(Time.deltaTime);
```

Unity calls the controller's Update. The controller passes the frame's elapsed
game time to the timer. This is still an Update-based countdown, with the
arithmetic and state handling moved into one reusable class.

On expiry, the controller stops spawning, clears targets, changes the gun
models, restores the environment, records the failed attempt and plays the
appropriate audio. These are gameplay decisions, so they stay in the shooting
controller. The timer cannot fail a minigame by itself.

The early return after HandleTimeExpired prevents UpdateTimerDisplay from
overwriting the failure message. Outside timed play, this Update leaves the
timer text alone so tutorials, transitions and victory can use it.

## 4. UI policy remains separate

UpdateTimerDisplay reads RemainingSeconds and rounds upwards before formatting
minutes/seconds. For example, 59.9 seconds displays `01:00`. This intentionally
differs from the live script's floor-based display. Remaining time itself is
not rounded, and is clamped to zero by the timer.

The example also ignores non-tutorial target reports when the timer is not
running. This prevents an extra hit from starting another level transition
after a level has already ended. These two small differences are explicit
example choices, not changes to the live game.

## 5. Coroutines still have a role

The tutorial's wait for speech and the between-level sequence remain
coroutines. The main attempt timer is stopped during the transition; the new
level starts a fresh countdown afterwards. There is no coroutine running
inside CountdownTimer.

Pause/Resume are provided to demonstrate reuse but are not wired to new
gameplay behaviour in this example. Pausing this timer alone would not pause
targets, input or other coroutines. The owner must coordinate those actions.

This timer has no automatic knowledge of Unity lifecycle changes. If the
owner stops calling Tick, the remaining time stays unchanged. It resumes
advancing on the next supplied tick. Coroutines have their own lifecycle,
so hiding/disabling a controller should be handled as an explicit gameplay
operation if that is needed. A background wall-clock deadline would require
a different timing policy; this example counts supplied elapsed game time.

## 6. How this would be integrated later

Do not add the example controller alongside the live shooting controller:
they would both try to control targets, displays and scoring.

If adopting this design later, the simplest migration would be to put
CountdownTimer.cs under Assets and port the example's timer-related changes
into the existing controller. That preserves its existing script identity
and callers. Its serialized duration would need an explicit field migration
or reassignment to preserve the Inspector value.

This differently named example controller is not a drop-in replacement:
ShootScript and GameConditions currently reference the existing controller
type, and scene callbacks are assigned to that component. Replacing the
whole component would also require migrating those references and Inspector
settings. None of that has been done here.

Keeping the simple timers in the working minigames remains a reasonable
choice. This example illustrates the tradeoff: centralized timer behaviour
and easier isolated testing, in exchange for another abstraction to maintain.

## Verification

Both C# files were compiled with the project's Unity references. The plain
timer passed 23 checks using `Verify-CountdownTimer.ps1`, covering independent
instances, exact/overshooting expiry, repeated ticks, pause/resume, restart,
cancellation and invalid input. Run the PowerShell script to repeat them.
The alternative controller has not been wired into or play-tested in the scene.
