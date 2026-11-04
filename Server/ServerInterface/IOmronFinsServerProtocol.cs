using System;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// 欧姆龙 FINS 请求的结构化表示。
    /// FINS 与 MC / Modbus 都不同：地址是「区码 + 字地址 + 位号」三段，
    /// 位/字由区码本身区分，响应用结束代码而非异常码。
    /// </summary>
    public sealed class OmronFinsRequestInfo
    {
        /// <summary>原始 FINS 帧头（10 字节），响应要交换源/目标地址后回显</summary>
        public byte[] FinsHeader { get; set; }

        /// <summary>指令代码：0x0101 读、0x0102 写</summary>
        public ushort CommandCode { get; set; }

        /// <summary>存储器区域代码</summary>
        public byte AreaCode { get; set; }

        /// <summary>字地址（2 字节，已含 CNT 的 0x8000 偏移）</summary>
        public int WordAddress { get; set; }

        /// <summary>位号 0~15，字访问时为 0</summary>
        public byte BitIndex { get; set; }

        /// <summary>点数</summary>
        public ushort Points { get; set; }

        /// <summary>是否写命令</summary>
        public bool IsWrite { get; set; }

        /// <summary>写入数据（仅写命令有值）</summary>
        public byte[] WriteData { get; set; }
    }

    /// <summary>
    /// 欧姆龙 FINS/TCP 从站协议（服务端侧）。
    /// 负责帧定界、握手、请求解析与响应组包；寄存器存取由
    /// <see cref="PlcTest.Server.PlcServer.Omron.OmronFinsServer"/> 负责。
    /// </summary>
    public interface IOmronFinsServerProtocol
    {
        /// <summary>
        /// 根据已收字节推断整帧长度（外层 [4..7] 大端为「帧总长 - 8」）。
        /// </summary>
        /// <returns>大于 0 整帧长度；等于 0 头部不足；小于 0 无法判定</returns>
        int GetRequestFrameLength(byte[] buffer, int available);

        /// <summary>
        /// 是否为节点地址握手请求（外层命令字 0）。
        /// </summary>
        bool IsHandshakeRequest(byte[] data);

        /// <summary>
        /// 构建握手响应（24 字节）：外层 16 字节 + 客户端节点(4) + PLC 节点(4)。
        /// </summary>
        /// <param name="localNode">PLC 节点号</param>
        /// <param name="remoteNode">分配给客户端的节点号</param>
        byte[] BuildHandshakeResponse(byte localNode, byte remoteNode);

        /// <summary>
        /// 解析读写请求；格式非法返回 null。
        /// </summary>
        OmronFinsRequestInfo ParseRequest(byte[] data);

        /// <summary>构建读位响应（每点 1 字节，ON=01）</summary>
        byte[] BuildReadBitResponse(OmronFinsRequestInfo request, bool[] bits);

        /// <summary>构建读字响应（每字 2 字节，低字节在前）</summary>
        byte[] BuildReadWordResponse(OmronFinsRequestInfo request, short[] words);

        /// <summary>构建写响应（只有结束代码）</summary>
        byte[] BuildWriteResponse(OmronFinsRequestInfo request);

        /// <summary>
        /// 构建失败响应。常用结束代码：0x1101 区域代码不正确、0x1102 地址越界、0x1103 数据长度不符。
        /// </summary>
        byte[] BuildErrorResponse(OmronFinsRequestInfo request, ushort endCode);
    }
}
