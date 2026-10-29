using PlcTest.Models;
using PlcTest.Client.ClientInterface;
using System;
using System.Linq;
using System.Text;

namespace PlcTest.Client.ClientProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱3EASCII通讯协议
    /// </summary>
    public class MelsecMc3EAsciiClientProtocol : IPlcClientProtocol
    {
        /// <summary>
        ///获取寄存器地址的真实编码
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码</returns>
        public int GetMemoryCode(MemoryArea memoryArea)
        {
            // ASCII 软元件代码由 2 个 ASCII 字符打包：高字节第 1 字符，低字节第 2 字符，
            // 单字符软元件（X/Y/M/L/F/V/B/D/W/S/Z/R）补 '*'，多字符软元件（SM/TS/DX/ZR…）用双字母。
            return Melsec3E4EDeviceCatalog.Get(memoryArea).AsciiCodeWord;
        }
        /// <summary>
        /// 该软元件区域允许的访问方式。
        /// 位软元件（M/X/Y 等）既可位访问也可字访问（字访问时每字 16 点）；字软元件（D/W/R 等）只能字访问。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>允许的访问方式</returns>
        public AreaAccess GetAreaAccess(MemoryArea memoryArea)
        {
            // 位/字能力由软元件表统一声明，协议不再自己维护第二套规则
            return Melsec3E4EDeviceCatalog.Get(memoryArea).Access;
        }

        /// <summary>
        /// 软元件编号上限取自 <see cref="Melsec3E4EDeviceCatalog"/>（基本型 QCPU 默认范围），
        /// 仅用于发出报文前的越界提示，不影响报文结构。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>最大编号；-1 表示协议不做限制</returns>
        public int GetMaxIndex(MemoryArea memoryArea)
        {
            return Melsec3E4EDeviceCatalog.Get(memoryArea).DefaultMaxIndex;
        }

        /// <summary>
        /// 根据响应前缀推断整帧长度：3E ASCII 响应为 18 字符头 + [14..17] 四位十六进制"数据长度"（从结束码起算）。
        /// </summary>
        public int GetResponseFrameLength(byte[] request, byte[] buffer, int available)
        {
            if (buffer == null || available < 18) return -1;
            if (buffer[0] != (byte)'D' || buffer[1] != (byte)'0') return -1;
            string lengthText = Encoding.ASCII.GetString(buffer, 14, 4);
            ushort bodyLength;
            if (!ushort.TryParse(lengthText, System.Globalization.NumberStyles.HexNumber, null, out bodyLength))
            {
                return -1;
            }
            return 18 + bodyLength;
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
            //软元件类型（2 个 ASCII 字符，高字节为第 1 字符）
            Data[8] = (byte)((memory >> 8) & 0xFF);
            Data[9] = (byte)(memory & 0xFF);
            //起始地址
            string Address = Melsec3E4EDeviceCatalog.Get(address.Area).FormatIndex(startAddress, 6);
            byte[] mbyte = Encoding.ASCII.GetBytes(Address);
            Array.Copy(mbyte, 0, Data, 10, 6);
            //软元件点数
            string Length = Convert.ToString(length, 16).PadLeft(4, '0').ToUpper();
            mbyte = Encoding.ASCII.GetBytes(Length);
            Array.Copy(mbyte, 0, Data, 16, 4);
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
            //软元件类型（2 个 ASCII 字符，高字节为第 1 字符）
            Data[8] = (byte)((memory >> 8) & 0xFF);
            Data[9] = (byte)(memory & 0xFF);
            //起始地址
            string Address = Melsec3E4EDeviceCatalog.Get(address.Area).FormatIndex(startAddress, 6);
            byte[] mbyte = Encoding.ASCII.GetBytes(Address);
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
        /// 拼接完整数据包
        /// </summary>
        /// <param name="data">命令报文</param>
        /// <returns>完整命令报文</returns>
        public byte[] BuildCompletePack(byte[] data)
        {
            byte[] Command = new byte[22 + data.Length];
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
            data.CopyTo(Command, 22);
            return Command;
        }
        /// <summary>
        /// 解析写返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <returns>返回写入结果</returns>
        public Result<bool> ParseWriteResponse(byte[] data)
        {
            Result<bool> result = new Result<bool>();
            if (data == null)
            {
                result.ErrorMsg = "返回数据为空";
                return result;
            }
            string mReStr = Encoding.ASCII.GetString(data);
            if (mReStr.Length < 22)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            //if (!CheckResponseHeader(data))
            //{
            //    result.ErrorMsg = "响应副标题错误";
            //    return result;
            //}
            ushort endCode = Convert.ToUInt16(mReStr.Substring(18, 4), 16);
            if (endCode != 0)
            {
                result.ErrorMsg = $"结束代码错误: 0x{endCode:X4}";
                result.Kind = ErrorKind.DeviceEndCode;
                result.EndCode = endCode;
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
            if (data == null)
            {
                result.ErrorMsg = "返回数据为空";
                return result;
            }
            string mReStr = Encoding.ASCII.GetString(data);
            if (mReStr.Length < 22 + size)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            //if (!CheckResponseHeader(data))
            //{
            //    result.ErrorMsg = "响应副标题错误";
            //    return result;
            //}
            ushort endCode = Convert.ToUInt16(mReStr.Substring(18, 4), 16);
            if (endCode != 0)
            {
                result.ErrorMsg = $"结束代码错误: 0x{endCode:X4}";
                result.Kind = ErrorKind.DeviceEndCode;
                result.EndCode = endCode;
                return result;
            }

            // 解析返回数据
            bool[] RealData = new bool[size];
            RealData = mReStr.Substring(22, size).Select(o => o == '1').Take(size).ToArray();

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
            if (data == null)
            {
                result.ErrorMsg = "返回数据为空";
                return result;
            }
            string mReStr = Encoding.ASCII.GetString(data);
            if (mReStr.Length < 22 + 4 * size)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            //if (!CheckResponseHeader(data))
            //{
            //    result.ErrorMsg = "响应副标题错误";
            //    return result;
            //}
            ushort endCode = Convert.ToUInt16(mReStr.Substring(18, 4), 16);
            if (endCode != 0)
            {
                result.ErrorMsg = $"结束代码错误: 0x{endCode:X4}";
                result.Kind = ErrorKind.DeviceEndCode;
                result.EndCode = endCode;
                return result;
            }
            //解析返回数据
            short[] RealData = new short[size];
            for (int i = 0; i < size; i++)
            {
                RealData[i] = Convert.ToInt16(mReStr.Substring(22 + 4 * i, 4), 16);
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
