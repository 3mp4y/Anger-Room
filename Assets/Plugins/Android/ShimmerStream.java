package com.angerroom.shimmer;

import android.app.Activity;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothManager;
import android.bluetooth.BluetoothSocket;
import android.os.SystemClock;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.Arrays;
import java.util.UUID;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;

/** One connection per instance. Unity polls queues on its main thread. */
public final class ShimmerStream {
    private final Activity activity;
    private final ArrayBlockingQueue<String> samples = new ArrayBlockingQueue<>(8192);
    private final ArrayBlockingQueue<String> events = new ArrayBlockingQueue<>(128);
    private volatile boolean stopping;
    private volatile boolean running;
    private volatile long queueDrops;
    private volatile BluetoothSocket socket;
    private volatile String status = "Idle";
    private InputStream input;
    private OutputStream output;
    private ShimmerStreamDecoder decoder;
    private boolean streaming;
    private boolean started;

    public ShimmerStream(Activity activity) { this.activity = activity; }

    public synchronized void start(String pairedDeviceName) {
        if (started) throw new IllegalStateException("Create a new receiver for each connection");
        started = true;
        running = true;
        new Thread(() -> run(pairedDeviceName), "ShimmerEdaPpg").start();
    }

    public void stop() { stopping = true; }
    public boolean isRunning() { return running; }
    public String getStatus() { return status; }
    public long getQueueDrops() { return queueDrops; }
    public String drainEvents() { return drain(events, 128); }
    public String drainSamples(int maximum) { return drain(samples, Math.max(1, Math.min(maximum, 1024))); }
    private String drain(ArrayBlockingQueue<String> queue, int maximum) {
        StringBuilder result = new StringBuilder();
        for (int i = 0; i < maximum; i++) {
            String row = queue.poll();
            if (row == null) break;
            result.append(row).append('\n');
        }
        return result.toString();
    }
    private void report(String message) {
        status = message;
        if (!events.offer(System.currentTimeMillis() + " " + message)) {
            events.poll(); events.offer(System.currentTimeMillis() + " " + message);
        }
    }

    private void run(String name) {
        ScheduledExecutorService timer = Executors.newSingleThreadScheduledExecutor();
        ScheduledFuture<?> timeout = null;
        try {
            BluetoothManager manager = (BluetoothManager)activity.getSystemService(Activity.BLUETOOTH_SERVICE);
            BluetoothAdapter adapter = manager == null ? null : manager.getAdapter();
            if (adapter == null || !adapter.isEnabled()) throw new IOException("Enable Bluetooth on the headset");
            BluetoothDevice selected = null;
            for (BluetoothDevice device : adapter.getBondedDevices()) {
                if (name.equals(device.getName()) || name.equalsIgnoreCase(device.getAddress())) {
                    if (selected != null) throw new IOException("More than one paired device matches; use its full address");
                    selected = device;
                }
            }
            if (selected == null) throw new IOException("Pair " + name + " in Quest Bluetooth settings or Shimmer Connection Test first");
            adapter.cancelDiscovery();
            checkStop();
            socket = selected.createRfcommSocketToServiceRecord(UUID.fromString("00001101-0000-1000-8000-00805f9b34fb"));
            report("Connecting to " + name);
            timeout = timer.schedule(this::close, 15, TimeUnit.SECONDS);
            socket.connect();
            timeout.cancel(false);
            input = socket.getInputStream(); output = socket.getOutputStream();
            checkStop();
            if ((request(0x3F, 0x25, 1)[0] & 255) != 3) throw new IOException("Only Shimmer3 is supported");
            byte[] firmware = request(0x2E, 0x2F, 6);
            if (ShimmerStreamDecoder.u16(firmware, 0) != 3 || ShimmerStreamDecoder.u16(firmware, 2) != 1)
                throw new IOException("Expected Shimmer3 LogAndStream 1.x; firmware=" + Arrays.toString(firmware));
            report("Firmware " + ShimmerStreamDecoder.u16(firmware, 2) + "." + (firmware[4] & 255) + "." + (firmware[5] & 255));
            int deviceStatus = statusRequest();
            if ((deviceStatus & 0x1B) != 0) throw new IOException("Shimmer must be undocked and idle; status=" + deviceStatus);
            byte[] before = inquiry();
            report("Previous inquiry " + Arrays.toString(before));
            // These sensor settings persist on Shimmer. Set only the requested measurement channels.
            set(0x08, 0x04, 0x01, 0x00); // GSR and internal ADC13 (optical PPG)
            set(0x05, 0x00, 0x01);       // 32768 / 256 = 128 Hz
            set(0x21, 0x04);             // GSR auto-range
            set(0x5E, 0x01);             // expansion power for the optical probe
            byte[] config = inquiry();
            decoder = new ShimmerStreamDecoder(config);
            report("Configuration verified: EDA + PPG at 128 Hz; inquiry " + Arrays.toString(config));
            checkStop();
            set(0x07);
            streaming = true;
            // Confirm acquisition by actual complete sample packets, not just the command ACK.
            while (!stopping) readStreamFrame(false);
            stopStreaming();
            report("Stopped. Samples=" + decoder.samples + "; estimated missing=" + decoder.estimatedMissing + "; queue drops=" + queueDrops);
        } catch (Exception error) {
            if (streaming) {
                try { stopStreaming(); } catch (Exception ignored) { }
            }
            report(stopping ? "Stopped" : "ERROR: " + error.getMessage());
        } finally {
            if (timeout != null) timeout.cancel(false);
            timer.shutdownNow();
            close();
            running = false;
        }
    }

