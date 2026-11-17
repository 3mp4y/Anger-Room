# Shimmer EDA and PPG reception in Unity

This integration receives **EDA (skin conductance)** and the **optical PPG
waveform** directly from a paired Shimmer3 GSR+ over the headset's Bluetooth
Classic connection. The PC is only used for building, installing and retrieving
recordings. The scene now has a shared recording session, startup readiness gate,
and recoverable Shimmer interruption handling. Logger events, eye tracking and
Shimmer now share session seconds. See [Timestamp synchronization](TimestampSynchronization.md)
for the mapping methods, exports, recovery procedure and accuracy limits.

## Session readiness and recovery

For simulator tests, select **Recording Session > Recording Session (component)**
and uncheck **ShimmerNeeded** before starting Play mode or building. It defaults
to true and is captured once at session startup. When false, no Bluetooth request,
connection, retry, Shimmer data file, or Shimmer recovery interruption occurs.
Logger and available eye tracking still record normally. Session metadata includes
`shimmer_required: false` and `shimmer_mode: "Shimmer disabled for testing"`.

**Require Eye Tracking** remains a separate setting on **Acquisition Gate**. Turn
it off separately for a simulator without gaze support. Changing ShimmerNeeded
during a run has no effect; restart the session. Restore it for sensor recordings.

`Recording Session` in `AngerRoom.unity` owns `RecordingSession`, `ShimmerReceiver`
and `AcquisitionGate`. The two lights inside `Start/StartBulb` are saved disabled.
They turn on after a second of fresh Shimmer samples and `EyeStateLogger.HasStableData`.
The existing eye settings remain `initialDelaySeconds=4` and `readyAfterSeconds=1`.
Production requires eye tracking; Quest 2 cannot satisfy that requirement.

A missing sample stream for one second (configurable), a terminated connection,
or application suspension pauses gameplay. Socket errors may arrive sooner.
Detection is not instantaneous: the event marks detection, not the exact radio failure.
The current minigame is canceled, including pending level transitions; it costs no
attempt or points. Completed games and rewards remain intact. The overall timer
preserves its remaining value; time already spent on the canceled attempt is not
refunded. The player is returned from the labyrinth to the room. Interrupted
tutorials become available again. Visual animations continue; scoring and gameplay
inputs are blocked until recovery.

The headset-facing status panel reports the outage. Connections are retried every
five seconds once the previous worker has finished. After fresh samples have been
stable for one second (and eyes are ready in production), release both buttons,
then press **A + B together on the right controller** to resume. A alone, B alone,
left-controller buttons, or a chord held through reconnection cannot resume.
Start the canceled minigame again with its normal Start button; it restarts at level
one. This is deliberate confirmation, not automatic gameplay resumption.

While that panel is visible, the interactive dialog canvases are temporarily
hidden (rendering only). Their GameObjects, sequence position and animations are
preserved. Each canvas's prior enabled state is restored on resume, including
canvases that were already disabled. The status canvas also has sorting order
32760; hiding the covered canvases prevents world-space depth occlusion that
sorting order alone would not resolve.

Readiness confirms delivery of packets, not good skin contact or physiological
signal quality. A blocked permission or a sensor still connected to another client
needs user correction. No repeated permission prompts are issued after denial.

## Debug controls

`GamesManager.enableDebugOperations` defaults to false. When enabled, a continuous
five-second right-grip hold skips only the opening host dialogs: it stops narration,
hides every dialog object, enables their normal scene targets and starts the overall
timer once. It does not bypass acquisition readiness or change minigame tutorials.

`DebugDialogSetup.Configure` defines three plain right-ray-clickable buttons for
skipping the cards, shooting and labyrinth tutorials individually. They appear only
in Debug mode after the opening dialogs finish or are skipped. A skip unlocks the
selected game's normal Start button without changing points, attempts or results;
it can finish an active tutorial but cannot cancel a scored attempt or affect another
active minigame. Holds are reset by release or acquisition blocking. Debug actions
are recorded in `diagnostics/session_events.csv`. The dedicated Quest 2 recovery build enables Debug
mode explicitly; normal builds use the Inspector setting.

