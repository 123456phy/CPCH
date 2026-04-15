using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.UI
{
    public partial class DownloadManagerForm : Form
    {
        private DownloadManager _downloadManager;
        private ConcurrentDownloader _concurrentDownloader;
        private ListView _downloadListView;
        private TextBox _urlTextBox;
        private TextBox _outputPathTextBox;
        private NumericUpDown _threadCountNumeric;
        private ComboBox _toolComboBox;
        private Button _btnStartDownload;
        private Button _btnCancelDownload;
        private Button _btnBrowse;
        private ProgressBar _progressBar;
        private Label _statusLabel;
        private CancellationTokenSource? _cancellationTokenSource;

        public DownloadManagerForm()
        {
            _downloadManager = new DownloadManager();
            _concurrentDownloader = new ConcurrentDownloader();
            InitializeComponent();
            InitializeDownloadTools();
            SetupEvents();
        }

        private void InitializeComponent()
        {
            this.Text = "下载管理器";
            this.Size = new Size(900, 650);
            this.StartPosition = FormStartPosition.CenterParent;

            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(10)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            // 设置面板
            var settingsPanel = CreateSettingsPanel();
            mainLayout.Controls.Add(settingsPanel, 0, 0);

            // 按钮面板
            var buttonPanel = CreateButtonPanel();
            mainLayout.Controls.Add(buttonPanel, 0, 1);

            // 下载列表
            _downloadListView = CreateDownloadListView();
            mainLayout.Controls.Add(_downloadListView, 0, 2);

            // 状态栏
            var statusPanel = CreateStatusPanel();
            mainLayout.Controls.Add(statusPanel, 0, 3);

            this.Controls.Add(mainLayout);
        }

        private Panel CreateSettingsPanel()
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle
            };

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            // URL
            layout.Controls.Add(new Label { Text = "下载 URL:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 0);
            _urlTextBox = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(_urlTextBox, 1, 0);

            // 保存路径
            layout.Controls.Add(new Label { Text = "保存路径:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 1);
            var pathPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            _outputPathTextBox = new TextBox { Width = 400 };
            _btnBrowse = new Button { Text = "浏览...", Width = 60 };
            _btnBrowse.Click += (s, e) =>
            {
                using var dialog = new SaveFileDialog();
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _outputPathTextBox.Text = dialog.FileName;
                }
            };
            pathPanel.Controls.Add(_outputPathTextBox);
            pathPanel.Controls.Add(_btnBrowse);
            layout.Controls.Add(pathPanel, 1, 1);

            // 下载工具
            layout.Controls.Add(new Label { Text = "下载工具:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 2);
            _toolComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            layout.Controls.Add(_toolComboBox, 1, 2);

            // 线程数
            layout.Controls.Add(new Label { Text = "并发线程:", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleRight }, 0, 3);
            _threadCountNumeric = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 256,
                Value = 8,
                Width = 100
            };
            layout.Controls.Add(_threadCountNumeric, 1, 3);

            panel.Controls.Add(layout);
            return panel;
        }

        private Panel CreateButtonPanel()
        {
            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnStartDownload = new Button
            {
                Text = "开始下载",
                Width = 100,
                Height = 35,
                BackColor = Color.LightGreen
            };
            _btnStartDownload.Click += async (s, e) => await StartDownloadAsync();

            _btnCancelDownload = new Button
            {
                Text = "取消下载",
                Width = 100,
                Height = 35,
                BackColor = Color.LightCoral,
                Enabled = false
            };
            _btnCancelDownload.Click += (s, e) => CancelDownload();

            var btnDetectTools = new Button
            {
                Text = "检测下载工具",
                Width = 120,
                Height = 35
            };
            btnDetectTools.Click += (s, e) => DetectDownloadTools();

            panel.Controls.Add(_btnStartDownload);
            panel.Controls.Add(_btnCancelDownload);
            panel.Controls.Add(btnDetectTools);

            return panel;
        }

        private ListView CreateDownloadListView()
        {
            var listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            listView.Columns.Add("文件名", 200);
            listView.Columns.Add("URL", 300);
            listView.Columns.Add("进度", 100);
            listView.Columns.Add("大小", 80);
            listView.Columns.Add("状态", 150);

            return listView;
        }

        private Panel CreateStatusPanel()
        {
            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1
            };

            _statusLabel = new Label
            {
                Text = "就绪",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(_statusLabel, 0, 0);

            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 100
            };
            panel.Controls.Add(_progressBar, 1, 0);

            return panel;
        }

        private void InitializeDownloadTools()
        {
            _toolComboBox.Items.Add("内置下载器 (.NET)");
            _toolComboBox.Items.Add("curl");
            _toolComboBox.Items.Add("wget");
            _toolComboBox.Items.Add("aria2");
            _toolComboBox.Items.Add("自定义工具");
            _toolComboBox.SelectedIndex = 0;

            DetectDownloadTools();
        }

        private void SetupEvents()
        {
            _downloadManager.ProgressChanged += (s, e) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => UpdateProgress(e.ProgressPercentage)));
                }
                else
                {
                    UpdateProgress(e.ProgressPercentage);
                }
            };

            _downloadManager.DownloadCompleted += (s, e) =>
            {
                if (InvokeRequired)
                {
                    Invoke(new Action(() => DownloadFinished(e.Success, e.OutputPath, e.ErrorMessage)));
                }
                else
                {
                    DownloadFinished(e.Success, e.OutputPath, e.ErrorMessage);
                }
            };
        }

        private void DetectDownloadTools()
        {
            var detection = DownloadManager.DetectAvailableTools();
            
            var items = _toolComboBox.Items.Cast<string>().ToList();
            items.Clear();

            items.Add("内置下载器 (.NET)");
            if (detection.CurlAvailable) items.Add("curl ✓");
            else items.Add("curl (未找到)");
            
            if (detection.WgetAvailable) items.Add("wget ✓");
            else items.Add("wget (未找到)");
            
            if (detection.Aria2Available) items.Add("aria2 ✓");
            else items.Add("aria2 (未找到)");
            
            items.Add("自定义工具");

            _toolComboBox.Items.Clear();
            _toolComboBox.Items.AddRange(items.ToArray());
            _toolComboBox.SelectedIndex = 0;

            _statusLabel.Text = $"检测完成 - curl: {(detection.CurlAvailable ? "可用" : "不可用")}, wget: {(detection.WgetAvailable ? "可用" : "不可用")}";
        }

        private async Task StartDownloadAsync()
        {
            if (string.IsNullOrEmpty(_urlTextBox.Text))
            {
                MessageBox.Show("请输入下载 URL", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _btnStartDownload.Enabled = false;
            _btnCancelDownload.Enabled = true;
            _cancellationTokenSource = new CancellationTokenSource();

            var selectedTool = _toolComboBox.SelectedIndex switch
            {
                1 => DownloadTool.Curl,
                2 => DownloadTool.Wget,
                3 => DownloadTool.Aria2,
                4 => DownloadTool.Custom,
                _ => DownloadTool.BuiltIn
            };

            try
            {
                _downloadManager.SetDownloadTool(selectedTool);
                _downloadManager.SetMaxThreads((int)_threadCountNumeric.Value);

                var result = await _downloadManager.DownloadAsync(_urlTextBox.Text, _outputPathTextBox.Text, _cancellationTokenSource.Token);

                if (result.Success)
                {
                    MessageBox.Show($"下载完成！\n文件：{result.OutputPath}\n大小：{result.BytesDownloaded / 1024 / 1024} MB", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"下载失败：{result.ErrorMessage}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"下载出错：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnStartDownload.Enabled = true;
                _btnCancelDownload.Enabled = false;
            }
        }

        private void CancelDownload()
        {
            _cancellationTokenSource?.Cancel();
            _statusLabel.Text = "下载已取消";
        }

        private void UpdateProgress(int percentage)
        {
            _progressBar.Value = percentage;
            _statusLabel.Text = $"下载进度：{percentage}%";
        }

        private void DownloadFinished(bool success, string? path, string? error)
        {
            _btnStartDownload.Enabled = true;
            _btnCancelDownload.Enabled = false;

            if (success)
            {
                _statusLabel.Text = $"下载完成：{path}";
            }
            else
            {
                _statusLabel.Text = $"下载失败：{error}";
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _cancellationTokenSource?.Dispose();
            _concurrentDownloader?.Dispose();
            base.OnFormClosing(e);
        }
    }
}
