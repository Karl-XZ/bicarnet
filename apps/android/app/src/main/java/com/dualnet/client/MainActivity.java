package com.dualnet.client;

import android.app.Activity;
import android.content.Intent;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.VpnService;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.InputType;
import android.view.Gravity;
import android.view.Window;
import android.widget.Button;
import android.widget.CheckBox;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.TextView;

import com.wireguard.android.backend.Backend;
import com.wireguard.android.backend.BackendException;
import com.wireguard.android.backend.GoBackend;
import com.wireguard.android.backend.Statistics;
import com.wireguard.android.backend.Tunnel;
import com.wireguard.config.Config;

import org.json.JSONArray;
import org.json.JSONObject;

import java.io.BufferedReader;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.PrintWriter;
import java.io.StringWriter;
import java.net.HttpURLConnection;
import java.net.URL;
import java.nio.charset.StandardCharsets;
import java.time.LocalTime;
import java.util.Arrays;
import java.util.Locale;
import java.util.stream.Collectors;

public class MainActivity extends Activity {
    private static final int VPN_REQUEST = 7101;
    private static final int BG = Color.rgb(248, 250, 252);
    private static final int CARD = Color.WHITE;
    private static final int TEXT = Color.rgb(15, 23, 42);
    private static final int MUTED = Color.rgb(71, 85, 105);
    private static final int LINE = Color.rgb(203, 213, 225);
    private static final int BLUE = Color.rgb(37, 99, 235);
    private static final int GREEN = Color.rgb(22, 163, 74);
    private static final int RED = Color.rgb(220, 38, 38);
    private static final String PREFS = "bicarnet-settings";
    private static final String PREF_SPLIT_TUNNEL = "splitTunnelEnabled";
    private static final String PREF_PUBLIC_ENDPOINT = "publicEndpointOverride";
    private static final String[] DOMESTIC_APP_PACKAGES = new String[]{
            "com.tencent.mm",
            "com.tencent.mobileqq",
            "com.tencent.tim",
            "com.tencent.wework",
            "com.eg.android.AlipayGphone",
            "com.taobao.taobao",
            "com.jingdong.app.mall",
            "com.sina.weibo",
            "com.ss.android.ugc.aweme",
            "com.ss.android.article.news",
            "com.kuaishou.nebula",
            "com.smile.gifmaker"
    };

    private SimpleTunnel tunnel = new SimpleTunnel("dualnet-client-android");
    private Backend backend;
    private Config config;
    private SharedPreferences prefs;

    private TextView statusBadge;
    private TextView statusTitle;
    private TextView statusHint;
    private TextView nodeText;
    private LinearLayout devicesList;
    private TextView diagnostics;
    private Button connectButton;
    private Button disconnectButton;
    private EditText serverEndpointInput;
    private CheckBox splitTunnelCheck;

    private String statusApiUrl = "http://10.77.0.1:8787/status";
    private String defaultPublicEndpoint = "";
    private String publicEndpoint = "";
    private String lanEndpoint = "";
    private String activeEndpoint = "";
    private String baseConfigText = "";
    private String profileName = "dualnet-client-android";
    private String profileAccount = "android-user";
    private String localDeviceName = "Android";
    private String localTunnelAddress = "";
    private String routeModeText = "仅设备互联";
    private boolean splitTunnelEnabled = true;
    private boolean internetExitEnabled;
    private boolean tunnelUp;

    @Override
    protected void onCreate(Bundle bundle) {
        super.onCreate(bundle);
        Window window = getWindow();
        window.setStatusBarColor(BG);
        window.setNavigationBarColor(BG);
        backend = new GoBackend(this);
        prefs = getSharedPreferences(PREFS, MODE_PRIVATE);
        splitTunnelEnabled = prefs.getBoolean(PREF_SPLIT_TUNNEL, true);
        localDeviceName = getDeviceDisplayName();
        buildUi();
        loadProfile();
        loadConfig();
        setStateCard("未连接", "点击“连接”开始使用", "同一 WiFi 下建议选择“局域网节点”。", MUTED);
    }