The three buttons and dialog-completion references are saved in the existing scene.
`DebugDialogSetup.ConfigureAndBuild` refreshes the wiring and builds the Quest 2
recovery APK. `SessionRecoveryValidation` passed 50 checks, including prior canvas
visibility restoration, the five-second grip, A+B confirmation, tutorial unlocks,
and preservation of time, points and attempts. Physical controller/UI and sensor
interruption tests are still required for the latest combined build.

## Session files and timing limits

Each run creates `session_<id>/` under `Application.persistentDataPath`:

- `session.json`: ID, UTC/monotonic anchor, headset, readiness marker and timing method.
- `Logger.csv`: original gameplay Logger messages, session ID and session seconds only.
- `eyegaze.csv`: mapped SDK timestamps, observation timestamps, validity and gaze.
- `shimmer.csv`: derived acquisition-time estimates, EDA/PPG and sensor validity/gap flags.
- `diagnostics/shimmer_raw.csv`: continuously saved original records.
- `diagnostics/shimmer_aligned.csv`: full alignment parameters, residuals and original ticks.
- `diagnostics/session_events.csv`: acquisition, attempt, debug and recovery events.
- `diagnostics/clock_sync.csv`: bracketed eye SDK clock reads and mapping IDs.
  `diagnostics/shimmer_raw.txt` retains native connection diagnostics.

All CSVs, including diagnostics, use semicolons, decimal commas, UTF-8 BOM and an explicit
`sep=;` line for the user's Excel settings. Alignment and analysis readers also accept
legacy comma-separated diagnostics. Existing recordings are not modified.

The recording clock continues through pauses; only the gameplay timer pauses.
Readiness and the later gameplay timer start have separate markers. Gaze coordinates
and `pluginTime` now come from the same SDK snapshot. Numeric output uses explicit
formatting independent of headset locale. Eye SDK acquisition-clock mapping remains to be validated on Quest Pro.

On each Shimmer reconnection a new `connection_segment` is appended to the raw CSV;
sequence numbers and device elapsed time restart within that segment. The export
fits a separate clock rate and arrival-envelope offset for each connection.
It is refreshed on pause/disconnection; it can also be regenerated on the PC.
**Fixed Bluetooth delay remains unknown**, so this is software alignment, not
verified millisecond synchronization. Original ticks and receive times are retained.

## Quest 2 hardware validation — 10 November 2026

The **full Anger Room Unity scene** received EDA and optical PPG directly from
Shimmer3-6033 (LogAndStream 1.1.5) on Quest 2 / Android API 34. The 90-second
test stopped normally and the saved CSV was retrieved over USB.

| Check | Observed result |
| --- | --- |
| Samples received by Unity and saved | 11,530 paired EDA/PPG rows |
| Device-time duration | 90.0703125 seconds |
| Observed sample rate against device clock | 128 Hz |
| Device tick spacing | Exactly 256 ticks for every interval |
| Sequence gaps / estimated missing samples | 0 / 0 |
| Native queue drops | 0 |
| Non-increasing timestamps | 0 |
| EDA conversion-invalid rows | 107 (0.93%) |
| PPG ADC-invalid rows | 253 (2.19%) |
| GSR range changes | 3 |

Visual inspection of the saved trace shows a repeating PPG pulse waveform and
varying EDA. There are also low/zero readings, some flattened PPG peaks, and an
abrupt EDA change around 45 seconds. These are signal-quality observations;
sample delivery remained continuous. This test establishes reception and saving
inside Unity, not physiological accuracy, artifact-free signals, synchronization,
or Quest Pro compatibility.

The participant confirmed deliberately moving/adjusting the sensors to test the
signals during this run. The observed disturbances are consistent with that
report; they were not accompanied by missing device samples.

