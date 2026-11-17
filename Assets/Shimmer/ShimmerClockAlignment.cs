using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AngerRoom.Shimmer
{
    // Pure .NET so the same export can be rerun on a PC after an interrupted app exit.
    // Never interpret the arrival residual as a bound on absolute synchronization error.
    public static class ShimmerClockAlignment
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        struct Anchor { public double X, Y; }

        public sealed class Fit
        {
            readonly List<Anchor> anchors = new List<Anchor>();
            long bucket = -1;
            double first = double.NaN, last, previousReceive = double.NegativeInfinity;
            public double Rate { get; private set; } = 1;
            public double Offset { get; private set; } = double.PositiveInfinity;
            public double Span => last - first;
            public long Count { get; private set; }
            public string Method { get; private set; } = "arrival_envelope_nominal_short";
            public double Map(double deviceSeconds) => Offset + Rate * deviceSeconds;

            public void Observe(double deviceSeconds, double received)
            {
                if (!EyeClockAlignment.Finite(deviceSeconds) || !EyeClockAlignment.Finite(received) ||
                    deviceSeconds < 0 || (Count > 0 && deviceSeconds <= last) || received < previousReceive)
                    throw new InvalidDataException("Non-monotonic or invalid Shimmer timing within a connection segment");
                if (Count == 0) first = deviceSeconds;
                last = deviceSeconds; previousReceive = received; Count++;
                long b = (long)(deviceSeconds / 2);
                var a = new Anchor { X = deviceSeconds, Y = received };
                if (b != bucket) { anchors.Add(a); bucket = b; }
                else if (received - deviceSeconds < anchors[anchors.Count - 1].Y - anchors[anchors.Count - 1].X)
                    anchors[anchors.Count - 1] = a;
            }

            public void EstimateRate()
            {
                if (Span < 30 || anchors.Count < 12) return;
                // Robust median pairwise slope of up to 64 evenly spaced low-delay anchors.
                // Use widely separated pairs to reduce the effect of packet jitter on rate.
                var slopes = new List<double>();
                int count = Math.Min(64, anchors.Count);
                for (int i = 0; i < count; i++)
                    for (int j = i + 1; j < count; j++)
                    {
                        Anchor a = anchors[i * (anchors.Count - 1) / (count - 1)];
                        Anchor b = anchors[j * (anchors.Count - 1) / (count - 1)];
                        if (b.X - a.X >= Span / 3) slopes.Add((b.Y - a.Y) / (b.X - a.X));
                    }
                slopes.Sort();
                double rate = slopes[slopes.Count / 2];
                // Large apparent drift is more likely a transport trend or corrupt timestamps.
                if (rate < 0.998 || rate > 1.002) { Method = "arrival_envelope_nominal_rejected_rate"; return; }
                Rate = rate;
                Method = "arrival_envelope_affine";
            }

            public void ObserveOffset(double deviceSeconds, double received)
            {
                Offset = Math.Min(Offset, received - Rate * deviceSeconds);
            }
        }

        // Read both legacy invariant CSV and current Excel-compatible CSV.
        // Preserve integer timestamp strings without a floating-point conversion.
        public static long Export(string rawPath, string outputPath)
        {
            if (string.Equals(Path.GetFullPath(rawPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Keep the raw recording separate from its derived export");
            var fits = new Dictionary<int, Fit>();
            string header;
            using (var reader = RecordingCsv.ReadRows(rawPath).GetEnumerator())
            {
                if (!reader.MoveNext()) throw new InvalidDataException("Missing Shimmer raw CSV header");
                header = string.Join(",", reader.Current);
                if ((reader.Current.Length != 20 && (reader.Current.Length != 23 || !header.EndsWith(",startup_settling,signal_check_passed,startup_status"))) || !header.StartsWith("session_id,connection_segment,receive_session_s,estimated_sample_session_s,sequence,"))
                    throw new InvalidDataException("Unsupported Shimmer raw CSV header");
                ReadRows(reader, header.Split(',').Length, (c, segment, x, y) => {
                    Fit fit;
                    if (!fits.TryGetValue(segment, out fit)) fits.Add(segment, fit = new Fit());
                    fit.Observe(x, y);
                });
            }
            foreach (Fit fit in fits.Values) fit.EstimateRate();
            using (var reader = RecordingCsv.ReadRows(rawPath).GetEnumerator())
            {
                reader.MoveNext();
                ReadRows(reader, header.Split(',').Length, (c, segment, x, y) => fits[segment].ObserveOffset(x, y));
            }
            long rows = 0;
            string temporary = outputPath + ".tmp";
            using (var writer = RecordingCsv.Create(temporary, "session_time_s,sync_method,clock_rate,clock_offset_s,fit_span_s,arrival_residual_s,absolute_error_bound_s," + header))
            using (var reader = RecordingCsv.ReadRows(rawPath).GetEnumerator())
            {
                reader.MoveNext();
                ReadRows(reader, header.Split(',').Length, (c, segment, x, y) => {
                    Fit fit = fits[segment];
                    double mapped = fit.Map(x);
                    var values = new string[c.Length + 7];
                    values[0] = RecordingCsv.Number(mapped);
                    values[1] = fit.Method;
                    values[2] = RecordingCsv.Number(fit.Rate);
                    values[3] = RecordingCsv.Number(fit.Offset);
                    values[4] = RecordingCsv.Number(fit.Span);
                    values[5] = RecordingCsv.Number(y - mapped);
                    values[6] = "";
                    foreach (int i in new[] { 2, 3, 7, 13, 17 }) c[i] = RecordingCsv.LocalizeNumber(c[i]);
                    Array.Copy(c, 0, values, 7, c.Length);
                    RecordingCsv.WriteRow(writer, values);
                    rows++;
                });
            }
            // Readers only see complete exports. Raw data remains authoritative if replacement fails.
            if (File.Exists(outputPath)) File.Replace(temporary, outputPath, null);
            else File.Move(temporary, outputPath);
            return rows;
        }

        static void ReadRows(IEnumerator<string[]> reader, int expectedColumns, Action<string[], int, double, double> action)
        {
            while (reader.MoveNext())
            {
                string[] c = reader.Current;
                if (c.Length == 1 && c[0].Length == 0) continue;
                if (c.Length != expectedColumns) throw new InvalidDataException("Incomplete Shimmer raw CSV row; original file retained");
                action(c, int.Parse(c[1], Inv), RecordingCsv.ParseNumber(c[7]), RecordingCsv.ParseNumber(c[2]));
            }
        }

        public static long ExportReadable(string alignedPath, string outputPath)
        {
            if (string.Equals(Path.GetFullPath(alignedPath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Keep the technical alignment file separate from the readable export");
            const string columns = "session_id,session_time_s,connection_segment,sequence,eda_us,ppg_mv,eda_conversion_valid,ppg_adc_valid,gsr_range,gsr_range_changed,estimated_missing_samples";
            string[] names = columns.Split(',');
            string temporary = outputPath + ".tmp";
            long count = 0;
            using (var rows = RecordingCsv.ReadRows(alignedPath).GetEnumerator())
            {
                if (!rows.MoveNext()) throw new InvalidDataException("Missing aligned Shimmer header");
                string[] header = rows.Current;
                var indices = new int[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    indices[i] = Array.IndexOf(header, names[i]);
                    if (indices[i] < 0) throw new InvalidDataException("Missing Shimmer column: " + names[i]);
                }
                // Derived from the same sample, in kOhm: R = 1000 / G(uS).
                // Append to preserve the existing exported column positions.
                string[] markers = { "startup_settling", "signal_check_passed", "startup_status" };
                using (var writer = RecordingCsv.Create(temporary, columns + ",skin_resistance_kohm," + string.Join(",", markers)))
                {
                    while (rows.MoveNext())
                    {
                        if (rows.Current.Length != header.Length) throw new InvalidDataException("Invalid aligned Shimmer row");
                        var values = new string[names.Length + 4];
                        for (int i = 0; i < names.Length; i++) values[i] = rows.Current[indices[i]];
                        values[names.Length] = "";
                        for (int i = 0; i < markers.Length; i++)
                        {
                            int source = Array.IndexOf(header, markers[i]);
                            values[names.Length + 1 + i] = source < 0 ? "" : rows.Current[source];
                        }
                        if (bool.TryParse(values[6], out bool valid) && valid &&
                            double.TryParse(values[4].Replace(',', '.'), NumberStyles.Float, Inv, out double conductance) &&
                            EyeClockAlignment.Finite(conductance) && conductance > 0)
                        {
                            double resistance = 1000.0 / conductance;
                            if (EyeClockAlignment.Finite(resistance))
                                values[names.Length] = RecordingCsv.Number(resistance);
                        }
                        foreach (int i in new[] { 1, 4, 5 })
                            if (values[i].Length > 0) values[i] = RecordingCsv.Number(RecordingCsv.ParseNumber(values[i]));
                        RecordingCsv.WriteRow(writer, values);
                        count++;
                    }
                }
            }
            if (File.Exists(outputPath)) File.Replace(temporary, outputPath, null);
            else File.Move(temporary, outputPath);
            return count;
        }
    }
}
