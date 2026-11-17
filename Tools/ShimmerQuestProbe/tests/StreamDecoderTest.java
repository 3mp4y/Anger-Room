import com.angerroom.shimmer.ShimmerStreamDecoder;
import java.io.IOException;

public final class StreamDecoderTest {
    static byte[] inquiry(boolean reverse) {
        return new byte[]{0, 1, 0, 0, 0, 9, 2, 1, (byte)(reverse ? 0x1C : 0x12), (byte)(reverse ? 0x12 : 0x1C)};
    }
    static byte[] packet(int ticks, int ppg, int gsr, boolean reverse) {
        int a = reverse ? gsr : ppg, b = reverse ? ppg : gsr;
        return new byte[]{0, (byte)ticks, (byte)(ticks >> 8), (byte)(ticks >> 16), (byte)a, (byte)(a >> 8), (byte)b, (byte)(b >> 8)};
    }
    static void check(boolean value, String message) { if (!value) throw new AssertionError(message); }
    public static void main(String[] args) throws Exception {
        for (boolean reverse : new boolean[]{false, true}) {
            ShimmerStreamDecoder decoder = new ShimmerStreamDecoder(inquiry(reverse));
            ShimmerStreamDecoder.Sample a = decoder.decode(packet(0xFFFF00, 2048, 2000 | (2 << 14), reverse), 100, 200);
            check(a.ppg == 2048 && a.gsrAdc == 2000 && a.range == 2, "Channel order / range decoding");
            check(Math.abs(a.ppgMv - 1500.3663003663) < 1e-8, "PPG conversion");
            check(Math.abs(a.eda - 1.93040293040293) < 1e-8, "EDA conversion");
            ShimmerStreamDecoder.Sample b = decoder.decode(packet(0, 4095, 2000 | (3 << 14), reverse), 101, 201);
            check(b.unwrapped == 0x1000000L && Math.abs(b.elapsed - 1.0 / 128) < 1e-12, "24-bit timestamp rollover");
            check(b.rangeChanged && !b.ppgValid && b.missing == 0, "Range change / saturation");
            ShimmerStreamDecoder.Sample c = decoder.decode(packet(768, 0, 0, reverse), 102, 202);
            check(c.missing == 2 && decoder.estimatedMissing == 2 && !c.edaValid && Double.isNaN(c.eda), "Gap / invalid EDA");
            check(c.csv().split(",", -1).length == 16 && c.csv().contains(",0,,false,"), "CSV invalid value is empty");
            try { decoder.decode(packet(768, 2048, 2000, reverse), 103, 203); throw new AssertionError("Duplicate accepted"); }
            catch (IOException expected) { }
        }
        for (int index : new int[]{0, 5, 6, 7, 8, 9}) {
            byte[] bad = inquiry(false); bad[index] = 0;
            if (index == 0) bad[index] = 1;
            try { new ShimmerStreamDecoder(bad); throw new AssertionError("Invalid inquiry accepted: " + index); }
            catch (IOException expected) { }
        }
        ShimmerStreamDecoder decoder = new ShimmerStreamDecoder(inquiry(false));
        for (int range = 0; range < 4; range++) {
            ShimmerStreamDecoder.Sample sample = decoder.decode(packet(256 * range, 1000, (range << 14) | 4095, false), 0, 0);
            check(!sample.edaValid, "Saturated EDA accepted");
        }
        System.out.println("PASS: EDA/PPG scaling, inquiry order, timestamp rollover, missing samples, invalid readings and malformed configurations");
    }
}
