using PlcTest.Models;
using PlcTest.Client.PlcInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlcTest.Client.ClientProtocolManagement.Template
{
    /// <summary>
    /// 协议模板类，未实现的新协议可以复制粘贴使用
    /// </summary>
    public class TemplateClientProtocol : IPlcClientProtocol
    {
        /// <summary>
        ///获取寄存器地址的真实编码
        /// </summary>
        /// <param name="MemoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码（int，协议可使用高位/低位表示不同语义）</returns>
        public int GetMemoryCode(Enums.MemoryArea MemoryArea)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 写入位数据转Byte数组
        /// </summary>
        /// <param name="bools">位数据</param>
        /// <returns>打包后的字节数组</returns>
        public byte[] GetWriteBitData(bool[] bools)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 写入字数据转Byte数组
        /// </summary>
        /// <param name="shorts">字数据</param>
        /// <returns打包后的字节数组></returns>
        public byte[] GetWriteWordData(short[] shorts)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 创建读取数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <returns>读取命令报文</returns>
        public byte[] BuildReadData(PlcAddress address)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 创建写入数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <param name="data">写入数据</param>
        /// <returns>写命令报文</returns>
        public byte[] BuildWriteData(PlcAddress address, byte[] data)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 拼接完整数据包
        /// </summary>
        /// <param name="data">命令报文</param>
        /// <returns>完整命令报文</returns>
        public byte[] BuildCompletePack(byte[] data)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 解析写返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <returns>返回写入结果</returns>
        public Result<bool> ParseWriteResponse(byte[] data)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 解析读位数据返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>位数据数组</returns>
        public Result<bool[]> ParseReadBitResponse(byte[] data, int size)
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// 解析读字数据返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>字数据数组</returns>
        public Result<short[]> ParseReadWordResponse(byte[] data, int size)
        {
            throw new NotImplementedException();
        }
    }
}
