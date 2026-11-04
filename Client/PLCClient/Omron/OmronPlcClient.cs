using PlcTest.Client.ClientInterface;
using PlcTest.Client.ClientProtocolManagement.Omron;
using PlcTest.Models;
using System;

namespace PlcTest.Client.PlcClient.Omron
{
    /// <summary>
    /// 欧姆龙 FINS/TCP 客户端。
    /// 与其它厂商的差别只有一处：TCP 建连后必须先做节点地址握手，
    /// 其余 8 个读写方法全部复用 <see cref="CommunicationPlcClient"/>，不再重复实现。
    /// </summary>
    public class OmronPlcClient : CommunicationPlcClient
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comm">通讯方式</param>
        /// <param name="protocol">协议实现（可选），为 null 时使用 <see cref="OmronFinsTcpClientProtocol"/></param>
        public OmronPlcClient(IClientCommunication comm, IPlcClientProtocol protocol = null)
            : base(comm, protocol ?? new OmronFinsTcpClientProtocol())
        {
        }

        /// <summary>
        /// 连接：建连成功后立刻做节点地址握手。
        /// 不握手时 PLC 会静默丢弃后续命令，表现是"连接正常但读不到数据"，所以失败必须显式返回。
        /// </summary>
        public override Result<bool> Connect()
        {
            Result<bool> result = base.Connect();
            if (!result.IsSuccess)
            {
                return result;
            }

            Result<bool> handshake = PerformHandshake();
            if (!handshake.IsSuccess)
            {
                // 握手不成功说明这条连接不可用，断开避免留下半连状态
                Disconnect();
                return handshake;
            }
            return result;
        }

        /// <summary>
        /// 节点地址握手：发 20 字节请求，从 24 字节响应里取回本机与 PLC 的节点号。
        /// </summary>
        private Result<bool> PerformHandshake()
        {
            OmronFinsTcpClientProtocol fins = _communicationProtocol as OmronFinsTcpClientProtocol;
            if (fins == null)
            {
                // 非 FINS 实现不做握手
                return Result<bool>.Success(true);
            }
            if (fins.IsHandshaked)
            {
                return Result<bool>.Success(true);
            }

            try
            {
                byte[] request = fins.BuildHandshakeFrame();
                byte[] response = _comm.SendAndReceiveData(request, fins.GetResponseFrameLength);

                string error;
                byte sa1;
                byte da1;
                if (!fins.ParseHandshakeResponse(response, out sa1, out da1, out error))
                {
                    return Result<bool>.Fail(error);
                }

                RaiseCommMessage($"FINS 节点地址握手成功：本机节点 {sa1}，PLC 节点 {da1}");
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail("FINS 节点地址握手异常: " + ex.Message);
            }
        }
    }
}
