using PlcTest.Models;

namespace PlcTest.Client.ClientInterface
{
    /// <summary>
    /// 支持接收协议私有选项的协议实现（汇川机型、字节序、S7 机架槽号、FINS 网络号……）。
    /// <para>
    /// 为什么用接口而不是反射设属性：反射要求键名与属性名完全一致，改名时编译期发现不了，
    /// 而且选项名和协议字段名一旦分家就会静默失效。接口方式下每个协议只认自己那几个键，
    /// 不认识的一律忽略，行为是显式的、可搜索的。
    /// </para>
    /// <para>
    /// Builder 在 <c>Build()</c> 里按接口判断并调用，所以顺序无关紧要：
    /// <c>UseProtocol(p).UseOptions(o)</c> 和 <c>UseOptions(o).UseProtocol(p)</c> 效果一样。
    /// </para>
    /// </summary>
    public interface IProtocolOptionHost
    {
        /// <summary>
        /// 应用选项。实现方应逐个键做「取到就用、取不到保持默认」，不要因为多余键抛异常
        /// （同一份选项集会发给不同协议，多余键是正常的）。
        /// </summary>
        /// <param name="options">选项集，可能为 null</param>
        void ApplyOptions(ProtocolOptions options);
    }
}
