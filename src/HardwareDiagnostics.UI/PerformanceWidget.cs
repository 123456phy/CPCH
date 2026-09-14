using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.UI
{
    /// <summary>
    /// 桌面小组件 - 常驻右上的迷你性能条（CPU / 内存），2 秒一刷，开销可忽略。
    /// 可拖动、透明、可置顶、可随主程序启动，右键菜单操作。
    /// </summary>
    public class PerformanceWidget : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private readonly Label _cpuLabel;
        private readonly Label _ramLabel;
        private readonly ProgressBar _cpuBar;
        private readonly ProgressBar _ramBar;
        private readonly Timer _timer;
        private PerformanceCounter? _cpuCounter;
        private bool _firstRead = true;

        public PerformanceWidget()
        {
            Text = "CPCH 性能";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(230, 96);
            BackColor = Color.FromArgb(30, 30, 30);
            Opacity = 0.88;
            TopMost = AppSettings.Current.WidgetTopMost;
            Font = new Font("Microsoft YaHei", 9F);

            var area = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1024, 768);
            Location = new Point(area.Right - Width - 12, 12);

            var title = new Label
            {
                Text = "CPCH 性能  ✕",
                Dock = DockStyle.Top,
                Height = 22,
                ForeColor = Color.WhiteSmoke,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Microsoft YaHei", 9F, FontStyle.Bold)
            };
            title.Click += (s, e) =>
            {
                // 点右上角 ✕ 区域关闭
                if (Cursor.Position.X > PointToScreen(new Point(Width - 30, 0)).X) Close();
            };

            _cpuLabel = new Label { Dock = DockStyle.Top, Height = 20, ForeColor = Color.LightGreen, Padding = new Padding(8, 0, 0, 0), Text = "CPU: --" };
            _cpuBar = new ProgressBar { Dock = DockStyle.Top, Height = 8, Minimum = 0, Maximum = 100 };
            _ramLabel = new Label { Dock = DockStyle.Top, Height = 20, ForeColor = Color.LightSkyBlue, Padding = new Padding(8, 0, 0, 0), Text = "内存: --" };
            _ramBar = new ProgressBar { Dock = DockStyle.Top, Height = 8, Minimum = 0, Maximum = 100 };

            Controls.Add(_ramBar);
            Controls.Add(_ramLabel);
            Controls.Add(_cpuBar);
            Controls.Add(_cpuLabel);
            Controls.Add(title);

            var menu = new ContextMenuStrip();
            var miTop = new ToolStripMenuItem("总在最前") { Checked = TopMost, CheckOnClick = false };
            miTop.Click += (s, e) => { TopMost = !TopMost; miTop.Checked = TopMost; };
            var miClose = new ToolStripMenuItem("关闭小组件", null, (s, e) => Close());
            menu.Items.Add(miTop);
            menu.Items.Add(miClose);
            ContextMenuStrip = menu;

            try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); } catch { }

            _timer = new Timer { Interval = 2000 };
            _timer.Tick += (s, e) => RefreshStats();
            _timer.Start();
            RefreshStats();
        }

        private void RefreshStats()
        {
            try
            {
                // CPU：第一次读数恒为 0，直接跳过显示
                if (_cpuCounter != null)
                {
                    float v = _cpuCounter.NextValue();
                    if (_firstRead) { _firstRead = false; }
                    else
                    {
                        int cpu = Math.Max(0, Math.Min(100, (int)v));
                        _cpuLabel.Text = $"CPU: {cpu}%";
                        _cpuBar.Value = cpu;
                    }
                }

                var st = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)) };
                if (GlobalMemoryStatusEx(ref st))
                {
                    int pct = (int)st.dwMemoryLoad;
                    double usedGB = (st.ullTotalPhys - st.ullAvailPhys) / 1073741824.0;
                    double totalGB = st.ullTotalPhys / 1073741824.0;
                    _ramLabel.Text = $"内存: {pct}%  {usedGB:F1}/{totalGB:F1} GB";
                    _ramBar.Value = Math.Max(0, Math.Min(100, pct));
                }
            }
            catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            try { _timer.Stop(); _timer.Dispose(); } catch { }
            try { _cpuCounter?.Dispose(); } catch { }
            base.OnFormClosed(e);
        }

        // 无边框拖动
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x84;
            const int HTCAPTION = 2;
            base.WndProc(ref m);
            if (m.Msg == WM_NCHITTEST) m.Result = (IntPtr)HTCAPTION;
        }
    }
}
