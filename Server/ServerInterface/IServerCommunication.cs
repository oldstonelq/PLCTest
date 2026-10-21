using System;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// 服务端通信接口（与 IClientCommunication 对称）
    /// </summary>
    public interface IServerCommunication
    {
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
}
