namespace PlcTest.Client.ClientInterface
{
    /// <summary>
    /// 西门子 S7 客户端协议需要额外提供的东西：两段建链握手。
    /// <para>
    /// S7 在 TCP 建连之后还要再握两次手：先 COTP 连接（0xE0），再 S7 的 Setup Communication（0xF0）。
    /// 缺任何一步，PLC 都不会应答后续读写请求——表现是「TCP 连上了但读不到数据」。
    /// 这两步与普通读写无关，所以没有塞进 <see cref="IPlcClientProtocol"/>：
    /// 三菱/欧姆龙/汇川的协议不需要实现它们。
    /// </para>
    /// <para>
    /// 判定依据是能力而不是厂商枚举：<c>PlcClientBuilder.Build()</c> 只问
    /// 「协议是否实现了这个接口」，换一个 S7 变体（S7-300 / S7-1500）时不必改客户端。
    /// </para>
    /// </summary>
    public interface IS7SessionProtocol
    {
        /// <summary>
        /// 是否已完成 COTP + Setup Communication 两段握手。为 true 时不会重复握手。
        /// </summary>
        bool IsSessionEstablished { get; }

        /// <summary>
        /// 握手时与 PLC 协商到的 PDU 长度（Setup Communication 响应里的值）。
        /// 0 表示尚未协商；单次读写的参数区 + 数据区不应超过这个长度。
        /// </summary>
        int NegotiatedPduLength { get; }

        /// <summary>
        /// 构建 COTP 连接请求（Connection Request，22 字节）。
        /// </summary>
        /// <returns>完整报文（含 TPKT 头）</returns>
        byte[] BuildCotpConnectRequest();

        /// <summary>
        /// 解析 COTP 连接确认（Connection Confirm，PDU 类型 0xD0）。
        /// </summary>
        /// <param name="data">响应报文</param>
        /// <param name="error">失败原因</param>
        /// <returns>true 表示 COTP 层已连接</returns>
        bool ParseCotpConnectResponse(byte[] data, out string error);

        /// <summary>
        /// 构建 S7 的 Setup Communication 请求（功能码 0xF0，协商 PDU 长度）。
        /// </summary>
        /// <returns>完整报文（含 TPKT + COTP 头）</returns>
        byte[] BuildSetupCommunicationRequest();

        /// <summary>
        /// 解析 Setup Communication 响应，取回 PLC 允许的 PDU 长度。
        /// 成功后实现方应把 <see cref="IsSessionEstablished"/> 置为 true。
        /// </summary>
        /// <param name="data">响应报文</param>
        /// <param name="pduLength">PLC 协商出的 PDU 长度</param>
        /// <param name="error">失败原因</param>
        /// <returns>true 表示 S7 会话已建立</returns>
        bool ParseSetupCommunicationResponse(byte[] data, out int pduLength, out string error);
    }
}
