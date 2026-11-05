namespace PlcTest.Models
{
    /// <summary>
    /// 软元件（寄存器区域）的展示信息，「软元件视图」里的一行。
    /// <para>
    /// 描述与位/字能力都来自各厂商的 catalog，界面不再自己维护一份名称表，
    /// 避免出现「README 写 M 是内部继电器、界面写 M 是辅助继电器」这种两套说法。
    /// </para>
    /// </summary>
    public sealed class DeviceAreaInfo
    {
        /// <summary>软元件区域</summary>
        public MemoryArea Area { get; private set; }

        /// <summary>中文描述，例如「数据寄存器」「内部继电器」</summary>
        public string Name { get; private set; }

        /// <summary>是否支持按位访问</summary>
        public bool SupportsBit { get; private set; }

        /// <summary>是否支持按字访问</summary>
        public bool SupportsWord { get; private set; }

        /// <summary>备注（编号范围、机型差异等）。暂时留空，字段先占位。</summary>
        public string Remark { get; private set; }

        public DeviceAreaInfo(MemoryArea area, string name, bool supportsBit, bool supportsWord, string remark = null)
        {
            Area = area;
            Name = name;
            SupportsBit = supportsBit;
            SupportsWord = supportsWord;
            Remark = remark ?? string.Empty;
        }

        /// <summary>
        /// 用 <see cref="AreaAccess"/> 构造：把「位/字可访问」一次性转成两个布尔值。
        /// </summary>
        public static DeviceAreaInfo FromAccess(MemoryArea area, string name, AreaAccess access, string remark = null)
        {
            return new DeviceAreaInfo(
                area,
                name,
                (access & AreaAccess.Bit) != 0,
                (access & AreaAccess.Word) != 0,
                remark);
        }

        public override string ToString()
        {
            return Area + " " + Name;
        }
    }
}
