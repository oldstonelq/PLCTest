using PlcTest.Client.ClientInterface;
using PlcTest.Models;
using PlcTest.Protocols;
using PlcTest.Server.ServerInterface;
using System;
using System.Collections.Generic;

namespace PlcTest.View.Support
{
    /// <summary>
    /// 一个协议在界面上的能力快照：支持哪些软元件、每个区域能不能按位/按字、默认落在哪个区域。
    /// <para>
    /// 存在的理由：客户端窗体和「软元件视图」原来各写了一份「遍历 GetSupportedAreas →
    /// 查 catalog 取名 → 合并成 DeviceAreaInfo」的循环，两个窗体还各自维护了
    /// 「哪些区域能按位」的过滤逻辑。协议从 10 种继续往上加时，这些副本一定会漂移——
    /// 之前两个窗体的位视图就写到了不同的列上，正是同一份逻辑抄两遍的后果。
    /// </para>
    /// <para>
    /// 所以把「这个协议有什么」收敛到一处：窗体只问它，不再自己判断厂商。
    /// 注意这里**不新增任何软元件知识**，名称与位/字能力仍旧只来自
    /// <see cref="DeviceAreaInfoProvider"/> 与协议实例自己的声明。
    /// </para>
    /// </summary>
    public sealed class ProtocolCapability
    {
        /// <summary>
        /// 位/字之外没有任何能力的空快照，供窗体在未连接时占位，避免到处判 null。
        /// </summary>
        public static readonly ProtocolCapability Empty =
            new ProtocolCapability(ProtocolType.MelsecMc3EBinary, new List<DeviceAreaInfo>());

        private readonly List<DeviceAreaInfo> _areas;
        private readonly Dictionary<MemoryArea, DeviceAreaInfo> _index;

        /// <summary>当前协议</summary>
        public ProtocolType Protocol { get; private set; }

        /// <summary>协议界面显示名（标题栏、错误提示用）</summary>
        public string DisplayName { get; private set; }

        /// <summary>按 catalog 顺序排列的区域列表，直接作为「软元件视图」的数据源</summary>
        public IReadOnlyList<DeviceAreaInfo> Areas { get { return _areas; } }

        /// <summary>是否一个区域都没有（未连接、或协议尚未登记软元件表）</summary>
        public bool IsEmpty { get { return _areas.Count == 0; } }

        /// <summary>
        /// 默认区域：优先 D（最常用），没有 D 时取第一个。
        /// 与旧窗体下拉框「优先选 D」的规则一致，避免换界面后默认值变了。
        /// </summary>
        public MemoryArea DefaultArea { get; private set; }

        private ProtocolCapability(ProtocolType protocol, List<DeviceAreaInfo> areas)
        {
            Protocol = protocol;
            DisplayName = protocol.GetDisplayName();
            _areas = areas;
            _index = new Dictionary<MemoryArea, DeviceAreaInfo>();
            foreach (DeviceAreaInfo info in areas)
            {
                _index[info.Area] = info;
            }

            DefaultArea = _index.ContainsKey(MemoryArea.D)
                ? MemoryArea.D
                : (areas.Count > 0 ? areas[0].Area : MemoryArea.D);
        }

        /// <summary>
        /// 从客户端取能力：行集合用客户端实际声明的区域（会随机型变化，如汇川 Easy 比 H3U 少一半），
        /// 描述查 catalog，位/字能力用客户端自己的 GetAreaAccess 覆盖——
        /// 与旧窗体 <c>BuildDeviceAreaInfos</c> 的口径完全一致。
        /// </summary>
        public static ProtocolCapability FromClient(IPlcClient client, ProtocolType protocol)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            var areas = new List<DeviceAreaInfo>();
            foreach (MemoryArea area in client.GetSupportedAreas())
            {
                DeviceAreaInfo spec = DeviceAreaInfoProvider.Get(protocol, area);
                string name = spec != null ? spec.Name : area.ToString();
                areas.Add(DeviceAreaInfo.FromAccess(area, name, client.GetAreaAccess(area)));
            }

            return new ProtocolCapability(protocol, areas);
        }

