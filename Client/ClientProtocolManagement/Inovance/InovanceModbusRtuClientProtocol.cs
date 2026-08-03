using PLCTest.Models;
using PLCTest.PLCInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static PLCTest.Models.Enums;

namespace PLCTest.Client.ClientProtocolManagement.Inovance
{
    /// <summary>
    /// Modbus RTU 实现的 Inovance 协议适配器。
    /// 构造 RTU 帧： [UnitId][PDU][CRClo][CRChi]
    /// </summary>
    public class InovanceModbusRtuClientProtocol : IPLCClientProtocol
    {
        /// <summary>
        /// 单元标识符，设备地址，一般默认1
        /// </summary>
        private readonly byte _unitId = 1;
        /// <summary>
        /// 获取寄存器地址的真实编码（返回 byte，以兼容通用接口）。
        /// </summary>
        /// <param name="MemoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码</returns>
        public int GetMemoryCode(MemoryArea MemoryArea)
        {
            switch (MemoryArea)
            {
                case MemoryArea.D:
                    return 0x00;
                case MemoryArea.R: 
                    return 0x3000;
                default: throw new ArgumentOutOfRangeException(nameof(MemoryArea), "无效寄存器类型");
            }
        }
        /// <summary>
        /// 写入位数据转Byte数组
        /// </summary>
        /// <param name="bools">位数据</param>
        /// <returns>打包后的字节数组</returns>
        public byte[] GetWriteBitData(bool[] bools)
        {
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
        public byte[] BuildReadData(PLCAddress address)
        {
            ushort start = (ushort)(GetMemoryCode(address.Area) + address.Address);
            ushort quantity = (ushort)address.Length;
            List<byte> pdu = new List<byte>();
            if (address.IsBit)
            {
                pdu.Add(0x01);
                pdu.Add((byte)(start >> 8)); pdu.Add((byte)(start & 0xFF));
                pdu.Add((byte)(quantity >> 8)); pdu.Add((byte)(quantity & 0xFF));
            }
            else
            {
                pdu.Add(0x03);
                pdu.Add((byte)(start >> 8)); pdu.Add((byte)(start & 0xFF));
                pdu.Add((byte)(quantity >> 8)); pdu.Add((byte)(quantity & 0xFF));
            }
            return pdu.ToArray();
        }
        /// <summary>
        /// 创建写入数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <param name="data">写入数据</param>
        /// <returns>写命令报文</returns>
        public byte[] BuildWriteData(PLCAddress address, byte[] data)
        {
            ushort start = (ushort)(GetMemoryCode(address.Area) + address.Address);
            ushort quantity = (ushort)address.Length;
            List<byte> pdu = new List<byte>();
            if (address.IsBit)
            {
                if (quantity == 1)
                {
                    pdu.Add(0x05);
                    pdu.Add((byte)(start >> 8)); pdu.Add((byte)(start & 0xFF));
                    bool on = (data.Length > 0 && (data[0] & 0x01) != 0);
                    pdu.Add((byte)(on ? 0xFF : 0x00)); pdu.Add(0x00);
                }
                else
                {
                    pdu.Add(0x0F);
                    pdu.Add((byte)(start >> 8)); pdu.Add((byte)(start & 0xFF));
                    pdu.Add((byte)(quantity >> 8)); pdu.Add((byte)(quantity & 0xFF));
                    pdu.Add((byte)data.Length);
                    pdu.AddRange(data);
                }
            }
            else
            {
                if (quantity == 1)
                {
                    pdu.Add(0x06);
                    pdu.Add((byte)(start >> 8)); pdu.Add((byte)(start & 0xFF));
                    pdu.AddRange(data.Take(2));
                }
                else
                {
                    pdu.Add(0x10);
                    pdu.Add((byte)(start >> 8)); pdu.Add((byte)(start & 0xFF));
                    pdu.Add((byte)(quantity >> 8)); pdu.Add((byte)(quantity & 0xFF));
                    pdu.Add((byte)data.Length);
                    pdu.AddRange(data);
                }
            }
            return pdu.ToArray();
        }
        /// <summary>
        /// 拼接完整数据包
        /// </summary>
        /// <param name="_data">命令报文</param>
        /// <returns>完整命令报文</returns>
        public byte[] BuildCompletePack(byte[] _data)
        {
            int len = 1 + _data.Length + 2;
            byte[] frame = new byte[len];
            frame[0] = _unitId;
            Array.Copy(_data, 0, frame, 1, _data.Length);
            ushort crc = ComputeCrc(frame, 1 + _data.Length);
            frame[len - 2] = (byte)(crc & 0xFF);
            frame[len - 1] = (byte)((crc >> 8) & 0xFF);
            return frame;
        }
        /// <summary>
        /// 解析写返回
        /// </summary>
        /// <param name="_data">返回数据</param>
        /// <returns>返回写入结果</returns>
        public Result<bool> ParseWriteResponse(byte[] _data)
        {
            var result = new Result<bool>();
            if (_data == null || _data.Length < 5)
            {
                result.ErrorMsg = "返回数据长度错误"; return result;
            }
            ushort crc = ComputeCrc(_data, _data.Length - 2);
            ushort respCrc = (ushort)(_data[_data.Length - 2] | (_data[_data.Length - 1] << 8));
            if (crc != respCrc) 
            { 
                result.ErrorMsg = "CRC 校验失败"; 
                return result;
            }
            int pduOffset = 1;
            byte func = _data[pduOffset];
            if ((func & 0x80) != 0) 
            {
                result.ErrorMsg = $"Modbus 异常: 0x{_data[pduOffset + 1]:X2}";
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
        public Result<bool[]> ParseReadBitResponse(byte[] _data, int size)
        {
            var result = new Result<bool[]>();
            if (_data == null || _data.Length < 5) 
            { 
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            ushort crc = ComputeCrc(_data, _data.Length - 2);
            ushort respCrc = (ushort)(_data[_data.Length - 2] | (_data[_data.Length - 1] << 8));
            if (crc != respCrc) 
            { 
                result.ErrorMsg = "CRC 校验失败";
                return result; 
            }
            int pduOffset = 1;
            byte func = _data[pduOffset];
            if ((func & 0x80) != 0) 
            {
                result.ErrorMsg = $"Modbus 异常: 0x{_data[pduOffset + 1]:X2}"; 
                return result; 
            }
            if (func != 0x01) 
            { 
                result.ErrorMsg = "功能码不匹配"; 
                return result; 
            }
            byte byteCount = _data[pduOffset + 1];
            if (_data.Length < pduOffset + 2 + byteCount + 2)
            { 
                result.ErrorMsg = "返回数据长度不足";
                return result; }

            bool[] bits = new bool[size];
            for (int i = 0; i < size; i++) 
            { 
                int b = i / 8; int bit = i % 8;
                bits[i] = (_data[pduOffset + 2 + b] & (1 << bit)) != 0; 
            }
            result.IsSuccess = true;
            result.Data = bits;
            return result;
        }
        /// <summary>
        /// 解析读字数据返回
        /// </summary>
        /// <param name="_data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>字数据数组</returns>
        public Result<short[]> ParseReadWordResponse(byte[] _data, int size)
        {
            var result = new Result<short[]>();
            if (_data == null || _data.Length < 5)
            { 
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            ushort crc = ComputeCrc(_data, _data.Length - 2);
            ushort respCrc = (ushort)(_data[_data.Length - 2] | (_data[_data.Length - 1] << 8));
            if (crc != respCrc) 
            { 
                result.ErrorMsg = "CRC 校验失败";
                return result; 
            }
            int pduOffset = 1;
            byte func = _data[pduOffset];
            if ((func & 0x80) != 0) 
            { 
                result.ErrorMsg = $"Modbus 异常: 0x{_data[pduOffset + 1]:X2}";
                return result;
            }
            if (func != 0x03) 
            { 
                result.ErrorMsg = "功能码不匹配";
                return result;
            }
            byte byteCount = _data[pduOffset + 1];
            if (_data.Length < pduOffset + 2 + byteCount + 2)
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
                regs[i] = (short)((_data[idx] << 8) | _data[idx + 1]);
            }
            result.IsSuccess = true;
            result.Data = regs;
            return result;
        }
        /// <summary>
        /// 获取CRC校验码
        /// </summary>
        /// <param name="data">数据</param>
        /// <param name="length">数据长度</param>
        /// <returns></returns>
        private static ushort ComputeCrc(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    bool lsb = (crc & 0x0001) != 0;
                    crc >>= 1;
                    if (lsb) crc ^= 0xA001;
                }
            }
            return crc;
        }
    }
}
