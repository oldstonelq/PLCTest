using PlcTest.Models;
using PlcTest.Client.PlcInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PlcTest.Models.Enums;

namespace PlcTest.Client.ClientProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱MC协议1E二进制协议实现类
    /// </summary>
    public class MelsecMc1EBinaryClientProtocol : IPlcClientProtocol
    {
        /// <summary>
        /// 获取寄存器地址的真实编码（返回 int，可包含扩展编码）
        /// </summary>
        /// <param name="MemoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码</returns>
        public int GetMemoryCode(MemoryArea MemoryArea)
        {
            switch (MemoryArea)
            {
                case MemoryArea.D:
                    return 0x4420;   //数据寄存器
                case MemoryArea.R:
                    return 0x5220;   //扩展寄存器
                case MemoryArea.TN:
                    return 0x544E;   //定时器-当前值 
                case MemoryArea.TS:
                    return 0x5453;   //定时器-触点
                case MemoryArea.CN:
                    return 0x434E;   //计数器-当前值
                case MemoryArea.CS:
                    return 0x4353;   //计数器-触点
                case MemoryArea.X:
                    return 0x5820;   //输入
                case MemoryArea.Y:
                    return 0x5920;   //输出
                case MemoryArea.M:
                    return 0x4D20;   //辅助继电器
                case MemoryArea.S:
                    return 0x5320;   //状态
                default:
                    throw new ArgumentOutOfRangeException(nameof(MemoryArea), "无效寄存器类型");
            }
        }
        /// <summary>
        /// 写入位数据转Byte数组
        /// </summary>
        /// <param name="bools">位数据</param>
        /// <returns>打包后的字节数组</returns>
        public byte[] GetWriteBitData(bool[] bools)
        {
            int mLength = (bools.Length + 1) / 2;
            byte[] mData = new byte[mLength];

            for (int i = 0; i < mLength; i++)
            {
                // 第 2*i 位 → 高4位 0x10
                if (bools[i * 2])
                {
                    mData[i] += 0x10;
                }
                // 判断是否存在下一组bit
                int secondBitIndex = i * 2 + 1;
                if (secondBitIndex < bools.Length)
                {
                    // 第 2*i+1 位 → 低4位 0x01
                    if (bools[secondBitIndex])
                    {
                        mData[i] += 0x01;
                    }
                }
            }
            return mData;
        }
        /// <summary>
        /// 写入字数据转Byte数组
        /// </summary>
        /// <param name="shorts">字数据</param>
        /// <returns打包后的字节数组></returns>
        public byte[] GetWriteWordData(short[] shorts)
        {
            byte[] mData = new byte[shorts.Length * 2];
            for (int i = 0; i < shorts.Length; i++)
            {
                byte[] mbyte = BitConverter.GetBytes(shorts[i]);
                mbyte.CopyTo(mData, i * 2);
            }
            return mData;
        }
        /// <summary>
        /// 创建读取数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <returns>读取命令报文</returns>
        public byte[] BuildReadData(PlcAddress address)
        {
            int memory = GetMemoryCode(address.Area);
            int startAddress = address.Address;
            bool isBit = address.IsBit;
            int length = address.Length;
            byte[] Data = new byte[12];
            //副头部：功能码：0x00 批量位读取 0x01 批量字读取
            Data[0] = isBit ? (byte)0x00 : (byte)0x01;
            //PLC编号 默认OXFF
            Data[1] = 0xFF;
            //超时时间 默认  0A 00
            Data[2] = 0x0A;
            Data[3] = 0x00;
            //起始地址
            Data[4] = BitConverter.GetBytes(startAddress)[0];
            Data[5] = BitConverter.GetBytes(startAddress)[1];
            Data[6] = BitConverter.GetBytes(startAddress)[2];
            Data[7] = BitConverter.GetBytes(startAddress)[3];
            //寄存器类型
            Data[8] = (byte)(memory);
            Data[9] = (byte)(memory >> 8);
            //读取长度
            Data[10] = BitConverter.GetBytes(length)[0];
            Data[11] = BitConverter.GetBytes(length)[1];
            return Data;
        }
        /// <summary>
        /// 创建写入数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <param name="data">写入数据</param>
        /// <returns>写命令报文</returns>
        public byte[] BuildWriteData(PlcAddress address, byte[] data)
        {
            int memory = GetMemoryCode(address.Area);
            int startAddress = address.Address;
            bool isBit = address.IsBit;
            int length = address.Length;
            byte[] Data = new byte[12+data.Length];
            //副头部：功能码：0x00 批量位写入 0x01 批量字写入
            Data[0] = isBit ? (byte)0x02 : (byte)0x03;
            //PLC编号 默认OXFF
            Data[1] = 0xFF;
            //超时时间 默认  0A 00
            Data[2] = 0x0A;
            Data[3] = 0x00;
            //起始地址
            Data[4] = BitConverter.GetBytes(startAddress)[0];
            Data[5] = BitConverter.GetBytes(startAddress)[1];
            Data[6] = BitConverter.GetBytes(startAddress)[2];
            Data[7] = BitConverter.GetBytes(startAddress)[3];
            //寄存器类型
            Data[8] = (byte)(memory);
            Data[9] = (byte)(memory >> 8);
            //读取长度
            Data[10] = BitConverter.GetBytes(length)[0];
            Data[11] = BitConverter.GetBytes(length)[1];
            //写入数据
            data.CopyTo(Data, 12);
            return Data;
        }
        /// <summary>
        /// 拼接完整数据包
        /// </summary>
        /// <param name="data">命令报文</param>
        /// <returns>完整命令报文</returns>
        public byte[] BuildCompletePack(byte[] data)
        {
            //1E没有头直接把读报文和写报文直接返回
            return data;
        }
        /// <summary>
        /// 解析写返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <returns>返回写入结果</returns>
        public Result<bool> ParseWriteResponse(byte[] data)
        {
            // 响应格式：副标题(1) + 结束代码(2)
            if (data == null || data.Length < 2)
                return Result<bool>.Fail("响应数据过短。");

            // 检查结束代码（小端）
            if (data[1] == 0x0000)
                return Result<bool>.Success(true);
            else
                return Result<bool>.Fail($"写入失败，结束代码: 0x{data[1]:X4}");
        }
        /// <summary>
        /// 解析读位数据返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>位数据数组</returns>
        public Result<bool[]> ParseReadBitResponse(byte[] data, int size)
        {
            // 响应格式：副标题(1) + 结束代码(2) + 数据(size字节)
            if (data == null || data.Length < 2 + size)
                return Result<bool[]>.Fail("响应数据长度不足。");
            
            if (data[1] != 0)
                return Result<bool[]>.Fail($"读取失败，结束代码: 0x{data[1]:X4}");

            bool[] bits = new bool[size];
            for (int i = 0; i < size; i++)
                bits[i] = data[2 + i] != 0x00;

            return Result<bool[]>.Success(bits);
        }
        /// <summary>
        /// 解析读字数据返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>字数据数组</returns>
        public Result<short[]> ParseReadWordResponse(byte[] data, int size)
        {
            // 响应格式：副标题(1) + 结束代码(2) + 数据(size * 2字节)
            int dataLen = size * 2;
            if (data == null || data.Length < 2 + dataLen)
                return Result<short[]>.Fail("响应数据长度不足。");
           
            if (data[1] != 0)
                return Result<short[]>.Fail($"读取失败，结束代码: 0x{data[1]:X4}");

            short[] values = new short[size];
            for (int i = 0; i < size; i++)
            {
                byte[] wordBytes = new byte[2];
                Array.Copy(data, 2 + i * 2, wordBytes, 0, 2);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(wordBytes);
                values[i] = BitConverter.ToInt16(wordBytes, 0);
            }
            return Result<short[]>.Success(values);
        }
    }
}
