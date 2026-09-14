using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Models;
using HardwareDiagnostics.Core.Utils;
using HardwareDiagnostics.Hardware;
using HardwareDiagnostics.Monitoring;

namespace HardwareDiagnostics.UI
{
    /// <summary>
    /// 一键体检报告 - 10 秒内汇总全身数据。教学点：每一节都标注“数据来源 + 你自己也能看的对等命令”，
    /// 用户拿着报告不但能求助，还能顺藤摸瓜学会自己查。
    /// </summary>
    public class HealthReportForm : Form
    {
        private TextBox _preview;

        private class Section
        {
            public string Title = "";
            public string Source = "";
            public List<string> Lines = new();
        }

        public HealthReportForm()
        {
            Text = "一键体检报告 🩺";
            Size = new Size(860, 640);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Microsoft YaHei", 9F);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            var tip = new Label
            {
                Dock = DockStyle.Fill,
                BackColor = Color.LightYellow,
                Padding = new Padding(10, 8, 10, 8),
                Text = "10 秒出一份全身报告。每节【来源】告诉你数据从哪来、对等命令是什么——照着敲，你也能亲手查到同样的数据。"
            };
            layout.Controls.Add(tip, 0, 0);

            _preview = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9F)
            };
            layout.Controls.Add(_preview, 0, 1);

            var btns = new FlowLayoutPanel { Dock = DockStyle.Fill };
            var btnGen = new Button { Text = "生成报告", Width = 130, Height = 34, Margin = new Padding(5) };
            btnGen.Click += async (s, e) => await GenerateAsync();
            var btnCopy = new Button { Text = "📋 复制求助", Width = 130, Height = 34, Margin = new Padding(5) };
            btnCopy.Click += (s, e) =>
            {
                try { Clipboard.SetText(_preview.Text); ToastNotifier.Success("已复制", "报告已进剪贴板，发帖求助时粘贴即可。"); }
                catch (Exception ex) { MessageBox.Show(this, "复制失败：" + ex.Message, "提示"); }
            };
            var btnSave = new Button { Text = "保存 HTML…", Width = 130, Height = 34, Margin = new Padding(5) };
            btnSave.Click += (s, e) => SaveHtml();
            btns.Controls.Add(btnGen);
            btns.Controls.Add(btnCopy);
            btns.Controls.Add(btnSave);
            layout.Controls.Add(btns, 0, 2);

