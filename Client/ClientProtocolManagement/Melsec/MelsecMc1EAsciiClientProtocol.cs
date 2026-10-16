using PlcTest.Models;
using PlcTest.Client.PlcInterface;
using System;
using System.Linq;
using System.Text;

namespace PlcTest.Client.ClientProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱1EASCII通讯协议
    /// </summary>
    public class MelsecMc1EAsciiClientProtocol : IPlcClientProtocol
    {
        /// <summary>
        /// 获取寄存器地址的真实编码（返回 int，可包含扩展编码）
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码</returns>
        public int GetMemoryCode(MemoryArea memoryArea)
        {
            switch (memoryArea)
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
                    throw new ArgumentOutOfRangeException(nameof(memoryArea), "无效寄存器类型");
            }
        }
        /// <summary>
        /// 写入位数据转Byte数组
        /// </summary>
        /// <param name="bools">位数据</param>
        /// <returns>打包后的字节数组</returns>
        public byte[] GetWriteBitData(bool[] bools)
        {
            byte[] mData = new byte[bools.Length];
            for (int i = 0; i < bools.Length; i++)
            {
                mData[i] = bools[i] ? (byte)0x31 : (byte)0x30;
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
            byte[] mData = new byte[shorts.Length * 4];
            for (int i = 0; i < shorts.Length; i++)
            {
                byte[] mbyte = Encoding.ASCII.GetBytes(Convert.ToString(shorts[i], 16).ToUpper().PadLeft(4, '0'));
                mbyte.CopyTo(mData, i * 4);
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
            byte[] Data = new byte[24];
            //副头部：功能码：0x00 批量位读取 0x01 批量字读取
            Data[0] = 0x30;
            Data[1] = isBit ? (byte)0x30 : (byte)0x31;
            //PLC编号 默认FF
            Data[2] = 0x46;
            Data[3] = 0x46;
            //超时时间 默认  0A 00
            Data[4] = 0x30;
            Data[5] = 0x30;
            Data[6] = 0x30;
            Data[7] = 0x41;
            //寄存器类型
            Data[8] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[0];
            Data[9] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[1];
            Data[10] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[2];
            Data[11] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[3];
            //起始地址
            string Address = startAddress.ToString().PadLeft(8, '0');
            byte[] mbyte = Encoding.ASCII.GetBytes(Address);
            Array.Copy(mbyte, 0, Data, 12, 8);
            //软元件点数
            // length = 8;
            ushort val = (ushort)length;
            // 先高低字节调换(大端): 高字节在前，低字节在后
            ushort bigEndianVal = (ushort)((val << 8) | (val >> 8));
            string Length = Convert.ToString(bigEndianVal, 16).PadLeft(4, '0').ToUpper();
            mbyte = Encoding.ASCII.GetBytes(Length);
            Array.Copy(mbyte, 0, Data, 20, 4);

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
            byte[] Data = new byte[24+ data.Length];
            //副头部：功能码：0x00 批量位读取 0x01 批量字读取
            Data[0] = 0x30;
            Data[1] = isBit ? (byte)0x32 : (byte)0x33;
            //PLC编号 默认FF
            Data[2] = 0x46;
            Data[3] = 0x46;
            //超时时间 默认  0A 00
            Data[4] = 0x30;
            Data[5] = 0x30;
            Data[6] = 0x30;
            Data[7] = 0x41;
            //寄存器类型
            Data[8] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[0];
            Data[9] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[1];
            Data[10] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[2];
            Data[11] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[3];
            //起始地址
            string Address = startAddress.ToString().PadLeft(8, '0');
            byte[] mbyte = Encoding.ASCII.GetBytes(Address);
            Array.Copy(mbyte, 0, Data, 12, 8);
            //软元件点数
            // length = 8;
            ushort val = (ushort)length;
            // 先高低字节调换(大端): 高字节在前，低字节在后
            ushort bigEndianVal = (ushort)((val << 8) | (val >> 8));
            string Length = Convert.ToString(bigEndianVal, 16).PadLeft(4, '0').ToUpper();
            mbyte = Encoding.ASCII.GetBytes(Length);
            Array.Copy(mbyte, 0, Data, 20, 4);
            //写入数据
            Array.Copy(data, 0, Data, 24, data.Length);
            return Data;
        }
        /// <summary>
        /// 拼接完整数据包
        /// </summary>
        /// <param name="data">命令报文</param>
        /// <returns>完整命令报文</returns>
        public byte[] BuildCompletePack(byte[] data)
        {
            //无需操作
            return data;
        }
        /// <summary>
        /// 解析写返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <returns>返回写入结果</returns>
        public Result<bool> ParseWriteResponse(byte[] data)
        {
            Result<bool> result = new Result<bool>();
            string mReStr = Encoding.ASCII.GetString(data);
            if (data == null || mReStr.Length < 4)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            //if (!CheckResponseHeader(data))
            //{
            //    result.ErrorMsg = "响应副标题错误";
            //    return result;
            //}
            if (Convert.ToUInt16(mReStr.Substring(2, 2), 16) != 0)
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
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>位数据数组</returns>
        public Result<bool[]> ParseReadBitResponse(byte[] data, int size)
        {
            Result<bool[]> result = new Result<bool[]>();
            string mReStr = Encoding.ASCII.GetString(data);
            if (data == null || mReStr.Length < 4)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            //if (!CheckResponseHeader(data))
            //{
            //    result.ErrorMsg = "响应副标题错误";
            //    return result;
            //}
            if (Convert.ToUInt16(mReStr.Substring(2, 2), 16) != 0)
            {
                result.ErrorMsg = "结束代码错误";
                return result;
            }

            // 解析返回数据
            bool[] RealData = new bool[size];
            RealData = mReStr.Substring(4, size).Select(o => o == '1').Take(size).ToArray();

            result.IsSuccess = true;
            result.Data = RealData;
            return result;
        }
        /// <summary>
        /// 解析读字数据返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>字数据数组</returns>
        public Result<short[]> ParseReadWordResponse(byte[] data, int size)
        {
            Result<short[]> result = new Result<short[]>();
            string mReStr = Encoding.ASCII.GetString(data);
            if (data == null || mReStr.Length < 4+size*4)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            //if (!CheckResponseHeader(data))
            //{
            //    result.ErrorMsg = "响应副标题错误";
            //    return result;
            //}
            if (Convert.ToUInt16(mReStr.Substring(2, 2), 16) != 0)
            {
                result.ErrorMsg = "结束代码错误";
                return result;
            }
            //解析返回数据
            short[] RealData = new short[size];
            for (int i = 0; i < size; i++)
            {
                RealData[i] = Convert.ToInt16(mReStr.Substring(4 + 4 * i, 4), 16);
            }

            result.IsSuccess = true;
            result.Data = RealData;
            return result;
        }
    }
}
