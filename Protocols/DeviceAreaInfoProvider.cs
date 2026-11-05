using PlcTest.Models;
using System;
using System.Collections.Generic;

namespace PlcTest.Protocols
{
    /// <summary>
    /// 把「协议 + 软元件区域」翻译成界面要显示的描述与位/字能力。
    /// <para>
    /// 这里**不维护任何范围或名称表**，全部查各厂商的 catalog：
    /// 三菱 3E/4E、三菱 1E、汇川、欧姆龙各有一份；西门子没有 catalog，只有 X/Y/M 三项，直接写死。
    /// 这样 README 里「一个区域叫什么、能不能按位读」只有一处真相。
    /// </para>
    /// </summary>
    public static class DeviceAreaInfoProvider
    {
        /// <summary>
        /// 该协议支持的全部软元件区域（按 catalog 的声明顺序）。
        /// 客户端界面的行集合建议直接用 <c>IPlcClient.GetSupportedAreas()</c>，
        /// 那个反映的是实际机型（例如汇川 Easy 比 H3U 少一半区域）；本方法供服务端等拿不到客户端实例的场景使用。
        /// </summary>
        public static IReadOnlyList<DeviceAreaInfo> GetAreas(ProtocolType protocol)
        {
            var result = new List<DeviceAreaInfo>();

            switch (protocol)
            {
                case ProtocolType.MelsecMc3EBinary:
                case ProtocolType.MelsecMc3EAscii:
                case ProtocolType.MelsecMc4EBinary:
                case ProtocolType.MelsecMc4EAscii:
                    foreach (Melsec3E4EDeviceSpec spec in Melsec3E4EDeviceCatalog.All)
                    {
                        result.Add(DeviceAreaInfo.FromAccess(spec.Area, spec.Name, spec.Access));
                    }
                    break;

                case ProtocolType.MelsecMc1EBinary:
                case ProtocolType.MelsecMc1EAscii:
                    foreach (Melsec1EDeviceSpec spec in Melsec1EDeviceCatalog.All)
                    {
                        result.Add(DeviceAreaInfo.FromAccess(spec.Area, spec.Name, spec.Access));
                    }
                    break;

                case ProtocolType.InovanceModbusTcp:
                case ProtocolType.InovanceModbusRtu:
                    // 以 H3U 为基准建表（服务端默认也是 H3U）；Easy 独有区域（B 区）在 Get 里兜底取名
                    foreach (InovanceDeviceSpec spec in InovanceDeviceCatalog.All)
                    {
                        if (InovanceDeviceCatalog.Contains(spec.Area, InovanceCpuModel.H3U))
                        {
                            result.Add(DeviceAreaInfo.FromAccess(spec.Area, spec.Name, spec.Access));
                        }
                    }
                    break;

                case ProtocolType.OmronFinsTcp:
                    foreach (OmronFinsDeviceSpec spec in OmronFinsDeviceCatalog.All)
                    {
                        result.Add(new DeviceAreaInfo(spec.Area, spec.Name, spec.SupportsBit, spec.SupportsWord));
                    }
                    break;

                case ProtocolType.SiemensS71200:
                    result.Add(new DeviceAreaInfo(MemoryArea.X, "输入", true, true));
                    result.Add(new DeviceAreaInfo(MemoryArea.Y, "输出", true, true));
                    result.Add(new DeviceAreaInfo(MemoryArea.M, "位存储区", true, true));
                    break;

                default:
                    // 新增协议时若忘了在这里登记软元件表，让它在第一次打开软元件视图时就炸出来，
                    // 而不是静默返回一张空表（那样只会看到界面一片空白，很难定位）。
                    throw new NotSupportedException(
                        "尚未登记软元件表: " + protocol + "，请在 DeviceAreaInfoProvider.GetAreas 中补充分支。");
            }

            return result;
        }

        /// <summary>
        /// 查单个区域的描述；协议不认识该区域时返回 null，调用方自行回退成区域名。
        /// </summary>
        public static DeviceAreaInfo Get(ProtocolType protocol, MemoryArea area)
        {
            foreach (DeviceAreaInfo info in GetAreas(protocol))
            {
                if (info.Area == area)
                {
                    return info;
                }
            }

            // 汇川 Easy 独有的区域（B 区）不在 H3U 表里，按 Easy 型号再查一次，
            // 否则客户端选 Easy 时 B 区只能显示成 "B"
            if ((protocol == ProtocolType.InovanceModbusTcp || protocol == ProtocolType.InovanceModbusRtu)
                && InovanceDeviceCatalog.Contains(area, InovanceCpuModel.Easy))
            {
                InovanceDeviceSpec spec = InovanceDeviceCatalog.Get(area, InovanceCpuModel.Easy);
                return DeviceAreaInfo.FromAccess(spec.Area, spec.Name, spec.Access);
            }

            return null;
        }

        /// <summary>
        /// 取描述文本，查不到时回退成区域枚举名（保证界面永远有内容可显示）。
        /// </summary>
        public static string GetName(ProtocolType protocol, MemoryArea area)
        {
            DeviceAreaInfo info = Get(protocol, area);
            return info != null ? info.Name : area.ToString();
        }
    }
}
