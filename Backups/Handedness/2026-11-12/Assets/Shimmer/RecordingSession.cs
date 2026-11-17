using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace AngerRoom.Shimmer
{
    // One timeline for the entire run, including preparation and connection outages.
    [DefaultExecutionOrder(-1000)]
    public sealed class RecordingSession : MonoBehaviour
    {
        public static RecordingSession Instance { get; private set; }
        public string SessionId { get; private set; }
        public string Folder { get; private set; }
        public string DiagnosticsFolder { get; private set; }
        public long OriginNanos { get; private set; }
        public double Now => SecondsAt(ClockNanos());
        public double SecondsAt(long nanos) => (nanos - OriginNanos) * 1e-9;
        public static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        public static string Csv(string value) => "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        StreamWriter events;
        StreamWriter logger;
        double lastFlush;
        AndroidJavaClass clock;
        SessionMetadata metadata;
        StreamWriter clockSync;
        double nextEyeCalibration;
        public EyeClockAlignment EyeClock { get; } = new EyeClockAlignment();

        [Serializable] class SessionMetadata
        {
            public string session_id, utc_anchor, headset, clock, timing_method;
            public long monotonic_anchor_ns;
            public double acquisition_ready_s = -1;
            public bool eye_tracking_required = true;
            public int schema_version = 3;
            public string user_csv_format = "UTF-8 BOM; sep=; preamble; semicolon separator; decimal comma. Diagnostics use invariant comma-separated CSV.";
            public string alignment_limit = "Shimmer fixed Bluetooth delay is unknown; no measured absolute synchronization error bound. Eye SDK mapping awaits Quest Pro validation.";
        }

        void Awake()
        {
            if (Instance != null) { Destroy(this); return; }
            Instance = this;
#if UNITY_ANDROID && !UNITY_EDITOR
            clock = new AndroidJavaClass("android.os.SystemClock");
#endif
            OriginNanos = ClockNanos();
            var utc = DateTime.UtcNow;
            SessionId = utc.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Folder = Path.Combine(Application.persistentDataPath, "session_" + SessionId);
            Directory.CreateDirectory(Folder);
            metadata = new SessionMetadata {
                session_id = SessionId, utc_anchor = utc.ToString("O"), headset = SystemInfo.deviceModel,
                monotonic_anchor_ns = OriginNanos, clock = clock == null ? "Stopwatch" : "Android elapsedRealtimeNanos",
                timing_method = "Events use the headset monotonic session clock. Eye SDK timestamps use bracketed SDK-to-session clock reads. Shimmer exports use per-connection robust affine arrival-envelope fits; original timestamps remain in shimmer_raw.csv."
            };
            SaveMetadata();
            OpenRecorders();
            Log("session_created", "Preparation begins; session clock never pauses");
        }

        void OpenRecorders()
        {
            DiagnosticsFolder = Path.Combine(Folder, "diagnostics");
            Directory.CreateDirectory(DiagnosticsFolder);
            logger = RecordingCsv.Create(Path.Combine(Folder, "Logger.csv"), "session_id,session_time_s,message");
            events = new StreamWriter(Path.Combine(DiagnosticsFolder, "session_events.csv"));
            events.WriteLine("session_id,session_time_s,event,details");
            clockSync = new StreamWriter(Path.Combine(DiagnosticsFolder, "clock_sync.csv"));
            clockSync.WriteLine("session_id,mapping_id,before_session_s,sdk_now_s,after_session_s,sdk_to_session_offset_s,bracket_half_width_s,accepted");
        }

        public long ClockNanos() => clock != null ? clock.CallStatic<long>("elapsedRealtimeNanos") :
            (long)(Stopwatch.GetTimestamp() * (1000000000.0 / Stopwatch.Frequency));
        public void Log(string name, string details = "")
        {
            if (events == null) return;
            events.WriteLine(Csv(SessionId) + "," + Number(Now) + "," + Csv(name) + "," + Csv(details));
            events.Flush();
        }
        public void LogApplication(string message)
        {
            if (logger == null) return;
            RecordingCsv.WriteRow(logger, SessionId, RecordingCsv.Number(Now), message);
            logger.Flush();
        }
        public void MarkReady(bool eyeRequired)
        {
            if (metadata.acquisition_ready_s >= 0) return;
            metadata.eye_tracking_required = eyeRequired;
            metadata.acquisition_ready_s = Now;
            SaveMetadata();
            Log("acquisition_ready", "Shimmer samples confirmed; eye required=" + eyeRequired);
        }
        void SaveMetadata() => File.WriteAllText(Path.Combine(Folder, "session.json"), JsonUtility.ToJson(metadata, true));
        void Update()
        {
            if (Now >= nextEyeCalibration && OVRPlugin.initialized)
            {
                nextEyeCalibration = Now + 1;
                // Select the narrowest of three brackets; scheduler stalls cannot masquerade as precision.
                double before = 0, after = double.PositiveInfinity, sdk = 0;
                for (int i = 0; i < 3; i++)
                {
                    double b = Now;
                    double t = OVRPlugin.GetTimeInSeconds();
                    double a = Now;
                    if (t > 0 && a - b < after - before) { before = b; after = a; sdk = t; }
                }
                bool accepted = EyeClock.Calibrate(before, sdk, after);
                clockSync?.WriteLine(Csv(SessionId) + "," + EyeClock.Revision + "," + Number(before) + "," + Number(sdk) +
                    "," + Number(after) + "," + (accepted ? Number(EyeClock.Offset) : "") + "," +
                    (accepted ? Number(EyeClock.HalfWidth) : "") + "," + accepted);
            }
            if (Now - lastFlush >= 1) { events?.Flush(); logger?.Flush(); clockSync?.Flush(); lastFlush = Now; }
        }
        void OnApplicationPause(bool paused)
        {
            EyeClock.Invalidate(); nextEyeCalibration = 0;
            if (paused) { events?.Flush(); logger?.Flush(); clockSync?.Flush(); }
        }
        void OnDestroy()
        {
            if (Instance != this) return;
            Log("session_closed"); events?.Dispose(); events = null; logger?.Dispose(); logger = null;
            clockSync?.Dispose(); clock?.Dispose(); Instance = null;
        }
    }
}