            Controls.Add(layout);
            Shown += async (s, e) => await GenerateAsync();
        }

        private async Task GenerateAsync()
        {
            _preview.Text = "正在体检，请稍候…";
            var sections = await Task.Run(() => Collect());
            _preview.Text = RenderText(sections);
        }

        private List<Section> Collect()
        {
            var list = new List<Section>();

            // 1. 系统概要
            var s1 = new Section
            {
                Title = "一、系统概要",
                Source = "来源：System.Environment（.NET 运行时直读）。对等命令：任务管理器→性能，或 PowerShell：$PSVersionTable；systeminfo"
            };
            try
            {
                s1.Lines.Add($"计算机名：{Environment.MachineName}  用户名：{Environment.UserName}");
                s1.Lines.Add($"系统：{Environment.OSVersion}  64 位系统：{Environment.Is64BitOperatingSystem}  64 位进程：{Environment.Is64BitProcess}");
                s1.Lines.Add($"开机时长：{TimeSpan.FromMilliseconds(Environment.TickCount):d\\天\\ h\\小\\时\\ m\\分}  处理器数：{Environment.ProcessorCount}");
            }
            catch (Exception ex) { s1.Lines.Add("采集失败：" + ex.Message); }
            list.Add(s1);

            // 2. CPU / 内存
            var s2 = new Section
            {
                Title = "二、CPU 与内存",
                Source = "来源：WMI Win32_Processor / Win32_ComputerSystem。对等命令：Get-CimInstance Win32_Processor | Select Name,MaxClockSpeed"
            };
            try
            {
                using var cpu = new ManagementObjectSearcher("SELECT Name,MaxClockSpeed,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor");
                foreach (ManagementObject m in cpu.Get())
                    s2.Lines.Add($"CPU：{m["Name"]}  主频：{m["MaxClockSpeed"]}MHz  物理核心：{m["NumberOfCores"]}  逻辑：{m["NumberOfLogicalProcessors"]}");
                using var cs = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                foreach (ManagementObject m in cs.Get())
                    s2.Lines.Add($"物理内存：{Convert.ToDouble(m["TotalPhysicalMemory"]) / 1073741824:F1} GB");
                s2.Lines.Add($"本程序当前占用：{MemoryOptimizer.GetMemoryUsageText()}（目标 <150MB）");
            }
            catch (Exception ex) { s2.Lines.Add("采集失败：" + ex.Message); }
            list.Add(s2);

            // 3. 磁盘
            var s3 = new Section
            {
                Title = "三、磁盘",
                Source = "来源：System.IO.DriveInfo。对等命令：Get-PSDrive -PSProvider FileSystem；此电脑→右键属性"
            };
            try
            {
                foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    double total = d.TotalSize / 1073741824.0, free = d.TotalFreeSpace / 1073741824.0;
                    s3.Lines.Add($"{d.Name} {d.VolumeLabel}  总：{total:F0}GB  可用：{free:F0}GB（{(free / total * 100):F0}%）  格式：{d.DriveFormat}");
                }
            }
            catch (Exception ex) { s3.Lines.Add("采集失败：" + ex.Message); }
            list.Add(s3);

            // 4. 设备异常
            var s4 = new Section
            {
                Title = "四、异常设备",
                Source = "来源：本程序设备管理器扫描（WMI Win32_PnPEntity）。对等操作：devmgmt.msc 看黄色叹号"
            };
            try
            {
                var bad = new DeviceManager().GetDevicesWithProblems();
                if (bad.Count == 0) s4.Lines.Add("全部正常，无异常设备。");
                foreach (var h in bad.Take(20))
                    s4.Lines.Add($"[{h.Status}] {h.Name}（{h.Description}）");
                if (bad.Count > 20) s4.Lines.Add($"……共 {bad.Count} 个，只显示前 20。");
            }
            catch (Exception ex) { s4.Lines.Add("采集失败：" + ex.Message); }
            list.Add(s4);

            // 5. 蓝屏
            var s5 = new Section
            {
                Title = "五、近期蓝屏（最多 5 条）",
                Source = "来源：系统事件日志 + C:\\Windows\\Minidump。对等操作：事件查看器→Windows 日志→系统，筛 BugCheck"
            };
            try
            {
                using var detector = new BSODDetector();
                var h = detector.GetBSODHistory().OrderByDescending(b => b.CrashTime).Take(5).ToList();
                if (h.Count == 0) s5.Lines.Add("无蓝屏记录。");
                foreach (var b in h)
                    s5.Lines.Add($"{b.CrashTime:yyyy-MM-dd HH:mm}  {b.BugCheckCode} {b.BugCheckString}  元凶：{b.CausedByDriver}");
            }
            catch (Exception ex) { s5.Lines.Add("采集失败：" + ex.Message); }
            list.Add(s5);

            // 6. 求助指引
            var s6 = new Section
            {
                Title = "六、求助时带上这些",
                Source = "来源：Windows 固定路径。把下面路径的文件一起打包，发帖基本一次就能定位"
            };
            s6.Lines.Add("DISM 日志：C:\\Windows\\Logs\\DISM\\dism.log");
            s6.Lines.Add("SFC 日志：C:\\Windows\\Logs\\CBS\\CBS.log");
            s6.Lines.Add("蓝屏转储：C:\\Windows\\Minidump\\（*.dmp）");
            s6.Lines.Add("本程序日志：程序目录 Logs\\ + SecurityLogs\\");
            list.Add(s6);

            return list;
        }

        private string RenderText(List<Section> sections)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"CPCH 一键体检报告（{DateTime.Now:yyyy-MM-dd HH:mm}）");
            sb.AppendLine(new string('=', 60));
            foreach (var s in sections)
            {
                sb.AppendLine();
                sb.AppendLine(s.Title);
                sb.AppendLine("【" + s.Source + "】");
                foreach (var l in s.Lines) sb.AppendLine("  " + l);
            }
            return sb.ToString();
        }

        private void SaveHtml()
        {
            if (string.IsNullOrWhiteSpace(_preview.Text))
            {
                MessageBox.Show(this, "先点“生成报告”。", "提示");
                return;
            }
            using var dlg = new SaveFileDialog { Filter = "网页文件|*.html", FileName = "CPCH_体检报告.html" };
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var sb = new StringBuilder();
                sb.Append("<html><head><meta charset='utf-8'><title>CPCH 体检报告</title></head><body style='font-family:微软雅黑;background:#f5f5f5'>");
                sb.Append("<h2>CPCH 一键体检报告（").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm")).Append("）</h2>");
                foreach (var line in _preview.Text.Split('\n'))
                {
                    string esc = global::System.Net.WebUtility.HtmlEncode(line);
                    if (line.StartsWith("一、") || line.StartsWith("二、") || line.StartsWith("三、") || line.StartsWith("四、") || line.StartsWith("五、") || line.StartsWith("六、"))
                        sb.Append("<h3>").Append(esc).Append("</h3>");
                    else if (line.StartsWith("【"))
                        sb.Append("<p style='color:#666;font-size:13px'>").Append(esc).Append("</p>");
                    else
                        sb.Append("<div>").Append(esc).Append("</div>");
                }
                sb.Append("</body></html>");
                File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                ToastNotifier.Success("已保存", dlg.FileName);
            }
            catch (Exception ex) { MessageBox.Show(this, "保存失败：" + ex.Message, "提示"); }
        }
    }
}
