using PlcTest.Models;
using PlcTest.Client.ClientInterface;
using System;

namespace PlcTest.Client.ClientProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱MC4E二进制通讯协议
    /// </summary>
    public class MelsecMc4EBinaryClientProtocol : IPlcClientProtocol
    {
        /// <summary>
        ///获取寄存器地址的真实编码
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码</returns>
        public int GetMemoryCode(MemoryArea memoryArea)
        {
            // 二进制软元件代码取自 Melsec3E4EDeviceCatalog（3E / 4E 共用同一张表）
            return Melsec3E4EDeviceCatalog.Get(memoryArea).BinaryCode;
        }
        /// <summary>
        /// 该软元件区域允许的访问方式。
        /// 位软元件（M/X/Y 等）既可位访问也可字访问（字访问时每字 16 点）；字软元件（D/W/R 等）只能字访问。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>允许的访问方式</returns>
        public AreaAccess GetAreaAccess(MemoryArea memoryArea)
        {
            // 位/字能力由 Melsec3E4EDeviceCatalog 统一声明，协议不再自己维护第二套规则
            return Melsec3E4EDeviceCatalog.Get(memoryArea).Access;
        }

        /// <summary>
        /// 软元件编号上限取自 <see cref="Melsec3E4EDeviceCatalog"/>（与 3E 共用同一张表）。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>最大编号；-1 表示协议不做限制</returns>
        public int GetMaxIndex(MemoryArea memoryArea)
        {
            return Melsec3E4EDeviceCatalog.Get(memoryArea).DefaultMaxIndex;
        }

        /// <summary>
        /// 根据响应前缀推断整帧长度：4E 二进制响应为 13 字节头 + [11..12] 小端"数据长度"（从结束码起算）。
        /// </summary>
        public int GetResponseFrameLength(byte[] request, byte[] buffer, int available)
        {
            if (buffer == null || available < 13) return -1;
            if (buffer[0] != 0xD4 || buffer[1] != 0x00) return -1;
            return 13 + (buffer[11] | (buffer[12] << 8));
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
            byte[] Data = new byte[10];
            //读:0401
            Data[0] = 0x01;
            Data[1] = 0x04;
            //以点为单位:0x01,字为单位0x00
            Data[2] = isBit ? (byte)0x01 : (byte)0x00;
            Data[3] = 0x00;
            //起始地址
            Data[4] = BitConverter.GetBytes(startAddress)[0];
            Data[5] = BitConverter.GetBytes(startAddress)[1];
            Data[6] = BitConverter.GetBytes(startAddress)[2];
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
        public byte[] BuildWriteData(PlcAddress address, byte[] data)
        {
            int memory = GetMemoryCode(address.Area);
            int startAddress = address.Address;
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
            Data[4] = BitConverter.GetBytes(startAddress)[0];
            Data[5] = BitConverter.GetBytes(startAddress)[1];
            Data[6] = BitConverter.GetBytes(startAddress)[2];
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
        /// <param name="data">命令报文</param>
        /// <returns>完整命令报文</returns>
        public byte[] BuildCompletePack(byte[] data)
        {
            byte[] Command = new byte[15 + data.Length];
            //副标题
            Command[0] = 0x54;
            Command[1] = 0x00;
            //串行编号 默认0x001
            Command[2] = 0x01;
            Command[3] = 0x00;
            //固定
            Command[4] = 0x00;
            Command[5] = 0x00;
            //网络号
            Command[6] = 0x00;
            //PLC编号
            Command[7] = 0xFF;
            //目标模块IO编号
            Command[8] = 0xFF;
            Command[9] = 0x03;
            //目标模块站号
            Command[10] = 0x00;
            //请求数据长度(剩余数据长度，CPU+data长度)
            Command[11] = (byte)((Command.Length - 13) % 256);
            Command[12] = (byte)((Command.Length - 13) / 256);
            //CPU监视定时器
            Command[13] = 0x0A;
            Command[14] = 0x00;
            data.CopyTo(Command, 15);
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

            if (data == null || data.Length < 15)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            if (!CheckResponseHeader(data))
            {
                result.ErrorMsg = "响应副标题错误";
                return result;
            }
            ushort endCode = BitConverter.ToUInt16(data, 13);
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

            // 4E 协议位软元件单次读取上限为 7168 点
            if (size <= 0 || size > 7168)
            {
                result.ErrorMsg = "读取点数超出范围（1-7168）";
                return result;
            }
            if (data == null || data.Length <= 15)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            if (!CheckResponseHeader(data))
            {
                result.ErrorMsg = "响应副标题错误";
                return result;
            }
            ushort endCode = BitConverter.ToUInt16(data, 13);
            if (endCode != 0)
            {
                result.ErrorMsg = $"结束代码错误: 0x{endCode:X4}";
                result.Kind = ErrorKind.DeviceEndCode;
                result.EndCode = endCode;
                return result;
            }

            // 解析返回数据：每字节存 2 个位（高4位=偶数位，低4位=奇数位）
            bool[] RealData = new bool[size];
            int byteCount = (size + 1) / 2;
            int dataOffset = 15;//数据起始位置
            for (int bIdx = 0; bIdx < byteCount && dataOffset + bIdx < data.Length; bIdx++)
            {
                byte val = data[dataOffset + bIdx];
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
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>字数据数组</returns>
        public Result<short[]> ParseReadWordResponse(byte[] data, int size)
        {
            Result<short[]> result = new Result<short[]>();

            // 4E 协议字软元件单次读取上限为 960 点
            if (size <= 0 || size > 960)
            {
                result.ErrorMsg = "读取点数超出范围（1-960）";
                return result;
            }
            if (data == null || data.Length <= 15)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            if (!CheckResponseHeader(data))
            {
                result.ErrorMsg = "响应副标题错误";
                return result;
            }
            ushort endCode = BitConverter.ToUInt16(data, 13);
            if (endCode != 0)
            {
                result.ErrorMsg = $"结束代码错误: 0x{endCode:X4}";
                result.Kind = ErrorKind.DeviceEndCode;
                result.EndCode = endCode;
                return result;
            }

            // 解析返回数据：每字 2 字节（小端）
            int dataOffset = 15;//数据起始位置
            int dataLen = data.Length - dataOffset;
            int expectedLen = size * 2;
            if (dataLen < expectedLen)
            {
                result.ErrorMsg = "返回数据长度不足";
                return result;
            }

            short[] RealData = new short[size];
            for (int i = 0; i < size; i++)
            {
                RealData[i] = BitConverter.ToInt16(data, dataOffset + i * 2);
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
            return data[0] == 0xD4 && data[1] == 0x00;
        }
    }
}
