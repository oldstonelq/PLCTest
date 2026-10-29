using PlcTest.Server.ServerInterface;
using System;

namespace PlcTest.Server.ServerProtocolManagement.Inovance
{
    /// <summary>
    /// Modbus RTU 从站协议。
    /// 帧结构：站号(1) + 功能码(1) + 数据 + CRC16(2，低字节在前)。
    /// RTU 没有长度字段，读/写单点固定 8 字节，批量写由字节数字段推算。
    /// </summary>
    public class InovanceModbusRtuServerProtocol : IModbusServerProtocol
    {
        /// <summary>
        /// 根据已收到的字节推断整帧长度。
        /// </summary>
        public int GetRequestFrameLength(byte[] buffer, int available)
        {
            if (buffer == null || available < 2)
            {
                return 0;
            }

            byte functionCode = buffer[1];
            switch (functionCode)
            {
                // 读线圈/离散量/寄存器/输入寄存器、写单线圈/单寄存器：固定 8 字节
                case 0x01:
                case 0x02:
                case 0x03:
                case 0x04:
                case 0x05:
                case 0x06:
                    return 8;

                // 写多个：站号+功能码+地址(2)+数量(2)+字节数(1)+数据+CRC(2)
                case 0x0F:
                case 0x10:
                    if (available < 7)
                    {
                        return 0;
                    }
                    return 9 + buffer[6];

                default:
                    return -1;
            }
        }

        /// <summary>
        /// 解析 RTU 帧；CRC 校验失败时返回 null。
        /// </summary>
        public ModbusRequestInfo ParseRequest(byte[] data)
        {
            if (data == null || data.Length < 8)
            {
                return null;
            }

            ushort expectedCrc = (ushort)(data[data.Length - 2] | (data[data.Length - 1] << 8));
            ushort actualCrc = ComputeCrc16(data, 0, data.Length - 2);
            if (expectedCrc != actualCrc)
            {
                return null;
            }

            var info = new ModbusRequestInfo
            {
                UnitId = data[0],
                FunctionCode = data[1],
            };

            switch (info.FunctionCode)
            {
                case 0x01:
                case 0x02:
                case 0x03:
                case 0x04:
                    info.StartAddress = ReadUInt16(data, 2);
                    info.Quantity = ReadUInt16(data, 4);
                    return info;

                case 0x05:
                case 0x06:
                    info.StartAddress = ReadUInt16(data, 2);
                    info.SingleValue = ReadUInt16(data, 4);
                    info.Quantity = 1;
                    return info;

                case 0x0F:
                case 0x10:
                    info.StartAddress = ReadUInt16(data, 2);
                    info.Quantity = ReadUInt16(data, 4);
                    int byteCount = data[6];
                    if (data.Length < 9 + byteCount) return null;
                    info.WriteData = new byte[byteCount];
                    Array.Copy(data, 7, info.WriteData, 0, byteCount);
                    return info;

                default:
                    return info;
            }
        }

        /// <summary>
        /// 读线圈响应：功能码 + 字节数 + 位打包数据。
        /// </summary>
        public byte[] BuildReadCoilsResponse(ModbusRequestInfo request, bool[] bits)
        {
            int byteCount = (bits.Length + 7) / 8;
            byte[] pdu = new byte[2 + byteCount];
            pdu[0] = request.FunctionCode;
            pdu[1] = (byte)byteCount;
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i])
                {
                    pdu[2 + i / 8] |= (byte)(1 << (i % 8));
                }
            }
            return BuildFrame(request, pdu);
        }

        /// <summary>
        /// 读寄存器响应：功能码 + 字节数 + 每寄存器两字节（高位在前）。
        /// </summary>
        public byte[] BuildReadRegistersResponse(ModbusRequestInfo request, short[] words)
        {
            int byteCount = words.Length * 2;
            byte[] pdu = new byte[2 + byteCount];
            pdu[0] = request.FunctionCode;
            pdu[1] = (byte)byteCount;
            for (int i = 0; i < words.Length; i++)
            {
                pdu[2 + i * 2] = (byte)(words[i] >> 8);
                pdu[3 + i * 2] = (byte)words[i];
            }
            return BuildFrame(request, pdu);
        }

        /// <summary>
        /// 写响应：回显起始地址与（值 / 数量）。
        /// </summary>
        public byte[] BuildWriteResponse(ModbusRequestInfo request)
        {
            ushort echo = request.IsMultipleWrite ? request.Quantity : request.SingleValue;
            byte[] pdu =
            {
                request.FunctionCode,
                (byte)(request.StartAddress >> 8), (byte)request.StartAddress,
                (byte)(echo >> 8), (byte)echo,
            };
            return BuildFrame(request, pdu);
        }

        /// <summary>
        /// 异常响应：功能码 | 0x80 + 异常码。
        /// </summary>
        public byte[] BuildExceptionResponse(ModbusRequestInfo request, byte exceptionCode)
        {
            byte[] pdu = { (byte)(request.FunctionCode | 0x80), exceptionCode };
            return BuildFrame(request, pdu);
        }

        /// <summary>
        /// 组包：站号 + PDU + CRC16（低字节在前）。
        /// </summary>
        private static byte[] BuildFrame(ModbusRequestInfo request, byte[] pdu)
        {
            byte[] frame = new byte[1 + pdu.Length + 2];
            frame[0] = request.UnitId;
            Array.Copy(pdu, 0, frame, 1, pdu.Length);

            ushort crc = ComputeCrc16(frame, 0, 1 + pdu.Length);
            frame[frame.Length - 2] = (byte)crc;
            frame[frame.Length - 1] = (byte)(crc >> 8);
            return frame;
        }

        /// <summary>
        /// Modbus CRC16：多项式 0xA001，初值 0xFFFF。
        /// </summary>
        private static ushort ComputeCrc16(byte[] data, int offset, int count)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < count; i++)
            {
                crc ^= data[offset + i];
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((crc & 0x0001) != 0)
                    {
                        crc >>= 1;
                        crc ^= 0xA001;
                    }
                    else
                    {
                        crc >>= 1;
                    }
                }
            }
            return crc;
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }
    }
}
