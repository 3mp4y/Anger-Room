The gun minigame now uses the same session language and text catalogue as the card minigame. Its timer, level, score, tutorial labels, transition countdown, victory and failure messages are localized. The child text under `EndTut` is also localized. Language remains fixed for each Play session.

The localization and explicit movement configuration for each spawned target are implemented. The other gameplay changes below remain review topics. Firing between levels and the current EndTut raycasts are intentionally retained: the room layout prevents the target overlap described in the original review. Shared anger control and insult gating are implemented; see InsultConditions.md. The shot-failure percentage correction is also implemented.

**Unity setup and checks**

The saved `Assets/Plugins/Scenes/AngerRoom.unity` scene already contains the two new assignments on **Minigame 2 - Shooting > GunPuzzle**:

| Field | Assigned object |
| --- | --- |
| Text Adapter | The existing TextAdapter on AudioManager |
| End Tutorial Text | EndTut / Text (TMP) |

No extra component or catalogue asset is needed. If the scene is open in Unity while files change, exit Play Mode and allow Unity to import and reload the saved scene. If you have unsaved scene edits you need to keep, retain them and assign those two fields manually before saving, rather than replacing your unsaved scene state. The three original TMP references remain assigned.

1. Select a language on **AudioManager > LanguageAdapter** before pressing Play.
2. Start the gun tutorial. Check the level, target-score and tutorial-timer labels.
3. After the narration ends, check the visible EndTut label. Shoot it to end the tutorial.
4. Start the real game. Check the score after a hit, the level label, the next-level countdown and the victory message.
5. In a separate run, let the timer expire. Check the two remaining-attempt messages and the third-attempt disabled message.
6. Repeat in the other languages, checking accents and wrapping at the actual headset viewing distance.

The new `EndTutorial` entry has stable ID **25** in `TextId.cs`. Its translations preserve the two-line presentation:

| English | Italian | Spanish |
| --- | --- | --- |
| End / Tutorial | Termina / il tutorial | Finalizar / tutorial |

Here the slash represents a line break. Edit these strings in **GameTexts > Entries > EndTutorial**. Use an actual line break in the Inspector. The adapter caches translations, so restart Play Mode after editing wording or the selected language.

`GunPuzzle.Start()` assigns the EndTut string through a direct `TMP_Text` reference even while its parent GameObject is inactive. This avoids waiting for a component on the hidden object to initialize. `TMP_Text` is the shared base type for TextMeshPro's world and canvas text components; the scene currently uses a world-space TextMeshPro component.

The existing `GamesManager.addTries` overload receives the same adapter for gun failures. Its attempt counting, score arithmetic and timing are unchanged. No new manager dependency was necessary.

**Files reviewed and responsibilities**

The saved scene uses `GunPuzzle.cs`, `ShootScript.cs`, `Spawn2.cs`, and `TargetMover.cs`, plus `Shooter/Target.cs` on the spawned target prefab. The scene's GunPuzzle and Spawn2 both reference `Shooter/ShootingTarget_Collider Variant.prefab`.

The older `Shooter/PewGun.cs`, `Shooter/TargetSpawner.cs` and `Shooter/GameManager.cs` form another implementation. Their script GUIDs were not found in the saved scenes or prefabs searched. They are not the implementation wired to this minigame. Keep them preserved, but clearly identify them as legacy so future work does not accidentally modify the wrong system.

**Fix first: spawning resumes after victory**

In `GunPuzzle.CoroutineHit()`, the victory branch hides the real gun and displays the victory text, then calls `Spawner.StartSpawning()`. That starts new targets after the game is over. This behavior was also reproduced with the real GunPuzzle code in the isolated checks.

The proposed fix is to finish in an idle/completed state with spawning stopped and targets cleared. Put that cleanup in one completion method shared by the appropriate exit paths. A completed activity should not retain a background spawning process.

**Make the gun's activity state explicit**

`started` currently controls the countdown, while `tutorial` controls hit scoring. `CoroutineHit()` accepts a hit whenever `tutorial` is false, including when gameplay has stopped. `ShootScript.Update()` has no check for a level transition or completed game; it relies on the gun object's activation. During a level transition the real gun remains active, so it can still fire and play miss reactions. The timeout condition also checks time and tutorial mode without requiring `started`.

I recommend `Idle`, `TutorialNarration`, `TutorialPractice`, `Playing`, and `LevelTransition` states. Only `Playing` should accept score changes and timeouts. Tutorial practice should accept shots without changing the real score. Entering an exit state should cancel any pending tutorial/transition routine that could later reactivate gameplay.

