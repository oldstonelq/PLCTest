using PlcTest.Models;
using System;

namespace PlcTest.Client.ClientInterface
{
    /// <summary>
    /// 协议接口
    /// </summary>
    public interface IPlcClientProtocol
    {
        /// <summary>
        ///获取寄存器地址的真实编码
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码（int，协议可使用高位/低位表示不同语义）</returns>
        int GetMemoryCode(MemoryArea memoryArea);
        /// <summary>
        /// 该软元件区域在本协议下允许的访问方式。
        /// 位/字的判定只有这一处真相：<see cref="PlcTest.Client.PlcClient.CommunicationPlcClient"/>
        /// 用返回值做门禁，具体报文由调用方法决定（读位就按位拼包，读字就按字拼包）。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>允许的访问方式</returns>
        AreaAccess GetAreaAccess(MemoryArea memoryArea);
        /// <summary>
        /// 该软元件区域在本协议下的最大编号，供客户端在发报文之前做越界校验。
        /// 只声明"能到多少"，校验规则（起始点、连续访问末点）统一由客户端计算，
        /// 避免每个协议各写一遍同样的判断。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>最大编号；返回 -1 表示协议不做限制（范围由从站或 PLC 组态决定）</returns>
        int GetMaxIndex(MemoryArea memoryArea);
        /// <summary>
        /// 根据响应报文前缀推断"整帧应该有多长"，供通讯层按长度读满，
        /// 不再靠"连续若干毫秒没有新数据"猜帧尾。
        /// </summary>
        /// <param name="request">原始请求报文（MC 1E 这类响应不带长度字段的协议要靠它推算）</param>
        /// <param name="buffer">已收到的响应字节</param>
        /// <param name="available">buffer 中的有效字节数</param>
        /// <returns>整帧长度；无法判定或前缀不足时返回 -1，调用方回退到空闲判据</returns>
        int GetResponseFrameLength(byte[] request, byte[] buffer, int available);
        /// <summary>
        /// 写入位数据转Byte数组
        /// </summary>
        /// <param name="bools">位数据</param>
        /// <returns>打包后的字节数组</returns>
        byte[] GetWriteBitData(bool[] bools);
        /// <summary>
        /// 写入字数据转Byte数组
        /// </summary>
        /// <param name="shorts">字数据</param>
        /// <returns打包后的字节数组></returns>
        byte[] GetWriteWordData(short[] shorts);
        /// <summary>
        /// 创建读取数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <returns>读取命令报文</returns>
        byte[] BuildReadData(PlcAddress address);
        /// <summary>
        /// 创建写入数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <param name="data">写入数据</param>
        /// <returns>写命令报文</returns>
        byte[] BuildWriteData(PlcAddress address, byte[] data);
        /// <summary>
        /// 拼接完整数据包
        /// </summary>
        /// <param name="data">命令报文</param>
        /// <returns>完整命令报文</returns>
        byte[] BuildCompletePack(byte[] data);
        /// <summary>
        /// 解析写返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <returns>返回写入结果</returns>
        Result<bool> ParseWriteResponse(byte[] data);
        /// <summary>
        /// 解析读位数据返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>位数据数组</returns>
        Result<bool[]> ParseReadBitResponse(byte[] data,int size);
        /// <summary>
        /// 解析读字数据返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>字数据数组</returns>
        Result<short[]> ParseReadWordResponse(byte[] data,int size);
    }
    /// <summary>
    /// 响应帧长度解析委托：通讯层收到首段字节后回调，由协议决定"这一帧还要读多少"。
    /// 返回值 -1 表示无法判定，通讯层回退到空闲判据。
    /// </summary>
    /// <param name="request">原始请求报文</param>
    /// <param name="buffer">已收到的响应字节</param>
    /// <param name="available">buffer 中的有效字节数</param>
    /// <returns>整帧长度；-1 表示无法判定</returns>
    public delegate int ResponseFrameLengthResolver(byte[] request, byte[] buffer, int available);
}
