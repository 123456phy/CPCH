using System;
using System.Drawing;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;
using Microsoft.Win32;

namespace HardwareDiagnostics.UI
{
    /// <summary>
    /// 设置页 - 常规 / 通知 / 监控 / DISM 四组，保存后写 %AppData%\CPCH\settings.xml，
    /// 开机自启写 HKCU Run，重启程序即生效。
    /// </summary>
    public class SettingsForm : Form
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunName = "CPCH";

        private CheckBox _cbAutoStart;
        private CheckBox _cbMinToTray;
        private CheckBox _cbStartMinimized;
        private CheckBox _cbToast;
        private CheckBox _cbBalloon;
        private NumericUpDown _numToastSec;
        private NumericUpDown _numRamInterval;
        private CheckBox _cbWidgetOnStart;
        private TextBox _txtDismSource;

        public SettingsForm()
        {
            Text = "设置 ⚙️";
            Size = new Size(520, 560);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildGeneralPage());
            tabs.TabPages.Add(BuildNotifyPage());
            tabs.TabPages.Add(BuildMonitorPage());
            tabs.TabPages.Add(BuildDismPage());
            Controls.Add(tabs);

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 50 };
            var btnSave = new Button { Text = "保存", Width = 100, Height = 32, Anchor = AnchorStyles.Right, Left = 290, Top = 9 };
            var btnCancel = new Button { Text = "取消", Width = 100, Height = 32, Anchor = AnchorStyles.Right, Left = 398, Top = 9 };
            btnSave.Click += (s, e) => { SaveAll(); DialogResult = DialogResult.OK; Close(); };
            btnCancel.Click += (s, e) => Close();
            bottom.Controls.Add(btnSave);
            bottom.Controls.Add(btnCancel);
            Controls.Add(bottom);

            LoadAll();
        }

        private TabPage BuildGeneralPage()
        {
            var page = new TabPage("常规");
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(14), AutoScroll = true };
            _cbAutoStart = new CheckBox { Text = "开机自动启动", Width = 440, Height = 30 };
            _cbMinToTray = new CheckBox { Text = "关闭时最小化到托盘（而不是退出）", Width = 440, Height = 30 };
            _cbStartMinimized = new CheckBox { Text = "启动时最小化到托盘", Width = 440, Height = 30 };
            var tip = new Label { Text = "托盘图标：双击还原，右键可退出。", Width = 440, Height = 40, ForeColor = Color.Gray };
            layout.Controls.Add(_cbAutoStart);
            layout.Controls.Add(_cbMinToTray);
            layout.Controls.Add(_cbStartMinimized);
            layout.Controls.Add(tip);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildNotifyPage()
        {
            var page = new TabPage("通知");
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(14) };
            _cbToast = new CheckBox { Text = "启用桌面右下角 Toast 弹窗", Width = 440, Height = 30 };
            _cbBalloon = new CheckBox { Text = "启用托盘气泡提示", Width = 440, Height = 30 };
            var lbl = new Label { Text = "Toast 显示时长（秒）：", Width = 440, Height = 26 };
            _numToastSec = new NumericUpDown { Minimum = 2, Maximum = 30, Width = 120 };
            var btnTest = new Button { Text = "发送一条测试通知", Width = 200, Height = 32 };
            btnTest.Click += (s, e) => ToastNotifier.Info("测试通知", "如果你看到这条，说明桌面弹窗工作正常。");
            layout.Controls.Add(_cbToast);
            layout.Controls.Add(_cbBalloon);
            layout.Controls.Add(lbl);
            layout.Controls.Add(_numToastSec);
            layout.Controls.Add(btnTest);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildMonitorPage()
        {
            var page = new TabPage("监控");
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(14) };
            var lbl = new Label { Text = "RAM 监控间隔（秒，最小 5）：", Width = 440, Height = 26 };
            _numRamInterval = new NumericUpDown { Minimum = 5, Maximum = 3600, Width = 120 };
            _cbWidgetOnStart = new CheckBox { Text = "启动时自动显示桌面小组件", Width = 440, Height = 30 };
            layout.Controls.Add(lbl);
            layout.Controls.Add(_numRamInterval);
            layout.Controls.Add(_cbWidgetOnStart);
            page.Controls.Add(layout);
            return page;
        }

        private TabPage BuildDismPage()
        {
            var page = new TabPage("DISM");
            var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(14) };
            var lbl = new Label { Text = "默认离线修复源（ISO 挂载后的盘符，如 X:\\sources\\install.wim）：", Width = 440, Height = 40 };
            _txtDismSource = new TextBox { Width = 440 };
            var btnBrowse = new Button { Text = "浏览 ISO 挂载盘…", Width = 200, Height = 32 };
            btnBrowse.Click += (s, e) =>
            {
                using var dlg = new OpenFileDialog { Filter = "安装映像|install.wim;install.esd|所有文件|*.*", Title = "选择离线修复源" };
                if (dlg.ShowDialog(this) == DialogResult.OK) _txtDismSource.Text = dlg.FileName;
            };
            layout.Controls.Add(lbl);
            layout.Controls.Add(_txtDismSource);
            layout.Controls.Add(btnBrowse);
            page.Controls.Add(layout);
            return page;
        }

        private void LoadAll()
        {
            var s = AppSettings.Current;
            _cbAutoStart.Checked = IsAutoStartEnabled();
            _cbMinToTray.Checked = s.MinimizeToTray;
            _cbStartMinimized.Checked = s.StartMinimized;
            _cbToast.Checked = s.EnableToast;
            _cbBalloon.Checked = s.EnableTrayBalloon;
            _numToastSec.Value = Math.Max(2, Math.Min(30, s.ToastSeconds));
            _numRamInterval.Value = Math.Max(5, Math.Min(3600, s.RamCheckIntervalSeconds));
            _cbWidgetOnStart.Checked = s.ShowWidgetOnStart;
            _txtDismSource.Text = s.DismSourcePath ?? "";
        }

        private void SaveAll()
        {
            var s = AppSettings.Current;
            s.MinimizeToTray = _cbMinToTray.Checked;
            s.StartMinimized = _cbStartMinimized.Checked;
            s.EnableToast = _cbToast.Checked;
            s.EnableTrayBalloon = _cbBalloon.Checked;
            s.ToastSeconds = (int)_numToastSec.Value;
            s.RamCheckIntervalSeconds = (int)_numRamInterval.Value;
            s.ShowWidgetOnStart = _cbWidgetOnStart.Checked;
            s.DismSourcePath = _txtDismSource.Text.Trim();
            s.AutoStartWithWindows = _cbAutoStart.Checked;
            s.Save();
            SetAutoStart(_cbAutoStart.Checked);
            Logger.Info("设置已保存");
        }

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
                return key?.GetValue(RunName) != null;
            }
            catch { return false; }
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
                if (key == null) return;
                if (enable)
                    key.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                else
                    key.DeleteValue(RunName, false);
            }
            catch (Exception ex)
            {
                Logger.Warning($"设置开机自启失败：{ex.Message}");
            }
        }
    }
}
