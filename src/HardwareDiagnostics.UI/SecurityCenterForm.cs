using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using HardwareDiagnostics.Security;

namespace HardwareDiagnostics.UI
{
    public partial class SecurityCenterForm : Form
    {
        private NetworkFirewall? _firewall;
        private USBSecurityGuard? _usbGuard;
        private ProcessProtector? _processProtector;
        private SecurityLogger? _logger;
        private ListView _packetListView;
        private ListView _usbListView;
        private ListView _logListView;
        private Button _btnStartFirewall;
        private Button _btnStopFirewall;
        private Button _btnStartUSBGuard;
        private Button _btnStopUSBGuard;
        private Button _btnEnableProtection;
        private Button _btnDisableProtection;
        private Button _btnClearLogs;
        private Label _lblStatus;
        private Label _lblPacketCount;
        private Label _lblThreatCount;
        private Label _lblUsbBlocked;
        private int _packetCount = 0;
        private int _threatCount = 0;
        private int _usbBlockedCount = 0;

        public SecurityCenterForm()
        {
            InitializeComponent();
            InitializeSecurityModules();
        }

        private void InitializeComponent()
        {
            this.Text = "安全中心";
            this.Size = new Size(1100, 750);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            // 标题
            var titleLabel = new Label
            {
                Text = "🔒 系统安全中心",
                Font = new Font("Microsoft YaHei", 18, FontStyle.Bold),
                ForeColor = Color.DarkGreen,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            mainLayout.Controls.Add(titleLabel, 0, 0);

            // 状态面板
            var statusPanel = CreateStatusPanel();
            mainLayout.Controls.Add(statusPanel, 0, 1);

            // 标签页
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            // 网络监控标签页
            var networkTab = new TabPage("网络防火墙");
            networkTab.Controls.Add(CreateNetworkPanel());
            tabControl.TabPages.Add(networkTab);

            // USB安全标签页
            var usbTab = new TabPage("USB安全");
            usbTab.Controls.Add(CreateUSBPanel());
            tabControl.TabPages.Add(usbTab);

            // 进程保护标签页
            var protectionTab = new TabPage("进程保护");
            protectionTab.Controls.Add(CreateProtectionPanel());
            tabControl.TabPages.Add(protectionTab);

            // 日志标签页
            var logTab = new TabPage("安全日志");
            logTab.Controls.Add(CreateLogPanel());
            tabControl.TabPages.Add(logTab);

            mainLayout.Controls.Add(tabControl, 0, 2);

            // 底部状态栏
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Height = 40
            };
            _lblStatus = new Label
            {
                Text = "就绪",
                Dock = DockStyle.Left,
                TextAlign = ContentAlignment.MiddleLeft,
                Width = 300
            };
            bottomPanel.Controls.Add(_lblStatus);
            mainLayout.Controls.Add(bottomPanel, 0, 3);

            this.Controls.Add(mainLayout);
        }

        private Panel CreateStatusPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.LightYellow
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 6,
                RowCount = 1
            };

            _lblPacketCount = CreateStatusLabel("数据包: 0");
            _lblThreatCount = CreateStatusLabel("威胁: 0", Color.Red);
            _lblUsbBlocked = CreateStatusLabel("USB拦截: 0", Color.Orange);

            layout.Controls.Add(_lblPacketCount, 0, 0);
            layout.Controls.Add(_lblThreatCount, 1, 0);
            layout.Controls.Add(_lblUsbBlocked, 2, 0);

