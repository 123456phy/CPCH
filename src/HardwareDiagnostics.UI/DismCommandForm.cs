using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;
using HardwareDiagnostics.System;

namespace HardwareDiagnostics.UI
{
    public partial class DismCommandForm : Form
    {
        private readonly DismManager _dismManager;
        private TextBox _outputTextBox;
        private ProgressBar _progressBar;
        private Label _statusLabel;

        public DismCommandForm()
        {
            _dismManager = new DismManager();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "DISM快捷命令";
            Size = new Size(1000, 700);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 9F);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

            // 命令按钮面板
            var commandPanel = new TabControl { Dock = DockStyle.Fill };

            // 系统维护标签页
            var maintenancePage = CreateCommandPage(new List<DismCommandItem>
            {
                new DismCommandItem("扫描健康", async (p) => await _dismManager.ScanHealthAsync(p)),
                new DismCommandItem("检查健康", async (p) => await _dismManager.CheckHealthAsync(p)),
                new DismCommandItem("修复系统", async (p) => await _dismManager.RestoreHealthAsync(p)),
                new DismCommandItem("快速扫描修复", async (p) => await _dismManager.QuickScanAndRepairAsync(p)),
                new DismCommandItem("分析组件", async (p) => await _dismManager.AnalyzeComponentStoreAsync(p)),
                new DismCommandItem("清理组件", async (p) => await _dismManager.StartComponentCleanupAsync(p)),
                new DismCommandItem("深度清理", async (p) => await _dismManager.DeepCleanupAsync(p)),
                new DismCommandItem("重置基础", async (p) => await _dismManager.StartComponentCleanupResetBaseAsync(p))
            });
            commandPanel.TabPages.Add(maintenancePage);

            // 功能管理标签页
            var featurePage = CreateCommandPage(new List<DismCommandItem>
            {
                new DismCommandItem("列出功能", async (p) => await _dismManager.GetFeaturesAsync(p)),
                new DismCommandItem("启用.NET 3.5", async (p) => await _dismManager.EnableFeatureAsync("NetFx3", p)),
                new DismCommandItem("启用Hyper-V", async (p) => await _dismManager.EnableFeatureAsync("Microsoft-Hyper-V", p)),
                new DismCommandItem("启用WSL", async (p) => await _dismManager.EnableFeatureAsync("Microsoft-Windows-Subsystem-Linux", p)),
                new DismCommandItem("启用沙盒", async (p) => await _dismManager.EnableFeatureAsync("Containers-DisposableClientVM", p)),
                new DismCommandItem("启用Telnet", async (p) => await _dismManager.EnableFeatureAsync("TelnetClient", p)),
                new DismCommandItem("启用IIS", async (p) => await _dismManager.EnableFeatureAsync("IIS-WebServerRole", p))
            });
            commandPanel.TabPages.Add(featurePage);

            // 驱动管理标签页
            var driverPage = CreateCommandPage(new List<DismCommandItem>
            {
                new DismCommandItem("列出驱动", async (p) => await _dismManager.GetDriversAsync(p)),
                new DismCommandItem("导出所有驱动", async (p) => await ExportDriversWithDialog(p)),
                new DismCommandItem("添加驱动", async (p) => await AddDriverWithDialog(p)),
                new DismCommandItem("移除驱动", async (p) => await RemoveDriverWithDialog(p))
            });
            commandPanel.TabPages.Add(driverPage);

            // 高级命令标签页
            var advancedPage = CreateCommandPage(new List<DismCommandItem>
            {
                new DismCommandItem("获取包列表", async (p) => await _dismManager.GetPackagesAsync(p)),
                new DismCommandItem("获取国际设置", async (p) => await _dismManager.GetIntlSettingsAsync(p)),
                new DismCommandItem("清理挂载点", async (p) => await _dismManager.CleanupMountPointsAsync(p)),
                new DismCommandItem("完整清理修复", async (p) => await _dismManager.CleanupImageAsync(p)),
                new DismCommandItem("重置更新组件", async (p) => await _dismManager.ResetWindowsUpdateComponentsAsync(p))
            });
            commandPanel.TabPages.Add(advancedPage);

