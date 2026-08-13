using PLCTest.Models;
using PLCTest.PLCInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using static PLCTest.Models.Enums;

namespace PLCTest.Client.ClientProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱3E二进制通讯协议
    /// </summary>
    public class MelsecMc3EBinaryClientProtocol : IPLCClientProtocol
    {
        /// <summary>
        ///获取寄存器地址的真实编码
        /// </summary>
        /// <param name="MemoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码</returns>
        public int GetMemoryCode(MemoryArea MemoryArea)
        {
            switch (MemoryArea)    //软元件                    类型     编号范围   进制 
            {
                case MemoryArea.SM://特殊继电器               位数据   000000~001023  10进制
                    return 0x91;
                case MemoryArea.SD://特殊寄存器               字数据   000000~001023  10进制
                    return 0xA9;
                case MemoryArea.X://输入                      位数据   000000~0007FF  16进制
                    return 0x9C;
                case MemoryArea.Y://输出                      位数据   000000~0007FF  16进制
                    return 0x9D;
                case MemoryArea.M://内部继电器*2              位数据   000000~008191  10进制
                    return 0x90;
                case MemoryArea.L://锁存继电器*2              位数据   000000~002027  10进制
                    return 0x92;
                case MemoryArea.F://报警器                    位数据   000000~001023  10进制
                    return 0x93;
                case MemoryArea.V://变址继电器                位数据   000000~001023  10进制
                    return 0x94;
                case MemoryArea.B://链接继电器                位数据   000000~0007FF  16进制
                    return 0xA0;
                case MemoryArea.D://数据寄存器                字数据   000000~011135  10进制
                    return 0xA8;
                case MemoryArea.W://链接寄存器                字数据   000000~0007FF  16进制
                    return 0xB4;
                case MemoryArea.TS://定时器*3 -触点           位数据   000000~000511  10进制
                    return 0XC1;
                case MemoryArea.TC://定时器*3 -线圈           位数据   000000~000511  10进制
                    return 0xC0;
                case MemoryArea.TN://定时器*3 -当前值         字数据   000000~000511  10进制
                    return 0xC2;
                case MemoryArea.SS://累计定时器*3 -触点       位数据   000000~000511  10进制
                    return 0xC7;
                case MemoryArea.SC://累计定时器*3 -线圈       位数据   000000~000511  10进制
                    return 0xC6;
                case MemoryArea.SN://累计定时器*3 -当前值     字数据   000000~000511  10进制
                    return 0xC8;
                case MemoryArea.CS://计数器*3 -触点           位数据   000000~000511  10进制
                    return 0xC4;
                case MemoryArea.CC://计数器*3 -线圈           位数据   000000~000511  10进制
                    return 0xC3;
                case MemoryArea.CN://计数器*3 -当前值         字数据   000000~000511  10进制
                    return 0xC5;
                case MemoryArea.SB://链接特殊继电器           位数据   000000~0003FF  16进制
                    return 0xA1;
                case MemoryArea.SW://链接特殊寄存器           字数据   000000~0003FF  16进制
                    return 0xB5;
                case MemoryArea.S://步进继电器*2              位数据   000000~002027  10进制
                    return 0x98;
                case MemoryArea.DX://直接输入                 位数据   000000~0007FF  16进制
                    return 0xA2;
                case MemoryArea.DY://直接输出                 位数据   000000~0007FF  16进制
                    return 0xA3;
                case MemoryArea.Z://变址寄存器                字数据   000000~000009  10进制
                    return 0xCC;
                case MemoryArea.R://文件寄存器*4              字数据   000000~032767  16进制
                    return 0xAF;
                case MemoryArea.ZR://文件寄存器*6             字数据   000000~00FFFF  16进制
                    return 0xB0;
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
        public byte[] BuildReadData(PLCAddress address)
        {
            int memory = GetMemoryCode(address.Area);
            int adress = address.Address;
            bool isBit = address.IsBit;
            int length = address.Length;
            byte[] Data = new byte[10];
            //读:0401
            Data[0] = 0x01;
            Data[1] = 0x04;
            //以点为单位:0x01,字为单位0x00
            Data[2] = isBit ? (byte)0x01 : (byte)0x00;
            Data[3] = 0x00;
            //起始地址
            Data[4] = BitConverter.GetBytes(adress)[0];
            Data[5] = BitConverter.GetBytes(adress)[1];
            Data[6] = BitConverter.GetBytes(adress)[2];
            //软元件类型
            Data[7] = (byte)memory;
            //软元件点数
            Data[8] = (byte)(length % 256);
            Data[9] = (byte)(length / 256);
            return Data;
        }
        /// <summary>
        /// 创建写入数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <param name="data">写入数据</param>
        /// <returns>写命令报文</returns>
        public byte[] BuildWriteData(PLCAddress address, byte[] data)
        {
            int memory = GetMemoryCode(address.Area);
            int adress = address.Address;
            bool isBit = address.IsBit;
            int length = address.Length;
            byte[] Data = new byte[10 + data.Length];
            //读:1401
            Data[0] = 0x01;
            Data[1] = 0x14;
            //以点为单位:0x01,字为单位0x00
            Data[2] = isBit ? (byte)0x01 : (byte)0x00;
            Data[3] = 0x00;
            //起始地址
            Data[4] = BitConverter.GetBytes(adress)[0];
            Data[5] = BitConverter.GetBytes(adress)[1];
            Data[6] = BitConverter.GetBytes(adress)[2];
            //软元件类型
            Data[7] = (byte)memory;
            //软元件点数
            Data[8] = (byte)(length % 256);
            Data[9] = (byte)(length / 256);
            data.CopyTo(Data, 10);
            return Data;
        }
        /// <summary>
        /// 拼接完整数据包
        /// </summary>
        /// <param name="_data">命令报文</param>
        /// <returns>完整命令报文</returns>
        public byte[] BuildCompletePack(byte[] _data)
        {
            byte[] Command = new byte[11 + _data.Length];
            //副标题
            Command[0] = 0x50;
            Command[1] = 0x00;
            //网络号
            Command[2] = 0x00;
            //PLC编号
            Command[3] = 0xFF;
            //目标模块IO编号
            Command[4] = 0xFF;
            Command[5] = 0x03;
            //目标模块站号
            Command[6] = 0x00;
            //请求数据长度(剩余数据长度，CPU+data长度)
            Command[7] = (byte)((Command.Length - 9) % 256);
            Command[8] = (byte)((Command.Length - 9) / 256);
            //CPU监视定时器
            Command[9] = 0x0A;
            Command[10] = 0x00;
            _data.CopyTo(Command, 11);
            return Command;
        }
        /// <summary>
        /// 解析写返回
        /// </summary>
        /// <param name="_data">返回数据</param>
        /// <returns>返回写入结果</returns>
        public Result<bool> ParseWriteResponse(byte[] _data)
        {
            Result<bool> result = new Result<bool>();

            if (_data == null || _data.Length < 11)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            if (!CheckResponseHeader(_data))
            {
                result.ErrorMsg = "响应副标题错误";
                return result;
            }
            if (BitConverter.ToUInt16(_data, 9) != 0)
            {
                result.ErrorMsg = "结束代码错误";
                return result;
            }

            result.IsSuccess = true;
            result.Data = true;
            return result;
        }
        /// <summary>
        /// 解析读位数据返回
        /// </summary>
        /// <param name="_data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>位数据数组</returns>
        public Result<bool[]> ParseReadBitResponse(byte[] _data,int size)
        {
            Result<bool[]> result = new Result<bool[]>();

            if (_data == null || _data.Length <= 11)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            if (!CheckResponseHeader(_data))
            {
                result.ErrorMsg = "响应副标题错误";
                return result;
            }
            if (BitConverter.ToUInt16(_data, 9) != 0)
            {
                result.ErrorMsg = "结束代码错误";
                return result;
            }

            // 解析返回数据：每字节存 2 个位（高4位=偶数位，低4位=奇数位）
            bool[] RealData = new bool[size];
            int byteCount = (size + 1) / 2;
            int dataOffset = 11;//数据起始位置
            for (int bIdx = 0; bIdx < byteCount && dataOffset + bIdx < _data.Length; bIdx++)
            {
                byte val = _data[dataOffset + bIdx];
                int idx0 = bIdx * 2;
                if (idx0 < size)
                    RealData[idx0] = (val & 0x10) != 0;
                int idx1 = bIdx * 2 + 1;
                if (idx1 < size)
                    RealData[idx1] = (val & 0x01) != 0;
            }
            result.IsSuccess = true;
            result.Data = RealData;
            return result;
        }
        /// <summary>
        /// 解析读字数据返回
        /// </summary>
        /// <param name="_data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>字数据数组</returns>
        public Result<short[]> ParseReadWordResponse(byte[] _data ,int size)
        {
            Result<short[]> result = new Result<short[]>();

            if (_data == null || _data.Length <= 11)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            if (!CheckResponseHeader(_data))
            {
                result.ErrorMsg = "响应副标题错误";
                return result;
            }
            if (BitConverter.ToUInt16(_data, 9) != 0)
            {
                result.ErrorMsg = "结束代码错误";
                return result;
            }

            // 解析返回数据：每字 2 字节（小端）
            int dataOffset = 11;//数据起始位置
            int dataLen = _data.Length - dataOffset;
            int expectedLen = size * 2;
            if (dataLen < expectedLen)
            {
                result.ErrorMsg = "返回数据长度不足";
                return result;
            }

            short[] RealData = new short[size];
            for (int i = 0; i < size; i++)
            {
                RealData[i] = BitConverter.ToInt16(_data, dataOffset + i * 2);
            }
            result.IsSuccess = true;
            result.Data = RealData;
            return result;
        }
        /// <summary>
        /// 校验 3E 响应副标题（0xD0 0x00）
        /// </summary>
        private static bool CheckResponseHeader(byte[] data)
        {
            return data[0] == 0xD0 && data[1] == 0x00;
        }
    }
}
