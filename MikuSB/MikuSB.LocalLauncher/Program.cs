using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MikuSB.LocalLauncher;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new LocalLauncherForm());
    }
}

internal sealed class LocalLauncherForm : Form
{
    private const int ServerPort = 13443;
    private static readonly string[] LocalLoginHosts =
    [
        "cbjq-serverlist.xoyocdn.com",
        "cbjq-client.xoyocdn.com",
        "cbjq-client-qq.xoyocdn.com",
        "cbjq-client-hsyq.xoyocdn.com",
        "js2sdk.xoyo.com",
        "sh-jxsj.xgsdk.com",
        "passport.xoyo.com",
        "xgsdk.xoyo.games",
        "xqdata.xoyo.games",
        "sh-qrcode.xoyo.com"
    ];

    private readonly string _rootDirectory;
    private readonly string _serverDirectory;
    private readonly string _serverExecutable;
    private readonly string _clientLauncherExecutable;
    private readonly string _routingScript;
    private readonly Label _routingStatus = new();
    private readonly Label _serverStatus = new();
    private readonly TextBox _activityLog = new();
    private readonly Button _prepareButton = new();
    private readonly Button _startServerButton = new();
    private readonly Button _startClientButton = new();
    private readonly Button _startAllButton = new();
    private readonly Button _gachaPresetButton = new();
    private readonly System.Windows.Forms.Timer _statusTimer = new();
    private bool _busy;

