namespace PlcTest.Client.ClientInterface
{
    /// <summary>
    /// 欧姆龙 FINS 客户端协议需要额外提供的东西：节点地址握手。
    /// <para>
    /// FINS/TCP 建连后必须先发一次节点地址请求，否则 PLC 会静默丢弃后续命令
    /// （表现是「连上了但读不到数据」）。握手这几个方法与普通读写无关，
    /// 所以没有塞进 <see cref="IPlcClientProtocol"/>——其它厂商的协议不需要实现它们。
    /// </para>
    /// <para>
    /// 与服务器端的 <c>IOmronFinsServerProtocol</c> 对称：客户端只认这个接口，
    /// 换一个 FINS 变体（例如 FINS/UDP）时不必再改 <c>OmronPlcClient</c>。
    /// </para>
    /// </summary>
    public interface IOmronFinsClientProtocol
    {
        /// <summary>
        /// 是否已完成节点地址握手。为 true 时不会重复握手。
        /// </summary>
        bool IsHandshaked { get; }

        /// <summary>
        /// 构建节点地址请求帧（20 字节，外层命令字 0）。
        /// </summary>
        byte[] BuildHandshakeFrame();

        /// <summary>
        /// 解析节点地址响应（24 字节），取回双方节点号；握手成功后实现方应把
        /// <see cref="IsHandshaked"/> 置为 true。
        /// </summary>
        /// <param name="data">响应报文</param>
        /// <param name="sa1">本机节点号（源）</param>
        /// <param name="da1">PLC 节点号（目的）</param>
        /// <param name="error">失败原因</param>
        /// <returns>true 表示握手成功</returns>
        bool ParseHandshakeResponse(byte[] data, out byte sa1, out byte da1, out string error);
    }
}
