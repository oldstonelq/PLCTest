
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
        /// 按 MC 3E 帧格式读取一整帧（9 字节头中的“请求数据长度”决定后续字节数）
        /// </summary>
        private byte[] ReadFrame()
        {
            byte[] header = new byte[9];
            if (!ReadExact(header, 0, header.Length)) return null;

            // 请求数据长度是小端（低字节在前）：header[7] 低字节、header[8] 高字节
            int bodyLen = header[7] | (header[8] << 8);
            if (bodyLen <= 0 || bodyLen > 0xFFFF - 9) return null;

            byte[] frame = new byte[9 + bodyLen];
            Array.Copy(header, frame, 9);
            if (!ReadExact(frame, 9, bodyLen)) return null;
            return frame;
        }
        /// <summary>
        /// 从串口读满指定字节数（受 ReadTimeout 约束）
        /// </summary>
        private bool ReadExact(byte[] buffer, int offset, int count)
        {
            int filled = 0;
            while (filled < count)
            {
                int n;
                try
                {
                    n = _serialPort.Read(buffer, offset + filled, count - filled);
                }
                catch (TimeoutException) { return false; }
                catch (Exception) { return false; }
                if (n <= 0) return false;
                filled += n;
            }
            return true;
        }
    }
}
