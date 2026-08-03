using System;

namespace PLCTest.SeverInterface
{
    /// <summary>
    /// 服务端通信接口（与 IClientCommunication 对称）
    /// </summary>
    public interface ISeverCommunication
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
        /// 发送数据到客户端
        /// </summary>
        /// <param name="data">要发送的字节数据</param>
        void SendToClient(byte[] data);
        /// <summary>
        /// 客户端数据到达事件
        /// </summary>
        event Action<byte[]> OnDataReceived;
    }
}
