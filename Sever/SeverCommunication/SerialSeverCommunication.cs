
using PLCTest.SeverInterface;
using System;
using System.IO.Ports;
using System.Threading;

namespace PLCTest.SeverCommunication
{
    /// <summary>
    /// 串口“服务端”通信实现（对 ISeverCommunication 的适配）
    /// 注意：由于 ISeverCommunication 使用 Socket 表示客户端，串口场景下会使用 null 作为客户端标识，
    /// SendToClient 方法会忽略传入的 Socket 并直接向串口写数据。
    /// </summary>
    public class SerialSeverCommunication : ISeverCommunication
    {
        /// <summary>
        /// 串口实例
        /// </summary>
        private readonly SerialPort serialPort;
        /// <summary>
        /// 异步操作锁
        /// </summary>
        private readonly object sync = new object();
        /// <summary>
        /// 是否运行
        /// </summary>
        public bool IsRunning { get; private set; }
        /// <summary>
        /// 客户端连接数量
        /// </summary>
        public int ClientCount => IsRunning ? 1 : 0;
        /// <summary>
        /// 接收数据委托
        /// </summary>
        public event Action<byte[]> OnDataReceived;
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="parity"></param>
        /// <param name="dataBits"></param>
        /// <param name="stopBits"></param>
        public SerialSeverCommunication(string portName, int baudRate = 9600, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
        {
            serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                ReadTimeout = 500,
                WriteTimeout = 500
            };
            serialPort.DataReceived += SerialPort_DataReceived;
        }
        /// <summary>
        /// 开始服务
        /// </summary>
        public void Start()
        {
            lock (sync)
            {
                if (IsRunning) return;
                serialPort.Open();
                IsRunning = true;
            }
        }
        /// <summary>
        /// 停止服务
        /// </summary>
        public void Stop()
        {
            lock (sync)
            {
                if (!IsRunning) return;
                serialPort.DataReceived -= SerialPort_DataReceived;
                try { serialPort.Close(); } catch { }
                IsRunning = false;
            }
        }

        /// <summary>
        /// 向串口发送数据
        /// </summary>
        public void SendToClient(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            if (!IsRunning) return;
            try
            {
                lock (sync)
                {
                    serialPort.Write(data, 0, data.Length);
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
                int bytesToRead = serialPort.BytesToRead;
                if (bytesToRead <= 0) return;
                byte[] buffer = new byte[bytesToRead];
                int read = serialPort.Read(buffer, 0, bytesToRead);
                if (read > 0)
                {
                    ThreadPool.QueueUserWorkItem(_ => OnDataReceived?.Invoke(buffer));
                }
            }
            catch { }
        }
    }
}
