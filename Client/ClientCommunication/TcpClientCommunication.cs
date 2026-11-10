using PlcTest.Client.ClientInterface;
using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace PlcTest.Client.ClientCommunication
{
    /// <summary>
    /// TCP通讯类 实现基础PLC通讯接口
    /// </summary>
    public class TcpClientCommunication : IClientCommunication, IFrameTransport
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
        /// <summary>
        /// 收发串行锁：同一连接上"一问一答"必须成对，防止 UI 读线程与按钮写操作交叉
        /// </summary>
        private readonly SemaphoreSlim _ioLock = new SemaphoreSlim(1, 1);
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
                    DisconnectServer();
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
                DisconnectServer();

            _tcpClient = new TcpClient();
            // 设置连接超时
            IAsyncResult connectResult = _tcpClient.BeginConnect(IpAddress, Port, null, null);
            bool connectSuccess = connectResult.AsyncWaitHandle.WaitOne(ConnectTimeout);

            if (!connectSuccess)
            {
                // 结束挂起的异步连接，避免残留连接对象
                try { _tcpClient.EndConnect(connectResult); } catch { }
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
        public void DisconnectServer()
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
        /// <param name="responseLengthResolver">响应帧长度解析器，为 null 时回退到空闲判据</param>
        /// <returns>PLC返回响应报文</returns>
        /// <exception cref="Exception">未连接、读写超时、通讯中断抛出异常</exception>
        public byte[] SendAndReceiveData(byte[] sendData, ResponseFrameLengthResolver responseLengthResolver)
        {
            if (sendData == null || sendData.Length == 0)
                throw new ArgumentException("发送报文不能为空", nameof(sendData));

            // 同一连接上的收发必须串行：否则 A 的请求可能读到 B 的响应
            _ioLock.Wait();
            try
            {
                if (!IsConnected || _stream == null)
                    throw new Exception("TCP未建立连接，无法发送数据");

                // 下发指令
                _stream.Write(sendData, 0, sendData.Length);
                _stream.Flush();

                return ReadCompleteResponse(sendData, responseLengthResolver);
            }
            finally
            {
                _ioLock.Release();
            }
        }

        /// <summary>
        /// 读取一帧完整响应：帧长读满与空闲判据都在 <see cref="ResponseFrameAssembler"/>，
        /// 这里只负责把 TCP 的「读一段 / 还有没有数据 / 读不到时的提示」交出去。
        /// </summary>
        private byte[] ReadCompleteResponse(byte[] request, ResponseFrameLengthResolver resolver)
        {
            return ResponseFrameAssembler.ReadFrame(request, resolver, this);
        }
        #endregion

        #region IFrameTransport（供 ResponseFrameAssembler 使用）
        /// <summary>
        /// 读一段：TCP 是字节流，一次 Read 可能只拿到半帧，ReadTimeout 到期会抛 IOException。
        /// </summary>
        int IFrameTransport.Read(byte[] buffer, int offset, int count, bool firstRead)
        {
            try
            {
                return _stream.Read(buffer, offset, count);
            }
            catch (IOException ex)
            {
                throw new Exception(firstRead ? "读取PLC响应超时或被对端断开" : "读取PLC响应超时，帧未收全", ex);
            }
        }

        /// <summary>
        /// 已到达但还没读走的字节数是否为 0（空闲判据用）
        /// </summary>
        bool IFrameTransport.HasPendingData
        {
            get { return _stream != null && _stream.DataAvailable; }
        }

        /// <summary>
        /// 首段或续读时读不到数据的提示
        /// </summary>
        string IFrameTransport.EmptyResponseMessage
        {
            get { return "无返回数据，连接已断开"; }
        }
        #endregion
    }
}
