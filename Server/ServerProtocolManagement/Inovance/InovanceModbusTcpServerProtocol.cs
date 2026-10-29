using PlcTest.Server.ServerInterface;
using System;

namespace PlcTest.Server.ServerProtocolManagement.Inovance
{
    /// <summary>
    /// Modbus TCP 从站协议。
    /// 帧结构：MBAP(7) + PDU，MBAP 的 Length 字段为「其后字节数」（含 UnitId）。
    /// </summary>
    public class InovanceModbusTcpServerProtocol : IModbusServerProtocol
    {
        /// <summary>支持的最大 PDU 长度（Modbus 规范 253，留余量）</summary>
        private const int MaxPduLength = 254;

        /// <summary>
        /// 根据已收到的字节推断整帧长度：MBAP 的 [4..5] 大端为「其后字节数」（含 UnitId）。
        /// </summary>
        public int GetRequestFrameLength(byte[] buffer, int available)
        {
            if (buffer == null || available < 6)
            {
                return 0;
            }

            int length = (buffer[4] << 8) | buffer[5];
            if (length < 2 || length > MaxPduLength)
            {
                return -1;
            }
            return 6 + length;
        }

        /// <summary>
        /// 解析 MBAP + PDU；协议标识不是 0 或长度不足时返回 null。
        /// </summary>
        public ModbusRequestInfo ParseRequest(byte[] data)
        {
            if (data == null || data.Length < 8)
            {
                return null;
            }
            if (data[2] != 0 || data[3] != 0)
            {
                return null;   // Modbus 协议标识必须为 0
            }

            var info = new ModbusRequestInfo
            {
                TransactionId = ReadUInt16(data, 0),
                UnitId = data[6],
                FunctionCode = data[7],
            };

            const int pdu = 8;   // PDU 数据起点（跳过 MBAP 7 + 功能码 1）
            switch (info.FunctionCode)
            {
                case 0x01:
                case 0x02:
                case 0x03:
                case 0x04:
                    if (data.Length < pdu + 4) return null;
                    info.StartAddress = ReadUInt16(data, pdu);
                    info.Quantity = ReadUInt16(data, pdu + 2);
                    return info;

                case 0x05:
                case 0x06:
                    if (data.Length < pdu + 4) return null;
                    info.StartAddress = ReadUInt16(data, pdu);
                    info.SingleValue = ReadUInt16(data, pdu + 2);
                    info.Quantity = 1;
                    return info;

                case 0x0F:
                case 0x10:
                    if (data.Length < pdu + 5) return null;
                    info.StartAddress = ReadUInt16(data, pdu);
                    info.Quantity = ReadUInt16(data, pdu + 2);
                    int byteCount = data[pdu + 4];
                    if (data.Length < pdu + 5 + byteCount) return null;
                    info.WriteData = new byte[byteCount];
                    Array.Copy(data, pdu + 5, info.WriteData, 0, byteCount);
                    return info;

                default:
                    return info;   // 不认识的功能码由上层回「非法功能」
            }
        }

        /// <summary>
        /// 读线圈响应：功能码 + 字节数 + 位打包数据（低位在前）。
        /// </summary>
        public byte[] BuildReadCoilsResponse(ModbusRequestInfo request, bool[] bits)
        {
            int byteCount = (bits.Length + 7) / 8;
            byte[] payload = new byte[byteCount];
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i])
                {
                    payload[i / 8] |= (byte)(1 << (i % 8));
                }
            }
            return BuildResponse(request, request.FunctionCode, new byte[] { (byte)byteCount }, payload);
        }

        /// <summary>
        /// 读寄存器响应：功能码 + 字节数 + 每寄存器两字节（高位在前）。
        /// </summary>
        public byte[] BuildReadRegistersResponse(ModbusRequestInfo request, short[] words)
        {
            byte[] payload = new byte[words.Length * 2];
            for (int i = 0; i < words.Length; i++)
            {
                payload[i * 2] = (byte)(words[i] >> 8);
                payload[i * 2 + 1] = (byte)words[i];
            }
            return BuildResponse(request, request.FunctionCode, new byte[] { (byte)payload.Length }, payload);
        }

        /// <summary>
        /// 写响应：0x05/0x06 回显「地址 + 值」，0x0F/0x10 回显「地址 + 数量」。
        /// </summary>
        public byte[] BuildWriteResponse(ModbusRequestInfo request)
        {
            ushort echo = request.IsMultipleWrite ? request.Quantity : request.SingleValue;
            byte[] payload =
            {
                (byte)(request.StartAddress >> 8), (byte)request.StartAddress,
                (byte)(echo >> 8), (byte)echo,
            };
            return BuildResponse(request, request.FunctionCode, null, payload);
        }

        /// <summary>
        /// 异常响应：功能码 | 0x80 + 异常码。
        /// </summary>
        public byte[] BuildExceptionResponse(ModbusRequestInfo request, byte exceptionCode)
        {
            return BuildResponse(request, (byte)(request.FunctionCode | 0x80), null, new[] { exceptionCode });
        }

        /// <summary>
        /// 组包：MBAP(7) + 功能码 + 可选字节数字段 + 数据。
        /// </summary>
        private static byte[] BuildResponse(ModbusRequestInfo request, byte functionCode, byte[] byteCountField, byte[] payload)
        {
            int pduLength = 1
                            + (byteCountField == null ? 0 : byteCountField.Length)
                            + (payload == null ? 0 : payload.Length);
            int lengthField = 1 + pduLength;   // UnitId(1) + PDU

            byte[] frame = new byte[6 + lengthField];
            frame[0] = (byte)(request.TransactionId >> 8);
            frame[1] = (byte)request.TransactionId;
            frame[2] = 0;
            frame[3] = 0;
            frame[4] = (byte)(lengthField >> 8);
            frame[5] = (byte)lengthField;
            frame[6] = request.UnitId;
            frame[7] = functionCode;

            int offset = 8;
            if (byteCountField != null && byteCountField.Length > 0)
            {
                Array.Copy(byteCountField, 0, frame, offset, byteCountField.Length);
                offset += byteCountField.Length;
            }
            if (payload != null && payload.Length > 0)
            {
                Array.Copy(payload, 0, frame, offset, payload.Length);
            }
            return frame;
        }

        private static ushort ReadUInt16(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }
    }
}
