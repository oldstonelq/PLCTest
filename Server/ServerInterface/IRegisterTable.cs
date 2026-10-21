using PlcTest.Models;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// 模拟 PLC 服务端的软元件寄存器表管理契约。
    /// 只有需要维护“客户端声明可读写地址集合”的模拟服务端才实现本接口；
    /// 对接真实 PLC、或不需要地址表校验的服务端可以不实现。
    /// </summary>
    public interface IRegisterTable
    {
        /// <summary>获取指定区域的寄存器数量。</summary>
        int GetRegisterCount(MemoryArea area);

        /// <summary>判断指定区域是否包含某个地址的寄存器。</summary>
        bool ContainsRegister(MemoryArea area, int address);

        /// <summary>尝试向指定区域添加一个寄存器地址。</summary>
        bool TryAddRegister(MemoryArea area, int address);

        /// <summary>清空指定区域的全部寄存器。</summary>
        void ClearRegisters(MemoryArea area);

        /// <summary>把指定区间内的寄存器值恢复为初始值。</summary>
        void ResetRegisterValues(MemoryArea area, int start, int length);

        /// <summary>把指定区域的全部寄存器值恢复为初始值。</summary>
        void ResetAllRegisterValues(MemoryArea area);
    }
}
