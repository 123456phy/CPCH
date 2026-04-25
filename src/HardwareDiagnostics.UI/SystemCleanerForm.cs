using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.System;

namespace HardwareDiagnostics.UI
{
    public partial class SystemCleanerForm : Form
    {
        private SystemCleaner _cleaner;
        private ListView _itemsListView;
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private Label _totalSizeLabel;
        private Button _btnScan;
        private Button _btnClean;
        private Button _btnCancel;
        private CancellationTokenSource? _cancellationTokenSource;

        public SystemCleanerForm()
        {
            _cleaner = new SystemCleaner();
            InitializeComponent();
            SetupEvents();
        }

        private void InitializeComponent()
        {
            this.Text = "系统垃圾清理";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            // 标题和安全提示
            var headerPanel = CreateHeaderPanel();
            mainLayout.Controls.Add(headerPanel, 0, 0);

            // 清理项目列表
            _itemsListView = CreateItemsListView();
            mainLayout.Controls.Add(_itemsListView, 0, 1);

            // 进度条
            var progressPanel = CreateProgressPanel();
            mainLayout.Controls.Add(progressPanel, 0, 2);

            // 状态栏
            var statusPanel = CreateStatusPanel();
            mainLayout.Controls.Add(statusPanel, 0, 3);

            // 按钮面板
            var buttonPanel = CreateButtonPanel();
            mainLayout.Controls.Add(buttonPanel, 0, 4);

            this.Controls.Add(mainLayout);
        }

        private Panel CreateHeaderPanel()
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
                ColumnCount = 1,
                RowCount = 2
            };

