using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;
using HardwareDiagnostics.Hardware;

namespace HardwareDiagnostics.UI
{
    public partial class HardwareTestForm : Form
    {
        private readonly HardwareTester _tester;
        private ListView _testListView;
        private ProgressBar _progressBar;
        private TextBox _logTextBox;
        private Button _startAllButton;
        private Button _startSelectedButton;

        public HardwareTestForm()
        {
            _tester = new HardwareTester();
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Text = "硬件测试中心";
            Size = new Size(900, 700);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 9F);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 4,
                ColumnCount = 1
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // 按钮面板
            var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill };
            _startAllButton = new Button
            {
                Text = "测试所有硬件",
                Width = 120,
                Height = 35,
                BackColor = Color.LightBlue
            };
            _startAllButton.Click += async (s, e) => await RunAllTestsAsync();

            _startSelectedButton = new Button
            {
                Text = "测试选中项目",
                Width = 120,
                Height = 35,
                Enabled = false
            };
            _startSelectedButton.Click += async (s, e) => await RunSelectedTestAsync();

            buttonPanel.Controls.AddRange(new Control[] { _startAllButton, _startSelectedButton });
            layout.Controls.Add(buttonPanel, 0, 0);

            // 测试列表
            _testListView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                CheckBoxes = true
            };
            _testListView.Columns.Add("选择", 50);
            _testListView.Columns.Add("设备", 150);
            _testListView.Columns.Add("测试类型", 150);
            _testListView.Columns.Add("状态", 100);
            _testListView.Columns.Add("结果", 300);

            // 添加测试项
            AddTestItem("鼠标", HardwareTestType.Mouse, "检测鼠标设备和功能");
            AddTestItem("触控板", HardwareTestType.Touchpad, "检测触控板状态");
            AddTestItem("显卡", HardwareTestType.GraphicsCard, "检测显卡和驱动");
            AddTestItem("键盘", HardwareTestType.Keyboard, "检测键盘设备");
            AddTestItem("音频", HardwareTestType.Audio, "检测音频设备");
            AddTestItem("网络", HardwareTestType.Network, "检测网络适配器");

            _testListView.ItemChecked += (s, e) => UpdateButtonState();
            _testListView.SelectedIndexChanged += (s, e) => UpdateButtonState();

            layout.Controls.Add(_testListView, 0, 1);

            // 进度条
            _progressBar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Style = ProgressBarStyle.Continuous,
                Minimum = 0,
                Maximum = 100
            };
            layout.Controls.Add(_progressBar, 0, 2);

            // 日志面板
            _logTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F)
            };
            layout.Controls.Add(_logTextBox, 0, 3);

            Controls.Add(layout);
        }

        private void AddTestItem(string deviceName, HardwareTestType testType, string description)
        {
            var item = new ListViewItem("");
            item.SubItems.Add(deviceName);
            item.SubItems.Add(description);
            item.SubItems.Add("待测试");
            item.SubItems.Add("");
            item.Tag = testType;
            _testListView.Items.Add(item);
        }

        private void UpdateButtonState()
        {
            bool hasSelection = _testListView.CheckedItems.Count > 0 || _testListView.SelectedItems.Count > 0;
            _startSelectedButton.Enabled = hasSelection;
        }

        private async Task RunAllTestsAsync()
        {
            _startAllButton.Enabled = false;
            _startSelectedButton.Enabled = false;
            _logTextBox.Clear();

            var progress = new Progress<TestProgress>(p =>
            {
                _progressBar.Value = Math.Min(p.Percentage, 100);
                LogMessage(p.Message);
            });

            try
            {
                var results = await _tester.RunAllTestsAsync(progress);

                // 更新列表显示结果
                for (int i = 0; i < results.Count && i < _testListView.Items.Count; i++)
                {
                    UpdateTestResult(_testListView.Items[i], results[i]);
                }

                // 显示总结
                int passed = results.Count(r => r.Status == TestStatus.Passed);
                int failed = results.Count(r => r.Status == TestStatus.Failed);
                int warning = results.Count(r => r.Status == TestStatus.Warning);

                MessageBox.Show(
                    $"测试完成!\n通过: {passed}\n失败: {failed}\n警告: {warning}",
                    "测试结果",
                    MessageBoxButtons.OK,
                    failed > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试执行失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _startAllButton.Enabled = true;
                UpdateButtonState();
                _progressBar.Value = 0;
            }
        }

        private async Task RunSelectedTestAsync()
        {
            var selectedItems = _testListView.CheckedItems.Count > 0
                ? _testListView.CheckedItems.Cast<ListViewItem>().ToList()
                : _testListView.SelectedItems.Cast<ListViewItem>().ToList();

            if (selectedItems.Count == 0) return;

            _startAllButton.Enabled = false;
            _startSelectedButton.Enabled = false;
            _logTextBox.Clear();

            var progress = new Progress<TestProgress>(p =>
            {
                _progressBar.Value = Math.Min(p.Percentage, 100);
                LogMessage(p.Message);
            });

            try
            {
                foreach (var item in selectedItems)
                {
                    if (item.Tag is HardwareTestType testType)
                    {
                        item.SubItems[3].Text = "测试中...";
                        TestResult result = testType switch
                        {
                            HardwareTestType.Mouse => await _tester.TestMouseAsync(progress),
                            HardwareTestType.Touchpad => await _tester.TestTouchpadAsync(progress),
                            HardwareTestType.GraphicsCard => await _tester.TestGraphicsCardAsync(progress),
                            HardwareTestType.Keyboard => await _tester.TestKeyboardAsync(progress),
                            HardwareTestType.Audio => await _tester.TestAudioAsync(progress),
                            HardwareTestType.Network => await _tester.TestNetworkAsync(progress),
                            _ => new TestResult { Status = TestStatus.Failed, ErrorMessage = "未知测试类型" }
                        };
                        UpdateTestResult(item, result);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"测试执行失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _startAllButton.Enabled = true;
                UpdateButtonState();
                _progressBar.Value = 0;
            }
        }

        private void UpdateTestResult(ListViewItem item, TestResult result)
        {
            item.SubItems[3].Text = GetTestStatusText(result.Status);
            item.SubItems[4].Text = result.ErrorMessage ?? result.Details ?? "";

            switch (result.Status)
            {
                case TestStatus.Passed:
                    item.ForeColor = Color.Green;
                    item.BackColor = Color.LightGreen;
                    break;
                case TestStatus.Warning:
                    item.ForeColor = Color.Orange;
                    item.BackColor = Color.LightYellow;
                    break;
                case TestStatus.Failed:
                    item.ForeColor = Color.Red;
                    item.BackColor = Color.LightPink;
                    break;
            }

            if (result.Recommendations.Count > 0)
            {
                LogMessage($"建议: {string.Join(", ", result.Recommendations)}");
            }
        }

        private void LogMessage(string message)
        {
            if (InvokeRequired)
            {
                Invoke(new Action<string>(LogMessage), message);
                return;
            }

            _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            _logTextBox.ScrollToCaret();
        }

        private string GetTestStatusText(TestStatus status)
        {
            return status switch
            {
                TestStatus.Passed => "通过",
                TestStatus.Warning => "警告",
                TestStatus.Failed => "失败",
                _ => "未知"
            };
        }
    }
}
