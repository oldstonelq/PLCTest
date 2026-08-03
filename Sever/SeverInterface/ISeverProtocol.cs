using PLCTest.Models;

namespace PLCTest.SeverInterface
{
    /// <summary>
    /// 服务端协议接口——负责解析请求报文和构建响应报文
    /// </summary>
    public interface ISeverProtocol
    {
        /// <summary>
        /// 验证请求报文头（检查 0x50 0x00 标识和最小长度）
        /// </summary>
        /// <param name="data">原始请求报文</param>
        /// <returns>true=有效, false=无效</returns>
        bool ValidateRequestHeader(byte[] data);

        /// <summary>
        /// 解析请求报文为结构化数据
        /// </summary>
        /// <param name="data">原始请求报文</param>
        /// <returns>解析后的请求信息</returns>
        RequestInfo ParseRequest(byte[] data);

        /// <summary>
        /// 构建 MC 3E 响应头（11 字节）
        /// </summary>
        /// <param name="request">原始请求报文（用于提取网络号等字段）</param>
        /// <param name="responseDataLength">响应数据区长度</param>
        /// <returns>11 字节响应头</returns>
        byte[] BuildResponseHeader(byte[] request, int responseDataLength);

        /// <summary>
        /// 构建字设备（D 寄存器）读取响应数据
        /// </summary>
        /// <param name="words">字数据数组，每个 short 占 2 字节</param>
        /// <returns>响应数据区字节</returns>
        byte[] BuildWordReadResponse(short[] words);

        /// <summary>
        /// 构建位设备（M 线圈）读取响应数据（每字节存 2 个位，高4位在前）
        /// </summary>
        /// <param name="bits">位数据数组</param>
        /// <returns>响应数据区字节</returns>
        byte[] BuildBitReadResponse(bool[] bits);
    }
}
