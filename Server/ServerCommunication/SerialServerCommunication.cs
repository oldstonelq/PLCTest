
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
        public event Action<byte[], object> OnDataReceived;
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
                _serialPort.Open();
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
        public void SendToClient(byte[] data, object client)
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
                int bytesToRead = _serialPort.BytesToRead;
                if (bytesToRead <= 0) return;
                byte[] buffer = new byte[bytesToRead];
                int read = _serialPort.Read(buffer, 0, bytesToRead);
                if (read > 0)
                {
                    ThreadPool.QueueUserWorkItem(_ => OnDataReceived?.Invoke(buffer, null));
                }
            }
            catch { }
        }
    }
}
