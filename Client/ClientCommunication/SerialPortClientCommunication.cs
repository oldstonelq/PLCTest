using PLCTest.PLCInterface;
using System;
using System.IO.Ports;
using System.Linq;

namespace PLCTest.ClientCommunication
{
    /// <summary>
    /// 串口RS232/RS485通讯类，实现基础通讯接口
    /// </summary>
    public class SerialCommunication : IClientCommunication
    {
        #region 私有对象
        /// <summary>
        /// 串口核心实例
        /// </summary>
        private SerialPort _serialPort;
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
        private const int BufferMaxLength = 4096;
        #endregion

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
                    DisConnectServer();
                    return false;
                }
            }
        }
        #endregion

        #region 打开/关闭串口
        /// <summary>
        /// 打开串口
        /// </summary>
        public  void ConnectServer()
        {
            // 已打开先关闭旧端口
            if (IsConnected)
                DisConnectServer();

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
                ReadBufferSize = BufferMaxLength,
                WriteBufferSize = BufferMaxLength
            };

            _serialPort.Open();
        }

        /// <summary>
        /// 关闭串口释放资源
        /// </summary>
        public  void DisConnectServer()
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
        public  byte[] SendAndRecevieData(byte[] sendData)
        {
            if (!IsConnected || _serialPort == null)
                throw new Exception("串口未打开，无法发送数据");

            // 发送数据
            _serialPort.Write(sendData, 0, sendData.Length);

            byte[] recvBuffer = new byte[BufferMaxLength];
            int recvLen = _serialPort.Read(recvBuffer, 0, BufferMaxLength);

            if (recvLen <= 0)
                throw new Exception("串口无返回数据，读取超时或设备离线");

            // 截取有效数据返回
            byte[] result = new byte[recvLen];
            Array.Copy(recvBuffer, result, recvLen);
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
                return SerialPort.GetPortNames().OrderBy(p => int.Parse(p.Replace("COM", ""))).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
        #endregion
    }
}