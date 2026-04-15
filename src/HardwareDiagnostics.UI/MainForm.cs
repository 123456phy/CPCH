using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Localization;
using HardwareDiagnostics.Core.Utils;
using HardwareDiagnostics.Hardware;
using HardwareDiagnostics.Monitoring;
using HardwareDiagnostics.System;

namespace HardwareDiagnostics.UI
{
    public partial class MainForm : Form
    {
        private readonly DeviceManager _deviceManager;
        private readonly CustomHardwareScanner _hardwareScanner;
        private readonly DriverManager _driverManager;
        private readonly VCRuntimeManager _vcRuntimeManager;
        private readonly DismManager _dismManager;
        private readonly ErrorParser _errorParser;
        private readonly AppCrashMonitor _appCrashMonitor;
        private readonly BSODDetector _bsodDetector;

        private TabControl _mainTabControl;
        private Label _authorLabel;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _memoryStatusLabel;
        private ToolStripStatusLabel _statusLabel;

        public MainForm()
        {
            _deviceManager = new DeviceManager();
            _hardwareScanner = new CustomHardwareScanner();
            _driverManager = new DriverManager();
            _vcRuntimeManager = new VCRuntimeManager();
            _dismManager = new DismManager();
            _errorParser = new ErrorParser();
            _appCrashMonitor = new AppCrashMonitor();
            _bsodDetector = new BSODDetector();

            InitializeComponent();
            SetupUI();
            ApplyLanguage();

            // 启动内存优化
            MemoryOptimizer.StartMonitoring();
        }

        private void InitializeComponent()
        {
            SuspendLayout();

            // 设置窗体属性
            Text = LanguageManager.GetString("AppTitle");
            Size = new Size(1200, 800);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei", 9F, FontStyle.Regular, GraphicsUnit.Point);

            ResumeLayout(false);
        }

        private void SetupUI()
        {
            // 创建主布局
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));

            // 作者信息标签
            _authorLabel = new Label
            {
                Text = LanguageManager.GetString("AuthorCredit"),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei", 12F, FontStyle.Bold, GraphicsUnit.Point),
                ForeColor = Color.DarkBlue,
                BackColor = Color.LightBlue
            };
            mainLayout.Controls.Add(_authorLabel, 0, 0);

            // 创建标签页控件
            _mainTabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // 添加各个功能标签页
            _mainTabControl.TabPages.Add(CreateHardwareTab());
            _mainTabControl.TabPages.Add(CreateSystemTab());
            _mainTabControl.TabPages.Add(CreateMonitoringTab());
            _mainTabControl.TabPages.Add(CreateToolsTab());

            mainLayout.Controls.Add(_mainTabControl, 0, 1);

            // 状态栏
            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("就绪");
            _memoryStatusLabel = new ToolStripStatusLabel("内存: 0 MB");
            _statusStrip.Items.AddRange(new ToolStripItem[] { _statusLabel, new ToolStripStatusLabel(" | "), _memoryStatusLabel });
            mainLayout.Controls.Add(_statusStrip, 0, 2);

            Controls.Add(mainLayout);

