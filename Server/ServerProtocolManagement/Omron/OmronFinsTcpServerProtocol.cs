using PlcTest.Server.ServerInterface;
using System;
using System.Text;

namespace PlcTest.Server.ServerProtocolManagement.Omron
{
    /// <summary>
    /// 欧姆龙 FINS/TCP 从站协议。
    /// 两层结构：外层 FINS/TCP（16 字节）+ 内层 FINS 帧（10 字节头 + 指令代码 + 结束代码 + 数据）。
    /// 依据 OMRON W342 手册；外层封装与节点地址握手参考 FINS/TCP 网关约定。
    /// </summary>
    public class OmronFinsTcpServerProtocol : IOmronFinsServerProtocol
    {
        /// <summary>外层头长度</summary>
        public const int OuterHeaderLength = 16;
        /// <summary>FINS 帧头长度</summary>
        public const int FinsHeaderLength = 10;
        /// <summary>读 / 写命令体长度：区码(1) + 字地址(2) + 位号(1) + 点数(2)</summary>
        public const int CommandBodyLength = 6;
        /// <summary>读写请求的最小长度（外层 + 帧头 + 指令码 + 命令体）</summary>
        public const int MinRequestLength = OuterHeaderLength + FinsHeaderLength + 2 + CommandBodyLength;
        /// <summary>响应固定头长度（外层 + 帧头 + 指令码 + 结束代码）</summary>
        public const int ResponseHeaderLength = OuterHeaderLength + FinsHeaderLength + 2 + 2;
        /// <summary>握手请求 / 响应长度</summary>
        public const int HandshakeRequestLength = 20;
        public const int HandshakeResponseLength = 24;

        private const uint CmdNodeAddressRequest = 0x00000000;
        private const uint CmdFinsFrame = 0x00000002;

        /// <summary>
        /// 外层 [4..7] 大端为「帧总长 - 8」。
        /// </summary>
        public int GetRequestFrameLength(byte[] buffer, int available)
        {
            if (buffer == null || available < 8)
            {
                return 0;
            }
            if (buffer[0] != 'F' || buffer[1] != 'I' || buffer[2] != 'N' || buffer[3] != 'S')
            {
                return -1;
            }

            uint length = ((uint)buffer[4] << 24) | ((uint)buffer[5] << 16)
                          | ((uint)buffer[6] << 8) | buffer[7];
            if (length == 0 || length > 0xFFFF)
            {
                return -1;
            }
            return (int)(8 + length);
        }

        /// <summary>
        /// 外层命令字为 0 即节点地址请求。
        /// </summary>
        public bool IsHandshakeRequest(byte[] data)
        {
            if (data == null || data.Length < OuterHeaderLength)
            {
                return false;
            }
            if (data[0] != 'F' || data[1] != 'I' || data[2] != 'N' || data[3] != 'S')
            {
                return false;
            }
            uint command = ReadUInt32BigEndian(data, 8);
            return command == CmdNodeAddressRequest;
        }

        /// <summary>
        /// 握手响应：外层 16 字节 + 客户端节点(4) + PLC 节点(4)，错误码为 0。
        /// </summary>
        public byte[] BuildHandshakeResponse(byte localNode, byte remoteNode)
        {
            byte[] response = new byte[HandshakeResponseLength];
            response[0] = (byte)'F';
            response[1] = (byte)'I';
            response[2] = (byte)'N';
            response[3] = (byte)'S';
            // 长度 = 24 - 8 = 16 = 0x10
            response[7] = (byte)(HandshakeResponseLength - 8);
            // 命令字 0：节点地址请求的响应
            response[11] = (byte)CmdNodeAddressRequest;
            // [12..15] 错误码 0
            // [16..19] 客户端节点（大端 4 字节，最后一个字节是节点号）
            response[19] = remoteNode;
            // [20..23] PLC 节点
            response[23] = localNode;
            return response;
        }

        /// <summary>
        /// 解析读写请求。
        /// </summary>
        public OmronFinsRequestInfo ParseRequest(byte[] data)
        {
            if (data == null || data.Length < MinRequestLength)
            {
                return null;
            }
            if (data[0] != 'F' || data[1] != 'I' || data[2] != 'N' || data[3] != 'S')
            {
                return null;
            }
            if (ReadUInt32BigEndian(data, 8) != CmdFinsFrame)
            {
                return null;
            }

            int headerOffset = OuterHeaderLength;
            byte[] finsHeader = new byte[FinsHeaderLength];
            Array.Copy(data, headerOffset, finsHeader, 0, FinsHeaderLength);

            int commandOffset = headerOffset + FinsHeaderLength;
            ushort commandCode = (ushort)((data[commandOffset] << 8) | data[commandOffset + 1]);

            int bodyOffset = commandOffset + 2;
            var info = new OmronFinsRequestInfo
            {
                FinsHeader = finsHeader,
                CommandCode = commandCode,
                AreaCode = data[bodyOffset],
                WordAddress = (data[bodyOffset + 1] << 8) | data[bodyOffset + 2],
                BitIndex = data[bodyOffset + 3],
                Points = (ushort)((data[bodyOffset + 4] << 8) | data[bodyOffset + 5]),
            };

            if (commandCode == 0x0102)
            {
                info.IsWrite = true;
                int dataOffset = bodyOffset + CommandBodyLength;
                int expected = info.Points * (IsBitAreaCode(info.AreaCode) ? 1 : 2);
                if (data.Length >= dataOffset + expected && expected > 0)
                {
                    info.WriteData = new byte[expected];
                    Array.Copy(data, dataOffset, info.WriteData, 0, expected);
                }
            }
            else if (commandCode != 0x0101)
            {
                return info;   // 不认识的功能码，交给上层回结束码
            }

            return info;
        }

