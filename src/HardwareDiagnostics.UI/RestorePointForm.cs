using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.System;

namespace HardwareDiagnostics.UI
{
    /// <summary>
    /// 还原点窗体 - 顶部“原理”先讲透，按钮只做三件事，每步都可复制等价 PowerShell 命令自己敲。
    /// </summary>
    public class RestorePointForm : Form
    {
        private readonly RestorePointManager _manager = new();
        private TextBox _output;
        private ProgressBar _progress;

        public RestorePointForm()
        {
            Text = "系统还原点 🛟";
            Size = new Size(860, 620);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 9F);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

            var principle = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.LightYellow,
                Text = RestorePointManager.GetPrinciple()
            };
            layout.Controls.Add(principle, 0, 0);

            var btns = new FlowLayoutPanel { Dock = DockStyle.Fill };
            btns.Controls.Add(MakeBtn("1. 启用保护", async p => await _manager.EnableProtectionAsync(p)));
            btns.Controls.Add(MakeBtn("2. 创建还原点", CreateWithDialogAsync));
            btns.Controls.Add(MakeBtn("刷新列表", async p => await _manager.ListRestorePointsAsync(p)));
            var btnRestore = new Button { Text = "打开系统还原…", Width = 140, Height = 34, Margin = new Padding(5) };
            btnRestore.Click += (s, e) =>
            {
                try { _manager.OpenSystemRestore(); }
                catch (Exception ex) { MessageBox.Show(this, "打开失败：" + ex.Message, "提示"); }
            };
            btns.Controls.Add(btnRestore);
            var btnCopy = new Button { Text = "📋 复制手工命令", Width = 150, Height = 34, Margin = new Padding(5) };
            btnCopy.Click += (s, e) =>
            {
                try { Clipboard.SetText(RestorePointManager.GetManualCommands()); ToastNotifier.Success("已复制", "手工命令已进剪贴板，去管理员 PowerShell 粘贴即可。"); }
                catch (Exception ex) { MessageBox.Show(this, "复制失败：" + ex.Message, "提示"); }
            };
            btns.Controls.Add(btnCopy);
            layout.Controls.Add(btns, 0, 1);

            _output = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F),
                BackColor = Color.Black,
                ForeColor = Color.LightGreen
            };
            layout.Controls.Add(_output, 0, 2);

            _progress = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous };
            layout.Controls.Add(_progress, 0, 3);

            Controls.Add(layout);
            Shown += async (s, e) => await RunAsync(p => _manager.ListRestorePointsAsync(p));
        }

        private Button MakeBtn(string text, Func<IProgress<string>, Task<DismOperationResult>> action)
        {
            var b = new Button { Text = text, Width = 140, Height = 34, Margin = new Padding(5) };
            b.Click += async (s, e) => await RunAsync(action);
            return b;
        }

        private async Task<DismOperationResult> CreateWithDialogAsync(IProgress<string> p)
        {
            string? name = InputDialog.Show("创建还原点", "给这个快照起个名（比如：删驱动前）：", "CPCH_" + DateTime.Now.ToString("MMdd_HHmm"));
            if (string.IsNullOrWhiteSpace(name))
                return new DismOperationResult { Success = false, Error = "用户取消" };
            return await _manager.CreateRestorePointAsync(name, p);
        }

        private async Task RunAsync(Func<IProgress<string>, Task<DismOperationResult>> action)
        {
            _output.Clear();
            _progress.Style = ProgressBarStyle.Marquee;
            var progress = new Progress<string>(m => { _output.AppendText(m + Environment.NewLine); _output.ScrollToCaret(); });
            try
            {
                var r = await action(progress);
                _progress.Style = ProgressBarStyle.Continuous;
                _progress.Value = r.Success ? 100 : 0;
                if (!r.Success && !string.IsNullOrEmpty(r.Error)) _output.AppendText("错误: " + r.Error + Environment.NewLine);
                _output.AppendText($"操作{(r.Success ? "成功" : "失败")}，耗时 {r.Duration.TotalSeconds:F1} 秒" + Environment.NewLine);
                if (r.Success) ToastNotifier.Success("还原点", "操作成功。");
                else ToastNotifier.Warning("还原点", "操作失败，详见输出。可能需要管理员权限或系统保护被组策略关闭。");
            }
            catch (Exception ex)
            {
                _progress.Style = ProgressBarStyle.Continuous;
                _output.AppendText("异常: " + ex.Message + Environment.NewLine);
            }
        }
    }
}
