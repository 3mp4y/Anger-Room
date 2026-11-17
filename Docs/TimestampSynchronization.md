# Timestamp synchronization

The three measurement streams retain their native sampling rates and use the same
`session_id` and `session_time_s` reference. Join by time, not row number. Logger
messages are kept in their own file; acquisition/debug events are technical diagnostics. The clock
starts when `RecordingSession` is created, using Android `elapsedRealtimeNanos`.
It includes preparation, application suspension and Shimmer outages. Only the
gameplay countdown pauses. `acquisition_ready` and `gameplay_timer_started` are
separate events; subtract either event's session time when analysis needs that zero.
Wall-clock UTC is an anchor for identifying a recording, not the synchronization clock.

## Files

Inside the headset's `Android/data/<package>/files/session_<id>/` folder:

| File | Analysis use |
| --- | --- |
| `Logger.csv` | Only the original messages sent to `Logger.Log`, with `session_id`, `session_time_s` and `message` |
| `eyegaze.csv` | SDK gaze time mapped into `session_time_s`; original `pluginTime`, observation time and validity retained |
| `shimmer.csv` | Session time, EDA/PPG, connection/sequence, sensor validity/range and missing-sample flags |
| `diagnostics/shimmer_raw.csv` | Authoritative, continuously written original Shimmer records |
| `diagnostics/shimmer_aligned.csv` | Full derived alignment, fit parameters, residuals and original timing columns |
| `diagnostics/session_events.csv` | Acquisition, recovery, attempt and debug events, separate from Logger messages |
| `diagnostics/clock_sync.csv` | Eye-clock calibration probes, bracket widths and mapping IDs |
| `session.json` | Session origin, schema version 5, headset, readiness marker, CSV format and timing method/limitations |

All CSVs, including diagnostics, use UTF-8 with a BOM, semicolon-separated fields and decimal
commas, matching the user's Spanish Windows/Excel regional settings. A `sep=;`
first line tells Excel which delimiter to use. For analysis in other tools, skip
this first line and specify separator `;` and decimal `,`. Text is quoted so commas,
semicolons, quotes and line breaks in messages do not split the columns. Numeric
formatting is explicit and does not depend on the headset's language.

Alignment and analysis readers accept both this format and legacy comma-separated
diagnostics with decimal points. Diagnostics are not mixed into `Logger.csv`. The native
connection log and temporary alignment snapshots also stay inside `diagnostics`.
This layout applies to future recordings; existing recordings are not converted.

The raw Shimmer file is flushed every second. On pause or connection termination,
the app snapshots it and fits/exports `shimmer.csv` on a background task. A snapshot
contains complete flushed rows; a later completed export replaces the earlier one.
The last snapshot may be incomplete if Android kills or suspends the process before
cleanup finishes. Copy the **whole folder** and rerun the exporter after the run to
include all saved raw rows. `shimmer.csv` is a derived file and can always be recreated.
This avoids changing previously written raw timestamps as a drift estimate improves.

From the project root in **PowerShell 7**, with a copied session folder:

```powershell
./Tools/ShimmerQuestProbe/AlignSession.ps1 -SessionFolder 'C:/recordings/session_<id>'
```

It uses the same C# mapping as the app and preserves the raw file. An incomplete or
malformed raw row causes an explicit failure, leaving an existing export intact;
do not silently discard damaged rows. No waveform resampling or interpolation occurs.

## Eye tracking

Once per second, the session brackets three `OVRPlugin.GetTimeInSeconds()` calls
with headset clock reads and selects the narrowest bracket. The midpoint establishes
the SDK-to-session offset. Brackets exceeding 4 ms are rejected. Each accepted
probe receives a mapping ID. Calibration expires after three seconds and is
invalidated on application pause/resume so a suspension-related clock offset is
not reused.

`EyeGazesState.Time` and the gaze poses are taken from the same SDK snapshot.
`observation_session_s` is captured immediately after the gaze getter returns,
before raycasting and CSV formatting. The mapping is accepted only for a successful
getter, a nonzero finite SDK timestamp, a fresh calibration and a mapped time within
one second of observation. This is a domain/staleness check, **not** an accuracy claim.

Use `time_source=sdk_mapped` and the existing gaze validity flags for gaze analysis.
`sample_session_s` contains the mapped SDK timestamp. Status rows or unusable SDK
times have `time_source=observation_only`, an empty `sample_session_s`, and the
observation time in `session_time_s`. Repeated/backward SDK timestamps are flagged,
not silently removed. `gaze_age_s` can be negative for a future/predicted pose.

The bracket half-width describes clock-read scheduling uncertainty only. It does
not measure eye-camera latency or establish that the SDK timestamp is an unfiltered
exposure time. The SDK-to-gaze clock-domain assumption must be verified with actual
Quest Pro data. Quest 2 can exercise the clock probes but has no eye samples.
Head/raycast scene data still describe the Unity observation frame, not a historical
reconstruction of the scene at the eye sample time.

## Shimmer

