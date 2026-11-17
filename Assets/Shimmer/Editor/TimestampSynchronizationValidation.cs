using System;
using System.Globalization;
using System.IO;
using System.Linq;
using AngerRoom.Shimmer;
using UnityEngine;

public static class TimestampSynchronizationValidation
{
    static int checks;
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Timestamp synchronization: " + message);
        checks++;
    }
    static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    public static void Run()
    {
        checks = 0;
        CultureInfo old = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var eye = new EyeClockAlignment();
            double mapped;
            Check(!eye.TryMap(500, 3, out mapped), "uncalibrated eye clock accepted");
            Check(eye.Calibrate(10, 50010.001, 10.002), "valid SDK bracket rejected");
            Check(eye.TryMap(50010.021, 10.03, out mapped) && Math.Abs(mapped - 10.021) < 1e-8, "SDK clock offset mapping");
            Check(Math.Abs(eye.HalfWidth - 0.001) < 1e-10, "bracket width");
            Check(!eye.TryMap(0, 10.03, out mapped), "missing SDK timestamp accepted");
            Check(!eye.TryMap(50014, 14, out mapped), "stale calibration accepted");
            Check(!eye.TryMap(60000, 10.03, out mapped), "wrong clock domain accepted");
            Check(!eye.Calibrate(10, 50010, 10.1) && eye.Revision == 1, "slow bracket replaced calibration");
            Check(!eye.Calibrate(10, double.NaN, 10.001), "NaN clock accepted");
            eye.Invalidate();
            Check(!eye.TryMap(50010.021, 10.03, out mapped), "pause did not invalidate calibration");
            Check(eye.Calibrate(120, 50100.001, 120.002) && eye.TryMap(50100.011, 120.02, out mapped) &&
                Math.Abs(mapped - 120.011) < 1e-8, "resume clock-domain offset not recalibrated");

            CheckFit(1.00025);
            CheckFit(0.9998);
            var shortFit = new ShimmerClockAlignment.Fit();
            shortFit.Observe(0, 2); shortFit.Observe(1, 3.01); shortFit.EstimateRate();
            Check(shortFit.Rate == 1 && shortFit.Method.Contains("short"), "short connection invented drift");
            var rejected = new ShimmerClockAlignment.Fit();
            for (int i = 0; i < 120; i++) rejected.Observe(i, 5 + i * 1.01);
            rejected.EstimateRate();
            Check(rejected.Rate == 1 && rejected.Method.Contains("rejected"), "transport trend accepted as large drift");
            bool threw = false;
            try { shortFit.Observe(0, 4); } catch (InvalidDataException) { threw = true; }
            Check(threw, "backward device clock accepted without a new segment");
            CheckExport();
            Debug.Log("TIMESTAMP_SYNC_TESTS_OK checks=" + checks);
        }
        finally { CultureInfo.CurrentCulture = old; }
    }

    static void CheckFit(double rate)
    {
        var fit = new ShimmerClockAlignment.Fit();
        // Batch-like nonnegative jitter plus a delayed first packet. A fixed 20ms latency
        // must remain in the result: arrival-only methods cannot identify/remove it.
        double previous = double.NegativeInfinity;
        for (int i = 0; i < 600 * 128; i++)
        {
            double x = i / 128.0;
            double arrival = 7 + rate * x + 0.020 + (i % 17) * 0.001;
            if (i == 0) arrival += 0.15;
            previous = Math.Max(previous, arrival);
            fit.Observe(x, previous);
        }
        fit.EstimateRate();
        previous = double.NegativeInfinity;
        for (int i = 0; i < 600 * 128; i++)
        {
            double x = i / 128.0;
            double arrival = 7 + rate * x + 0.020 + (i % 17) * 0.001;
            if (i == 0) arrival += 0.15;
            previous = Math.Max(previous, arrival);
            fit.ObserveOffset(x, previous);
        }
        Check(Math.Abs(fit.Rate - rate) < 0.00001, "drift estimate exceeded 10ppm tolerance");
        Check(Math.Abs(fit.Map(500) - (7.020 + 500 * rate)) < 0.003, "jitter/fixed-delay treatment");
        Check(fit.Map(0) < 7.04, "first packet delay persisted as clock offset");
    }

    static void CheckExport()
    {
        string folder = "Logs/TimestampSyncChecks";
        Directory.CreateDirectory(folder);
        string raw = Path.Combine(folder, "shimmer_raw.csv"), output = Path.Combine(folder, "shimmer.csv");
        using (var writer = new StreamWriter(raw))
        {
            writer.WriteLine("session_id,connection_segment,receive_session_s,estimated_sample_session_s,sequence,shimmer_ticks_24,shimmer_ticks_unwrapped,shimmer_elapsed_s,android_receive_elapsed_ns,android_receive_utc_ms,gsr_raw_word,gsr_adc,gsr_range,eda_us,eda_conversion_valid,gsr_range_changed,ppg_raw,ppg_mv,ppg_adc_valid,estimated_missing_samples");
            for (int segment = 1; segment <= 2; segment++)
                for (int i = 0; i < 8192; i++)
                {
                    double x = i / 128.0 + (i >= 4096 ? 0.25 : 0); // lost packet gap stays a gap
                    double received = segment * 100 + x * 1.0001 + 0.02;
                    long ticks = 16777000 + (long)(x * 32768); // exercise raw rollover retention
                    writer.WriteLine("\"fixture\"," + segment + "," + N(received) + "," + N(segment * 100 + x + 0.02) + "," +
                        (i + 1) + "," + (ticks & 0xffffff) + "," + ticks + "," + N(x) + "," + (long)(received * 1e9) +
                        ",1794325000000,34768,2000,2,1.93,true,false,2048,1500.36,true," + (i == 4096 ? 32 : 0));
                }
        }
        Check(ShimmerClockAlignment.Export(raw, output) == 16384, "export lost rows");
        var first = RecordingCsv.ReadRows(output).ToArray();
        Check(first[0][0] == "session_time_s" && File.ReadAllLines(output)[0] == "sep=;", "missing common time column or Excel preamble");
        Check(first[1].Length == 27 && first[1][6] == "", "unknown error bound must be empty");
        double t0 = RecordingCsv.ParseNumber(first[1][0]), t1 = RecordingCsv.ParseNumber(first[8193][0]);
        Check(Math.Abs(t1 - t0 - 100) < 1e-6, "reconnection gap collapsed");
        Check(RecordingCsv.ParseNumber(first[4097][0]) - RecordingCsv.ParseNumber(first[4096][0]) > 0.25,
            "missing samples interpolated away");
        Check(first[4097][26] == "32", "gap flag altered");
        Check(ShimmerClockAlignment.Export(raw, output) == 16384 && first[1].SequenceEqual(RecordingCsv.ReadRows(output).Skip(1).First()), "export is not repeatable");
        // The same samples in the new format must produce exactly the same alignment.
        string excelRaw = Path.Combine(folder, "shimmer_raw_excel.csv");
        var legacy = RecordingCsv.ReadRows(raw).ToArray();
        legacy[1][8] = "9007199254740993"; // Above exact double integer precision.
        using (var writer = RecordingCsv.Create(excelRaw, string.Join(",", legacy[0])))
            foreach (var row in legacy.Skip(1))
            {
                foreach (int i in new[] { 2, 3, 7, 13, 17 }) row[i] = RecordingCsv.LocalizeNumber(row[i]);
                RecordingCsv.WriteRow(writer, row);
            }
        Check(ShimmerClockAlignment.Export(excelRaw, output) == 16384, "new-format raw samples lost");
        var excel = RecordingCsv.ReadRows(output).ToArray();
        Check(excel[1][0] == first[1][0] && excel[8193][0] == first[8193][0] && excel[1][15] == "9007199254740993", "format changed alignment or integer timestamp precision");
        string readable = Path.Combine(folder, "readable.csv");
        Check(ShimmerClockAlignment.ExportReadable(output, readable) == 16384 &&
            RecordingCsv.ReadRows(readable).Skip(1).First()[4] == "1,93", "new-format aligned export failed");
        File.AppendAllText(raw, "incomplete,row\n");
        bool threw = false;
        try { ShimmerClockAlignment.Export(raw, output); } catch (InvalidDataException) { threw = true; }
        Check(threw && RecordingCsv.ReadRows(output).Count() == 16385, "bad input replaced a complete export");
    }
}