            var titleLabel = new Label
            {
                Text = "🧹 系统垃圾清理",
                Font = new Font("Microsoft YaHei", 14, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(titleLabel, 0, 0);

            var safetyLabel = new Label
            {
                Text = "✅ 安全清理：只清理临时文件和缓存，绝不清理注册表或系统关键文件",
                Font = new Font("Microsoft YaHei", 9),
                ForeColor = Color.Green,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(safetyLabel, 0, 1);

            panel.Controls.Add(layout);
            return panel;
        }

        private ListView CreateItemsListView()
        {
            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = true
            };
            listView.Columns.Add("清理项目", 200);
            listView.Columns.Add("描述", 300);
            listView.Columns.Add("大小", 100);
            listView.Columns.Add("安全等级", 80);

            return listView;
        }

        private Panel CreateProgressPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill
            };

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 25,
                Minimum = 0,
                Maximum = 100
            };
            panel.Controls.Add(_progressBar);

            _statusLabel = new Label
            {
                Text = "就绪",
                Dock = DockStyle.Bottom,
                Height = 20,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(_statusLabel);

            return panel;
        }

        private Panel CreateStatusPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill
            };

            _totalSizeLabel = new Label
            {
                Text = "可清理空间：0 MB",
                Font = new Font("Microsoft YaHei", 10, FontStyle.Bold),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(_totalSizeLabel);

            return panel;
        }

        private FlowLayoutPanel CreateButtonPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnScan = new Button
            {
                Text = "扫描垃圾",
                Width = 120,
                Height = 40,
                BackColor = Color.LightBlue
            };
            _btnScan.Click += async (s, e) => await ScanItemsAsync();

            _btnClean = new Button
            {
                Text = "开始清理",
                Width = 120,
                Height = 40,
                BackColor = Color.LightGreen,
                Enabled = false
            };
            _btnClean.Click += async (s, e) => await CleanItemsAsync();

            _btnCancel = new Button
            {
                Text = "取消",
                Width = 100,
                Height = 40,
                BackColor = Color.LightCoral,
                Enabled = false
            };
            _btnCancel.Click += (s, e) => CancelClean();

            var btnSelectAll = new Button
            {
                Text = "全选",
                Width = 80,
                Height = 40
            };
            btnSelectAll.Click += (s, e) =>
            {
                foreach (ListViewItem item in _itemsListView.Items)
                {
                    item.Checked = true;
                }
            };

            var btnDeselectAll = new Button
            {
                Text = "全不选",
                Width = 80,
                Height = 40
            };
            btnDeselectAll.Click += (s, e) =>
            {
                foreach (ListViewItem item in _itemsListView.Items)
                {
                    item.Checked = false;
                }
            };

            panel.Controls.Add(_btnScan);
            panel.Controls.Add(_btnClean);
            panel.Controls.Add(_btnCancel);
            panel.Controls.Add(btnSelectAll);
            panel.Controls.Add(btnDeselectAll);

            return panel;
        }

        private void SetupEvents()
        {
            _cleaner.ProgressChanged += (s, e) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() =>
                    {
                        _progressBar.Value = e.ProgressPercentage;
                        _statusLabel.Text = $"正在清理：{e.CurrentItem} ({e.ItemsCompleted}/{e.TotalItems})";
                    }));
                }
                else
                {
                    _progressBar.Value = e.ProgressPercentage;
                    _statusLabel.Text = $"正在清理：{e.CurrentItem} ({e.ItemsCompleted}/{e.TotalItems})";
                }
            };

            _cleaner.CleanCompleted += (s, e) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => OnCleanCompleted(e.Result)));
                }
                else
                {
                    OnCleanCompleted(e.Result);
                }
            };
        }

        private async Task ScanItemsAsync()
        {
            _btnScan.Enabled = false;
            _statusLabel.Text = "正在扫描...";
            _progressBar.Style = ProgressBarStyle.Marquee;

            await Task.Run(() =>
            {
                var items = _cleaner.GetCleanableItems();
                long totalSize = 0;

                Invoke(new Action(() =>
                {
                    _itemsListView.Items.Clear();

                    foreach (var item in items)
                    {
                        var listItem = new ListViewItem(item.Name);
                        listItem.SubItems.Add(item.Description);
                        listItem.SubItems.Add(FormatBytes(item.SizeBytes));
                        listItem.SubItems.Add(item.SafeLevel.ToString());
                        listItem.Tag = item;

                        // 根据安全等级设置颜色
                        if (item.SafeLevel == SafetyLevel.Warning)
                        {
                            listItem.ForeColor = Color.Orange;
                        }
                        else
                        {
                            listItem.ForeColor = Color.Green;
                        }

                        _itemsListView.Items.Add(listItem);
                        totalSize += item.SizeBytes;
                    }

                    _totalSizeLabel.Text = $"可清理空间：{FormatBytes(totalSize)}";
                    _btnClean.Enabled = true;
                    _statusLabel.Text = "扫描完成";
                    _progressBar.Style = ProgressBarStyle.Continuous;
                    _progressBar.Value = 0;
                }));
            });

            _btnScan.Enabled = true;
        }

        private async Task CleanItemsAsync()
        {
            var selectedItems = new List<string>();
            foreach (ListViewItem item in _itemsListView.Items)
            {
                if (item.Checked)
                {
                    selectedItems.Add(item.Text);
                }
            }

            if (selectedItems.Count == 0)
            {
                MessageBox.Show("请至少选择一个要清理的项目", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 确认对话框
            var result = MessageBox.Show(
                $"确定要清理选中的 {selectedItems.Count} 个项目吗？\n\n" +
                "注意：清理后的文件将无法恢复！",
                "确认清理",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
            {
                return;
            }

            _btnClean.Enabled = false;
            _btnScan.Enabled = false;
            _btnCancel.Enabled = true;
            _cancellationTokenSource = new CancellationTokenSource();

            var cleanResult = await _cleaner.CleanAsync(selectedItems, _cancellationTokenSource.Token);
        }

        private void OnCleanCompleted(CleanResult result)
        {
            _btnClean.Enabled = true;
            _btnScan.Enabled = true;
            _btnCancel.Enabled = false;
            _progressBar.Value = 100;

            if (result.WasCancelled)
            {
                _statusLabel.Text = "清理已取消";
                MessageBox.Show("清理操作已取消", "取消", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (result.Success)
            {
                _statusLabel.Text = $"清理完成，释放了 {FormatBytes(result.TotalCleanedBytes)}";
                
                var message = $"清理完成！\n\n" +
                    $"成功清理：{result.SuccessfulItems.Count} 个项目\n" +
                    $"释放空间：{FormatBytes(result.TotalCleanedBytes)}\n" +
                    $"耗时：{result.Duration.TotalSeconds:F1} 秒";

                if (result.FailedItems.Count > 0)
                {
                    message += $"\n\n失败项目：{result.FailedItems.Count} 个";
                }

                MessageBox.Show(message, "清理完成", MessageBoxButtons.OK, MessageBoxIcon.Information);

                // 重新扫描
                _ = ScanItemsAsync();
            }
            else
            {
                _statusLabel.Text = "清理失败";
                MessageBox.Show("清理过程中出现错误，请重试", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CancelClean()
        {
            _cleaner.Cancel();
            _btnCancel.Enabled = false;
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;

            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }

            return $"{size:F2} {sizes[order]}";
        }
    }
}