            // 设置定时器更新状态
            var timer = new Timer { Interval = 5000 };
            timer.Tick += (s, e) => UpdateStatus();
            timer.Start();
        }

        private TabPage CreateHardwareTab()
        {
            var page = new TabPage(LanguageManager.GetString("MenuHardware"));

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical
            };

            // 左侧：硬件列表
            var leftPanel = new Panel { Dock = DockStyle.Fill };
            var leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var btnScan = new Button
            {
                Text = LanguageManager.GetString("BtnScan"),
                Width = 100,
                Height = 30
            };
            btnScan.Click += async (s, e) => await ScanHardwareAsync();

            var btnRefresh = new Button
            {
                Text = LanguageManager.GetString("BtnRefresh"),
                Width = 100,
                Height = 30
            };
            btnRefresh.Click += async (s, e) => await RefreshHardwareAsync();

            btnPanel.Controls.AddRange(new Control[] { btnScan, btnRefresh });
            leftLayout.Controls.Add(btnPanel, 0, 0);

            var hardwareTree = new TreeView
            {
                Dock = DockStyle.Fill,
                Name = "hardwareTree"
            };
            leftLayout.Controls.Add(hardwareTree, 0, 1);
            leftPanel.Controls.Add(leftLayout);

            splitContainer.Panel1.Controls.Add(leftPanel);

            // 右侧：详细信息
            var rightPanel = new Panel { Dock = DockStyle.Fill };
            var detailsText = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                Name = "hardwareDetails"
            };
            rightPanel.Controls.Add(detailsText);

            splitContainer.Panel2.Controls.Add(rightPanel);

            // 硬件树节点选择事件
            hardwareTree.AfterSelect += (s, e) => ShowHardwareDetails(e.Node, detailsText);

            page.Controls.Add(splitContainer);
            return page;
        }

        private TabPage CreateSystemTab()
        {
            var page = new TabPage(LanguageManager.GetString("MenuSystem"));

            var tabControl = new TabControl { Dock = DockStyle.Fill };

            // VC++运行库标签页
            var vcPage = new TabPage(LanguageManager.GetString("SystemVCRuntime"));
            var vcLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            vcLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            vcLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var vcBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var btnScanVC = new Button
            {
                Text = LanguageManager.GetString("BtnScan"),
                Width = 100,
                Height = 30
            };
            btnScanVC.Click += async (s, e) => await ScanVCRuntimesAsync(vcLayout);

            var btnInstallAll = new Button
            {
                Text = "安装全部缺失",
                Width = 120,
                Height = 30
            };
            btnInstallAll.Click += async (s, e) => await InstallAllVCRuntimesAsync(vcLayout);

            vcBtnPanel.Controls.AddRange(new Control[] { btnScanVC, btnInstallAll });
            vcLayout.Controls.Add(vcBtnPanel, 0, 0);

            var vcListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Name = "vcListView"
            };
            vcListView.Columns.Add("运行库", 300);
            vcListView.Columns.Add("版本", 150);
            vcListView.Columns.Add("架构", 80);
            vcListView.Columns.Add("状态", 100);
            vcListView.Columns.Add("操作", 100);

            vcLayout.Controls.Add(vcListView, 0, 1);
            vcPage.Controls.Add(vcLayout);
            tabControl.TabPages.Add(vcPage);

            // DISM工具标签页
            var dismPage = new TabPage(LanguageManager.GetString("SystemDism"));
            var dismLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            dismLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            dismLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            dismLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));

            var dismBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var btnScanHealth = new Button
            {
                Text = "扫描健康",
                Width = 100,
                Height = 30
            };
            btnScanHealth.Click += async (s, e) => await RunDismCommandAsync("/Online /Cleanup-Image /ScanHealth", dismLayout);

            var btnCheckHealth = new Button
            {
                Text = "检查健康",
                Width = 100,
                Height = 30
            };
            btnCheckHealth.Click += async (s, e) => await RunDismCommandAsync("/Online /Cleanup-Image /CheckHealth", dismLayout);

            var btnRestoreHealth = new Button
            {
                Text = "修复系统",
                Width = 100,
                Height = 30
            };
            btnRestoreHealth.Click += async (s, e) => await RunDismCommandAsync("/Online /Cleanup-Image /RestoreHealth", dismLayout);

            var btnCleanup = new Button
            {
                Text = "清理组件",
                Width = 100,
                Height = 30
            };
            btnCleanup.Click += async (s, e) => await RunDismCommandAsync("/Online /Cleanup-Image /StartComponentCleanup", dismLayout);

            dismBtnPanel.Controls.AddRange(new Control[] { btnScanHealth, btnCheckHealth, btnRestoreHealth, btnCleanup });
            dismLayout.Controls.Add(dismBtnPanel, 0, 0);

            var dismOutput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                Name = "dismOutput"
            };
            dismLayout.Controls.Add(dismOutput, 0, 1);

            var dismProgress = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Marquee,
                Visible = false,
                Name = "dismProgress"
            };
            dismLayout.Controls.Add(dismProgress, 0, 2);

            dismPage.Controls.Add(dismLayout);
            tabControl.TabPages.Add(dismPage);

            // 错误解析标签页
            var errorPage = new TabPage(LanguageManager.GetString("SystemErrorParser"));
            var errorLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            errorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            errorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            errorLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var errorInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Name = "errorInput"
            };
            errorLayout.Controls.Add(errorInput, 0, 0);

            var errorBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var btnAnalyze = new Button
            {
                Text = LanguageManager.GetString("BtnAnalyze"),
                Width = 100,
                Height = 30
            };
            btnAnalyze.Click += (s, e) => AnalyzeError(errorInput.Text, errorLayout);

            var btnClear = new Button
            {
                Text = LanguageManager.GetString("BtnClear"),
                Width = 100,
                Height = 30
            };
            btnClear.Click += (s, e) =>
            {
                errorInput.Clear();
                var output = errorLayout.Controls.Find("errorOutput", true).FirstOrDefault() as TextBox;
                output?.Clear();
            };

            errorBtnPanel.Controls.AddRange(new Control[] { btnAnalyze, btnClear });
            errorLayout.Controls.Add(errorBtnPanel, 0, 1);

            var errorOutput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                Name = "errorOutput"
            };
            errorLayout.Controls.Add(errorOutput, 0, 2);

            errorPage.Controls.Add(errorLayout);
            tabControl.TabPages.Add(errorPage);

            page.Controls.Add(tabControl);
            return page;
        }

        private TabPage CreateMonitoringTab()
        {
            var page = new TabPage(LanguageManager.GetString("MenuMonitoring"));

            var tabControl = new TabControl { Dock = DockStyle.Fill };

            // 应用崩溃监控标签页
            var crashPage = new TabPage(LanguageManager.GetString("MonitorAppCrash"));
            var crashLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            crashLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            crashLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            crashLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));

            // 拖放区域
            var dropPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.LightGray,
                BorderStyle = BorderStyle.FixedSingle,
                AllowDrop = true
            };
            var dropLabel = new Label
            {
                Text = "拖放应用程序快捷方式或EXE文件到此处开始监控",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei", 10F)
            };
            dropPanel.Controls.Add(dropLabel);

            dropPanel.DragEnter += (s, e) =>
            {
                if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                {
                    e.Effect = DragDropEffects.Copy;
                }
            };

            dropPanel.DragDrop += (s, e) =>
            {
                if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
                {
                    foreach (var file in files)
                    {
                        if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                        {
                            StartMonitoringApp(file, crashLayout);
                        }
                    }
                }
            };

            // 浏览按钮
            var browsePanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var btnBrowse = new Button
            {
                Text = LanguageManager.GetString("BtnBrowse"),
                Width = 100,
                Height = 30
            };
            btnBrowse.Click += (s, e) =>
            {
                using var dialog = new OpenFileDialog
                {
                    Filter = "可执行文件|*.exe|快捷方式|*.lnk|所有文件|*.*",
                    Title = "选择要监控的应用程序"
                };
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    StartMonitoringApp(dialog.FileName, crashLayout);
                }
            };

            var btnStartMonitor = new Button
            {
                Text = LanguageManager.GetString("BtnStart"),
                Width = 100,
                Height = 30
            };
            btnStartMonitor.Click += (s, e) =>
            {
                _appCrashMonitor.StartMonitoring();
                UpdateStatus("应用崩溃监控已启动");
            };

            var btnStopMonitor = new Button
            {
                Text = LanguageManager.GetString("BtnStop"),
                Width = 100,
                Height = 30
            };
            btnStopMonitor.Click += (s, e) =>
            {
                _appCrashMonitor.StopMonitoring();
                UpdateStatus("应用崩溃监控已停止");
            };

            browsePanel.Controls.AddRange(new Control[] { btnBrowse, btnStartMonitor, btnStopMonitor });

            var topPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 2,
                ColumnCount = 1
            };
            topPanel.Controls.Add(dropPanel, 0, 0);
            topPanel.Controls.Add(browsePanel, 0, 1);
            crashLayout.Controls.Add(topPanel, 0, 0);

            // 监控列表
            var monitorList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Name = "monitorList"
            };
            monitorList.Columns.Add("进程ID", 80);
            monitorList.Columns.Add("进程名称", 150);
            monitorList.Columns.Add("可执行路径", 400);
            monitorList.Columns.Add("启动时间", 150);
            monitorList.Columns.Add("状态", 100);

            crashLayout.Controls.Add(monitorList, 0, 1);

            // 崩溃报告
            var crashReport = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                Name = "crashReport"
            };
            crashLayout.Controls.Add(crashReport, 0, 2);

            // 崩溃事件处理
            _appCrashMonitor.CrashDetected += (s, e) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        ShowCrashReport(e, crashReport);
                        UpdateMonitorList(monitorList);
                    }));
                }
                else
                {
                    ShowCrashReport(e, crashReport);
                    UpdateMonitorList(monitorList);
                }
            };

            crashPage.Controls.Add(crashLayout);
            tabControl.TabPages.Add(crashPage);

            // 蓝屏检测标签页
            var bsodPage = new TabPage(LanguageManager.GetString("MonitorBSOD"));
            var bsodLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            bsodLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            bsodLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bsodLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));

            var bsodBtnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var btnScanBSOD = new Button
            {
                Text = "扫描历史记录",
                Width = 120,
                Height = 30
            };
            btnScanBSOD.Click += (s, e) => ScanBSODHistory(bsodLayout);

            var btnEnableBSOD = new Button
            {
                Text = "启用蓝屏报告",
                Width = 120,
                Height = 30
            };
            btnEnableBSOD.Click += (s, e) =>
            {
                _bsodDetector.EnableBSODReporting();
                UpdateStatus("蓝屏报告已启用");
            };

            bsodBtnPanel.Controls.AddRange(new Control[] { btnScanBSOD, btnEnableBSOD });
            bsodLayout.Controls.Add(bsodBtnPanel, 0, 0);

            var bsodListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                Name = "bsodListView"
            };
            bsodListView.Columns.Add("时间", 150);
            bsodListView.Columns.Add("错误代码", 150);
            bsodListView.Columns.Add("错误名称", 250);
            bsodListView.Columns.Add("导致驱动", 200);
            bsodListView.Columns.Add("转储文件", 200);

            bsodListView.SelectedIndexChanged += (s, e) =>
            {
                if (bsodListView.SelectedItems.Count > 0)
                {
                    ShowBSODDetails(bsodListView.SelectedItems[0], bsodLayout);
                }
            };

            bsodLayout.Controls.Add(bsodListView, 0, 1);

            var bsodDetails = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                Name = "bsodDetails"
            };
            bsodLayout.Controls.Add(bsodDetails, 0, 2);

            bsodPage.Controls.Add(bsodLayout);
            tabControl.TabPages.Add(bsodPage);

            page.Controls.Add(tabControl);
            return page;
        }

        private TabPage CreateToolsTab()
        {
            var page = new TabPage(LanguageManager.GetString("MenuTools"));

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));

            // 设备管理器按钮
            var btnDeviceManager = new Button
            {
                Text = "打开设备管理器",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F)
            };
            btnDeviceManager.Click += (s, e) => _driverManager.OpenDeviceManager();
            layout.Controls.Add(btnDeviceManager, 0, 0);

            // 设备健康检测按钮
            var btnDeviceHealth = new Button
            {
                Text = "设备健康检测",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F),
                BackColor = Color.LightBlue
            };
            btnDeviceHealth.Click += (s, e) =>
            {
                using var form = new DeviceHealthForm();
                form.ShowDialog(this);
            };
            layout.Controls.Add(btnDeviceHealth, 1, 0);

            // 硬件测试按钮
            var btnHardwareTest = new Button
            {
                Text = "硬件测试中心",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F),
                BackColor = Color.LightGreen
            };
            btnHardwareTest.Click += (s, e) =>
            {
                using var form = new HardwareTestForm();
                form.ShowDialog(this);
            };
            layout.Controls.Add(btnHardwareTest, 2, 0);

            // 驱动管理器按钮
            var btnDriverManager = new Button
            {
                Text = "驱动管理器",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F),
                BackColor = Color.LightYellow
            };
            btnDriverManager.Click += (s, e) =>
            {
                using var form = new DriverManagerForm();
                form.ShowDialog(this);
            };
            layout.Controls.Add(btnDriverManager, 0, 1);

            // DISM快捷命令按钮
            var btnDismCommands = new Button
            {
                Text = "DISM快捷命令",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F),
                BackColor = Color.LightCyan
            };
            btnDismCommands.Click += (s, e) =>
            {
                using var form = new DismCommandForm();
                form.ShowDialog(this);
            };
            layout.Controls.Add(btnDismCommands, 1, 1);

            // 系统信息按钮
            var btnSystemInfo = new Button
            {
                Text = "系统信息",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F)
            };
            btnSystemInfo.Click += (s, e) => ShowSystemInfo();
            layout.Controls.Add(btnSystemInfo, 2, 1);

            // 事件查看器按钮
            var btnEventViewer = new Button
            {
                Text = "事件查看器",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F)
            };
            btnEventViewer.Click += (s, e) => OpenEventViewer();
            layout.Controls.Add(btnEventViewer, 0, 2);

            // 系统配置按钮
            var btnMsConfig = new Button
            {
                Text = "系统配置",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F)
            };
            btnMsConfig.Click += (s, e) => OpenMsConfig();
            layout.Controls.Add(btnMsConfig, 1, 2);

            // 内置终端按钮
            var btnTerminal = new Button
            {
                Text = "管理员终端 💻",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F),
                BackColor = Color.LightGray
            };
            btnTerminal.Click += async (s, e) => { await LaunchAdminTerminal(); };
            layout.Controls.Add(btnTerminal, 2, 2);

            // 安全中心按钮
            var btnSecurity = new Button
            {
                Text = "安全中心 🔒",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F),
                BackColor = Color.LightPink
            };
            btnSecurity.Click += (s, e) =>
            {
                using var form = new SecurityCenterForm();
                form.ShowDialog(this);
            };
            layout.Controls.Add(btnSecurity, 3, 2);

            // 下载管理器按钮
            var btnDownloadManager = new Button
            {
                Text = "下载管理器 📥",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F),
                BackColor = Color.LightGreen
            };
            btnDownloadManager.Click += (s, e) =>
            {
                using var form = new DownloadManagerForm();
                form.ShowDialog(this);
            };
            layout.Controls.Add(btnDownloadManager, 0, 3);

            // RAM 清理按钮
            var btnRamCleaner = new Button
            {
                Text = "RAM 清理 🧹",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F),
                BackColor = Color.LightYellow
            };
            btnRamCleaner.Click += (s, e) =>
            {
                using var form = new RamCleanerForm();
                form.ShowDialog(this);
            };
            layout.Controls.Add(btnRamCleaner, 1, 3);

            // DISM 教程按钮
            var btnDismTutorial = new Button
            {
                Text = "DISM 教程 📖",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F),
                BackColor = Color.LightBlue
            };
            btnDismTutorial.Click += (s, e) => ShowDismTutorial();
            layout.Controls.Add(btnDismTutorial, 2, 3);

            page.Controls.Add(layout);
            return page;
        }

        // 异步操作方法
        private async Task ScanHardwareAsync()
        {
            UpdateStatus("正在扫描硬件...");
            var tree = _mainTabControl.TabPages[0].Controls.Find("hardwareTree", true).FirstOrDefault() as TreeView;
            if (tree != null)
            {
                tree.Nodes.Clear();
                var devices = await Task.Run(() => _deviceManager.GetAllDevices());
                DisplayHardwareTree(tree, devices);
            }
            UpdateStatus("硬件扫描完成");
        }

        private async Task RefreshHardwareAsync()
        {
            await ScanHardwareAsync();
        }

        private async Task ScanVCRuntimesAsync(TableLayoutPanel layout)
        {
            UpdateStatus("正在扫描VC++运行库...");
            var listView = layout.Controls.Find("vcListView", true).FirstOrDefault() as ListView;
            if (listView != null)
            {
                listView.Items.Clear();
                var runtimes = await Task.Run(() => _vcRuntimeManager.ScanInstalledRuntimes());
                DisplayVCRuntimes(listView, runtimes);
            }
            UpdateStatus("VC++运行库扫描完成");
        }

        private async Task InstallAllVCRuntimesAsync(TableLayoutPanel layout)
        {
            var progress = new Progress<string>(msg => UpdateStatus(msg));
            await _vcRuntimeManager.InstallAllMissingRuntimesAsync(progress);
            await ScanVCRuntimesAsync(layout);
        }

        private async Task RunDismCommandAsync(string arguments, TableLayoutPanel layout)
        {
            UpdateStatus("正在执行DISM命令...");
            var output = layout.Controls.Find("dismOutput", true).FirstOrDefault() as TextBox;
            var progressBar = layout.Controls.Find("dismProgress", true).FirstOrDefault() as ProgressBar;

            if (output != null && progressBar != null)
            {
                output.Clear();
                progressBar.Visible = true;

                var progress = new Progress<string>(msg =>
                {
                    output.AppendText(msg + Environment.NewLine);
                });

                var result = await _dismManager.ExecuteCommandAsync(arguments, progress);
                progressBar.Visible = false;

                if (!result.Success)
                {
                    output.AppendText($"错误: {result.Error}" + Environment.NewLine);
                }
            }
            UpdateStatus("DISM命令执行完成");
        }

        // 显示方法
        private void DisplayHardwareTree(TreeView tree, List<Core.Models.HardwareInfo> devices)
        {
            var categories = devices.GroupBy(d => d.Type).OrderBy(g => g.Key);

            foreach (var category in categories)
            {
                var categoryNode = new TreeNode(GetHardwareTypeName(category.Key))
                {
                    Tag = category.Key
                };

                foreach (var device in category)
                {
                    var deviceNode = new TreeNode(device.Name)
                    {
                        Tag = device,
                        ForeColor = GetStatusColor(device.Status)
                    };
                    categoryNode.Nodes.Add(deviceNode);
                }

                tree.Nodes.Add(categoryNode);
            }

            tree.ExpandAll();
        }

        private void ShowHardwareDetails(TreeNode? node, TextBox detailsText)
        {
            if (node?.Tag is Core.Models.HardwareInfo device)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"设备名称: {device.Name}");
                sb.AppendLine($"描述: {device.Description}");
                sb.AppendLine($"制造商: {device.Manufacturer}");
                sb.AppendLine($"设备ID: {device.DeviceId}");
                sb.AppendLine($"硬件ID: {device.HardwareId}");
                sb.AppendLine($"状态: {GetStatusText(device.Status)}");
                sb.AppendLine($"扫描时间: {device.LastScanTime:yyyy-MM-dd HH:mm:ss}");

                if (device.Properties.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("属性:");
                    foreach (var prop in device.Properties)
                    {
                        sb.AppendLine($"  {prop.Key}: {prop.Value}");
                    }
                }

                detailsText.Text = sb.ToString();
            }
        }

        private void DisplayVCRuntimes(ListView listView, List<Core.Models.VCRuntimeInfo> runtimes)
        {
            foreach (var runtime in runtimes)
            {
                var item = new ListViewItem(runtime.Name);
                item.SubItems.Add(runtime.Version);
                item.SubItems.Add(runtime.Architecture);
                item.SubItems.Add(runtime.IsInstalled ? "已安装" : "未安装");

                if (!runtime.IsInstalled)
                {
                    var installBtn = new Button
                    {
                        Text = "安装",
                        Tag = runtime
                    };
                    installBtn.Click += async (s, e) =>
                    {
                        var progress = new Progress<string>(msg => UpdateStatus(msg));
                        await _vcRuntimeManager.DownloadAndInstallRuntimeAsync(runtime, progress);
                    };
                    item.SubItems.Add("点击安装");
                }
                else
                {
                    item.SubItems.Add("-");
                }

                listView.Items.Add(item);
            }
        }

        private void AnalyzeError(string errorText, TableLayoutPanel layout)
        {
            var output = layout.Controls.Find("errorOutput", true).FirstOrDefault() as TextBox;
            if (output != null)
            {
                var result = _errorParser.AnalyzeError(errorText);
                var sb = new StringBuilder();

                sb.AppendLine("错误分析结果:");
                sb.AppendLine($"严重程度: {result.Severity}");
                sb.AppendLine();

                if (result.FoundErrorCodes.Count > 0)
                {
                    sb.AppendLine("发现的错误代码:");
                    foreach (var code in result.FoundErrorCodes)
                    {
                        sb.AppendLine($"  - {code}");
                    }
                    sb.AppendLine();
                }

                if (result.Explanations.Count > 0)
                {
                    sb.AppendLine("详细说明:");
                    foreach (var explanation in result.Explanations)
                    {
                        sb.AppendLine($"  {explanation}");
                    }
                    sb.AppendLine();
                }

                if (result.Recommendations.Count > 0)
                {
                    sb.AppendLine("建议的解决方案:");
                    foreach (var rec in result.Recommendations)
                    {
                        sb.AppendLine($"  - {rec}");
                    }
                }

                output.Text = sb.ToString();
            }
        }

        private void StartMonitoringApp(string filePath, TableLayoutPanel layout)
        {
            try
            {
                _appCrashMonitor.MonitorProcess(filePath);
                string fileName = Path.GetFileName(filePath);
                UpdateStatus($"开始监控: {fileName}");
                UpdateMonitorList(layout.Controls.Find("monitorList", true).FirstOrDefault() as ListView);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动监控失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateMonitorList(ListView? listView)
        {
            if (listView == null) return;

            listView.Items.Clear();
            var processes = _appCrashMonitor.GetMonitoredProcesses();

            foreach (var process in processes)
            {
                var item = new ListViewItem(process.ProcessId.ToString());
                item.SubItems.Add(process.ProcessName);
                item.SubItems.Add(process.ExecutablePath);
                item.SubItems.Add(process.StartTime.ToString("yyyy-MM-dd HH:mm:ss"));
                item.SubItems.Add(process.IsRunning ? "运行中" : "已停止");
                listView.Items.Add(item);
            }
        }

        private void ShowCrashReport(Core.Models.CrashReport report, TextBox textBox)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine("应用程序崩溃报告");
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine();
            sb.AppendLine($"应用程序: {report.ApplicationName}");
            sb.AppendLine($"崩溃时间: {report.CrashTime:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"异常类型: {report.ExceptionType}");
            sb.AppendLine($"异常消息: {report.ExceptionMessage}");
            sb.AppendLine();
            sb.AppendLine($"根本原因: {report.RootCause}");
            sb.AppendLine();
            sb.AppendLine("建议的解决方案:");
            foreach (var rec in report.Recommendations)
            {
                sb.AppendLine($"  - {rec}");
            }

            textBox.Text = sb.ToString();
        }

        private void ScanBSODHistory(TableLayoutPanel layout)
        {
            var listView = layout.Controls.Find("bsodListView", true).FirstOrDefault() as ListView;
            if (listView != null)
            {
                listView.Items.Clear();
                var bsods = _bsodDetector.GetBSODHistory();

                foreach (var bsod in bsods)
                {
                    var item = new ListViewItem(bsod.CrashTime.ToString("yyyy-MM-dd HH:mm:ss"));
                    item.SubItems.Add(bsod.BugCheckCode);
                    item.SubItems.Add(bsod.BugCheckString);
                    item.SubItems.Add(bsod.CausedByDriver);
                    item.SubItems.Add(Path.GetFileName(bsod.FilePath));
                    item.Tag = bsod;
                    listView.Items.Add(item);
                }
            }
        }

        private void ShowBSODDetails(ListViewItem item, TableLayoutPanel layout)
        {
            if (item.Tag is Core.Models.BSODInfo bsod)
            {
                var details = layout.Controls.Find("bsodDetails", true).FirstOrDefault() as TextBox;
                if (details != null)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"错误代码: {bsod.BugCheckCode}");
                    sb.AppendLine($"错误名称: {bsod.BugCheckString}");
                    sb.AppendLine();
                    sb.AppendLine($"用户友好说明:");
                    sb.AppendLine(bsod.UserFriendlyExplanation);
                    sb.AppendLine();
                    sb.AppendLine($"可能的原因:");
                    foreach (var cause in bsod.PossibleCauses)
                    {
                        sb.AppendLine($"  - {cause}");
                    }
                    sb.AppendLine();
                    sb.AppendLine($"建议的解决方案:");
                    foreach (var solution in bsod.Solutions)
                    {
                        sb.AppendLine($"  - {solution}");
                    }

                    details.Text = sb.ToString();
                }
            }
        }

        private void ShowSystemInfo()
        {
            // 显示系统信息对话框
            using var dialog = new Form
            {
                Text = "系统信息",
                Size = new Size(600, 400),
                StartPosition = FormStartPosition.CenterParent
            };

            var textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F)
            };

            var sb = new StringBuilder();
            sb.AppendLine($"操作系统: {Environment.OSVersion}");
            sb.AppendLine($"计算机名: {Environment.MachineName}");
            sb.AppendLine($"用户名: {Environment.UserName}");
            sb.AppendLine($"处理器数量: {Environment.ProcessorCount}");
            sb.AppendLine($"系统目录: {Environment.SystemDirectory}");
            sb.AppendLine($"当前目录: {Environment.CurrentDirectory}");
            sb.AppendLine($"CLR版本: {Environment.Version}");
            sb.AppendLine($"64位操作系统: {Environment.Is64BitOperatingSystem}");
            sb.AppendLine($"64位进程: {Environment.Is64BitProcess}");
            sb.AppendLine($"系统启动时间: {DateTime.Now - TimeSpan.FromMilliseconds(Environment.TickCount)}");

            textBox.Text = sb.ToString();
            dialog.Controls.Add(textBox);
            dialog.ShowDialog(this);
        }

        private void OpenEventViewer()
        {
            try
            {
                Process.Start("eventvwr.msc");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开事件查看器: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenMsConfig()
        {
            try
            {
                Process.Start("msconfig.exe");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开系统配置: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowAbout()
        {
            MessageBox.Show(
                "硬件检测与系统维护工具\n\n" +
                "版本：1.0\n" +
                "作者：furry 皓予\n" +
                "开发方式：vibe coding\n\n" +
                "本工具用于检测硬件状态、管理系统维护任务、\n" +
                "监控系统健康状况以及修复系统问题。\n\n" +
                "请以管理员身份运行以获得完整功能。",
                "关于",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void ShowDismTutorial()
        {
            var tutorial = DismTutorial.GetIntroduction();
            var tutorialItems = DismTutorial.GetTutorials();
            var scenarios = DismTutorial.GetCommonScenarios();

            var form = new Form
            {
                Text = "DISM 新手教程",
                Size = new Size(900, 700),
                StartPosition = FormStartPosition.CenterParent
            };

            var tabControl = new TabControl { Dock = DockStyle.Fill };

            // 简介标签页
            var introTab = new TabPage("简介");
            var introTextBox = new TextBox
            {
                Text = tutorial,
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Microsoft YaHei", 10F)
            };
            introTab.Controls.Add(introTextBox);
            tabControl.TabPages.Add(introTab);

            // 命令列表标签页
            var commandsTab = new TabPage("命令列表");
            var commandListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            commandListView.Columns.Add("分类", 120);
            commandListView.Columns.Add("名称", 150);
            commandListView.Columns.Add("命令", 300);
            commandListView.Columns.Add("说明", 200);

            foreach (var item in tutorialItems)
            {
                var listViewItem = new ListViewItem(item.Category);
                listViewItem.SubItems.Add(item.Title);
                listViewItem.SubItems.Add(item.Command);
                listViewItem.SubItems.Add(item.Description);
                listViewItem.Tag = item;
                commandListView.Items.Add(listViewItem);
            }

            commandListView.SelectedIndexChanged += (s, e) =>
            {
                if (commandListView.SelectedItems.Count > 0)
                {
                    var selectedItem = commandListView.SelectedItems[0];
                    if (selectedItem.Tag is DismTutorialItem tutorialItem)
                    {
                        var details = $"用途：{tutorialItem.Usage}\n\n执行时间：{tutorialItem.Duration}\n风险等级：{tutorialItem.RiskLevel}";
                        MessageBox.Show(details, "详细信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            };

            commandsTab.Controls.Add(commandListView);
            tabControl.TabPages.Add(commandsTab);

            // 使用场景标签页
            var scenariosTab = new TabPage("使用场景");
            var scenariosTextBox = new TextBox
            {
                Text = scenarios,
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Microsoft YaHei", 10F)
            };
            scenariosTab.Controls.Add(scenariosTextBox);
            tabControl.TabPages.Add(scenariosTab);

            form.Controls.Add(tabControl);
            form.ShowDialog(this);
        }

        private async Task LaunchAdminTerminal()
        {
            if (!AdminTerminal.IsRunningAsAdmin())
            {
                var result = MessageBox.Show(
                    "管理员终端需要管理员权限才能运行。\n\n是否以管理员身份重启本软件？",
                    "需要管理员权限",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    AdminTerminal.RestartAsAdmin();
                }
                return;
            }

            UpdateStatus("正在启动管理员终端...");
            var terminal = new AdminTerminal();
            await terminal.StartAsync();
            UpdateStatus("管理员终端已启动");
        }

        // 辅助方法
        private void ApplyLanguage()
        {
            Text = LanguageManager.GetString("AppTitle");
            _authorLabel.Text = LanguageManager.GetString("AuthorCredit");
        }

        private void UpdateStatus(string? message = null)
        {
            if (message != null)
            {
                _statusLabel.Text = message;
            }
            _memoryStatusLabel.Text = $"内存: {MemoryOptimizer.GetMemoryUsageText()}";
        }

        private string GetHardwareTypeName(Core.Models.HardwareType type)
        {
            return type switch
            {
                Core.Models.HardwareType.Processor => "处理器",
                Core.Models.HardwareType.Memory => "内存",
                Core.Models.HardwareType.Motherboard => "主板",
                Core.Models.HardwareType.GraphicsCard => "显卡",
                Core.Models.HardwareType.Storage => "存储设备",
                Core.Models.HardwareType.Network => "网络适配器",
                Core.Models.HardwareType.Audio => "音频设备",
                Core.Models.HardwareType.USB => "USB控制器",
                Core.Models.HardwareType.Bluetooth => "蓝牙设备",
                _ => "其他设备"
            };
        }

        private string GetStatusText(Core.Models.HardwareStatus status)
        {
            return status switch
            {
                Core.Models.HardwareStatus.Normal => "正常",
                Core.Models.HardwareStatus.Warning => "警告",
                Core.Models.HardwareStatus.Error => "错误",
                Core.Models.HardwareStatus.Disabled => "已禁用",
                _ => "未知"
            };
        }

        private Color GetStatusColor(Core.Models.HardwareStatus status)
        {
            return status switch
            {
                Core.Models.HardwareStatus.Normal => Color.Green,
                Core.Models.HardwareStatus.Warning => Color.Orange,
                Core.Models.HardwareStatus.Error => Color.Red,
                Core.Models.HardwareStatus.Disabled => Color.Gray,
                _ => Color.Black
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _appCrashMonitor.Dispose();
            _bsodDetector.Dispose();
            MemoryOptimizer.StopMonitoring();
            base.OnFormClosing(e);
        }
    }
}
