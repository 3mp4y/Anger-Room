package com.angerroom.shimmer;

import java.io.IOException;

/** Shimmer3 LogAndStream, GSR + internal ADC13 only, CRC off, one sample/packet. */
public final class ShimmerStreamDecoder {
    public static final int PPG_CHANNEL = 0x12;
    public static final int GSR_CHANNEL = 0x1C;
    private static final double[] FEEDBACK_OHMS = {40200, 287000, 1000000, 3300000};
    public final int divider;
    public final double sampleRateHz;
    private final int ppgOffset;
    private final int gsrOffset;
    private int previousTick = -1;
    private int previousRange = -1;
    private long unwrappedTicks;
    private long firstTicks;
    public long samples;
    public long estimatedMissing;

    public ShimmerStreamDecoder(byte[] inquiry) throws IOException {
        if (inquiry.length != 10 || (inquiry[6] & 255) != 2 || (inquiry[7] & 255) != 1)
            throw new IOException("Expected exactly two channels and buffer size 1");
        divider = u16(inquiry, 0);
        if (divider != 256) throw new IOException("Sampling rate readback is not 128 Hz");
        if ((inquiry[5] & 1) == 0) throw new IOException("Expansion power is off; PPG would be unpowered");
        if (((inquiry[5] >> 1) & 7) != 4) throw new IOException("GSR autorange readback failed");
        sampleRateHz = 32768.0 / divider;
        int first = inquiry[8] & 255, second = inquiry[9] & 255;
        if (first == PPG_CHANNEL && second == GSR_CHANNEL) { ppgOffset = 4; gsrOffset = 6; }
        else if (first == GSR_CHANNEL && second == PPG_CHANNEL) { ppgOffset = 6; gsrOffset = 4; }
        else throw new IOException("Inquiry lacks GSR and internal ADC13 channels");
    }

    public Sample decode(byte[] packet, long receiveNanos, long receiveUtcMs) throws IOException {
        if (packet.length != 8 || packet[0] != 0) throw new IOException("Invalid data frame");
        int ticks = (packet[1] & 255) | ((packet[2] & 255) << 8) | ((packet[3] & 255) << 16);
        long missing = 0;
        if (previousTick < 0) { unwrappedTicks = ticks; firstTicks = ticks; }
        else {
            int delta = (ticks - previousTick) & 0xFFFFFF;
            if (delta == 0 || delta >= 0x800000) throw new IOException("Duplicate or backwards Shimmer timestamp");
            unwrappedTicks += delta;
            // Firmware timing jitter is tolerated; gaps are estimates, not sequence numbers.
            if (delta > divider * 1.5) missing = Math.max(0, Math.round(delta / (double)divider) - 1);
        }
        previousTick = ticks;
        int word = u16(packet, gsrOffset), adc = word & 0x3FFF, range = word >>> 14;
        int ppg = u16(packet, ppgOffset);
        boolean changed = previousRange >= 0 && previousRange != range;
        previousRange = range;
        boolean edaValid = adc > 682 && adc < 4095;
        double eda = edaValid ? ((adc * 3000.0 / 4095.0 / 500.0) - 1.0) * 1e6 / FEEDBACK_OHMS[range] : Double.NaN;
        boolean ppgValid = ppg > 0 && ppg < 4095;
        estimatedMissing += missing;
        samples++;
        return new Sample(samples, ticks, unwrappedTicks, (unwrappedTicks - firstTicks) / 32768.0,
                receiveNanos, receiveUtcMs, word, adc, range, eda, edaValid, changed,
                ppg, ppg <= 4095 ? ppg * 3000.0 / 4095.0 : Double.NaN, ppgValid, missing);
    }

    public static int u16(byte[] bytes, int offset) { return (bytes[offset] & 255) | ((bytes[offset + 1] & 255) << 8); }

    public static final class Sample {
        public final long sequence, ticks, unwrapped, receiveNanos, receiveUtcMs, missing;
        public final double elapsed, eda, ppgMv;
        public final int gsrWord, gsrAdc, range, ppg;
        public final boolean edaValid, rangeChanged, ppgValid;
        Sample(long sequence, long ticks, long unwrapped, double elapsed, long receiveNanos, long receiveUtcMs,
               int gsrWord, int gsrAdc, int range, double eda, boolean edaValid, boolean rangeChanged,
               int ppg, double ppgMv, boolean ppgValid, long missing) {
            this.sequence = sequence; this.ticks = ticks; this.unwrapped = unwrapped; this.elapsed = elapsed;
            this.receiveNanos = receiveNanos; this.receiveUtcMs = receiveUtcMs;
            this.gsrWord = gsrWord; this.gsrAdc = gsrAdc; this.range = range; this.eda = eda;
            this.edaValid = edaValid; this.rangeChanged = rangeChanged; this.ppg = ppg;
            this.ppgMv = ppgMv; this.ppgValid = ppgValid; this.missing = missing;
        }
        public String csv() {
            return sequence + "," + ticks + "," + unwrapped + "," + elapsed + "," + receiveNanos + "," + receiveUtcMs
                    + "," + gsrWord + "," + gsrAdc + "," + range + "," + (edaValid ? Double.toString(eda) : "")
                    + "," + edaValid + "," + rangeChanged + "," + ppg + "," + (Double.isNaN(ppgMv) ? "" : Double.toString(ppgMv))
                    + "," + ppgValid + "," + missing;
        }
    }
}
