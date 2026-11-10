using PlcTest.Models;
using System.Collections.Generic;

namespace PlcTest.Client.ClientInterface
{
    /// <summary>
    /// 单帧装不下时把一次读写拆成多帧的协议。
    /// <para>
    /// 存在的理由：S7 的单帧读写受协商 PDU 长度约束（S7-1200 通常 960 字节），
    /// 一次读 2000 个字必须拆成多次。谁来拆？——**协议**，因为「地址怎么往后推」是协议知识：
    /// S7 的 <see cref="PlcAddress.Address"/> 是字节偏移，而三菱是软元件编号。
    /// 客户端只负责按计划逐帧收发再把结果拼起来。
    /// </para>
    /// <para>
    /// 不放进 <see cref="IPlcClientProtocol"/>：不分段的协议（三菱/汇川/欧姆龙目前的实现）
    /// 没有这个概念，硬加一个方法只会让它们返回「一段」了事，反而掩盖了「其实没做限制」这件事。
    /// </para>
    /// </summary>
    public interface IFrameSegmentProtocol
    {
        /// <summary>
        /// 把一个读请求拆成若干子请求。返回 1 段表示不用拆。
        /// 各段的顺序即数据的先后顺序，长度之和必须等于原请求长度。
        /// </summary>
        /// <param name="address">原始请求地址</param>
        /// <returns>分段后的地址列表（至少 1 项）</returns>
        IReadOnlyList<PlcAddress> PlanRead(PlcAddress address);

        /// <summary>
        /// 把一个写请求拆成若干子请求。数据切片由调用方按各段的 Length 顺序切。
        /// </summary>
        /// <param name="address">原始请求地址</param>
        /// <returns>分段后的地址列表（至少 1 项）</returns>
        IReadOnlyList<PlcAddress> PlanWrite(PlcAddress address);
    }
}
