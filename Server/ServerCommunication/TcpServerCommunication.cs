
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
        /// 客户端连接的Socket对象集合
        /// </summary>
        private readonly List<Socket> _sockets = new List<Socket>();
        /// <summary>
        /// Socket对象集合管理锁
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
        /// 已连接客户端数量
        /// </summary>
        public int ClientCount
        {
            get
            {
                lock (_socketsLock)
                {
                    return _sockets.Count;
                }
            }
        }

        /// <summary>
        /// 客户端数据到达事件（附带客户端 Socket）
        /// </summary>
        public event Action<byte[], object> OnDataReceived;

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
            IsRunning = true;
            // 绑定IP地址和端口号
            ListenSocket.Bind(IpAndPoint);
            //监听数量
            ListenSocket.Listen(10);
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
            if (this._listenSocket != null)
            {
                this._listenSocket.Close();
            }
            foreach (var socket in _sockets)
            {
                if (socket != null && socket.Connected)
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Close();
                }
            }
        }
        /// <summary>
        /// 添加Socket对象
        /// </summary>
        /// <param name="client"></param>
        private void AddSocket(Socket client)
        {
            if (client == null) return;
            lock (_socketsLock)
            {
                _sockets.Add(client);
            }
        }
        /// <summary>
        /// 移除Socket对象
        /// </summary>
        /// <param name="client"></param>
        private void RemoveSocket(Socket client)
        {
            if (client == null) return;
            lock (_socketsLock)
            {
                if (_sockets.Contains(client))
                    _sockets.Remove(client);
            }
            try { client.Shutdown(System.Net.Sockets.SocketShutdown.Both); } catch { }
            try { client.Close(); } catch { }
            try { client.Dispose(); } catch { }

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
                    Socket[] listCopy;
                    lock (_socketsLock)
                    {
                        listCopy = _sockets.ToArray();
                    }

                    foreach (var s in listCopy)
                    {
                        bool remove = false;
                        try
                        {
                            // 常用断连检测：如果未连接或可读但可用字节为0，说明远端已关闭连接
                            if (!s.Connected)
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
                            RemoveSocket(s);
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
                        // EndAccept 可能抛出 ObjectDisposedException，当 socket 已关闭时忽略
                        client = _listenSocket.EndAccept(iResult);
                    }
                    catch (ObjectDisposedException)
                    {
                        // 如果正在关闭，静默返回；否则记录日志
                        return;
                    }
                    catch (SocketException)
                    {
                        // 视情况处理或记录
                        return;
                    }

                    if (client != null && client.Connected)
                    {
                        Task task = ReceiveDataFromClient(client);
                        AddSocket(client);
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
        }
        private Task ReceiveDataFromClient(Socket rcvSocket)
        {
            return Task.Run(() =>
            {
                using (rcvSocket)
                {
                    while (IsRunning)
                    {
                        byte[] byt = new byte[1024];
                        int len = rcvSocket.Receive(byt, 0, byt.Length, SocketFlags.None);
                        if (len <= 0)
                        {
                            continue;
                        }
                        byte[] receivedData = new byte[len];
                        Array.Copy(byt, 0, receivedData, 0, len);
                        OnDataReceived?.Invoke(receivedData, rcvSocket);
                    }
                }
            });
        }
        /// <summary>
        /// 向指定客户端发送数据
        /// </summary>
        /// <param name="data">要发送的字节数据</param>
        /// <param name="client">目标客户端 Socket</param>
        public void SendToClient(byte[] data, object client)
        {
            var socket = client as Socket;
            if (socket == null || data == null || data.Length == 0) return;
            if (!socket.Connected) return;
            try
            {
                socket.Send(data);
            }
            catch { }
        }
    }
}
