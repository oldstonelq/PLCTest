using PlcTest.Models;
using System.Collections.Generic;

namespace PlcTest.Client.ClientInterface
{
    /// <summary>
    /// 一次多变量读取里的一项结果。与请求项一一对应，顺序不变。
    /// <para>
    /// 某项失败只体现在该项的 <see cref="ErrorCode"/> 上，整帧仍可能是成功的——
    /// 真实 PLC 也是这么回：第二个地址越界时，第一个地址的数据照样带回来。
    /// </para>
    /// </summary>
    public class S7ItemValue
    {
        /// <summary>该项是位还是字</summary>
        public bool IsBit { get; set; }

        /// <summary>位数据（<see cref="IsBit"/> 为 true 时有值）</summary>
        public bool[] Bits { get; set; }

        /// <summary>字数据（<see cref="IsBit"/> 为 false 时有值）</summary>
        public short[] Words { get; set; }

        /// <summary>0 表示成功；非 0 是数据项返回错误码（0x05 地址越界、0x0A 对象不存在）</summary>
        public ushort ErrorCode { get; set; }

        /// <summary>该项是否成功</summary>
        public bool IsSuccess
        {
            get { return ErrorCode == 0; }
        }
    }

    /// <summary>
    /// S7 的多变量读写：一条请求里带多个不连续的地址，PLC 一次回全部数据。
    /// <para>
    /// 为什么不放进 <see cref="IPlcClientProtocol"/>：三菱/汇川/欧姆龙的批量读写是「同区连续地址」，
    /// 靠地址 + 长度就能表达；S7 的多变量是**任意区、任意 DB 混合**，签名（地址列表 + 每项结果）
    /// 与单变量完全不同。塞进公共接口会让 10 个协议都被迫实现一个用不上的方法。
    /// </para>
    /// </summary>
    public interface IS7MultiVarProtocol
    {
        /// <summary>
        /// 构建多变量读请求的完整报文（含 TPKT + COTP + S7 头）。
        /// </summary>
        /// <param name="addresses">要读的地址，顺序即响应里数据项的顺序</param>
        /// <returns>完整报文</returns>
        byte[] BuildMultiVarReadPack(IList<PlcAddress> addresses);

        /// <summary>
        /// 构建多变量写请求的完整报文。
        /// </summary>
        /// <param name="addresses">要写的地址</param>
        /// <param name="data">每项的写入数据，与地址一一对应（位=逐点 0/1 字节，字=大端字节）</param>
        /// <returns>完整报文</returns>
        byte[] BuildMultiVarWritePack(IList<PlcAddress> addresses, IList<byte[]> data);

        /// <summary>
        /// 解析多变量读响应。项数必须与请求一致，否则说明帧错位，直接判失败。
        /// </summary>
        /// <param name="data">响应报文</param>
        /// <param name="addresses">原请求地址，用于确定每项的长度与位字类型</param>
        /// <returns>与地址顺序一致的项结果</returns>
        Result<S7ItemValue[]> ParseMultiVarReadResponse(byte[] data, IList<PlcAddress> addresses);
    }
}