    public LocalLauncherForm()
    {
        _rootDirectory = FindRootDirectory();
        _serverDirectory = Path.Combine(_rootDirectory, "Server");
        _serverExecutable = Path.Combine(_serverDirectory, "MikuSB.exe");
        _clientLauncherExecutable = Path.Combine(_serverDirectory, "SnowLauncher.exe");
        _routingScript = Path.Combine(_rootDirectory, "Configure-LocalLoginRouting.ps1");

        Text = "Snowbreak Local Launcher";
        MinimumSize = new Size(760, 560);
        Size = new Size(760, 560);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(20, 27, 44);
        ForeColor = Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 24, 28, 24),
            ColumnCount = 1,
            RowCount = 5,
            BackColor = BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            Text = "尘白禁区 · 本地离线启动器",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 20, FontStyle.Bold),
            ForeColor = Color.FromArgb(119, 184, 255),
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            Text = "保留系统代理 / VPN；仅本地登录域名绕过代理。服务端会持续运行，关闭游戏或本窗口不会自动关服。",
            AutoSize = true,
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(205, 215, 232),
            Margin = new Padding(0, 0, 0, 18)
        }, 0, 1);

        var statusPanel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            BackColor = Color.FromArgb(29, 39, 62),
            Padding = new Padding(16, 12, 16, 12),
            Margin = new Padding(0, 0, 0, 16)
        };
        _routingStatus.AutoSize = true;
        _routingStatus.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Regular);
        _serverStatus.AutoSize = true;
        _serverStatus.Font = new Font("Microsoft YaHei UI", 10, FontStyle.Regular);
        _serverStatus.Margin = new Padding(0, 6, 0, 0);
        statusPanel.Controls.Add(_routingStatus, 0, 0);
        statusPanel.Controls.Add(_serverStatus, 0, 1);
        layout.Controls.Add(statusPanel, 0, 2);

        _activityLog.Dock = DockStyle.Fill;
        _activityLog.Multiline = true;
        _activityLog.ReadOnly = true;
        _activityLog.ScrollBars = ScrollBars.Vertical;
        _activityLog.BackColor = Color.FromArgb(13, 19, 31);
        _activityLog.ForeColor = Color.FromArgb(213, 224, 241);
        _activityLog.Font = new Font("Cascadia Mono", 9);
        _activityLog.BorderStyle = BorderStyle.FixedSingle;
        _activityLog.Margin = new Padding(0, 0, 0, 16);
        layout.Controls.Add(_activityLog, 0, 3);

        var actionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0)
        };
        layout.Controls.Add(actionPanel, 0, 4);

        ConfigureButton(_startAllButton, "启动本地双端", Color.FromArgb(44, 110, 197));
        ConfigureButton(_prepareButton, "配置本地登录路由", Color.FromArgb(58, 69, 93));
        ConfigureButton(_startServerButton, "仅启动服务端", Color.FromArgb(58, 69, 93));
        ConfigureButton(_startClientButton, "仅启动客户端", Color.FromArgb(58, 69, 93));
        ConfigureButton(_gachaPresetButton, "卡池显示（暂不可部署）", Color.FromArgb(83, 66, 128));
        var openLogButton = new Button();
        ConfigureButton(openLogButton, "打开日志", Color.FromArgb(58, 69, 93));

        _startAllButton.Click += async (_, _) => await RunExclusiveAsync(StartAllAsync);
        _prepareButton.Click += async (_, _) => await RunExclusiveAsync(PrepareRoutingAsync);
        _startServerButton.Click += async (_, _) => await RunExclusiveAsync(StartServerAsync);
        _startClientButton.Click += (_, _) => StartClient();
        _gachaPresetButton.Click += (_, _) => OpenGachaPreset();
        openLogButton.Click += (_, _) => OpenServerLog();
        actionPanel.Controls.AddRange([_startAllButton, _prepareButton, _startServerButton, _startClientButton, _gachaPresetButton, openLogButton]);

        _statusTimer.Interval = 1500;
        _statusTimer.Tick += (_, _) => RefreshStatus();
        Shown += (_, _) =>
        {
            AppendLog($"Workspace: {_rootDirectory}");
            RefreshStatus();
            _statusTimer.Start();
        };
    }

    private async Task StartAllAsync()
    {
        if (!await PrepareRoutingAsync())
            return;

        if (!await EnsureServerReadyAsync())
            return;

        StartClient();
    }

    private async Task StartServerAsync()
    {
        await EnsureServerReadyAsync();
    }

    private async Task<bool> PrepareRoutingAsync()
    {
        if (!File.Exists(_routingScript))
        {
            ShowError($"Missing routing script:\n{_routingScript}");
            return false;
        }

        AppendLog("Checking local login routing. The current VPN and proxy will be preserved.");
        var powerShell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = powerShell,
            Arguments = $"-NoProfile -EP Bypass -File {QuoteArgument(_routingScript)}",
            WorkingDirectory = _rootDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            ShowError("Unable to start the local routing setup.");
            return false;
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        var output = (await standardOutput).Trim();
        var error = (await standardError).Trim();
        if (!string.IsNullOrWhiteSpace(output))
            AppendLog(output);
        if (!string.IsNullOrWhiteSpace(error))
            AppendLog(error);

        RefreshStatus();
        if (process.ExitCode != 0 || !HasLocalLoginRouting())
        {
            ShowError("Local login routing was not completed. If Windows asked for permission, approve the UAC prompt and retry.");
            return false;
        }

        AppendLog("Local login routing is ready.");
        return true;
    }

    private async Task<bool> EnsureServerReadyAsync()
    {
        if (!File.Exists(_serverExecutable))
        {
            ShowError($"Missing server executable:\n{_serverExecutable}");
            return false;
        }

        if (IsServerListening())
        {
            AppendLog($"Server is already listening on 127.0.0.1:{ServerPort}.");
            return true;
        }

        AppendLog("Starting the server in a visible console window.");
        var commandInterpreter = Environment.GetEnvironmentVariable("ComSpec") ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
        Process.Start(new ProcessStartInfo
        {
            FileName = commandInterpreter,
            Arguments = $"/k {QuoteArgument(_serverExecutable)}",
            WorkingDirectory = _serverDirectory,
            UseShellExecute = true
        });

        for (var second = 1; second <= 20; second++)
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            if (!IsServerListening())
                continue;

            AppendLog($"Server is ready after {second} second(s).");
            RefreshStatus();
            return true;
        }

        RefreshStatus();
        ShowError($"The server did not listen on port {ServerPort} within 20 seconds. Check the server console or log.");
        return false;
    }

    private void StartClient()
    {
        if (!File.Exists(_clientLauncherExecutable))
        {
            ShowError($"Missing client launcher:\n{_clientLauncherExecutable}");
            return;
        }

        if (!HasLocalLoginRouting())
        {
            ShowError("Local login routing is not ready. Click 配置本地登录路由 first, or use 启动本地双端.");
            return;
        }

        if (!IsServerListening())
        {
            ShowError("The server is not listening yet. Start it first, then launch the client.");
            return;
        }

        // SnowLauncher writes status through the console and its loader
        // expects a real console handle.  Start it through a normal cmd.exe
        // instead of inheriting this WinForms process's non-console context.
        var commandInterpreter = Environment.GetEnvironmentVariable("ComSpec") ?? Path.Combine(Environment.SystemDirectory, "cmd.exe");
        Process.Start(new ProcessStartInfo
        {
            FileName = commandInterpreter,
            Arguments = $"/c {QuoteArgument(_clientLauncherExecutable)}",
            WorkingDirectory = _serverDirectory,
            UseShellExecute = true
        });
        AppendLog("Client launch request sent.");
    }

    private void OpenServerLog()
    {
        var logPath = Path.Combine(_serverDirectory, "Config", "Logs", "Server.log");
        var target = File.Exists(logPath) ? logPath : Path.GetDirectoryName(logPath)!;
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = File.Exists(logPath) ? $"/select,{QuoteArgument(logPath)}" : QuoteArgument(target),
            UseShellExecute = true
        });
    }

    private void OpenGachaPreset()
    {
        const string message = "服务端已经能返回完整卡池列表，但客户端用于展示的分组表是加密/编译格式。\n\n" +
                               "在复原原始格式前，部署猜测或明文表补丁会导致客户端停在登录完成界面。为避免破坏正常登录，本版本只保留卡池数据的服务端支持，暂不部署展示补丁。";
        AppendLog("Gacha display deployment is disabled until the encrypted client table format is reproduced.");
        MessageBox.Show(this, message, "卡池显示暂不可部署", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async Task RunExclusiveAsync(Func<Task> action)
    {
        if (_busy)
            return;

        _busy = true;
        SetActionEnabled(false);
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            AppendLog(exception.ToString());
            ShowError(exception.Message);
        }
        finally
        {
            _busy = false;
            SetActionEnabled(true);
            RefreshStatus();
        }
    }

    private void RefreshStatus()
    {
        var routingReady = HasLocalLoginRouting();
        _routingStatus.Text = routingReady
            ? "● 本地登录路由：已就绪（VPN / 系统代理保持启用）"
            : "● 本地登录路由：需要配置";
        _routingStatus.ForeColor = routingReady ? Color.FromArgb(101, 209, 142) : Color.FromArgb(246, 190, 83);

        var serverReady = IsServerListening();
        _serverStatus.Text = serverReady
            ? $"● 本地服务端：运行中（127.0.0.1:{ServerPort}）"
            : $"● 本地服务端：未运行（端口 {ServerPort}）";
        _serverStatus.ForeColor = serverReady ? Color.FromArgb(101, 209, 142) : Color.FromArgb(205, 215, 232);
    }

    private bool HasLocalLoginRouting()
    {
        try
        {
            var hostsPath = Path.Combine(Environment.SystemDirectory, "drivers", "etc", "hosts");
            if (!File.Exists(hostsPath))
                return false;

            var content = File.ReadAllText(hostsPath);
            return LocalLoginHosts.All(host => Regex.IsMatch(
                content,
                $"(?im)^\\s*127\\.0\\.0\\.1\\s+{Regex.Escape(host)}(\\s|$)"));
        }
        catch
        {
            return false;
        }
    }

    private static bool IsServerListening() =>
        IPGlobalProperties.GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Any(endpoint => endpoint.Port == ServerPort &&
                             (endpoint.Address.Equals(IPAddress.Any) || endpoint.Address.Equals(IPAddress.Loopback)));

    private void ConfigureButton(Button button, string text, Color backColor)
    {
        button.Text = text;
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Padding = new Padding(13, 7, 13, 7);
        button.Margin = new Padding(0, 0, 10, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(90, 110, 145);
        button.BackColor = backColor;
        button.ForeColor = Color.White;
        button.Font = new Font("Microsoft YaHei UI", 9, FontStyle.Regular);
        button.Cursor = Cursors.Hand;
    }

    private void SetActionEnabled(bool enabled)
    {
        _prepareButton.Enabled = enabled;
        _startServerButton.Enabled = enabled;
        _startClientButton.Enabled = enabled;
        _startAllButton.Enabled = enabled;
        _gachaPresetButton.Enabled = enabled;
    }

    private void AppendLog(string message)
    {
        var normalized = message.Replace("\r\n", "\n").Replace('\r', '\n');
        foreach (var line in normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            _activityLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");
    }

    private void ShowError(string message) =>
        MessageBox.Show(this, message, "Snowbreak Local Launcher", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static string QuoteArgument(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private static string FindRootDirectory()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current != null; current = current.Parent)
        {
            var serverPath = Path.Combine(current.FullName, "Server", "MikuSB.exe");
            if (File.Exists(serverPath))
                return current.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate the Snowbreak local workspace containing Server\\MikuSB.exe.");
    }
}

internal sealed class GachaPresetForm : Form
{
    // This follows the game's normal patch naming convention.  The previous
    // OfflineGachaDisplay_999_P name mounted below the shipped gacha table and
    // therefore lost the file override despite containing the selected rows.
    private const string PatchName = "Patch_OfflineGachaDisplay-WindowsNoEditor_999_P.pak";
    private const int PoolSlotCount = 4;

    private readonly string _rootDirectory;
    private readonly string _resourceDirectory;
    private readonly string _patchDirectory;
    private readonly string _clientPaksDirectory;
    private readonly string _gameExecutable;
    private readonly string _repakExecutable;
    private readonly string _settingsPath;
    private readonly List<GachaPoolOption> _options;
    private readonly List<GachaPeriod> _periods;
    private readonly int[] _slotIds = new int[PoolSlotCount];
    private readonly Button[] _slotButtons = new Button[PoolSlotCount];
    private readonly ListView _poolList = new();
    private readonly TextBox _searchBox = new();
    private readonly ComboBox _typeFilter = new();
    private readonly RichTextBox _detailBox = new();
    private readonly Label _status = new();
    private readonly HashSet<string> _selectedPeriodKeys = new(StringComparer.Ordinal);
    private GachaPeriod? _currentPeriod;
    private bool _updatingCatalog;

    public IReadOnlyList<string> AppliedPoolLabels { get; private set; } = [];

    public GachaPresetForm(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
        _resourceDirectory = Path.Combine(_rootDirectory, "Server", "Resources");
        _patchDirectory = Path.Combine(_rootDirectory, "Patches", "OfflineGachaDisplay");
        _clientPaksDirectory = Path.Combine(_rootDirectory, "data", "game", "Game", "Content", "Paks");
        _gameExecutable = Path.Combine(_rootDirectory, "data", "game", "Game", "Binaries", "Win64", "Game.exe");
        _repakExecutable = Path.Combine(_rootDirectory, "Tools", "repak", "repak.exe");
        _settingsPath = Path.Combine(_patchDirectory, "GachaPreset.json");
        _options = LoadOptions();
        _periods = LoadPeriods();

        Text = "卡池多期浏览与编排 · 本地离线启动器";
        MinimumSize = new Size(1180, 720);
        Size = new Size(1180, 720);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Color.FromArgb(20, 27, 44);
        ForeColor = Color.White;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 20),
            ColumnCount = 1,
            RowCount = 5,
            BackColor = BackColor
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(layout);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "卡池多期浏览与编排（每期包含四个底层池）",
            Font = new Font("Microsoft YaHei UI", 16, FontStyle.Bold),
            ForeColor = Color.FromArgb(157, 131, 255),
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);

        layout.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "左侧每一行都是一个完整活动期，包含标准角色、标准武器、特选角色、特选武器四个底层卡池。可同时勾选任意数量的活动期；常驻池和新手池会始终保留，右侧显示当前浏览期的四池详情。",
            MaximumSize = new Size(1100, 0),
            Font = new Font("Microsoft YaHei UI", 9),
            ForeColor = Color.FromArgb(205, 215, 232),
            Margin = new Padding(0, 0, 0, 16)
        }, 0, 1);

        var browser = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 14)
        };
        browser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        browser.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));

        var catalogPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Color.FromArgb(29, 39, 62),
            Padding = new Padding(12)
        };
        catalogPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        catalogPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var catalogTools = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 8)
        };
        catalogTools.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "检索期别",
            ForeColor = Color.FromArgb(221, 229, 243),
            Font = new Font("Microsoft YaHei UI", 9),
            Margin = new Padding(0, 7, 8, 0)
        });
        _searchBox.Width = 280;
        _searchBox.Font = new Font("Microsoft YaHei UI", 9);
        _searchBox.PlaceholderText = "角色、武器、卡池名称或任一底层 ID";
        _searchBox.TextChanged += (_, _) => RefreshCatalog();
        catalogTools.Controls.Add(_searchBox);
        _typeFilter.Width = 130;
        _typeFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _typeFilter.Font = new Font("Microsoft YaHei UI", 9);
        _typeFilter.Margin = new Padding(10, 0, 0, 0);
        _typeFilter.Items.AddRange(["完整四池活动期"]);
        _typeFilter.SelectedIndex = 0;
        _typeFilter.SelectedIndexChanged += (_, _) => RefreshCatalog();
        catalogTools.Controls.Add(_typeFilter);
        catalogPanel.Controls.Add(catalogTools, 0, 0);

        _poolList.Dock = DockStyle.Fill;
        _poolList.View = View.Details;
        _poolList.FullRowSelect = true;
        _poolList.CheckBoxes = true;
        _poolList.GridLines = true;
        _poolList.HideSelection = false;
        _poolList.MultiSelect = false;
        _poolList.Font = new Font("Microsoft YaHei UI", 9);
        _poolList.BackColor = Color.FromArgb(245, 247, 252);
        _poolList.ForeColor = Color.FromArgb(30, 36, 50);
        _poolList.Columns.Add("完整活动期", 215);
        _poolList.Columns.Add("角色概率提升", 120);
        _poolList.Columns.Add("武器概率提升", 120);
        _poolList.Columns.Add("四池 ID", 105);
        _poolList.Columns.Add("活动期", 130);
        _poolList.SelectedIndexChanged += (_, _) => ShowSelectedPeriod();
        _poolList.ItemCheck += (_, eventArgs) => OnPeriodCheckChanged(eventArgs);
        catalogPanel.Controls.Add(_poolList, 0, 1);
        browser.Controls.Add(catalogPanel, 0, 0);

        var detailPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Color.FromArgb(29, 39, 62),
            Padding = new Padding(14, 12, 14, 12),
            Margin = new Padding(12, 0, 0, 0)
        };
        detailPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        detailPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        detailPanel.Controls.Add(new Label
        {
            AutoSize = true,
            Text = "当前浏览期的四池预览（勾选状态决定是否写入）",
            Font = new Font("Microsoft YaHei UI", 10, FontStyle.Bold),
            ForeColor = Color.FromArgb(221, 229, 243),
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);
        var slots = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 12)
        };
        slots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        slots.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (var index = 0; index < PoolSlotCount; index++)
        {
            var button = new Button
            {
                Dock = DockStyle.Fill,
                Height = 58,
                FlatStyle = FlatStyle.Flat,
                FlatAppearance = { BorderColor = Color.FromArgb(106, 121, 160) },
                BackColor = Color.FromArgb(48, 59, 89),
                ForeColor = Color.White,
                Font = new Font("Microsoft YaHei UI", 9),
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(index % 2 == 0 ? 0 : 6, index < 2 ? 0 : 6, index % 2 == 0 ? 6 : 0, 0),
                Cursor = Cursors.Default,
                Enabled = false
            };
            _slotButtons[index] = button;
            slots.Controls.Add(button, index % 2, index / 2);
        }
        detailPanel.Controls.Add(slots, 0, 1);
        _detailBox.Dock = DockStyle.Fill;
        _detailBox.ReadOnly = true;
        _detailBox.BorderStyle = BorderStyle.FixedSingle;
        _detailBox.BackColor = Color.FromArgb(13, 19, 31);
        _detailBox.ForeColor = Color.FromArgb(216, 226, 243);
        _detailBox.Font = new Font("Microsoft YaHei UI", 9);
        _detailBox.DetectUrls = false;
        _detailBox.Text = "从左侧选中一个完整活动期，即可查看四个底层卡池及各自的保底、概率和消耗。";
        detailPanel.Controls.Add(_detailBox, 0, 2);
        browser.Controls.Add(detailPanel, 1, 0);
        layout.Controls.Add(browser, 0, 2);

        var initialPeriods = GetInitialPeriods();
        foreach (var period in initialPeriods)
            _selectedPeriodKeys.Add(period.Key);
        _currentPeriod = initialPeriods.FirstOrDefault() ?? _periods[0];
        Array.Copy(_currentPeriod.Rows.Select(row => row.Pool.Id).ToArray(), _slotIds, PoolSlotCount);
        UpdateSlotButtons();
        RefreshCatalog();

        _status.AutoSize = true;
        _status.Font = new Font("Microsoft YaHei UI", 9);
        _status.ForeColor = Color.FromArgb(205, 215, 232);
        _status.Text = $"提示：已从完整 gacha 表自动识别 {_periods.Count} 个四池活动期；默认选择最近四期，可继续勾选更多期。";
        _status.Margin = new Padding(0, 0, 0, 12);
        layout.Controls.Add(_status, 0, 3);

        var actions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0)
        };
        var applyButton = new Button();
        ConfigureButton(applyButton, "应用到客户端", Color.FromArgb(88, 70, 166));
        var restoreButton = new Button();
        ConfigureButton(restoreButton, "选择最近四期", Color.FromArgb(58, 69, 93));
        var selectAllButton = new Button();
        ConfigureButton(selectAllButton, "选择全部完整期", Color.FromArgb(58, 69, 93));
        var clearButton = new Button();
        ConfigureButton(clearButton, "清空选择", Color.FromArgb(58, 69, 93));
        var cancelButton = new Button();
        ConfigureButton(cancelButton, "取消", Color.FromArgb(58, 69, 93));
        applyButton.Click += (_, _) => ApplySelection();
        restoreButton.Click += (_, _) => RestorePreferredSelection();
        selectAllButton.Click += (_, _) => SetSelectedPeriods(_periods);
        clearButton.Click += (_, _) => SetSelectedPeriods([]);
        cancelButton.Click += (_, _) => Close();
        actions.Controls.AddRange([applyButton, restoreButton, selectAllButton, clearButton, cancelButton]);
        layout.Controls.Add(actions, 0, 4);
    }

    private List<GachaPoolOption> LoadOptions()
    {
        var gachaPath = Path.Combine(_resourceDirectory, "gacha", "gacha.json");
        var languagePath = Path.Combine(_resourceDirectory, "all_language", "ui", "gacha.json");
        if (!File.Exists(gachaPath) || !File.Exists(languagePath))
            throw new FileNotFoundException("Gacha data or its Chinese text table is missing from Server\\Resources.");

        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var languageDocument = JsonDocument.Parse(File.ReadAllText(languagePath)))
        {
            foreach (var entry in languageDocument.RootElement.EnumerateArray())
            {
                var key = GetString(entry, "Key");
                var name = GetString(entry, "zh_CN");
                if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(name))
                    names[key] = name;
            }
        }

        var options = new List<GachaPoolOption>();
        using (var gachaDocument = JsonDocument.Parse(File.ReadAllText(gachaPath)))
        {
            foreach (var entry in gachaDocument.RootElement.EnumerateArray())
            {
                var id = GetInt32(entry, "ID");
                if (id <= 0)
                    continue;

                var desc = GetString(entry, "Desc");
                var name = names.TryGetValue($"{desc}_name", out var localizedName)
                    ? localizedName
                    : $"未命名卡池 {id}";
                var detailText = FindPrimaryDetail(entry, names);
                var showResource = GetArrayString(entry, "ShowResource", 0);
                var category = showResource.Contains("weapon", StringComparison.OrdinalIgnoreCase)
                    ? "武器池"
                    : showResource.Contains("role", StringComparison.OrdinalIgnoreCase)
                        ? "角色池"
                        : GetTypeLabel(GetInt32(entry, "Type"));
                var mode = GetInt32(entry, "ProtectType") == 1
                    ? "特选 · 100%UP"
                    : "标准 · 大小保底";
                options.Add(new GachaPoolOption(
                    id,
                    GetInt32(entry, "Index"),
                    GetInt32(entry, "Type"),
                    name,
                    category,
                    mode,
                    ExtractHighlightedTarget(detailText),
                    FormatPoolTime(entry),
                    FormatCost(entry),
                    FormatGuarantee(entry),
                    FormatProbability(entry),
                    GetStringArray(entry, "Pool"),
                    GetArrayString(entry, "ShowResource", 1),
                    GetNestedInt(entry, "ShowResource", 8, 1),
                    GetNestedInt(entry, "ShowResource", 8, 2),
                    GetArrayString(entry, "PoolTime", 0),
                    CleanRichText(detailText)));
            }
        }

        if (options.Count < PoolSlotCount)
            throw new InvalidDataException("The gacha table does not contain enough pools to build the four display slots.");

        return options
            .OrderByDescending(option => option.Index)
            .ThenByDescending(option => option.Id)
            .ToList();
    }

    private List<GachaPeriod> LoadPeriods()
    {
        var groupPath = Path.Combine(_resourceDirectory, "gacha", "gacha_group.json");
        var profilePath = Path.Combine(_resourceDirectory, "item", "templates", "profile.json");
        var paintingPath = Path.Combine(_resourceDirectory, "item", "card_skin", "painting.json");
        foreach (var requiredPath in new[] { groupPath, profilePath, paintingPath })
        {
            if (!File.Exists(requiredPath))
                throw new FileNotFoundException($"Required gacha presentation data is missing: {requiredPath}");
        }

        var curatedRowsByGachaId = new Dictionary<int, GachaDisplayRow>();
        using (var groupDocument = JsonDocument.Parse(File.ReadAllText(groupPath)))
        {
            foreach (var entry in groupDocument.RootElement.EnumerateArray())
            {
                var gachaId = GetInt32(entry, "gachaid");
                var pool = _options.FirstOrDefault(option => option.Id == gachaId);
                if (pool is null)
                    continue;
                curatedRowsByGachaId[gachaId] = new GachaDisplayRow(
                    GetInt32(entry, "poolid"),
                    GetString(entry, "poolname"),
                    GetString(entry, "poolicon"),
                    GetString(entry, "poolmark"),
                    pool);
            }
        }

        var groupNames = new Dictionary<(int Detail, int Particular), string>();
        using (var profileDocument = JsonDocument.Parse(File.ReadAllText(profilePath)))
        {
            foreach (var entry in profileDocument.RootElement.EnumerateArray())
            {
                var name = GetString(entry, "I18n");
                if (!name.StartsWith("card.girl", StringComparison.Ordinal) || !name.EndsWith("_suits", StringComparison.Ordinal))
                    continue;
                groupNames[(GetInt32(entry, "Particular"), GetInt32(entry, "Level"))] = name;
            }
        }

        var groupIcons = new Dictionary<int, string>();
        using (var paintingDocument = JsonDocument.Parse(File.ReadAllText(paintingPath)))
        {
            foreach (var entry in paintingDocument.RootElement.EnumerateArray())
            {
                var id = GetInt32(entry, "ID");
                var icon = GetString(entry, "P11");
                if (id > 0 && !string.IsNullOrWhiteSpace(icon))
                    groupIcons[id] = icon;
            }
        }

        var halfPeriods = new List<GachaHalfPeriod>();
        foreach (var role in _options.Where(option => option.Category == "角色池" &&
                                                       (option.Mode.StartsWith("标准", StringComparison.Ordinal) ||
                                                        option.Mode.StartsWith("特选", StringComparison.Ordinal))))
        {
            var weapon = _options.FirstOrDefault(option =>
                option.Id == role.Id + 1 &&
                option.Category == "武器池" &&
                string.Equals(option.Mode, role.Mode, StringComparison.Ordinal) &&
                string.Equals(option.PoolStart, role.PoolStart, StringComparison.Ordinal) &&
                option.Type == role.Type);
            if (weapon is null)
                continue;

            halfPeriods.Add(new GachaHalfPeriod(role, weapon));
        }

        var periods = new List<GachaPeriod>();
        foreach (var group in halfPeriods.GroupBy(pair =>
                     (pair.Role.ResourceName, pair.Weapon.ResourceName, pair.Role.PoolStart, pair.Role.Type)))
        {
            var special = group
                .Where(pair => pair.Role.Mode.StartsWith("特选", StringComparison.Ordinal))
                .OrderByDescending(pair => Math.Max(pair.Role.Index, pair.Weapon.Index))
                .FirstOrDefault();
            var standard = group
                .Where(pair => pair.Role.Mode.StartsWith("标准", StringComparison.Ordinal))
                .OrderByDescending(pair => Math.Max(pair.Role.Index, pair.Weapon.Index))
                .FirstOrDefault();
            if (special is null || standard is null)
                continue;

            var pools = new[] { special.Role, special.Weapon, standard.Role, standard.Weapon };
            var curatedRows = pools
                .Select(pool => curatedRowsByGachaId.GetValueOrDefault(pool.Id))
                .Where(row => row is not null)
                .Cast<GachaDisplayRow>()
                .ToList();

            List<GachaDisplayRow> periodRows;
            if (curatedRows.Count == PoolSlotCount && curatedRows.Select(row => row.PoolName).Distinct(StringComparer.Ordinal).Count() == 1)
            {
                periodRows = pools
                    .Select(pool => curatedRowsByGachaId[pool.Id])
                    .ToList();
            }
            else
            {
                var groupName = groupNames.GetValueOrDefault((special.Role.RoleDetail, special.Role.RoleParticular));
                if (string.IsNullOrWhiteSpace(groupName))
                    groupName = $"offline.gacha.period.{special.Role.Id}";
                var paintingId = 1700000 + special.Role.RoleDetail * 1000 + special.Role.RoleParticular * 10 + 1;
                var groupIcon = groupIcons.GetValueOrDefault(paintingId) ?? string.Empty;
                periodRows = pools
                    .Select((pool, index) => new GachaDisplayRow(index + 1, groupName, groupIcon, string.Empty, pool))
                    .ToList();
            }

            var role = special.Role.HighlightedTarget;
            var weapon = special.Weapon.HighlightedTarget;
            periods.Add(new GachaPeriod(
                $"period:{special.Role.Id}-{special.Weapon.Id}-{standard.Role.Id}-{standard.Weapon.Id}",
                $"{role} · {weapon}",
                role,
                weapon,
                special.Role.TimeRange,
                periodRows));
        }

        if (periods.Count == 0)
            throw new InvalidDataException("The local gacha table does not contain a complete four-pool period.");

        return periods
            .OrderByDescending(period => period.Rows.Max(row => row.Pool.Index))
            .ThenByDescending(period => period.Rows.Max(row => row.Pool.Id))
            .ToList();
    }

    private List<GachaDisplayRow> LoadFixedDisplayRows()
    {
        var groupPath = Path.Combine(_resourceDirectory, "gacha", "gacha_group.json");
        var rows = new List<GachaDisplayRow>();
        using var groupDocument = JsonDocument.Parse(File.ReadAllText(groupPath));
        foreach (var entry in groupDocument.RootElement.EnumerateArray())
        {
            var groupName = GetString(entry, "poolname");
            if (groupName is not ("gacha.gacha4" or "gacha.gacha5"))
                continue;
            var pool = _options.FirstOrDefault(option => option.Id == GetInt32(entry, "gachaid"));
            if (pool is null)
                continue;
            rows.Add(new GachaDisplayRow(
                GetInt32(entry, "poolid"),
                groupName,
                GetString(entry, "poolicon"),
                GetString(entry, "poolmark"),
                pool));
        }
        return rows.OrderBy(row => row.PoolId).ToList();
    }

    private void RefreshCatalog()
    {
        var query = _searchBox.Text.Trim();
        var selectedKey = _currentPeriod?.Key;
        var candidates = _periods.Where(period => MatchesFilter(period, query)).ToList();

        _updatingCatalog = true;
        _poolList.BeginUpdate();
        try
        {
            _poolList.Items.Clear();
            foreach (var period in candidates)
            {
                var item = new ListViewItem(period.Name) { Tag = period };
                item.SubItems.Add(period.RoleTarget);
                item.SubItems.Add(period.WeaponTarget);
                item.SubItems.Add(string.Join(", ", period.Rows.Select(row => row.Pool.Id)));
                item.SubItems.Add(period.TimeRange);
                item.Checked = _selectedPeriodKeys.Contains(period.Key);
                _poolList.Items.Add(item);
                if (period.Key == selectedKey)
                    item.Selected = true;
            }
        }
        finally
        {
            _poolList.EndUpdate();
            _updatingCatalog = false;
        }

        if (_poolList.SelectedItems.Count == 0 && _poolList.Items.Count > 0)
            _poolList.Items[0].Selected = true;
        UpdateSelectionStatus();
    }

    private void OnPeriodCheckChanged(ItemCheckEventArgs eventArgs)
    {
        if (_updatingCatalog || eventArgs.Index < 0 || eventArgs.Index >= _poolList.Items.Count ||
            _poolList.Items[eventArgs.Index].Tag is not GachaPeriod period)
        {
            return;
        }

        if (eventArgs.NewValue == CheckState.Checked)
            _selectedPeriodKeys.Add(period.Key);
        else
            _selectedPeriodKeys.Remove(period.Key);

        BeginInvoke((Action)UpdateSelectionStatus);
    }

    private void UpdateSelectionStatus()
    {
        var count = _selectedPeriodKeys.Count;
        _status.Text = count == 0
            ? "尚未选择活动期；至少勾选一期后才能应用。"
            : $"已选择 {count} 期，共 {count * PoolSlotCount} 个底层卡池。可勾选超过四期，用于验证客户端实际展示上限。";
        _status.ForeColor = count == 0
            ? Color.FromArgb(246, 120, 120)
            : Color.FromArgb(205, 215, 232);
    }

    private bool MatchesFilter(GachaPeriod period, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        var searchable = string.Join(" ", period.Name, period.RoleTarget, period.WeaponTarget, period.Key,
            string.Join(" ", period.Rows.Select(row => string.Join(" ", row.Pool.Id, row.Pool.Name, row.Pool.HighlightedTarget, row.Pool.ResourceName))));
        return searchable.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private void ShowSelectedPeriod()
    {
        if (_poolList.SelectedItems.Count == 0 || _poolList.SelectedItems[0].Tag is not GachaPeriod period)
            return;

        _currentPeriod = period;
        Array.Copy(period.Rows.Select(row => row.Pool.Id).ToArray(), _slotIds, PoolSlotCount);
        UpdateSlotButtons();
        UpdateSelectionStatus();

        var detailRows = period.Rows.Select((row, index) => string.Join(Environment.NewLine,
        [
            $"【槽位 {index + 1}】{row.Pool.Mode} · {row.Pool.Category}",
            $"卡池：{row.Pool.Name}（ID {row.Pool.Id}，排序 {row.Pool.Index}）",
            $"概率提升：{row.Pool.HighlightedTarget}",
            $"消耗：{row.Pool.Cost}",
            $"保底：{row.Pool.Guarantee}；五星概率：{row.Pool.Probability}",
            $"活动期：{row.Pool.TimeRange}",
            $"奖池表：{row.Pool.PoolNames}",
            $"说明：{row.Pool.DetailText}"
        ]));
        _detailBox.Text = string.Join(Environment.NewLine + Environment.NewLine, new[]
        {
            $"本期：{period.Name}",
            "本地服状态：四池均已延长开放期。以下四条会作为一个完整分组写入客户端：",
            string.Join(Environment.NewLine + Environment.NewLine, detailRows)
        });
    }

    private void UpdateSlotButtons()
    {
        for (var index = 0; index < PoolSlotCount; index++)
        {
            var option = _options.FirstOrDefault(candidate => candidate.Id == _slotIds[index]);
            _slotButtons[index].Text = option is null
                ? $"槽位 {index + 1}\n（未选择）"
                : $"槽位 {index + 1}\n{option.Mode} · {option.Category}\n{option.Name}（ID {option.Id}）";
            _slotButtons[index].BackColor = option is null
                ? Color.FromArgb(86, 62, 75)
                : Color.FromArgb(48, 59, 89);
        }
    }

    private static string FindPrimaryDetail(JsonElement element, IReadOnlyDictionary<string, string> localizedText)
    {
        if (!element.TryGetProperty("detail", out var detail))
            return "本地数据未提供卡池说明。";

        var values = new List<string>();
        CollectStrings(detail, values);
        foreach (var key in values.Where(key => key.EndsWith("detaildes11", StringComparison.Ordinal)))
        {
            if (localizedText.TryGetValue(key, out var detailText))
                return detailText;
        }
        foreach (var key in values)
        {
            if (localizedText.TryGetValue(key, out var detailText) && detailText.Contains("限定", StringComparison.Ordinal))
                return detailText;
        }

        return "本地数据未提供卡池说明。";
    }

    private static void CollectStrings(JsonElement element, ICollection<string> output)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                output.Add(element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Array:
                foreach (var child in element.EnumerateArray())
                    CollectStrings(child, output);
                break;
        }
    }

    private static string ExtractHighlightedTarget(string detailText)
    {
        var match = Regex.Match(detailText, "限定[^【]*【(?<target>[^】]+)】");
        return match.Success ? match.Groups["target"].Value : "（见卡池说明）";
    }

    private static string FormatPoolTime(JsonElement element)
    {
        if (!element.TryGetProperty("PoolTime", out var poolTime) || poolTime.ValueKind != JsonValueKind.Array)
            return "未提供";
        var times = poolTime.EnumerateArray().Select(value => FormatCompactTime(value.ToString())).ToArray();
        return times.Length >= 2 ? $"{times[0]} ～ {times[1]}" : "未提供";
    }

    private static string FormatCompactTime(string value) =>
        DateTime.TryParseExact(value, "yyyyMMddHHmm", null, System.Globalization.DateTimeStyles.None, out var time)
            ? time.ToString("yyyy-MM-dd HH:mm")
            : value;

    private static string FormatCost(JsonElement element)
    {
        var one = GetNestedInt(element, "CastOne", 0, 2);
        var oneCount = GetNestedInt(element, "CastOne", 0, 4);
        var tenCount = GetNestedInt(element, "CastTen", 0, 4);
        if (oneCount <= 0)
            return "不消耗卡券 / 本地数据未提供";
        var voucher = one switch
        {
            1 => "角色共鸣券",
            2 => "武器共鸣券",
            _ => "共鸣券"
        };
        return $"{voucher}：单抽 ×{oneCount}，十连 ×{(tenCount > 0 ? tenCount : oneCount * 10)}";
    }

    private static string FormatGuarantee(JsonElement element)
    {
        var protect = GetNestedInt(element, "ProtectNum", 0);
        var up = GetNestedInt(element, "UpNum", 0);
        return protect <= 0
            ? "未提供"
            : up > 0 && up != protect
                ? $"{protect} 抽保底；概率提升 {up} 抽"
                : $"{protect} 抽保底";
    }

    private static string FormatProbability(JsonElement element)
    {
        if (!element.TryGetProperty("UIPro", out var probabilities) || probabilities.ValueKind != JsonValueKind.Object ||
            !probabilities.TryGetProperty("5", out var fiveStar) || fiveStar.ValueKind != JsonValueKind.Array)
            return "未提供";
        var values = fiveStar.EnumerateArray().Select(value => value.ToString()).ToArray();
        return values.Length >= 2 ? $"单抽 {values[0]}% / 十连 {values[1]}%" : $"{string.Join("% / ", values)}%";
    }

    private static int GetNestedInt(JsonElement element, string propertyName, int firstIndex, int? secondIndex = null)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            return 0;
        var first = property.EnumerateArray().ElementAtOrDefault(firstIndex);
        if (first.ValueKind == JsonValueKind.Undefined)
            return 0;
        var value = first;
        if (secondIndex.HasValue)
        {
            if (first.ValueKind != JsonValueKind.Array)
                return 0;
            value = first.EnumerateArray().ElementAtOrDefault(secondIndex.Value);
        }
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsed)
                ? parsed
                : 0;
    }

    private static string GetArrayString(JsonElement element, string propertyName, int index)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            return "未提供";
        var value = property.EnumerateArray().ElementAtOrDefault(index);
        return value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? "未提供" : value.ToString();
    }

    private static string GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            return "未提供";
        var values = property.EnumerateArray().Select(value => value.ToString()).Where(value => !string.IsNullOrWhiteSpace(value));
        return string.Join("、", values);
    }

    private static string CleanRichText(string value) =>
        Regex.Replace(value, "<[^>]+>", string.Empty).Replace("%%", "%").Replace("\\n", Environment.NewLine).Trim();

    private static string GetTypeLabel(int type) => type switch
    {
        0 => "新手",
        1 => "常驻",
        2 => "角色/武器",
        3 => "特殊",
        4 => "定向",
        5 => "特别",
        6 => "定向",
        _ => $"类型 {type}"
    };

    private List<GachaPeriod> GetInitialPeriods()
    {
        var saved = ReadSavedSettings();
        if (saved?.SelectedPeriodKeys is { Length: > 0 } savedKeys)
        {
            var selected = _periods.Where(period =>
                savedKeys.Contains(period.Key, StringComparer.Ordinal) ||
                savedKeys.Contains(period.Rows[0].PoolName, StringComparer.Ordinal)).ToList();
            if (selected.Count > 0)
                return selected;
        }

        if (!string.IsNullOrWhiteSpace(saved?.SelectedPeriodKey))
        {
            var savedPeriod = _periods.FirstOrDefault(period =>
                string.Equals(period.Key, saved.SelectedPeriodKey, StringComparison.Ordinal) ||
                string.Equals(period.Rows[0].PoolName, saved.SelectedPeriodKey, StringComparison.Ordinal));
            if (savedPeriod is not null)
                return ExpandLegacySelection(savedPeriod);
        }

        // Compatibility with the first GUI build, which stored four raw IDs.
        // Only accept it when all four form one actual period; arbitrary mixes
        // deliberately fall back to the complete recommended period.
        if (saved?.SelectedGachaIds is { Length: PoolSlotCount } legacyIds)
        {
            var legacyPeriod = _periods.FirstOrDefault(period =>
                period.Rows.Select(row => row.Pool.Id).OrderBy(id => id)
                    .SequenceEqual(legacyIds.OrderBy(id => id)));
            if (legacyPeriod is not null)
                return ExpandLegacySelection(legacyPeriod);
        }

        return _periods.Take(Math.Min(4, _periods.Count)).ToList();
    }

    private List<GachaPeriod> ExpandLegacySelection(GachaPeriod preferredPeriod) =>
        new[] { preferredPeriod }
            .Concat(_periods.Where(period => !string.Equals(period.Key, preferredPeriod.Key, StringComparison.Ordinal)))
            .Take(Math.Min(4, _periods.Count))
            .ToList();

    private GachaPresetSettings? ReadSavedSettings()
    {
        if (!File.Exists(_settingsPath))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GachaPresetSettings>(File.ReadAllText(_settingsPath));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private void RestorePreferredSelection()
    {
        SetSelectedPeriods(_periods.Take(Math.Min(4, _periods.Count)));
    }

    private void SetSelectedPeriods(IEnumerable<GachaPeriod> periods)
    {
        var selected = periods.ToList();
        _selectedPeriodKeys.Clear();
        foreach (var period in selected)
            _selectedPeriodKeys.Add(period.Key);

        _currentPeriod = selected.FirstOrDefault() ?? _currentPeriod ?? _periods[0];
        Array.Copy(_currentPeriod.Rows.Select(row => row.Pool.Id).ToArray(), _slotIds, PoolSlotCount);
        UpdateSlotButtons();
        RefreshCatalog();
    }

    private void ApplySelection()
    {
        var selectedPeriods = _periods.Where(period => _selectedPeriodKeys.Contains(period.Key)).ToList();
        if (selectedPeriods.Count == 0)
        {
            ShowError("请至少勾选一个包含四个底层卡池的完整活动期。");
            return;
        }

        if (IsTargetGameRunning())
        {
            ShowError("检测到本地 Game.exe 仍在运行。请完全退出游戏客户端后再应用卡池编排。");
            return;
        }

        try
        {
            _status.Text = $"正在生成 {selectedPeriods.Count} 期、{selectedPeriods.Count * PoolSlotCount} 池的显示补丁…";
            _status.ForeColor = Color.FromArgb(246, 190, 83);
            Cursor = Cursors.WaitCursor;
            BuildAndInstallPatch(selectedPeriods);
            SaveSelection(selectedPeriods);
            AppliedPoolLabels = selectedPeriods.Select(period => period.Name).ToArray();
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            _status.Text = $"应用失败：{exception.Message}";
            _status.ForeColor = Color.FromArgb(246, 120, 120);
            ShowError(exception.Message);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void BuildAndInstallPatch(IReadOnlyList<GachaPeriod> periods)
    {
        var gachaPath = Path.Combine(_resourceDirectory, "gacha", "gacha.json");
        foreach (var requiredPath in new[] { gachaPath, _repakExecutable, _clientPaksDirectory, _gameExecutable })
        {
            if (!File.Exists(requiredPath) && !Directory.Exists(requiredPath))
                throw new FileNotFoundException($"Required local file or directory is missing: {requiredPath}");
        }

        Directory.CreateDirectory(_patchDirectory);
        var buildId = DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
        var stagingDirectory = Path.Combine(_patchDirectory, $"gui-staging-{buildId}-{Guid.NewGuid():N}");
        // Safety rollback: overriding the live Settings/gacha table interrupts
        // the client's Lua bootstrap before login completes. Keep this patch at
        // the inert analysis path until the original table format is reproduced.
        var csvDirectory = Path.Combine(stagingDirectory, "Game", "Content", "gacha");
        var temporaryPak = Path.Combine(_patchDirectory, $"gui-build-{buildId}-{Guid.NewGuid():N}.pak");
        var installedPak = Path.Combine(_clientPaksDirectory, PatchName);
        var archivedPak = Path.Combine(_patchDirectory, "backups", $"{Path.GetFileNameWithoutExtension(PatchName)}-{buildId}.pak");
        var artifactPak = Path.Combine(_patchDirectory, PatchName);

        try
        {
            Directory.CreateDirectory(csvDirectory);
            var rows = new List<string> { "poolid\tpoolname\tpoolicon\tpoolmark\tgachaid" };
            var poolId = 1;
            var displayRows = periods.SelectMany(period => period.Rows).Concat(LoadFixedDisplayRows());
            foreach (var row in displayRows)
                rows.Add($"{poolId++}\t{row.PoolName}\t{row.PoolIcon}\t{row.PoolMark}\t{row.Pool.Id}");
            File.WriteAllLines(Path.Combine(csvDirectory, "gacha_group.txt"), rows, new UTF8Encoding(true));

            RunRepak("pack", stagingDirectory, temporaryPak, "--mount-point", "../../../", "--version", "V8B");
            var pakEntries = RunRepak("list", temporaryPak);
            if (!pakEntries.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                .Contains("Game/Content/gacha/gacha_group.txt", StringComparer.Ordinal))
            {
                throw new InvalidDataException("The generated patch does not contain gacha_group.txt at the expected mount path.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(archivedPak)!);
            if (File.Exists(installedPak))
                File.Copy(installedPak, archivedPak, overwrite: false);

            File.Copy(temporaryPak, artifactPak, overwrite: true);
            try
            {
                File.Copy(temporaryPak, installedPak, overwrite: true);
            }
            catch
            {
                if (File.Exists(archivedPak))
                    File.Copy(archivedPak, installedPak, overwrite: true);
                throw;
            }
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
            if (File.Exists(temporaryPak))
                File.Delete(temporaryPak);
        }
    }

    private string RunRepak(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _repakExecutable,
            WorkingDirectory = _patchDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start repak.exe.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            var diagnostic = string.Join(Environment.NewLine, new[] { standardOutput.Trim(), standardError.Trim() }
                .Where(text => !string.IsNullOrWhiteSpace(text)));
            throw new InvalidOperationException($"repak failed with exit code {process.ExitCode}.{Environment.NewLine}{diagnostic}".Trim());
        }

        return standardOutput;
    }

    private void SaveSelection(IReadOnlyList<GachaPeriod> periods)
    {
        Directory.CreateDirectory(_patchDirectory);
        var settings = new GachaPresetSettings(
            periods.Select(period => period.Key).ToArray(),
            periods.FirstOrDefault()?.Key,
            periods.SelectMany(period => period.Rows).Select(row => row.Pool.Id).ToArray());
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(true));
    }

    private bool IsTargetGameRunning()
    {
        foreach (var process in Process.GetProcessesByName("Game"))
        {
            try
            {
                var executablePath = process.MainModule?.FileName;
                if (string.Equals(executablePath, _gameExecutable, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch
            {
                // If Windows denies inspection of a Game.exe process, do not risk changing a live PAK.
                return true;
            }
            finally
            {
                process.Dispose();
            }
        }

        return false;
    }

    private void ConfigureButton(Button button, string text, Color backColor)
    {
        button.Text = text;
        button.AutoSize = true;
        button.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        button.Padding = new Padding(13, 7, 13, 7);
        button.Margin = new Padding(0, 0, 10, 0);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(90, 110, 145);
        button.BackColor = backColor;
        button.ForeColor = Color.White;
        button.Font = new Font("Microsoft YaHei UI", 9, FontStyle.Regular);
        button.Cursor = Cursors.Hand;
    }

    private void ShowError(string message) =>
        MessageBox.Show(this, message, "卡池编排", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private static int GetInt32(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return 0;
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
            return number;
        return property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out var parsed)
            ? parsed
            : 0;
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return string.Empty;
        return property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? string.Empty
            : property.ToString();
    }

    private sealed record GachaPresetSettings(
        string[]? SelectedPeriodKeys,
        string? SelectedPeriodKey,
        int[]? SelectedGachaIds);

    private sealed record GachaDisplayRow(
        int PoolId,
        string PoolName,
        string PoolIcon,
        string PoolMark,
        GachaPoolOption Pool);

    private sealed record GachaPeriod(
        string Key,
        string Name,
        string RoleTarget,
        string WeaponTarget,
        string TimeRange,
        IReadOnlyList<GachaDisplayRow> Rows);

    private sealed record GachaHalfPeriod(
        GachaPoolOption Role,
        GachaPoolOption Weapon);

    private sealed record GachaPoolOption(
        int Id,
        int Index,
        int Type,
        string Name,
        string Category,
        string Mode,
        string HighlightedTarget,
        string TimeRange,
        string Cost,
        string Guarantee,
        string Probability,
        string PoolNames,
        string ResourceName,
        int RoleDetail,
        int RoleParticular,
        string PoolStart,
        string DetailText)
    {
        public string GuaranteeShort => Guarantee == "未提供" ? "-" : Guarantee.Replace("抽保底", "抽").Split('；')[0];
        public string DisplayName => $"{Name}  ·  ID {Id}  ·  {Category}  ·  序号 {Index}";
    }
}
