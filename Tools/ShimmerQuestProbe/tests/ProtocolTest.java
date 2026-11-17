import com.angerroom.shimmerprobe.ShimmerProtocol;
import java.io.IOException;

public final class ProtocolTest {
    public static void main(String[] args) throws Exception {
        byte[] frame = {(byte)0xFF, 0x2F, 3, 0, 1, 0, 2, (byte)201};
        String result = ShimmerProtocol.firmware(ShimmerProtocol.payload(frame, 0x2F, 6));
        if (!result.equals("firmware ID 3, version 1.2.201")) throw new AssertionError(result);
        byte[][] invalid = { {}, {(byte)0xFF}, {(byte)0xFE},
            {(byte)0xFF, 0x25, 3, 0, 1, 0, 2, 3},
            {0, 0x2F, 3, 0, 1, 0, 2, 3},
            {(byte)0xFF, 0x2F, 3, 0, 1, 0, 2},
            {(byte)0xFF, 0x2F, 3, 0, 1, 0, 2, 3, 4} };
        for (byte[] bytes : invalid) {
            try { ShimmerProtocol.payload(bytes, 0x2F, 6); throw new AssertionError("Accepted invalid frame"); }
            catch (IOException expected) { }
        }
        System.out.println("PASS: firmware decoding and rejection of NACK, bad headers, truncated and extra bytes");
    }
}
