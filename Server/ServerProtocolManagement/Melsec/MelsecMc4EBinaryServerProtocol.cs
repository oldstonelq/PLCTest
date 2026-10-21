using PlcTest.Models;
using PlcTest.Server.ServerInterface;
using System;

namespace PlcTest.Server.ServerProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱 MC 4E 二进制服务端协议实现。
    /// 请求：15 字节固定头（副标题 0x5400 + 串行号 + 固定 + 网络/PLC/IO/站 + 长度 + 定时器）
    ///       + 10 字节命令区（命令2 + 子命令2 + 地址3 + 设备码1 + 点数2）+ [写数据]；
    /// 响应：15 字节头（副标题 0xD400 … 长度 + 结束码）+ 数据。
    /// </summary>
    public class MelsecMc4EBinaryServerProtocol : IServerProtocol
    {
        /// <summary>
        /// 验证请求报文头（副标题 0x54 0x00）
        /// </summary>
        public bool ValidateRequestHeader(byte[] data)
        {
            if (data == null || data.Length < 15)
                return false;
            return data[0] == 0x54 && data[1] == 0x00;
        }

        /// <summary>
        /// 解析请求报文为结构化数据
        /// </summary>
        public RequestInfo ParseRequest(byte[] data)
        {
            var info = new RequestInfo();
            if (data == null || data.Length < 25)
            {
                info.Command = 0;
                return info;
            }

            // 请求数据长度（偏移 11-12，小端）：从 CPU 定时器起算 = 2 + 命令区
            ushort reqDataLen = BitConverter.ToUInt16(data, 11);

            // 命令（偏移 15-16）
            info.Command = BitConverter.ToUInt16(data, 15);

            // 起始地址（3 字节小端，偏移 19~21）
            info.StartAddress = data[19] | (data[20] << 8) | (data[21] << 16);

            // 设备码（偏移 22，单字节）
            info.DeviceCode = data[22];
            info.DeviceArea = DecodeDeviceArea(info.DeviceCode);

            // 点数（偏移 23-24，小端）
            info.Points = BitConverter.ToUInt16(data, 23);

            // 写入数据区：命令区长度 = reqDataLen - 2，固定占 10 字节
            if (info.Command == 0x1401)
            {
                int dataLength = Math.Max(0, reqDataLen - 12);
                int dataStart = 25;
                if (dataLength > 0 && data.Length >= dataStart + dataLength)
                {
                    info.WriteData = new byte[dataLength];
                    Array.Copy(data, dataStart, info.WriteData, 0, dataLength);
                }
            }

            return info;
        }

        /// <summary>
        /// 设备码（单字节）→ 内存区域
        /// </summary>
        private static MemoryArea? DecodeDeviceArea(int deviceCode)
        {
            switch (deviceCode)
            {
                case 0x91: return MemoryArea.SM;
                case 0xA9: return MemoryArea.SD;
                case 0x9C: return MemoryArea.X;
                case 0x9D: return MemoryArea.Y;
                case 0x90: return MemoryArea.M;
                case 0x92: return MemoryArea.L;
                case 0x93: return MemoryArea.F;
                case 0x94: return MemoryArea.V;
                case 0xA0: return MemoryArea.B;
                case 0xA8: return MemoryArea.D;
                case 0xB4: return MemoryArea.W;
                case 0xC1: return MemoryArea.TS;
                case 0xC0: return MemoryArea.TC;
                case 0xC2: return MemoryArea.TN;
                case 0xC7: return MemoryArea.SS;
                case 0xC6: return MemoryArea.SC;
                case 0xC8: return MemoryArea.SN;
                case 0xC4: return MemoryArea.CS;
                case 0xC3: return MemoryArea.CC;
                case 0xC5: return MemoryArea.CN;
                case 0xA1: return MemoryArea.SB;
                case 0xB5: return MemoryArea.SW;
                case 0x98: return MemoryArea.S;
                case 0xA2: return MemoryArea.DX;
                case 0xA3: return MemoryArea.DY;
                case 0xCC: return MemoryArea.Z;
                case 0xAF: return MemoryArea.R;
                case 0xB0: return MemoryArea.ZR;
                default: return null;
            }
        }

        /// <summary>
        /// 构建响应头（13 字节，不含结束码；结束码由 <see cref="BuildEndCodeResponse"/> 提供）
        /// </summary>
        public byte[] BuildResponseHeader(byte[] request, int responseDataLength)
        {
            byte[] header = new byte[13];
            // 副标题：响应标识 0xD400
            header[0] = 0xD4;
            header[1] = 0x00;
            // 回显串行号 / 固定值 / 网络号 / PLC号 / IO号 / 站号（请求偏移 2~10，共 9 字节）
            if (request != null && request.Length >= 11)
            {
                Array.Copy(request, 2, header, 2, 9);
            }
            // 响应数据长度（小端，已包含结束码 2 字节与数据）
            header[11] = (byte)(responseDataLength & 0xFF);
            header[12] = (byte)((responseDataLength >> 8) & 0xFF);
            return header;
        }

        /// <summary>
        /// 构建字设备读取响应数据（每个 short → 2 字节，小端）
        /// </summary>
        public byte[] BuildWordReadResponse(short[] words)
        {
            if (words == null || words.Length == 0)
                return new byte[0];

            byte[] data = new byte[words.Length * 2];
            Buffer.BlockCopy(words, 0, data, 0, data.Length);
            return data;
        }

        /// <summary>
        /// 构建位设备读取响应数据（每字节 2 点，高4位=偶数位，低4位=奇数位）
        /// </summary>
        public byte[] BuildBitReadResponse(bool[] bits)
        {
            if (bits == null || bits.Length == 0)
                return new byte[0];

            int byteCount = (bits.Length + 1) / 2;
            byte[] data = new byte[byteCount];

            for (int bIdx = 0; bIdx < byteCount; bIdx++)
            {
                int idx0 = bIdx * 2;
                if (idx0 < bits.Length && bits[idx0])
                    data[bIdx] |= 0x10;

                int idx1 = bIdx * 2 + 1;
                if (idx1 < bits.Length && bits[idx1])
                    data[bIdx] |= 0x01;
            }

            return data;
        }

        /// <summary>
        /// 构建结束码响应数据区（2 字节小端）
        /// </summary>
        public byte[] BuildEndCodeResponse(ushort endCode)
        {
            return new byte[2]
            {
                (byte)(endCode & 0xFF),
                (byte)((endCode >> 8) & 0xFF)
            };
        }

        /// <summary>
        /// 从写入数据区解析位数据（每字节 2 点）
        /// </summary>
        public bool[] ParseWriteBits(byte[] data, int points)
        {
            if (data == null || points <= 0)
                return new bool[0];

            bool[] bits = new bool[points];
            int byteCount = (points + 1) / 2;
            for (int bIdx = 0; bIdx < byteCount && bIdx < data.Length; bIdx++)
            {
                int idx0 = bIdx * 2;
                if (idx0 < points)
                    bits[idx0] = (data[bIdx] & 0x10) != 0;

                int idx1 = bIdx * 2 + 1;
                if (idx1 < points)
                    bits[idx1] = (data[bIdx] & 0x01) != 0;
            }
            return bits;
        }

        /// <summary>
        /// 从写入数据区解析字数据（每字 2 字节小端）
        /// </summary>
        public short[] ParseWriteWords(byte[] data, int points)
        {
            if (data == null || points <= 0)
                return new short[0];

            short[] words = new short[points];
            for (int i = 0; i < points; i++)
            {
                int off = i * 2;
                if (data.Length >= off + 2)
                    words[i] = BitConverter.ToInt16(data, off);
            }
            return words;
        }
    }
}
