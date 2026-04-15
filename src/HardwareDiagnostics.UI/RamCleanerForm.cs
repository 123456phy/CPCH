using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.UI
{
    public partial class RamCleanerForm : Form
    {
        private RamCleaner _ramCleaner;
        private Label _memoryUsageLabel;
        private Label _availableMemoryLabel;
        private ProgressBar _memoryProgressBar;
        private Button _btnCleanNow;
        private Button _btnStartMonitor;
        private Button _btnStopMonitor;
        private ListView _processListView;
        private Timer _updateTimer;

        public RamCleanerForm()
        {
            _ramCleaner = new RamCleaner();
            InitializeComponent();
            SetupTimer();
            SetupRamCleanerEvents();
        }

        private void InitializeComponent()
        {
            this.Text = "RAM 内存清理";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            // 内存状态面板
            var statusPanel = CreateMemoryStatusPanel();
            mainLayout.Controls.Add(statusPanel, 0, 0);

            // 控制按钮
            var controlPanel = CreateControlPanel();
            mainLayout.Controls.Add(controlPanel, 0, 1);

            // 进程列表
            _processListView = CreateProcessListView();
            mainLayout.Controls.Add(_processListView, 0, 2);

            // 状态栏
            var statusLabel = new Label
            {
                Text = "提示：自动清理内存占用超过 80% 时的无用进程",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Blue
            };
            mainLayout.Controls.Add(statusLabel, 0, 3);

            this.Controls.Add(mainLayout);
        }

        private Panel CreateMemoryStatusPanel()
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
                ColumnCount = 2,
                RowCount = 3
            };

            _memoryUsageLabel = new Label
            {
                Text = "内存使用：0 MB",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F, FontStyle.Bold)
            };
            layout.Controls.Add(_memoryUsageLabel, 0, 0);

            _availableMemoryLabel = new Label
            {
                Text = "可用内存：0 MB",
                Dock = DockStyle.Fill,
                Font = new Font("Microsoft YaHei", 11F, FontStyle.Bold)
            };
            layout.Controls.Add(_availableMemoryLabel, 1, 0);

            _memoryProgressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Height = 30
            };
            layout.Controls.Add(_memoryProgressBar, 0, 1);
            layout.SetColumnSpan(_memoryProgressBar, 2);

            panel.Controls.Add(layout);
            return panel;
        }

        private FlowLayoutPanel CreateControlPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnCleanNow = new Button
            {
                Text = "立即清理",
                Width = 100,
                Height = 35,
                BackColor = Color.LightGreen
            };
            _btnCleanNow.Click += (s, e) =>
            {
                _ramCleaner.CleanNow();
                UpdateMemoryStatus();
                MessageBox.Show("内存清理完成！", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            _btnStartMonitor = new Button
            {
                Text = "启动监控",
                Width = 100,
                Height = 35,
                BackColor = Color.LightBlue
            };
            _btnStartMonitor.Click += (s, e) =>
            {
                _ramCleaner.StartMonitoring(10);
                _btnStartMonitor.Enabled = false;
                _btnStopMonitor.Enabled = true;
            };

            _btnStopMonitor = new Button
            {
                Text = "停止监控",
                Width = 100,
                Height = 35,
                BackColor = Color.LightCoral,
                Enabled = false
            };
            _btnStopMonitor.Click += (s, e) =>
            {
                _ramCleaner.StopMonitoring();
                _btnStartMonitor.Enabled = true;
                _btnStopMonitor.Enabled = false;
            };

            panel.Controls.Add(_btnCleanNow);
            panel.Controls.Add(_btnStartMonitor);
            panel.Controls.Add(_btnStopMonitor);

            return panel;
        }

        private ListView CreateProcessListView()
        {
            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            listView.Columns.Add("进程名", 150);
            listView.Columns.Add("PID", 80);
            listView.Columns.Add("内存占用 (MB)", 120);
            listView.Columns.Add("占用百分比", 100);

            return listView;
        }

        private void SetupTimer()
        {
            _updateTimer = new Timer { Interval = 2000 };
            _updateTimer.Tick += (s, e) =>
            {
                UpdateMemoryStatus();
                UpdateProcessList();
            };
            _updateTimer.Start();
        }

        private void SetupRamCleanerEvents()
        {
            _ramCleaner.RamCleaned += (s, e) =>
            {
                MessageBox.Show($"已清理进程 {e.ProcessName} 的内存，释放 {e.CleanedMemoryMB} MB", "内存清理", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
        }

        private void UpdateMemoryStatus()
        {
            try
            {
                var memInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
                var availableMB = (int)(memInfo.AvailablePhysicalMemory / (1024 * 1024));
                var totalMB = GetTotalPhysicalMemoryMB();
                var usedMB = totalMB - availableMB;
                var usagePercent = (usedMB * 100) / totalMB;

                _memoryUsageLabel.Text = $"已用内存：{usedMB} MB";
                _availableMemoryLabel.Text = $"可用内存：{availableMB} MB";
                _memoryProgressBar.Value = Math.Min(100, usagePercent);

                if (usagePercent >= 80)
                    _memoryProgressBar.ForeColor = Color.Red;
                else if (usagePercent >= 60)
                    _memoryProgressBar.ForeColor = Color.Orange;
                else
                    _memoryProgressBar.ForeColor = Color.Green;
            }
            catch { }
        }

        private int GetTotalPhysicalMemoryMB()
        {
            try
            {
                var memInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
                return (int)(memInfo.TotalPhysicalMemory / (1024 * 1024));
            }
            catch
            {
                return 4096; // 默认 4GB
            }
        }

        private void UpdateProcessList()
        {
            _processListView.Items.Clear();
            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                try
                {
                    var memoryMB = process.WorkingSet64 / (1024 * 1024);
                    if (memoryMB >= 50) // 只显示占用超过 50MB 的进程
                    {
                        var item = new ListViewItem(process.ProcessName);
                        item.SubItems.Add(process.Id.ToString());
                        item.SubItems.Add(memoryMB.ToString());
                        item.SubItems.Add("0%"); // 简化处理
                        _processListView.Items.Add(item);

                        if (item.SubItems[2].ForeColor != Color.Red && memoryMB > 500)
                        {
                            item.ForeColor = Color.Red;
                        }
                    }
                }
                catch { }
                finally
                {
                    process.Dispose();
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _ramCleaner.Dispose();
            _updateTimer.Stop();
            _updateTimer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