Local, gitignored evidence:

- `Logs/ShimmerStream/Quest2Recording/shimmer_20261110_165145_871_c3e5e323.csv`
- Matching `.txt` log, including native and Unity final sample counts.
- `Logs/ShimmerStream/Quest2Recording/summary.json`
- `Logs/ShimmerStream/Quest2Recording/eda-ppg.png`

An initial incremental APK failed while loading cached scene data (`level0`,
position out of bounds). A successful **clean build** resolved that startup
failure. For reproducing this test from the copied project/cache, use
`AngerRoom.Shimmer.Editor.ShimmerTestBuild.BuildClean` in place of `Build` below.

## Components

- `Assets/Plugins/Android/ShimmerStream.java`: background Bluetooth worker,
  device/firmware checks, configuration readback, bounded sample queue and stop.
- `Assets/Plugins/Android/ShimmerStreamDecoder.java`: packet layout validation,
  EDA/PPG conversions, timestamp rollover and gap estimates.
- `Assets/Shimmer/ShimmerReceiver.cs`: Unity lifecycle, permissions, sample events,
  live status panel and CSV recording. Public `StartRecording`, `StopRecording`,
  `Latest`, `ReceivedSamples`, and `SampleReceived` expose the current connection.
- `Assets/Shimmer/Editor/ShimmerTestBuild.cs`: builds the existing Anger Room scene
  with a separate Android application ID and an explicitly labelled Quest 2 recovery mode.

## Test build

The test build is named **Anger Room Shimmer Test** and uses package
`com.angerroom.shimmerstreamtest`. Use `ShimmerTestBuild.BuildRecoveryQuest2` for
the recovery test. Its `ACQUISITION_RECOVERY_QUEST2_TEST` define bypasses the eye
requirement only in that APK and labels it in the status panel. It records until
the application is closed, across connection segments. The saved scene continues
to require eyes. The normal application ID, product name and SDK setting are
restored after building; the test targets the locally installed Android SDK 34.
The earlier 90-second hardware test above predates this session integration.

Keep the Shimmer undocked, switched on, idle, and disconnected from Consensys or
other Bluetooth clients. Pair it with the headset first using the connection-test
app or Quest Bluetooth settings. Connect the optical probe to its 3.5 mm socket
and attach both the optical probe and EDA electrodes to obtain physiological
signals. Allow **Nearby devices** when the Unity test app requests it.

The world-space panel shows acquisition and recovery status. Pausing the app stops
the connection and requires confirmation after recovery. Close and reopen the app
for a new session. The receiver is already wired into the saved production scene.

## Device configuration

The worker requires Shimmer3 hardware ID 3 and LogAndStream firmware ID 3, major
version 1; the device used in the connection test reports **1.1.5**. Shimmer3R
and legacy BtStream layouts are deliberately rejected.

Configuration:

| Setting | Value |
| --- | --- |
| Sensors | GSR + internal ADC13 (optical PPG) |
| Sampling divider | 256, giving nominal 128 Hz |
| GSR range | Automatic, with the actual range decoded in each sample |
| Expansion power | Enabled, required for the optical probe |
| Packet CRC | Off (the default after a completed disconnect on this firmware) |
| SD recording | Not started |

**Sensor/rate/range/power settings persist on Shimmer.** The previous inquiry is
saved to the session log for reference; this prototype does not restore it on
disconnect. Reconfigure in Consensys if a later experiment requires other
settings. The worker refuses to reconfigure an actively recording/streaming or
docked unit. It reads the inquiry again after configuration and requires both
expected channels, power on, 128 Hz and one sample per packet. Unexpected bytes or
unsupported layouts stop acquisition rather than silently guessing offsets.

The current connection assumes a fresh Bluetooth session with CRC off. A stale
CRC setting from another client will produce a protocol error; fully disconnect
the old client and reconnect the device. No firmware flashing is performed.

