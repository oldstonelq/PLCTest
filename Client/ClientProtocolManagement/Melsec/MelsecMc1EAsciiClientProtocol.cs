using PlcTest.Models;
using PlcTest.Client.ClientInterface;
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
        /// <summary>
        /// 适用机型，决定软元件编号上限。默认 FX3G / FX3GC（依据 Document/Melsec/1E寄存器地址.png）。
        /// </summary>
        public Melsec1ECpuModel CpuModel { get; set; } = Melsec1ECpuModel.Fx3G;

        public int GetMemoryCode(MemoryArea memoryArea)
        {
            // 2 字节软元件代码取自 1E 软元件表；单字符软元件用空格(0x20)补齐，与 3E 的 '*' 不同
            return Melsec1EDeviceCatalog.Get(memoryArea).Code;
        }
        /// <summary>
        /// 该软元件区域允许的访问方式。
        /// A 兼容 1E 帧支持的软元件较少：位软元件 X/Y/M/S/TS/CS，字软元件 D/R/TN/CN。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>允许的访问方式</returns>
        public AreaAccess GetAreaAccess(MemoryArea memoryArea)
        {
            // 位/字能力由 1E 软元件表统一声明：TS/CS 是触点只支持位，TN/CN 是当前值只支持字
            return Melsec1EDeviceCatalog.Get(memoryArea).Access;
        }

        /// <summary>
        /// 软元件编号上限取自 <see cref="Melsec1EDeviceCatalog"/>，按 <see cref="CpuModel"/> 机型取值（默认 FX3G/FX3GC）。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>最大编号；-1 表示协议不做限制</returns>
        public int GetMaxIndex(MemoryArea memoryArea)
        {
            return Melsec1EDeviceCatalog.Get(memoryArea).GetMaxIndex(CpuModel);
        }



        /// <summary>
        /// 根据请求推断整帧长度：1E ASCII 响应没有长度字段，只能由"功能码字符 + 点数"推算。
        /// 响应 = 副标题(2 字符) + 结束码(2 字符) + 数据。
        /// </summary>
        public int GetResponseFrameLength(byte[] request, byte[] buffer, int available)
        {
            if (request == null || request.Length < 24) return -1;
            ushort swapped;
            if (!ushort.TryParse(Encoding.ASCII.GetString(request, 20, 4),
                    System.Globalization.NumberStyles.HexNumber, null, out swapped))
            {
                return -1;
            }
            int points = (ushort)((swapped << 8) | (swapped >> 8));
            switch ((char)request[1])
            {
                case '0': return 4 + points;
                case '1': return 4 + points * 4;
                case '2':
                case '3': return 4;
                default: return -1;
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
            //寄存器类型：1E ASCII 为 4 位十六进制文本（如 0x4420 → "4420"）
            Data[8] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[0];
            Data[9] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[1];
            Data[10] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[2];
            Data[11] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[3];
            //起始地址
            string Address = startAddress.ToString("X8");
            byte[] mbyte = Encoding.ASCII.GetBytes(Address);
            Array.Copy(mbyte, 0, Data, 12, 8);
            //软元件点数
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
            byte[] Data = new byte[24 + data.Length];
            //副头部：功能码：0x02 批量位写入 0x03 批量字写入
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
            //寄存器类型：1E ASCII 为 4 位十六进制文本（如 0x4420 → "4420"）
            Data[8] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[0];
            Data[9] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[1];
            Data[10] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[2];
            Data[11] = Encoding.ASCII.GetBytes(memory.ToString("X4"))[3];
            //起始地址
            string Address = startAddress.ToString("X8");
            byte[] mbyte = Encoding.ASCII.GetBytes(Address);
            Array.Copy(mbyte, 0, Data, 12, 8);
            //软元件点数
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
            ushort endCode = Convert.ToUInt16(mReStr.Substring(2, 2), 16);
            if (endCode != 0)
            {
                result.ErrorMsg = $"结束代码错误: 0x{endCode:X2}";
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
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            string mReStr = Encoding.ASCII.GetString(data);
            if (mReStr.Length < 4 + size)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            ushort endCode = Convert.ToUInt16(mReStr.Substring(2, 2), 16);
            if (endCode != 0)
            {
                result.ErrorMsg = $"结束代码错误: 0x{endCode:X2}";
                result.Kind = ErrorKind.DeviceEndCode;
                result.EndCode = endCode;
                return result;
            }

            // 解析返回数据
            bool[] RealData = mReStr.Substring(4, size).Select(o => o == '1').Take(size).ToArray();

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
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            string mReStr = Encoding.ASCII.GetString(data);
            if (mReStr.Length < 4 + size * 4)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            ushort endCode = Convert.ToUInt16(mReStr.Substring(2, 2), 16);
            if (endCode != 0)
            {
                result.ErrorMsg = $"结束代码错误: 0x{endCode:X2}";
                result.Kind = ErrorKind.DeviceEndCode;
                result.EndCode = endCode;
                return result;
            }
            //解析返回数据（4 位十六进制文本，用 ToUInt16 再转 short，避免 0x8000 以上溢出）
            short[] RealData = new short[size];
            for (int i = 0; i < size; i++)
            {
                RealData[i] = unchecked((short)Convert.ToUInt16(mReStr.Substring(4 + 4 * i, 4), 16));
            }

            result.IsSuccess = true;
            result.Data = RealData;
            return result;
        }
    }
}
