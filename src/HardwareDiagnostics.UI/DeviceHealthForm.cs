using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;
using HardwareDiagnostics.Hardware;

namespace HardwareDiagnostics.UI
{
    public partial class DeviceHealthForm : Form
    {
        private readonly DeviceHealthChecker _healthChecker;
        private ListView _deviceListView;
        private TextBox _detailsTextBox;
        private Button _refreshButton;
        private Button _fixButton;
        private Label _statusLabel;

        public DeviceHealthForm()
        {
            _healthChecker = new DeviceHealthChecker();
            InitializeComponent();
            Load += async (s, e) => await RefreshDeviceListAsync();
        }

        private void InitializeComponent()
        {
            Text = "设备健康状态检测";
            Size = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 9F);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));

            // 按钮面板
            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            _refreshButton = new Button
            {
                Text = "刷新",
                Width = 100,
                Height = 30
            };
            _refreshButton.Click += async (s, e) => await RefreshDeviceListAsync();

            _fixButton = new Button
            {
                Text = "尝试修复选中设备",
                Width = 150,
                Height = 30,
                Enabled = false
            };
            _fixButton.Click += async (s, e) => await FixSelectedDeviceAsync();

            var btnShowProblematic = new Button
            {
                Text = "仅显示问题设备",
                Width = 130,
                Height = 30
            };
            btnShowProblematic.Click += async (s, e) => await ShowProblematicDevicesAsync();

            buttonPanel.Controls.AddRange(new Control[] { _refreshButton, _fixButton, btnShowProblematic });
            layout.Controls.Add(buttonPanel, 0, 0);

            // 设备列表
            _deviceListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            _deviceListView.Columns.Add("设备名称", 250);
            _deviceListView.Columns.Add("类型", 100);
            _deviceListView.Columns.Add("制造商", 150);
            _deviceListView.Columns.Add("状态", 80);
            _deviceListView.Columns.Add("错误信息", 300);
            _deviceListView.SelectedIndexChanged += DeviceListView_SelectedIndexChanged;

            layout.Controls.Add(_deviceListView, 0, 1);

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
            layout.Controls.Add(detailsPanel, 0, 2);

            Controls.Add(layout);
        }

        private async Task RefreshDeviceListAsync()
        {
            _refreshButton.Enabled = false;
            _deviceListView.Items.Clear();

            try
            {
                var devices = await Task.Run(() => _healthChecker.CheckAllDevicesHealth());

                foreach (var device in devices.OrderBy(d => d.Status))
                {
                    var item = new ListViewItem(device.Name);
                    item.SubItems.Add(device.PNPClass);
                    item.SubItems.Add(device.Manufacturer);
                    item.SubItems.Add(GetStatusText(device.Status));
                    item.SubItems.Add(device.ErrorDescription);
                    item.Tag = device;

                    // 根据状态设置颜色
                    switch (device.Status)
                    {
                        case DeviceHealthStatus.Error:
                            item.ForeColor = Color.Red;
                            item.BackColor = Color.LightPink;
                            break;
                        case DeviceHealthStatus.Warning:
                            item.ForeColor = Color.Orange;
                            item.BackColor = Color.LightYellow;
                            break;
                        case DeviceHealthStatus.Disabled:
                            item.ForeColor = Color.Gray;
                            break;
                        case DeviceHealthStatus.Normal:
                            item.ForeColor = Color.Green;
                            break;
                    }

                    _deviceListView.Items.Add(item);
                }

                int problemCount = devices.Count(d => d.Status != DeviceHealthStatus.Normal);
                if (problemCount > 0)
                {
                    MessageBox.Show($"检测到 {problemCount} 个设备存在问题！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"刷新设备列表失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _refreshButton.Enabled = true;
            }
        }

        private async Task ShowProblematicDevicesAsync()
        {
            _deviceListView.Items.Clear();

            try
            {
                var devices = await Task.Run(() => _healthChecker.GetProblematicDevices());

                foreach (var device in devices)
                {
                    var item = new ListViewItem(device.Name);
                    item.SubItems.Add(device.PNPClass);
                    item.SubItems.Add(device.Manufacturer);
                    item.SubItems.Add(GetStatusText(device.Status));
                    item.SubItems.Add(device.ErrorDescription);
                    item.Tag = device;

                    if (device.Status == DeviceHealthStatus.Error)
                    {
                        item.ForeColor = Color.Red;
                        item.BackColor = Color.LightPink;
                    }
                    else if (device.Status == DeviceHealthStatus.Warning)
                    {
                        item.ForeColor = Color.Orange;
                        item.BackColor = Color.LightYellow;
                    }

                    _deviceListView.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"获取问题设备失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeviceListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_deviceListView.SelectedItems.Count > 0)
            {
                var device = _deviceListView.SelectedItems[0].Tag as DeviceHealthInfo;
                if (device != null)
                {
                    ShowDeviceDetails(device);
                    _fixButton.Enabled = device.Status != DeviceHealthStatus.Normal;
                }
            }
            else
            {
                _fixButton.Enabled = false;
            }
        }

        private void ShowDeviceDetails(DeviceHealthInfo device)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"设备名称: {device.Name}");
            sb.AppendLine($"设备ID: {device.DeviceId}");
            sb.AppendLine($"硬件ID: {device.HardwareId}");
            sb.AppendLine($"设备类型: {device.PNPClass}");
            sb.AppendLine($"制造商: {device.Manufacturer}");
            sb.AppendLine($"服务: {device.Service}");
            sb.AppendLine();
            sb.AppendLine($"健康状态: {GetStatusText(device.Status)}");
            sb.AppendLine($"错误代码: {device.ErrorCode}");
            sb.AppendLine($"错误描述: {device.ErrorDescription}");
            sb.AppendLine($"设备存在: {(device.IsPresent ? "是" : "否")}");
            sb.AppendLine();
            sb.AppendLine("驱动信息:");
            sb.AppendLine($"  驱动版本: {device.DriverInfo.DriverVersion}");
            sb.AppendLine($"  驱动日期: {device.DriverInfo.DriverDate}");
            sb.AppendLine($"  驱动提供商: {device.DriverInfo.DriverProvider}");
            sb.AppendLine($"  已安装: {(device.DriverInfo.IsInstalled ? "是" : "否")}");
            sb.AppendLine($"  已签名: {(device.DriverInfo.IsSigned ? "是" : "否")}");
            sb.AppendLine($"  最新: {(device.DriverInfo.IsUpToDate ? "是" : "否")}");
            sb.AppendLine();
            sb.AppendLine("设备能力:");
            sb.AppendLine($"  可禁用: {(device.Capabilities.CanBeDisabled ? "是" : "否")}");
            sb.AppendLine($"  可移除: {(device.Capabilities.CanBeRemoved ? "是" : "否")}");
            sb.AppendLine($"  电源管理: {(device.Capabilities.HasPowerManagement ? "是" : "否")}");
            sb.AppendLine($"  支持唤醒: {(device.Capabilities.SupportsWakeUp ? "是" : "否")}");

            _detailsTextBox.Text = sb.ToString();
        }

        private async Task FixSelectedDeviceAsync()
        {
            if (_deviceListView.SelectedItems.Count == 0) return;

            var device = _deviceListView.SelectedItems[0].Tag as DeviceHealthInfo;
            if (device == null) return;

            var result = MessageBox.Show(
                $"尝试修复设备: {device.Name}?\n\n修复操作可能包括:\n- 重新安装驱动程序\n- 启用设备\n- 重置设备状态",
                "确认修复",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                try
                {
                    // 这里可以添加具体的修复逻辑
                    MessageBox.Show("修复功能需要管理员权限执行设备管理器操作。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await RefreshDeviceListAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"修复失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string GetStatusText(DeviceHealthStatus status)
        {
            return status switch
            {
                DeviceHealthStatus.Normal => "正常",
                DeviceHealthStatus.Warning => "警告",
                DeviceHealthStatus.Error => "错误",
                DeviceHealthStatus.Disabled => "已禁用",
                _ => "未知"
            };
        }
    }
}
