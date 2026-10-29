
using PlcTest.Server.ServerInterface;
using System;
using System.IO.Ports;
using System.Threading;

namespace PlcTest.Server.ServerCommunication
{
    /// <summary>
    /// 串口“服务端”通信实现（对 IServerCommunication 的适配）
    /// 注意：串口无客户端概念，client 标识恒为 null，SendToClient 会忽略 client 参数并直接向串口写数据。
    /// </summary>
    public class SerialServerCommunication : IServerCommunication
    {
        /// <summary>
        /// 串口实例
        /// </summary>
        private readonly SerialPort _serialPort;
        /// <summary>
        /// 请求帧长解析（由服务端协议注入）；未设置或协议判不出长度时，按空闲超时收帧。
        /// </summary>
        public ServerFrameLengthResolver FrameLengthResolver { get; set; }
        /// <summary>探测缓冲大小（足够容纳 MC 1E 的最大写请求）</summary>
        private const int ProbeBufferSize = 4096;
        /// <summary>等首字节的超时（毫秒）</summary>
        private const int FirstByteTimeoutMs = 500;
        /// <summary>协议判不出帧长时的空闲超时（毫秒）</summary>
        private const int IdleTimeoutMs = 60;
        /// <summary>
        /// 异步操作锁
        /// </summary>
        private readonly object _sync = new object();
        /// <summary>
        /// 是否运行
        /// </summary>
        public bool IsRunning { get; private set; }
        /// <summary>
        /// 客户端连接数量
        /// </summary>
        public int ClientCount => IsRunning ? 1 : 0;
        /// <summary>
        /// 接收数据委托（串口无客户端概念，client 恒为 null）
        /// </summary>
        public event Action<byte[], IClientSession> OnDataReceived;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="parity"></param>
        /// <param name="dataBits"></param>
        /// <param name="stopBits"></param>
        public SerialServerCommunication(string portName, int baudRate = 9600, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
        {
            _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            _serialPort.DataReceived += SerialPort_DataReceived;
        }
        /// <summary>
        /// 开始服务
        /// </summary>
        public void Start()
        {
            lock (_sync)
            {
                if (IsRunning) return;
                try
                {
                    _serialPort.Open();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"串口打开失败: {ex.Message}", ex);
                }
                IsRunning = true;
            }
        }
        /// <summary>
        /// 停止服务
        /// </summary>
        public void Stop()
        {
            lock (_sync)
            {
                if (!IsRunning) return;
                _serialPort.DataReceived -= SerialPort_DataReceived;
                try { _serialPort.Close(); } catch { }
                IsRunning = false;
            }
        }

        /// <summary>
        /// 向串口发送数据（串口无客户端概念，忽略 client 参数）
        /// </summary>
        public void SendToClient(byte[] data, IClientSession client)
        {
            if (data == null || data.Length == 0) return;
            if (!IsRunning) return;
            try
            {
                lock (_sync)
                {
                    _serialPort.Write(data, 0, data.Length);
                }
            }
            catch { }
        }
        /// <summary>
        /// 串口委托实际方法
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // 按 MC 3E 帧长度读满完整帧，避免半包/粘包
                while (IsRunning)
                {
                    byte[] frame = ReadFrame();
                    if (frame == null) return;
                    byte[] copy = frame;
                    ThreadPool.QueueUserWorkItem(_ => OnDataReceived?.Invoke(copy, null));
                }
            }
            catch { }
        }
        /// <summary>
        /// 读取一整帧请求。
        /// 帧长由注入的 <see cref="FrameLengthResolver"/>（即服务端协议）决定：
        /// 协议能算出长度就按长度读满；算不出（如 MC 1E）就回退到「一小段时间没有新数据」判据。
        /// </summary>
        private byte[] ReadFrame()
        {
            byte[] buffer = new byte[ProbeBufferSize];
            int filled = 0;
            int savedTimeout = _serialPort.ReadTimeout;

            try
            {
                // 先等首字节，避免在没有请求时空转
                _serialPort.ReadTimeout = FirstByteTimeoutMs;
                if (!ReadOnce(buffer, ref filled))
                {
                    return null;
                }

                // 收齐整帧：协议能算出长度就按长度读，算不出就用空闲超时
                _serialPort.ReadTimeout = IdleTimeoutMs;
                ServerFrameLengthResolver resolver = FrameLengthResolver;
                while (filled < buffer.Length)
                {
                    int total = resolver != null ? resolver(buffer, filled) : -1;
                    if (total > 0 && filled >= total)
                    {
                        break;
                    }

                    if (!ReadOnce(buffer, ref filled))
                    {
                        break;
                    }
                }
            }
            finally
            {
                try { _serialPort.ReadTimeout = savedTimeout; } catch { }
            }

            return filled > 0 ? Trim(buffer, filled) : null;
        }

        /// <summary>
        /// 从串口读一次（至少 1 字节，受当前 ReadTimeout 约束）。
        /// </summary>
        private bool ReadOnce(byte[] buffer, ref int filled)
        {
            try
            {
                int n = _serialPort.Read(buffer, filled, buffer.Length - filled);
                if (n <= 0)
                {
                    return false;
                }
                filled += n;
                return true;
            }
            catch (TimeoutException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static byte[] Trim(byte[] source, int length)
        {
            if (length == source.Length)
            {
                return source;
            }
            byte[] result = new byte[length];
            Array.Copy(source, result, length);
            return result;
        }
    }
}
