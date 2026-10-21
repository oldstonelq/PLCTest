using PlcTest.Models;
using System;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// 服务端协议接口——负责“请求解析”与“响应构建”，把帧结构的差异收敛在协议实现里。
    /// 服务端核心逻辑（寄存器表、读写分发）不感知具体是 1E/3E/4E、二进制还是 ASCII。
    /// </summary>
    public interface IServerProtocol
    {
        /// <summary>
        /// 校验请求报文头是否属于本协议
        /// </summary>
        bool ValidateRequestHeader(byte[] data);

        /// <summary>
        /// 解析请求报文为结构化数据
        /// </summary>
        RequestInfo ParseRequest(byte[] data);

        /// <summary>
        /// 构建响应头。返回的字节数组包含协议头（含长度字段与结束码占位，成功时结束码为 0）。
        /// </summary>
        /// <param name="request">原始请求报文（用于回显网络号/站号等字段）</param>
        /// <param name="responseDataLength">响应数据区长度（不含结束码）</param>
        byte[] BuildResponseHeader(byte[] request, int responseDataLength);

        /// <summary>
        /// 构建字设备读取响应数据
        /// </summary>
        byte[] BuildWordReadResponse(short[] words);

        /// <summary>
        /// 构建位设备读取响应数据
        /// </summary>
        byte[] BuildBitReadResponse(bool[] bits);

        /// <summary>
        /// 构建仅含结束码的响应数据区（成功传 0，失败传错误码）
        /// </summary>
        byte[] BuildEndCodeResponse(ushort endCode);

        /// <summary>
        /// 从写入请求的数据区解析位数据
        /// </summary>
        bool[] ParseWriteBits(byte[] data, int points);

        /// <summary>
        /// 从写入请求的数据区解析字数据
        /// </summary>
        short[] ParseWriteWords(byte[] data, int points);
    }
}
