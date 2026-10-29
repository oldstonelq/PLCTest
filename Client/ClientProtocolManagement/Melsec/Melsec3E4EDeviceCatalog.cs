using PlcTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlcTest.Client.ClientProtocolManagement.Melsec
{
    // 三菱 MC 协议里有两套软元件体系，不要混用：
    //   · QnA 兼容 3E / 4E 帧（Q/L/iQ-R 系列）—— 见本文件 Melsec3E4EDeviceCatalog，28 种软元件，
    //     单字符代码补 '*'，编号进制按软元件类型区分；
    //   · A 兼容 1E 帧（FX 系列）—— 见 Melsec1EDeviceCatalog，仅 10 种软元件，
    //     单字符代码补空格，编号统一十六进制，上限还跟机型有关。

    /// <summary>
    /// 软元件编号的进制（3E / 4E 帧）。
    /// 同一帧内不同软元件的编号进制并不一致：
    /// 输入 X、输出 Y、链接继电器 B、链接寄存器 W、链接特殊继电器 SB、链接特殊寄存器 SW、
    /// 直接输入 DX、直接输出 DY、连号文件寄存器 ZR 用十六进制，
    /// 其余（SM/SD/M/L/F/V/D/TS/TC/TN/SS/SC/SN/CS/CC/CN/S/Z/R）用十进制。
    /// </summary>
    public enum MelsecDeviceRadix
    {
        /// <summary>十进制编号（如 D100 表示第 100 个字）</summary>
        Decimal,
        /// <summary>十六进制编号（如 X1F 表示第 31 点）</summary>
        Hexadecimal,
    }

    /// <summary>
    /// 单个软元件的元数据（3E / 4E 帧）。
    /// 数据来源：《MELSEC Q 系列 基本型 QCPU 软元件一览》与 MC 协议手册「Available devices」表。
    /// </summary>
    public sealed class Melsec3E4EDeviceSpec
    {
        /// <summary>软元件区域</summary>
        public MemoryArea Area { get; private set; }
        /// <summary>中文名称</summary>
        public string Name { get; private set; }
        /// <summary>ASCII 模式下的软元件代码（固定 2 字符，单字符软元件以 '*' 补齐）</summary>
        public string AsciiCode { get; private set; }
        /// <summary>二进制模式下的软元件代码</summary>
        public byte BinaryCode { get; private set; }
        /// <summary>该区域允许的访问方式</summary>
        public AreaAccess Access { get; private set; }
        /// <summary>编号进制</summary>
        public MelsecDeviceRadix Radix { get; private set; }
        /// <summary>默认最大编号（CPU 参数可扩展，故仅作提示与越界判断参考）</summary>
        public int DefaultMaxIndex { get; private set; }

        internal Melsec3E4EDeviceSpec(MemoryArea area, string name, string asciiCode, byte binaryCode,
            AreaAccess access, MelsecDeviceRadix radix, int defaultMaxIndex)
        {
            Area = area;
            Name = name;
            AsciiCode = asciiCode;
            BinaryCode = binaryCode;
            Access = access;
            Radix = radix;
            DefaultMaxIndex = defaultMaxIndex;
        }

        /// <summary>
        /// ASCII 软元件代码打包成 int：高字节第 1 字符，低字节第 2 字符。
        /// </summary>
        public int AsciiCodeWord
        {
            get { return (AsciiCode[0] << 8) | AsciiCode[1]; }
        }

        /// <summary>
        /// 按本软元件的进制格式化编号，左侧补零到指定字符数。
        /// </summary>
        /// <param name="index">软元件编号</param>
        /// <param name="digits">目标字符数（3E ASCII 为 6，1E ASCII 为 8）</param>
        public string FormatIndex(int index, int digits)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), index, "软元件编号不能为负");

            string text = Radix == MelsecDeviceRadix.Hexadecimal
                ? index.ToString("X")
                : index.ToString();

            if (text.Length > digits)
                throw new ArgumentOutOfRangeException(nameof(index), index,
                    $"{Name}({Area}) 编号超出 {digits} 位表示范围");

            return text.PadLeft(digits, '0');
        }
    }

    /// <summary>
    /// 三菱 QnA 兼容 3E / 4E 帧共用的软元件表——软元件代码、位/字能力、编号进制、编号范围的唯一来源。
    /// 3E 与 4E 只在帧头（副标题、请求数据长度位置）上有差别，软元件代码与编号规则完全一致，
    /// 因此两者共用这一张表；各协议实现按需查表，不再各自维护一份 switch。
    /// </summary>
    public static class Melsec3E4EDeviceCatalog
    {
        private const AreaAccess Bit = AreaAccess.Bit;
        private const AreaAccess Word = AreaAccess.Word;
        private const AreaAccess Both = AreaAccess.Both;
        private const MelsecDeviceRadix Dec = MelsecDeviceRadix.Decimal;
        private const MelsecDeviceRadix Hex = MelsecDeviceRadix.Hexadecimal;

        private static readonly Dictionary<MemoryArea, Melsec3E4EDeviceSpec> _items =
            new List<Melsec3E4EDeviceSpec>
            {
                // 内部系统软元件
                new Melsec3E4EDeviceSpec(MemoryArea.SM, "特殊继电器",       "SM", 0x91, Both, Dec, 1023),
                new Melsec3E4EDeviceSpec(MemoryArea.SD, "特殊寄存器",       "SD", 0xA9, Word, Dec, 1023),
                // 内部用户软元件 —— 位软元件
                new Melsec3E4EDeviceSpec(MemoryArea.X,  "输入",             "X*", 0x9C, Both, Hex, 0x7FF),
                new Melsec3E4EDeviceSpec(MemoryArea.Y,  "输出",             "Y*", 0x9D, Both, Hex, 0x7FF),
                new Melsec3E4EDeviceSpec(MemoryArea.M,  "内部继电器",       "M*", 0x90, Both, Dec, 8191),
                new Melsec3E4EDeviceSpec(MemoryArea.L,  "锁存继电器",       "L*", 0x92, Both, Dec, 2027),
                new Melsec3E4EDeviceSpec(MemoryArea.F,  "报警器",           "F*", 0x93, Both, Dec, 1023),
                new Melsec3E4EDeviceSpec(MemoryArea.V,  "变址继电器",       "V*", 0x94, Both, Dec, 1023),
                new Melsec3E4EDeviceSpec(MemoryArea.B,  "链接继电器",       "B*", 0xA0, Both, Hex, 0x7FF),
                // 内部用户软元件 —— 字软元件
                new Melsec3E4EDeviceSpec(MemoryArea.D,  "数据寄存器",       "D*", 0xA8, Word, Dec, 11135),
                new Melsec3E4EDeviceSpec(MemoryArea.W,  "链接寄存器",       "W*", 0xB4, Word, Hex, 0x7FF),
                // 定时器：触点/线圈只能按位访问，当前值只能按字访问
                new Melsec3E4EDeviceSpec(MemoryArea.TS, "定时器触点",       "TS", 0xC1, Bit,  Dec, 511),
                new Melsec3E4EDeviceSpec(MemoryArea.TC, "定时器线圈",       "TC", 0xC0, Bit,  Dec, 511),
                new Melsec3E4EDeviceSpec(MemoryArea.TN, "定时器当前值",     "TN", 0xC2, Word, Dec, 511),
                // 累计定时器
                new Melsec3E4EDeviceSpec(MemoryArea.SS, "累计定时器触点",   "SS", 0xC7, Bit,  Dec, 511),
                new Melsec3E4EDeviceSpec(MemoryArea.SC, "累计定时器线圈",   "SC", 0xC6, Bit,  Dec, 511),
                new Melsec3E4EDeviceSpec(MemoryArea.SN, "累计定时器当前值", "SN", 0xC8, Word, Dec, 511),
                // 计数器
                new Melsec3E4EDeviceSpec(MemoryArea.CS, "计数器触点",       "CS", 0xC4, Bit,  Dec, 511),
                new Melsec3E4EDeviceSpec(MemoryArea.CC, "计数器线圈",       "CC", 0xC3, Bit,  Dec, 511),
                new Melsec3E4EDeviceSpec(MemoryArea.CN, "计数器当前值",     "CN", 0xC5, Word, Dec, 511),
                // 链接特殊软元件
                new Melsec3E4EDeviceSpec(MemoryArea.SB, "链接特殊继电器",   "SB", 0xA1, Both, Hex, 0x3FF),
                new Melsec3E4EDeviceSpec(MemoryArea.SW, "链接特殊寄存器",   "SW", 0xB5, Word, Hex, 0x3FF),
                // 其他
                new Melsec3E4EDeviceSpec(MemoryArea.S,  "步进继电器",       "S*", 0x98, Both, Dec, 2027),
                new Melsec3E4EDeviceSpec(MemoryArea.DX, "直接输入",         "DX", 0xA2, Bit,  Hex, 0x7FF),
                new Melsec3E4EDeviceSpec(MemoryArea.DY, "直接输出",         "DY", 0xA3, Bit,  Hex, 0x7FF),
                new Melsec3E4EDeviceSpec(MemoryArea.Z,  "变址寄存器",       "Z*", 0xCC, Word, Dec, 9),
                new Melsec3E4EDeviceSpec(MemoryArea.R,  "文件寄存器",       "R*", 0xAF, Word, Dec, 32767),
                new Melsec3E4EDeviceSpec(MemoryArea.ZR, "连号文件寄存器",   "ZR", 0xB0, Word, Hex, 0xFFFF),
            }.ToDictionary(spec => spec.Area);

        /// <summary>
        /// 按区域取软元件元数据；不支持的区城抛出异常。
        /// </summary>
        public static Melsec3E4EDeviceSpec Get(MemoryArea area)
        {
            Melsec3E4EDeviceSpec spec;
            if (!_items.TryGetValue(area, out spec))
                throw new ArgumentOutOfRangeException(nameof(area), area, "不支持的软元件区域");
            return spec;
        }

        /// <summary>
        /// 判断区域是否在三菱 MC 协议支持范围内。
        /// </summary>
        public static bool Contains(MemoryArea area)
        {
            return _items.ContainsKey(area);
        }

        /// <summary>
        /// 全部软元件（按枚举声明顺序）。
        /// </summary>
        public static IEnumerable<Melsec3E4EDeviceSpec> All
        {
            get { return _items.Values; }
        }
    }
}
