using System.Diagnostics;
using System.Security.Cryptography;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace DualNetClient;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

internal sealed class ClientProfile
{
    public string ProfileName { get; set; } = "dualnet-client-windows";
    public string TunnelName { get; set; } = "dualnet-client-windows";
    public string Account { get; set; } = "windows-user";
    public string Endpoint { get; set; } = "";
    public string LanEndpoint { get; set; } = "";
    public string ConfigPath { get; set; } = "dualnet-client-windows.conf";
    public string WireGuardExe { get; set; } = @"C:\Program Files\WireGuard\wireguard.exe";
    public string StatusApiUrl { get; set; } = "http://10.77.0.1:8787/status";
    public string GeneratedAt { get; set; } = "";
}

internal sealed class PeerStatus
{
    public string Name { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string AllowedIps { get; set; } = "";
    public long LatestHandshakeUnix { get; set; }
    public string LatestHandshake { get; set; } = "never";
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
    public bool Online { get; set; }
}

internal sealed class DeviceStatusResponse
{
    public string Server { get; set; } = "dualnet-server";
    public string GeneratedAt { get; set; } = DateTimeOffset.Now.ToString("O");
    public List<PeerStatus> Devices { get; set; } = new();
}

internal sealed class ActivationRequest
{
    public string Code { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Role { get; set; } = "client";
    public string ProfileName { get; set; } = "";
    public string Account { get; set; } = "";
    public string TunnelAddress { get; set; } = "";
}

internal sealed class ActivationResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public string Token { get; set; } = "";
    public string ActivatedAt { get; set; } = "";
}

internal sealed class LocalActivation
{
    public string DeviceId { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Platform { get; set; } = "windows";
    public string Role { get; set; } = "client";
    public string Token { get; set; } = "";
    public string ActivatedAt { get; set; } = "";
}

internal sealed class ActivationStore
{
    public int Version { get; set; } = 1;
    public List<ActivationCodeRecord> Codes { get; set; } = new();
}

internal sealed class ActivationCodeRecord
{
    public string CodeHash { get; set; } = "";
    public string CodeSuffix { get; set; } = "";
    public string Role { get; set; } = "client";
    public bool Redeemed { get; set; }
    public string DeviceIdHash { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Platform { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public string Account { get; set; } = "";
    public string TunnelAddress { get; set; } = "";
    public string ActivatedAt { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public bool Blocked { get; set; }
    public string BlockedAt { get; set; } = "";
    public string BlockReason { get; set; } = "";
}

internal sealed class AdminDeviceRecord
{
    public string DeviceIdHash { get; set; } = "";
    public string DeviceName { get; set; } = "";
    public string Platform { get; set; } = "";
    public string Role { get; set; } = "client";
    public string ProfileName { get; set; } = "";
    public string Account { get; set; } = "";
    public string TunnelAddress { get; set; } = "";
    public string ActivatedAt { get; set; } = "";
    public bool Blocked { get; set; }
    public string BlockedAt { get; set; } = "";
    public string BlockReason { get; set; } = "";
    public bool Online { get; set; }
    public string Endpoint { get; set; } = "";
    public string LatestHandshake { get; set; } = "";
    public long RxBytes { get; set; }
    public long TxBytes { get; set; }
}

internal sealed class AdminDevicesResponse
{
    public string GeneratedAt { get; set; } = DateTimeOffset.Now.ToString("O");
    public List<AdminDeviceRecord> Devices { get; set; } = new();
}

internal sealed class AdminCodeRecord
{
    public string Code { get; set; } = "";
    public string CodeSuffix { get; set; } = "";
    public string Role { get; set; } = "client";
    public bool Redeemed { get; set; }
    public bool Blocked { get; set; }
    public string DeviceName { get; set; } = "";
    public string Platform { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public string Account { get; set; } = "";
    public string TunnelAddress { get; set; } = "";
    public string ActivatedAt { get; set; } = "";
}

internal sealed class AdminCodesResponse
{
    public string GeneratedAt { get; set; } = DateTimeOffset.Now.ToString("O");
    public List<AdminCodeRecord> Codes { get; set; } = new();
}

internal sealed class AdminCreateCodesRequest
{
    public int Count { get; set; } = 1;
    public string Role { get; set; } = "client";
}

internal sealed class AdminCreateCodesResponse
{
    public bool Ok { get; set; }
    public string Message { get; set; } = "";
    public List<string> Codes { get; set; } = new();
}

internal sealed class AdminActionRequest
{
    public string DeviceIdHash { get; set; } = "";
    public string Reason { get; set; } = "";
}

internal sealed class ServerPeerConfig
{
    public string Name { get; set; } = "";
    public string PublicKey { get; set; } = "";
    public string AllowedIps { get; set; } = "";
}

internal sealed class MainForm : Form
{
    private const int StatusApiPort = 8787;
    private const string DefaultServerTunnel = "dualnet-server";
#if BICARNET_CLIENT_ONLY
    private static readonly bool ClientOnly = true;
#else
    private static readonly bool ClientOnly = false;
#endif
#if BICARNET_ADMIN_ONLY
    private static readonly bool AdminOnly = true;
#else
    private static readonly bool AdminOnly = false;
#endif
    private static readonly Color Blue = Color.FromArgb(37, 99, 235);
    private static readonly Color Green = Color.FromArgb(22, 163, 74);
    private static readonly Color Red = Color.FromArgb(220, 38, 38);
    private static readonly Color Slate = Color.FromArgb(51, 65, 85);
    private static readonly Color Pale = Color.FromArgb(248, 250, 252);

    private readonly Label _clientBadge = new();
    private readonly Label _clientHeadline = new();
    private readonly Label _clientHint = new();
    private readonly Label _serverBadge = new();
    private readonly Label _serverHeadline = new();
    private readonly Label _serverHint = new();
    private readonly TabControl _tabs = new();
    private readonly FlowLayoutPanel _deviceCards = new();
    private readonly Label _deviceSummary = new();
    private readonly TextBox _diagnostics = new();
    private readonly TextBox _profilePath = new();
    private readonly TextBox _clientConfigPath = new();
    private readonly TextBox _serverConfigPath = new();
    private readonly TextBox _statusApi = new();
    private readonly FlowLayoutPanel _adminDeviceCards = new();
    private readonly Label _adminSummary = new();
    private readonly Dictionary<string, AdminDeviceRecord> _adminDevices = new();

    private ClientProfile _profile = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _listenerCts;

    public MainForm()
    {
        Text = "bicarnet";
        Width = 1040;
        Height = 740;
        MinimumSize = new Size(900, 640);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Pale;

        _tabs.Dock = DockStyle.Fill;
        _tabs.Font = new Font("Microsoft YaHei UI", 10);
        Controls.Add(_tabs);

        var home = new TabPage("首页");
        var devices = new TabPage("设备");
        var advanced = new TabPage("高级");
        _tabs.TabPages.Add(home);
        _tabs.TabPages.Add(devices);
        _tabs.TabPages.Add(advanced);
        if (AdminOnly)
            _tabs.TabPages.Add(new TabPage("管理"));

        BuildHome(home);
        BuildDevices(devices);
        BuildAdvanced(advanced);
        if (AdminOnly)
            BuildAdmin(_tabs.TabPages[^1]);

        LoadDefaultProfile();
        LoadDefaultServerConfig();
        RefreshAllStatus();
        StartStatusApiIfServerIsRunning();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopStatusApi();
        base.OnFormClosing(e);
    }

    private void BuildHome(TabPage page)
    {
        page.BackColor = Pale;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(22) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(root);

        root.Controls.Add(new Label
        {
            Text = "bicarnet 双向隧道",
            Font = new Font("Microsoft YaHei UI", 22, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Height = 48,
            Dock = DockStyle.Top
        });

        root.Controls.Add(new Label
        {
            Text = "选择这台电脑的角色，然后点击主按钮。普通用户无需查看日志或配置文件。",
            Font = new Font("Microsoft YaHei UI", 10),
            ForeColor = Slate,
            Height = 30,
            Dock = DockStyle.Top
        });

        var cards = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = ClientOnly ? 1 : 2, RowCount = 1, Height = 230, Padding = new Padding(0, 14, 0, 0) };
        cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, ClientOnly ? 100 : 50));
        if (!ClientOnly)
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.Controls.Add(cards);

        cards.Controls.Add(BuildRoleCard(
            "作为连接端",
            "让这台电脑连接到外网服务器，访问远程资源。",
            _clientBadge,
            _clientHeadline,
            _clientHint,
            "一键连接",
            ConnectClient,
            "断开连接",
            DisconnectClient), 0, 0);

        cards.Controls.Add(BuildRoleCard(
            "作为服务端",
            "让其他手机、电脑接入这台电脑，并查看在线设备。",
            _serverBadge,
            _serverHeadline,
            _serverHint,
            "一键启动服务端",
            StartServer,
            "停止服务端",
            StopServer), 1, 0);

        if (ClientOnly && cards.Controls.Count > 1)
            cards.Controls.RemoveAt(cards.Controls.Count - 1);

        var tips = CardPanel();
        tips.Dock = DockStyle.Top;
        tips.Height = 150;
        tips.Controls.Add(new Label
        {
            Text = "连接提示",
            Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(16, 14),
            AutoSize = true
        });
        tips.Controls.Add(new Label
        {
            Text = "手机和这台电脑在同一 WiFi 时，请在 App 里选择“局域网节点”。手机使用 4G/外网时，请选择“公网节点”，并确认路由器已转发 UDP 51820。",
            Font = new Font("Microsoft YaHei UI", 10),
            ForeColor = Slate,
            Location = new Point(16, 48),
            Size = new Size(900, 58)
        });
        tips.Controls.Add(FlatButton("刷新状态", RefreshAllStatus, 120, new Point(16, 104)));
        tips.Controls.Add(FlatButton("查看在线设备", ShowDevicesAndRefresh, 140, new Point(146, 104)));
        root.Controls.Add(tips);
    }

    private Panel BuildRoleCard(string title, string desc, Label badge, Label headline, Label hint, string primaryText, Action primary, string secondaryText, Action secondary)
    {
        var card = CardPanel();
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 14, 0);
        card.Controls.Add(new Label { Text = title, Font = new Font("Microsoft YaHei UI", 14, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(16, 14), AutoSize = true });
        card.Controls.Add(new Label { Text = desc, Font = new Font("Microsoft YaHei UI", 10), ForeColor = Slate, Location = new Point(16, 48), Size = new Size(400, 42) });
        badge.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold);
        badge.ForeColor = Color.White;
        badge.BackColor = Slate;
        badge.TextAlign = ContentAlignment.MiddleCenter;
        badge.Location = new Point(16, 94);
        badge.Size = new Size(92, 28);
        card.Controls.Add(badge);
        headline.Font = new Font("Microsoft YaHei UI", 11, FontStyle.Bold);
        headline.ForeColor = Color.FromArgb(15, 23, 42);
        headline.Location = new Point(120, 92);
        headline.Size = new Size(330, 28);
        card.Controls.Add(headline);
        hint.Font = new Font("Microsoft YaHei UI", 9);
        hint.ForeColor = Slate;
        hint.Location = new Point(16, 128);
        hint.Size = new Size(430, 32);
        card.Controls.Add(hint);
        card.Controls.Add(PrimaryButton(primaryText, primary, 150, new Point(16, 170)));
        card.Controls.Add(OutlineButton(secondaryText, secondary, 120, new Point(178, 170)));
        return card;
    }

    private void BuildDevices(TabPage page)
    {
        page.BackColor = Pale;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(22) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(root);

        root.Controls.Add(new Label { Text = "设备列表（在线/离线）", Font = new Font("Microsoft YaHei UI", 22, FontStyle.Bold), Height = 48, Dock = DockStyle.Top });
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48 };
        bar.Controls.Add(PrimaryButton("刷新设备", RefreshDevicesFromBestSource, 120, Point.Empty));
        bar.Controls.Add(OutlineButton("从本机读取", RefreshDevicesFromLocalServer, 120, Point.Empty));
        bar.Controls.Add(OutlineButton("从状态接口读取", RefreshDevicesFromStatusApi, 140, Point.Empty));
        _deviceSummary.Text = "尚未刷新";
        if (ClientOnly && bar.Controls.Count > 1)
            bar.Controls.RemoveAt(1);

        _deviceSummary.AutoSize = true;
        _deviceSummary.Padding = new Padding(12, 9, 0, 0);
        bar.Controls.Add(_deviceSummary);
        root.Controls.Add(bar);

        _deviceCards.Dock = DockStyle.Fill;
        _deviceCards.FlowDirection = FlowDirection.TopDown;
        _deviceCards.WrapContents = false;
        _deviceCards.AutoScroll = true;
        root.Controls.Add(_deviceCards);
    }

