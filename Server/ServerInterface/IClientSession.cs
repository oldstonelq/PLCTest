using System.Net;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// 服务端已连接客户端的抽象句柄——替代裸 object 作为客户端标识，
    /// 让「收数据的是谁、回数据发给谁」在类型上说得清楚。
    /// </summary>
    public interface IClientSession
    {
        /// <summary>
        /// 远端地址（TCP 为客户端 IP:Port，串口无客户端概念时为 null）
        /// </summary>
        EndPoint RemoteEndPoint { get; }

        /// <summary>
        /// 连接是否仍然有效
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 向该客户端发送数据
        /// </summary>
        /// <param name="data">要发送的字节数据</param>
        void Send(byte[] data);
    }
}
