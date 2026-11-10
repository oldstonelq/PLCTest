using System;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// 服务端通信接口（与 IClientCommunication 对称）
    /// </summary>
    public interface IServerCommunication
    {
        /// <summary>
        /// 请求帧长解析：收到数据后回调，由协议决定"这一帧还要读多少"。
        /// 返回值语义同 <see cref="IServerProtocol.GetRequestFrameLength"/>：
        /// &gt;0 整帧长度、=0 头部不足继续读、&lt;0 无法判定（通讯层回退空闲判据）。
        /// 未设置时通讯层按空闲判据收帧。
        /// </summary>
        ServerFrameLengthResolver FrameLengthResolver { get; set; }
        /// <summary>
        /// 服务是否正在运行
        /// </summary>
        bool IsRunning { get; }
        /// <summary>
        /// 已连接客户端数量
        /// </summary>
        int ClientCount { get; }
        /// <summary>
        /// 启动服务端监听
        /// </summary>
        void Start();
        /// <summary>
        /// 停止服务端监听并断开所有客户端
        /// </summary>
        void Stop();
        /// <summary>
        /// 发送数据到指定客户端
        /// </summary>
        /// <param name="data">要发送的字节数据</param>
        /// <param name="client">目标客户端句柄（串口无客户端概念时传 null）</param>
        void SendToClient(byte[] data, IClientSession client);
        /// <summary>
        /// 客户端数据到达事件（附带客户端句柄）
        /// </summary>
        event Action<byte[], IClientSession> OnDataReceived;
    }

    /// <summary>
    /// 服务端请求帧长解析委托：通讯层收到首段字节后回调，由协议决定"这一帧还要读多少"。
    /// </summary>
    /// <param name="buffer">已收到的字节</param>
    /// <param name="available">buffer 中的有效字节数</param>
    /// <returns>整帧长度；0 表示头部不足需继续读；-1 表示无法判定</returns>
    public delegate int ServerFrameLengthResolver(byte[] buffer, int available);
}
