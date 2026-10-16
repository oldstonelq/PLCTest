using PlcTest.Models;
using PlcTest.Client.PlcInterface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlcTest.Client.ClientProtocolManagement.Inovance
{
    /// <summary>
    /// 使用 Modbus TCP 实现的汇川（Inovance）简单协议适配器。
    /// 将请求 PDU 构建为 Modbus PDU，并通过 BuildCompletePack 封装为 MBAP + PDU。
    /// 该实现支持读取/写入线圈（位）和保持寄存器（字）。
    /// </summary>
    public class InovanceModbusTcpClientProtocol : IPlcClientProtocol
    {
        /// <summary>
        /// 事务标识符（Transaction Identifier），用于区分请求和响应。每次发送请求时递增。
        /// </summary>
        private ushort _transactionId = 0;
        /// <summary>
        /// 单元标识符，设备地址，一般默认1
        /// </summary>
        private readonly byte _unitId = 1;
        /// <summary>
        /// 获取寄存器地址的真实编码（返回 byte，以兼容通用接口）。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码</returns>
        public int GetMemoryCode(MemoryArea memoryArea)
        {
            switch (memoryArea)
            {
                case MemoryArea.D:
                    return 0x00;
                case MemoryArea.R:
                    // 对于汇川 R 区返回 0x3000（需要使用 int 表示）
                    return 0x3000;
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
            // 按 Modbus coil 格式，低位先行打包（LSB first）
            int byteCount = (bools.Length + 7) / 8;
            byte[] data = new byte[byteCount];
            for (int i = 0; i < bools.Length; i++)
            {
                if (bools[i])
                {
                    int b = i / 8;
                    int bit = i % 8;
                    data[b] |= (byte)(1 << bit);
                }
            }
            return data;
        }
        /// <summary>
        /// 写入字数据转Byte数组
        /// </summary>
        /// <param name="shorts">字数据</param>
        /// <returns打包后的字节数组></returns>
        public byte[] GetWriteWordData(short[] shorts)
        {
            // Modbus registers are big-endian per register
            byte[] data = new byte[shorts.Length * 2];
            for (int i = 0; i < shorts.Length; i++)
            {
                data[i * 2] = (byte)((shorts[i] >> 8) & 0xFF);
                data[i * 2 + 1] = (byte)(shorts[i] & 0xFF);
            }
            return data;
        }
        /// <summary>
        /// 创建读取数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <returns>读取命令报文</returns>
        public byte[] BuildReadData(PlcAddress address)
        {
            // Modbus function: 0x01 Read Coils (bit), 0x03 Read Holding Registers (word)
            // 寄存器真实起始地址 = 区段基地址 + 地址偏移
            ushort start = (ushort)(GetMemoryCode(address.Area) + address.Address);
            ushort quantity = (ushort)address.Length;
            List<byte> pdu = new List<byte>();
            if (address.IsBit)
            {
                pdu.Add(0x01); // Read Coils
                pdu.Add((byte)(start >> 8));
                pdu.Add((byte)(start & 0xFF));
                pdu.Add((byte)(quantity >> 8));
                pdu.Add((byte)(quantity & 0xFF));
            }
            else
            {
                pdu.Add(0x03); // Read Holding Registers
                pdu.Add((byte)(start >> 8));
                pdu.Add((byte)(start & 0xFF));
                pdu.Add((byte)(quantity >> 8));
                pdu.Add((byte)(quantity & 0xFF));
            }
            return pdu.ToArray();
        }
        /// <summary>
        /// 创建写入数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <param name="data">写入数据</param>
        /// <returns>写命令报文</returns>
        public byte[] BuildWriteData(PlcAddress address, byte[] data)
        {
            // Build PDU for write operations
            // 寄存器真实起始地址 = 区段基地址 + 地址偏移
            ushort start = (ushort)(GetMemoryCode(address.Area) + address.Address);
            ushort quantity = (ushort)address.Length;
            List<byte> pdu = new List<byte>();
            if (address.IsBit)
            {
                if (quantity == 1)
                {
                    // Write Single Coil (0x05)
                    pdu.Add(0x05);
                    pdu.Add((byte)(start >> 8));
                    pdu.Add((byte)(start & 0xFF));
                    // data expected as single coil value byte array or packed; interpret first bit
                    bool on = (data.Length > 0 && (data[0] & 0x01) != 0);
                    pdu.Add((byte)(on ? 0xFF : 0x00));
                    pdu.Add(0x00);
                }
                else
                {
                    // Write Multiple Coils (0x0F)
                    pdu.Add(0x0F);
                    pdu.Add((byte)(start >> 8));
                    pdu.Add((byte)(start & 0xFF));
                    pdu.Add((byte)(quantity >> 8));
                    pdu.Add((byte)(quantity & 0xFF));
                    byte byteCount = (byte)data.Length;
                    pdu.Add(byteCount);
                    pdu.AddRange(data);
                }
            }
            else
            {
                if (quantity == 1)
                {
                    // Write Single Register (0x06)
                    pdu.Add(0x06);
                    pdu.Add((byte)(start >> 8));
                    pdu.Add((byte)(start & 0xFF));
                    // data length should be 2 bytes
                    pdu.AddRange(data.Take(2));
                }
                else
                {
                    // Write Multiple Registers (0x10)
                    pdu.Add(0x10);
                    pdu.Add((byte)(start >> 8));
                    pdu.Add((byte)(start & 0xFF));
                    pdu.Add((byte)(quantity >> 8));
                    pdu.Add((byte)(quantity & 0xFF));
                    byte byteCount = (byte)(data.Length);
                    pdu.Add(byteCount);
                    pdu.AddRange(data);
                }
            }
            return pdu.ToArray();
        }
        /// <summary>
        /// 拼接完整数据包
        /// </summary>
        /// <param name="data">命令报文</param>
        /// <returns>完整命令报文</returns>
        public byte[] BuildCompletePack(byte[] data)
        {
            // MBAP header: Transaction ID (2) + Protocol ID (2=0) + Length (2) + Unit ID (1)
            _transactionId++;
            ushort trans = _transactionId;
            ushort protocolId = 0;
            ushort length = (ushort)(data.Length + 1); // unit id + pdu
            byte[] mbap = new byte[7];
            mbap[0] = (byte)(trans >> 8);
            mbap[1] = (byte)(trans & 0xFF);
            mbap[2] = (byte)(protocolId >> 8);
            mbap[3] = (byte)(protocolId & 0xFF);
            mbap[4] = (byte)(length >> 8);
            mbap[5] = (byte)(length & 0xFF);
            mbap[6] = _unitId;
            byte[] packet = new byte[mbap.Length + data.Length];
            Array.Copy(mbap, 0, packet, 0, mbap.Length);
            Array.Copy(data, 0, packet, mbap.Length, data.Length);
            return packet;
        }
        /// <summary>
        /// 解析写返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <returns>返回写入结果</returns>
        public Result<bool> ParseWriteResponse(byte[] data)
        {
            Result<bool> result = new Result<bool>();
            if (data == null || data.Length < 8)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            int pduOffset = 7;
            byte func = data[pduOffset];
            if ((func & 0x80) != 0)
            {
                byte ex = data[pduOffset + 1];
                result.ErrorMsg = $"Modbus 异常响应: 0x{ex:X2}";
                return result;
            }
            // Success for write: function echoed
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
            if (data == null || data.Length < 9)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            int pduOffset = 7;
            byte func = data[pduOffset];
            if ((func & 0x80) != 0)
            {
                byte ex = data[pduOffset + 1];
                result.ErrorMsg = $"Modbus 异常响应: 0x{ex:X2}";
                return result;
            }
            if (func != 0x01)
            {
                result.ErrorMsg = "功能码不匹配";
                return result;
            }
            byte byteCount = data[pduOffset + 1];
            if (data.Length < pduOffset + 2 + byteCount)
            {
                result.ErrorMsg = "返回数据长度不足";
                return result;
            }
            bool[] bits = new bool[size];
            for (int i = 0; i < size; i++)
            {
                int b = i / 8;
                int bit = i % 8;
                bits[i] = (data[pduOffset + 2 + b] & (1 << bit)) != 0;
            }
            result.IsSuccess = true;
            result.Data = bits;
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
            if (data == null || data.Length < 9)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            int pduOffset = 7;
            byte func = data[pduOffset];
            if ((func & 0x80) != 0)
            {
                byte ex = data[pduOffset + 1];
                result.ErrorMsg = $"Modbus 异常响应: 0x{ex:X2}";
                return result;
            }
            if (func != 0x03)
            {
                result.ErrorMsg = "功能码不匹配";
                return result;
            }
            byte byteCount = data[pduOffset + 1];
            if (data.Length < pduOffset + 2 + byteCount)
            {
                result.ErrorMsg = "返回数据长度不足";
                return result;
            }
            if (byteCount < size * 2)
            {
                result.ErrorMsg = "返回字节数与期望不符";
                return result;
            }
            short[] regs = new short[size];
            for (int i = 0; i < size; i++)
            {
                int idx = pduOffset + 2 + i * 2;
                regs[i] = (short)((data[idx] << 8) | data[idx + 1]);
            }
            result.IsSuccess = true;
            result.Data = regs;
            return result;
        }
    }
}
