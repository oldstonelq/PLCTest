using PLCTest.PLCInterface;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PLCTest.ClientCommunication
{
    /// <summary>
    /// TCP通讯类 实现基础PLC通讯接口
    /// </summary>
    public class TcpClientCommunication : IClientCommunication
    {
        #region 私有对象
        /// <summary>
        /// TCP客户端实例
        /// </summary>
        private TcpClient _tcpClient;
        /// <summary>
        /// 网络数据流
        /// </summary>
        private NetworkStream _stream;
        #endregion

        #region 通讯配置参数
        /// <summary>
        /// PLC IP地址
        /// </summary>
        public string IpAddress { get; set; }
        /// <summary>
        /// PLC端口号
        /// </summary>
        public int Port { get; set; }
        /// <summary>
        /// 连接超时 ms
        /// </summary>
        public int ConnectTimeout { get; set; } = 2000;
        /// <summary>
        /// 读写超时 ms
        /// </summary>
        public int ReadWriteTimeout { get; set; } = 1000;
        /// <summary>
        /// 接收缓冲区最大长度(PLC报文通用4096足够)
        /// </summary>
        private const int BufferMaxLength = 4096;
        #endregion

        #region 构造函数
        public TcpClientCommunication(string IP,int Port)
        { 
            this.IpAddress= IP;
            this.Port =Port ;
        }
        #endregion

        #region 连接状态检测
        /// <summary>
        /// 是否在线连接
        /// </summary>
        public bool IsConnected
        {
            get
            {
                try
                {
                    if (_tcpClient == null) return false;
                    Socket socket = _tcpClient.Client;
                    if (socket == null) return false;

                    // Poll检测远端断开
                    bool remoteClosed = socket.Poll(10, SelectMode.SelectRead) && socket.Available == 0;
                    return socket.Connected && !remoteClosed;
                }
                catch
                {
                    DisConnectServer();
                    return false;
                }
            }
        }
        #endregion

        #region 连接/断开
        /// <summary>
        /// 同步连接服务端
        /// </summary>
        /// <exception cref="Exception">连接失败抛出异常</exception>
        public void ConnectServer()
        {
            // 已连接先释放旧连接
            if (IsConnected)
                DisConnectServer();

            _tcpClient = new TcpClient();
            // 设置连接超时
            IAsyncResult connectResult = _tcpClient.BeginConnect(IpAddress, Port, null, null);
            bool connectSuccess = connectResult.AsyncWaitHandle.WaitOne(ConnectTimeout);

            if (!connectSuccess)
            {
                _tcpClient.Close();
                _tcpClient = null;
                throw new TimeoutException($"连接PLC[{IpAddress}:{Port}]超时{ConnectTimeout}ms");
            }

            // 完成异步连接
            _tcpClient.EndConnect(connectResult);
            _stream = _tcpClient.GetStream();
            // 设置读写超时
            _stream.ReadTimeout = ReadWriteTimeout;
            _stream.WriteTimeout = ReadWriteTimeout;
        }

        /// <summary>
        /// 断开连接并释放资源
        /// </summary>
        public  void DisConnectServer()
        {
            try
            {
                _stream?.Close();
                _stream?.Dispose();
                _tcpClient?.Close();
                _tcpClient?.Dispose();
            }
            catch
            {
                // 断开忽略异常
            }
            finally
            {
                _stream = null;
                _tcpClient = null;
            }
        }
        #endregion

        #region 收发数据（修复命名+返回接收报文）
        /// <summary>
        /// 发送报文并同步读取返回数据
        /// </summary>
        /// <param name="sendData">下发指令字节数组</param>
        /// <returns>PLC返回响应报文</returns>
        /// <exception cref="Exception">未连接、读写超时、通讯中断抛出异常</exception>
        public byte[] SendAndRecevieData(byte[] sendData)
        {
            try
            {
                if (!IsConnected || _stream == null)
                    throw new Exception("TCP未建立连接，无法发送数据");

                // 下发指令
                _stream.Write(sendData, 0, sendData.Length);

                // 接收缓冲区
                byte[] recvBuffer = new byte[BufferMaxLength];
                int recvLen = _stream.Read(recvBuffer, 0, BufferMaxLength);

                if (recvLen <= 0)
                    throw new Exception("无返回数据，连接已断开");

                // 截取有效长度报文返回
                byte[] result = new byte[recvLen];
                Array.Copy(recvBuffer, result, recvLen);
                return result;
            }
            catch (Exception)
            {
                return new byte[0];
            }
        }
        #endregion

        #region 异步扩展（适配UI不卡死）
        /// <summary>
        /// 异步收发
        /// </summary>
        public async Task<byte[]> SendAndRecevieDataAsync(byte[] sendData)
        {
            if (!IsConnected || _stream == null)
                throw new Exception("TCP未建立连接");

            await _stream.WriteAsync(sendData, 0, sendData.Length);
            byte[] buffer = new byte[BufferMaxLength];
            int len = await _stream.ReadAsync(buffer, 0, BufferMaxLength);

            if (len <= 0)
                throw new Exception("异步读取数据为空");
            
            byte[] res = new byte[len];
            Array.Copy(buffer, res, len);
            return res;
        }

        /// <summary>
        /// 异步连接
        /// </summary>
        public async Task ConnectServerAsync()
        {
            if (IsConnected) DisConnectServer();
            _tcpClient = new TcpClient();
            var connectTask = _tcpClient.ConnectAsync(IpAddress, Port);
            if (await Task.WhenAny(connectTask, Task.Delay(ConnectTimeout)) != connectTask)
            {
                _tcpClient.Close();
                throw new TimeoutException("异步连接PLC超时");
            }
            await connectTask;
            _stream = _tcpClient.GetStream();
            _stream.ReadTimeout = ReadWriteTimeout;
            _stream.WriteTimeout = ReadWriteTimeout;
        }
        #endregion
    }
}