        /// <summary>
        /// 从服务端取能力。服务端接口没有 GetAreaAccess，位/字能力只能取 catalog；
        /// catalog 里查不到的区域按旧窗体的兜底规则处理（只有 D 当字区）。
        /// </summary>
        public static ProtocolCapability FromServer(IPlcServer server, ProtocolType protocol)
        {
            if (server == null)
            {
                throw new ArgumentNullException("server");
            }

            var areas = new List<DeviceAreaInfo>();
            foreach (MemoryArea area in server.GetSupportedAreas())
            {
                DeviceAreaInfo spec = DeviceAreaInfoProvider.Get(protocol, area);
                if (spec != null)
                {
                    areas.Add(spec);
                    continue;
                }

                bool isD = area == MemoryArea.D;
                areas.Add(new DeviceAreaInfo(area, area.ToString(), !isD, isD));
            }

            return new ProtocolCapability(protocol, areas);
        }

        /// <summary>
        /// 查区域信息；当前协议不支持该区域时返回 null。
        /// </summary>
        public DeviceAreaInfo Find(MemoryArea area)
        {
            DeviceAreaInfo info;
            return _index.TryGetValue(area, out info) ? info : null;
        }

        /// <summary>当前协议是否支持该区域</summary>
        public bool Supports(MemoryArea area)
        {
            return _index.ContainsKey(area);
        }

        /// <summary>该区域能否按位访问</summary>
        public bool SupportsBit(MemoryArea area)
        {
            DeviceAreaInfo info = Find(area);
            return info != null && info.SupportsBit;
        }

        /// <summary>该区域能否按字访问</summary>
        public bool SupportsWord(MemoryArea area)
        {
            DeviceAreaInfo info = Find(area);
            return info != null && info.SupportsWord;
        }

        /// <summary>
        /// 区域的能力，用 <see cref="AreaAccess"/> 表示，供「位/字视图开关」和写入口过滤使用。
        /// 不支持的区域返回 <see cref="AreaAccess.None"/>。
        /// </summary>
        public AreaAccess GetAccess(MemoryArea area)
        {
            DeviceAreaInfo info = Find(area);
            if (info == null)
            {
                return AreaAccess.None;
            }

            AreaAccess access = AreaAccess.None;
            if (info.SupportsBit)
            {
                access |= AreaAccess.Bit;
            }
            if (info.SupportsWord)
            {
                access |= AreaAccess.Word;
            }
            return access;
        }

        /// <summary>
        /// 按所需能力过滤区域，替代两个窗体里各自那份 FilterAreas。
        /// <paramref name="needed"/> 传 <see cref="AreaAccess.None"/> 表示不过滤。
        /// </summary>
        public IReadOnlyList<DeviceAreaInfo> Filter(AreaAccess needed)
        {
            if (needed == AreaAccess.None)
            {
                return _areas;
            }

            var result = new List<DeviceAreaInfo>();
            foreach (DeviceAreaInfo info in _areas)
            {
                if ((GetAccess(info.Area) & needed) != 0)
                {
                    result.Add(info);
                }
            }
            return result;
        }

        /// <summary>
        /// 取一个能用于错误提示的示例地址，如「D0」「M0」。
        /// 位地址优先 M（三菱/汇川/西门子都有），字地址优先默认区（通常是 D）。
        /// </summary>
        public string SuggestExample(bool bit)
        {
            MemoryArea area;
            if (bit)
            {
                if (SupportsBit(MemoryArea.M))
                {
                    area = MemoryArea.M;
                }
                else
                {
                    area = FirstArea(AreaAccess.Bit);
                }
            }
            else
            {
                area = SupportsWord(DefaultArea) ? DefaultArea : FirstArea(AreaAccess.Word);
            }

            return area.ToString() + "0";
        }

        /// <summary>
        /// 取第一个具备指定能力的区域；一个都没有时退回默认区（提示文本仍然有内容可显示）。
        /// </summary>
        private MemoryArea FirstArea(AreaAccess needed)
        {
            IReadOnlyList<DeviceAreaInfo> filtered = Filter(needed);
            return filtered.Count > 0 ? filtered[0].Area : DefaultArea;
        }

        /// <summary>
        /// 调试用：把整张能力表拼成一行。
        /// </summary>
        public override string ToString()
        {
            return DisplayName + " (" + _areas.Count + " 区域)";
        }
    }
}
