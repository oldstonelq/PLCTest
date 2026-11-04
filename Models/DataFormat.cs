namespace PlcTest.Models
{
    /// <summary>
    /// 32 位数据（占两个 16 位寄存器）的字节序与字序。
    /// 命名与含义沿用 HslCommunication 的 DataFormat：
    /// A、B 是**第一个**寄存器（低地址）的高、低字节，C、D 是**第二个**寄存器的高、低字节。
    /// <para>
    /// 举一个值为 0x12345678 的 32 位数（A=0x12、B=0x34、C=0x56、D=0x78），
    /// 四种格式下两个寄存器里实际存的是：
    /// <list type="table">
    /// <item><term>ABCD</term><description>0x1234、0x5678（高字在前，标准大端）</description></item>
    /// <item><term>BADC</term><description>0x3412、0x7856（字序不变，字内字节交换）</description></item>
    /// <item><term>CDAB</term><description>0x5678、0x1234（字序颠倒，低字在前）</description></item>
    /// <item><term>DCBA</term><description>0x7856、0x3412（字序与字节序全颠倒，完整小端）</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public enum DataFormat
    {
        /// <summary>
        /// A B C D：高字在前、字内高字节在前。多数 Modbus 设备的默认（大端）排列。
        /// 注意三菱 MC 的双字是低字在前，不属于这一种。
        /// </summary>
        ABCD = 0,

        /// <summary>
        /// B A D C：**寄存器之间的先后顺序不变**，只交换每个 16 位寄存器内部的字节（A↔B、C↔D）。
        /// 相对少见，出现在部分仪表的自定义寄存器布局里。
        /// </summary>
        BADC = 1,

        /// <summary>
        /// C D A B：字序颠倒（低字在前），字内字节顺序不变。
        /// 汇川 32 位计数器/双字、欧姆龙 DMOV 写出的 32 位数都是这种。
        /// </summary>
        CDAB = 2,

        /// <summary>
        /// D C B A：字序与字内字节序全部颠倒，等价于把 4 个字节整体倒序。
        /// </summary>
        DCBA = 3,
    }
}