This extends the approach used for the card fix. State transitions would explicitly start and stop input, spawning and timing together, rather than relying on several methods to update separate flags consistently. `StartGun()` should establish its complete initial state; currently it relies on `Endtutorial()` having already cleared the tutorial flag.

**Resolve the nearest valid shot hit once**

`ShootScript.Shoot()` performs separate raycasts for regular targets and EndTut. During a tutorial it always prioritizes the EndTut result. Consequently, if a regular target is in front of EndTut along the ray, the farther EndTut can still end the tutorial. Objects excluded from both masks cannot block either ray.

Use one raycast covering the intended hittable objects and any objects that should obstruct shots, then handle the nearest collider according to its role. Decide explicitly which scenery blocks a shot. If separate raycasts are retained, compare their hit distances and account for blockers.

The target lookup also assumes `hit.transform.GetComponent<Target>()` succeeds. A collider on the target layer but without that component on the same GameObject causes a null reference. Resolve the Target on the collider or an appropriate parent, check the result, and handle a non-target collider deliberately. The current target root has both the Target component and a collider; this recommendation protects against changes to prefab hierarchies and layers.

**Make a hit a single accepted action**

`Target` declares `_hasBeenHit` but never uses it. Its `OnHit()` has no guard against repeated calls. A `TryHit()` method that marks the target hit and returns whether this was the first accepted hit would let ShootScript award credit exactly once. Disable the collider immediately on acceptance, then perform effects and cleanup. This is defensive hardening for multiple callers in the same frame. With the current single-gun input path and 1.1-second cooldown, a successfully hit target is destroyed before the next normal shot, so ordinary firing does not reproduce this concern.

`Target.OnHit()` also plays the hit sound on the target's own AudioSource and immediately destroys that object. The sound can be cut off with its source. A separate sound-emitter object or shared effects player should own the sound for its duration. If a hit VFX prefab is assigned later, give that effect its own cleanup policy too. The currently assigned target has a hit sound but no hit VFX prefab.

**Shot failures now use a true percentage**

The old `Random.Range(0, 101)` provided 101 possible integers. ShootScript now uses `Random.Range(0, 100)`, producing 0 through 99, and retains the success test `roll >= failureChance`. A configured 10 means ten failure outcomes out of one hundred; 0 never deliberately fails and 100 always deliberately fails.

This correction applies to the deliberate failure of a shot that has hit a target during real gameplay. Ordinary geometric misses, tutorial hits, and the configured level percentages are unchanged.

`ShootScript.FailureChance` already clamps values, but GunPuzzle writes directly to the public `failureChance` field, bypassing the property. Keep the backing field private and serialized, and route runtime changes through the property or a configuration method. This is encapsulation: one place owns the rule that the value must stay between 0 and 100.

**Movement configuration implemented on 27 October 2026**

The old `GunPuzzle.targetMover` reference to the prefab component has been removed from the script and saved scene. No replacement Inspector assignment is needed: GunPuzzle already references Spawn2, and Spawn2 already references the target prefab containing TargetMover.

The data flow is now explicit:

1. GunPuzzle calls `ConfigureTargetMovementForCurrentLevel()` before starting each gameplay level. It calculates the settings from the level number, so retries do not inherit the previous attempt's difficulty.
2. That method calls `Spawner.ConfigureMovement(minimumSpeed, maximumSpeed, interval)`. Spawn2 stores those values for future targets; it does not change any target or prefab at this point.
3. `SpawnTarget()` creates an instance of the prefab, retrieves that instance's TargetMover, and calls `SetSpeeds()` on it. The prefab's own component is never modified.
4. TargetMover initializes its speed and direction in `Start()`. Spawn2 has already supplied the settings by then, so the first movement uses the intended range. Existing targets retain their own settings if the spawner is configured again.

The preserved settings are: stationary targets during the tutorial; level one 0.2-0.6 units/second with a new speed every 2 seconds; level two 0.6-1.5 every 0.8 seconds; level three 1.0-2.4 every 0.32 seconds. These settings remain identical in the control and anger versions. The source comments explain ownership, initialization order, and retry behavior at each relevant method.

Spawn2 reports a setup error and stops spawning if no movement configuration has been supplied or if the spawned prefab has no TargetMover on its root. The assigned prefab already satisfies this requirement.

