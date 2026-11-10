using PlcTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlcTest.Protocols
{
    /// <summary>
    /// 地址编址方式。欧姆龙的两类区在这一点上完全不同。
    /// </summary>
    public enum OmronFinsAddressing
    {
        /// <summary>
        /// 字编号 + 位号：位地址 = 字编号 × 16 + 位号（0~15）。
        /// D / CIO / W / HR / AR / EM 都是这种。
        /// </summary>
        WordAndBit,
        /// <summary>
        /// 整个编号即地址：定时器 / 计数器没有"第几个字、第几位"的概念，
        /// 编号本身就是 FINS 的 3 字节地址，位字段恒 00。
        /// </summary>
        NumberOnly,
    }

    /// <summary>
    /// 欧姆龙 FINS 单个软元件的区码与编号范围。
    /// 数据来源：OMRON W342 手册「5-2-2 I/O 存储器地址的指定」（CS/CJ/CP 系列）。
    /// </summary>
    public sealed class OmronFinsDeviceSpec
    {
        /// <summary>软元件区域</summary>
        public MemoryArea Area { get; private set; }
        /// <summary>中文名称</summary>
        public string Name { get; private set; }
        /// <summary>位访问区码；0 表示不支持按位</summary>
        public byte BitCode { get; private set; }
        /// <summary>字访问区码；0 表示不支持按字</summary>
        public byte WordCode { get; private set; }
        /// <summary>字编号上限；-1 表示不支持按字</summary>
        public int MaxWordIndex { get; private set; }
        /// <summary>位编号上限；-1 表示不支持按位</summary>
        public int MaxBitIndex { get; private set; }
        /// <summary>编址方式</summary>
        public OmronFinsAddressing Addressing { get; private set; }

        internal OmronFinsDeviceSpec(MemoryArea area, string name, byte bitCode, byte wordCode,
            int maxWordIndex, int maxBitIndex, OmronFinsAddressing addressing)
        {
            Area = area;
            Name = name;
            BitCode = bitCode;
            WordCode = wordCode;
            MaxWordIndex = maxWordIndex;
            MaxBitIndex = maxBitIndex;
            Addressing = addressing;
        }

        /// <summary>是否支持按位访问</summary>
        public bool SupportsBit
        {
            get { return BitCode != 0; }
        }

        /// <summary>是否支持按字访问</summary>
        public bool SupportsWord
        {
            get { return WordCode != 0; }
        }

        /// <summary>
        /// 把统一地址模型里的 Address 换算成 FINS 的 3 字节地址。
        /// </summary>
        /// <param name="address">统一地址</param>
        /// <param name="isBit">是否按位</param>
        /// <param name="wordAddress">输出：字地址部分（2 字节）</param>
        /// <param name="bitIndex">输出：位号（0~15），字访问时为 0</param>
        public void ResolveAddress(int address, bool isBit, out int wordAddress, out int bitIndex)
        {
            if (Addressing == OmronFinsAddressing.NumberOnly)
            {
                // 定时器 / 计数器：编号即地址，位字段恒 0
                wordAddress = address;
                bitIndex = 0;
                return;
            }

            if (isBit)
            {
                wordAddress = address / 16;
                bitIndex = address % 16;
            }
            else
            {
                wordAddress = address;
                bitIndex = 0;
            }
        }
    }

    /// <summary>
    /// 区码反查结果：服务端把收到的区码翻译回统一区域。
    /// </summary>
    public sealed class OmronFinsAreaResolution
    {
        /// <summary>软元件</summary>
        public OmronFinsDeviceSpec Spec { get; private set; }
        /// <summary>是否按位访问</summary>
        public bool IsBit { get; private set; }
        /// <summary>EM 存储体号；非 EM 为 -1</summary>
        public int Bank { get; private set; }
        /// <summary>地址偏移（CNT 为 0x8000）</summary>
        public int AddressOffset { get; private set; }

        internal OmronFinsAreaResolution(OmronFinsDeviceSpec spec, bool isBit, int bank, int addressOffset)
        {
            Spec = spec;
            IsBit = isBit;
            Bank = bank;
            AddressOffset = addressOffset;
        }
    }

    /// <summary>
    /// 欧姆龙 FINS 软元件表——区码、编号范围、编址方式的唯一来源。
    /// 依据 OMRON W342（FINS 通信指令手册）5-2-2 节的 CS/CJ/CP 系列区码表。
    /// </summary>
    public static class OmronFinsDeviceCatalog
    {
        /// <summary>EM 区支持的 bank 上限（含）</summary>
        public const int MaxEmBank = 0x18;

        private static readonly Dictionary<MemoryArea, OmronFinsDeviceSpec> _items = CreateItems();

        private static Dictionary<MemoryArea, OmronFinsDeviceSpec> CreateItems()
        {
            var list = new List<OmronFinsDeviceSpec>
            {
                //                                    名称           位码   字码  字数上限  位数上限           编址方式
                // DM 区：字 D0~D32767，位 D0.00~D32767.15
                new OmronFinsDeviceSpec(MemoryArea.D,   "DM 数据存储区", 0x02, 0x82, 32767, 32767 * 16 + 15, OmronFinsAddressing.WordAndBit),
                // CIO 区：字 0~6143（手册标 CIO 0000~6143）
                new OmronFinsDeviceSpec(MemoryArea.CIO, "CIO 内继电器区", 0x30, 0xB0, 6143,  6143 * 16 + 15,  OmronFinsAddressing.WordAndBit),
                // WR 区：字 0~511
                new OmronFinsDeviceSpec(MemoryArea.W,   "WR 工作区",      0x31, 0xB1, 511,   511 * 16 + 15,   OmronFinsAddressing.WordAndBit),
                // HR 区：字 0~511
                new OmronFinsDeviceSpec(MemoryArea.HR,  "HR 保持继电器",  0x32, 0xB2, 511,   511 * 16 + 15,   OmronFinsAddressing.WordAndBit),
                // AR 区：字 0~959（A000~A447 只读、A448~A959 读写）
                new OmronFinsDeviceSpec(MemoryArea.AR,  "AR 辅助继电器",  0x33, 0xB3, 959,   959 * 16 + 15,   OmronFinsAddressing.WordAndBit),
                // EM 区：bank 由 PlcAddress.DbNumber 指定，区码按 bank 动态计算，见 GetEmBitCode / GetEmWordCode
                new OmronFinsDeviceSpec(MemoryArea.EM,  "EM 扩展存储区",  0,    0,    32767, 32767 * 16 + 15, OmronFinsAddressing.WordAndBit),
                // 定时器：完成标志（位，区码 09）与当前值（字，区码 89），编号 T0~T4095
                new OmronFinsDeviceSpec(MemoryArea.TC,  "TIM 完成标志",   0x09, 0,     -1,    4095,            OmronFinsAddressing.NumberOnly),
                new OmronFinsDeviceSpec(MemoryArea.TN,  "TIM 当前值",     0,    0x89,  4095,  -1,              OmronFinsAddressing.NumberOnly),
                // 计数器：区码与定时器**共用**，靠地址偏移 0x8000 区分
                new OmronFinsDeviceSpec(MemoryArea.CC,  "CNT 完成标志",   0x09, 0,     -1,    4095,            OmronFinsAddressing.NumberOnly),
                new OmronFinsDeviceSpec(MemoryArea.CN,  "CNT 当前值",     0,    0x89,  4095,  -1,              OmronFinsAddressing.NumberOnly),
            };

            return list.ToDictionary(spec => spec.Area);
        }

        /// <summary>
        /// 计数器区的地址偏移。TIM 与 CNT 共用区码 09 / 89，
        /// CNT 的地址从 0x8000 起，不加偏移会读到定时器。
        /// </summary>
        public static int GetAddressOffset(MemoryArea area)
        {
            return (area == MemoryArea.CC || area == MemoryArea.CN) ? 0x8000 : 0;
        }

        /// <summary>
        /// EM 区按 bank 计算位区码。
        /// bank 0~F：0x20~0x2F；bank 10~18：0xE0~0xE8。
        /// </summary>
        public static byte GetEmBitCode(int bank)
        {
            if (bank < 0 || bank > MaxEmBank)
            {
                throw new ArgumentOutOfRangeException(nameof(bank), bank, "EM 存储体号必须在 0~0x18 之间");
            }
            return bank < 0x10 ? (byte)(0x20 + bank) : (byte)(0xE0 + bank - 0x10);
        }

        /// <summary>
        /// EM 区按 bank 计算字区码。
        /// bank 0~F：0xA0~0xAF；bank 10~18：0x60~0x68。
        /// </summary>
        public static byte GetEmWordCode(int bank)
        {
            if (bank < 0 || bank > MaxEmBank)
            {
                throw new ArgumentOutOfRangeException(nameof(bank), bank, "EM 存储体号必须在 0~0x18 之间");
            }
            return bank < 0x10 ? (byte)(0xA0 + bank) : (byte)(0x60 + bank - 0x10);
        }

        /// <summary>
        /// 按区域取软元件元数据；不支持的区抛异常。
        /// </summary>
        public static OmronFinsDeviceSpec Get(MemoryArea area)
        {
            OmronFinsDeviceSpec spec;
            if (!_items.TryGetValue(area, out spec))
            {
                throw new ArgumentOutOfRangeException(nameof(area), area, "欧姆龙 FINS 不支持该软元件");
            }
            return spec;
        }

        /// <summary>该区域是否为欧姆龙支持的软元件</summary>
        public static bool Contains(MemoryArea area)
        {
            return _items.ContainsKey(area);
        }

        /// <summary>
        /// 区码反查区域——服务端用（客户端是「区域 → 区码」，这里正好相反）。
        /// 需要地址参与判断的两个特例：
        /// ① TIM 与 CNT 共用区码 09 / 89，靠 CNT 的 0x8000 偏移区分；
        /// ② EM 的区码随 bank 变化，要从码值反推 bank。
        /// </summary>
        /// <param name="areaCode">区码</param>
        /// <param name="wordAddress">请求里的字地址（含偏移），用于区分 TIM/CNT</param>
        /// <returns>解析结果；区码不认识时返回 null</returns>
        public static OmronFinsAreaResolution Resolve(byte areaCode, int wordAddress)
        {
            // ① EM 区：区码随 bank 变化，先按区段反推
            int emBank = -1;
            bool emBit = false;
            if (areaCode >= 0x20 && areaCode <= 0x2F)
            {
                emBank = areaCode - 0x20; emBit = true;
            }
            else if (areaCode >= 0xE0 && areaCode <= 0xE8)
            {
                emBank = areaCode - 0xE0 + 0x10; emBit = true;
            }
            else if (areaCode >= 0xA0 && areaCode <= 0xAF)
            {
                emBank = areaCode - 0xA0; emBit = false;
            }
            else if (areaCode >= 0x60 && areaCode <= 0x68)
            {
                emBank = areaCode - 0x60 + 0x10; emBit = false;
            }

            if (emBank >= 0)
            {
                return new OmronFinsAreaResolution(Get(MemoryArea.EM), emBit, emBank, 0);
            }

            // ② 定时器 / 计数器：同一区码，靠地址偏移区分
            if (areaCode == 0x09)
            {
                bool isCounter = wordAddress >= 0x8000;
                return new OmronFinsAreaResolution(
                    Get(isCounter ? MemoryArea.CC : MemoryArea.TC), true, -1, isCounter ? 0x8000 : 0);
            }
            if (areaCode == 0x89)
            {
                bool isCounter = wordAddress >= 0x8000;
                return new OmronFinsAreaResolution(
                    Get(isCounter ? MemoryArea.CN : MemoryArea.TN), false, -1, isCounter ? 0x8000 : 0);
            }

            // ③ 其余靠精确匹配
            foreach (OmronFinsDeviceSpec spec in _items.Values)
            {
                if (spec.BitCode == areaCode && spec.SupportsBit)
                {
                    return new OmronFinsAreaResolution(spec, true, -1, 0);
                }
                if (spec.WordCode == areaCode && spec.SupportsWord)
                {
                    return new OmronFinsAreaResolution(spec, false, -1, 0);
                }
            }

            return null;
        }

        /// <summary>全部软元件</summary>
        public static IEnumerable<OmronFinsDeviceSpec> All
        {
            get { return _items.Values; }
        }
    }
}
