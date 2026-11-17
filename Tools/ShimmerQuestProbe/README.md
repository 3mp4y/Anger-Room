# Shimmer / Quest Bluetooth connection test

A small, standalone Android diagnostic app for the Shimmer3 GSR+. It uses the
headset's Bluetooth radio; the PC only installs the APK and retrieves diagnostics
over USB. No Bluetooth adapter is needed on the PC.

The app has its own package (`com.angerroom.shimmerprobe`) and does not modify the
Anger Room scenes, recording code, application package, or Unity Android manifest.
It runs as a 2D panel in Quest. The transport code is Java, suitable for adapting
into a Unity Android plugin after the connection test succeeds.

## Run the hardware test

1. Switch on Shimmer, stop recording, and disconnect it in Consensys or any other
   host. Leave the unit undocked and nearby. SD synchronization should be disabled.
2. Connect the Quest to the PC with USB and authorize USB debugging if requested.
3. Build with `./Tools/ShimmerQuestProbe/Build.ps1` from the repository root.
   This uses Unity's existing Android SDK/JDK; it downloads no dependencies.
4. Install `Builds/ShimmerQuestProbe/ShimmerConnectionTest.apk` using ADB or Meta
   Quest Developer Hub. The app is named **Shimmer Connection Test**.
5. Open it from the headset's Unknown Sources apps and allow **Nearby devices**.
6. Press **Scan / retry permissions**, then select `Shimmer3-6033` (or your own
   unit's Classic Bluetooth name). Approve pairing; Shimmer3's usual PIN is `1234`.
7. The app opens the SPP/RFCOMM socket, verifies hardware ID 3, and asks for the
   firmware version 15 times, one second apart. The socket closes after the test.

To launch and automatically select a specific advertised Shimmer3 suffix:

```powershell
$adb = 'C:/Program Files/Unity/Hub/Editor/2022.3.45f1/Editor/Data/PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe'
& $adb devices -l
# Replace HEADSET_SERIAL with the intended device from the list above.
& $adb -s HEADSET_SERIAL install -r './Builds/ShimmerQuestProbe/ShimmerConnectionTest.apk'
& $adb -s HEADSET_SERIAL shell am start -n com.angerroom.shimmerprobe/.ProbeActivity --es target_suffix 6033
```

Automatic selection occurs once per launch and only for a name starting with
`Shimmer3-` and ending in the requested suffix. Pairing still uses Android's
system dialog. To retest, select the paired device in the panel.

## Interpret results

- **RFCOMM_CONNECTED**: the serial Bluetooth transport opened successfully.
- **PASS**: hardware ID 3 and all 15 firmware replies were valid and consistent.
- **Pairing failure**: check the headset pairing dialog/PIN, distance and whether
  another host is connected. Try the app's Bluetooth settings button or the
  headset's Bluetooth settings directly.
- **Connection timeout**: pairing alone does not establish SPP support. Verify the
  Classic Bluetooth entry, disconnect other hosts, and retry.
- **Response timeout / unexpected frame**: the socket opened, but the protocol
  exchange did not validate. Save the raw response bytes; this is not a pass.
  Existing streaming, CRC settings, firmware differences or an unsolicited status
  frame may require extending the parser. The test deliberately rejects unknown
  framing instead of searching arbitrary bytes for a success pattern.
- **NACK**: the device rejected the request; check its SD synchronization setting.

The timing shown is command round-trip time, **not sensor latency or synchronization
accuracy**. A pass does not test GSR samples, calibration, packet loss under
streaming load, operation during VR gameplay, long-term clock drift, or Quest Pro
compatibility. These are subsequent integration tests. No sensor configuration,
firmware update, streaming start, or SD recording command is sent by this app.

## Retrieve logs

Every launch writes a UTC-stamped text file in the app's private `files` directory.
The debug build allows readout through `run-as`:

```powershell
& $adb -s HEADSET_SERIAL shell run-as com.angerroom.shimmerprobe ls files
# Substitute the actual filename returned above.
& $adb -s HEADSET_SERIAL shell run-as com.angerroom.shimmerprobe cat files/probe-TIMESTAMP.txt
```

The `ShimmerProbe` logcat tag also reports diagnostics where headset logcat permits
them. Logs contain device names and Bluetooth addresses. The app has no Internet
permission and does not upload results.

## Validation and next step

### Hardware result: 10 November 2026

Tested on the USB-connected **Meta Quest 2**, Android API 34, against
**Shimmer3-6033** using the Quest's Bluetooth radio:

- Pairing completed through the headset dialog (after an initial unsuccessful attempt).
- Bluetooth Classic SPP/RFCOMM connection opened successfully.
- Hardware response: `FF 25 03` (Shimmer3).
- Firmware response: `FF 2F 03 00 01 00 01 05` (LogAndStream ID 3, version 1.1.5).
- All **15/15** repeated firmware requests passed with identical payloads.
- Mean command round trip: **36.7 ms**; maximum: **63.3 ms**.
- Socket closed normally after the test.

The full local diagnostic log is saved at
`Logs/ShimmerProbe/quest2-6033-test.txt` (gitignored). This is a successful
connection test on Quest 2, not a GSR acquisition or Quest Pro validation.

### Build checks

`Build.ps1` compiles the Java app, runs a JVM test for firmware decoding and
malformed responses, builds the DEX/APK, and verifies its signature. All generated
files and the local test signing key stay in the gitignored `Builds` directory.
This is a local debug APK, not a release-signed application.

After a connection pass: implement a firmware-aware streaming client (preferably
using the Shimmer Android SDK), retain raw sample timestamps, validate GSR
calibration and packet continuity, and then add shared session timing to Unity.
Repeat the test on Quest Pro before drawing conclusions about the experiment
headset. The PC coordinator remains an alternative if direct streaming is unstable.

## References

- [Shimmer's Bluetooth protocol](https://github.com/ShimmerResearch/log-and-stream-common/blob/main/docs/SHIMMER3_BT_COMMUNICATION_PROTOCOL.md): hardware request `0x3F`, response `FF 25 <hardware>`; firmware request `0x2E`, response `FF 2F <six-byte payload>`.
- [Android Bluetooth permissions](https://developer.android.com/develop/connectivity/bluetooth/bt-permissions).
- [Android RFCOMM connections](https://developer.android.com/develop/connectivity/bluetooth/connect-bluetooth-devices).
- [Shimmer Android SDK](https://github.com/ShimmerResearch/ShimmerAndroidAPI).
