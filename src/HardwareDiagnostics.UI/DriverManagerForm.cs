using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;
using HardwareDiagnostics.Hardware;

namespace HardwareDiagnostics.UI
{
    public partial class DriverManagerForm : Form
    {
        private readonly DriverDetector _driverDetector;
        private ListView _driverListView;
        private TextBox _detailsTextBox;
        private Button _scanButton;
        private Button _downloadButton;
        private Button _installButton;
        private Label _statusLabel;
        private List<DriverDetectionResult> _currentDrivers = new();

        public DriverManagerForm()
        {
            _driverDetector = new DriverDetector();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "驱动程序管理器";
            Size = new Size(1100, 750);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 9F);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // 信息面板
            var infoPanel = new Panel { Dock = DockStyle.Fill };
            var lblComputer = new Label
            {
                Text = $"计算机型号: {_driverDetector.GetComputerModel()}",
                Dock = DockStyle.Top,
                Height = 25
            };
            var lblOS = new Label
            {
                Text = $"操作系统: {_driverDetector.GetOperatingSystemInfo()}",
                Dock = DockStyle.Top,
                Height = 25
            };
            infoPanel.Controls.Add(lblOS);
            infoPanel.Controls.Add(lblComputer);
            layout.Controls.Add(infoPanel, 0, 0);

            // 按钮面板
            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            _scanButton = new Button
            {
                Text = "扫描所有驱动",
                Width = 120,
                Height = 30
            };
            _scanButton.Click += async (s, e) => await ScanAllDriversAsync();

            var btnScanMissing = new Button
            {
                Text = "扫描缺失驱动",
                Width = 120,
                Height = 30
            };
            btnScanMissing.Click += async (s, e) => await ScanMissingDriversAsync();

            var btnScanOutdated = new Button
            {
                Text = "扫描过时驱动",
                Width = 120,
                Height = 30
            };
            btnScanOutdated.Click += async (s, e) => await ScanOutdatedDriversAsync();

            _downloadButton = new Button
            {
                Text = "下载选中驱动",
                Width = 120,
                Height = 30,
                Enabled = false
            };
            _downloadButton.Click += async (s, e) => await DownloadSelectedDriverAsync();

            _installButton = new Button
            {
                Text = "安装驱动",
                Width = 100,
                Height = 30,
                Enabled = false
            };
            _installButton.Click += async (s, e) => await InstallDriverAsync();

            buttonPanel.Controls.AddRange(new Control[] { _scanButton, btnScanMissing, btnScanOutdated, _downloadButton, _installButton });
            layout.Controls.Add(buttonPanel, 0, 1);

            // 驱动列表
            _driverListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            _driverListView.Columns.Add("设备名称", 250);
            _driverListView.Columns.Add("类型", 100);
            _driverListView.Columns.Add("制造商", 150);
            _driverListView.Columns.Add("驱动版本", 120);
            _driverListView.Columns.Add("状态", 80);
            _driverListView.Columns.Add("下载源", 200);
            _driverListView.SelectedIndexChanged += DriverListView_SelectedIndexChanged;

            layout.Controls.Add(_driverListView, 0, 2);

