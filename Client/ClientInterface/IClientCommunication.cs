using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlcTest.Client.ClientInterface
{
    /// <summary>
    /// 客户端通讯方式接口
    /// </summary>
    public interface IClientCommunication
    {
        /// <summary>
        /// 连接状态
        /// </summary>
        bool IsConnected { get; }
        /// <summary>
        /// 连接服务端
        /// </summary>
        void ConnectServer();
        /// <summary>
        /// 断开服务端连接
        /// </summary>
        void DisconnectServer();
        /// <summary>
        /// 发送并返回数据
        /// </summary>
        /// <param name="data">发送报文</param>
        /// <returns>返回接收报文</returns>
        byte[] SendAndReceiveData(byte[] data);
    }
}
