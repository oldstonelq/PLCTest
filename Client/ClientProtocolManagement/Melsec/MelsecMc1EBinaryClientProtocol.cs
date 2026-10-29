using PlcTest.Models;
using PlcTest.Client.ClientInterface;
using System;

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
        /// 根据请求推断整帧长度：1E 二进制响应没有长度字段，只能由"功能码 + 点数"推算。
        /// 响应 = 副标题(1) + 结束码(2) + 数据。
        /// </summary>
        public int GetResponseFrameLength(byte[] request, byte[] buffer, int available)
        {
            if (request == null || request.Length < 12) return -1;
            int points = request[10] | (request[11] << 8);
            switch (request[0])
            {
                case 0x00: return 3 + (points + 1) / 2;
                case 0x01: return 3 + points * 2;
                case 0x02:
                case 0x03: return 3;
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
            // 响应格式：副标题(1) + 结束代码(2，小端)
            if (data == null || data.Length < 3)
                return Result<bool>.Fail("响应数据过短。");

            // 检查完整 16 位结束代码，高字节非 0 的失败不能被漏判
            ushort endCode = BitConverter.ToUInt16(data, 1);
            if (endCode == 0)
                return Result<bool>.Success(true);
            else
                return Result<bool>.DeviceFail($"写入失败，结束代码: 0x{endCode:X4}", endCode);
        }
        /// <summary>
        /// 解析读位数据返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>位数据数组</returns>
        public Result<bool[]> ParseReadBitResponse(byte[] data, int size)
        {
            // 响应格式：副标题(1) + 结束代码(2，小端) + 位数据（每字节 2 点：高 4 位=偶数位，低 4 位=奇数位）
            int dataLen = (size + 1) / 2;
            if (data == null || data.Length < 3 + dataLen)
                return Result<bool[]>.Fail("响应数据长度不足。");

            ushort endCode = BitConverter.ToUInt16(data, 1);
            if (endCode != 0)
                return Result<bool[]>.DeviceFail($"读取失败，结束代码: 0x{endCode:X4}", endCode);

            bool[] bits = new bool[size];
            for (int i = 0; i < size; i++)
            {
                byte val = data[3 + i / 2];
                bits[i] = (i % 2 == 0) ? (val & 0x10) != 0 : (val & 0x01) != 0;
            }

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
            // 响应格式：副标题(1) + 结束代码(2，小端) + 数据(size * 2字节)
            int dataLen = size * 2;
            if (data == null || data.Length < 3 + dataLen)
                return Result<short[]>.Fail("响应数据长度不足。");

            ushort endCode = BitConverter.ToUInt16(data, 1);
            if (endCode != 0)
                return Result<short[]>.DeviceFail($"读取失败，结束代码: 0x{endCode:X4}", endCode);

            short[] values = new short[size];
            for (int i = 0; i < size; i++)
            {
                byte[] wordBytes = new byte[2];
                Array.Copy(data, 3 + i * 2, wordBytes, 0, 2);
                if (!BitConverter.IsLittleEndian)
                    Array.Reverse(wordBytes);
                values[i] = BitConverter.ToInt16(wordBytes, 0);
            }
            return Result<short[]>.Success(values);
        }
    }
}
