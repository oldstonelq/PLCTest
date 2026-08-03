using PLCTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLCTest.Models.Enums;

namespace PLCTest.PLCInterface
{
    /// <summary>
    /// 协议接口
    /// </summary>
    public interface IPLCClientProtocol
    {
        /// <summary>
        ///获取寄存器地址的真实编码
        /// </summary>
        /// <param name="MemoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码（int，协议可使用高位/低位表示不同语义）</returns>
        int GetMemoryCode(MemoryArea MemoryArea);
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
        byte[] BuildReadData(PLCAddress address);
        /// <summary>
        /// 创建写入数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <param name="data">写入数据</param>
        /// <returns>写命令报文</returns>
        byte[] BuildWriteData(PLCAddress address, byte[] data);
        /// <summary>
        /// 拼接完整数据包
        /// </summary>
        /// <param name="_data">命令报文</param>
        /// <returns>完整命令报文</returns>
        byte[] BuildCompletePack(byte[] _data);
        /// <summary>
        /// 解析写返回
        /// </summary>
        /// <param name="_data">返回数据</param>
        /// <returns>返回写入结果</returns>
        Result<bool> ParseWriteResponse(byte[] _data);
        /// <summary>
        /// 解析读位数据返回
        /// </summary>
        /// <param name="_data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>位数据数组</returns>
        Result<bool[]> ParseReadBitResponse(byte[] _data,int size);
        /// <summary>
        /// 解析读字数据返回
        /// </summary>
        /// <param name="_data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>字数据数组</returns>
        Result<short[]> ParseReadWordResponse(byte[] _data,int size);
    }
}