The Java decoder retains original 24-bit ticks, unwraps rollover and preserves gaps.
Within each connection, `shimmer_elapsed_s` uses nominal 32768 ticks/second. The
native packet-completion time is captured on the Bluetooth worker before Unity's
queue is drained; Unity frame stalls therefore do not change receive timestamps.

The exporter estimates:

```text
session_time_s = clock_offset_s + clock_rate * shimmer_elapsed_s
```

For each connection it selects the minimum arrival-minus-device offset in each
two-second window. With at least 30 seconds and 12 windows, it estimates the rate
using the median pairwise slope of up to 64 evenly spaced low-delay anchors;
pairs must span at least one third of the recording. The final offset is the
minimum of `receive_session_s - clock_rate * shimmer_elapsed_s` over **all** samples.
This produces one consistent affine timeline per connection rather than jumps from
continually revised live fits. Short segments use nominal rate. Apparent rate errors
outside +/-2000 ppm are rejected and explicitly marked as nominal-rate fallbacks.
These are engineering rejection thresholds, not the device's oscillator specification.

In `diagnostics/shimmer_aligned.csv`, `sync_method`, `clock_rate`, `clock_offset_s` and `fit_span_s` make every mapping
auditable. `arrival_residual_s` reports excess arrival delay relative to the fit;
it is not the total transport latency. `absolute_error_bound_s` is deliberately
empty because no such bound has been measured. The raw file also preserves the
old first-arrival estimate as `estimated_sample_session_s` for comparison; use the
derived file's `session_time_s` for aligned analysis.

**Unknown fixed Bluetooth delay remains in the offset.** Changes in transport delay
can also bias the estimated rate. The affine model assumes approximately constant
clock rate within a connection; thermal/nonlinear drift is not modeled. Reconnection
creates a new fit and does not remove the real session gap. Do not bridge these gaps
or regard a small arrival residual as proof of millisecond synchronization.

The Shimmer3 real-world-clock command is not used: its sample counter and real-world
clock differ by an offset that is not exposed over Bluetooth. Simply treating a
real-world-clock read as the low 24-bit sample counter would produce incorrect timing.
See Shimmer's [timekeeping documentation](https://github.com/ShimmerResearch/log-and-stream-common/blob/main/docs/SHIMMER3_TIMEKEEPING.md)
and [timestamp guidance](https://github.com/ShimmerResearch/ShimmerAndroidAPI/wiki/Timestamps).

## Validation and next hardware test

For a manual Quest 2 test build in Unity, use **Tools > Anger Room > Build Quest 2
Test APK**. This sets the eye-readiness bypass and Debug mode for that APK only,
targets API 34 and uses the `com.angerroom.shimmerstreamtest` package. It restores
project settings afterward. A normal Build uses the saved production requirement
for eye tracking, which Quest 2 cannot satisfy. The readiness panel reports Shimmer
and gaze readiness independently, so a pending gaze requirement does not make a
working Shimmer stream appear stuck at "confirming samples".

`TimestampSynchronizationValidation.Run` is included in the Quest test build.
It exercises positive/negative clock drift, jitter, delayed first packets, short
segments, rejected rates, invalid/stale eye clocks, resume recalibration, reconnection
gaps, rollover fields, missing samples, French culture, repeatable exports and
preservation of a complete export when input is malformed. Existing scene/recovery
checks also run. These synthetic tests validate implementation, not hardware accuracy.

On 10 November 2026, the synchronization build passed 28 timing checks, the 50
existing scene/recovery checks and the Shimmer CSV parser checks. The Android
IL2CPP build succeeded (`Logs/timestamp-sync-build.log`), producing
`Builds/ShimmerStream/AngerRoomShimmerTest.apk`. The saved production scene was not
changed; this test APK retains the explicit Quest 2 eye-readiness bypass and Debug mode.

The earlier Quest 2 session's 5,339 rows were successfully re-exported through the
PowerShell command without modifying its original recording. Every saved raw tick,
EDA value, PPG value and missing-sample count was verified unchanged. The replay
outputs are under `Logs/TimestampSyncHardwareReplay/`. The new APK has not been
installed or hardware-tested: the user elected to test later.

For the next Quest 2 run: record at least two minutes, trigger a logged gameplay
action, disconnect/reconnect Shimmer, resume with right-controller A+B, then leave
the app normally. Retrieve the entire session folder and regenerate the export.
Check row counts, per-segment monotonic timestamps, packet gaps, fitted rate,
arrival residuals, and preservation of the pause gap against `diagnostics/session_events.csv`.

The 11 November CSV cleanup has separate code-level checks in
`RecordingFileValidation.Run` and `Tools/ShimmerQuestProbe/ValidateRecordingCsv.ps1`.
These cover Logger separation, delimiter/encoding, numeric precision, quoted text,
gaze columns, Shimmer validity/gap preservation and retention of technical data.
It does not retrofit the old APK or modify existing recordings.

On Quest Pro, use a production build requiring eye readiness. Check accepted clock
probes, `sdk_mapped` gaze rows, timestamp age/repetition and behavior after suspension.
Quantifying the remaining cross-sensor offset requires a common externally observed
timing stimulus; simultaneous manual starts or physiological responses cannot by
themselves establish the absolute synchronization error.
