using PlcTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlcTest.Client.ClientProtocolManagement.Inovance
{
    /// <summary>
    /// 汇川 PLC 机型。不同机型的 Modbus 基地址相同，差别只在编号范围与"有没有该软元件区"。
    /// </summary>
    public enum InovanceCpuModel
    {
        /// <summary>H3U / H3U-XP</summary>
        H3U,
        /// <summary>Easy 系列</summary>
        Easy,
    }

    /// <summary>
    /// Modbus 地址空间。两个空间独立编址，可以出现相同的数字（如 M0 与 D0 都在 0x0000），
    /// 靠功能码区分：线圈用 01/05，保持寄存器用 03/06/16。
    /// </summary>
    public enum InovanceAddressSpace
    {
        /// <summary>线圈区（位），功能码 01 / 05</summary>
        Coil,
        /// <summary>保持寄存器区（字），功能码 03 / 06 / 16</summary>
        HoldingRegister,
    }

    /// <summary>
    /// 一段连续的地址区间。
    /// 同一个软元件内部可能分成多段，而且每点的寄存器宽度可以不同——
    /// 典型例子是汇川的 C200–C255：32 位计数器，每个占两个 16 位寄存器，
    /// 起始地址另起一段（0xF700），与 C0–C199（0xF400，16 位）不连续。
    /// </summary>
    public sealed class InovanceRegisterSegment
    {
        /// <summary>该段的起始编号</summary>
        public int StartIndex { get; private set; }
        /// <summary>该段的结束编号（含）</summary>
        public int EndIndex { get; private set; }
        /// <summary>该段的 Modbus 起始地址</summary>
        public int BaseAddress { get; private set; }
        /// <summary>每个点占几个 16 位寄存器：16 位软元件为 1，32 位为 2</summary>
        public int RegistersPerPoint { get; private set; }

        internal InovanceRegisterSegment(int startIndex, int endIndex, int baseAddress, int registersPerPoint)
        {
            if (startIndex < 0 || endIndex < startIndex)
                throw new ArgumentOutOfRangeException(nameof(startIndex), startIndex, "地址段区间非法");
            if (registersPerPoint < 1 || registersPerPoint > 2)
                throw new ArgumentOutOfRangeException(nameof(registersPerPoint), registersPerPoint, "每点只能是 1 或 2 个寄存器");

            StartIndex = startIndex;
            EndIndex = endIndex;
            BaseAddress = baseAddress;
            RegistersPerPoint = registersPerPoint;
        }

        /// <summary>编号是否落在本段内</summary>
        public bool Contains(int index)
        {
            return index >= StartIndex && index <= EndIndex;
        }

        /// <summary>是否为 32 位（每点两个寄存器）</summary>
        public bool Is32Bit
        {
            get { return RegistersPerPoint == 2; }
        }

        /// <summary>编号换算成该段内的 Modbus 地址</summary>
        public int ToAddress(int index)
        {
            return BaseAddress + (index - StartIndex) * RegistersPerPoint;
        }
    }

    /// <summary>
    /// 汇川单个软元件的 Modbus 元数据。
    /// </summary>
    public sealed class InovanceDeviceSpec
    {
        private readonly List<InovanceRegisterSegment> _segments;

        /// <summary>软元件区域</summary>
        public MemoryArea Area { get; private set; }
        /// <summary>中文名称</summary>
        public string Name { get; private set; }
        /// <summary>所属 Modbus 地址空间</summary>
        public InovanceAddressSpace Space { get; private set; }
        /// <summary>该区域首段的 Modbus 起始地址</summary>
        public int BaseAddress { get; private set; }
        /// <summary>允许的访问方式（线圈区只能位，寄存器区只能字）</summary>
        public AreaAccess Access { get; private set; }
        /// <summary>H3U 下的最大编号；-1 表示该机型没有这个软元件</summary>
        public int MaxIndexH3U { get; private set; }
        /// <summary>Easy 下的最大编号；-1 表示该机型没有这个软元件</summary>
        public int MaxIndexEasy { get; private set; }

        internal InovanceDeviceSpec(MemoryArea area, string name, InovanceAddressSpace space, int baseAddress,
            int maxIndexH3U, int maxIndexEasy)
        {
            Area = area;
            Name = name;
            Space = space;
            BaseAddress = baseAddress;
            Access = space == InovanceAddressSpace.Coil ? AreaAccess.Bit : AreaAccess.Word;
            MaxIndexH3U = maxIndexH3U;
            MaxIndexEasy = maxIndexEasy;

            // 默认整段一个区间，每点 1 个寄存器；有多段的软元件（如 CN）在表里再覆盖
            int maxIndex = Math.Max(maxIndexH3U, maxIndexEasy);
            _segments = new List<InovanceRegisterSegment>
            {
                new InovanceRegisterSegment(0, maxIndex < 0 ? 0 : maxIndex, baseAddress, 1)
            };
        }

        /// <summary>该软元件的全部地址段</summary>
        public IReadOnlyList<InovanceRegisterSegment> Segments
        {
            get { return _segments; }
        }

        /// <summary>
        /// 覆盖地址段划分（仅建表时使用）。
        /// </summary>
        internal void SetSegments(params InovanceRegisterSegment[] segments)
        {
            _segments.Clear();
            _segments.AddRange(segments);
        }

        /// <summary>
        /// 按机型取最大编号；-1 表示该机型没有这个软元件。
        /// </summary>
        public int GetMaxIndex(InovanceCpuModel model)
        {
            return model == InovanceCpuModel.Easy ? MaxIndexEasy : MaxIndexH3U;
        }

        /// <summary>
        /// 该机型是否存在这个软元件。
        /// </summary>
        public bool IsSupported(InovanceCpuModel model)
        {
            return GetMaxIndex(model) >= 0;
        }

        /// <summary>
        /// 找编号所在的地址段；不在任何段内返回 null。
        /// </summary>
        public InovanceRegisterSegment FindSegment(int index)
        {
            for (int i = 0; i < _segments.Count; i++)
            {
                if (_segments[i].Contains(index))
                {
                    return _segments[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 编号换算成真实 Modbus 起始地址。
        /// </summary>
        public int ResolveStartAddress(int index)
        {
            InovanceRegisterSegment segment = FindSegment(index);
            if (segment == null)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"{Name}({Area}) 编号 {index} 不在任何地址段内");
            }
            return segment.ToAddress(index);
        }

        /// <summary>
        /// 从 index 起连续 points 个点，占多少个 16 位寄存器。
        /// 16 位软元件等于点数，32 位软元件等于点数 × 2。
        /// </summary>
        public int ResolveRegisterCount(int index, int points)
        {
            InovanceRegisterSegment segment = FindSegment(index);
            if (segment == null)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, $"{Name}({Area}) 编号 {index} 不在任何地址段内");
            }
            return points * segment.RegistersPerPoint;
        }

        /// <summary>
        /// 本次访问是否落在同一段内。跨段读写（例如 C195 起 10 个点跨过 C199/C200 边界）
        /// 的地址与宽度都不连续，必须拒绝，否则会读到错误的寄存器。
        /// </summary>
        public bool CanAccess(int index, int points)
        {
            InovanceRegisterSegment segment = FindSegment(index);
            if (segment == null)
            {
                return false;
            }
            int last = index + (points > 1 ? points : 1) - 1;
            return last <= segment.EndIndex;
        }
    }

    /// <summary>
    /// Modbus 单帧的数量上限。超出后 PLC 直接回异常码，读不到任何数据，
    /// 所以在拼包前先拦住并给出可读原因。
    /// 数据来源：H3U 手册 9.4.2「Modbus 功能码及数据编址」。
    /// </summary>
    public static class InovanceModbusLimits
    {
        /// <summary>读线圈 / 离散量（01 / 02）一次最多 2000 点</summary>
        public const int MaxReadCoils = 2000;
        /// <summary>读保持寄存器 / 输入寄存器（03 / 04）一次最多 125 个</summary>
        public const int MaxReadRegisters = 125;
        /// <summary>写多个线圈（0F）一次最多 1968 点</summary>
        public const int MaxWriteCoils = 1968;
        /// <summary>写多个寄存器（10）一次最多 123 个</summary>
        public const int MaxWriteRegisters = 123;
    }

    /// <summary>
    /// 汇川 Modbus 软元件表——起始地址、地址空间、位/字能力、地址分段、各机型编号范围的唯一来源。
    /// 数据来源：<c>Document/Inovance/Easy寄存器地址.png</c> 与 <c>H3U寄存器地址.png</c>（第 9 章 Modbus 协议）。
    /// 两机型的 S / M / X / Y / D / R 基地址完全一致，差异只在编号范围与机型独有区（Easy 有 B，H3U 有 SM/SD/T/C）。
    /// </summary>
    public static class InovanceDeviceCatalog
    {
        private static readonly Dictionary<MemoryArea, InovanceDeviceSpec> _items = CreateItems();

        private static Dictionary<MemoryArea, InovanceDeviceSpec> CreateItems()
        {
            var list = new List<InovanceDeviceSpec>
            {
                // 线圈区（位）—— 功能码 01 / 05
                //                                            名称         空间  基地址  H3U    Easy
                new InovanceDeviceSpec(MemoryArea.M,  "辅助继电器",   InovanceAddressSpace.Coil, 0x0000, 8511,  7999),
                new InovanceDeviceSpec(MemoryArea.SM, "特殊继电器",   InovanceAddressSpace.Coil, 0x2400, 1023,  -1),
                new InovanceDeviceSpec(MemoryArea.B,  "位元件",       InovanceAddressSpace.Coil, 0x3000, -1,    32767),
                new InovanceDeviceSpec(MemoryArea.S,  "状态继电器",   InovanceAddressSpace.Coil, 0xE000, 4095,  4095),
                new InovanceDeviceSpec(MemoryArea.TS, "定时器触点",   InovanceAddressSpace.Coil, 0xF000, 511,   -1),
                new InovanceDeviceSpec(MemoryArea.CS, "计数器触点",   InovanceAddressSpace.Coil, 0xF400, 255,   -1),
                new InovanceDeviceSpec(MemoryArea.X,  "输入继电器",   InovanceAddressSpace.Coil, 0xF800, 255,   1023),
                new InovanceDeviceSpec(MemoryArea.Y,  "输出继电器",   InovanceAddressSpace.Coil, 0xFC00, 255,   1023),
                // 保持寄存器区（字）—— 功能码 03 / 06 / 16
                new InovanceDeviceSpec(MemoryArea.D,  "数据寄存器",   InovanceAddressSpace.HoldingRegister, 0x0000, 8511,  7999),
                new InovanceDeviceSpec(MemoryArea.SD, "特殊寄存器",   InovanceAddressSpace.HoldingRegister, 0x2400, 1023,  -1),
                new InovanceDeviceSpec(MemoryArea.R,  "扩展寄存器",   InovanceAddressSpace.HoldingRegister, 0x3000, 32767, 32767),
                new InovanceDeviceSpec(MemoryArea.TN, "定时器当前值", InovanceAddressSpace.HoldingRegister, 0xF000, 255,   -1),
                new InovanceDeviceSpec(MemoryArea.CN, "计数器当前值", InovanceAddressSpace.HoldingRegister, 0xF400, 255,   -1),
            };

            // 计数器当前值分两段（见 H3U 寄存器地址图「寄存器编址」）：
            //   C0–C199   16 位，0xF400 起，每点 1 个寄存器；
            //   C200–C255 32 位，0xF700 起，每点 2 个寄存器（低 16 位在前），不支持 0x06 单写。
            InovanceDeviceSpec counter = list.First(spec => spec.Area == MemoryArea.CN);
            counter.SetSegments(
                new InovanceRegisterSegment(0, 199, 0xF400, 1),
                new InovanceRegisterSegment(200, 255, 0xF700, 2));

            return list.ToDictionary(spec => spec.Area);
        }

        /// <summary>
        /// 按区域与机型取软元件元数据；该机型没有此软元件时抛异常。
        /// </summary>
        public static InovanceDeviceSpec Get(MemoryArea area, InovanceCpuModel model)
        {
            InovanceDeviceSpec spec;
            if (!_items.TryGetValue(area, out spec))
                throw new ArgumentOutOfRangeException(nameof(area), area, "汇川 Modbus 不支持该软元件");

            if (!spec.IsSupported(model))
            {
                string modelName = model == InovanceCpuModel.Easy ? "Easy" : "H3U";
                throw new ArgumentOutOfRangeException(nameof(area), area, $"{modelName} 机型没有 {spec.Name}({area})");
            }

            return spec;
        }

        /// <summary>
        /// 判断该机型是否存在此软元件。
        /// </summary>
        public static bool Contains(MemoryArea area, InovanceCpuModel model)
        {
            InovanceDeviceSpec spec;
            return _items.TryGetValue(area, out spec) && spec.IsSupported(model);
        }

        /// <summary>
        /// 全部软元件（两种机型合并，含各自独有区）。
        /// </summary>
        public static IEnumerable<InovanceDeviceSpec> All
        {
            get { return _items.Values; }
        }
    }
}
