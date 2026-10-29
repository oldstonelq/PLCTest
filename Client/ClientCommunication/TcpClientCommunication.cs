using PlcTest.Client.ClientInterface;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlcTest.Client.ClientCommunication
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
        /// <summary>
        /// 接收缓冲区最大长度(PLC报文通用4096足够)
        /// </summary>
        private const int _bufferMaxLength = 4096;
        /// <summary>
        /// 拼接后续分片时的轮询间隔 ms
        /// </summary>
        private const int _idleWaitMs = 20;
        /// <summary>
        /// 连续多少次无新数据即认为一帧已收全（20ms * 3 = 60ms 兜底）
        /// </summary>
        private const int _maxIdleRounds = 3;
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
        /// 读取一帧完整响应。
        /// 优先按协议给出的帧长读满；协议判不出长度（返回 -1）时才回退到空闲判据。
        /// TCP 是字节流，一次 Read 拿到的可能只是半帧。
        /// </summary>
        private byte[] ReadCompleteResponse(byte[] request, ResponseFrameLengthResolver resolver)
        {
            byte[] recvBuffer = new byte[_bufferMaxLength];

            // 首段阻塞读取，ReadTimeout 到期会抛 IOException
            int total;
            try
            {
                total = _stream.Read(recvBuffer, 0, recvBuffer.Length);
            }
            catch (IOException ex)
            {
                throw new Exception("读取PLC响应超时或被对端断开", ex);
            }

            if (total <= 0)
                throw new Exception("无返回数据，连接已断开");

            int frameLength = -1;
            if (resolver != null)
            {
                try
                {
                    frameLength = resolver(request, recvBuffer, total);
                }
                catch
                {
                    frameLength = -1;
                }
            }

            // 协议给出的长度超出缓冲上限，视为判定不可信
            if (frameLength > recvBuffer.Length)
            {
                frameLength = -1;
            }

            if (frameLength > 0)
            {
                // 按协议长度读满，不再靠"静默 60ms"猜
                while (total < frameLength)
                {
                    int n;
                    try
                    {
                        n = _stream.Read(recvBuffer, total, frameLength - total);
                    }
                    catch (IOException ex)
                    {
                        throw new Exception("读取PLC响应超时，帧未收全", ex);
                    }

                    if (n <= 0)
                        throw new Exception("无返回数据，连接已断开");
                    total += n;
                }

                // 一问一答模式下不应多收；多出来的一律截掉，避免污染解析
                if (total > frameLength)
                {
                    total = frameLength;
                }
            }
            else
            {
                // 回退：连续 _maxIdleRounds 次没有新数据即认为一帧收全
                int idleWait = 0;
                while (total < recvBuffer.Length && idleWait < _maxIdleRounds)
                {
                    if (_stream.DataAvailable)
                    {
                        int recvLen = _stream.Read(recvBuffer, total, recvBuffer.Length - total);
                        if (recvLen <= 0)
                            break;
                        total += recvLen;
                        idleWait = 0;
                        continue;
                    }

                    Thread.Sleep(_idleWaitMs);
                    idleWait++;
                }
            }

            // 截取有效长度报文返回
            byte[] result = new byte[total];
            Array.Copy(recvBuffer, result, total);
            return result;
        }
        #endregion
    }
}