## Output and interpretation

The integrated scene writes the session files listed above. When the receiver is
used alone without `RecordingSession`, it instead creates
`shimmer_<UTC date>_<unique id>.csv` and a matching `.txt` diagnostic log in
`Application.persistentDataPath`; that standalone mode has no cross-stream alignment.
The raw CSV is flushed every second and on pause/stop.

| Columns | Meaning |
| --- | --- |
| `sequence` | Count of decoded packets in this connection |
| `shimmer_ticks_24`, `shimmer_ticks_unwrapped` | Original 24-bit device tick value and rollover-expanded ticks |
| `shimmer_elapsed_s` | Time from the first received device sample, at nominal 32768 ticks/s |
| `android_receive_elapsed_ns`, `android_receive_utc_ms` | Headset time when the complete packet was read; includes Bluetooth buffering/delay |
| `gsr_raw_word`, `gsr_adc`, `gsr_range` | Original GSR word, ADC field, and actual feedback resistor range |
| `eda_us` | Unfiltered skin conductance in microsiemens; empty when conversion is invalid/saturated |
| `skin_resistance_kohm` | Appended to the user-facing `shimmer.csv`: per-sample skin resistance, `1000 / eda_us`; empty for invalid, nonpositive or nonfinite conductance |
| `eda_conversion_valid` | ADC is within the numeric conversion range; **not** proof of electrode contact or signal quality |
| `gsr_range_changed` | Range differs from the preceding sample; settling artifacts may extend beyond this sample |
| `ppg_raw`, `ppg_mv` | Optical ADC counts and voltage; **not** heart rate, oxygen saturation or an assessed pulse |
| `ppg_adc_valid` | ADC value is not at a rail or outside 12-bit range; **not** a signal-quality verdict |
| `estimated_missing_samples` | Estimated omitted sampling intervals since the previous packet |

PPG voltage is `raw * 3000 / 4095` mV. EDA conversion uses the per-sample range:

```text
feedback_ohms = [40200, 287000, 1000000, 3300000][range]
eda_us = ((gsr_adc * 3000 / 4095 / 500) - 1) * 1000000 / feedback_ohms
```

Raw values are always retained. No filtering, pulse detection, motion-artifact
removal or range-transition correction is performed. Clock alignment changes
only derived timing columns; it never filters, interpolates or resamples signals.
The 24-bit device clock wraps about every 512 seconds; the decoder unwraps it
within a connection. Duplicated/backwards timestamps are rejected. Gap counts
are estimates based on the configured sample interval, not hardware sequence
numbers. Native queue overflow is reported separately in the log.

## Rebuild and checks

Run Unity 2022.3.45f1 in batch mode against this project with:

```text
-batchmode -nographics -quit -buildTarget Android
-executeMethod AngerRoom.Shimmer.Editor.ShimmerTestBuild.BuildRecoveryQuest2Incremental
```

Output: `Builds/ShimmerStream/AngerRoomShimmerTest.apk`.

The combined recovery/debug build succeeded and was installed on the connected
Quest 2. Build/check log: `Logs/session-debug-build.log` (50 checks passed).
The launched app recorded session `20261110_180223_677_03867d34`, confirmed Shimmer
readiness and logged the physical five-second grip dialog skip. A snapshot under
`Logs/SessionRecovery/` contains 5,339 Shimmer samples and the shared session files.
No Unity runtime exceptions were observed in this launch. On 10 November 2026,
the user confirmed all three requested Quest 2 checks: the Shimmer recovery panel
is unobstructed while a dialog is active; the dialog returns after reconnection
and A+B confirmation; and the five-second right-grip skip dismisses the dialogs
and reveals the three tutorial-skip buttons. In a subsequent test report that day,
the user also confirmed that the tutorial-skip buttons work and that Shimmer
interruption during minigames works. These are user-reported hardware confirmations;
the automated checks separately cover preservation of time, attempts and points.
Quest Pro eye-tracking readiness and the new clock mapping still require a hardware
test. The earlier hardware reports above predate the timestamp synchronization build.
Use `BuildRecoveryQuest2` instead for a clean build, or
`DebugDialogSetup.ConfigureAndBuild` to refresh debug scene wiring first.

