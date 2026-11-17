using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using AngerRoom.Shimmer;
using UnityEngine;

public static class RecordingFileValidation
{
    static int checks;
    static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Recording files: " + message);
        checks++;
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run file validation in a separate Unity batch process.");
        checks = 0;
        string folder = Path.GetFullPath("Logs/RecordingFileChecks");
        Directory.CreateDirectory(folder);
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        var originalSession = RecordingSession.Instance;
        GameObject root = null;
        try
        {
            // Output must match the user's Spanish Excel even if Android uses English.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Check(RecordingCsv.Number(1234.567) == "1234,567", "output depended on headset locale");
            root = new GameObject("Recording file validation");
            var session = root.AddComponent<RecordingSession>();
            typeof(RecordingSession).GetProperty("Instance").SetValue(null, session);
            typeof(RecordingSession).GetProperty("Folder").SetValue(session, folder);
            typeof(RecordingSession).GetProperty("SessionId").SetValue(session, "file_check");
            typeof(RecordingSession).GetMethod("OpenRecorders", Private).Invoke(session, null);
            var logger = root.AddComponent<Logger>();
            const string message = "Shot, hit; target \"café\"\nSecond line";
            logger.Log(message);
            session.Log("shimmer_connection_start", message);
            // Inspect completed files, as a user does after copying the recording.
            // Mono's File.ReadAllBytes cannot reopen a still-writing stream on Windows.
            foreach (string name in new[] { "logger", "events", "clockSync" })
            {
                (typeof(RecordingSession).GetField(name, Private).GetValue(session) as IDisposable)?.Dispose();
                typeof(RecordingSession).GetField(name, Private).SetValue(session, null);
            }
            string loggerPath = Path.Combine(folder, "Logger.csv");
            byte[] bytes = File.ReadAllBytes(loggerPath);
            Check(bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf, "missing UTF-8 BOM");
            Check(File.ReadAllLines(loggerPath)[0] == "sep=;", "missing Excel separator directive");
            var rows = RecordingCsv.ReadRows(loggerPath).ToArray();
            Check(rows.Length == 2 && rows[0].Length == 3 && rows[1][2] == message, "Logger changed text or CSV columns");
            Check(double.TryParse(rows[1][1], NumberStyles.Float, CultureInfo.GetCultureInfo("es-ES"), out _), "session time is not a Spanish numeric value");
            string eventsPath = Path.Combine(session.DiagnosticsFolder, "session_events.csv");
            var events = RecordingCsv.ReadRows(eventsPath).ToArray();
            Check(events.Length == 2 && events[1][2] == "shimmer_connection_start" && events[1][3] == message, "diagnostic event escaping changed");
            Check(File.Exists(Path.Combine(session.DiagnosticsFolder, "clock_sync.csv")), "clock probes not in diagnostics folder");
            foreach (string diagnostic in new[] { "session_events.csv", "clock_sync.csv" })
            {
                string path = Path.Combine(session.DiagnosticsFolder, diagnostic);
                byte[] diagnosticBytes = File.ReadAllBytes(path);
                Check(diagnosticBytes[0] == 0xef && diagnosticBytes[1] == 0xbb && diagnosticBytes[2] == 0xbf &&
                    File.ReadAllLines(path)[0] == "sep=;", "diagnostic format differs from main files: " + diagnostic);
            }
            string appendPath = Path.Combine(folder, "append.csv");
            using (var writer = RecordingCsv.Create(appendPath, "id,value"))
                RecordingCsv.WriteRow(writer, "9007199254740993", RecordingCsv.Number(1.25));
            using (var writer = RecordingCsv.Create(appendPath, "id,value", true))
                RecordingCsv.WriteRow(writer, "9007199254740994", RecordingCsv.Number(2.5));
            var appended = RecordingCsv.ReadRows(appendPath).ToArray();
            Check(appended.Length == 3 && appended[1][0] == "9007199254740993" && appended[2][1] == "2,5" &&
                File.ReadAllLines(appendPath).Count(line => line == "sep=;") == 1, "append duplicated preamble/header or lost precision");

            var eyes = root.AddComponent<EyeStateLogger>();
            typeof(EyeStateLogger).GetMethod("InitLogFile", Private).Invoke(eyes, null);
            typeof(EyeStateLogger).GetField("_observationSessionSeconds", Private).SetValue(eyes, 1.25);
            typeof(EyeStateLogger).GetMethod("WriteRow", Private).Invoke(eyes, new object[] {
                new EyeStateLogger.EyePacket { t = "1794409550885", reason = message, hp = "[1;2;3]" }
            });
            typeof(EyeStateLogger).GetMethod("CloseLogFile", Private).Invoke(eyes, null);
            var eyeRows = RecordingCsv.ReadRows(Path.Combine(folder, "eyegaze.csv")).ToArray();
            Check(eyeRows.Length == 2 && eyeRows[0].Length == eyeRows[1].Length && eyeRows[1][1] == "1,25" &&
                eyeRows[1].Last() == message, "gaze delimiter/escaping/time mismatch");

            string aligned = Path.Combine(folder, "aligned_fixture.csv");
            File.WriteAllText(aligned,
                "session_id,session_time_s,connection_segment,sequence,eda_us,ppg_mv,eda_conversion_valid,ppg_adc_valid,gsr_range,gsr_range_changed,estimated_missing_samples\n" +
                "file_check,1234.567,2,42,,1500.3663,false,true,2,false,3\n");
            string readable = Path.Combine(folder, "shimmer.csv");
            Check(ShimmerClockAlignment.ExportReadable(aligned, readable) == 1, "Shimmer row lost");
            var shimmer = RecordingCsv.ReadRows(readable).ToArray();
            Check(shimmer[1].Length == 15 && shimmer[1][1] == "1234,567" && shimmer[1][4] == "" &&
                shimmer[1][5] == "1500,3663" && shimmer[1][10] == "3", "Shimmer precision/blank/gap preservation");
            Check(shimmer[0][11] == "skin_resistance_kohm" && shimmer[1][11] == "", "invalid EDA must leave resistance blank");
            Check(shimmer[0][12] == "startup_settling" && shimmer[1].Skip(12).All(x => x == ""), "Legacy data must not invent readiness markers");
            Check(ShimmerClockAlignment.ExportReadable(aligned, readable) == 1, "repeat Shimmer export failed");
            Check(File.ReadAllText(aligned).Contains("1234.567"), "technical data was reformatted");
            foreach (string eda in new[] { "4", "8", "0", "-1", "NaN", "Infinity" })
                File.AppendAllText(aligned, "file_check,1235,2,43," + eda + ",1500,true,true,2,false,0\n");
            File.AppendAllText(aligned, "file_check,1236,2,44,4,1500,false,true,2,false,0\n");
            Check(ShimmerClockAlignment.ExportReadable(aligned, readable) == 8, "resistance export lost rows");
            shimmer = RecordingCsv.ReadRows(readable).ToArray();
            Check(shimmer[2][11] == "250" && shimmer[3][11] == "125", "resistance unit conversion incorrect");
            Check(shimmer.Skip(4).All(row => row[11] == ""), "nonpositive, nonfinite or invalid EDA produced resistance");
            Debug.Log("RECORDING_FILE_TESTS_OK checks=" + checks);
        }
        finally
        {
            if (root != null)
            {
                var session = root.GetComponent<RecordingSession>();
                foreach (string name in new[] { "logger", "events", "clockSync" })
                    (typeof(RecordingSession).GetField(name, Private).GetValue(session) as IDisposable)?.Dispose();
            }
            typeof(RecordingSession).GetProperty("Instance").SetValue(null, originalSession);
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
