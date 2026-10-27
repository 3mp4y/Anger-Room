The labyrinth now uses the shared TextAdapter and GameTexts catalogue. Language is selected before Play and stays fixed for that session. Dialogue objects retain their separate language variants.

**What is localized**

- The running level/time display, victory, remaining-attempt and game-disabled messages.
- The points label updated by GamesManager after a failed attempt.
- The three signs inside the mazes: Level 1, Level 2 and Level 3.
- The tutorial sign and the entrance button's initial Tutorial label.
- The entrance button's later Start label and the tutorial's Timer label, which were previously English strings in Inspector events.

The catalogue already contained LabyrinthStatus, Victory, AttemptFailed, GameDisabled, Points and TimerLabel. New entries are TutorialLabel (26), StartLabel (27) and LevelNumber (35). Tutorial is currently spelled the same in all three languages, but having an entry allows its wording to be edited consistently later. LevelNumber uses `{0}` for the sign's level. LabyrinthStatus uses `{0}` for level, `{1:00}` for minutes and `{2:00}` for seconds. Keep these placeholders when editing translations.

**Unity setup and verification**

The saved AngerRoom scene is already wired. No new component is required. On `GameComponents / Minigame 3 - Labirinth / LabyrinthScript`, the added fields are:

| Inspector field | Assigned component |
| --- | --- |
| Text Adapter | Existing TextAdapter on AudioManager |
| Start Button Text | StartLabirinthButton / Text (TMP) |
| Tutorial Sign Text | LabirintoTutorial / Structure / Text (TMP) |
| Level Signs, element 0 | Labirinto1 / Structure / Text (TMP) |
| Level Signs, element 1 | Labirinto2 / Structure / Text (TMP) |
| Level Signs, element 2 | Labirinto3 / Structure / Text (TMP) (1) |

The original TMPTimer and Congrats assignments are retained. The Level Signs array order determines the numbers printed on the signs; it does not control gameplay progression.

On `StartLabirinthButton / [BuildingBlock] TUTORIAL_Lab`, two existing **When Unselect** listeners now call `LabyrinthScript.ShowStartButtonLabel()` and `LabyrinthScript.ShowTutorialTimerLabel()`. They replace the two TMP `text` setters containing Start and Timer, at the same positions in the event list. Other listeners and their ordering are unchanged. The return-to-theatre button still clears the timer with its existing empty-string event.

1. Exit Play Mode and let Unity import the changed files. Reload the saved scene. If you have unsaved scene work to retain, keep it and reproduce the assignments and two event replacements listed above before saving.
2. On AudioManager's LanguageAdapter, select English, Italian or Spanish before pressing Play.
3. Enter the labyrinth tutorial. Check the tutorial sign, timer label and the entrance button's change to Start / Inizia / Empezar.
4. Enter the actual mazes and inspect each level sign and the countdown. The existing level-3 status problem described below remains a separate gameplay issue.
5. Finish the last maze and check the victory message.
6. In a fresh run, let the timer expire three times. Check the remaining-attempt messages, updated points and final disabled message.
7. Repeat for the other languages. Check wrapping and accents in the headset, especially the longer Italian level signs. Restart Play Mode after changing either language or catalogue wording.

**How the files connect**

LanguageAdapter supplies the selected language to TextAdapter. TextAdapter selects strings from GameTexts, using the stable numeric keys defined in TextId. LabyrinthScript requests those strings and writes them to TMP components. `Start()` assigns static labels once, including references to inactive objects. `Update()` supplies the changing level/minutes/seconds values to LabyrinthStatus. On timeout, `gm.addTries(2, congrats, textAdapter)` gives GamesManager the same translator for its existing failure messages and points update.

The two public label methods exist because UnityEvents need callable component methods. They change only displayed text. Keeping English wording out of the event arguments prevents the Inspector from overwriting translated labels later.

**Review and follow-up decisions**

1. **The status display does not advance to level 3.** FinishButton1 calls `setLevel(2)`, but FinishButton2 sets the time to 180 seconds and teleports to maze 3 without calling `setLevel(3)`. Consequently the status display can still say Level 2 while the physical sign correctly says Level 3. The smallest correction is to add `setLevel(3)` before the second button's teleport. A more maintainable design would expose one `EnterLevel` method that applies the number, time, destination and anger obstacles together. This makes a partially configured transition harder to create. The current events have been preserved for this localization change.

2. **Laser fix implemented.** `LaserSparatoLabirinto.Shoot()` now performs one raycast with `Physics.AllLayers`. The nearest hit determines the beam endpoint; only that collider's layer is checked against the Player mask. A wall hit therefore prevents a farther player from being reset, and a player can be hit without requiring a separate scenery hit. With no hit, the beam extends to maxLineDistance. Trigger handling continues to follow the project physics setting. The unused Any field was removed; existing Player mask assignments are retained. Comments explain why the raycast is shared and how the bitmask check works. The actual project compiled with zero errors after this change; physics behavior still needs a Unity Play Mode check.

3. **The countdown runs during the three-second blackout.** `TpLabirinth()` resets time immediately and starts DarkenerTeleport, which waits three seconds. It does not pause `started`; level transitions retain its true value, and the start buttons set it true immediately after requesting the teleport. That spends roughly three seconds of the new level's time before the player arrives. If the intended allowance is playable time, introduce a transition state and start/resume the countdown after teleporting. This would also provide a natural guard against overlapping teleport requests. The chosen approach is to add three seconds to the configured timers manually; no pause flag or transition state has been added. The user will also add the missing setLevel(3) event.

4. **Make reset behavior and configuration explicit.** Lasers reset X/Z while preserving the player's height; OstacoloColpito assigns the destination's full position and rotation; DarkenerTeleport preserves height and does not rotate. These differences may be intentional, but a shared teleport/reset method with explicit options would make the policy clear. Add configuration checks for required references and for `SpawnAngerObstacles` indexes, so a missing Inspector assignment produces a useful error before interaction. Public fields can gradually become private serialized fields when outside scripts do not need to write them. Keep UnityEvent methods available, or update the scene listeners when renaming them.

`Movimento_sfera` is a small, understandable oscillator. Its PingPong phase uses global `Time.time`, so a sphere enabled later starts at the current global phase rather than necessarily at its initial position. Keep this if synchronized movement is intended; use elapsed time since activation if each activation should restart the motion. Its comment says world X, but the code uses `transform.right`, so rotation changes the movement axis. Correcting that comment would help future editing. Empty Start/Update methods and old commented-out experiments in OstacoloColpito can also be cleaned up separately without changing behavior.

**Validation and preserved files**

The actual project compiled with the installed Unity C# compiler with zero errors. Forty-five isolated checks exercised the actual labyrinth and localization source in all three languages: startup signs, the two event methods, countdown formatting, fixed session language, victory and passing the shared adapter to GamesManager exactly once on timeout. These checks use Unity/manager doubles; they do not exercise VR interaction, physics, rendering or audio in Play Mode.

The scene comparison allows changes only to the labyrinth component's text references and the tutorial button's two text listeners. The existing gameplay events and Dialogue objects were preserved. Pre-edit copies of all five modified assets and their metadata are in `Backups/LabyrinthTextLocalization/2026-10-27`, with a SHA256 manifest. Those copies include the earlier insult-gating work as it existed immediately before this localization task.