`Tools/ShimmerQuestProbe/tests/StreamDecoderTest.java` tests the Java conversion
and framing logic with synthetic packets, reversed inquiry channel order,
timestamp rollover, gaps and invalid readings. `ShimmerTestBuild.ValidateParser`
checks Unity CSV parsing under French numeric culture, including empty invalid
EDA fields. Hardware tests must also verify actual sample continuity and inspect
the recorded PPG waveform; passing parser checks does not establish signal
quality or compatibility with Quest Pro.

`Tools/ShimmerQuestProbe/AnalyzeRecording.ps1 -CsvPath <diagnostics/shimmer_raw.csv>` produces
the continuity and numeric-validity report. Run `PlotRecording.ps1` with Windows
PowerShell 5.1, `-CsvPath <recording.csv> -OutputPath <plot.png>`, to plot the EDA
recording and a ten-second PPG window using .NET charting.

## References

- [Shimmer streaming packet format and conversion equations](https://github.com/ShimmerResearch/log-and-stream-common/blob/main/docs/SHIMMER3_STREAMING_DATA_FORMAT.md)
- [Sensor enable bits and expansion power](https://github.com/ShimmerResearch/log-and-stream-common/blob/main/docs/SHIMMER3_CONFIGURATION_INFOMEM.md)
- [Bluetooth commands and inquiry format](https://github.com/ShimmerResearch/log-and-stream-common/blob/main/docs/SHIMMER3_BT_COMMUNICATION_PROTOCOL.md)

The first-party Shimmer Android SDK remains a suitable next step if wider device
or firmware support is needed. This implementation is deliberately restricted
to the two channels and hardware being tested.

## Startup settling and signal readiness (policy 1)

Every Bluetooth connection retains all samples from the first packet. Startup waits
at least 5 seconds of both device and receive time, and for a 2-second continuous
window of fresh, advancing samples. The window must have finite EDA in 0.2–125 µS,
PPG between 50 and 2950 mV, and PPG 5th-to-95th percentile spread of at least 10 mV.
GSR range changes restart confirmation after a 0.5-second settling allowance.
These are startup plausibility checks, not a validated pulse or contact-quality
classifier. EDA is allowed to vary naturally; a constant EDA baseline is not required.

The receiver Inspector exposes Startup Settling Seconds (minimum 5), Signal
Confirmation Seconds (minimum 2), and Minimum Ppg Span Milli Volts. Settings are
captured per connection and logged with the policy version. Eye tracking is separate;
ShimmerNeeded=false skips this gate along with Shimmer acquisition.

`startup_settling`, `signal_check_passed`, and `startup_status` are appended to raw,
aligned, and main Shimmer CSVs. Settling is true until the first ready sample in
that connection, including any extra time spent waiting for acceptable signals.
No samples are dropped, filtered, interpolated or overwritten. Later signal faults
are reflected in signal_check_passed/status without reopening the startup gate.
Legacy recordings have blank markers: readiness was not assessed for them.
`shimmer_settling_started`, `shimmer_settling_completed`, and, for connections ending
early, `shimmer_settling_incomplete` are logged in session_events.csv. Completion
records the first ready device time and sample sequence. Schema version is 5.

All CSVs, including diagnostics, use UTF-8 BOM, sep=;, semicolon fields and decimal
commas. Readers accept legacy comma/decimal-point files. The main shimmer.csv also
includes skin_resistance_kohm = 1000 / eda_us for valid positive conductance.
Anger Room waits for readiness before startup or the existing manual Resume step.
Loss of transport still pauses/cancels the attempt; startup plausibility does not add a new mid-game pause rule.

