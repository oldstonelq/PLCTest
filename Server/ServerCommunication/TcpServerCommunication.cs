
using PlcTest.Server.ServerInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlcTest.Server.ServerCommunication
{
    public class TcpServerCommunication : IServerCommunication
    {
        /// <summary>
        /// IP地址和Port口
        /// </summary>
        private IPEndPoint _ipAndPoint;
        public IPEndPoint IpAndPoint
        {
            get { return _ipAndPoint; }
            set { _ipAndPoint = value; }
        }
        /// <summary>
        /// 负责监听的Socket对象
        /// </summary>
        private Socket _listenSocket;
        public Socket ListenSocket
        {
            get { return _listenSocket; }
            set { _listenSocket = value; }
        }
        /// <summary>
        /// 客户端连接句柄集合
        /// </summary>
        private readonly List<TcpClientSession> _sessions = new List<TcpClientSession>();
        /// <summary>
        /// 客户端集合管理锁
        /// </summary>
        private readonly object _socketsLock = new object();
        /// <summary>
        /// 取消令牌
        /// </summary>
        private CancellationTokenSource _cleanupCts;

        /// <summary>
        /// 服务是否在运行
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// 请求帧长解析（由服务端协议注入）；未设置时一律按空闲判据收帧。
        /// </summary>
        public ServerFrameLengthResolver FrameLengthResolver { get; set; }

        /// <summary>探测缓冲大小（足够容纳 MC 1E 的最大写请求）</summary>
        private const int ProbeBufferSize = 4096;
        /// <summary>协议判不出帧长时的空闲判据（毫秒）</summary>
        private const int IdleTimeoutMs = 60;

        /// <summary>
        /// 已连接客户端数量
        /// </summary>
        public int ClientCount
        {
            get
            {
                lock (_socketsLock)
                {
                    return _sessions.Count;
                }
            }
        }

        /// <summary>
        /// 客户端数据到达事件（附带客户端连接句柄）
        /// </summary>
        public event Action<byte[], IClientSession> OnDataReceived;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="Ip"></param>
        /// <param name="Port"></param>
        public TcpServerCommunication(string Ip, int Port)
        {
            this._ipAndPoint = new IPEndPoint(IPAddress.Parse(Ip), Port);
            this._listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        }

        /// <summary>
        /// 启动服务端监听
        /// </summary>
        public void Start()
        {
            if (IsRunning)
            {
                return;
            }
            // Stop 时监听 Socket 已被关闭，重启时需要重新创建
            if (_listenSocket == null)
            {
                _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            // 先绑定监听成功再置运行状态，避免 Bind 失败后状态与实际不一致
            ListenSocket.Bind(IpAndPoint);
            //监听数量
            ListenSocket.Listen(10);
            IsRunning = true;
            //开始异步接受客户端连接
            BeginAcceptNext();
            //启动Socket清理任务
            StartSocketCleanup();
        }
        /// <summary>
        /// 停止监听
        /// </summary>
        public void Stop()
        {
            IsRunning = false;
            StopSocketCleanup();
            var listen = _listenSocket;
            _listenSocket = null;
            if (listen != null)
            {
                try { listen.Close(); } catch { }
            }
            // 客户端集合可能被接收线程并发修改，先加锁取快照再关闭
            List<TcpClientSession> snapshot;
            lock (_socketsLock)
            {
                snapshot = new List<TcpClientSession>(_sessions);
                _sessions.Clear();
            }
            foreach (var session in snapshot)
            {
                session.Dispose();
            }
        }
        /// <summary>
        /// 添加客户端连接句柄
        /// </summary>
        /// <param name="session"></param>
        private void AddSession(TcpClientSession session)
        {
            if (session == null) return;
            lock (_socketsLock)
            {
                _sessions.Add(session);
            }
        }
        /// <summary>
        /// 移除客户端连接句柄
        /// </summary>
        /// <param name="session"></param>
        private void RemoveSession(TcpClientSession session)
        {
            if (session == null) return;
            lock (_socketsLock)
            {
                _sessions.Remove(session);
            }
            try { session.Dispose(); } catch { }
        }
        /// <summary>
        /// 开始Socket清理任务
        /// </summary>
        private void StartSocketCleanup()
        {
            if (_cleanupCts != null) return;
            _cleanupCts = new CancellationTokenSource();
            var ct = _cleanupCts.Token;
            Task.Run(() => CleanupLoop(ct), ct);
        }
        /// <summary>
        /// 停止Socket清理任务
        /// </summary>
        private void StopSocketCleanup()
        {
            if (_cleanupCts == null) return;
            _cleanupCts.Cancel();
            _cleanupCts.Dispose();
            _cleanupCts = null;
        }
        /// <summary>
        /// 清理无效的Socket连接
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        private async Task CleanupLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    TcpClientSession[] listCopy;
                    lock (_socketsLock)
                    {
                        listCopy = _sessions.ToArray();
                    }

                    foreach (var session in listCopy)
                    {
                        bool remove = false;
                        try
                        {
                            // 常用断连检测：如果未连接或可读但可用字节为0，说明远端已关闭连接
                            var s = session.Socket;
                            if (s == null || !s.Connected)
                            {
                                remove = true;
                            }
                            else if (s.Poll(1000, SelectMode.SelectRead) && s.Available == 0)
                            {
                                remove = true;
                            }
                        }
                        catch
                        {
                            remove = true;
                        }

                        if (remove)
                            RemoveSession(session);
                    }

                    await Task.Delay(2000, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }

        }
        /// <summary>
        /// 接受客户端连接
        /// </summary>
        private void BeginAcceptNext()
        {
            try
            {
                _listenSocket.BeginAccept(new AsyncCallback((iResult) =>
                {
                    Socket client = null;
                    try
                    {
                        var listen = _listenSocket;
                        if (listen == null)
                        {
                            return;
                        }
                        // EndAccept 可能抛出 ObjectDisposedException，当 socket 已关闭时忽略
                        client = listen.EndAccept(iResult);
                    }
                    catch (ObjectDisposedException)
                    {
                        // 如果正在关闭，静默返回；否则记录日志
                        return;
                    }
                    catch (NullReferenceException)
                    {
                        return;
                    }
                    catch (SocketException)
                    {
                        // 视情况处理或记录
                        return;
                    }

                    if (client != null && client.Connected)
                    {
                        var session = new TcpClientSession(client);
                        Task task = ReceiveDataFromClient(session);
                        AddSession(session);
                    }

                    // 如果仍在工作，继续接受下一个连接
                    if (IsRunning)
                    {
                        BeginAcceptNext();
                    }
                }), null);
            }
            catch (ObjectDisposedException)
            {
                // 监听 socket 已被关闭
            }
            catch (NullReferenceException)
            {
                // Stop 与 BeginAcceptNext 并发时监听 socket 已被置空
            }
        }
        private Task ReceiveDataFromClient(TcpClientSession session)
        {
            return Task.Run(() =>
            {
                using (var rcvSocket = session.Socket)
                {
                    while (IsRunning)
                    {
                        // 按 MC 3E 帧长度读满一整帧，避免半包被截断、粘包被合并
                        byte[] frame = ReadFrame(rcvSocket);
                        if (frame == null)
                        {
                            break;
                        }
                        OnDataReceived?.Invoke(frame, session);
                    }
                    RemoveSession(session);
                }
            });
        }
        /// <summary>
        /// 读取完整的一帧：9 字节请求头中的“请求数据长度”字段决定后续字节数
        /// </summary>
        /// <param name="socket">客户端 Socket</param>
        /// <returns>完整帧；对端关闭或帧非法时返回 null</returns>
        private byte[] ReadFrame(Socket socket)
        {
            byte[] buffer = new byte[ProbeBufferSize];
            int filled = 0;
            ServerFrameLengthResolver resolver = FrameLengthResolver;

            while (true)
            {
                int total = resolver != null ? resolver(buffer, filled) : -1;

                if (total > 0)
                {
                    return ReadExact(socket, buffer, filled, total);
                }

                // total == 0 表示头部不足，继续读；total < 0 表示协议判不出长度，回退空闲判据
                if (total < 0 || filled >= buffer.Length)
                {
                    return ReadUntilIdle(socket, buffer, filled);
                }

                int n;
                try
                {
                    n = socket.Receive(buffer, filled, buffer.Length - filled, SocketFlags.None);
                }
                catch (SocketException) { return null; }
                catch (ObjectDisposedException) { return null; }
                catch (Exception) { return null; }

                if (n <= 0)
                {
                    return filled > 0 ? Trim(buffer, filled) : null;
                }
                filled += n;
            }
        }

        /// <summary>
        /// 按协议给出的整帧长度把剩余字节读满。
        /// </summary>
        private static byte[] ReadExact(Socket socket, byte[] buffer, int filled, int total)
        {
            if (total > buffer.Length)
            {
                byte[] grown = new byte[total];
                Array.Copy(buffer, grown, filled);
                buffer = grown;
            }

            while (filled < total)
            {
                int n;
                try
                {
                    n = socket.Receive(buffer, filled, total - filled, SocketFlags.None);
                }
                catch (SocketException) { return null; }
                catch (ObjectDisposedException) { return null; }
                catch (Exception) { return null; }

                if (n <= 0)
                {
                    return null;
                }
                filled += n;
            }

            return Trim(buffer, total);
        }

        /// <summary>
        /// 协议没有长度字段时的兜底：继续收，直到 <see cref="IdleTimeoutMs"/> 内没有新数据为止。
        /// </summary>
        private static byte[] ReadUntilIdle(Socket socket, byte[] buffer, int filled)
        {
            while (filled < buffer.Length)
            {
                bool readable;
                try
                {
                    readable = socket.Poll(IdleTimeoutMs * 1000, SelectMode.SelectRead);
                }
                catch
                {
                    break;
                }
                if (!readable)
                {
                    break;
                }

                int n;
                try
                {
                    n = socket.Receive(buffer, filled, buffer.Length - filled, SocketFlags.None);
                }
                catch
                {
                    break;
                }
                if (n <= 0)
                {
                    break;
                }
                filled += n;
            }

            return filled > 0 ? Trim(buffer, filled) : null;
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
        /// <summary>
        /// 向指定客户端发送数据
        /// </summary>
        /// <param name="data">要发送的字节数据</param>
        /// <param name="client">目标客户端连接句柄</param>
        public void SendToClient(byte[] data, IClientSession client)
        {
            if (client == null || data == null || data.Length == 0) return;
            client.Send(data);
        }
    }

    /// <summary>
    /// TCP 客户端连接句柄（IClientSession 的 TCP 实现）
    /// </summary>
    internal sealed class TcpClientSession : IClientSession
    {
        private readonly Socket _socket;

        public TcpClientSession(Socket socket)
        {
            _socket = socket;
        }

        /// <summary>
        /// 底层 Socket（仅供 TcpServerCommunication 内部使用）
        /// </summary>
        public Socket Socket => _socket;

        public EndPoint RemoteEndPoint
        {
            get
            {
                try { return _socket?.RemoteEndPoint; }
                catch { return null; }
            }
        }

        public bool IsConnected
        {
            get
            {
                try { return _socket != null && _socket.Connected; }
                catch { return false; }
            }
        }

        public void Send(byte[] data)
        {
            if (data == null || data.Length == 0 || _socket == null) return;
            try
            {
                if (_socket.Connected)
                    _socket.Send(data);
            }
            catch { }
        }

        /// <summary>
        /// 关闭并释放连接
        /// </summary>
        public void Dispose()
        {
            try { _socket?.Shutdown(SocketShutdown.Both); } catch { }
            try { _socket?.Close(); } catch { }
            try { _socket?.Dispose(); } catch { }
        }
    }
}
