using PlcTest.Server.ServerInterface;
using PlcTest.Server.ServerProtocolManagement.Melsec;

namespace PlcTest.Server.PlcServer.Melsec
{
    /// <summary>
    /// 三菱 MC 3E 二进制协议服务端：通用逻辑在 <see cref="MelsecPlcServer"/>，
    /// 这里只提供默认协议实现，保留这个入口是为了让 Builder/调用方语义保持清晰。
    /// </summary>
    public class MelsecMc3EPlcServer : MelsecPlcServer
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comm">服务端通讯方式</param>
        /// <param name="protocol">协议实现（可选），若为 null 则使用 MelsecMc3EServerProtocol</param>
        public MelsecMc3EPlcServer(IServerCommunication comm, IServerProtocol protocol = null)
            : base(comm, protocol ?? new MelsecMc3EServerProtocol())
        {
        }
    }
}