    private void buildUi() {
        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(false);
        scroll.setBackgroundColor(BG);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(dp(20), dp(20), dp(20), dp(28));
        scroll.addView(root, new ScrollView.LayoutParams(-1, -2));

        LinearLayout header = new LinearLayout(this);
        header.setOrientation(LinearLayout.HORIZONTAL);
        header.setGravity(Gravity.CENTER_VERTICAL);
        header.setPadding(0, dp(4), 0, dp(18));
        root.addView(header);

        TextView logo = text("b", 28, Color.WHITE, true);
        logo.setGravity(Gravity.CENTER);
        logo.setBackground(rounded(BLUE, BLUE, 18));
        header.addView(logo, new LinearLayout.LayoutParams(dp(56), dp(56)));

        LinearLayout titleBox = new LinearLayout(this);
        titleBox.setOrientation(LinearLayout.VERTICAL);
        titleBox.setPadding(dp(14), 0, 0, 0);
        header.addView(titleBox, new LinearLayout.LayoutParams(0, -2, 1));
        titleBox.addView(text("bicarnet", 28, TEXT, true));
        titleBox.addView(text("本机：" + localDeviceName, 13, MUTED, false));
        titleBox.addView(text("一键连接隧道，查看真实设备状态", 14, MUTED, false));

        LinearLayout stateCard = card();
        statusBadge = pill("未连接", MUTED);
        stateCard.addView(statusBadge);
        statusTitle = text("", 22, TEXT, true);
        statusTitle.setPadding(0, dp(18), 0, 0);
        stateCard.addView(statusTitle);
        statusHint = text("", 14, MUTED, false);
        statusHint.setPadding(0, dp(8), 0, 0);
        stateCard.addView(statusHint);
        root.addView(stateCard);

        LinearLayout buttons = new LinearLayout(this);
        buttons.setOrientation(LinearLayout.HORIZONTAL);
        buttons.setPadding(0, dp(2), 0, dp(10));
        root.addView(buttons);

        connectButton = primary("连接");
        connectButton.setOnClickListener(v -> connectTunnel());
        buttons.addView(connectButton, new LinearLayout.LayoutParams(0, dp(56), 1));

        disconnectButton = secondary("断开");
        disconnectButton.setOnClickListener(v -> disconnectTunnel());
        LinearLayout.LayoutParams disconnectLp = new LinearLayout.LayoutParams(0, dp(56), 1);
        disconnectLp.setMargins(dp(12), 0, 0, 0);
        buttons.addView(disconnectButton, disconnectLp);

        LinearLayout nodeCard = card();
        nodeCard.addView(text("连接节点", 18, TEXT, true));
        nodeText = text("节点：读取中", 14, MUTED, false);
        nodeText.setPadding(0, dp(8), 0, dp(14));
        nodeCard.addView(nodeText);

        nodeCard.addView(text("服务端公网 IP / 域名", 14, TEXT, true));
        serverEndpointInput = new EditText(this);
        serverEndpointInput.setSingleLine(true);
        serverEndpointInput.setTextSize(14);
        serverEndpointInput.setTextColor(TEXT);
        serverEndpointInput.setHint("例如 1.2.3.4 或 vpn.example.com");
        serverEndpointInput.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_VARIATION_URI);
        serverEndpointInput.setPadding(dp(12), 0, dp(12), 0);
        serverEndpointInput.setBackground(rounded(Color.rgb(248, 250, 252), LINE, 10));
        LinearLayout.LayoutParams endpointInputLp = new LinearLayout.LayoutParams(-1, dp(48));
        endpointInputLp.setMargins(0, dp(8), 0, dp(10));
        nodeCard.addView(serverEndpointInput, endpointInputLp);

        LinearLayout endpointButtons = new LinearLayout(this);
        endpointButtons.setOrientation(LinearLayout.HORIZONTAL);
        Button applyEndpoint = secondary("应用服务端");
        applyEndpoint.setOnClickListener(v -> applyServerEndpointOverride());
        endpointButtons.addView(applyEndpoint, new LinearLayout.LayoutParams(0, dp(48), 1));
        Button resetEndpoint = secondary("恢复默认");
        resetEndpoint.setOnClickListener(v -> resetServerEndpoint());
        LinearLayout.LayoutParams resetEndpointLp = new LinearLayout.LayoutParams(0, dp(48), 1);
        resetEndpointLp.setMargins(dp(12), 0, 0, 0);
        endpointButtons.addView(resetEndpoint, resetEndpointLp);
        nodeCard.addView(endpointButtons);

        splitTunnelCheck = new CheckBox(this);
        splitTunnelCheck.setText("智能分流：微信、QQ 等国内应用不走代理");
        splitTunnelCheck.setTextSize(14);
        splitTunnelCheck.setTextColor(TEXT);
        splitTunnelCheck.setChecked(splitTunnelEnabled);
        splitTunnelCheck.setPadding(0, dp(12), 0, dp(4));
        splitTunnelCheck.setOnCheckedChangeListener((buttonView, isChecked) -> applySplitTunnelSetting(isChecked));
        nodeCard.addView(splitTunnelCheck);

