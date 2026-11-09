using PlcTest.Models;
using System.Collections.Generic;

namespace PlcTest.Protocols
{
    /// <summary>
    /// 协议私有选项的登记表：哪个协议有哪些可配项、默认值多少、取值范围多大。
    /// <para>
    /// 这里只放「描述」，不碰任何协议实例；真正把值应用到协议上的事由
    /// <c>IProtocolOptionHost</c> 的各个实现完成。界面因此不需要认识任何厂商类型，
    /// 新增一个参数的流程是：本表加一行 + 对应协议类的 ApplyOptions 加一行。
    /// </para>
    /// <para>
    /// 客户端与服务端分开登记：同一个协议枚举在两端可配的东西不一样
    /// （例如汇川服务端只有机型影响软元件表，没有字节序可配）。
    /// </para>
    /// </summary>
    public static class ProtocolOptionCatalog
    {
        private static readonly ProtocolOption[] None = new ProtocolOption[0];

        /// <summary>
        /// 客户端协议的可配项，按界面显示顺序返回；没有可配项时返回空表（界面据此隐藏选项区）。
        /// </summary>
        public static IReadOnlyList<ProtocolOption> ForClient(ProtocolType protocol)
        {
            switch (protocol)
            {
                case ProtocolType.InovanceModbusTcp:
                case ProtocolType.InovanceModbusRtu:
                    return new[]
                    {
                        ProtocolOption.Choice("CpuModel", "机型", InovanceCpuModel.H3U,
                            "影响可用软元件与编号上限"),
                        ProtocolOption.Choice("DataFormat", "字节序", DataFormat.CDAB,
                            "32 位数据（两个寄存器）的排列"),
                    };

                case ProtocolType.OmronFinsTcp:
                    return new[]
                    {
                        ProtocolOption.Choice("DataFormat", "字节序", DataFormat.CDAB,
                            "DMOV 写出的 32 位数据为低字在前"),
                        ProtocolOption.Int("TargetNetwork", "目标网络号", 0, 0, 255,
                            "跨网段路由时填写，同网段为 0"),
                        ProtocolOption.Int("TargetUnit", "目标单元号", 0, 0, 255,
                            "CPU 单元固定 0"),
                    };

                case ProtocolType.SiemensS71200:
                    return new[]
                    {
                        ProtocolOption.Int("Rack", "机架号", 0, 0, 7,
                            "写进 COTP 目标 TSAP"),
                        ProtocolOption.Int("Slot", "槽号", 1, 0, 31,
                            "S7-1200 通常为 1；连不上时优先换这个"),
                        ProtocolOption.Int("PduLength", "申请 PDU", 960, 240, 960,
                            "协商失败时可下调到 480"),
                    };

                case ProtocolType.MelsecMc3EBinary:
                case ProtocolType.MelsecMc3EAscii:
                case ProtocolType.MelsecMc4EBinary:
                case ProtocolType.MelsecMc4EAscii:
                    // 3E/4E 帧头有网络号与站号；1E 帧没有这两个字段，所以不提供
                    return new[]
                    {
                        ProtocolOption.Int("NetworkNumber", "网络号", 0, 0, 255,
                            "直连本机 CPU 为 0；经 MELSECNET 路由才改"),
                        ProtocolOption.Int("StationNumber", "站号", 0, 0, 255,
                            "直连本机 CPU 为 0"),
                    };

                default:
                    // 三菱 1E 帧没有网络号/站号字段，32 位数据也固定低字在前，所以没有可配项
                    return None;
            }
        }

        /// <summary>
        /// 服务端协议的可配项。
        /// </summary>
        public static IReadOnlyList<ProtocolOption> ForServer(ProtocolType protocol)
        {
            switch (protocol)
            {
                case ProtocolType.InovanceModbusTcp:
                case ProtocolType.InovanceModbusRtu:
                    return new[]
                    {
                        ProtocolOption.Choice("CpuModel", "机型", InovanceCpuModel.H3U,
                            "决定服务端声明哪些软元件"),
                    };


                default:
                    return None;
            }
        }

        /// <summary>
        /// 按描述生成一份带默认值的选项集，供界面初始化和测试使用。
        /// </summary>
        public static ProtocolOptions CreateDefaults(IReadOnlyList<ProtocolOption> schema)
        {
            var options = new ProtocolOptions();
            if (schema == null)
            {
                return options;
            }

            foreach (ProtocolOption option in schema)
            {
                options.Set(option.Key, option.DefaultValue);
            }
            return options;
        }
    }
}
