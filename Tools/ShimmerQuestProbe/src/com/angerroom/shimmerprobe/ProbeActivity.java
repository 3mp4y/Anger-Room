package com.angerroom.shimmerprobe;

import android.Manifest;
import android.app.Activity;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothDevice;
import android.bluetooth.BluetoothManager;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.content.pm.PackageManager;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import android.util.Log;
import android.view.View;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;
import java.io.File;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.Map;

public final class ProbeActivity extends Activity {
    private BluetoothAdapter adapter;
    private LinearLayout devicesLayout;
    private TextView status;
    private PrintWriter logFile;
    private final Map<String, BluetoothDevice> devices = new LinkedHashMap<>();
    private ShimmerConnection connection;
    private String pendingAddress;
    private boolean registered;
    private boolean destroyed;
    private boolean automaticAttempted;
    private String targetSuffix;

    private final BroadcastReceiver receiver = new BroadcastReceiver() {
        @Override public void onReceive(Context context, Intent intent) {
            if (!permitted()) return;
            BluetoothDevice device = intent.getParcelableExtra(BluetoothDevice.EXTRA_DEVICE);
            if (BluetoothDevice.ACTION_FOUND.equals(intent.getAction()) && device != null) addDevice(device);
            else if (BluetoothAdapter.ACTION_DISCOVERY_FINISHED.equals(intent.getAction())) {
                message("Scan finished. Select your Shimmer below, or scan again.");
            } else if (BluetoothDevice.ACTION_BOND_STATE_CHANGED.equals(intent.getAction()) && device != null) {
                int state = intent.getIntExtra(BluetoothDevice.EXTRA_BOND_STATE, BluetoothDevice.BOND_NONE);
                message("Pairing " + device.getAddress() + ": " + state);
                addDevice(device);
                if (device.getAddress().equals(pendingAddress)) {
                    if (state == BluetoothDevice.BOND_BONDED) { pendingAddress = null; test(device); }
                    else if (state == BluetoothDevice.BOND_NONE) { pendingAddress = null; message("Pairing did not complete. Select the device to retry."); }
                }
            }
        }
    };

    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        targetSuffix = getIntent().getStringExtra("target_suffix");
        try {
            File file = new File(getFilesDir(), "probe-" + System.currentTimeMillis() + ".txt");
            logFile = new PrintWriter(new FileWriter(file), true);
            Log.i("ShimmerProbe", "LOG_FILE " + file.getAbsolutePath());
        } catch (Exception e) { Log.e("ShimmerProbe", "Cannot save diagnostic log", e); }

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(24, 16, 24, 16);
        ScrollView scroll = new ScrollView(this);
        scroll.addView(root);
        setContentView(scroll);
        TextView title = new TextView(this);
        title.setText("Shimmer / Quest connection test");
        title.setTextSize(26);
        root.addView(title);
        TextView instructions = new TextView(this);
        instructions.setText("Turn on Shimmer and disconnect Consensys. Scan, then select the Shimmer's Classic Bluetooth entry. Approve pairing (PIN 1234 if requested). This test reads identity only; it does not record GSR.");
        instructions.setTextSize(18);
        root.addView(instructions);
        LinearLayout actions = new LinearLayout(this);
        root.addView(actions);
        button(actions, "Scan / retry permissions", v -> prepare());
        button(actions, "Bluetooth settings", v -> {
            try { startActivity(new Intent(Settings.ACTION_BLUETOOTH_SETTINGS)); }
            catch (Exception error) { message("Open Bluetooth from Quest Settings: " + error.getMessage()); }
        });
        button(actions, "Cancel test", v -> { if (connection != null) connection.cancel(); pendingAddress = null; });
        devicesLayout = new LinearLayout(this);
        devicesLayout.setOrientation(LinearLayout.VERTICAL);
        root.addView(devicesLayout);
        status = new TextView(this);
        status.setTextSize(16);
        status.setTextIsSelectable(true);
        root.addView(status);
        BluetoothManager manager = (BluetoothManager)getSystemService(BLUETOOTH_SERVICE);
        adapter = manager == null ? null : manager.getAdapter();
        IntentFilter filter = new IntentFilter(BluetoothDevice.ACTION_FOUND);
        filter.addAction(BluetoothDevice.ACTION_BOND_STATE_CHANGED);
        filter.addAction(BluetoothAdapter.ACTION_DISCOVERY_FINISHED);
        registerReceiver(receiver, filter);
        registered = true;
        message("Headset: " + Build.MODEL + "; Android API " + Build.VERSION.SDK_INT);
        prepare();
    }

    private void button(LinearLayout parent, String label, View.OnClickListener listener) {
        Button button = new Button(this);
        button.setText(label);
        button.setOnClickListener(listener);
        parent.addView(button);
    }

    private boolean permitted() {
        if (Build.VERSION.SDK_INT >= 31)
            return checkSelfPermission(Manifest.permission.BLUETOOTH_CONNECT) == PackageManager.PERMISSION_GRANTED
                    && checkSelfPermission(Manifest.permission.BLUETOOTH_SCAN) == PackageManager.PERMISSION_GRANTED;
        return checkSelfPermission(Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED;
    }

    private void prepare() {
        if (connection != null || pendingAddress != null) { message("A connection or pairing test is already running."); return; }
        if (adapter == null) { message("FAIL: no Bluetooth adapter available"); return; }
        if (!permitted()) {
            message("Allow Nearby devices in the headset permission dialog to continue.");
            requestPermissions(Build.VERSION.SDK_INT >= 31
                    ? new String[]{Manifest.permission.BLUETOOTH_CONNECT, Manifest.permission.BLUETOOTH_SCAN}
                    : new String[]{Manifest.permission.ACCESS_FINE_LOCATION}, 1);
            return;
        }
        try {
            if (!adapter.isEnabled()) { message("Bluetooth is disabled. Enable it in Quest Settings, then scan again."); return; }
            adapter.cancelDiscovery();
            devices.clear();
            devicesLayout.removeAllViews();
            for (BluetoothDevice device : adapter.getBondedDevices()) addDevice(device);
            if (connection != null || pendingAddress != null) return;
            message(adapter.startDiscovery() ? "Scanning from the HEADSET for nearby devices..." : "FAIL: discovery could not start; try Quest Bluetooth settings.");
        } catch (Exception error) { message("Bluetooth setup failed: " + error); }
    }

    @Override public void onRequestPermissionsResult(int request, String[] permissions, int[] results) {
        super.onRequestPermissionsResult(request, permissions, results);
        if (permitted()) prepare();
        else message("Permission denied. Use Scan / retry permissions, or allow Nearby devices in app permissions.");
    }

    private void addDevice(BluetoothDevice device) {
        String name = device.getName();
        if (name == null) name = "Unnamed device";
        boolean fresh = !devices.containsKey(device.getAddress());
        devices.put(device.getAddress(), device);
        if (fresh) message("FOUND " + name + " " + device.getAddress() + " bond=" + device.getBondState());
        devicesLayout.removeAllViews();
        for (BluetoothDevice item : devices.values()) {
            String label = item.getName() + "  " + item.getAddress()
                    + (item.getBondState() == BluetoothDevice.BOND_BONDED ? "  [paired]" : "  [pair]");
            button(devicesLayout, label, v -> select(item));
        }
        if (!automaticAttempted && targetSuffix != null
                && name.toUpperCase(java.util.Locale.US).startsWith("SHIMMER3-")
                && name.toUpperCase(java.util.Locale.US).endsWith(targetSuffix.toUpperCase(java.util.Locale.US))) {
            automaticAttempted = true;
            select(device);
        }
    }

    private void select(BluetoothDevice device) {
        if (connection != null || pendingAddress != null) { message("Finish or cancel the current test first."); return; }
        try {
            adapter.cancelDiscovery();
            if (device.getBondState() != BluetoothDevice.BOND_BONDED) {
                pendingAddress = device.getAddress();
                message("Pairing " + device.getName() + ". Approve the headset dialog; enter 1234 if asked.");
                if (!device.createBond()) { pendingAddress = null; message("Could not begin pairing. Try Quest Bluetooth settings."); }
            } else test(device);
        } catch (Exception error) { pendingAddress = null; message("Pairing failed: " + error); }
    }

    private void test(BluetoothDevice device) {
        message("TEST_START " + device.getName() + " " + device.getAddress());
        connection = new ShimmerConnection(device, new ShimmerConnection.Listener() {
            @Override public void message(String text) { runOnUiThread(() -> ProbeActivity.this.message(text)); }
            @Override public void finished() { runOnUiThread(() -> { connection = null; message("Test finished; socket closed. Select the device to repeat."); }); }
        });
        new Thread(connection, "ShimmerProbe").start();
    }

    private void message(String text) {
        if (destroyed) return;
        String line = Instant.now().toString() + " " + text;
        Log.i("ShimmerProbe", line);
        if (logFile != null) logFile.println(line);
        // Keep the newest status visible, above previous diagnostics.
        status.setText(text + "\n\n" + status.getText());
    }

    @Override public void onDestroy() {
        destroyed = true;
        if (connection != null) connection.cancel();
        if (adapter != null && permitted()) try { adapter.cancelDiscovery(); } catch (Exception ignored) { }
        if (registered) unregisterReceiver(receiver);
        if (logFile != null) logFile.close();
        super.onDestroy();
    }
}
