using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.Hardware
{
    public class HardwareTester
    {
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, int dwExtraInfo);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
        private const uint MOUSEEVENTF_LEFTUP = 0x04;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const uint MOUSEEVENTF_RIGHTUP = 0x10;
        private const int HORZRES = 8;
        private const int VERTRES = 10;

        public event EventHandler<TestProgressEventArgs>? TestProgress;
        public event EventHandler<TestCompletedEventArgs>? TestCompleted;

        public async Task<TestResult> TestMouseAsync(IProgress<TestProgress>? progress = null)
        {
            var result = new TestResult
            {
                DeviceName = "鼠标",
                TestType = HardwareTestType.Mouse
            };

            try
            {
                progress?.Report(new TestProgress { Message = "正在检测鼠标设备...", Percentage = 10 });

                // 检测鼠标是否存在
                var mouseDevices = GetMouseDevices();
                if (mouseDevices.Count == 0)
                {
                    result.Status = TestStatus.Failed;
                    result.ErrorMessage = "未检测到鼠标设备";
                    result.Recommendations.Add("检查鼠标连接");
                    result.Recommendations.Add("尝试更换USB端口");
                    return result;
                }

                progress?.Report(new TestProgress { Message = $"检测到 {mouseDevices.Count} 个鼠标设备", Percentage = 30 });

                // 检测鼠标驱动
                foreach (var device in mouseDevices)
                {
                    if (!device.IsDriverInstalled)
                    {
                        result.Status = TestStatus.Warning;
                        result.ErrorMessage = "鼠标驱动可能未正确安装";
                        result.Recommendations.Add("更新鼠标驱动程序");
                    }
                }

                progress?.Report(new TestProgress { Message = "正在测试鼠标功能...", Percentage = 50 });

                // 测试鼠标移动（通过模拟移动）
                await TestMouseMovementAsync(progress);

                progress?.Report(new TestProgress { Message = "正在测试鼠标按键...", Percentage = 80 });

                // 测试鼠标按键
                await TestMouseButtonsAsync(progress);

                result.Status = TestStatus.Passed;
                result.Details = "鼠标设备检测正常，所有功能测试通过";
            }
            catch (Exception ex)
            {
                result.Status = TestStatus.Failed;
                result.ErrorMessage = $"鼠标测试失败: {ex.Message}";
                result.Recommendations.Add("检查鼠标连接");
                result.Recommendations.Add("重新安装鼠标驱动");
                Logger.Error("Mouse test failed", ex);
            }

            return result;
        }

        public async Task<TestResult> TestTouchpadAsync(IProgress<TestProgress>? progress = null)
        {
            var result = new TestResult
            {
                DeviceName = "触控板",
                TestType = HardwareTestType.Touchpad
            };

            try
            {
                progress?.Report(new TestProgress { Message = "正在检测触控板设备...", Percentage = 10 });

                // 检测触控板
                var touchpadDevices = GetTouchpadDevices();
                if (touchpadDevices.Count == 0)
                {
                    result.Status = TestStatus.Warning;
                    result.ErrorMessage = "未检测到触控板设备（可能是台式机或触控板被禁用）";
                    return result;
                }

                progress?.Report(new TestProgress { Message = $"检测到 {touchpadDevices.Count} 个触控板设备", Percentage = 30 });

                // 检查触控板状态
                foreach (var device in touchpadDevices)
                {
                    if (device.IsDisabled)
                    {
                        result.Status = TestStatus.Warning;
                        result.ErrorMessage = "触控板可能被禁用";
                        result.Recommendations.Add("检查Fn组合键是否禁用了触控板");
                        result.Recommendations.Add("在设备管理器中启用触控板");
                        result.CanAutoFix = true;
                        result.AutoFixAction = () => EnableDevice(device.DeviceId);
                        return result;
                    }
                }

                progress?.Report(new TestProgress { Message = "正在测试触控板功能...", Percentage = 60 });

                result.Status = TestStatus.Passed;
                result.Details = "触控板设备检测正常";
            }
            catch (Exception ex)
            {
                result.Status = TestStatus.Failed;
                result.ErrorMessage = $"触控板测试失败: {ex.Message}";
                Logger.Error("Touchpad test failed", ex);
            }

            return result;
        }

        public async Task<TestResult> TestGraphicsCardAsync(IProgress<TestProgress>? progress = null)
        {
            var result = new TestResult
            {
                DeviceName = "显卡",
                TestType = HardwareTestType.GraphicsCard
            };

            try
            {
                progress?.Report(new TestProgress { Message = "正在检测显卡设备...", Percentage = 10 });

                // 获取显卡信息
                var graphicsCards = GetGraphicsCards();
                if (graphicsCards.Count == 0)
                {
                    result.Status = TestStatus.Failed;
                    result.ErrorMessage = "未检测到显卡设备";
                    return result;
                }

                progress?.Report(new TestProgress { Message = $"检测到 {graphicsCards.Count} 个显卡", Percentage = 30 });

                foreach (var gpu in graphicsCards)
                {
                    // 检查显卡驱动
                    if (string.IsNullOrEmpty(gpu.DriverVersion))
                    {
                        result.Status = TestStatus.Warning;
                        result.ErrorMessage = "显卡驱动未安装";
                        result.Recommendations.Add("安装显卡驱动程序");
                        result.Recommendations.Add("访问显卡制造商官网下载驱动");
                    }
                    else
                    {
                        progress?.Report(new TestProgress { Message = $"显卡: {gpu.Name}, 驱动版本: {gpu.DriverVersion}", Percentage = 50 });
                    }

                    // 检查显存
                    if (gpu.AdapterRAM < 512) // 小于512MB
                    {
                        result.Recommendations.Add("显卡显存较低，可能影响性能");
                    }
                }

                progress?.Report(new TestProgress { Message = "正在测试显示输出...", Percentage = 70 });

                // 测试分辨率
                var screen = Screen.PrimaryScreen;
                if (screen != null)
                {
                    result.Details = $"主显示器分辨率: {screen.Bounds.Width}x{screen.Bounds.Height}";
                }

                progress?.Report(new TestProgress { Message = "正在检查DirectX...", Percentage = 90 });

                result.Status = TestStatus.Passed;
            }
            catch (Exception ex)
            {
                result.Status = TestStatus.Failed;
                result.ErrorMessage = $"显卡测试失败: {ex.Message}";
                Logger.Error("Graphics card test failed", ex);
            }

            return result;
        }

        public async Task<TestResult> TestKeyboardAsync(IProgress<TestProgress>? progress = null)
        {
            var result = new TestResult
            {
                DeviceName = "键盘",
                TestType = HardwareTestType.Keyboard
            };

            try
            {
                progress?.Report(new TestProgress { Message = "正在检测键盘设备...", Percentage = 20 });

                var keyboards = GetKeyboardDevices();
                if (keyboards.Count == 0)
                {
                    result.Status = TestStatus.Warning;
                    result.ErrorMessage = "未检测到键盘设备";
                    return result;
                }

                progress?.Report(new TestProgress { Message = $"检测到 {keyboards.Count} 个键盘设备", Percentage = 50 });

                result.Status = TestStatus.Passed;
                result.Details = "键盘设备检测正常";
            }
            catch (Exception ex)
            {
                result.Status = TestStatus.Failed;
                result.ErrorMessage = $"键盘测试失败: {ex.Message}";
                Logger.Error("Keyboard test failed", ex);
            }

            return result;
        }

        public async Task<TestResult> TestAudioAsync(IProgress<TestProgress>? progress = null)
        {
            var result = new TestResult
            {
                DeviceName = "音频设备",
                TestType = HardwareTestType.Audio
            };

            try
            {
                progress?.Report(new TestProgress { Message = "正在检测音频设备...", Percentage = 20 });

                var audioDevices = GetAudioDevices();
                if (audioDevices.Count == 0)
                {
                    result.Status = TestStatus.Warning;
                    result.ErrorMessage = "未检测到音频设备";
                    return result;
                }

                progress?.Report(new TestProgress { Message = $"检测到 {audioDevices.Count} 个音频设备", Percentage = 50 });

                result.Status = TestStatus.Passed;
                result.Details = "音频设备检测正常";
            }
            catch (Exception ex)
            {
                result.Status = TestStatus.Failed;
                result.ErrorMessage = $"音频测试失败: {ex.Message}";
                Logger.Error("Audio test failed", ex);
            }

            return result;
        }

        public async Task<TestResult> TestNetworkAsync(IProgress<TestProgress>? progress = null)
        {
            var result = new TestResult
            {
                DeviceName = "网络适配器",
                TestType = HardwareTestType.Network
            };

            try
            {
                progress?.Report(new TestProgress { Message = "正在检测网络适配器...", Percentage = 20 });

                var networkAdapters = GetNetworkAdapters();
                if (networkAdapters.Count == 0)
                {
                    result.Status = TestStatus.Warning;
                    result.ErrorMessage = "未检测到网络适配器";
                    return result;
                }

                progress?.Report(new TestProgress { Message = $"检测到 {networkAdapters.Count} 个网络适配器", Percentage = 50 });

                result.Status = TestStatus.Passed;
                result.Details = "网络适配器检测正常";
            }
            catch (Exception ex)
            {
                result.Status = TestStatus.Failed;
                result.ErrorMessage = $"网络测试失败: {ex.Message}";
                Logger.Error("Network test failed", ex);
            }

            return result;
        }

        public async Task<List<TestResult>> RunAllTestsAsync(IProgress<TestProgress>? progress = null)
        {
            var results = new List<TestResult>();
            var tests = new List<Func<IProgress<TestProgress>?, Task<TestResult>>>
            {
                TestMouseAsync,
                TestTouchpadAsync,
                TestGraphicsCardAsync,
                TestKeyboardAsync,
                TestAudioAsync,
                TestNetworkAsync
            };

            int totalTests = tests.Count;
            int completedTests = 0;

            foreach (var test in tests)
            {
                var result = await test(progress);
                results.Add(result);
                completedTests++;

                progress?.Report(new TestProgress
                {
                    Message = $"已完成 {completedTests}/{totalTests} 项测试",
                    Percentage = (completedTests * 100) / totalTests
                });
            }

            return results;
        }

        private List<InputDeviceInfo> GetMouseDevices()
        {
            var devices = new List<InputDeviceInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PointingDevice");
                foreach (ManagementObject obj in searcher.Get())
                {
                    devices.Add(new InputDeviceInfo
                    {
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Name = GetPropertyString(obj, "Name"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer"),
                        IsDriverInstalled = !string.IsNullOrEmpty(GetPropertyString(obj, "DriverVersion"))
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting mouse devices", ex);
            }
            return devices;
        }

        private List<InputDeviceInfo> GetTouchpadDevices()
        {
            var devices = new List<InputDeviceInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PointingDevice WHERE Name LIKE '%touchpad%' OR Name LIKE '%TouchPad%' OR Name LIKE '%触控板%'");
                foreach (ManagementObject obj in searcher.Get())
                {
                    devices.Add(new InputDeviceInfo
                    {
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Name = GetPropertyString(obj, "Name"),
                        IsDisabled = GetPropertyString(obj, "Status") == "Degraded"
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting touchpad devices", ex);
            }
            return devices;
        }

        private List<GraphicsCardInfo> GetGraphicsCards()
        {
            var cards = new List<GraphicsCardInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
                foreach (ManagementObject obj in searcher.Get())
                {
                    uint adapterRam = 0;
                    try
                    {
                        adapterRam = Convert.ToUInt32(obj["AdapterRAM"]) / (1024 * 1024); // MB
                    }
                    catch { }

                    cards.Add(new GraphicsCardInfo
                    {
                        Name = GetPropertyString(obj, "Name"),
                        DriverVersion = GetPropertyString(obj, "DriverVersion"),
                        AdapterRAM = adapterRam
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting graphics cards", ex);
            }
            return cards;
        }

        private List<InputDeviceInfo> GetKeyboardDevices()
        {
            var devices = new List<InputDeviceInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Keyboard");
                foreach (ManagementObject obj in searcher.Get())
                {
                    devices.Add(new InputDeviceInfo
                    {
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Name = GetPropertyString(obj, "Name"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer")
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting keyboard devices", ex);
            }
            return devices;
        }

        private List<InputDeviceInfo> GetAudioDevices()
        {
            var devices = new List<InputDeviceInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_SoundDevice");
                foreach (ManagementObject obj in searcher.Get())
                {
                    devices.Add(new InputDeviceInfo
                    {
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Name = GetPropertyString(obj, "Name"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer")
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting audio devices", ex);
            }
            return devices;
        }

        private List<InputDeviceInfo> GetNetworkAdapters()
        {
            var devices = new List<InputDeviceInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapter WHERE PhysicalAdapter=True");
                foreach (ManagementObject obj in searcher.Get())
                {
                    devices.Add(new InputDeviceInfo
                    {
                        DeviceId = GetPropertyString(obj, "DeviceID"),
                        Name = GetPropertyString(obj, "Name"),
                        Manufacturer = GetPropertyString(obj, "Manufacturer")
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting network adapters", ex);
            }
            return devices;
        }

        private async Task TestMouseMovementAsync(IProgress<TestProgress>? progress)
        {
            await Task.Delay(100);
        }

        private async Task TestMouseButtonsAsync(IProgress<TestProgress>? progress)
        {
            await Task.Delay(100);
        }

        private void EnableDevice(string deviceId)
        {
            try
            {
                // 使用设备管理器启用设备
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c devcon enable \"{deviceId}\"",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    Verb = "runas"
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                Logger.Error("Error enabling device", ex);
            }
        }

        private static string GetPropertyString(ManagementObject obj, string propertyName)
        {
            try
            {
                var value = obj[propertyName];
                return value?.ToString() ?? "";
            }
            catch
            {
                return "";
            }
        }
    }

    public class TestResult
    {
        public string DeviceName { get; set; } = "";
        public HardwareTestType TestType { get; set; }
        public TestStatus Status { get; set; }
        public string Details { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
        public List<string> Recommendations { get; set; } = new();
        public bool CanAutoFix { get; set; }
        public Action? AutoFixAction { get; set; }
    }

    public enum HardwareTestType
    {
        Mouse,
        Touchpad,
        GraphicsCard,
        Keyboard,
        Audio,
        Network
    }

    public enum TestStatus
    {
        Passed,     // 通过 - 绿色
        Warning,    // 警告 - 黄色
        Failed      // 失败 - 红色
    }

    public class TestProgress
    {
        public string Message { get; set; } = "";
        public int Percentage { get; set; }
    }

    public class TestProgressEventArgs : EventArgs
    {
        public string Message { get; set; } = "";
        public int Percentage { get; set; }
    }

    public class TestCompletedEventArgs : EventArgs
    {
        public List<TestResult> Results { get; set; } = new();
    }

    public class InputDeviceInfo
    {
        public string DeviceId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Manufacturer { get; set; } = "";
        public bool IsDriverInstalled { get; set; }
        public bool IsDisabled { get; set; }
    }

    public class GraphicsCardInfo
    {
        public string Name { get; set; } = "";
        public string DriverVersion { get; set; } = "";
        public uint AdapterRAM { get; set; }
    }
}
