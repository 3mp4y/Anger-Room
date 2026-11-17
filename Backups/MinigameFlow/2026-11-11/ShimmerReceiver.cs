using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace AngerRoom.Shimmer
{
    /// <summary>Receives raw EDA/PPG and exports a software-aligned session timeline.</summary>
    [DefaultExecutionOrder(-400)]
    public sealed class ShimmerReceiver : MonoBehaviour
    {
        public string pairedDeviceName = "Shimmer3-6033";
        public bool connectOnStart = true;
        public bool showDiagnostics = true;
        [Tooltip("Zero records until stopped. The test build uses 90 seconds after the first sample.")]
        public float recordingDurationSeconds;

        public string Status { get; private set; } = "Idle";
        public string CsvPath { get; private set; }
        public long ReceivedSamples { get; private set; }
        public Sample Latest { get; private set; }
        public event Action<Sample> SampleReceived;
        public bool IsConnectingOrRunning => bridge != null || permissionPending;
        public bool AutoRetryAllowed { get; private set; } = true;
        public bool HasFreshSamples(float maximumAgeSeconds) => bridge != null && !stopping && Latest != null &&
            RecordingClockNanos() - Latest.ReceiveAndroidNanos < maximumAgeSeconds * 1e9;
        public int ConnectionSegment { get; private set; }
        long RecordingClockNanos()
        {
            if (RecordingSession.Instance != null) return RecordingSession.Instance.ClockNanos();
            using (var systemClock = new AndroidJavaClass("android.os.SystemClock"))
                return systemClock.CallStatic<long>("elapsedRealtimeNanos");
        }
        double segmentAnchor;
        Task exportTask = Task.CompletedTask;
        string exportError;
        bool exportReported;

        public sealed class Sample
        {
            public long Sequence, ShimmerTicks24, ShimmerTicksUnwrapped, ReceiveAndroidNanos, ReceiveUtcMs, EstimatedMissing;
            public double ShimmerElapsedSeconds, EdaMicroSiemens, PpgMilliVolts;
            public int GsrRawWord, GsrAdc, GsrRange, PpgRaw;
            public bool EdaConversionValid, GsrRangeChanged, PpgAdcValid;
        }

        const string Header = "sequence,shimmer_ticks_24,shimmer_ticks_unwrapped,shimmer_elapsed_s,android_receive_elapsed_ns,android_receive_utc_ms,gsr_raw_word,gsr_adc,gsr_range,eda_us,eda_conversion_valid,gsr_range_changed,ppg_raw,ppg_mv,ppg_adc_valid,estimated_missing_samples";
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        AndroidJavaObject bridge;
        StreamWriter csv;
        StreamWriter events;
        bool permissionPending;
        bool stopping;
        double firstSampleAt = -1;
        double lastFlush;
        double lastStatusPoll;
        UnityEngine.UI.Text hud;
        GameObject hudRoot;

        void Start()
        {
            if (connectOnStart) StartRecording();
        }

        public void StartRecording()
        {
            if (bridge != null || permissionPending) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            const string connect = "android.permission.BLUETOOTH_CONNECT";
            const string scan = "android.permission.BLUETOOTH_SCAN";
            if (!Permission.HasUserAuthorizedPermission(connect) || !Permission.HasUserAuthorizedPermission(scan))
            {
                permissionPending = true;
                Status = "Allow Nearby devices to connect Shimmer";
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ =>
                {
                    if (this == null || !permissionPending) return;
                    if (Permission.HasUserAuthorizedPermission(connect) && Permission.HasUserAuthorizedPermission(scan))
                    {
                        permissionPending = false;
                        StartRecording();
                    }
                };
                callbacks.PermissionDenied += _ => PermissionDenied();
                callbacks.PermissionDeniedAndDontAskAgain += _ => PermissionDenied();
                Permission.RequestUserPermissions(new[] { connect, scan }, callbacks);
                return;
            }
            try
            {
                string id = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", Invariant) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
                var session = RecordingSession.Instance;
                ConnectionSegment++;
                CsvPath = session != null ? Path.Combine(session.DiagnosticsFolder, "shimmer_raw.csv") : Path.Combine(Application.persistentDataPath, "shimmer_" + id + ".csv");
                bool append = session != null && File.Exists(CsvPath);
                csv = new StreamWriter(CsvPath, append);
                if (!append) csv.WriteLine(session == null ? Header : "session_id,connection_segment,receive_session_s,estimated_sample_session_s," + Header);
                csv.Flush();
                events = new StreamWriter(Path.ChangeExtension(CsvPath, ".txt"), append) { AutoFlush = true };
                session?.Log("shimmer_connection_start", "segment=" + ConnectionSegment);
                RecordEvent("Session=" + id + "; device=" + pairedDeviceName + "; headset=" + SystemInfo.deviceModel);
                RecordEvent("EDA+PPG 128 Hz. Raw clocks retained; session export uses software arrival-envelope alignment with unknown fixed Bluetooth delay. Sensor settings persist on Shimmer.");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    bridge = new AndroidJavaObject("com.angerroom.shimmer.ShimmerStream", activity);
                ReceivedSamples = 0;
                Latest = null;
                firstSampleAt = -1;
                stopping = false;
                bridge.Call("start", pairedDeviceName);
                Status = "Connecting to " + pairedDeviceName;
                Debug.Log("[Shimmer] CSV " + CsvPath);
            }
            catch (Exception error) { Fail(error); }
#else
            Status = "Shimmer Bluetooth requires an Android headset build";
#endif
        }

        void PermissionDenied()
        {
            if (this == null) return;
            permissionPending = false;
            AutoRetryAllowed = false;
            Status = "Nearby devices permission denied. Allow it in app settings and reopen the app.";
        }

        public void StopRecording()
        {
            permissionPending = false;
            if (bridge == null || stopping) return;
            stopping = true;
            bridge.Call("stop");
            RecordEvent("Stop requested");
            csv?.Flush();
        }

        void Update()
        {
            if (!exportReported && exportTask.IsCompleted)
            {
                exportReported = true;
                if (exportError != null) Debug.LogError("[Shimmer] Alignment export failed; raw CSV retained: " + exportError);
            }
            if (bridge != null)
            {
                try
                {
                    DrainSamples();
                    string notices = bridge.Call<string>("drainEvents");
                    if (!string.IsNullOrEmpty(notices))
                    {
                        events?.Write(notices);
                        events?.Flush();
                        Debug.Log("[Shimmer] " + notices.TrimEnd());
                    }
                    double now = Time.realtimeSinceStartupAsDouble;
                    if (now - lastStatusPoll >= 0.25)
                    {
                        Status = bridge.Call<string>("getStatus");
                        lastStatusPoll = now;
                    }
                    if (now - lastFlush >= 1) { csv?.Flush(); lastFlush = now; }
                    if (firstSampleAt >= 0 && recordingDurationSeconds > 0 && now - firstSampleAt >= recordingDurationSeconds)
                        StopRecording();
                    if (!bridge.Call<bool>("isRunning"))
                    {
                        // Worker is finished: no new rows can arrive while the final queue is drained.
                        while (DrainSamples()) { }
                        events?.Write(bridge.Call<string>("drainEvents"));
                        Status = bridge.Call<string>("getStatus");
                        RecordEvent("Unity received=" + ReceivedSamples + "; native queue drops=" + bridge.Call<long>("getQueueDrops"));
                        Release();
                    }
                }
                catch (Exception error) { Fail(error); }
            }
            if (showDiagnostics) UpdateHud();
        }

        bool DrainSamples()
        {
            string batch = bridge.Call<string>("drainSamples", 512);
            if (string.IsNullOrEmpty(batch)) return false;
            foreach (string row in batch.Split('\n'))
            {
                if (row.Length == 0) continue;
                Sample sample = ParseSample(row);
                var session = RecordingSession.Instance;
                if (session == null) csv.WriteLine(row);
                else
                {
                    double received = session.SecondsAt(sample.ReceiveAndroidNanos);
                    if (Latest == null) segmentAnchor = received - sample.ShimmerElapsedSeconds;
                    csv.WriteLine(RecordingSession.Csv(session.SessionId) + "," + ConnectionSegment + "," +
                        RecordingSession.Number(received) + "," + RecordingSession.Number(segmentAnchor + sample.ShimmerElapsedSeconds) + "," + row);
                }
                Latest = sample;
                ReceivedSamples++;
                if (firstSampleAt < 0) firstSampleAt = Time.realtimeSinceStartupAsDouble;
                SampleReceived?.Invoke(sample);
            }
            return true;
        }

        public static Sample ParseSample(string row)
        {
            string[] c = row.Split(',');
            if (c.Length != 16) throw new FormatException("Invalid Shimmer sample column count");
            return new Sample
            {
                Sequence = long.Parse(c[0], Invariant), ShimmerTicks24 = long.Parse(c[1], Invariant),
                ShimmerTicksUnwrapped = long.Parse(c[2], Invariant), ShimmerElapsedSeconds = double.Parse(c[3], Invariant),
                ReceiveAndroidNanos = long.Parse(c[4], Invariant), ReceiveUtcMs = long.Parse(c[5], Invariant),
                GsrRawWord = int.Parse(c[6], Invariant), GsrAdc = int.Parse(c[7], Invariant), GsrRange = int.Parse(c[8], Invariant),
                EdaMicroSiemens = c[9].Length == 0 ? double.NaN : double.Parse(c[9], Invariant),
                EdaConversionValid = bool.Parse(c[10]), GsrRangeChanged = bool.Parse(c[11]), PpgRaw = int.Parse(c[12], Invariant),
                PpgMilliVolts = c[13].Length == 0 ? double.NaN : double.Parse(c[13], Invariant),
                PpgAdcValid = bool.Parse(c[14]), EstimatedMissing = long.Parse(c[15], Invariant)
            };
        }

        void RecordEvent(string message)
        {
            events?.WriteLine(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(Invariant) + " " + message);
            Debug.Log("[Shimmer] " + message);
        }

        void Fail(Exception error)
        {
            Status = "ERROR: " + error.Message;
            Debug.LogError("[Shimmer] " + error);
            try { events?.WriteLine(Status); bridge?.Call("stop"); } catch { }
            Release();
        }

        void Release()
        {
            bool hadRecording = csv != null;
            RecordingSession.Instance?.Log("shimmer_connection_end", "segment=" + ConnectionSegment + "; status=" + Status);
            bridge?.Dispose(); bridge = null;
            csv?.Dispose(); csv = null;
            events?.Dispose(); events = null;
            if (hadRecording) QueueAlignedExport();
        }

        void QueueAlignedExport()
        {
            if (string.IsNullOrEmpty(CsvPath) || Path.GetFileName(CsvPath) != "shimmer_raw.csv") return;
            try
            {
                // Snapshot only flushed, complete rows. Fit/write off the Unity thread so recovery UI animates.
                csv?.Flush();
                string snapshot = CsvPath + ".snapshot_" + Guid.NewGuid().ToString("N");
                File.Copy(CsvPath, snapshot);
                string diagnosticFolder = Path.GetDirectoryName(CsvPath);
                string output = Path.Combine(diagnosticFolder, "shimmer_aligned.csv");
                string readable = Path.Combine(Path.GetDirectoryName(diagnosticFolder), "shimmer.csv");
                exportReported = false;
                exportTask = exportTask.ContinueWith(_ => {
                    try
                    {
                        ShimmerClockAlignment.Export(snapshot, output);
                        ShimmerClockAlignment.ExportReadable(output, readable);
                        exportError = null;
                    }
                    catch (Exception error) { exportError = error.ToString(); }
                    finally { try { File.Delete(snapshot); } catch { } }
                }, TaskScheduler.Default);
            }
            catch (Exception error) { Debug.LogError("[Shimmer] Could not snapshot raw CSV for alignment: " + error); }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused || bridge == null) return;
            RecordEvent("Application paused; stopping acquisition. Resume continues the session with a new connection segment.");
            StopRecording();
            // Android may suspend Unity before the worker finishes; export the safely flushed prefix too.
            QueueAlignedExport();
        }

        void OnDestroy()
        {
            try { StopRecording(); } finally { Release(); }
            if (hudRoot != null) Destroy(hudRoot);
        }

        void UpdateHud()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            if (hud == null)
            {
                hudRoot = new GameObject("Shimmer status", typeof(RectTransform), typeof(Canvas));
                DontDestroyOnLoad(hudRoot);
                var canvas = hudRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                var rect = hudRoot.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(800, 180);
                var background = hudRoot.AddComponent<UnityEngine.UI.Image>();
                background.color = new Color(0.02f, 0.04f, 0.07f, 0.88f);
                background.raycastTarget = false;
                var textObject = new GameObject("Readings", typeof(RectTransform), typeof(UnityEngine.UI.Text));
                textObject.transform.SetParent(hudRoot.transform, false);
                hud = textObject.GetComponent<UnityEngine.UI.Text>();
                hud.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                hud.fontSize = 25;
                hud.color = Color.white;
                hud.raycastTarget = false;
                var textRect = textObject.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(15, 10); textRect.offsetMax = new Vector2(-15, -10);
            }
            hudRoot.transform.SetPositionAndRotation(camera.transform.TransformPoint(0, -0.3f, 0.9f), camera.transform.rotation);
            hudRoot.transform.localScale = Vector3.one * 0.0008f;
            string values = Latest == null ? "Waiting for samples" :
                "EDA " + (Latest.EdaConversionValid ? Latest.EdaMicroSiemens.ToString("F3", Invariant) + " uS" : "out of range") +
                "    PPG " + Latest.PpgMilliVolts.ToString("F1", Invariant) + " mV" + (Latest.PpgAdcValid ? "" : " (check probe)");
            hud.text = "SHIMMER EDA + PPG | " + ReceivedSamples + " samples\n" + values + "\n" + Status;
        }

#if SHIMMER_STREAM_TEST
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartTestReceiver()
        {
            if (FindObjectOfType<ShimmerReceiver>() != null) return;
            var root = new GameObject("Shimmer EDA PPG Test");
            DontDestroyOnLoad(root);
            var receiver = root.AddComponent<ShimmerReceiver>();
            receiver.recordingDurationSeconds = 90;
        }
#endif
    }
}
