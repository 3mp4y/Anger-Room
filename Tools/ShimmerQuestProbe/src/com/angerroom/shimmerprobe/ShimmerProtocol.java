package com.angerroom.shimmerprobe;

import java.io.IOException;
import java.util.Locale;

/** Only read-only identity commands. No configuration or acquisition commands. */
public final class ShimmerProtocol {
    public static final int GET_DEVICE_VERSION = 0x3F;
    public static final int GET_FW_VERSION = 0x2E;

    private ShimmerProtocol() { }

    public static byte[] payload(byte[] frame, int response, int length) throws IOException {
        if (frame.length > 0 && (frame[0] & 255) == 0xFE)
            throw new IOException("Shimmer rejected the command (NACK). Check that SD sync is disabled.");
        if (frame.length != length + 2 || (frame[0] & 255) != 0xFF || (frame[1] & 255) != response)
            throw new IOException("Unexpected response: " + hex(frame) + ". Stop acquisition and reconnect; no configuration was changed.");
        byte[] result = new byte[length];
        System.arraycopy(frame, 2, result, 0, length);
        return result;
    }

    public static String firmware(byte[] payload) {
        if (payload.length != 6) throw new IllegalArgumentException("Expected six firmware bytes");
        return String.format(Locale.US, "firmware ID %d, version %d.%d.%d",
                u16(payload, 0), u16(payload, 2), payload[4] & 255, payload[5] & 255);
    }

    private static int u16(byte[] bytes, int offset) {
        return (bytes[offset] & 255) | ((bytes[offset + 1] & 255) << 8);
    }

    public static String hex(byte[] bytes) {
        StringBuilder result = new StringBuilder();
        for (byte value : bytes) result.append(String.format(Locale.US, "%02X ", value & 255));
        return result.toString().trim();
    }
}
