using PlcTest.Client.ClientInterface;
using System;
using System.IO.Ports;
using System.Linq;
using System.Threading;

namespace PlcTest.Client.ClientCommunication
{
    /// <summary>
    /// 串口RS232/RS485通讯类，实现基础通讯接口
    /// </summary>
    public class SerialPortClientCommunication : IClientCommunication, IFrameTransport
    {
        #region 私有对象
        /// <summary>
        /// 串口核心实例
        /// </summary>
        private SerialPort _serialPort;
        /// <summary>
        /// 收发串行锁：同一串口上"一问一答"必须成对，防止读线程与写操作交叉
        /// </summary>
        private readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);
        #endregion

        #region 串口配置参数
        /// <summary>
        /// 串口号 COM1 COM2 ...
        /// </summary>
        public string PortName { get; set; }
        /// <summary>
        /// 波特率 9600 19200 38400 57600 115200
        /// </summary>
        public int BaudRate { get; set; } = 9600;
        /// <summary>
        /// 数据位 7 / 8
        /// </summary>
        public int DataBits { get; set; } = 8;
        /// <summary>
        /// 校验位
        /// </summary>
        public Parity Parity { get; set; } = Parity.None;
        /// <summary>
        /// 停止位
        /// </summary>
        public StopBits StopBits { get; set; } = StopBits.One;
        /// <summary>
        /// 读写超时 ms
        /// </summary>
        public int ReadWriteTimeout { get; set; } = 1000;
        #endregion
        /// <summary>
        /// 沟站函数
        /// </summary>
        /// <param name="portName"></param>
        /// <param name="baudRate"></param>
        /// <param name="parity"></param>
        /// <param name="dataBits"></param>
        /// <param name="stopBits"></param>
        public SerialPortClientCommunication(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            PortName = portName;
            BaudRate = baudRate;
            Parity = parity;
            DataBits = dataBits;
            StopBits = stopBits;
        }
        #region 连接状态
        public bool IsConnected
        {
            get
            {
                try
                {
                    if (_serialPort == null)
                        return false;
                    return _serialPort.IsOpen;
                }
                catch
                {
                    DisconnectServer();
                    return false;
                }
            }
        }
        #endregion

        #region 打开/关闭串口
        /// <summary>
        /// 打开串口
        /// </summary>
        public void ConnectServer()
        {
            // 已打开先关闭旧端口
            if (IsConnected)
                DisconnectServer();

            _serialPort = new SerialPort
            {
                PortName = PortName,
                BaudRate = BaudRate,
                DataBits = DataBits,
                Parity = Parity,
                StopBits = StopBits,
                ReadTimeout = ReadWriteTimeout,
                WriteTimeout = ReadWriteTimeout,
                // 缓冲区大小
                ReadBufferSize = ResponseFrameAssembler.MaxBufferLength,
                WriteBufferSize = ResponseFrameAssembler.MaxBufferLength
            };

            _serialPort.Open();
        }

        /// <summary>
        /// 关闭串口释放资源
        /// </summary>
        public void DisconnectServer()
        {
            try
            {
                if (_serialPort != null)
                {
                    if (_serialPort.IsOpen)
                        _serialPort.Close();
                    _serialPort.Dispose();
                }
            }
            catch
            {
                // 关闭时忽略异常
            }
            finally
            {
                _serialPort = null;
            }
        }
        #endregion

        #region 发送并接收数据
        /// <summary>
        /// 下发串口指令并读取返回报文
        /// </summary>
        /// <param name="sendData">发送字节数组</param>
        /// <param name="responseLengthResolver">响应帧长度解析器，为 null 时回退到空闲判据</param>
        /// <returns>设备返回数据</returns>
        public byte[] SendAndReceiveData(byte[] sendData, ResponseFrameLengthResolver responseLengthResolver)
        {
            if (sendData == null || sendData.Length == 0)
                throw new ArgumentException("发送报文不能为空", nameof(sendData));

            // 同一串口上的收发必须串行
            _ioLock.Wait();
            try
            {
                if (!IsConnected || _serialPort == null)
                    throw new Exception("串口未打开，无法发送数据");

                // 发送数据
                _serialPort.Write(sendData, 0, sendData.Length);

                return ReadCompleteResponse(sendData, responseLengthResolver);
            }
            finally
            {
                _ioLock.Release();
            }
        }

        /// <summary>
        /// 读取一帧完整响应：帧长读满与空闲判据都在 <see cref="ResponseFrameAssembler"/>，
        /// 这里只负责把串口的「读一段 / 还有没有数据 / 读不到时的提示」交出去。
        /// </summary>
        private byte[] ReadCompleteResponse(byte[] request, ResponseFrameLengthResolver resolver)
        {
            return ResponseFrameAssembler.ReadFrame(request, resolver, this);
        }
        #endregion

        #region IFrameTransport（供 ResponseFrameAssembler 使用）
        /// <summary>
        /// 读一段：串口 Read 在 ReadTimeout 到期时抛 TimeoutException，端口被关时抛 InvalidOperationException。
        /// </summary>
        int IFrameTransport.Read(byte[] buffer, int offset, int count, bool firstRead)
        {
            try
            {
                return _serialPort.Read(buffer, offset, count);
            }
            catch (TimeoutException ex)
            {
                throw new Exception(firstRead ? "串口读取超时，设备无响应" : "串口读取超时，帧未收全", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new Exception("串口已关闭", ex);
            }
        }

        /// <summary>
        /// 串口接收缓冲区里是否已有待读字节（端口关闭时视作没有）
        /// </summary>
        bool IFrameTransport.HasPendingData
        {
            get
            {
                try
                {
                    return _serialPort != null && _serialPort.IsOpen && _serialPort.BytesToRead > 0;
                }
                catch (InvalidOperationException)
                {
                    // 端口在探询与读取之间被关闭，按「没有待读数据」处理，交给下一次 Read 报错
                    return false;
                }
            }
        }

        /// <summary>
        /// 首段或续读时读不到数据的提示
        /// </summary>
        string IFrameTransport.EmptyResponseMessage
        {
            get { return "串口无返回数据，读取超时或设备离线"; }
        }
        #endregion

        #region 辅助静态工具方法
        /// <summary>
        /// 获取本机所有可用串口列表
        /// </summary>
        /// <returns>COM数组</returns>
        public static string[] GetAllSerialPorts()
        {
            try
            {
                // 按 COM 后的数字升序排列；不符合 COM 命名习惯的端口名排到末尾，而不是让整个列表报错
                return SerialPort.GetPortNames()
                    .OrderBy(p =>
                    {
                        int number;
                        return int.TryParse(p.Replace("COM", ""), out number) ? number : int.MaxValue;
                    })
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
        #endregion
    }
}
