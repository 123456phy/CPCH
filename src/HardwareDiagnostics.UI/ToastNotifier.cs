using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.UI
{
    /// <summary>
    /// 桌面右下角 Toast 弹窗 - 无需 Win10 SDK，纯 WinForms 实现，XP 也能跑。
    /// 特性：队列串行、自动消失、点击即关、超时不堆积、受设置总开关控制。
    /// </summary>
    public static class ToastNotifier
    {
        private static readonly Queue<ToastItem> _queue = new();
        private static ToastForm? _current;
        private static readonly object _lock = new();

        public static void Info(string title, string message) => Show(title, message, Color.DodgerBlue);
        public static void Success(string title, string message) => Show(title, message, Color.SeaGreen);
        public static void Warning(string title, string message) => Show(title, message, Color.DarkOrange);
        public static void Error(string title, string message) => Show(title, message, Color.Crimson);

        public static void Show(string title, string message, Color accent)
        {
            try
            {
                if (!AppSettings.Current.EnableToast) return;
                lock (_lock)
                {
                    // 队列太长说明用户不在看，丢掉旧的，只留最近 3 条
                    while (_queue.Count >= 3) _queue.Dequeue();
                    _queue.Enqueue(new ToastItem(title, message, accent));
                    if (_current == null)
                        ShowNext();
                }
            }
            catch { }
        }

        private static void ShowNext()
        {
            if (_queue.Count == 0) { _current = null; return; }
            var item = _queue.Dequeue();
            _current = new ToastForm(item, OnToastClosed);
            try { _current.Show(); }
            catch { _current = null; ShowNext(); }
        }

        private static void OnToastClosed()
        {
            lock (_lock)
            {
                _current = null;
                ShowNext();
            }
        }

        private class ToastItem
        {
            public string Title;
            public string Message;
            public Color Accent;
            public ToastItem(string t, string m, Color c) { Title = t; Message = m; Accent = c; }
        }

        private class ToastForm : Form
        {
            private readonly Action _onClosed;
            private readonly Timer _timer;
            private int _ticksLeft;

            public ToastForm(ToastItem item, Action onClosed)
            {
                _onClosed = onClosed;
                FormBorderStyle = FormBorderStyle.None;
                ShowInTaskbar = false;
                TopMost = true;
                StartPosition = FormStartPosition.Manual;
                Size = new Size(320, 110);
                BackColor = Color.FromArgb(45, 45, 48);
                Opacity = 0.94;
                Font = new Font("Microsoft YaHei", 9F);

                var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
                Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);

                var accent = new Panel { Dock = DockStyle.Left, Width = 6, BackColor = item.Accent };
                var title = new Label
                {
                    Text = item.Title,
                    Dock = DockStyle.Top,
                    Height = 30,
                    ForeColor = Color.White,
                    Font = new Font("Microsoft YaHei", 10F, FontStyle.Bold),
                    Padding = new Padding(10, 6, 10, 0)
                };
                var body = new Label
                {
                    Text = item.Message,
                    Dock = DockStyle.Fill,
                    ForeColor = Color.Gainsboro,
                    Padding = new Padding(10, 0, 10, 6)
                };
                Controls.Add(body);
                Controls.Add(title);
                Controls.Add(accent);

                Click += (s, e) => Close();
                title.Click += (s, e) => Close();
                body.Click += (s, e) => Close();

                _ticksLeft = Math.Max(2, AppSettings.Current.ToastSeconds) * 2;
                _timer = new Timer { Interval = 500 };
                _timer.Tick += (s, e) =>
                {
                    if (--_ticksLeft <= 0) Close();
                };
                _timer.Start();
            }

            protected override void OnFormClosed(FormClosedEventArgs e)
            {
                try { _timer.Stop(); _timer.Dispose(); } catch { }
                base.OnFormClosed(e);
                try { _onClosed(); } catch { }
            }

            // 无边框窗口支持拖动
            protected override void WndProc(ref Message m)
            {
                const int WM_NCHITTEST = 0x84;
                const int HTCAPTION = 2;
                base.WndProc(ref m);
                if (m.Msg == WM_NCHITTEST) m.Result = (IntPtr)HTCAPTION;
            }
        }
    }
}
