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
    public class SerialPortClientCommunication : IClientCommunication
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
        /// <summary>
        /// 接收缓冲区最大长度
        /// </summary>
        private const int _bufferMaxLength = 4096;
        /// <summary>
        /// 拼接后续字节时的轮询间隔 ms
        /// </summary>
        private const int _idleWaitMs = 20;
        /// <summary>
        /// 连续多少次无新数据即认为一帧已收全（20ms * 3 = 60ms 兜底）
        /// </summary>
        private const int _maxIdleRounds = 3;
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
                ReadBufferSize = _bufferMaxLength,
                WriteBufferSize = _bufferMaxLength
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
        /// <returns>设备返回数据</returns>
        public byte[] SendAndReceiveData(byte[] sendData)
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

                return ReadCompleteResponse();
            }
            finally
            {
                _ioLock.Release();
            }
        }

        /// <summary>
        /// 读取一帧完整响应
        /// 串口按字节流返回，一次 Read 可能只拿到半帧，需把紧随其后的字节拼齐
        /// </summary>
        private byte[] ReadCompleteResponse()
        {
            byte[] recvBuffer = new byte[_bufferMaxLength];

            int total;
            try
            {
                total = _serialPort.Read(recvBuffer, 0, recvBuffer.Length);
            }
            catch (TimeoutException ex)
            {
                throw new Exception("串口读取超时，设备无响应", ex);
            }
            catch (InvalidOperationException ex)
            {
                throw new Exception("串口已关闭", ex);
            }

            if (total <= 0)
                throw new Exception("串口无返回数据，读取超时或设备离线");

            // 后续字节：连续 _maxIdleRounds 次没有新数据即认为一帧收全
            int idleWait = 0;
            while (total < recvBuffer.Length && idleWait < _maxIdleRounds)
            {
                int available = _serialPort.BytesToRead;
                if (available > 0)
                {
                    int recvLen = _serialPort.Read(recvBuffer, total, Math.Min(available, recvBuffer.Length - total));
                    if (recvLen <= 0)
                        break;
                    total += recvLen;
                    idleWait = 0;
                    continue;
                }

                Thread.Sleep(_idleWaitMs);
                idleWait++;
            }

            // 截取有效数据返回
            byte[] result = new byte[total];
            Array.Copy(recvBuffer, result, total);
            return result;
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
