package com.angerroom.shimmerprobe;

import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothSocket;
import android.os.SystemClock;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.util.Arrays;
import java.util.Locale;
import java.util.UUID;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;

/** Runs entirely off the UI thread; closing the socket interrupts connect/read. */
public final class ShimmerConnection implements Runnable {
    public interface Listener { void message(String text); void finished(); }
    private final BluetoothDevice device;
    private final Listener listener;
    private volatile BluetoothSocket socket;
    private volatile boolean cancelled;
    private volatile boolean connectTimedOut;

    public ShimmerConnection(BluetoothDevice device, Listener listener) {
        this.device = device;
        this.listener = listener;
    }

    public void cancel() { cancelled = true; close(); }

    private void close() {
        BluetoothSocket current = socket;
        if (current != null) try { current.close(); } catch (IOException ignored) { }
    }

    @Override public void run() {
        ScheduledExecutorService timer = Executors.newSingleThreadScheduledExecutor();
        ScheduledFuture<?> watchdog = null;
        try {
            if (cancelled) return;
            socket = device.createRfcommSocketToServiceRecord(UUID.fromString("00001101-0000-1000-8000-00805f9b34fb"));
            if (cancelled) return;
            listener.message("Opening Bluetooth Classic serial connection (15 s timeout)...");
            watchdog = timer.schedule(() -> { connectTimedOut = true; close(); }, 15, TimeUnit.SECONDS);
            socket.connect();
            watchdog.cancel(false);
            if (connectTimedOut) throw new IOException("Connection timed out");
            listener.message("RFCOMM_CONNECTED: testing Shimmer protocol...");
            byte[] hardware = request(ShimmerProtocol.GET_DEVICE_VERSION, 0x25, 1);
            int model = hardware[0] & 255;
            listener.message("Hardware ID: " + model + (model == 3 ? " (Shimmer3)" : ""));
            if (model != 3) throw new IOException("This test expects Shimmer3 hardware ID 3");
            byte[] firstFirmware = null;
            double total = 0;
            double maximum = 0;
            for (int i = 1; i <= 15; i++) {
                if (cancelled) throw new IOException("Cancelled");
                long start = SystemClock.elapsedRealtimeNanos();
                byte[] firmware = request(ShimmerProtocol.GET_FW_VERSION, 0x2F, 6);
                double ms = (SystemClock.elapsedRealtimeNanos() - start) / 1e6;
                if (firstFirmware == null) {
                    firstFirmware = firmware;
                    listener.message(ShimmerProtocol.firmware(firmware));
                } else if (!Arrays.equals(firstFirmware, firmware)) {
                    throw new IOException("Firmware response changed during test");
                }
                total += ms;
                maximum = Math.max(maximum, ms);
                listener.message(String.format(Locale.US, "Reply %d/15 OK (round trip %.1f ms)", i, ms));
                if (i < 15) Thread.sleep(1000);
            }
            listener.message(String.format(Locale.US,
                    "PASS: Shimmer3 replied correctly 15/15 times. Mean round trip %.1f ms; max %.1f ms.\nConnection test only: GSR streaming and timestamp synchronization are not tested.", total / 15, maximum));
        } catch (Exception error) {
            listener.message(cancelled ? "CANCELLED" : "FAIL: " + (connectTimedOut ? "Bluetooth connection timed out" : error.toString()));
        } finally {
            if (watchdog != null) watchdog.cancel(false);
            timer.shutdownNow();
            close();
            listener.finished();
        }
    }

    private byte[] request(int command, int response, int size) throws Exception {
        socket.getOutputStream().write(command);
        socket.getOutputStream().flush();
        InputStream input = socket.getInputStream();
        ByteArrayOutputStream frame = new ByteArrayOutputStream();
        long deadline = SystemClock.elapsedRealtime() + 3000;
        while (frame.size() < size + 2) {
            if (cancelled) throw new IOException("Cancelled");
            if (SystemClock.elapsedRealtime() >= deadline)
                throw new IOException("Response timeout for " + Integer.toHexString(command) + "; received " + ShimmerProtocol.hex(frame.toByteArray()));
            if (input.available() == 0) { Thread.sleep(10); continue; }
            int value = input.read();
            if (value < 0) throw new IOException("Shimmer disconnected");
            frame.write(value);
            if (frame.size() == 1 && value == 0xFE) break;
        }
        byte[] bytes = frame.toByteArray();
        listener.message("RX " + ShimmerProtocol.hex(bytes));
        return ShimmerProtocol.payload(bytes, response, size);
    }
}