        LinearLayout nodeButtons = new LinearLayout(this);
        nodeButtons.setOrientation(LinearLayout.HORIZONTAL);
        nodeButtons.setPadding(0, dp(6), 0, 0);
        Button lan = secondary("局域网节点");
        lan.setOnClickListener(v -> selectEndpoint(lanEndpoint));
        nodeButtons.addView(lan, new LinearLayout.LayoutParams(0, dp(52), 1));
        Button pub = secondary("公网节点");
        pub.setOnClickListener(v -> selectEndpoint(publicEndpoint));
        LinearLayout.LayoutParams pubLp = new LinearLayout.LayoutParams(0, dp(52), 1);
        pubLp.setMargins(dp(12), 0, 0, 0);
        nodeButtons.addView(pub, pubLp);
        nodeCard.addView(nodeButtons);

        TextView tip = text("同 WiFi 选局域网；4G/外网选公网。公网模式需要路由器转发 UDP 51820。", 13, MUTED, false);
        tip.setPadding(0, dp(12), 0, 0);
        nodeCard.addView(tip);
        root.addView(nodeCard);

        LinearLayout deviceCard = card();
        LinearLayout deviceHeader = new LinearLayout(this);
        deviceHeader.setOrientation(LinearLayout.HORIZONTAL);
        deviceHeader.setGravity(Gravity.CENTER_VERTICAL);
        deviceCard.addView(deviceHeader);
        deviceHeader.addView(text("设备列表（在线/离线）", 18, TEXT, true), new LinearLayout.LayoutParams(0, -2, 1));
        Button refresh = secondary("刷新");
        refresh.setOnClickListener(v -> refreshDevices());
        deviceHeader.addView(refresh, new LinearLayout.LayoutParams(dp(92), dp(50)));

        devicesList = new LinearLayout(this);
        devicesList.setOrientation(LinearLayout.VERTICAL);
        devicesList.setPadding(0, dp(14), 0, 0);
        deviceCard.addView(devicesList);
        root.addView(deviceCard);
        setDevicesMessage("尚未刷新", "连接成功后会自动刷新，也可以手动点击“刷新”。", MUTED);

        LinearLayout diagCard = card();
        diagCard.addView(text("诊断详情", 16, TEXT, true));
        diagnostics = text("", 12, Color.rgb(51, 65, 85), false);
        diagnostics.setPadding(0, dp(10), 0, 0);
        diagCard.addView(diagnostics);
        root.addView(diagCard);

