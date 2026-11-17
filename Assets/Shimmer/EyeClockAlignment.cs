using System;

namespace AngerRoom.Shimmer
{
    // Map the SDK's seconds clock to session seconds with a bracketed clock read.
    // The bracket bounds call timing only, not the eye sensor's acquisition latency.
    public sealed class EyeClockAlignment
    {
        public double Offset { get; private set; }
        public double HalfWidth { get; private set; }
        public double CalibratedAt { get; private set; } = double.NegativeInfinity;
        public int Revision { get; private set; }
        public static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);

        public bool Calibrate(double before, double sdkNow, double after)
        {
            if (!Finite(before) || !Finite(after) || !Finite(sdkNow) || sdkNow <= 0 ||
                after < before || after - before > 0.004) return false;
            Offset = (before + after) * 0.5 - sdkNow;
            HalfWidth = (after - before) * 0.5;
            CalibratedAt = after;
            Revision++;
            return true;
        }

        public bool TryMap(double sdkTime, double observation, out double mapped)
        {
            mapped = sdkTime + Offset;
            // A loose plausibility check detects a different clock domain or stale data.
            // It is not a measured accuracy guarantee. Future predicted poses are allowed.
            return Finite(sdkTime) && sdkTime > 0 && Finite(observation) &&
                observation >= CalibratedAt && observation - CalibratedAt <= 3 &&
                Math.Abs(mapped - observation) <= 1;
        }

        public void Invalidate() { CalibratedAt = double.NegativeInfinity; }
    }
}
