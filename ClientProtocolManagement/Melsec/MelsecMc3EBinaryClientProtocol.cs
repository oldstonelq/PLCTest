using PLCTest.Models;
using PLCTest.PLCInterface;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace PLCTest.ClientProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱3E二进制通讯协议
    /// </summary>
    public class MelsecMc3EBinaryClientProtocol : IPLCClientProtocol
    {
        /// <summary>
        /// 构建完整的报文数据包
        /// </summary>
        /// <param name="_data"></param>
        /// <returns></returns>
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
        /// 构建读取数据的报文
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="adress"></param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>
        /// <returns></returns>
        public byte[] BuildReadData(byte memory, int adress, int length, bool isBit)
        {
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
            Data[7] = memory;
            //软元件点数
            Data[8] = (byte)(length % 256);
            Data[9] = (byte)(length / 256);
            return Data;
        }
        /// <summary>
        /// 构建写入数据的报文
        /// </summary>
        /// <param name="memory"></param>
        /// <param name="adress"></param>
        /// <param name="length"></param>
        /// <param name="isBit"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public byte[] BuildWriteData(byte memory, int adress, int length, bool isBit, byte[] data)
        {
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
            Data[7] = memory;
            //软元件点数
            Data[8] = (byte)(length % 256);
            Data[9] = (byte)(length / 256);
            data.CopyTo(Data, 10);
            return Data;
        }
        /// <summary>
        /// 解析读取位数据的返回报文
        /// </summary>
        /// <param name="_data">完整响应报文</param>
        /// <param name="size">期望的点数</param>
        /// <returns></returns>
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
