using System;
using System.Collections.Generic;

namespace AngerRoom.Shimmer
{
    // Startup plausibility only, not a validated pulse/contact quality classifier.
    // Readiness latches for this connection; subsequent signal artefacts are marked,
    // not converted into gameplay interruptions or microphone muting.
    public sealed class ShimmerSignalReadiness
    {
        public const string Columns = "startup_settling,signal_check_passed,startup_status";
        public readonly double SettlingSeconds, ConfirmationSeconds, MinimumPpgSpanMv;
        public bool IsReady { get; private set; }
        public bool SignalCheckPassed { get; private set; }
        public string Status { get; private set; } = "waiting_samples";
        public bool HasSamples { get; private set; }
        public double FirstDeviceTime { get; private set; }
        public double ReadyDeviceTime { get; private set; } = double.NaN;
        int segment = -1;
        double firstReceive, lastDevice = -1, lastReceive = -1, validSince = -1, lastRangeChange = double.NegativeInfinity;
        struct Point { public double Time, Value; }
        readonly Queue<Point> window = new Queue<Point>();

        public ShimmerSignalReadiness(double settlingSeconds = 5, double confirmationSeconds = 2, double minimumPpgSpanMv = 10)
        {
            if (!Finite(settlingSeconds) || settlingSeconds < 5 || !Finite(confirmationSeconds) || confirmationSeconds < 2 ||
                !Finite(minimumPpgSpanMv) || minimumPpgSpanMv <= 0) throw new ArgumentOutOfRangeException("Invalid startup policy");
            SettlingSeconds = settlingSeconds; ConfirmationSeconds = confirmationSeconds; MinimumPpgSpanMv = minimumPpgSpanMv;
        }
        public void Reset()
        {
            IsReady = SignalCheckPassed = HasSamples = false;
            segment = -1; lastDevice = lastReceive = validSince = -1;
            lastRangeChange = double.NegativeInfinity; ReadyDeviceTime = double.NaN;
            Status = "waiting_samples"; window.Clear();
        }
        static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        void Reject(string reason) { window.Clear(); validSince = -1; SignalCheckPassed = false; Status = reason; }

        public void Observe(int connection, double deviceTime, double received, double now,
            double edaUs, bool edaValid, double ppgMv, bool ppgValid, bool rangeChanged)
        {
            if (segment != connection) { Reset(); segment = connection; }
            if (!Finite(deviceTime) || !Finite(received) || !Finite(now) || deviceTime < 0 ||
                (HasSamples && (deviceTime <= lastDevice || received < lastReceive)))
            { Reject("invalid_timing"); return; }
            if (!HasSamples) { HasSamples = true; FirstDeviceTime = deviceTime; firstReceive = received; }
            if (lastDevice >= 0 && (deviceTime - lastDevice > 0.25 || received - lastReceive > 0.5)) Reject("sample_gap");
            lastDevice = deviceTime; lastReceive = received;
            if (now < received || now - received > 0.5) { Reject("stale_samples"); return; }
            if (rangeChanged) lastRangeChange = deviceTime;
            if (!edaValid || !Finite(edaUs) || edaUs < 0.2 || edaUs > 125) { Reject("check_eda"); return; }
            if (deviceTime - lastRangeChange < 0.5) { Reject("gsr_range_settling"); return; }
            if (!ppgValid || !Finite(ppgMv) || ppgMv < 50 || ppgMv > 2950) { Reject("check_ppg_level"); return; }
            if (validSince < 0) validSince = deviceTime;
            window.Enqueue(new Point { Time = deviceTime, Value = ppgMv });
            while (window.Count > 0 && window.Peek().Time < deviceTime - ConfirmationSeconds) window.Dequeue();
            SignalCheckPassed = false;
            if (deviceTime - validSince < ConfirmationSeconds || window.Count < 128)
            { Status = "confirming_signals"; return; }
            // Robust spread ignores an isolated spike in an otherwise flat ADC stream.
            var values = new double[window.Count]; int i = 0;
            foreach (var point in window) values[i++] = point.Value;
            Array.Sort(values);
            double span = values[(int)((values.Length - 1) * 0.95)] - values[(int)((values.Length - 1) * 0.05)];
            if (span < MinimumPpgSpanMv) { Status = "check_ppg_variation"; return; }
            SignalCheckPassed = true;
            if (!IsReady && (deviceTime - FirstDeviceTime < SettlingSeconds || received - firstReceive < SettlingSeconds))
            { Status = "settling"; return; }
            if (!IsReady) { IsReady = true; ReadyDeviceTime = deviceTime; }
            Status = "ready";
        }

        public string Message => Status == "waiting_samples" ? "Waiting for samples" :
            Status == "check_eda" ? "Check EDA electrodes" :
            Status == "check_ppg_level" ? "Check PPG probe: signal near zero or saturated" :
            Status == "check_ppg_variation" ? "Check PPG probe: no clear signal variation" :
            Status == "gsr_range_settling" ? "Waiting for EDA range to settle" :
            Status == "stale_samples" || Status == "invalid_timing" ? "Waiting for fresh advancing samples" :
            Status == "ready" ? "Ready" : Status == "settling" ? "Allowing sensors to settle" : "Confirming EDA and PPG signals";
    }
}
