using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HardwareDiagnostics.Core.Utils;

namespace HardwareDiagnostics.Security
{
    /// <summary>
    /// 轻量级网络防火墙 - 监控可疑数据包
    /// 使用Raw Socket实现，内存占用<50MB
    /// </summary>
    public class NetworkFirewall : IDisposable
    {
        [DllImport("ws2_32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int WSAIoctl(IntPtr s, int dwIoControlCode, ref int lpvInBuffer,
            int cbInBuffer, ref int lpvOutBuffer, int cbOutBuffer, ref int lpcbBytesReturned,
            IntPtr lpOverlapped, IntPtr lpCompletionRoutine);

        [DllImport("ws2_32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern SocketError WSAStartup(short wVersionRequested, ref WSAData lpWSAData);

        [StructLayout(LayoutKind.Sequential)]
        private struct WSAData
        {
            public short wVersion;
            public short wHighVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)]
            public string szDescription;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 129)]
            public string szSystemStatus;
            public short iMaxSockets;
            public short iMaxUdpDg;
            public IntPtr lpVendorInfo;
        }

        private const int SIO_RCVALL = unchecked((int)0x98000001);
        private const int IPPROTO_IP = 0;

        private Socket? _rawSocket;
        private bool _isRunning;
        private Thread? _captureThread;
        private readonly object _lock = new();
        private readonly List<SuspiciousPacket> _suspiciousPackets = new();
        private readonly Dictionary<string, ConnectionTracker> _connectionTrackers = new();
        private readonly SecurityLogger _securityLogger;

        // 可靠性增强：信任白名单 + 告警节流 + 过期清理
        private readonly HashSet<string> _trustedIPs = new(StringComparer.OrdinalIgnoreCase) { "127.0.0.1", "::1" };
        private readonly Dictionary<string, DateTime> _lastAlertTimes = new();
        private static readonly TimeSpan AlertThrottleInterval = TimeSpan.FromSeconds(60);
        private const int MaxTrackers = 5000;
        private static readonly TimeSpan TrackerStaleAfter = TimeSpan.FromMinutes(10);
        private const int MaxStoredPackets = 500;
        private long _droppedPacketCount;

        /// <summary>因节流/白名单被抑制的告警数（可用于判断是否漏报）。</summary>
        public long DroppedPacketCount => _droppedPacketCount;

        public event EventHandler<SuspiciousPacketEventArgs>? SuspiciousPacketDetected;
        public event EventHandler<AttackDetectedEventArgs>? AttackDetected;

        public bool IsRunning => _isRunning;
        public int SuspiciousPacketCount => _suspiciousPackets.Count;

        public NetworkFirewall()
        {
            _securityLogger = new SecurityLogger();
        }