A separate future improvement could put duration, target count, spawn interval and movement values into an Inspector-editable list of level settings. That has not been implemented: the existing later-level `time_left = 21` and the other gameplay timings remain unchanged.

**Validate spawning inputs and manage target lifetime records**

`Spawn2.GetRandomTransformYValue()` assumes at least one row remains after excluding recent choices. Zero available rows causes an index error. The saved scene has six rows and excludes three, which is valid; the problem arises with settings such as one row excluding one, or zero rows. Validate row count and exclusion count, and provide an explicit fallback if every row is excluded.

The existing history logic does remove its oldest value once the count exceeds `_excludeCount`. The empty-list case happens earlier: with two total rows and an exclusion count of two, both rows enter the history, its count equals two (so nothing is removed), and the next selection has no candidates. It fails before reaching the removal. Six rows excluding either two or three are both valid. The movement update does not change this algorithm or the Inspector exclusion value; you can test the value two independently.

`Start()` appends generated positions to a serialized list. If a designer populates that list in the Inspector, generated entries are added too. Choose either generated positions or an intentionally authored list. For generated positions, keep the list private runtime data and rebuild it from row count and spacing.

Destroyed targets remain in `_spawnedTargets` until `ClearTargets()`. Remove expired entries or unregister targets when they are destroyed. This is currently a modest bookkeeping issue because levels clear the list. If profiling on the headset shows allocation spikes, consider pooling targets and shot trails; at the current spawning frequency, pooling should follow measurement rather than being the first change.

**Smaller maintainability improvements**

- `Shoot()` sets the reload flag but does not enforce cooldown itself; only Update checks it. A future UnityEvent or another script calling Shoot directly could bypass the cooldown. Put permission checks in the public action as well as input polling.
- Shot trails are created only in the tutorial branch. Confirm whether hiding them during real gameplay is intentional before changing this. If both modes should show them, share one trail-rendering method.
- `Begin()` records the active grandchildren it hides, but `Reset()` enables every object in `ObjectsToDeactivateWhilePlaying`, even if an object was already inactive beforehand. Record and restore prior activation state consistently.
- Use descriptive names and PascalCase public methods, such as `TargetSpawner`, `ShotOrigin`, `EndTutorial`, and `SetSpawnInterval`. Preserve serialized field names with migration attributes and update UnityEvent bindings if renaming methods. Avoid a broad rename until behavior is covered by tests.
- Remove duplicated imports, obsolete commented implementations and unused fields from active scripts after preserving their old versions. `Target.scoreValue` is currently unused; the game counts accepted hits instead. Make that scoring contract explicit.

The victory spawning restart is confirmed unwanted and remains a separate pending fix. State restructuring and the EndTut raycast change are not required for the intended between-level shooting behavior and current layout. Row validation and hit handling remain proposals. The percentage calculation has been corrected.

**Validation and preserved files**

For the localization integration on 26 October, the updated Assembly-CSharp sources compiled against the installed Unity 2022.3.45f1 libraries with zero errors. An isolated harness passed 87 assertions using the actual GunPuzzle, GamesManager and localization classes with engine/audio/spawner substitutes. It covered all three languages, the inactive EndTut label, tutorial completion, score and level changes, countdowns, victory, all three failed attempts, and fixed session language. It also reproduced the pre-existing victory spawning restart for the review.

That localization change only added the two GunPuzzle reference fields to the scene. Existing catalogue entries were preserved and the new ID is present once. Dialogue objects, audio scripts, shooting/spawning/movement/target code, and the latest card fix were not modified in that step. Actual Unity rendering, VR input, physics, and translated label fit still require the Play Mode/headset checks above.

Exact pre-change copies of the five edited files and their metadata are in `Backups/GunTextLocalization/2026-10-26`, with a hash manifest. These copies stay outside Assets to avoid duplicate C# classes and metadata GUIDs. Restoring that saved scene later would also replace any scene edits made since the backup, so restore individual changes deliberately.

The movement update has its own preserved copies in `Backups/GunTargetMovement/2026-10-27`. Its isolated checks passed 133 assertions using the real GunPuzzle, Spawn2 and TargetMover with engine doubles: they cover tutorial/level/retry settings, the first chosen speed, independence of targets and spawners, unchanged prefab values, invalid configuration reporting, and valid row histories of both two and three. Actual Unity lifecycle and visible motion still need a Play Mode check: select a spawned target instance and inspect its TargetMover values during the tutorial, each level, and a retry.