    private void BuildAdvanced(TabPage page)
    {
        page.BackColor = Pale;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(22) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(root);

        root.Controls.Add(new Label { Text = "高级设置与诊断", Font = new Font("Microsoft YaHei UI", 20, FontStyle.Bold), Height = 44, Dock = DockStyle.Top });

        var grid = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 3, AutoSize = true };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        root.Controls.Add(grid);
        AddRow(grid, "Profile", _profilePath, OutlineButton("选择", BrowseProfile, 80, Point.Empty));
        AddRow(grid, "连接端配置", _clientConfigPath, OutlineButton("选择", BrowseConfig, 80, Point.Empty));
        AddRow(grid, "服务端配置", _serverConfigPath, OutlineButton("选择", BrowseServerConfig, 80, Point.Empty));
        AddRow(grid, "设备接口", _statusApi, null);

        if (ClientOnly)
        {
            for (var i = grid.Controls.Count - 1; i >= 0; i--)
            {
                var position = grid.GetPositionFromControl(grid.Controls[i]);
                if (position.Row == 2)
                    grid.Controls.RemoveAt(i);
            }
        }

        _diagnostics.Multiline = true;
        _diagnostics.ReadOnly = true;
        _diagnostics.ScrollBars = ScrollBars.Vertical;
        _diagnostics.Dock = DockStyle.Fill;
        _diagnostics.Font = new Font("Consolas", 10);
        root.Controls.Add(_diagnostics);
    }

    private void BuildAdmin(TabPage page)
    {
        page.BackColor = Pale;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(22) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        page.Controls.Add(root);

        root.Controls.Add(new Label { Text = "管理端", Font = new Font("Microsoft YaHei UI", 22, FontStyle.Bold), Height = 48, Dock = DockStyle.Top });
        var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48 };
        bar.Controls.Add(PrimaryButton("刷新绑定设备", RefreshAdminDevices, 140, Point.Empty));
        bar.Controls.Add(OutlineButton("查看激活码", RefreshAdminCodes, 120, Point.Empty));
        bar.Controls.Add(OutlineButton("新建普通激活码", CreateClientActivationCode, 150, Point.Empty));
        _adminSummary.Text = "尚未刷新";
        _adminSummary.AutoSize = true;
        _adminSummary.Padding = new Padding(12, 9, 0, 0);
        bar.Controls.Add(_adminSummary);
        root.Controls.Add(bar);

        _adminDeviceCards.Dock = DockStyle.Fill;
        _adminDeviceCards.FlowDirection = FlowDirection.TopDown;
        _adminDeviceCards.WrapContents = false;
        _adminDeviceCards.AutoScroll = true;
        root.Controls.Add(_adminDeviceCards);
    }

    private static Panel CardPanel()
    {
        return new Panel { BackColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(12) };
    }

    private static Button PrimaryButton(string text, Action action, int width, Point location)
    {
        var button = new Button { Text = text, Width = width, Height = 34, Location = location, BackColor = Blue, ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
        button.FlatAppearance.BorderSize = 0;
        button.Click += (_, _) => RunUiAction(action);
        return button;
    }

    private static Button OutlineButton(string text, Action action, int width, Point location)
    {
        var button = new Button { Text = text, Width = width, Height = 34, Location = location, BackColor = Color.White, ForeColor = Slate, FlatStyle = FlatStyle.Flat };
        button.FlatAppearance.BorderColor = Color.FromArgb(203, 213, 225);
        button.Click += (_, _) => RunUiAction(action);
        return button;
    }

    private static void RunUiAction(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "bicarnet 操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static Button FlatButton(string text, Action action, int width, Point location)
    {
        var button = OutlineButton(text, action, width, location);
        button.Height = 30;
        return button;
    }

    private static void AddRow(TableLayoutPanel grid, string label, TextBox text, Control? right)
    {
        var row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleLeft, Height = 32, Dock = DockStyle.Fill }, 0, row);
        text.Dock = DockStyle.Fill;
        text.Margin = new Padding(0, 4, 8, 4);
        grid.Controls.Add(text, 1, row);
        if (right is not null) grid.Controls.Add(right, 2, row);
    }