            maintenancePage.Text = "系统维护";
            featurePage.Text = "功能管理";
            driverPage.Text = "驱动管理";
            advancedPage.Text = "高级命令";

            layout.Controls.Add(commandPanel, 0, 0);

            // 状态面板
            var statusPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            _statusLabel = new Label
            {
                Text = "就绪",
                AutoSize = true,
                Padding = new Padding(5)
            };
            statusPanel.Controls.Add(_statusLabel);
            layout.Controls.Add(statusPanel, 0, 1);

            // 输出面板
            _outputTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                BackColor = Color.Black,
                ForeColor = Color.LightGreen
            };
            layout.Controls.Add(_outputTextBox, 0, 2);

            // 进度条
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100
            };
            layout.Controls.Add(_progressBar, 0, 3);

            Controls.Add(layout);
        }

        private TabPage CreateCommandPage(List<DismCommandItem> commands)
        {
            var page = new TabPage();
            var flowLayout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            foreach (var cmd in commands)
            {
                var btn = new Button
                {
                    Text = cmd.Name,
                    Width = 130,
                    Height = 35,
                    Margin = new Padding(5)
                };
                btn.Click += async (s, e) => await ExecuteCommandAsync(cmd.Action);
                flowLayout.Controls.Add(btn);
            }

            page.Controls.Add(flowLayout);
            return page;
        }

        private async Task ExecuteCommandAsync(Func<IProgress<string>, Task<DismOperationResult>> command)
        {
            _outputTextBox.Clear();
            _progressBar.Style = ProgressBarStyle.Marquee;
            _progressBar.MarqueeAnimationSpeed = 30;

            var progress = new Progress<string>(msg =>
            {
                _outputTextBox.AppendText(msg + Environment.NewLine);
                _outputTextBox.ScrollToCaret();
                _statusLabel.Text = msg;
            });

            try
            {
                var result = await command(progress);

                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = result.Success ? 100 : 0;

                if (!result.Success && !string.IsNullOrEmpty(result.Error))
                {
                    _outputTextBox.AppendText($"错误: {result.Error}" + Environment.NewLine);
                }

                _outputTextBox.AppendText($"操作{(result.Success ? "成功" : "失败")}，耗时: {result.Duration.TotalSeconds:F1}秒" + Environment.NewLine);
                _statusLabel.Text = $"操作{(result.Success ? "成功" : "失败")}";
            }
            catch (Exception ex)
            {
                _progressBar.Style = ProgressBarStyle.Continuous;
                _progressBar.Value = 0;
                _outputTextBox.AppendText($"执行异常: {ex.Message}" + Environment.NewLine);
                _statusLabel.Text = "执行失败";
            }
        }

        private async Task<DismOperationResult> ExportDriversWithDialog(IProgress<string> progress)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "选择驱动导出目录"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return await _dismManager.ExportAllDriversAsync(dialog.SelectedPath, progress);
            }

            return new DismOperationResult { Success = false, Error = "用户取消" };
        }

        private async Task<DismOperationResult> AddDriverWithDialog(IProgress<string> progress)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "驱动文件|*.inf|所有文件|*.*",
                Title = "选择驱动文件"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                return await _dismManager.AddDriverAsync(dialog.FileName, false, progress);
            }

            return new DismOperationResult { Success = false, Error = "用户取消" };
        }

        private async Task<DismOperationResult> RemoveDriverWithDialog(IProgress<string> progress)
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "驱动文件|*.inf|所有文件|*.*",
                Title = "选择要移除的驱动文件"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                var result = MessageBox.Show(
                    "确定要移除这个驱动吗？这可能会影响系统稳定性。",
                    "确认移除",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    return await _dismManager.RemoveDriverAsync(dialog.FileName, progress);
                }
            }

            return new DismOperationResult { Success = false, Error = "用户取消" };
        }
    }

    public class DismCommandItem
    {
        public string Name { get; }
        public Func<IProgress<string>, Task<DismOperationResult>> Action { get; }

        public DismCommandItem(string name, Func<IProgress<string>, Task<DismOperationResult>> action)
        {
            Name = name;
            Action = action;
        }
    }
}
