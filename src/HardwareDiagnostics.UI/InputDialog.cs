using System;
using System.Drawing;
using System.Windows.Forms;

namespace HardwareDiagnostics.UI
{
    /// <summary>通用单行输入框（替代 InputBox，不引入额外依赖）。返回 null 表示取消。</summary>
    public static class InputDialog
    {
        public static string? Show(string title, string label, string defaultValue = "")
        {
            using var form = new Form
            {
                Text = title,
                Size = new Size(480, 170),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                Font = new Font("Microsoft YaHei", 9F)
            };
            var lbl = new Label { Text = label, Dock = DockStyle.Top, Height = 30, Padding = new Padding(10, 8, 10, 0) };
            var txt = new TextBox { Text = defaultValue, Dock = DockStyle.Top, Margin = new Padding(10) };
            // 用 Panel 包一下留边距
            var pad = new Panel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(10, 6, 10, 0) };
            txt.Dock = DockStyle.Fill;
            pad.Controls.Add(txt);
            var btns = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 45, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 6, 10, 6) };
            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Width = 90 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Width = 90 };
            btns.Controls.Add(ok);
            btns.Controls.Add(cancel);
            form.Controls.Add(btns);
            form.Controls.Add(pad);
            form.Controls.Add(lbl);
            form.AcceptButton = ok;
            form.CancelButton = cancel;
            return form.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
        }
    }
}