            // 详情面板
            var detailsPanel = new Panel { Dock = DockStyle.Fill };
            _detailsTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F)
            };
            detailsPanel.Controls.Add(_detailsTextBox);
            layout.Controls.Add(detailsPanel, 0, 3);

            // 状态栏
            _statusLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 25,
                Text = "就绪"
            };
            Controls.Add(_statusLabel);

            Controls.Add(layout);
        }

        private async Task ScanAllDriversAsync()
        {
            _scanButton.Enabled = false;
            _driverListView.Items.Clear();
            _statusLabel.Text = "正在扫描驱动程序...";

            try
            {
                _currentDrivers = await Task.Run(() => _driverDetector.DetectAllDrivers());
                DisplayDrivers(_currentDrivers);
                _statusLabel.Text = $"扫描完成，共找到 {_currentDrivers.Count} 个驱动程序";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"扫描失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLabel.Text = "扫描失败";
            }
            finally
            {
                _scanButton.Enabled = true;
            }
        }

        private async Task ScanMissingDriversAsync()
        {
            _scanButton.Enabled = false;
            _driverListView.Items.Clear();
            _statusLabel.Text = "正在扫描缺失驱动...";

            try
            {
                _currentDrivers = await Task.Run(() => _driverDetector.GetMissingDrivers());
                DisplayDrivers(_currentDrivers);

                if (_currentDrivers.Count == 0)
                {
                    MessageBox.Show("未发现缺失的驱动程序！", "好消息", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"发现 {_currentDrivers.Count} 个缺失或损坏的驱动程序！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                _statusLabel.Text = $"发现 {_currentDrivers.Count} 个问题驱动";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"扫描失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLabel.Text = "扫描失败";
            }
            finally
            {
                _scanButton.Enabled = true;
            }
        }

        private async Task ScanOutdatedDriversAsync()
        {
            _scanButton.Enabled = false;
            _driverListView.Items.Clear();
            _statusLabel.Text = "正在扫描过时驱动...";

            try
            {
                _currentDrivers = await Task.Run(() => _driverDetector.GetOutdatedDrivers());
                DisplayDrivers(_currentDrivers);
                _statusLabel.Text = $"发现 {_currentDrivers.Count} 个过时驱动";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"扫描失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _statusLabel.Text = "扫描失败";
            }
            finally
            {
                _scanButton.Enabled = true;
            }
        }

        private void DisplayDrivers(List<DriverDetectionResult> drivers)
        {
            _driverListView.Items.Clear();

            foreach (var driver in drivers.OrderBy(d => d.Status))
            {
                var item = new ListViewItem(driver.DeviceName);
                item.SubItems.Add(driver.DeviceClass);
                item.SubItems.Add(driver.Manufacturer);
                item.SubItems.Add(driver.InstalledDriver?.DriverVersion ?? "未安装");
                item.SubItems.Add(GetDriverStatusText(driver.Status));
                item.SubItems.Add(driver.DownloadSource?.Description ?? "未知");
                item.Tag = driver;

                // 根据状态设置颜色
                switch (driver.Status)
                {
                    case DriverStatus.Missing:
                    case DriverStatus.Corrupted:
                    case DriverStatus.Error:
                        item.ForeColor = Color.Red;
                        item.BackColor = Color.LightPink;
                        break;
                    case DriverStatus.Outdated:
                        item.ForeColor = Color.Orange;
                        item.BackColor = Color.LightYellow;
                        break;
                    case DriverStatus.Disabled:
                        item.ForeColor = Color.Gray;
                        break;
                    case DriverStatus.Normal:
                        item.ForeColor = Color.Green;
                        break;
                }

                _driverListView.Items.Add(item);
            }
        }

        private void DriverListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_driverListView.SelectedItems.Count > 0)
            {
                var driver = _driverListView.SelectedItems[0].Tag as DriverDetectionResult;
                if (driver != null)
                {
                    ShowDriverDetails(driver);
                    _downloadButton.Enabled = driver.Status != DriverStatus.Normal && driver.DownloadSource != null;
                    _installButton.Enabled = driver.Status == DriverStatus.Missing || driver.Status == DriverStatus.Corrupted;
                }
            }
            else
            {
                _downloadButton.Enabled = false;
                _installButton.Enabled = false;
            }
        }

        private void ShowDriverDetails(DriverDetectionResult driver)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"设备名称: {driver.DeviceName}");
            sb.AppendLine($"设备ID: {driver.DeviceId}");
            sb.AppendLine($"硬件ID: {driver.HardwareId}");
            sb.AppendLine($"设备类型: {driver.DeviceClass}");
            sb.AppendLine($"制造商: {driver.Manufacturer}");
            sb.AppendLine();
            sb.AppendLine($"驱动状态: {GetDriverStatusText(driver.Status)}");
            sb.AppendLine($"错误代码: {driver.ErrorCode}");
            sb.AppendLine();
            sb.AppendLine("已安装驱动信息:");
            sb.AppendLine($"  版本: {driver.InstalledDriver?.DriverVersion ?? "未安装"}");
            sb.AppendLine($"  日期: {driver.InstalledDriver?.DriverDate ?? "未知"}");
            sb.AppendLine($"  提供商: {driver.InstalledDriver?.DriverProvider ?? "未知"}");
            sb.AppendLine($"  已签名: {(driver.InstalledDriver?.IsSigned == true ? "是" : "否")}");
            sb.AppendLine();

            if (driver.DownloadSource != null)
            {
                sb.AppendLine("驱动下载源:");
                sb.AppendLine($"  描述: {driver.DownloadSource.Description}");
                sb.AppendLine($"  下载链接: {driver.DownloadSource.DownloadUrl}");
                sb.AppendLine();
            }

            if (driver.RepairSuggestions.Count > 0)
            {
                sb.AppendLine("修复建议:");
                foreach (var suggestion in driver.RepairSuggestions)
                {
                    sb.AppendLine($"  - {suggestion}");
                }
            }

            _detailsTextBox.Text = sb.ToString();
        }

        private async Task DownloadSelectedDriverAsync()
        {
            if (_driverListView.SelectedItems.Count == 0) return;

            var driver = _driverListView.SelectedItems[0].Tag as DriverDetectionResult;
            if (driver == null) return;

            using var dialog = new SaveFileDialog
            {
                FileName = $"{driver.DeviceName}_Driver.exe",
                Filter = "可执行文件|*.exe|压缩文件|*.zip|所有文件|*.*"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _downloadButton.Enabled = false;
                _statusLabel.Text = "正在下载驱动...";

                var progress = new Progress<string>(msg => _statusLabel.Text = msg);
                bool success = await _driverDetector.DownloadDriverAsync(driver, dialog.FileName, progress);

                if (success)
                {
                    MessageBox.Show("驱动下载完成！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("驱动下载失败，请尝试手动访问制造商官网下载。", "失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                _downloadButton.Enabled = true;
                _statusLabel.Text = "就绪";
            }
        }

        private async Task InstallDriverAsync()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "驱动文件|*.inf;*.exe;*.zip|所有文件|*.*",
                Title = "选择驱动文件"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _installButton.Enabled = false;
                _statusLabel.Text = "正在安装驱动...";

                var progress = new Progress<string>(msg => _statusLabel.Text = msg);
                var result = await _driverDetector.InstallDriverAsync(dialog.FileName, progress);

                if (result.Success)
                {
                    MessageBox.Show("驱动安装成功！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await ScanAllDriversAsync();
                }
                else
                {
                    MessageBox.Show($"驱动安装失败:\n{result.ErrorMessage}", "失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                _installButton.Enabled = true;
                _statusLabel.Text = "就绪";
            }
        }

        private string GetDriverStatusText(DriverStatus status)
        {
            return status switch
            {
                DriverStatus.Normal => "正常",
                DriverStatus.Missing => "缺失",
                DriverStatus.Outdated => "过时",
                DriverStatus.Corrupted => "损坏",
                DriverStatus.Disabled => "已禁用",
                DriverStatus.Error => "错误",
                _ => "未知"
            };
        }
    }
}