            panel.Controls.Add(layout);
            return panel;
        }

        private Label CreateStatusLabel(string text, Color? foreColor = null)
        {
            return new Label
            {
                Text = text,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
                ForeColor = foreColor ?? Color.Black
            };
        }

        private Panel CreateNetworkPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // 按钮面板
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnStartFirewall = new Button
            {
                Text = "启动防火墙",
                Width = 120,
                Height = 35,
                BackColor = Color.LightGreen
            };
            _btnStartFirewall.Click += (s, e) => StartFirewall();

            _btnStopFirewall = new Button
            {
                Text = "停止防火墙",
                Width = 120,
                Height = 35,
                BackColor = Color.LightCoral,
                Enabled = false
            };
            _btnStopFirewall.Click += (s, e) => StopFirewall();

            buttonPanel.Controls.Add(_btnStartFirewall);
            buttonPanel.Controls.Add(_btnStopFirewall);

            layout.Controls.Add(buttonPanel, 0, 0);

            // 数据包列表
            _packetListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _packetListView.Columns.Add("时间", 120);
            _packetListView.Columns.Add("源IP", 120);
            _packetListView.Columns.Add("目标IP", 120);
            _packetListView.Columns.Add("威胁类型", 150);
            _packetListView.Columns.Add("描述", 300);

            layout.Controls.Add(_packetListView, 0, 1);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateUSBPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));

            // 按钮面板
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnStartUSBGuard = new Button
            {
                Text = "启动USB监控",
                Width = 120,
                Height = 35,
                BackColor = Color.LightGreen
            };
            _btnStartUSBGuard.Click += (s, e) => StartUSBGuard();

            _btnStopUSBGuard = new Button
            {
                Text = "停止USB监控",
                Width = 120,
                Height = 35,
                BackColor = Color.LightCoral,
                Enabled = false
            };
            _btnStopUSBGuard.Click += (s, e) => StopUSBGuard();

            buttonPanel.Controls.Add(_btnStartUSBGuard);
            buttonPanel.Controls.Add(_btnStopUSBGuard);

            layout.Controls.Add(buttonPanel, 0, 0);

            // USB设备列表
            _usbListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _usbListView.Columns.Add("时间", 120);
            _usbListView.Columns.Add("设备名称", 200);
            _usbListView.Columns.Add("VID/PID", 120);
            _usbListView.Columns.Add("风险等级", 80);
            _usbListView.Columns.Add("状态", 100);

            layout.Controls.Add(_usbListView, 0, 1);

            // 说明标签
            var infoLabel = new Label
            {
                Text = "USB安全说明：\n" +
                       "• 自动检测BadUSB、自动化键盘、单片机等可疑设备\n" +
                       "• 高风险设备将被自动阻止\n" +
                       "• 低风险设备需手动确认后才允许连接",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.Fixed3D,
                Padding = new Padding(5)
            };
            layout.Controls.Add(infoLabel, 0, 2);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateProtectionPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));

            // 按钮面板
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnEnableProtection = new Button
            {
                Text = "启用进程保护",
                Width = 120,
                Height = 35,
                BackColor = Color.LightGreen
            };
            _btnEnableProtection.Click += (s, e) => EnableProtection();

            _btnDisableProtection = new Button
            {
                Text = "禁用进程保护",
                Width = 120,
                Height = 35,
                BackColor = Color.LightCoral,
                Enabled = false
            };
            _btnDisableProtection.Click += (s, e) => DisableProtection();

            buttonPanel.Controls.Add(_btnEnableProtection);
            buttonPanel.Controls.Add(_btnDisableProtection);

            layout.Controls.Add(buttonPanel, 0, 0);

            // 保护状态列表
            var protectionList = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true
            };
            protectionList.Columns.Add("保护项目", 200);
            protectionList.Columns.Add("状态", 100);
            protectionList.Columns.Add("说明", 400);

            protectionList.Items.Add(new ListViewItem(new[] { "调试器检测", "未启用", "检测是否有调试器附加到本进程" }));
            protectionList.Items.Add(new ListViewItem(new[] { "父进程验证", "未启用", "验证父进程是否合法" }));
            protectionList.Items.Add(new ListViewItem(new[] { "内存完整性", "未启用", "监控内存是否被修改" }));
            protectionList.Items.Add(new ListViewItem(new[] { "窗口保护", "未启用", "防止窗口被关闭" }));
            protectionList.Items.Add(new ListViewItem(new[] { "看门狗线程", "未启用", "监控进程是否被挂起" }));

            layout.Controls.Add(protectionList, 0, 1);

            // 说明标签
            var infoLabel = new Label
            {
                Text = "进程保护说明：\n" +
                       "• 启用后将防止第三方软件强制终止本软件\n" +
                       "• 检测到调试器时会自动响应\n" +
                       "• 内存完整性检查可发现内存篡改\n" +
                       "• 注意：某些杀毒软件可能会误报此功能",
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.Fixed3D,
                Padding = new Padding(5),
                ForeColor = Color.DarkBlue
            };
            layout.Controls.Add(infoLabel, 0, 2);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateLogPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10)
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // 按钮面板
            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnClearLogs = new Button
            {
                Text = "清空日志",
                Width = 100,
                Height = 35
            };
            _btnClearLogs.Click += (s, e) => ClearLogs();

            var btnExportLogs = new Button
            {
                Text = "导出日志",
                Width = 100,
                Height = 35
            };
            btnExportLogs.Click += (s, e) => ExportLogs();

            buttonPanel.Controls.Add(_btnClearLogs);
            buttonPanel.Controls.Add(btnExportLogs);

            layout.Controls.Add(buttonPanel, 0, 0);

            // 日志列表
            _logListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _logListView.Columns.Add("时间", 120);
            _logListView.Columns.Add("事件类型", 100);
            _logListView.Columns.Add("严重级别", 80);
            _logListView.Columns.Add("消息", 500);

            layout.Controls.Add(_logListView, 0, 1);

            panel.Controls.Add(layout);
            return panel;
        }

        private void InitializeSecurityModules()
        {
            _logger = new SecurityLogger();
            _logger.OnLogEntry += (entry) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => AddLogEntry(entry)));
                }
                else
                {
                    AddLogEntry(entry);
                }
            };

            _firewall = new NetworkFirewall();
            _firewall.SuspiciousPacketDetected += (s, e) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => HandleSuspiciousPacket(e.Packet)));
                }
                else
                {
                    HandleSuspiciousPacket(e.Packet);
                }
            };

            _usbGuard = new USBSecurityGuard();
            _usbGuard.SuspiciousDeviceDetected += (s, e) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => HandleUSBEvent(e.Device, e.Risk ?? new RiskAssessment { Level = RiskLevel.Medium, Factors = new List<RiskFactor>() })));
                }
                else
                {
                    HandleUSBEvent(e.Device, e.Risk ?? new RiskAssessment { Level = RiskLevel.Medium, Factors = new List<RiskFactor>() });
                }
            };

            _processProtector = new ProcessProtector();
            _processProtector.ProtectionTriggered += (s, e) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => HandleProtectionEvent(e.Message)));
                }
                else
                {
                    HandleProtectionEvent(e.Message);
                }
            };
        }

        private void StartFirewall()
        {
            try
            {
                _firewall?.StartMonitoring();
                _btnStartFirewall.Enabled = false;
                _btnStopFirewall.Enabled = true;
                UpdateStatus("网络防火墙已启动");
                _logger?.LogInfo("网络防火墙已启动");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动防火墙失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopFirewall()
        {
            _firewall?.StopMonitoring();
            _btnStartFirewall.Enabled = true;
            _btnStopFirewall.Enabled = false;
            UpdateStatus("网络防火墙已停止");
            _logger?.LogInfo("网络防火墙已停止");
        }

        private void StartUSBGuard()
        {
            try
            {
                _usbGuard?.StartMonitoring();
                _btnStartUSBGuard.Enabled = false;
                _btnStopUSBGuard.Enabled = true;
                UpdateStatus("USB监控已启动");
                _logger?.LogInfo("USB监控已启动");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动USB监控失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopUSBGuard()
        {
            _usbGuard?.StopMonitoring();
            _btnStartUSBGuard.Enabled = true;
            _btnStopUSBGuard.Enabled = false;
            UpdateStatus("USB监控已停止");
            _logger?.LogInfo("USB监控已停止");
        }

        private void EnableProtection()
        {
            try
            {
                _processProtector?.StartProtection();
                _btnEnableProtection.Enabled = false;
                _btnDisableProtection.Enabled = true;
                UpdateStatus("进程保护已启用");
                _logger?.LogInfo("进程保护已启用");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启用进程保护失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisableProtection()
        {
            _processProtector?.StopProtection();
            _btnEnableProtection.Enabled = true;
            _btnDisableProtection.Enabled = false;
            UpdateStatus("进程保护已禁用");
            _logger?.LogInfo("进程保护已禁用");
        }

        private void HandleSuspiciousPacket(SuspiciousPacket packet)
        {
            _packetCount++;
            _threatCount++;

            var item = new ListViewItem(new[]
            {
                packet.Timestamp.ToString("HH:mm:ss"),
                packet.SourceIP,
                packet.DestinationIP,
                packet.ThreatType.ToString(),
                packet.Description
            });

            item.ForeColor = packet.Severity >= ThreatSeverity.High ? Color.Red :
                            packet.Severity >= ThreatSeverity.Medium ? Color.Orange : Color.DarkOrange;

            _packetListView.Items.Insert(0, item);
            if (_packetListView.Items.Count > 1000)
                _packetListView.Items.RemoveAt(_packetListView.Items.Count - 1);

            UpdateStatusLabels();
            _logger?.LogSuspiciousPacket(packet);
        }

        private void HandleUSBEvent(USBDeviceInfo device, RiskAssessment risk)
        {
            var item = new ListViewItem(new[]
            {
                DateTime.Now.ToString("HH:mm:ss"),
                device.DeviceName,
                device.VIDPID,
                risk.Level.ToString(),
                risk.Level >= RiskLevel.High ? "已阻止" : "待确认"
            });

            item.ForeColor = risk.Level >= RiskLevel.High ? Color.Red :
                            risk.Level >= RiskLevel.Medium ? Color.Orange : Color.Green;

            _usbListView.Items.Insert(0, item);

            if (risk.Level >= RiskLevel.High)
            {
                _usbBlockedCount++;
                UpdateStatusLabels();
            }

            _logger?.LogUSBEvent(device, risk);
        }

        private void HandleProtectionEvent(string reason)
        {
            _logger?.LogSecurityEvent(SecurityEventType.DebuggerDetected, $"保护触发: {reason}", "High");
        }

        private void AddLogEntry(SecurityLogEntry entry)
        {
            var item = new ListViewItem(new[]
            {
                entry.Timestamp.ToString("HH:mm:ss"),
                entry.EventType.ToString(),
                entry.Severity,
                entry.Message
            });

            item.ForeColor = entry.Severity == "High" ? Color.Red :
                            entry.Severity == "Medium" ? Color.Orange : Color.Black;

            _logListView.Items.Insert(0, item);
            if (_logListView.Items.Count > 2000)
                _logListView.Items.RemoveAt(_logListView.Items.Count - 1);
        }

        private void ClearLogs()
        {
            _logListView.Items.Clear();
            _logger?.LogInfo("日志已清空");
        }

        private void ExportLogs()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Filter = "日志文件|*.log|文本文件|*.txt";
                dialog.FileName = $"SecurityLog_{DateTime.Now:yyyyMMdd_HHmmss}.log";

                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var lines = new List<string>();
                        foreach (ListViewItem item in _logListView.Items)
                        {
                            lines.Add($"[{item.SubItems[0].Text}] [{item.SubItems[1].Text}] [{item.SubItems[2].Text}] {item.SubItems[3].Text}");
                        }
                        File.WriteAllLines(dialog.FileName, lines);
                        MessageBox.Show("日志导出成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }

        private void UpdateStatus(string message)
        {
            _lblStatus.Text = $"[{DateTime.Now:HH:mm:ss}] {message}";
        }

        private void UpdateStatusLabels()
        {
            _lblPacketCount.Text = $"数据包: {_packetCount}";
            _lblThreatCount.Text = $"威胁: {_threatCount}";
            _lblUsbBlocked.Text = $"USB拦截: {_usbBlockedCount}";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopFirewall();
            StopUSBGuard();
            DisableProtection();
            _firewall?.Dispose();
            _usbGuard?.Dispose();
            _processProtector?.Dispose();
            _logger?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
