using PlcTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlcTest.Client.ClientProtocolManagement.Melsec
{
    /// <summary>
    /// A 兼容 1E 帧适用的 CPU 机型。机型只影响软元件编号上限，不影响软元件代码。
    /// </summary>
    public enum Melsec1ECpuModel
    {
        /// <summary>FX3G / FX3GC</summary>
        Fx3G,
        /// <summary>FX3U / FX3UC</summary>
        Fx3U,
    }

    /// <summary>
    /// 1E 帧单个软元件的元数据。
    /// 数据来源：PlcTest/Document/Melsec/1E寄存器地址.png（A 兼容 1E 帧 / FX 系列）。
    /// 注意与 3E 帧的差别：1E 的单字符代码用空格(0x20)补齐而不是 '*'，
    /// 编号统一按十六进制，只支持下面这 12 种软元件。
    /// </summary>
    public sealed class Melsec1EDeviceSpec
    {
        /// <summary>软元件区域</summary>
        public MemoryArea Area { get; private set; }
        /// <summary>中文名称</summary>
        public string Name { get; private set; }
        /// <summary>2 字节软元件代码（如 D 为 0x4420，即 ASCII "D "）</summary>
        public int Code { get; private set; }
        /// <summary>代码的可读形式，便于对照手册</summary>
        public string CodeText { get; private set; }
        /// <summary>允许的访问方式</summary>
        public AreaAccess Access { get; private set; }
        /// <summary>FX3G / FX3GC 下的最大编号</summary>
        public int MaxIndexFx3G { get; private set; }
        /// <summary>FX3U / FX3UC 下的最大编号</summary>
        public int MaxIndexFx3U { get; private set; }

        internal Melsec1EDeviceSpec(MemoryArea area, string name, string codeText, int code,
            AreaAccess access, int maxIndexFx3G, int maxIndexFx3U)
        {
            Area = area;
            Name = name;
            CodeText = codeText;
            Code = code;
            Access = access;
            MaxIndexFx3G = maxIndexFx3G;
            MaxIndexFx3U = maxIndexFx3U;
        }

        /// <summary>
        /// 按机型取最大编号。
        /// </summary>
        public int GetMaxIndex(Melsec1ECpuModel model)
        {
            return model == Melsec1ECpuModel.Fx3U ? MaxIndexFx3U : MaxIndexFx3G;
        }
    }

    /// <summary>
    /// 三菱 A 兼容 1E 帧软元件表——代码、位/字能力、各机型编号上限的唯一来源。
    /// </summary>
    public static class Melsec1EDeviceCatalog
    {
        private static readonly Dictionary<MemoryArea, Melsec1EDeviceSpec> _items =
            new List<Melsec1EDeviceSpec>
            {
                //                                                名称         代码   码值    访问    FX3G   FX3U
                new Melsec1EDeviceSpec(MemoryArea.D,  "数据寄存器",     "D ",  0x4420, AreaAccess.Word, 0x1F3F, 0x1F3F),
                new Melsec1EDeviceSpec(MemoryArea.R,  "扩展寄存器",     "R ",  0x5220, AreaAccess.Word, 0x5DBF, 0x7FFF),
                new Melsec1EDeviceSpec(MemoryArea.TN, "定时器当前值",   "TN",  0x544E, AreaAccess.Word, 0x013F, 0x01FF),
                new Melsec1EDeviceSpec(MemoryArea.TS, "定时器触点",     "TS",  0x5453, AreaAccess.Bit,  0x013F, 0x01FF),
                new Melsec1EDeviceSpec(MemoryArea.TC, "定时器线圈",     "TC",  0x5443, AreaAccess.Bit,  0x013F, 0x01FF),
                new Melsec1EDeviceSpec(MemoryArea.CN, "计数器当前值",   "CN",  0x434E, AreaAccess.Word, 0x00C7, 0x00FF),
                new Melsec1EDeviceSpec(MemoryArea.CS, "计数器触点",     "CS",  0x4353, AreaAccess.Bit,  0x00C7, 0x00FF),
                new Melsec1EDeviceSpec(MemoryArea.CC, "计数器线圈",     "CC",  0x4343, AreaAccess.Bit,  0x00C7, 0x00FF),
                new Melsec1EDeviceSpec(MemoryArea.X,  "输入",           "X ",  0x5820, AreaAccess.Both, 0x007F, 0x00FF),
                new Melsec1EDeviceSpec(MemoryArea.Y,  "输出",           "Y ",  0x5920, AreaAccess.Both, 0x007F, 0x00FF),
                new Melsec1EDeviceSpec(MemoryArea.M,  "辅助继电器",     "M ",  0x4D20, AreaAccess.Both, 0x1DFF, 0x1DFF),
                new Melsec1EDeviceSpec(MemoryArea.S,  "状态",           "S ",  0x5320, AreaAccess.Both, 0x0FFF, 0x0FFF),
            }.ToDictionary(spec => spec.Area);

        /// <summary>
        /// 按区域取软元件元数据；1E 不支持的软元件抛异常。
        /// </summary>
        public static Melsec1EDeviceSpec Get(MemoryArea area)
        {
            Melsec1EDeviceSpec spec;
            if (!_items.TryGetValue(area, out spec))
                throw new ArgumentOutOfRangeException(nameof(area), area, "1E 帧不支持该软元件");
            return spec;
        }

        /// <summary>
        /// 判断区域是否在 1E 帧支持范围内。
        /// </summary>
        public static bool Contains(MemoryArea area)
        {
            return _items.ContainsKey(area);
        }

        /// <summary>
        /// 全部软元件（1E 共 12 种）。
        /// </summary>
        public static IEnumerable<Melsec1EDeviceSpec> All
        {
            get { return _items.Values; }
        }

        /// <summary>
        /// 校验起始编号与连续访问的末点是否越界。
        /// </summary>
        /// <param name="area">软元件区域</param>
        /// <param name="index">起始编号</param>
        /// <param name="length">连续访问点数（≤1 视为单点）</param>
        /// <param name="model">CPU 机型</param>
        /// <returns>合法返回 null，否则返回错误描述</returns>
        public static string ValidateIndex(MemoryArea area, int index, int length, Melsec1ECpuModel model)
        {
            Melsec1EDeviceSpec spec;
            if (!_items.TryGetValue(area, out spec))
                return $"1E 帧不支持软元件 {area}";

            if (index < 0)
                return $"{spec.Name}({area}) 编号不能为负: {index}";

            int max = spec.GetMaxIndex(model);
            string modelName = model == Melsec1ECpuModel.Fx3U ? "FX3U/FX3UC" : "FX3G/FX3GC";

            if (index > max)
                return $"{spec.Name}({area}) 起始编号 {index} 超出 {modelName} 范围 0~{max}";

            int span = length > 1 ? length : 1;
            int last = index + span - 1;
            if (last > max)
                return $"{spec.Name}({area}) 连续访问到编号 {last}，超出 {modelName} 范围 0~{max}";

            return null;
        }
    }
}