        setContentView(scroll);
    }

    private LinearLayout card() {
        LinearLayout card = new LinearLayout(this);
        card.setOrientation(LinearLayout.VERTICAL);
        card.setPadding(dp(18), dp(18), dp(18), dp(18));
        card.setBackground(rounded(CARD, LINE, 14));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(-1, -2);
        lp.setMargins(0, 0, 0, dp(16));
        card.setLayoutParams(lp);
        return card;
    }

    private TextView text(String value, int sp, int color, boolean bold) {
        TextView tv = new TextView(this);
        tv.setText(value);
        tv.setTextSize(sp);
        tv.setTextColor(color);
        tv.setIncludeFontPadding(true);
        if (bold) tv.setTypeface(Typeface.DEFAULT, Typeface.BOLD);
        return tv;
    }

    private TextView pill(String value, int color) {
        TextView tv = text(value, 13, Color.WHITE, true);
        tv.setGravity(Gravity.CENTER);
        tv.setPadding(dp(14), dp(7), dp(14), dp(7));
        tv.setBackground(rounded(color, color, 99));
        return tv;
    }

    private Button primary(String value) {
        Button b = new Button(this);
        b.setText(value);
        b.setAllCaps(false);
        b.setTextSize(16);
        b.setTextColor(Color.WHITE);
        b.setBackground(rounded(BLUE, BLUE, 14));
        return b;
    }

    private Button secondary(String value) {
        Button b = new Button(this);
        b.setText(value);
        b.setAllCaps(false);
        b.setTextSize(15);
        b.setTextColor(TEXT);
        b.setBackground(rounded(Color.rgb(241, 245, 249), LINE, 14));
        return b;
    }

    private GradientDrawable rounded(int fill, int stroke, int radiusDp) {
        GradientDrawable drawable = new GradientDrawable();
        drawable.setShape(GradientDrawable.RECTANGLE);
        drawable.setColor(fill);
        drawable.setCornerRadius(dp(radiusDp));
        drawable.setStroke(dp(1), stroke);
        return drawable;
    }

    private int dp(float value) {
        return Math.round(value * getResources().getDisplayMetrics().density);
    }

    private String getDeviceDisplayName() {
        String manufacturer = Build.MANUFACTURER == null ? "" : Build.MANUFACTURER.trim();
        String model = Build.MODEL == null ? "" : Build.MODEL.trim();
        if (manufacturer.length() == 0 && model.length() == 0) return "Android";
        if (manufacturer.length() == 0) return model;
        if (model.length() == 0) return manufacturer;
        if (model.toLowerCase(Locale.ROOT).startsWith(manufacturer.toLowerCase(Locale.ROOT))) {
            return model;
        }
        return manufacturer + " " + model;
    }

    private String extractInterfaceAddress(String configText) {
        for (String line : configText.split("\\r?\\n")) {
            String trimmed = line.trim();
            if (trimmed.toLowerCase(Locale.ROOT).startsWith("address")) {
                String[] parts = trimmed.split("=", 2);
                if (parts.length == 2) {
                    String address = parts[1].trim();
                    int comma = address.indexOf(',');
                    if (comma >= 0) address = address.substring(0, comma).trim();
                    return address;
                }
            }
        }
        return "";
    }

    private String extractPeerAllowedIps(String configText) {
        for (String line : configText.split("\\r?\\n")) {
            String trimmed = line.trim();
            if (trimmed.toLowerCase(Locale.ROOT).startsWith("allowedips")) {
                String[] parts = trimmed.split("=", 2);
                return parts.length == 2 ? parts[1].trim() : "";
            }
        }
        return "";
    }

    private void updateRouteMode(String configText) {
        String allowedIps = extractPeerAllowedIps(configText);
        internetExitEnabled = allowedIps.contains("0.0.0.0/0") || allowedIps.contains("::/0");
        if (internetExitEnabled) {
            routeModeText = splitTunnelEnabled ? "互联网出口 + 智能分流" : "互联网出口（全局代理）";
        } else {
            routeModeText = "仅设备互联（不会代理 Google）";
        }
    }

    private void appendRouteMode() {
        if (nodeText != null) nodeText.append("\n流量模式：" + routeModeText);
    }

    private String extractEndpointPort(String endpointValue) {
        int colon = endpointValue == null ? -1 : endpointValue.lastIndexOf(':');
        if (colon >= 0 && colon + 1 < endpointValue.length()) return endpointValue.substring(colon + 1);
        return "51820";
    }

    private String normalizeEndpoint(String value, String fallbackEndpoint) {
        String endpointValue = value == null ? "" : value.trim();
        if (endpointValue.startsWith("http://")) endpointValue = endpointValue.substring(7);
        if (endpointValue.startsWith("https://")) endpointValue = endpointValue.substring(8);
        int slash = endpointValue.indexOf('/');
        if (slash >= 0) endpointValue = endpointValue.substring(0, slash);
        endpointValue = endpointValue.trim();
        if (endpointValue.length() == 0) return fallbackEndpoint;
        if (endpointValue.lastIndexOf(':') < 0) {
            endpointValue = endpointValue + ":" + extractEndpointPort(fallbackEndpoint);
        }
        return endpointValue;
    }

    private String applySplitTunnel(String configText) {
        String rewritten = configText.replaceAll("(?m)^ExcludedApplications\\s*=\\s*.*\\r?\\n?", "");
        if (!splitTunnelEnabled) return rewritten;
        String excluded = "ExcludedApplications = " + String.join(", ", DOMESTIC_APP_PACKAGES);
        if (rewritten.contains("\n[Peer]")) {
            return rewritten.replaceFirst("\\n\\[Peer\\]", "\n" + excluded + "\n\n[Peer]");
        }
        return rewritten + "\n" + excluded + "\n";
    }

    private void rebuildConfigAfterSettingChange(String title, String detail) {
        try {
            if (baseConfigText.length() > 0) {
                updateRouteMode(baseConfigText);
                config = parseConfigForEndpoint(activeEndpoint);
            }
            updateNodeText();
            appendRouteMode();
            if (tunnelUp) {
                setTunnelState(Tunnel.State.DOWN);
                setStateCard("设置已保存", title, "VPN 已断开，请重新点击“连接”让新设置生效。", BLUE);
            } else {
                setStateCard("设置已保存", title, detail, BLUE);
            }
        } catch (Exception ex) {
            setStateCard("配置错误", "设置未能生效", safeMessage(ex), RED);
            log("设置应用失败: " + safeMessage(ex));
        }
    }

    private void applySplitTunnelSetting(boolean enabled) {
        splitTunnelEnabled = enabled;
        prefs.edit().putBoolean(PREF_SPLIT_TUNNEL, enabled).apply();
        rebuildConfigAfterSettingChange(
                enabled ? "智能分流已开启" : "智能分流已关闭",
                enabled ? "微信、QQ 等国内应用不会走代理。" : "所有流量会按当前 VPN 路由走隧道。");
        log("智能分流: " + (enabled ? "开启" : "关闭"));
    }

    private void applyServerEndpointOverride() {
        String normalized = normalizeEndpoint(serverEndpointInput == null ? "" : serverEndpointInput.getText().toString(), defaultPublicEndpoint);
        publicEndpoint = normalized;
        activeEndpoint = publicEndpoint;
        if (serverEndpointInput != null) serverEndpointInput.setText(publicEndpoint);
        prefs.edit().putString(PREF_PUBLIC_ENDPOINT, publicEndpoint).apply();
        rebuildConfigAfterSettingChange("服务端地址已更新", "当前会连接：" + publicEndpoint);
        log("服务端地址已更新: " + publicEndpoint);
    }

    private void resetServerEndpoint() {
        publicEndpoint = defaultPublicEndpoint;
        activeEndpoint = publicEndpoint;
        if (serverEndpointInput != null) serverEndpointInput.setText(publicEndpoint);
        prefs.edit().remove(PREF_PUBLIC_ENDPOINT).apply();
        rebuildConfigAfterSettingChange("已恢复默认服务端", "当前会连接：" + publicEndpoint);
        log("服务端地址已恢复默认: " + publicEndpoint);
    }

    private void loadProfile() {
        try {
            JSONObject obj = new JSONObject(readAsset("dualnet-profile.json"));
            profileName = obj.optString("profileName", profileName);
            profileAccount = obj.optString("account", profileAccount);
            tunnel = new SimpleTunnel(obj.optString("tunnelName", profileName));
            defaultPublicEndpoint = obj.optString("endpoint", "");
            publicEndpoint = normalizeEndpoint(prefs.getString(PREF_PUBLIC_ENDPOINT, defaultPublicEndpoint), defaultPublicEndpoint);
            lanEndpoint = obj.optString("lanEndpoint", publicEndpoint);
            activeEndpoint = publicEndpoint.length() > 0 ? publicEndpoint : lanEndpoint;
            statusApiUrl = obj.optString("statusApiUrl", statusApiUrl);
            if (serverEndpointInput != null) serverEndpointInput.setText(publicEndpoint);
            updateNodeText();
            appendRouteMode();
            log("本机设备：" + localDeviceName + "；隧道身份：" + profileName + " / " + profileAccount);
            log("配置已加载。账号：" + obj.optString("account", "android-user"));
        } catch (Exception ex) {
            setStateCard("配置错误", "无法读取连接配置", safeMessage(ex), RED);
            log("profile 加载失败: " + safeMessage(ex));
        }
    }

    private void loadConfig() {
        try {
            baseConfigText = readAsset("dualnet-android.conf");
            localTunnelAddress = extractInterfaceAddress(baseConfigText);
            updateRouteMode(baseConfigText);
            config = parseConfigForEndpoint(activeEndpoint);
            updateNodeText();
            appendRouteMode();
            log("WireGuard 配置已加载");
        } catch (Exception ex) {
            setStateCard("配置错误", "WireGuard 配置不可用", safeMessage(ex), RED);
            log("WireGuard 配置加载失败: " + safeMessage(ex));
        }
    }

    private void selectEndpoint(String value) {
        if (value == null || value.length() == 0) {
            log("节点为空，无法切换");
            return;
        }
        activeEndpoint = value;
        updateNodeText();
        appendRouteMode();
        try {
            config = parseConfigForEndpoint(activeEndpoint);
            setStateCard("未连接", "已切换节点", "现在可以点击“连接”。", MUTED);
            setDevicesMessage("尚未刷新", "连接成功后会自动刷新，也可以手动点击“刷新”。", MUTED);
            log("已切换节点: " + activeEndpoint);
        } catch (Exception ex) {
            setStateCard("配置错误", "切换节点失败", safeMessage(ex), RED);
        }
    }

    private void updateNodeText() {
        if (nodeText == null) return;
        String mode = activeEndpoint.equals(lanEndpoint) ? "局域网节点" : "公网节点";
        nodeText.setText("当前节点：" + mode + " / " + activeEndpoint + "\n设备接口：" + statusApiUrl);
    }

    private Config parseConfigForEndpoint(String endpointValue) throws Exception {
        String rewritten = baseConfigText.replaceAll("(?m)^Endpoint\\s*=\\s*.*$", "Endpoint = " + endpointValue);
        rewritten = applySplitTunnel(rewritten);
        return Config.parse(new java.io.ByteArrayInputStream(rewritten.getBytes(StandardCharsets.UTF_8)));
    }

    private String readAsset(String name) throws Exception {
        try (InputStream in = getAssets().open(name);
             BufferedReader reader = new BufferedReader(new InputStreamReader(in, StandardCharsets.UTF_8))) {
            return reader.lines().collect(Collectors.joining("\n"));
        }
    }

    private void connectTunnel() {
        try {
            if (config == null) {
                setStateCard("配置错误", "配置未加载", "请重新安装最新版 App。", RED);
                return;
            }
            Intent intent = VpnService.prepare(this);
            if (intent != null) {
                setStateCard("等待授权", "请允许系统 VPN 权限", "授权后会自动继续连接。", BLUE);
                startActivityForResult(intent, VPN_REQUEST);
                return;
            }
            connectButton.setEnabled(false);
            setStateCard("正在连接", "正在启动 VPN 服务", "通常需要几秒钟，请稍候。", BLUE);
            startService(new Intent(this, GoBackend.VpnService.class));
            new Handler(Looper.getMainLooper()).postDelayed(() -> {
                try {
                    setTunnelState(Tunnel.State.UP);
                } catch (Exception ex) {
                    connectButton.setEnabled(true);
                    setStateCard("连接失败", "无法启动 VPN", safeMessage(ex), RED);
                    log("连接失败: " + safeMessage(ex));
                    logStack(ex);
                }
            }, 1500);
        } catch (Exception ex) {
            connectButton.setEnabled(true);
            setStateCard("连接失败", "无法启动 VPN", safeMessage(ex), RED);
            log("连接失败: " + safeMessage(ex));
            logStack(ex);
        }
    }

    private void disconnectTunnel() {
        try {
            setTunnelState(Tunnel.State.DOWN);
        } catch (Exception ex) {
            log("断开失败: " + safeMessage(ex));
            logStack(ex);
        }
    }

    private void updateTunnelUi(Tunnel.State state) {
        if (state == Tunnel.State.UP) {
            tunnelUp = true;
            connectButton.setEnabled(true);
            setStateCard("已启动", "正在确认服务端连通性", "如果长时间失败，请切换局域网/公网节点。", BLUE);
            refreshDevices();
        } else {
            tunnelUp = false;
            connectButton.setEnabled(true);
            setStateCard("未连接", "隧道已断开", "需要使用时再次点击“连接”。", MUTED);
            setDevicesMessage("尚未刷新", "连接成功后会自动刷新，也可以手动点击“刷新”。", MUTED);
        }
    }

    private void setTunnelState(Tunnel.State state) throws Exception {
        if (Looper.myLooper() == Looper.getMainLooper()) {
            new Thread(() -> {
                try {
                    backend.setState(tunnel, state, state == Tunnel.State.UP ? config : null);
                    runOnUiThread(() -> updateTunnelUi(state));
                    log("状态切换为 " + state);
                } catch (Exception ex) {
                    runOnUiThread(() -> {
                        connectButton.setEnabled(true);
                        setStateCard("连接失败", state == Tunnel.State.UP ? "无法启动 VPN" : "无法断开 VPN", safeMessage(ex), RED);
                        log("VPN 状态切换失败: " + safeMessage(ex));
                        logStack(ex);
                    });
                }
            }).start();
            return;
        }
        backend.setState(tunnel, state, state == Tunnel.State.UP ? config : null);
        if (state == Tunnel.State.UP) {
            tunnelUp = true;
            connectButton.setEnabled(true);
            setStateCard("已启动", "正在确认服务端连通性", "如果长时间失败，请切换局域网/公网节点。", BLUE);
            refreshDevices();
        } else {
            tunnelUp = false;
            connectButton.setEnabled(true);
            setStateCard("未连接", "隧道已断开", "需要使用时再次点击“连接”。", MUTED);
            setDevicesMessage("尚未刷新", "连接成功后会自动刷新，也可以手动点击“刷新”。", MUTED);
        }
        log("状态切换为 " + state);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == VPN_REQUEST) {
            if (resultCode == RESULT_OK) connectTunnel();
            else setStateCard("未授权", "系统 VPN 权限未允许", "请再次点击连接并允许授权。", RED);
        }
    }

    private void refreshDevices() {
        if (!tunnelUp) {
            setDevicesMessage("尚未连接", "请先点击“连接”，连通后会自动刷新设备列表。", MUTED);
            log("尚未连接，跳过设备刷新");
            return;
        }
        setDevicesMessage("正在刷新", "正在读取服务端设备状态。", BLUE);
        log("正在刷新设备列表");
        new Thread(() -> {
            try {
                String body = fetchStatusBody();
                runOnUiThread(() -> {
                    setStateCard("已连接", "服务端连通正常", "设备列表已更新。", GREEN);
                    renderDevices(body);
                    setStateCard(
                            "已连接",
                            internetExitEnabled ? "互联网出口已启用" : "仅设备互联",
                            internetExitEnabled ? "Google 等公网访问会通过服务端出口。" : "当前只访问隧道内网，Google 不会走隧道。",
                            GREEN);
                    log("设备列表已刷新");
                });
            } catch (Exception ex) {
                runOnUiThread(() -> {
                    boolean handshaked = hasLocalHandshakeSafe();
                    if (handshaked) {
                        setStateCard("已连接", "VPN 已完成握手", "设备列表接口暂不可达；隧道本身已连接。", GREEN);
                        setDevicesMessage("设备列表暂不可用", "手机已完成 WireGuard 握手，但状态接口不可达：" + safeMessage(ex), BLUE);
                    } else {
                        setStateCard("未连通", "VPN 已启动，但服务端未握手", "同 WiFi 请点“局域网节点”；外网请确认 UDP 51820 能进入服务端。", RED);
                        setDevicesMessage("无法读取设备列表", "隧道未与服务端完成握手：" + safeMessage(ex), RED);
                    }
                    log("刷新设备失败: " + safeMessage(ex));
                });
            }
        }).start();
    }

    private String fetchStatusBody() throws Exception {
        Exception last = null;
        for (String urlValue : statusUrlCandidates()) {
            try {
                log("读取设备接口：" + urlValue);
                HttpURLConnection conn = (HttpURLConnection) new URL(urlValue).openConnection();
                conn.setConnectTimeout(3500);
                conn.setReadTimeout(3500);
                conn.setRequestMethod("GET");
                int code = conn.getResponseCode();
                InputStream stream = code >= 200 && code < 300 ? conn.getInputStream() : conn.getErrorStream();
                String body;
                try (BufferedReader reader = new BufferedReader(new InputStreamReader(stream, StandardCharsets.UTF_8))) {
                    body = reader.lines().collect(Collectors.joining("\n"));
                }
                if (code < 200 || code >= 300) throw new IllegalStateException("HTTP " + code + ": " + body);
                return body;
            } catch (Exception ex) {
                last = ex;
                log("设备接口不可达：" + safeMessage(ex));
            }
        }
        throw last == null ? new IllegalStateException("没有可用的设备接口") : last;
    }

    private String[] statusUrlCandidates() {
        String host = "";
        int colon = activeEndpoint.lastIndexOf(':');
        if (colon > 0) host = activeEndpoint.substring(0, colon);
        String direct = host.length() == 0 ? "" : "http://" + host + ":8787/status";
        if (direct.length() == 0 || direct.equals(statusApiUrl)) return new String[]{statusApiUrl};
        return new String[]{direct, statusApiUrl};
    }

    private boolean hasLocalHandshakeSafe() {
        try {
            Statistics stats = backend.getStatistics(tunnel);
            if (stats.totalRx() > 0 || stats.totalTx() > 0) return true;
            for (com.wireguard.crypto.Key key : stats.peers()) {
                Statistics.PeerStats peerStats = stats.peer(key);
                if (peerStats != null && peerStats.latestHandshakeEpochMillis() > 0) return true;
            }
        } catch (Exception ignored) {
            return false;
        }
        return false;
    }

    private void renderDevices(String json) {
        try {
            JSONObject root = new JSONObject(json);
            JSONArray arr = optArray(root, "devices", "Devices");
            devicesList.removeAllViews();
            if (arr == null || arr.length() == 0) {
                setDevicesMessage("暂无设备", "服务端还没有加载 peer。", MUTED);
                return;
            }
            for (int i = 0; i < arr.length(); i++) {
                JSONObject d = arr.getJSONObject(i);
                devicesList.addView(deviceRow(d));
            }
        } catch (Exception ex) {
            setDevicesMessage("设备数据解析失败", safeMessage(ex), RED);
        }
    }

    private LinearLayout deviceRow(JSONObject d) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.VERTICAL);
        row.setPadding(dp(14), dp(14), dp(14), dp(14));
        row.setBackground(rounded(Color.rgb(248, 250, 252), LINE, 12));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(-1, -2);
        lp.setMargins(0, 0, 0, dp(10));
        row.setLayoutParams(lp);

        boolean online = optBoolean(d, "online", "Online");
        LinearLayout top = new LinearLayout(this);
        top.setGravity(Gravity.CENTER_VERTICAL);
        row.addView(top);
        top.addView(pill(online ? "在线" : "离线", online ? GREEN : MUTED));
        String rawName = optString(d, "name", "Name", "-");
        boolean local = isLocalDevice(d, rawName);
        TextView name = text(local ? rawName + "\n本机：" + localDeviceName : rawName, 16, TEXT, true);
        name.setPadding(dp(12), 0, 0, 0);
        top.addView(name, new LinearLayout.LayoutParams(0, -2, 1));

        String endpoint = optString(d, "endpoint", "Endpoint", "");
        TextView detail = text(
                "地址：" + optString(d, "allowedIps", "AllowedIps", "-")
                        + "\n来源：" + (endpoint.length() == 0 ? "尚未握手" : endpoint)
                        + "\n最近握手：" + optString(d, "latestHandshake", "LatestHandshake", "-"),
                13, MUTED, false);
        detail.setPadding(0, dp(10), 0, 0);
        row.addView(detail);
        return row;
    }

    private JSONArray optArray(JSONObject obj, String lower, String upper) {
        JSONArray value = obj.optJSONArray(lower);
        return value != null ? value : obj.optJSONArray(upper);
    }

    private String optString(JSONObject obj, String lower, String upper, String fallback) {
        if (obj.has(lower)) return obj.optString(lower, fallback);
        return obj.optString(upper, fallback);
    }

    private boolean isLocalDevice(JSONObject obj, String reportedName) {
        if (reportedName != null && reportedName.contains(profileName)) return true;
        String allowedIps = optString(obj, "allowedIps", "AllowedIps", "");
        if (localTunnelAddress.length() == 0 || allowedIps.length() == 0) return false;
        String localIp = localTunnelAddress.split("/")[0];
        return allowedIps.contains(localIp);
    }

    private boolean optBoolean(JSONObject obj, String lower, String upper) {
        if (obj.has(lower)) return obj.optBoolean(lower);
        return obj.optBoolean(upper);
    }

    private void setDevicesMessage(String title, String detail, int color) {
        devicesList.removeAllViews();
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(dp(14), dp(14), dp(14), dp(14));
        box.setBackground(rounded(Color.rgb(248, 250, 252), LINE, 12));
        box.addView(text(title, 15, color == MUTED ? TEXT : color, true));
        TextView d = text(detail, 13, MUTED, false);
        d.setPadding(0, dp(8), 0, 0);
        box.addView(d);
        devicesList.addView(box);
    }

    private void setStateCard(String badge, String title, String hint, int color) {
        statusBadge.setText(badge);
        statusBadge.setBackground(rounded(color, color, 99));
        statusTitle.setText(title);
        statusHint.setText(hint);
    }

    private void log(String message) {
        String line = "[" + LocalTime.now().withNano(0) + "] " + message + "\n";
        if (Looper.myLooper() == Looper.getMainLooper()) {
            diagnostics.append(line);
        } else {
            runOnUiThread(() -> diagnostics.append(line));
        }
    }

    private String safeMessage(Exception ex) {
        if (ex instanceof BackendException backendException) {
            String reason = backendException.getReason().name();
            Object[] format = backendException.getFormat();
            return format.length == 0 ? reason : reason + " " + Arrays.toString(format);
        }
        return ex.getMessage() == null ? ex.getClass().getSimpleName() : ex.getMessage();
    }

    private void logStack(Exception ex) {
        StringWriter writer = new StringWriter();
        ex.printStackTrace(new PrintWriter(writer));
        log(writer.toString());
    }

    private static final class SimpleTunnel implements Tunnel {
        private final String name;

        private SimpleTunnel(String name) {
            this.name = name;
        }

        @Override
        public String getName() {
            return name;
        }

        @Override
        public void onStateChange(State newState) {
            // UI is updated by explicit operations.
        }
    }
}
