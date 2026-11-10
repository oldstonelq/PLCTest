using PlcTest.Models;
using System.Collections.Generic;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// 西门子 S7 服务端协议接口：负责「请求分类 / 解析」与「响应构建」，
    /// 把 TPKT + COTP + S7 头 + 参数区 + 数据项这五层的差异收敛在实现里。
    /// <para>
    /// 刻意**不实现 <see cref="IServerProtocol"/>**：那个接口围绕 MC 的「结束码」设计
    /// （<c>BuildEndCodeResponse(ushort)</c>、<c>ParseRequest</c> 返回 MC 的 <see cref="RequestInfo"/>），
    /// 而 S7 没有结束码 —— 错误走 S7 头的 [错误类][错误码] 或数据项返回码。
    /// 与 <see cref="IModbusServerProtocol"/>、<see cref="IOmronFinsServerProtocol"/> 一样，
    /// 协议族各用各的接口，硬套 MC 那套只会把两种语义缠在一起。
    /// </para>
    /// </summary>
    public interface IS7ServerProtocol
    {
        /// <summary>
        /// 本协议支持的软元件区域。与客户端 <c>GetSupportedAreas()</c> 对称。
        /// </summary>
        IReadOnlyList<MemoryArea> GetSupportedAreas();

        /// <summary>
        /// 从 TPKT 头推断整帧长度（S7 的长度字段在前 4 字节，比 MC 的「靠位置猜」简单）。
        /// </summary>
        /// <param name="buffer">已收到的字节</param>
        /// <param name="available">buffer 中的有效字节数</param>
        /// <returns>整帧长度；0 表示头部还不够</returns>
        int GetRequestFrameLength(byte[] buffer, int available);

        /// <summary>
        /// 判断这一帧是握手还是读写。服务端据此分派，避免在帧处理里散落字节偏移。
        /// </summary>
        S7RequestKind ClassifyRequest(byte[] data);

        /// <summary>
        /// 构建 COTP 连接确认（CC）。目标引用回显请求的源引用，参数区原样带回。
        /// </summary>
        byte[] BuildCotpConnectResponse(byte[] request);

        /// <summary>
        /// 构建 Setup Communication 的应答，把请求里申请的 PDU 长度回显（受服务端上限约束）。
        /// </summary>
        byte[] BuildSetupCommunicationResponse(byte[] request);

        /// <summary>
        /// 解析读变量请求（功能码 0x04）。支持多变量：项数取自参数区，逐项解析。
        /// </summary>
        /// <returns>解析失败时返回 false，error 给中文原因</returns>
        bool TryParseReadRequest(byte[] data, out S7RequestInfo request, out string error);

        /// <summary>
        /// 解析写变量请求（功能码 0x05），含每项的写入数据。
        /// </summary>
        bool TryParseWriteRequest(byte[] data, out S7RequestInfo request, out string error);

        /// <summary>
        /// 构建读响应：参数区回显「功能码 + 项数」，数据区按顺序给每项一个数据项。
        /// 位数据项按 S7 规范**按位打包**（50 位只占 7 字节）。
        /// </summary>
        /// <param name="request">原始请求，用于回显 PDU 引用</param>
        /// <param name="items">与请求项一一对应的结果，顺序必须一致</param>
        byte[] BuildReadResponse(byte[] request, IReadOnlyList<S7ItemResult> items);

        /// <summary>
        /// 构建写响应：参数区 <c>05 项数</c>，数据区每项 1 字节返回码（0xFF 成功）。
        /// </summary>
        /// <param name="request">原始请求，用于回显 PDU 引用</param>
        /// <param name="errorCodes">与请求项一一对应的错误码，0 表示成功</param>
        byte[] BuildWriteResponse(byte[] request, IReadOnlyList<ushort> errorCodes);

        /// <summary>
        /// 构建作业级错误响应：整个请求根本无法解析时（区码不认识、项数不合法、数据被截断），
        /// 错误填在 S7 头 [错误类][错误码]，参数区回显功能码、项数填 0。
        /// <para>
        /// 不用「数据项错误」代替：数据项错误表达的是「这一项读不到」，而解析失败是「整条作业都不成立」，
        /// 两者的错误类/错误码位置不同，客户端报出来的文案也不一样。
        /// </para>
        /// </summary>
        /// <param name="request">原始请求，用于回显 PDU 引用</param>
        /// <param name="functionCode">原请求的功能码（0x04 / 0x05）</param>
        /// <param name="errorClass">S7 错误类，如 0x81 应用关系</param>
        /// <param name="errorCode">S7 错误码，如 0x05 参数非法</param>
        byte[] BuildJobErrorResponse(byte[] request, byte functionCode, byte errorClass, byte errorCode);
    }
}