        /// <summary>
        /// 读位响应：每点 1 字节（01 / 00）。
        /// </summary>
        public byte[] BuildReadBitResponse(OmronFinsRequestInfo request, bool[] bits)
        {
            byte[] payload = new byte[bits.Length];
            for (int i = 0; i < bits.Length; i++)
            {
                payload[i] = bits[i] ? (byte)0x01 : (byte)0x00;
            }
            return BuildResponse(request, 0x0000, payload);
        }

        /// <summary>
        /// 读字响应：每字 2 字节，低字节在前。
        /// </summary>
        public byte[] BuildReadWordResponse(OmronFinsRequestInfo request, short[] words)
        {
            byte[] payload = new byte[words.Length * 2];
            for (int i = 0; i < words.Length; i++)
            {
                payload[i * 2] = (byte)words[i];
                payload[i * 2 + 1] = (byte)(words[i] >> 8);
            }
            return BuildResponse(request, 0x0000, payload);
        }

        /// <summary>
        /// 写响应：只有结束代码。
        /// </summary>
        public byte[] BuildWriteResponse(OmronFinsRequestInfo request)
        {
            return BuildResponse(request, 0x0000, new byte[0]);
        }

        /// <summary>
        /// 失败响应：只改结束代码。
        /// </summary>
        public byte[] BuildErrorResponse(OmronFinsRequestInfo request, ushort endCode)
        {
            return BuildResponse(request, endCode, new byte[0]);
        }

        /// <summary>
        /// 组包：外层 16 + 帧头 10（交换源/目标地址、ICF=C0）+ 指令代码回显 + 结束代码 + 数据。
        /// </summary>
        private static byte[] BuildResponse(OmronFinsRequestInfo request, ushort endCode, byte[] payload)
        {
            int total = ResponseHeaderLength + (payload == null ? 0 : payload.Length);
            byte[] frame = new byte[total];

            // 外层
            frame[0] = (byte)'F';
            frame[1] = (byte)'I';
            frame[2] = (byte)'N';
            frame[3] = (byte)'S';
            int length = total - 8;
            frame[4] = (byte)(length >> 24);
            frame[5] = (byte)(length >> 16);
            frame[6] = (byte)(length >> 8);
            frame[7] = (byte)length;
            frame[11] = (byte)CmdFinsFrame;

            // FINS 帧头：ICF 置 C0，源/目标地址互换（手册：响应的 DNA 回填请求的 SNA，反之亦然）
            byte[] req = request.FinsHeader;
            int h = OuterHeaderLength;
            frame[h + 0] = 0xC0;      // ICF：响应
            frame[h + 1] = 0x00;      // RSV
            frame[h + 2] = req[2];    // GCT 回显
            frame[h + 3] = req[6];    // DNA ← 请求的 SNA
            frame[h + 4] = req[7];    // DA1 ← 请求的 SA1
            frame[h + 5] = req[8];    // DA2 ← 请求的 SA2
            frame[h + 6] = req[3];    // SNA ← 请求的 DNA
            frame[h + 7] = req[4];    // SA1 ← 请求的 DA1
            frame[h + 8] = req[5];    // SA2 ← 请求的 DA2
            frame[h + 9] = req[9];    // SID 回显

            // 指令代码回显
            int c = h + FinsHeaderLength;
            frame[c] = (byte)(request.CommandCode >> 8);
            frame[c + 1] = (byte)request.CommandCode;

            // 结束代码
            frame[c + 2] = (byte)(endCode >> 8);
            frame[c + 3] = (byte)endCode;

            if (payload != null && payload.Length > 0)
            {
                Array.Copy(payload, 0, frame, ResponseHeaderLength, payload.Length);
            }
            return frame;
        }

        /// <summary>
        /// 区码是否为位访问：TIM/CNT 用 09、EM 用 0x20~0x2F / 0xE0~0xE8，其余靠 catalog 判定。
        /// </summary>
        private static bool IsBitAreaCode(byte areaCode)
        {
            if (areaCode == 0x09) return true;
            if (areaCode == 0x89) return false;
            if ((areaCode >= 0x20 && areaCode <= 0x2F) || (areaCode >= 0xE0 && areaCode <= 0xE8)) return true;
            if ((areaCode >= 0xA0 && areaCode <= 0xAF) || (areaCode >= 0x60 && areaCode <= 0x68)) return false;
            return false;
        }

        private static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
                   | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }
    }
}
