using System;
using System.IO;
using System.Linq;
using AngerRoom.Shimmer;

public static class ShimmerSignalValidation
{
    static int checks;
    static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); checks++; }
    static double Pulse(double t) => 1400 + 100 * Math.Sin(2 * Math.PI * 1.2 * t);
    static void Feed(ShimmerSignalReadiness state, double until, Func<double, double> ppg,
        Func<double, double> eda = null, Func<double, double> receive = null, Func<double, double> now = null)
    {
        for (int i = 0; i <= (int)(until * 128); i++)
        {
            double t = i / 128.0;
            state.Observe(1, t, receive == null ? t : receive(t), now == null ? t : now(t),
                eda == null ? 2 : eda(t), true, ppg(t), true, false);
        }
    }
    public static void Run()
    {
        checks = 0;
        var state = new ShimmerSignalReadiness();
        Feed(state, 4.99, Pulse);
        Check(!state.IsReady, "Released before five seconds");
        state.Observe(1, 5, 5, 5, 2, true, Pulse(5), true, false);
        Check(state.IsReady && state.SignalCheckPassed && state.ReadyDeviceTime == 5, "Good signal did not release at five seconds");
        state.Observe(1, 5.01, 5.01, 5.01, 2, true, 0, false, false);
        Check(state.IsReady && !state.SignalCheckPassed, "Later artefact must be flagged without revoking connection startup");
        state.Observe(2, 0, 10, 10, 2, true, 1400, true, false);
        Check(!state.IsReady, "Reconnection inherited readiness");
        foreach (double level in new[] { 0.0, 0.7326, 1400.0, 2999.0, double.NaN })
        {
            state.Reset(); Feed(state, 10, _ => level);
            Check(!state.IsReady, "Flat/near-rail/nonfinite PPG released: " + level);
        }
        state.Reset(); Feed(state, 10, t => t == 4 ? 1700 : 1400);
        Check(!state.IsReady, "Single spike passed robust PPG variation test");
        foreach (double level in new[] { 0.0, 0.1, 126.0, double.NaN })
        {
            state.Reset(); Feed(state, 8, Pulse, _ => level);
            Check(!state.IsReady, "Invalid EDA released: " + level);
        }
        state.Reset(); Feed(state, 8, Pulse, now: t => t + 2);
        Check(!state.IsReady, "Stale buffered samples released");
        state.Reset(); Feed(state, 8, Pulse, receive: t => t + 1);
        Check(!state.IsReady, "Future samples released");
        state.Reset(); Feed(state, 8, Pulse, receive: t => 0, now: t => 0);
        Check(!state.IsReady, "Burst replay bypassed wall-clock settling");
        state.Reset(); Feed(state, 4.99, Pulse);
        state.Observe(1, 5, 5, 5, 2, true, Pulse(5), true, true);
        Check(!state.IsReady && state.Status == "gsr_range_settling", "GSR range switch did not reset confirmation");
        for (int i = 641; i <= 960; i++) { double t = i / 128.0; state.Observe(1, t, t, t, 2, true, Pulse(t), true, false); }
        Check(state.IsReady && state.ReadyDeviceTime >= 7.5, "Range switch confirmation window incorrect");
        state.Reset(); Feed(state, 4, Pulse);
        for (int i = 640; i <= 768; i++) { double t = i / 128.0; state.Observe(1, t, t, t, 2, true, Pulse(t), true, false); }
        Check(!state.IsReady, "Gap did not restart continuous confirmation");
        CheckExports();
        Console.WriteLine("SHIMMER_SIGNAL_TESTS_OK checks=" + checks);
    }
    static void CheckExports()
    {
        string folder = Path.Combine("Logs", "StartupSignalChecks"); Directory.CreateDirectory(folder);
        string raw = Path.Combine(folder, "raw.csv"), aligned = Path.Combine(folder, "aligned.csv"), readable = Path.Combine(folder, "readable.csv");
        const string header = "session_id,connection_segment,receive_session_s,estimated_sample_session_s,sequence,shimmer_ticks_24,shimmer_ticks_unwrapped,shimmer_elapsed_s,android_receive_elapsed_ns,android_receive_utc_ms,gsr_raw_word,gsr_adc,gsr_range,eda_us,eda_conversion_valid,gsr_range_changed,ppg_raw,ppg_mv,ppg_adc_valid,estimated_missing_samples";
        var state = new ShimmerSignalReadiness();
        using (var writer = RecordingCsv.Create(raw, header + "," + ShimmerSignalReadiness.Columns))
            for (int i = 0; i < 1024; i++)
            {
                double t = i / 128.0;
                state.Observe(1, t, t, t, 2, true, t < 3.125 ? 0.7 : Pulse(t), true, false);
                RecordingCsv.WriteRow(writer, "fixture", "1", RecordingCsv.Number(t), RecordingCsv.Number(t), (i + 1).ToString(),
                    (i * 256).ToString(), (i * 256).ToString(), RecordingCsv.Number(t), "9007199254740993", "1794567730139",
                    "34768", "2000", "2", "2", "true", "false", "1864", RecordingCsv.Number(t < 3.125 ? 0.7 : Pulse(t)), "true", "0",
                    (!state.IsReady).ToString(), state.SignalCheckPassed.ToString(), state.Status);
            }
        string original = File.ReadAllText(raw);
        Check(ShimmerClockAlignment.Export(raw, aligned) == 1024 && ShimmerClockAlignment.ExportReadable(aligned, readable) == 1024, "Settling rows were dropped");
        var rawRows = RecordingCsv.ReadRows(raw).ToArray(); var rows = RecordingCsv.ReadRows(readable).ToArray();
        int marker = Array.IndexOf(rows[0], "startup_settling");
        Check(marker >= 0 && rows[1][marker] == "True" && rows.Last()[marker] == "False", "Settling markers missing from user CSV");
        int firstReady = Array.FindIndex(rawRows, 1, r => r[20] == "False");
        Check(firstReady > 0 && RecordingCsv.ParseNumber(rawRows[firstReady][7]) >= 5.125, "Startup failure replay released too early");
        Check(rows.Skip(1).Select(r => r[marker]).SequenceEqual(rawRows.Skip(1).Select(r => r[20])), "Exporter changed settling intervals");
        Check(RecordingCsv.ReadRows(aligned).Skip(1).First()[15] == "9007199254740993", "Timestamp precision changed");
        Check(original == File.ReadAllText(raw), "Exporter modified raw recording");
    }
}
