using PlcTest.Models;
using System.Collections.Generic;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// S7 请求帧的种类。服务端靠它决定怎么应答，避免在一堆字节偏移的 if 里判断。
    /// </summary>
    public enum S7RequestKind
    {
        /// <summary>认不出来的帧（TPKT 版本不对、协议 ID 不是 0x32、功能码不认识）</summary>
        Unknown,
        /// <summary>COTP 连接请求（PDU 类型 0xE0），回 CC</summary>
        CotpConnect,
        /// <summary>Setup Communication（功能码 0xF0），回协商后的 PDU 长度</summary>
        SetupCommunication,
        /// <summary>读变量（功能码 0x04）</summary>
        ReadVar,
        /// <summary>写变量（功能码 0x05）</summary>
        WriteVar,
    }

    /// <summary>
    /// 一个变量项：S7 的读/写请求里，参数区是「功能码 + 项数 + N × 12 字节 ANY 指针」，
    /// 这 12 字节就是这里的六个字段。
    /// <para>
    /// 单变量与多变量走同一个模型——单变量只是 <c>Items.Count == 1</c>，
    /// 不需要两套解析。原先「项数 &gt; 1 直接拒绝」的做法会把合法的批量请求判成错误。
    /// </para>
    /// </summary>
    public class S7ItemRequest
    {
        /// <summary>解析出的区域</summary>
        public MemoryArea Area { get; set; }

        /// <summary>DB 块号，非 DB 区为 0</summary>
        public int DbNumber { get; set; }

        /// <summary>
        /// 区内字节偏移。S7 帧里的地址字段是**位**偏移，解析时已除以 8，
        /// 所以这里的 100 表示 M100.0 所在的那个字节。
        /// </summary>
        public int ByteOffset { get; set; }

        /// <summary>本项按位还是按字访问</summary>
        public bool IsBit { get; set; }

        /// <summary>位访问=位数；字访问=字数</summary>
        public int Length { get; set; }

        /// <summary>写入数据（仅写请求有值）。已按「位」还原成逐点 0/1 或按「字」还原成大端字节</summary>
        public byte[] WriteData { get; set; }
    }

    /// <summary>
    /// 一个变量项的读取结果。错误与数据二选一：<see cref="ErrorCode"/> 非 0 时
    /// 该项在响应里只有一个「返回码 + 错误码」的数据项，不带数据。
    /// <para>
    /// 多变量请求里**每项独立成败**——某一项地址越界不应该让整帧失败，
    /// 真实 PLC 也是这样：好的项照常回数据，坏的项回错误码。
    /// </para>
    /// </summary>
    public class S7ItemResult
    {
        /// <summary>本项是位还是字，决定数据项用 TS=0x03 还是 TS=0x04 编码</summary>
        public bool IsBit { get; set; }

        /// <summary>位数据（IsBit 为 true 时使用）</summary>
        public bool[] Bits { get; set; }

        /// <summary>字数据（IsBit 为 false 时使用）</summary>
        public short[] Words { get; set; }

        /// <summary>0 表示成功；非 0 时按数据项错误码编码（0x05 地址越界、0x0A 对象不存在）</summary>
        public ushort ErrorCode { get; set; }

        /// <summary>构造一个成功的位项</summary>
        public static S7ItemResult OfBits(bool[] bits)
        {
            return new S7ItemResult { IsBit = true, Bits = bits };
        }

        /// <summary>构造一个成功的字项</summary>
        public static S7ItemResult OfWords(short[] words)
        {
            return new S7ItemResult { IsBit = false, Words = words };
        }

        /// <summary>构造一个失败项</summary>
        public static S7ItemResult Fail(bool isBit, ushort errorCode)
        {
            return new S7ItemResult { IsBit = isBit, ErrorCode = errorCode };
        }
    }

    /// <summary>
    /// S7 读写请求解析后的结构化数据。
    /// <para>
    /// 为什么不复用 <see cref="RequestInfo"/>：那是 MC 的模型（软元件代码 + 24 位起始地址 + 结束码），
    /// S7 的地址是「区码 + DB 号 + 字节偏移」三段，硬塞进去会把 DB 号和字节偏移的含义搅在一起。
    /// </para>
    /// </summary>
    public class S7RequestInfo
    {
        /// <summary>请求里的变量项，顺序与帧内一致。单变量时只有一项。</summary>
        public IReadOnlyList<S7ItemRequest> Items { get; set; }

        /// <summary>项数（便于调用方少写一次 Items.Count）</summary>
        public int Count
        {
            get { return Items == null ? 0 : Items.Count; }
        }
    }
}