        public void StartMonitoring()
        {
            lock (_lock)
            {
                if (_isRunning) return;

                try
                {
                    // 创建Raw Socket
                    _rawSocket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.IP);
                    _rawSocket.Bind(new IPEndPoint(GetLocalIPAddress(), 0));

                    // 设置接收所有数据包
                    int optionValue = 1;
                    byte[] optionInValue = BitConverter.GetBytes(optionValue);
                    byte[] optionOutValue = new byte[4];
                    int returnValue = _rawSocket.IOControl(IOControlCode.ReceiveAll, optionInValue, optionOutValue);

                    _isRunning = true;
                    _captureThread = new Thread(CaptureThreadProc)
                    {
                        IsBackground = true,
                        Priority = ThreadPriority.BelowNormal
                    };
                    _captureThread.Start();

                    Logger.Info("Network firewall started");
                    _securityLogger.LogSecurityEvent(SecurityEventType.FirewallStarted, "网络防火墙已启动");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to start network firewall", ex);
                    throw;
                }
            }
        }

        public void StopMonitoring()
        {
            Thread? threadToJoin = null;
            lock (_lock)
            {
                if (!_isRunning) return;

                _isRunning = false;
                threadToJoin = _captureThread;
                try { _rawSocket?.Close(); } catch { }
                _rawSocket = null;

                Logger.Info("Network firewall stopped");
                try { _securityLogger.LogSecurityEvent(SecurityEventType.FirewallStopped, "网络防火墙已停止"); } catch { }
            }

            // 在锁外等捕获线程退出，避免 Join 时持锁死锁
            try { threadToJoin?.Join(TimeSpan.FromSeconds(2)); } catch { }
        }

        /// <summary>加入信任 IP（白名单），命中后跳过检测、不告警。</summary>
        public void AddTrustedIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return;
            lock (_trustedIPs) { _trustedIPs.Add(ip.Trim()); }
        }

        /// <summary>从白名单移除。</summary>
        public void RemoveTrustedIP(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip)) return;
            lock (_trustedIPs) { _trustedIPs.Remove(ip.Trim()); }
        }

        public bool IsTrusted(string ip)
        {
            if (string.IsNullOrEmpty(ip)) return false;
            lock (_trustedIPs) { return _trustedIPs.Contains(ip); }
        }

        /// <summary>同源同类型 60 秒内只报一次，防止告警轰炸。</summary>
        private bool ShouldThrottle(string sourceIP, ThreatType type)
        {
            string key = sourceIP + "|" + type;
            var now = DateTime.UtcNow;
            lock (_lastAlertTimes)
            {
                if (_lastAlertTimes.TryGetValue(key, out var last) && now - last < AlertThrottleInterval)
                    return true;
                _lastAlertTimes[key] = now;
                // 顺手清理过期节流键，防止字典无限增长
                if (_lastAlertTimes.Count > 2000)
                {
                    var cutoff = now - AlertThrottleInterval;
                    foreach (var k in _lastAlertTimes.Where(p => p.Value < cutoff).Select(p => p.Key).ToList())
                        _lastAlertTimes.Remove(k);
                }
                return false;
            }
        }

        /// <summary>清理 10 分钟无活动的 tracker，超 5000 个按最旧淘汰。</summary>
        private void CleanupStaleTrackers()
        {
            lock (_connectionTrackers)
            {
                if (_connectionTrackers.Count == 0) return;
                var cutoff = DateTime.Now - TrackerStaleAfter;
                foreach (var k in _connectionTrackers.Where(p => p.Value.LastSeen < cutoff).Select(p => p.Key).ToList())
                    _connectionTrackers.Remove(k);
                while (_connectionTrackers.Count > MaxTrackers)
                {
                    var oldest = _connectionTrackers.OrderBy(p => p.Value.LastSeen).First().Key;
                    _connectionTrackers.Remove(oldest);
                }
            }
        }

        private void CaptureThreadProc()
        {
            byte[] buffer = new byte[65535];

            while (_isRunning)
            {
                try
                {
                    if (_rawSocket == null) break;

                    int received = _rawSocket.Receive(buffer);
                    if (received > 0)
                    {
                        ProcessPacket(buffer, received);
                    }
                }
                catch (SocketException)
                {
                    // Socket关闭，正常退出
                    break;
                }
                catch (Exception ex)
                {
                    Logger.Error("Error capturing packet", ex);
                }
            }
        }

        private void ProcessPacket(byte[] buffer, int length)
        {
            try
            {
                // 解析IP头
                if (length < 20) return;

                byte ipHeaderLength = (byte)((buffer[0] & 0x0F) * 4);
                byte protocol = buffer[9];
                string srcIP = $"{buffer[12]}.{buffer[13]}.{buffer[14]}.{buffer[15]}";
                string dstIP = $"{buffer[16]}.{buffer[17]}.{buffer[18]}.{buffer[19]}";

                // 只处理TCP/UDP/ICMP
                if (protocol != 6 && protocol != 17 && protocol != 1) return;

                // 白名单直接放行，不做检测
                if (IsTrusted(srcIP)) return;

                var packet = new PacketInfo
                {
                    Timestamp = DateTime.Now,
                    SourceIP = srcIP,
                    DestinationIP = dstIP,
                    Protocol = protocol,
                    Length = length
                };

                // 检测可疑特征
                var threat = DetectThreat(packet, buffer, ipHeaderLength, length);
                if (threat != null)
                {
                    HandleSuspiciousPacket(threat);
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"Error processing packet: {ex.Message}");
            }
        }

        private SuspiciousPacket? DetectThreat(PacketInfo packet, byte[] buffer, int ipHeaderLength, int totalLength)
        {
            // 1. 检测端口扫描
            var portScanThreat = DetectPortScan(packet);
            if (portScanThreat != null) return portScanThreat;

            // 2. 检测SYN Flood
            var synFloodThreat = DetectSynFlood(packet, buffer, ipHeaderLength);
            if (synFloodThreat != null) return synFloodThreat;

            // 3. 检测畸形包
            var malformedThreat = DetectMalformedPacket(packet, buffer, ipHeaderLength, totalLength);
            if (malformedThreat != null) return malformedThreat;

            // 4. 检测已知攻击签名
            var signatureThreat = DetectAttackSignature(packet, buffer, ipHeaderLength, totalLength);
            if (signatureThreat != null) return signatureThreat;

            // 5. 检测可疑外连
            var outboundThreat = DetectSuspiciousOutbound(packet, buffer, ipHeaderLength);
            if (outboundThreat != null) return outboundThreat;

            return null;
        }

        private SuspiciousPacket? DetectPortScan(PacketInfo packet)
        {
            string key = packet.SourceIP;
            lock (_connectionTrackers)
            {
                if (!_connectionTrackers.TryGetValue(key, out var tracker))
                {
                    tracker = new ConnectionTracker { SourceIP = packet.SourceIP };
                    _connectionTrackers[key] = tracker;
                }

                tracker.AddConnection(packet.DestinationIP, DateTime.Now);

                // 检测端口扫描特征
                if (tracker.UniqueDestinations.Count > 20 && tracker.GetConnectionRate() > 10)
                {
                    return new SuspiciousPacket
                    {
                        Timestamp = DateTime.Now,
                        SourceIP = packet.SourceIP,
                        ThreatType = ThreatType.PortScan,
                        Severity = ThreatSeverity.High,
                        Description = $"检测到端口扫描行为，目标主机数: {tracker.UniqueDestinations.Count}",
                        Details = $"连接速率: {tracker.GetConnectionRate():F1} conn/s"
                    };
                }
            }

            return null;
        }

        private SuspiciousPacket? DetectSynFlood(PacketInfo packet, byte[] buffer, int ipHeaderLength)
        {
            // TCP协议且是SYN包
            if (packet.Protocol != 6 || buffer.Length < ipHeaderLength + 14) return null;

            byte tcpFlags = buffer[ipHeaderLength + 13];
            bool isSyn = (tcpFlags & 0x02) != 0;
            bool isAck = (tcpFlags & 0x10) != 0;

            if (isSyn && !isAck)
            {
                string key = $"{packet.SourceIP}:{packet.DestinationIP}";
                lock (_connectionTrackers)
                {
                    if (!_connectionTrackers.TryGetValue(key, out var tracker))
                    {
                        tracker = new ConnectionTracker { SourceIP = packet.SourceIP };
                        _connectionTrackers[key] = tracker;
                    }

                    tracker.SynCount++;
                    tracker.LastSynTime = DateTime.Now;
                    tracker.LastSeen = DateTime.Now;

                    // SYN Flood检测：大量SYN无ACK
                    if (tracker.SynCount > 100 && tracker.SynCount > tracker.AckCount * 3)
                    {
                        return new SuspiciousPacket
                        {
                            Timestamp = DateTime.Now,
                            SourceIP = packet.SourceIP,
                            ThreatType = ThreatType.SynFlood,
                            Severity = ThreatSeverity.Critical,
                            Description = "检测到SYN Flood攻击",
                            Details = $"SYN包: {tracker.SynCount}, ACK包: {tracker.AckCount}"
                        };
                    }
                }
            }
            else if (isAck)
            {
                string key = $"{packet.SourceIP}:{packet.DestinationIP}";
                lock (_connectionTrackers)
                {
                    if (_connectionTrackers.TryGetValue(key, out var tracker))
                    {
                        tracker.AckCount++;
                    }
                }
            }

            return null;
        }

        private SuspiciousPacket? DetectMalformedPacket(PacketInfo packet, byte[] buffer, int ipHeaderLength, int totalLength)
        {
            // 检测异常大的包
            if (totalLength > 1500)
            {
                return new SuspiciousPacket
                {
                    Timestamp = DateTime.Now,
                    SourceIP = packet.SourceIP,
                    ThreatType = ThreatType.MalformedPacket,
                    Severity = ThreatSeverity.Medium,
                    Description = "检测到超大网络包",
                    Details = $"包大小: {totalLength} bytes"
                };
            }

            // 检测IP头长度异常
            if (ipHeaderLength < 20 || ipHeaderLength > 60)
            {
                return new SuspiciousPacket
                {
                    Timestamp = DateTime.Now,
                    SourceIP = packet.SourceIP,
                    ThreatType = ThreatType.MalformedPacket,
                    Severity = ThreatSeverity.High,
                    Description = "检测到畸形IP头",
                    Details = $"IP头长度: {ipHeaderLength}"
                };
            }

            return null;
        }

        private SuspiciousPacket? DetectAttackSignature(PacketInfo packet, byte[] buffer, int ipHeaderLength, int totalLength)
        {
            // 转换为字符串进行签名检测（Latin1 避免二进制误解码，限 512 字节防大包拷贝）
            if (totalLength - ipHeaderLength < 10) return null;

            int payloadLen = Math.Min(totalLength - ipHeaderLength, 512);
            string payload;
            try { payload = Encoding.GetEncoding("iso-8859-1").GetString(buffer, ipHeaderLength, payloadLen); }
            catch { return null; }

            // SQL注入检测
            string[] sqlSignatures = { "' OR '", "' AND '", "'; DROP", "UNION SELECT", "INSERT INTO", "DELETE FROM" };
            foreach (var sig in sqlSignatures)
            {
                if (payload.IndexOf(sig, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new SuspiciousPacket
                    {
                        Timestamp = DateTime.Now,
                        SourceIP = packet.SourceIP,
                        ThreatType = ThreatType.SqlInjection,
                        Severity = ThreatSeverity.Critical,
                        Description = "检测到SQL注入攻击",
                        Details = $"匹配签名: {sig}"
                    };
                }
            }

            // XSS检测
            string[] xssSignatures = { "<script>", "javascript:", "onerror=", "onload=" };
            foreach (var sig in xssSignatures)
            {
                if (payload.IndexOf(sig, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return new SuspiciousPacket
                    {
                        Timestamp = DateTime.Now,
                        SourceIP = packet.SourceIP,
                        ThreatType = ThreatType.XSS,
                        Severity = ThreatSeverity.High,
                        Description = "检测到XSS攻击",
                        Details = $"匹配签名: {sig}"
                    };
                }
            }

            return null;
        }

        private SuspiciousPacket? DetectSuspiciousOutbound(PacketInfo packet, byte[] buffer, int ipHeaderLength)
        {
            // 检测可疑的外连端口
            if (packet.Protocol != 6 && packet.Protocol != 17) return null;
            if (buffer.Length < ipHeaderLength + 4) return null;

            // 获取目标端口
            int destPort = (buffer[ipHeaderLength] << 8) | buffer[ipHeaderLength + 1];

            // 检测可疑端口
            int[] suspiciousPorts = { 4444, 5555, 6666, 7777, 8888, 31337, 12345 };
            if (suspiciousPorts.Contains(destPort))
            {
                return new SuspiciousPacket
                {
                    Timestamp = DateTime.Now,
                    SourceIP = packet.SourceIP,
                    DestinationIP = packet.DestinationIP,
                    ThreatType = ThreatType.SuspiciousOutbound,
                    Severity = ThreatSeverity.High,
                    Description = "检测到可疑的外连行为",
                    Details = $"目标端口: {destPort} (已知恶意软件常用端口)"
                };
            }

            // 检测IRC端口
            if (destPort == 6667 || destPort == 6668 || destPort == 6669)
            {
                return new SuspiciousPacket
                {
                    Timestamp = DateTime.Now,
                    SourceIP = packet.SourceIP,
                    ThreatType = ThreatType.IrcConnection,
                    Severity = ThreatSeverity.Medium,
                    Description = "检测到IRC连接",
                    Details = $"IRC端口: {destPort}"
                };
            }

            return null;
        }

        private void HandleSuspiciousPacket(SuspiciousPacket packet)
        {
            // 节流：同源同类型 60 秒只报一次
            if (ShouldThrottle(packet.SourceIP, packet.ThreatType))
            {
                Interlocked.Increment(ref _droppedPacketCount);
                return;
            }

            lock (_suspiciousPackets)
            {
                _suspiciousPackets.Add(packet);

                // 只保留最近500条
                while (_suspiciousPackets.Count > MaxStoredPackets)
                {
                    _suspiciousPackets.RemoveAt(0);
                }
            }

            // 记录安全日志（失败不影响捕获线程）
            try { _securityLogger.LogSuspiciousPacket(packet); } catch (Exception ex) { Logger.Debug($"Log packet failed: {ex.Message}"); }

            // 触发事件（订阅者异常不能拖垮捕获线程）
            try { SuspiciousPacketDetected?.Invoke(this, new SuspiciousPacketEventArgs { Packet = packet }); }
            catch (Exception ex) { Logger.Debug($"Packet event handler failed: {ex.Message}"); }

            // 如果是高危威胁，触发攻击检测事件
            if (packet.Severity == ThreatSeverity.Critical || packet.Severity == ThreatSeverity.High)
            {
                try
                {
                    AttackDetected?.Invoke(this, new AttackDetectedEventArgs
                    {
                        AttackType = packet.ThreatType.ToString(),
                        SourceIP = packet.SourceIP,
                        Severity = packet.Severity
                    });
                }
                catch (Exception ex) { Logger.Debug($"Attack event handler failed: {ex.Message}"); }
            }

            // 顺手做一次过期清理（低频触发：每 100 条一次）
            if (_suspiciousPackets.Count % 100 == 0)
            {
                try { CleanupStaleTrackers(); } catch { }
            }

            Logger.Warning($"[SECURITY] {packet.ThreatType} from {packet.SourceIP}: {packet.Description}");
        }

        private IPAddress GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip;
                    }
                }
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Debug($"GetLocalIPAddress failed: {ex.Message}");
            }
            return IPAddress.Parse("127.0.0.1");
        }

        public List<SuspiciousPacket> GetRecentSuspiciousPackets(int count = 100)
        {
            lock (_suspiciousPackets)
            {
                var result = new List<SuspiciousPacket>();
                int startIndex = Math.Max(0, _suspiciousPackets.Count - count);
                for (int i = startIndex; i < _suspiciousPackets.Count; i++)
                {
                    result.Add(_suspiciousPackets[i]);
                }
                return result;
            }
        }

        public void ClearSuspiciousPackets()
        {
            lock (_suspiciousPackets)
            {
                _suspiciousPackets.Clear();
            }
        }

        public void Dispose()
        {
            StopMonitoring();
            _securityLogger?.Dispose();
        }
    }

    public class PacketInfo
    {
        public DateTime Timestamp { get; set; }
        public string SourceIP { get; set; } = "";
        public string DestinationIP { get; set; } = "";
        public byte Protocol { get; set; }
        public int Length { get; set; }
    }

    public class SuspiciousPacket
    {
        public DateTime Timestamp { get; set; }
        public string SourceIP { get; set; } = "";
        public string DestinationIP { get; set; } = "";
        public ThreatType ThreatType { get; set; }
        public ThreatSeverity Severity { get; set; }
        public string Description { get; set; } = "";
        public string Details { get; set; } = "";
        public byte[] RawData { get; set; } = Array.Empty<byte>();
    }

    public class ConnectionTracker
    {
        public string SourceIP { get; set; } = "";
        public HashSet<string> UniqueDestinations { get; } = new();
        private readonly List<DateTime> _connectionTimes = new();
        public int SynCount { get; set; }
        public int AckCount { get; set; }
        public DateTime LastSynTime { get; set; }
        public DateTime LastSeen { get; set; } = DateTime.Now;

        public void AddConnection(string destIP, DateTime time)
        {
            UniqueDestinations.Add(destIP);
            _connectionTimes.Add(time);
            LastSeen = time;

            // 清理旧数据
            var cutoff = time.AddMinutes(-1);
            _connectionTimes.RemoveAll(t => t < cutoff);
        }

        public double GetConnectionRate()
        {
            if (_connectionTimes.Count < 2) return 0;
            var span = _connectionTimes.Last() - _connectionTimes.First();
            return span.TotalSeconds > 0 ? _connectionTimes.Count / span.TotalSeconds : 0;
        }
    }

    public enum ThreatType
    {
        PortScan,
        SynFlood,
        MalformedPacket,
        SqlInjection,
        XSS,
        SuspiciousOutbound,
        IrcConnection,
        Unknown
    }

    public enum ThreatSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    public class SuspiciousPacketEventArgs : EventArgs
    {
        public SuspiciousPacket Packet { get; set; } = null!;
    }

    public class AttackDetectedEventArgs : EventArgs
    {
        public string AttackType { get; set; } = "";
        public string SourceIP { get; set; } = "";
        public ThreatSeverity Severity { get; set; }
    }
}
