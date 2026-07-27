using PLCTest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLCTest.ClientProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱3EASCII通讯协议
    /// </summary>
    public class MelsecMc3EAsciiClientProtocol
    {
        /// <summary>
        /// 完整的包
        /// </summary>
        /// <param name="_data">数据区数组</param>
        /// <returns>完整的数据包</returns>
        public byte[] CompletePack(byte[] _data)
        {
            byte[] Command = new byte[22 + _data.Length];
            // 副标题
            Command[0] = 0x35;
            Command[1] = 0x30;
            Command[2] = 0x30;
            Command[3] = 0x30;
            //网络号
            Command[4] = 0x30;
            Command[5] = 0x30;
            // PLC编号
            Command[6] = 0x46;
            Command[7] = 0x46;
            // 目标模块IO编号
            Command[8] = 0x30;
            Command[9] = 0x33;
            Command[10] = 0x46;
            Command[11] = 0x46;
            //目标模块站号
            Command[12] = 0x30;
            Command[13] = 0x30;
            //数据长度
            byte[] mByte = Encoding.ASCII.GetBytes(Convert.ToString(Command.Length - 18, 16).PadLeft(4, '0').ToUpper());
            Command[14] = mByte[0];
            Command[15] = mByte[1];
            Command[16] = mByte[2];
            Command[17] = mByte[3];
            //CPU监视定时器
            Command[18] = 0x30;
            Command[19] = 0x30;
            Command[20] = 0x31;
            Command[21] = 0x30;
            _data.CopyTo(Command, 22);
            return Command;
        }
        /// <summary>
        /// 构建读数据指令数据区数组
        /// </summary>
        /// <param name="memory">软元件类型</param>
        /// <param name="adress">起始地址</param>
        /// <param name="length">地址长度</param>
        /// <param name="isBit">是否为位操作</param>
        /// <returns>数据区数组</returns>
        public byte[] BuildReadData(string memory, int adress, int length, bool isBit)
        {
            byte[] Data = new byte[20];
            //读:0401
            Data[0] = 0x30;
            Data[1] = 0x34;
            Data[2] = 0x30;
            Data[3] = 0x31;
            //以点为单位:0001,字为单位:0000
            Data[4] = 0x30;
            Data[5] = 0x30;
            Data[6] = 0x30;
            Data[7] = isBit ? (byte)0x31 : (byte)0x30;
            //软元件类型
            Data[8] = Encoding.ASCII.GetBytes(memory)[0];
            Data[9] = Encoding.ASCII.GetBytes("*")[0];
            //起始地址
            string Adress = adress.ToString().PadLeft(6, '0');
            byte[] mbyte = Encoding.ASCII.GetBytes(Adress);
            Array.Copy(mbyte, 0, Data, 10, 6);
            //软元件点数
            string Length = Convert.ToString(length, 16).PadLeft(4, '0').ToUpper();
            mbyte = Encoding.ASCII.GetBytes(Length);
            Array.Copy(mbyte, 0, Data, 16, 4);
            return Data;
        }
        /// <summary>
        /// 构建写数据指令数据区数组
        /// </summary>
        /// <param name="memory">软元件类型</param>
        /// <param name="adress">起始地址</param>
        /// <param name="length">地址长度</param>
        /// <param name="isBit">是否为位操作</param>
        /// <param name="data">写入的数据</param>
        /// <returns>数据区数组</returns>
        public byte[] BuildWriteData(string memory, int adress, int length, bool isBit, byte[] data)
        {
            byte[] Data = new byte[20 + data.Length];
            //写:1401
            Data[0] = 0x31;
            Data[1] = 0x34;
            Data[2] = 0x30;
            Data[3] = 0x31;
            //以点为单位:0001,字为单位:0000
            Data[4] = 0x30;
            Data[5] = 0x30;
            Data[6] = 0x30;
            Data[7] = isBit ? (byte)0x31 : (byte)0x30;
            //软元件类型
            Data[8] = Encoding.ASCII.GetBytes(memory)[0];
            Data[9] = Encoding.ASCII.GetBytes("*")[0];
            //起始地址
            string Adress = adress.ToString().PadLeft(6, '0');
            byte[] mbyte = Encoding.ASCII.GetBytes(Adress);
            Array.Copy(mbyte, 0, Data, 10, 6);
            //软元件点数
            string Length = Convert.ToString(length, 16).PadLeft(4, '0').ToUpper();
            mbyte = Encoding.ASCII.GetBytes(Length);
            Array.Copy(mbyte, 0, Data, 16, 4);
            //状态
            Array.Copy(data, 0, Data, 20, data.Length);
            return Data;
        }
        /// <summary>
        /// 解析读取位数据的返回报文
        /// </summary>
        /// <param name="_data">完整响应报文</param>
        /// <param name="size">期望的点数</param>
        /// <returns></returns>
        public Result<bool[]> ParseReadBitResponse(byte[] _data, int size)
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
            int dataOffset = 11;
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
        /// 解析读取字数据的返回报文
        /// </summary>
        /// <param name="_data">完整响应报文</param>
        /// <param name="size">期望的字数</param>
        /// <returns></returns>
        public Result<short[]> ParseReadWordResponse(byte[] _data, int size)
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
            int dataOffset = 11;
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
        /// 解析写入数据的返回报文
        /// </summary>
        /// <param name="_data">完整响应报文</param>
        /// <returns></returns>
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
        /// 校验 3E 响应副标题（0xD0 0x00）
        /// </summary>
        private static bool CheckResponseHeader(byte[] data)
        {
            return data[0] == 0xD0 && data[1] == 0x00;
        }
    }
}