    private void LoadDefaultProfile()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "dualnet-client-windows.json"),
            Path.Combine(Environment.CurrentDirectory, "dualnet-client-windows.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "bicarnet", "dualnet-client-windows.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DualNet", "dualnet-client-windows.json")
        };
        foreach (var path in candidates.Where(File.Exists))
        {
            LoadProfile(path);
            return;
        }
        ApplyProfile();
        SetClientMessage("未配置", "请选择连接端 Profile", "打开“高级”页选择 profile 文件。", Red);
    }

    private void LoadDefaultServerConfig()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "server", "dualnet-server.conf"),
            Path.Combine(AppContext.BaseDirectory, "dualnet-server.conf"),
            Path.Combine(Environment.CurrentDirectory, "runtime", "server", "dualnet-server.conf")
        };
        _serverConfigPath.Text = candidates.FirstOrDefault(File.Exists) ?? "dualnet-server.conf";
    }

    private void BrowseProfile()
    {
        using var dialog = new OpenFileDialog { Filter = "bicarnet profile (*.json)|*.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadProfile(dialog.FileName);
    }

    private void BrowseConfig()
    {
        using var dialog = new OpenFileDialog { Filter = "WireGuard config (*.conf)|*.conf|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _profile.ConfigPath = dialog.FileName;
        _profile.TunnelName = Path.GetFileNameWithoutExtension(dialog.FileName);
        ApplyProfile();
    }

    private void BrowseServerConfig()
    {
        using var dialog = new OpenFileDialog { Filter = "WireGuard config (*.conf)|*.conf|All files (*.*)|*.*" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _serverConfigPath.Text = dialog.FileName;
    }

    private void LoadProfile(string path)
    {
        try
        {
            var json = File.ReadAllText(path, Encoding.UTF8);
            _profile = JsonSerializer.Deserialize<ClientProfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ClientProfile();
            _profilePath.Text = path;
            ApplyProfile();
            Log($"已加载 profile: {path}");
        }
        catch (Exception ex)
        {
            Log($"加载 profile 失败: {ex.Message}");
        }
    }

    private void ApplyProfile()
    {
        _clientConfigPath.Text = _profile.ConfigPath;
        _statusApi.Text = string.IsNullOrWhiteSpace(_profile.StatusApiUrl) ? "http://10.77.0.1:8787/status" : _profile.StatusApiUrl;
    }

    private void RefreshAllStatus()
    {
        RefreshClientStatus();
        if (!ClientOnly)
            RefreshServerStatus();
    }

    private void RefreshClientStatus()
    {
        var state = GetServiceState(_profile.TunnelName);
        if (state == "运行中")
            SetClientMessage("已连接", "连接端正在运行", $"节点：{_profile.Endpoint}", Green);
        else if (state == "未安装")
            SetClientMessage("未连接", "点击“一键连接”开始使用", $"同 WiFi 可在 App 选择局域网节点：{_profile.LanEndpoint}", Slate);
        else
            SetClientMessage("未运行", "连接端服务已安装但未运行", "可先断开，再重新一键连接。", Red);
    }

    private void RefreshServerStatus()
    {
        var state = GetServiceState(DefaultServerTunnel);
        var apiRunning = _listener is not null;
        if (state == "运行中" && apiRunning)
            SetServerMessage("运行中", "服务端已准备好", $"设备列表接口：10.77.0.1:{StatusApiPort}", Green);
        else if (state == "运行中")
            SetServerMessage("部分就绪", "隧道已运行，设备接口未开启", "点击“启动状态接口”或重新点击“一键启动服务端”。", Blue);
        else
            SetServerMessage("未启动", "点击“一键启动服务端”", "启动后手机和电脑才能查看在线设备。", Slate);
    }

    private void SetClientMessage(string badge, string headline, string hint, Color color)
    {
        _clientBadge.Text = badge;
        _clientBadge.BackColor = color;
        _clientHeadline.Text = headline;
        _clientHint.Text = hint;
    }

    private void SetServerMessage(string badge, string headline, string hint, Color color)
    {
        _serverBadge.Text = badge;
        _serverBadge.BackColor = color;
        _serverHeadline.Text = headline;
        _serverHint.Text = hint;
    }

    private bool EnsureActivated()
    {
        if (IsActivated()) return true;

        using var dialog = new Form
        {
            Text = "bicarnet 激活",
            Width = 430,
            Height = 230,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };
        var label = new Label
        {
            Text = "首次使用这台电脑需要输入一次性激活码。",
            Location = new Point(18, 18),
            Size = new Size(370, 24)
        };
        var input = new TextBox
        {
            Location = new Point(18, 56),
            Width = 370,
            CharacterCasing = CharacterCasing.Upper
        };
        var hint = new Label
        {
            Text = "激活成功后会绑定当前设备，以后不再要求输入。",
            Location = new Point(18, 88),
            Size = new Size(370, 34),
            ForeColor = Slate
        };
        var ok = new Button
        {
            Text = "激活",
            DialogResult = DialogResult.OK,
            Location = new Point(216, 136),
            Width = 82
        };
        var cancel = new Button
        {
            Text = "取消",
            DialogResult = DialogResult.Cancel,
            Location = new Point(306, 136),
            Width = 82
        };
        dialog.Controls.AddRange(new Control[] { label, input, hint, ok, cancel });
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;

        if (dialog.ShowDialog(this) != DialogResult.OK) return false;
        var code = input.Text.Trim();
        if (string.IsNullOrWhiteSpace(code))
        {
            MessageBox.Show("请输入激活码。", "bicarnet 激活", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        try
        {
            var response = ClaimActivationAsync(code).GetAwaiter().GetResult();
            if (!response.Ok) throw new InvalidOperationException(response.Message);
            SaveLocalActivation(new LocalActivation
            {
                DeviceId = GetDeviceId(),
                DeviceName = Environment.MachineName,
                Platform = "windows",
                Role = AdminOnly ? "admin" : "client",
                Token = response.Token,
                ActivatedAt = string.IsNullOrWhiteSpace(response.ActivatedAt) ? DateTimeOffset.Now.ToString("O") : response.ActivatedAt
            });
            Log("设备已激活。");
            MessageBox.Show("激活成功，这台电脑以后不需要再次输入激活码。", "bicarnet 激活", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("激活失败：" + ex.Message + "\n\n请确认服务端已启动状态接口，并且当前网络可访问 TCP 8787。", "bicarnet 激活失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Log("激活失败: " + ex.Message);
            return false;
        }
    }

    private bool IsActivated()
    {
        try
        {
            var path = GetLocalActivationPath();
            if (!File.Exists(path)) return false;
            var activation = JsonSerializer.Deserialize<LocalActivation>(File.ReadAllText(path, Encoding.UTF8), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return activation is not null
                && activation.DeviceId == GetDeviceId()
                && activation.Platform.Equals("windows", StringComparison.OrdinalIgnoreCase)
                && activation.Role.Equals(AdminOnly ? "admin" : "client", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(activation.Token);
        }
        catch { return false; }
    }

    private async Task<ActivationResponse> ClaimActivationAsync(string code)
    {
        var request = new ActivationRequest
        {
            Code = code,
            DeviceId = GetDeviceId(),
            DeviceName = Environment.MachineName,
            Platform = "windows",
            Role = AdminOnly ? "admin" : "client",
            ProfileName = _profile.ProfileName,
            Account = _profile.Account,
            TunnelAddress = GetConfigAddress(_profile.ConfigPath)
        };
        var body = JsonSerializer.Serialize(request);
        Exception? last = null;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
        foreach (var url in ActivationUrlCandidates())
        {
            try
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await http.PostAsync(url, content);
                var text = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ActivationResponse>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (!response.IsSuccessStatusCode)
                    throw new InvalidOperationException(result?.Message ?? $"HTTP {(int)response.StatusCode}: {text}");
                return result ?? throw new InvalidOperationException("激活接口返回空数据。");
            }
            catch (Exception ex)
            {
                last = ex;
                Log($"激活接口不可达 {url}: {ex.Message}");
            }
        }
        throw last ?? new InvalidOperationException("没有可用的激活接口。");
    }

    private IEnumerable<string> ActivationUrlCandidates()
    {
        var endpoints = new[] { _profile.Endpoint, _profile.LanEndpoint };
        foreach (var endpoint in endpoints)
        {
            var host = EndpointHost(endpoint);
            if (!string.IsNullOrWhiteSpace(host))
                yield return $"http://{host}:{StatusApiPort}/activate";
        }
        if (Uri.TryCreate(_statusApi.Text.Trim(), UriKind.Absolute, out var statusUri))
            yield return new UriBuilder(statusUri) { Path = "activate", Query = "" }.Uri.ToString();
    }

    private static string EndpointHost(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) return "";
        var value = endpoint.Trim();
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) value = value[7..];
        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) value = value[8..];
        var slash = value.IndexOf('/');
        if (slash >= 0) value = value[..slash];
        var colon = value.LastIndexOf(':');
        if (colon > 0) value = value[..colon];
        return value.Trim();
    }

    private static string GetConfigAddress(string configPath)
    {
        try
        {
            if (!File.Exists(configPath)) return "";
            foreach (var line in File.ReadLines(configPath))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("Address", StringComparison.OrdinalIgnoreCase)) continue;
                var parts = trimmed.Split('=', 2);
                if (parts.Length != 2) return "";
                return parts[1].Split(',')[0].Trim();
            }
        }
        catch { }
        return "";
    }

    private static void SaveLocalActivation(LocalActivation activation)
    {
        var path = GetLocalActivationPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(activation, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
    }

    private static string GetLocalActivationPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "bicarnet", AdminOnly ? "admin-activation.json" : "activation.json");
    }

    private static string GetDeviceId()
    {
        try
        {
            var machineGuid = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", "")?.ToString();
            if (!string.IsNullOrWhiteSpace(machineGuid)) return "windows-" + Sha256(machineGuid);
        }
        catch { }
        return "windows-" + Sha256(Environment.MachineName);
    }

    private void ConnectClient()
    {
        SetClientMessage("正在连接", "正在安装并启动连接端", "通常需要几秒钟，请稍候。", Blue);
        Application.DoEvents();
        if (!EnsureActivated()) return;
        if (!ValidateAdminAndWireGuard()) return;
        if (!File.Exists(_profile.ConfigPath))
        {
            MessageBox.Show("找不到连接配置。请打开“高级”页重新选择配置文件。", "无法连接", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        InstallTunnel(_profile.ConfigPath);
        WaitForServiceState(_profile.TunnelName, "运行中", TimeSpan.FromSeconds(12));
        RefreshClientStatus();
        MessageBox.Show("连接端服务已启动。", "bicarnet", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void DisconnectClient()
    {
        SetClientMessage("正在断开", "正在停止连接端", "请稍候。", Blue);
        Application.DoEvents();
        if (!ValidateAdminAndWireGuard()) return;
        UninstallTunnel(_profile.TunnelName);
        WaitForServiceState(_profile.TunnelName, "未安装", TimeSpan.FromSeconds(8));
        RefreshClientStatus();
    }

    private void StartServer()
    {
        SetServerMessage("正在启动", "正在安装并启动服务端", "正在配置防火墙、WireGuard 隧道和设备状态接口。", Blue);
        Application.DoEvents();
        if (!ValidateAdminAndWireGuard()) return;
        if (!File.Exists(_serverConfigPath.Text))
        {
            MessageBox.Show("找不到服务端配置。请打开“高级”页选择 dist/windows/server/dualnet-server.conf。", "无法启动服务端", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        EnsureFirewallRule("bicarnet WireGuard UDP 51820", "UDP", "51820");
        EnsureFirewallRule($"bicarnet Status API TCP {StatusApiPort}", "TCP", StatusApiPort.ToString());
        if (GetServiceState(DefaultServerTunnel) != "运行中")
        {
            InstallTunnel(_serverConfigPath.Text);
            WaitForServiceState(DefaultServerTunnel, "运行中", TimeSpan.FromSeconds(12));
        }
        StartStatusApi();
        ApplyBlockedPeers();
        RefreshServerStatus();
        RefreshDevicesFromLocalServer();
        if (GetServiceState(DefaultServerTunnel) == "运行中" && _listener is not null)
            MessageBox.Show("服务端已启动。现在可以让手机或电脑连接，并在“设备”页刷新查看。", "bicarnet 服务端已就绪", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void StopServer()
    {
        SetServerMessage("正在停止", "正在停止服务端", "请稍候。", Blue);
        Application.DoEvents();
        if (!ValidateAdminAndWireGuard()) return;
        StopStatusApi();
        UninstallTunnel(DefaultServerTunnel);
        WaitForServiceState(DefaultServerTunnel, "未安装", TimeSpan.FromSeconds(8));
        RefreshServerStatus();
    }

    private void RefreshDevicesFromBestSource()
    {
        if (ClientOnly)
        {
            RefreshDevicesFromStatusApi();
            return;
        }

        if (GetServiceState(DefaultServerTunnel) == "运行中")
            RefreshDevicesFromLocalServer();
        else
            RefreshDevicesFromStatusApi();
    }

    private void ShowDevicesAndRefresh()
    {
        if (_tabs.TabPages.Count > 1)
            _tabs.SelectedIndex = 1;
        RefreshDevicesFromBestSource();
    }

    private void StartStatusApiIfServerIsRunning()
    {
        if (ClientOnly) return;
        if (GetServiceState(DefaultServerTunnel) != "运行中") return;
        StartStatusApi();
    }

    private string GetServiceState(string tunnelName)
    {
        var result = RunProcess("sc.exe", $"query \"WireGuardTunnel${tunnelName}\"");
        if (result.ExitCode != 0) return "未安装";
        if (result.Output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase)) return "运行中";
        if (result.Output.Contains("STOPPED", StringComparison.OrdinalIgnoreCase)) return "已停止";
        return "已安装";
    }

    private string WaitForServiceState(string tunnelName, string expectedState, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        string state;
        do
        {
            state = GetServiceState(tunnelName);
            if (state == expectedState) return state;
            Application.DoEvents();
            Thread.Sleep(300);
        } while (DateTime.UtcNow < deadline);

        return state;
    }

    private void RefreshDevicesFromLocalServer()
    {
        try
        {
            RenderDevices(BuildDeviceStatus());
            Log("已从本机服务端读取设备列表。");
        }
        catch (Exception ex)
        {
            ShowDeviceError("无法读取本机设备列表", "请先在首页点击“一键启动服务端”。", ex.Message);
        }
    }

    private async void RefreshDevicesFromStatusApi()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var json = await http.GetStringAsync(_statusApi.Text.Trim());
            var response = JsonSerializer.Deserialize<DeviceStatusResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (response is null) throw new InvalidOperationException("状态接口返回空数据。");
            RenderDevices(response);
        }
        catch (Exception ex)
        {
            ShowDeviceError("无法连接设备列表接口", "如果这是手机或远端电脑，请先确认 VPN 已连接；如果是服务端本机，请点击“一键启动服务端”。", ex.Message);
        }
    }

    private async void RefreshAdminDevices()
    {
        try
        {
            if (!EnsureActivated()) return;
            var token = LoadLocalActivationToken();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            http.DefaultRequestHeaders.Add("X-Bicarnet-Admin-Token", token);
            var json = await http.GetStringAsync(AdminUrl("/admin/devices"));
            var response = JsonSerializer.Deserialize<AdminDevicesResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (response is null) throw new InvalidOperationException("管理接口返回空数据。");
            RenderAdminDevices(response);
        }
        catch (Exception ex)
        {
            _adminDeviceCards.Controls.Clear();
            _adminSummary.Text = "刷新失败";
            var card = CardPanel();
            card.Width = 860;
            card.Height = 110;
            card.Controls.Add(new Label { Text = "无法读取管理设备列表", Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold), ForeColor = Red, Location = new Point(16, 14), AutoSize = true });
            card.Controls.Add(new Label { Text = ex.Message, Font = new Font("Consolas", 9), ForeColor = Slate, Location = new Point(16, 50), Size = new Size(790, 40) });
            _adminDeviceCards.Controls.Add(card);
        }
    }

    private void RenderAdminDevices(AdminDevicesResponse response)
    {
        _adminDevices.Clear();
        _adminDeviceCards.Controls.Clear();
        var clientDevices = response.Devices.Where(d => d.Role != "admin").ToList();
        _adminSummary.Text = $"{clientDevices.Count(d => d.Online)}/{clientDevices.Count} 台客户端在线，绑定总数 {response.Devices.Count}";
        foreach (var d in response.Devices.OrderBy(d => d.Role == "admin").ThenBy(d => d.Blocked).ThenBy(d => d.DeviceName))
        {
            _adminDevices[d.DeviceIdHash] = d;
            _adminDeviceCards.Controls.Add(AdminDeviceCard(d));
        }
    }

    private Panel AdminDeviceCard(AdminDeviceRecord d)
    {
        var card = CardPanel();
        card.Width = 880;
        card.Height = 138;
        card.Margin = new Padding(0, 0, 0, 10);
        var color = d.Blocked ? Red : d.Online ? Green : Slate;
        card.Controls.Add(new Label { Text = d.Role == "admin" ? "管理员" : d.Blocked ? "已停用" : d.Online ? "在线" : "离线", BackColor = color, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(16, 16), Size = new Size(74, 26) });
        card.Controls.Add(new Label { Text = $"{d.DeviceName} / {d.ProfileName}", Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(106, 14), Size = new Size(540, 28) });
        card.Controls.Add(new Label { Text = $"账号：{d.Account}    地址：{d.TunnelAddress}    平台：{d.Platform}", Font = new Font("Microsoft YaHei UI", 9), ForeColor = Slate, Location = new Point(106, 46), Size = new Size(720, 22) });
        card.Controls.Add(new Label { Text = $"来源：{(string.IsNullOrWhiteSpace(d.Endpoint) ? "尚未握手" : d.Endpoint)}    最近握手：{d.LatestHandshake}", Font = new Font("Microsoft YaHei UI", 9), ForeColor = Slate, Location = new Point(106, 70), Size = new Size(720, 22) });
        card.Controls.Add(new Label { Text = $"激活：{d.ActivatedAt}    {(d.Blocked ? "停用原因：" + d.BlockReason : "")}", Font = new Font("Microsoft YaHei UI", 9), ForeColor = Slate, Location = new Point(106, 94), Size = new Size(560, 22) });
        if (d.Role != "admin")
        {
            var action = d.Blocked ? OutlineButton("恢复", () => SetAdminDeviceBlocked(d.DeviceIdHash, false), 80, new Point(760, 54))
                                   : OutlineButton("踢下线", () => SetAdminDeviceBlocked(d.DeviceIdHash, true), 80, new Point(760, 54));
            card.Controls.Add(action);
        }
        return card;
    }

    private async void SetAdminDeviceBlocked(string deviceIdHash, bool blocked)
    {
        try
        {
            if (!_adminDevices.TryGetValue(deviceIdHash, out var device)) return;
            var confirm = MessageBox.Show(blocked ? $"确认踢下线并停用 {device.DeviceName}？" : $"确认恢复 {device.DeviceName}？", "bicarnet 管理", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (confirm != DialogResult.OK) return;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.Add("X-Bicarnet-Admin-Token", LoadLocalActivationToken());
            var body = JsonSerializer.Serialize(new AdminActionRequest { DeviceIdHash = deviceIdHash, Reason = blocked ? "管理员手动停用" : "" });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(AdminUrl(blocked ? "/admin/block" : "/admin/unblock"), content);
            var text = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException(text);
            MessageBox.Show(text, "bicarnet 管理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshAdminDevices();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "管理操作失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async void RefreshAdminCodes()
    {
        try
        {
            if (!EnsureActivated()) return;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            http.DefaultRequestHeaders.Add("X-Bicarnet-Admin-Token", LoadLocalActivationToken());
            var json = await http.GetStringAsync(AdminUrl("/admin/codes"));
            var response = JsonSerializer.Deserialize<AdminCodesResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (response is null) throw new InvalidOperationException("激活码接口返回空数据。");
            RenderAdminCodes(response);
        }
        catch (Exception ex)
        {
            _adminDeviceCards.Controls.Clear();
            _adminSummary.Text = "激活码刷新失败";
            var card = CardPanel();
            card.Width = 860;
            card.Height = 110;
            card.Controls.Add(new Label { Text = "无法读取激活码列表", Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold), ForeColor = Red, Location = new Point(16, 14), AutoSize = true });
            card.Controls.Add(new Label { Text = ex.Message, Font = new Font("Consolas", 9), ForeColor = Slate, Location = new Point(16, 50), Size = new Size(790, 40) });
            _adminDeviceCards.Controls.Add(card);
        }
    }

    private void RenderAdminCodes(AdminCodesResponse response)
    {
        _adminDeviceCards.Controls.Clear();
        var unused = response.Codes.Count(c => !c.Redeemed);
        var admin = response.Codes.Count(c => c.Role == "admin");
        _adminSummary.Text = $"激活码 {response.Codes.Count} 个，未使用 {unused} 个，管理员码 {admin} 个";
        foreach (var code in response.Codes.OrderBy(c => c.Role).ThenBy(c => c.Redeemed).ThenBy(c => c.CodeSuffix))
            _adminDeviceCards.Controls.Add(AdminCodeCard(code));
    }

    private static Panel AdminCodeCard(AdminCodeRecord c)
    {
        var card = CardPanel();
        card.Width = 880;
        card.Height = 116;
        card.Margin = new Padding(0, 0, 0, 10);
        var color = c.Blocked ? Red : c.Redeemed ? Green : Slate;
        card.Controls.Add(new Label { Text = c.Role == "admin" ? "管理员码" : c.Redeemed ? "已绑定" : "未使用", BackColor = color, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(16, 16), Size = new Size(84, 26) });
        card.Controls.Add(new Label { Text = string.IsNullOrWhiteSpace(c.Code) ? $"尾号 {c.CodeSuffix}" : c.Code, Font = new Font("Consolas", 12, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(116, 14), Size = new Size(520, 28) });
        var binding = c.Redeemed ? $"{c.DeviceName} / {c.ProfileName} / {c.Account}" : "尚未绑定设备";
        card.Controls.Add(new Label { Text = $"绑定：{binding}", Font = new Font("Microsoft YaHei UI", 9), ForeColor = Slate, Location = new Point(116, 46), Size = new Size(720, 22) });
        card.Controls.Add(new Label { Text = $"平台：{c.Platform}    地址：{c.TunnelAddress}    激活：{c.ActivatedAt}", Font = new Font("Microsoft YaHei UI", 9), ForeColor = Slate, Location = new Point(116, 70), Size = new Size(720, 22) });
        return card;
    }

    private async void CreateClientActivationCode()
    {
        try
        {
            if (!EnsureActivated()) return;
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
            http.DefaultRequestHeaders.Add("X-Bicarnet-Admin-Token", LoadLocalActivationToken());
            var body = JsonSerializer.Serialize(new AdminCreateCodesRequest { Count = 1, Role = "client" });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(AdminUrl("/admin/codes/create"), content);
            var text = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<AdminCreateCodesResponse>(text, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (!response.IsSuccessStatusCode || result is null || !result.Ok)
                throw new InvalidOperationException(result?.Message ?? text);
            MessageBox.Show(string.Join(Environment.NewLine, result.Codes), "新建普通激活码", MessageBoxButtons.OK, MessageBoxIcon.Information);
            RefreshAdminCodes();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "新建激活码失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string LoadLocalActivationToken()
    {
        var path = GetLocalActivationPath();
        if (!File.Exists(path)) throw new InvalidOperationException("管理端尚未激活。");
        var activation = JsonSerializer.Deserialize<LocalActivation>(File.ReadAllText(path, Encoding.UTF8), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (activation is null || string.IsNullOrWhiteSpace(activation.Token)) throw new InvalidOperationException("管理端激活信息无效。");
        return activation.Token;
    }

    private string AdminUrl(string path)
    {
        var status = _statusApi.Text.Trim();
        if (!Uri.TryCreate(status, UriKind.Absolute, out var uri))
            return "http://10.77.0.1:8787" + path;
        return new UriBuilder(uri) { Path = path.TrimStart('/'), Query = "" }.Uri.ToString();
    }

    private void RenderDevices(DeviceStatusResponse response)
    {
        _deviceCards.Controls.Clear();
        var online = response.Devices.Count(d => d.Online);
        _deviceSummary.Text = $"{online}/{response.Devices.Count} 台在线，最后刷新 {DateTime.Now:HH:mm:ss}";
        foreach (var d in response.Devices.OrderByDescending(d => d.Online).ThenBy(d => d.Name))
            _deviceCards.Controls.Add(DeviceCard(d));
    }

    private void ShowDeviceError(string title, string suggestion, string detail)
    {
        _deviceCards.Controls.Clear();
        _deviceSummary.Text = title;
        var card = CardPanel();
        card.Width = 860;
        card.Height = 130;
        card.Controls.Add(new Label { Text = title, Font = new Font("Microsoft YaHei UI", 13, FontStyle.Bold), ForeColor = Red, Location = new Point(16, 14), AutoSize = true });
        card.Controls.Add(new Label { Text = suggestion, Font = new Font("Microsoft YaHei UI", 10), ForeColor = Slate, Location = new Point(16, 48), Size = new Size(790, 28) });
        card.Controls.Add(new Label { Text = $"技术信息：{detail}", Font = new Font("Consolas", 9), ForeColor = Color.FromArgb(100, 116, 139), Location = new Point(16, 82), Size = new Size(790, 28) });
        _deviceCards.Controls.Add(card);
        Log($"{title}: {detail}");
    }

    private static Panel DeviceCard(PeerStatus d)
    {
        var card = CardPanel();
        card.Width = 860;
        card.Height = 118;
        card.Margin = new Padding(0, 0, 0, 10);
        var color = d.Online ? Green : Slate;
        card.Controls.Add(new Label { Text = d.Online ? "在线" : "离线", BackColor = color, ForeColor = Color.White, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(16, 16), Size = new Size(62, 26) });
        card.Controls.Add(new Label { Text = d.Name, Font = new Font("Microsoft YaHei UI", 12, FontStyle.Bold), ForeColor = Color.FromArgb(15, 23, 42), Location = new Point(92, 14), Size = new Size(520, 28) });
        card.Controls.Add(new Label { Text = $"地址：{d.AllowedIps}    来源：{(string.IsNullOrWhiteSpace(d.Endpoint) ? "尚未握手" : d.Endpoint)}", Font = new Font("Microsoft YaHei UI", 9), ForeColor = Slate, Location = new Point(92, 48), Size = new Size(720, 24) });
        card.Controls.Add(new Label { Text = $"最近握手：{d.LatestHandshake}    流量：RX {FormatBytes(d.RxBytes)} / TX {FormatBytes(d.TxBytes)}", Font = new Font("Microsoft YaHei UI", 9), ForeColor = Slate, Location = new Point(92, 74), Size = new Size(720, 24) });
        return card;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / 1024.0 / 1024.0:F1} MB";
    }

    private void StartStatusApi()
    {
        if (_listener is not null)
        {
            RefreshServerStatus();
            return;
        }
        try
        {
            _listenerCts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{StatusApiPort}/");
            _listener.Start();
            _ = Task.Run(() => ListenLoop(_listenerCts.Token));
            Log($"状态接口已启动: http://10.77.0.1:{StatusApiPort}/status");
        }
        catch (Exception ex)
        {
            _listener = null;
            MessageBox.Show($"状态接口启动失败：{ex.Message}", "需要检查权限或端口占用", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        RefreshServerStatus();
    }

    private void StopStatusApi()
    {
        try
        {
            _listenerCts?.Cancel();
            _listener?.Stop();
            _listener?.Close();
        }
        catch { }
        finally
        {
            _listener = null;
            _listenerCts = null;
        }
        RefreshServerStatus();
    }

    private async Task ListenLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _listener is not null && _listener.IsListening)
        {
            try
            {
                var context = await _listener.GetContextAsync();
                _ = Task.Run(() => HandleRequest(context), token);
            }
            catch when (token.IsCancellationRequested) { return; }
            catch { if (_listener is null || !_listener.IsListening) return; }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "";
            if (path.Equals("/activate", StringComparison.OrdinalIgnoreCase))
            {
                HandleActivationRequest(context);
                return;
            }
            if (path.Equals("/admin/devices", StringComparison.OrdinalIgnoreCase))
            {
                HandleAdminDevicesRequest(context);
                return;
            }
            if (path.Equals("/admin/block", StringComparison.OrdinalIgnoreCase))
            {
                HandleAdminActionRequest(context, true);
                return;
            }
            if (path.Equals("/admin/unblock", StringComparison.OrdinalIgnoreCase))
            {
                HandleAdminActionRequest(context, false);
                return;
            }
            if (path.Equals("/admin/codes", StringComparison.OrdinalIgnoreCase))
            {
                HandleAdminCodesRequest(context);
                return;
            }
            if (path.Equals("/admin/codes/create", StringComparison.OrdinalIgnoreCase))
            {
                HandleAdminCreateCodesRequest(context);
                return;
            }

            if (!path.Equals("/status", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }
            var json = JsonSerializer.Serialize(BuildDeviceStatus(), new JsonSerializerOptions { WriteIndented = true });
            var bytes = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }
        catch (Exception ex)
        {
            var bytes = Encoding.UTF8.GetBytes("{\"error\":\"" + ex.Message.Replace("\"", "'") + "\"}");
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json; charset=utf-8";
            context.Response.OutputStream.Write(bytes, 0, bytes.Length);
            context.Response.Close();
        }
    }

    private void HandleActivationRequest(HttpListenerContext context)
    {
        if (!context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(context, 405, new ActivationResponse { Ok = false, Message = "Activation requires POST." });
            return;
        }

        var body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8).ReadToEnd();
        var request = JsonSerializer.Deserialize<ActivationRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (request is null || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.DeviceId))
        {
            WriteJson(context, 400, new ActivationResponse { Ok = false, Message = "激活码或设备信息为空。" });
            return;
        }

        var storePath = GetActivationStorePath();
        if (!File.Exists(storePath))
        {
            WriteJson(context, 503, new ActivationResponse { Ok = false, Message = "服务端未安装激活码库 activation-codes.json。" });
            return;
        }

        lock (typeof(MainForm))
        {
            var store = JsonSerializer.Deserialize<ActivationStore>(File.ReadAllText(storePath, Encoding.UTF8), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ActivationStore();
            var codeHash = Sha256(NormalizeCode(request.Code));
            var deviceHash = Sha256(request.DeviceId);
            var record = store.Codes.FirstOrDefault(c => c.CodeHash.Equals(codeHash, StringComparison.OrdinalIgnoreCase));
            var requestedRole = NormalizeRole(request.Role);
            if (record is null)
            {
                WriteJson(context, 403, new ActivationResponse { Ok = false, Message = "激活码无效。" });
                return;
            }

            if (!NormalizeRole(record.Role).Equals(requestedRole, StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context, 403, new ActivationResponse { Ok = false, Message = requestedRole == "admin" ? "这不是管理员激活码。" : "这不是客户端激活码。" });
                return;
            }

            if (record.Redeemed && !record.DeviceIdHash.Equals(deviceHash, StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context, 409, new ActivationResponse { Ok = false, Message = "这个激活码已经被其他设备使用。" });
                return;
            }

            if (record.Blocked && record.DeviceIdHash.Equals(deviceHash, StringComparison.OrdinalIgnoreCase))
            {
                WriteJson(context, 403, new ActivationResponse { Ok = false, Message = "此设备已被管理员停用。" });
                return;
            }

            var token = NewToken();
            var activatedAt = DateTimeOffset.Now.ToString("O");
            record.Redeemed = true;
            record.DeviceIdHash = deviceHash;
            record.DeviceName = request.DeviceName;
            record.Platform = request.Platform;
            record.Role = requestedRole;
            record.ProfileName = request.ProfileName;
            record.Account = request.Account;
            record.TunnelAddress = request.TunnelAddress;
            record.ActivatedAt = activatedAt;
            record.TokenHash = Sha256(token);
            File.WriteAllText(storePath, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            WriteJson(context, 200, new ActivationResponse { Ok = true, Message = "激活成功。", Token = token, ActivatedAt = activatedAt });
        }
    }

    private void HandleAdminDevicesRequest(HttpListenerContext context)
    {
        if (!IsAdminRequest(context))
        {
            WriteJson(context, 403, new { ok = false, message = "Admin token required." });
            return;
        }
        WriteJson(context, 200, BuildAdminDevicesResponse());
    }

    private void HandleAdminCodesRequest(HttpListenerContext context)
    {
        if (!IsAdminRequest(context))
        {
            WriteJson(context, 403, new { ok = false, message = "Admin token required." });
            return;
        }
        WriteJson(context, 200, BuildAdminCodesResponse());
    }

    private void HandleAdminCreateCodesRequest(HttpListenerContext context)
    {
        if (!context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(context, 405, new AdminCreateCodesResponse { Ok = false, Message = "POST required." });
            return;
        }
        if (!IsAdminRequest(context))
        {
            WriteJson(context, 403, new AdminCreateCodesResponse { Ok = false, Message = "Admin token required." });
            return;
        }

        var body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8).ReadToEnd();
        var request = JsonSerializer.Deserialize<AdminCreateCodesRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new AdminCreateCodesRequest();
        var count = Math.Clamp(request.Count, 1, 20);
        var role = NormalizeRole(request.Role);
        if (role == "admin")
        {
            WriteJson(context, 400, new AdminCreateCodesResponse { Ok = false, Message = "管理端只允许从这里新建普通激活码。" });
            return;
        }

        lock (typeof(MainForm))
        {
            var storePath = GetActivationStorePath();
            var store = LoadActivationStore(storePath);
            var codes = new List<string>();
            for (var i = 0; i < count; i++)
            {
                var code = NewActivationCode();
                codes.Add(code);
                store.Codes.Add(NewActivationCodeRecord(code, "client"));
            }
            SaveActivationStore(storePath, store);
            AppendPlainActivationCodes(codes, "client");
            WriteJson(context, 200, new AdminCreateCodesResponse { Ok = true, Message = "激活码已创建。", Codes = codes });
        }
    }

    private void HandleAdminActionRequest(HttpListenerContext context, bool block)
    {
        if (!context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(context, 405, new { ok = false, message = "POST required." });
            return;
        }
        if (!IsAdminRequest(context))
        {
            WriteJson(context, 403, new { ok = false, message = "Admin token required." });
            return;
        }

        var body = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8).ReadToEnd();
        var request = JsonSerializer.Deserialize<AdminActionRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (request is null || string.IsNullOrWhiteSpace(request.DeviceIdHash))
        {
            WriteJson(context, 400, new { ok = false, message = "DeviceIdHash required." });
            return;
        }

        lock (typeof(MainForm))
        {
            var storePath = GetActivationStorePath();
            var store = LoadActivationStore(storePath);
            var record = store.Codes.FirstOrDefault(c => c.Redeemed && c.DeviceIdHash.Equals(request.DeviceIdHash, StringComparison.OrdinalIgnoreCase));
            if (record is null)
            {
                WriteJson(context, 404, new { ok = false, message = "Device not found." });
                return;
            }
            if (NormalizeRole(record.Role) == "admin")
            {
                WriteJson(context, 400, new { ok = false, message = "Admin devices cannot be blocked from this UI." });
                return;
            }

            record.Blocked = block;
            record.BlockedAt = block ? DateTimeOffset.Now.ToString("O") : "";
            record.BlockReason = block ? request.Reason : "";
            SaveActivationStore(storePath, store);

            var wireGuardMessage = block ? TryRemovePeer(record) : TryRestorePeer(record);
            WriteJson(context, 200, new { ok = true, blocked = block, message = wireGuardMessage });
        }
    }

    private bool IsAdminRequest(HttpListenerContext context)
    {
        var token = context.Request.Headers["X-Bicarnet-Admin-Token"] ?? "";
        if (string.IsNullOrWhiteSpace(token)) return false;
        var tokenHash = Sha256(token);
        var storePath = GetActivationStorePath();
        if (!File.Exists(storePath)) return false;
        var store = LoadActivationStore(storePath);
        return store.Codes.Any(c =>
            c.Redeemed &&
            !c.Blocked &&
            NormalizeRole(c.Role) == "admin" &&
            c.TokenHash.Equals(tokenHash, StringComparison.OrdinalIgnoreCase));
    }

    private AdminDevicesResponse BuildAdminDevicesResponse()
    {
        var store = LoadActivationStore(GetActivationStorePath());
        var status = TryBuildDeviceStatus();
        var devices = new List<AdminDeviceRecord>();
        foreach (var record in store.Codes.Where(c => c.Redeemed))
        {
            var peer = FindPeerStatus(status, record);
            devices.Add(new AdminDeviceRecord
            {
                DeviceIdHash = record.DeviceIdHash,
                DeviceName = record.DeviceName,
                Platform = record.Platform,
                Role = NormalizeRole(record.Role),
                ProfileName = record.ProfileName,
                Account = record.Account,
                TunnelAddress = record.TunnelAddress,
                ActivatedAt = record.ActivatedAt,
                Blocked = record.Blocked,
                BlockedAt = record.BlockedAt,
                BlockReason = record.BlockReason,
                Online = peer?.Online ?? false,
                Endpoint = peer?.Endpoint ?? "",
                LatestHandshake = peer?.LatestHandshake ?? "",
                RxBytes = peer?.RxBytes ?? 0,
                TxBytes = peer?.TxBytes ?? 0
            });
        }
        return new AdminDevicesResponse { Devices = devices.OrderBy(d => d.Role).ThenBy(d => d.Blocked).ThenBy(d => d.DeviceName).ToList() };
    }

    private AdminCodesResponse BuildAdminCodesResponse()
    {
        var store = LoadActivationStore(GetActivationStorePath());
        var plainCodes = ReadPlainActivationCodes();
        var codes = store.Codes.Select(record =>
        {
            var normalizedRole = NormalizeRole(record.Role);
            var code = plainCodes.FirstOrDefault(c =>
                NormalizeRole(c.Role) == normalizedRole &&
                Sha256(NormalizeCode(c.Code)).Equals(record.CodeHash, StringComparison.OrdinalIgnoreCase)).Code ?? "";
            return new AdminCodeRecord
            {
                Code = code,
                CodeSuffix = record.CodeSuffix,
                Role = normalizedRole,
                Redeemed = record.Redeemed,
                Blocked = record.Blocked,
                DeviceName = record.DeviceName,
                Platform = record.Platform,
                ProfileName = record.ProfileName,
                Account = record.Account,
                TunnelAddress = record.TunnelAddress,
                ActivatedAt = record.ActivatedAt
            };
        }).ToList();
        return new AdminCodesResponse { Codes = codes };
    }

    private DeviceStatusResponse TryBuildDeviceStatus()
    {
        try { return BuildDeviceStatus(); }
        catch { return new DeviceStatusResponse(); }
    }

    private static PeerStatus? FindPeerStatus(DeviceStatusResponse response, ActivationCodeRecord record)
    {
        var tunnelIp = AddressIp(record.TunnelAddress);
        return response.Devices.FirstOrDefault(d =>
            (!string.IsNullOrWhiteSpace(record.ProfileName) && d.Name.Contains(record.ProfileName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(tunnelIp) && d.AllowedIps.Contains(tunnelIp, StringComparison.OrdinalIgnoreCase)));
    }

    private string TryRemovePeer(ActivationCodeRecord record)
    {
        var peer = FindServerPeer(record);
        if (peer is null) return "已停用，但未找到可移除的 WireGuard peer。";
        var result = RunProcess(ResolveWireGuardTool("wg.exe"), $"set {DefaultServerTunnel} peer {peer.PublicKey} remove");
        return result.ExitCode == 0 ? "已停用并踢下线。" : $"已停用，踢下线失败：{result.Error}{result.Output}";
    }

    private void ApplyBlockedPeers()
    {
        try
        {
            var storePath = GetActivationStorePath();
            if (!File.Exists(storePath)) return;
            var store = LoadActivationStore(storePath);
            foreach (var record in store.Codes.Where(c => c.Redeemed && c.Blocked && NormalizeRole(c.Role) != "admin"))
                Log(TryRemovePeer(record));
        }
        catch (Exception ex)
        {
            Log("应用停用设备列表失败: " + ex.Message);
        }
    }

    private string TryRestorePeer(ActivationCodeRecord record)
    {
        var peer = FindServerPeer(record);
        if (peer is null) return "已恢复，但未找到可恢复的 WireGuard peer。";
        var result = RunProcess(ResolveWireGuardTool("wg.exe"), $"set {DefaultServerTunnel} peer {peer.PublicKey} allowed-ips {peer.AllowedIps}");
        return result.ExitCode == 0 ? "已恢复并重新允许连接。" : $"已恢复，WireGuard 恢复失败：{result.Error}{result.Output}";
    }

    private ServerPeerConfig? FindServerPeer(ActivationCodeRecord record)
    {
        var tunnelIp = AddressIp(record.TunnelAddress);
        return ParseServerPeers(_serverConfigPath.Text).FirstOrDefault(p =>
            (!string.IsNullOrWhiteSpace(record.ProfileName) && p.Name.Contains(record.ProfileName, StringComparison.OrdinalIgnoreCase)) ||
            (!string.IsNullOrWhiteSpace(tunnelIp) && p.AllowedIps.Contains(tunnelIp, StringComparison.OrdinalIgnoreCase)));
    }

    private static ActivationStore LoadActivationStore(string path)
    {
        return JsonSerializer.Deserialize<ActivationStore>(File.ReadAllText(path, Encoding.UTF8), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new ActivationStore();
    }

    private static void SaveActivationStore(string path, ActivationStore store)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
    }

    private static void WriteJson(HttpListenerContext context, int statusCode, object payload)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    private static string GetActivationStorePath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "activation-codes.json");
        if (File.Exists(local)) return local;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "bicarnet", "activation-codes.json");
    }

    private static string GetActivationPlainPath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "activation-codes-plain.txt");
        if (File.Exists(local)) return local;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "bicarnet", "activation-codes-plain.txt");
    }

    private static string NewActivationCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        var chars = bytes.Select(b => alphabet[b % alphabet.Length]).ToArray();
        var raw = new string(chars);
        return $"BICAR-{raw[..4]}-{raw.Substring(4, 4)}-{raw.Substring(8, 4)}";
    }

    private static ActivationCodeRecord NewActivationCodeRecord(string code, string role)
    {
        var normalized = NormalizeCode(code);
        return new ActivationCodeRecord
        {
            CodeHash = Sha256(normalized),
            CodeSuffix = normalized.Substring(Math.Max(0, normalized.Length - 4)),
            Role = NormalizeRole(role)
        };
    }

    private static List<(string Code, string Role)> ReadPlainActivationCodes()
    {
        var result = new List<(string Code, string Role)>();
        var path = GetActivationPlainPath();
        if (!File.Exists(path)) return result;
        var role = "client";
        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            var line = raw.Trim();
            if (line.StartsWith("# Role:", StringComparison.OrdinalIgnoreCase))
            {
                role = NormalizeRole(line.Split(':', 2)[1].Trim());
                continue;
            }
            if (line.StartsWith("#") || string.IsNullOrWhiteSpace(line)) continue;
            if (NormalizeCode(line).Length > 0)
                result.Add((line, role));
        }
        return result;
    }

    private static void AppendPlainActivationCodes(IEnumerable<string> codes, string role)
    {
        var path = GetActivationPlainPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var lines = new List<string>
        {
            "",
            "# Generated by admin UI: " + DateTimeOffset.Now.ToString("O"),
            "# Role: " + NormalizeRole(role),
            ""
        };
        lines.AddRange(codes);
        File.AppendAllLines(path, lines, Encoding.UTF8);
    }

    private static string NormalizeCode(string code)
    {
        return new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }

    private static string NormalizeRole(string role)
    {
        return role.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "client";
    }

    private static string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private DeviceStatusResponse BuildDeviceStatus()
    {
        var result = RunProcess(ResolveWireGuardTool("wg.exe"), $"show {DefaultServerTunnel} dump");
        if (result.ExitCode != 0)
            throw new InvalidOperationException(result.Error.Trim().Length > 0 ? result.Error.Trim() : result.Output.Trim());

        var names = ParsePeerNames(_serverConfigPath.Text);
        var devices = new List<PeerStatus>();
        var lines = result.Output.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines.Skip(1))
        {
            var parts = line.Split('\t');
            if (parts.Length < 8) continue;
            var publicKey = parts[0];
            var latest = long.TryParse(parts[4], out var ts) ? ts : 0;
            var rx = long.TryParse(parts[5], out var r) ? r : 0;
            var tx = long.TryParse(parts[6], out var t) ? t : 0;
            devices.Add(new PeerStatus
            {
                Name = names.TryGetValue(publicKey, out var name) ? name : publicKey[..Math.Min(12, publicKey.Length)],
                PublicKey = publicKey,
                Endpoint = parts[2] == "(none)" ? "" : parts[2],
                AllowedIps = parts[3],
                LatestHandshakeUnix = latest,
                LatestHandshake = FormatHandshake(latest),
                RxBytes = rx,
                TxBytes = tx,
                Online = latest > 0 && DateTimeOffset.UtcNow.ToUnixTimeSeconds() - latest <= 180
            });
        }
        return new DeviceStatusResponse { Server = DefaultServerTunnel, GeneratedAt = DateTimeOffset.Now.ToString("O"), Devices = devices };
    }

    private static Dictionary<string, string> ParsePeerNames(string configPath)
    {
        var map = new Dictionary<string, string>();
        if (!File.Exists(configPath)) return map;
        string? pendingName = null;
        foreach (var raw in File.ReadLines(configPath))
        {
            var line = raw.Trim();
            if (line.StartsWith("#"))
            {
                pendingName = line.TrimStart('#').Trim();
                continue;
            }
            if (line.StartsWith("PublicKey", StringComparison.OrdinalIgnoreCase) && pendingName is not null)
            {
                map[line.Split('=', 2)[1].Trim()] = pendingName;
                pendingName = null;
            }
        }
        return map;
    }

    private static List<ServerPeerConfig> ParseServerPeers(string configPath)
    {
        var peers = new List<ServerPeerConfig>();
        if (!File.Exists(configPath)) return peers;
        string? pendingName = null;
        ServerPeerConfig? current = null;
        foreach (var raw in File.ReadLines(configPath))
        {
            var line = raw.Trim();
            if (line.StartsWith("#"))
            {
                pendingName = line.TrimStart('#').Trim();
                continue;
            }
            if (line.Equals("[Peer]", StringComparison.OrdinalIgnoreCase))
            {
                if (current is not null && !string.IsNullOrWhiteSpace(current.PublicKey)) peers.Add(current);
                current = new ServerPeerConfig { Name = pendingName ?? "" };
                pendingName = null;
                continue;
            }
            if (current is null) continue;
            if (line.StartsWith("PublicKey", StringComparison.OrdinalIgnoreCase))
                current.PublicKey = line.Split('=', 2)[1].Trim();
            else if (line.StartsWith("AllowedIPs", StringComparison.OrdinalIgnoreCase))
                current.AllowedIps = line.Split('=', 2)[1].Trim();
        }
        if (current is not null && !string.IsNullOrWhiteSpace(current.PublicKey)) peers.Add(current);
        return peers;
    }

    private static string AddressIp(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "";
        return address.Split('/')[0].Split(',')[0].Trim();
    }

    private static string FormatHandshake(long unix)
    {
        if (unix <= 0) return "从未连接";
        var dt = DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
        var age = DateTime.Now - dt;
        if (age.TotalSeconds < 60) return $"{Math.Max(0, (int)age.TotalSeconds)} 秒前";
        if (age.TotalMinutes < 60) return $"{(int)age.TotalMinutes} 分钟前";
        if (age.TotalHours < 24) return $"{(int)age.TotalHours} 小时前";
        return dt.ToString("yyyy-MM-dd HH:mm");
    }

    private bool ValidateAdminAndWireGuard()
    {
        if (!IsAdministrator())
        {
            MessageBox.Show("请以管理员身份运行 bicarnet。安装或停止隧道服务需要管理员权限。", "需要管理员权限", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
        try { ResolveWireGuardTool("wireguard.exe"); return true; }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "未找到 WireGuard", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
    }

    private void InstallTunnel(string configPath)
    {
        var tunnelName = Path.GetFileNameWithoutExtension(configPath);
        if (GetServiceState(tunnelName) != "未安装") UninstallTunnel(tunnelName);
        RunLogged(ResolveWireGuardTool("wireguard.exe"), $"/installtunnelservice \"{Path.GetFullPath(configPath)}\"");
    }

    private void UninstallTunnel(string tunnelName)
    {
        if (GetServiceState(tunnelName) == "未安装") return;
        RunLogged(ResolveWireGuardTool("wireguard.exe"), $"/uninstalltunnelservice {tunnelName}");
    }

    private static string ResolveWireGuardTool(string name)
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WireGuard", name),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "WireGuard", name)
        };
        foreach (var candidate in candidates)
            if (File.Exists(candidate)) return candidate;
        throw new FileNotFoundException($"找不到 {name}。请先安装 WireGuard for Windows。");
    }

    private void EnsureFirewallRule(string displayName, string protocol, string port)
    {
        var check = RunProcess("powershell.exe", $"-NoProfile -NonInteractive -Command \"Get-NetFirewallRule -DisplayName '{displayName}' -ErrorAction SilentlyContinue\"");
        if (check.ExitCode == 0 && !string.IsNullOrWhiteSpace(check.Output)) return;
        RunLogged("powershell.exe", $"-NoProfile -NonInteractive -Command \"New-NetFirewallRule -DisplayName '{displayName}' -Direction Inbound -Action Allow -Protocol {protocol} -LocalPort {port}\"");
    }

    private void RunLogged(string fileName, string arguments)
    {
        Log($"> {Path.GetFileName(fileName)} {arguments}");
        var result = RunProcess(fileName, arguments);
        if (!string.IsNullOrWhiteSpace(result.Output)) Log(result.Output.Trim());
        if (!string.IsNullOrWhiteSpace(result.Error)) Log(result.Error.Trim());
        Log($"ExitCode: {result.ExitCode}");
    }

    private static (int ExitCode, string Output, string Error) RunProcess(string fileName, string arguments)
    {
        const int timeoutMs = 120000;
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true
        };
        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Unable to start {fileName}");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); }
            catch { }
            return (-1, "", $"Command timed out after {timeoutMs / 1000} seconds: {Path.GetFileName(fileName)} {arguments}");
        }
        var output = outputTask.GetAwaiter().GetResult();
        var error = errorTask.GetAwaiter().GetResult();
        return (process.ExitCode, output, error);
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }

    private void Log(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        if (InvokeRequired)
        {
            BeginInvoke(() => Log(message));
            return;
        }
        _diagnostics.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }
}
