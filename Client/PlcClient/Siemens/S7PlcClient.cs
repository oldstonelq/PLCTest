using PlcTest.Client.ClientInterface;
using PlcTest.Models;
using System;

namespace PlcTest.Client.PlcClient.Siemens
{
    /// <summary>
    /// 西门子 S7 客户端。
    /// 与其它厂商的差别只有一处：TCP 建连后还要做 COTP 连接与 Setup Communication 两段握手，
    /// 其余 8 个读写方法全部复用 <see cref="CommunicationPlcClient"/>，不再重复实现。
    /// </summary>
    public class S7PlcClient : CommunicationPlcClient
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comm">通讯方式</param>
        /// <param name="protocol">S7 协议实现；必须实现 <see cref="IS7SessionProtocol"/> 才能握手</param>
        public S7PlcClient(IClientCommunication comm, IPlcClientProtocol protocol)
            : base(comm, protocol)
        {
            if (!(protocol is IS7SessionProtocol))
            {
                // 传错了协议就别等到 Connect 才发现「TCP 连上了但读不到」
                throw new ArgumentException("S7PlcClient 需要实现了 IS7SessionProtocol 的协议实现", nameof(protocol));
            }
        }

        /// <summary>
        /// 连接：TCP 建连成功后依次做 COTP 连接与 Setup Communication。
        /// 少任何一步，S7-1200 都不会应答后续读写请求，所以失败必须显式返回并断开。
        /// </summary>
        public override Result<bool> Connect()
        {
            Result<bool> result = base.Connect();
            if (!result.IsSuccess)
            {
                return result;
            }

            Result<bool> session = EstablishSession();
            if (!session.IsSuccess)
            {
                // 握手不成功说明这条连接不可用，断开避免留下半连状态
                Disconnect();
                return session;
            }
            return result;
        }

        /// <summary>
        /// 两段握手：COTP 连接（0xE0）→ Setup Communication（0xF0，协商 PDU 长度）。
        /// 每次都重新握手：TCP 会话已经换了，旧会话的协商结果不能沿用。
        /// </summary>
        private Result<bool> EstablishSession()
        {
            // 只认接口：换 S7 变体（S7-300 / S7-1500）时不必改这里
            IS7SessionProtocol s7 = _communicationProtocol as IS7SessionProtocol;
            if (s7 == null)
            {
                return Result<bool>.Success(true);
            }

            try
            {
                // 1) COTP 连接
                byte[] cotpRequest = s7.BuildCotpConnectRequest();
                byte[] cotpResponse = _comm.SendAndReceiveData(cotpRequest, _communicationProtocol.GetResponseFrameLength);

                string error;
                if (!s7.ParseCotpConnectResponse(cotpResponse, out error))
                {
                    return Result<bool>.Fail(error);
                }
                RaiseCommMessage("S7 COTP 连接已建立");

                // 2) Setup Communication
                byte[] setupRequest = s7.BuildSetupCommunicationRequest();
                byte[] setupResponse = _comm.SendAndReceiveData(setupRequest, _communicationProtocol.GetResponseFrameLength);

                int pduLength;
                if (!s7.ParseSetupCommunicationResponse(setupResponse, out pduLength, out error))
                {
                    return Result<bool>.Fail(error);
                }
                RaiseCommMessage($"S7 会话已建立：协商 PDU 长度 {pduLength} 字节");
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail("S7 建链握手异常: " + ex.Message);
            }
        }
    }
}
