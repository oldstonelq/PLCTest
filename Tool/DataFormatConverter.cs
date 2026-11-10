using PlcTest.Models;
using System;

namespace PlcTest.Tool
{
    /// <summary>
    /// 32 位数据的编解码工具：在「两个 16 位寄存器」与 int / uint / float 之间按 <see cref="DataFormat"/> 转换。
    /// <para>
    /// 输入输出都以**寄存器值**为单位（<c>short[]</c>），而不是原始字节流——
    /// 协议层已经把报文里的字节解析成寄存器值了，这里只需要处理寄存器之间的排列。
    /// </para>
    /// </summary>
    public static class DataFormatConverter
    {
        /// <summary>
        /// 把一个 16 位寄存器拆成高、低两个字节。
        /// </summary>
        private static void SplitWord(short word, out byte high, out byte low)
        {
            high = (byte)((word >> 8) & 0xFF);
            low = (byte)(word & 0xFF);
        }

        /// <summary>
        /// 把两个寄存器的内容按指定格式还原成 ABCD 顺序的 4 个字节（bytes[0] 为最高字节 A）。
        /// </summary>
        /// <param name="first">第一个寄存器（低地址）的值</param>
        /// <param name="second">第二个寄存器（高地址）的值</param>
        /// <param name="format">排列方式</param>
        /// <returns>4 个字节，A B C D 顺序</returns>
        public static byte[] ToBytes(short first, short second, DataFormat format)
        {
            byte a, b, c, d;
            SplitWord(first, out a, out b);
            SplitWord(second, out c, out d);

            switch (format)
            {
                // 字序不变，仅交换字内字节
                case DataFormat.BADC:
                    return new byte[] { b, a, d, c };
                // 字序颠倒
                case DataFormat.CDAB:
                    return new byte[] { c, d, a, b };
                // 字序与字节序全颠倒
                case DataFormat.DCBA:
                    return new byte[] { d, c, b, a };
                default:
                    return new byte[] { a, b, c, d };
            }
        }

        /// <summary>
        /// 从寄存器数组中读取一段 32 位数据，返回 ABCD 顺序的 4 个字节。
        /// </summary>
        /// <param name="words">寄存器数组</param>
        /// <param name="index">起始下标（占用 index、index+1 两个寄存器）</param>
        /// <param name="format">排列方式</param>
        public static byte[] ToBytes(short[] words, int index, DataFormat format)
        {
            if (words == null)
            {
                throw new ArgumentNullException(nameof(words));
            }
            if (index < 0 || index + 1 >= words.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "32 位数据需要连续 2 个寄存器");
            }
            return ToBytes(words[index], words[index + 1], format);
        }

        /// <summary>
        /// 按指定格式把两个寄存器解释成 32 位无符号整数。
        /// </summary>
        public static uint ToUInt32(short[] words, int index, DataFormat format)
        {
            byte[] abcd = ToBytes(words, index, format);
            return ((uint)abcd[0] << 24)
                 | ((uint)abcd[1] << 16)
                 | ((uint)abcd[2] << 8)
                 | abcd[3];
        }

        /// <summary>
        /// 按指定格式把两个寄存器解释成 32 位有符号整数。
        /// </summary>
        public static int ToInt32(short[] words, int index, DataFormat format)
        {
            return unchecked((int)ToUInt32(words, index, format));
        }

        /// <summary>
        /// 按指定格式把两个寄存器解释成单精度浮点数。
        /// </summary>
        public static float ToFloat(short[] words, int index, DataFormat format)
        {
            byte[] abcd = ToBytes(words, index, format);
            // BitConverter 按主机字节序工作，先转成本机顺序再转 float
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(abcd);
            }
            return BitConverter.ToSingle(abcd, 0);
        }

        /// <summary>
        /// 把 32 位无符号整数按指定格式拆成两个寄存器。
        /// </summary>
        public static short[] FromUInt32(uint value, DataFormat format)
        {
            byte a = (byte)((value >> 24) & 0xFF);
            byte b = (byte)((value >> 16) & 0xFF);
            byte c = (byte)((value >> 8) & 0xFF);
            byte d = (byte)(value & 0xFF);
            return Arrange(a, b, c, d, format);
        }

        /// <summary>
        /// 把 32 位有符号整数按指定格式拆成两个寄存器。
        /// </summary>
        public static short[] FromInt32(int value, DataFormat format)
        {
            return FromUInt32(unchecked((uint)value), format);
        }

        /// <summary>
        /// 把单精度浮点数按指定格式拆成两个寄存器。
        /// </summary>
        public static short[] FromFloat(float value, DataFormat format)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            return Arrange(bytes[0], bytes[1], bytes[2], bytes[3], format);
        }

        /// <summary>
        /// 把 ABCD 顺序的 4 个字节按指定格式放回两个寄存器，与 <see cref="ToBytes(short, short, DataFormat)"/> 互逆。
        /// </summary>
        private static short[] Arrange(byte a, byte b, byte c, byte d, DataFormat format)
        {
            byte w0High, w0Low, w1High, w1Low;
            switch (format)
            {
                case DataFormat.BADC:
                    w0High = b; w0Low = a; w1High = d; w1Low = c;
                    break;
                case DataFormat.CDAB:
                    w0High = c; w0Low = d; w1High = a; w1Low = b;
                    break;
                case DataFormat.DCBA:
                    w0High = d; w0Low = c; w1High = b; w1Low = a;
                    break;
                default:
                    w0High = a; w0Low = b; w1High = c; w1Low = d;
                    break;
            }

            short first = unchecked((short)((w0High << 8) | w0Low));
            short second = unchecked((short)((w1High << 8) | w1Low));
            return new short[] { first, second };
        }

        /// <summary>
        /// 给界面/日志用的中文说明。
        /// </summary>
        public static string Describe(DataFormat format)
        {
            switch (format)
            {
                case DataFormat.BADC: return "BADC（字内字节交换，字序不变）";
                case DataFormat.CDAB: return "CDAB（低字在前）";
                case DataFormat.DCBA: return "DCBA（全倒序）";
                default: return "ABCD（高字在前）";
            }
        }
    }
}