    private void checkStop() throws IOException { if (stopping) throw new IOException("Stopped"); }
    private void close() { if (socket != null) try { socket.close(); } catch (IOException ignored) { } }

    private int read(long deadline) throws Exception {
        while (input.available() == 0) {
            if (SystemClock.elapsedRealtime() >= deadline) throw new IOException("Bluetooth response/sample timeout");
            Thread.sleep(5);
        }
        int value = input.read();
        if (value < 0) throw new IOException("Bluetooth disconnected");
        return value;
    }
    private byte[] readBytes(int count, long deadline) throws Exception {
        byte[] bytes = new byte[count];
        for (int i = 0; i < count; i++) bytes[i] = (byte)read(deadline);
        return bytes;
    }
    private void expect(int value, long deadline) throws Exception {
        int actual = read(deadline);
        if (actual != value) throw new IOException("Protocol: expected " + value + ", got " + actual);
    }
    private void send(int... values) throws Exception {
        byte[] bytes = new byte[values.length];
        for (int i = 0; i < values.length; i++) bytes[i] = (byte)values[i];
        output.write(bytes); output.flush();
    }
    private void set(int... values) throws Exception {
        checkStop(); send(values); expect(0xFF, SystemClock.elapsedRealtime() + 3000);
    }
    private byte[] request(int command, int response, int count) throws Exception {
        checkStop(); send(command);
        long deadline = SystemClock.elapsedRealtime() + 3000;
        expect(0xFF, deadline); expect(response, deadline);
        return readBytes(count, deadline);
    }
    private int statusRequest() throws Exception {
        byte[] response = request(0x72, 0x8A, 2);
        if ((response[0] & 255) != 0x71) throw new IOException("Invalid status response");
        return response[1] & 255;
    }
    private byte[] inquiry() throws Exception {
        byte[] header = request(0x01, 0x02, 8);
        int count = header[6] & 255;
        if (count > 45) throw new IOException("Invalid inquiry channel count");
        byte[] result = Arrays.copyOf(header, 8 + count);
        byte[] channels = readBytes(count, SystemClock.elapsedRealtime() + 3000);
        System.arraycopy(channels, 0, result, 8, count);
        return result;
    }
    private boolean readStreamFrame(boolean awaitingStop) throws Exception {
        long deadline = SystemClock.elapsedRealtime() + 3000;
        int header = read(deadline);
        if (header == 0x00) {
            byte[] packet = new byte[8];
            byte[] tail = readBytes(7, deadline);
            System.arraycopy(tail, 0, packet, 1, 7);
            ShimmerStreamDecoder.Sample sample = decoder.decode(packet, SystemClock.elapsedRealtimeNanos(), System.currentTimeMillis());
            if (!samples.offer(sample.csv())) queueDrops++;
            if (decoder.samples == 1 || decoder.samples % 128 == 0)
                report("Streaming EDA + PPG | samples=" + decoder.samples + " | missing~=" + decoder.estimatedMissing + " | queue drops=" + queueDrops);
            return false;
        }
        if (header == 0xFF) {
            if (awaitingStop) return true;
            header = read(deadline);
        }
        if (header == 0x8A) {
            expect(0x71, deadline);
            int status = read(deadline);
            if (!awaitingStop && (status & 0x10) == 0) throw new IOException("Shimmer stopped acquisition; status=" + status);
            report("Device status=" + status);
            return false;
        }
        throw new IOException("Unexpected stream header " + header + "; stopping rather than guessing framing");
    }
    private void stopStreaming() throws Exception {
        if (!streaming) return;
        send(0x20);
        long deadline = SystemClock.elapsedRealtime() + 3000;
        while (SystemClock.elapsedRealtime() < deadline) {
            if (readStreamFrame(true)) { streaming = false; return; }
        }
        throw new IOException("No stop acknowledgement");
    }
}
