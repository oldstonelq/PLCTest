using PlcTest.Models;
using PlcTest.Tool;
using System;

namespace PlcTest.Client.ClientInterface
{
    /// <summary>
    /// 支持配置 32 位数据排列方式的协议（汇川 Modbus、欧姆龙 FINS）。
    /// 三菱 MC 的 32 位数据固定为低字在前，不实现该接口。
    /// </summary>
    public interface IDataFormatConfigurable
    {
        /// <summary>
        /// 32 位数据（占两个 16 位寄存器）的字节序与字序。
        /// 默认值由各协议按厂商惯例给出：汇川/欧姆龙为 <see cref="DataFormat.CDAB"/>（低字在前）。
        /// </summary>
        DataFormat DataFormat { get; set; }
    }

    /// <summary>
    /// 让调用方不必自己把 <see cref="IDataFormatConfigurable.DataFormat"/> 传给转换器：
    /// <c>protocol.ToInt32(words, 0)</c> 即可按协议当前配置解码。
    /// </summary>
    public static class DataFormatConfigurableExtensions
    {
        /// <summary>
        /// 按协议当前配置把两个寄存器解释成 32 位有符号整数。
        /// </summary>
        public static int ToInt32(this IDataFormatConfigurable source, short[] words, int index)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return DataFormatConverter.ToInt32(words, index, source.DataFormat);
        }

        /// <summary>
        /// 按协议当前配置把两个寄存器解释成 32 位无符号整数。
        /// </summary>
        public static uint ToUInt32(this IDataFormatConfigurable source, short[] words, int index)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return DataFormatConverter.ToUInt32(words, index, source.DataFormat);
        }

        /// <summary>
        /// 按协议当前配置把两个寄存器解释成单精度浮点数。
        /// </summary>
        public static float ToFloat(this IDataFormatConfigurable source, short[] words, int index)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return DataFormatConverter.ToFloat(words, index, source.DataFormat);
        }

        /// <summary>
        /// 按协议当前配置把 32 位有符号整数拆成可写入的两个寄存器。
        /// </summary>
        public static short[] FromInt32(this IDataFormatConfigurable source, int value)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return DataFormatConverter.FromInt32(value, source.DataFormat);
        }

        /// <summary>
        /// 按协议当前配置把单精度浮点数拆成可写入的两个寄存器。
        /// </summary>
        public static short[] FromFloat(this IDataFormatConfigurable source, float value)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            return DataFormatConverter.FromFloat(value, source.DataFormat);
        }
    }
}
