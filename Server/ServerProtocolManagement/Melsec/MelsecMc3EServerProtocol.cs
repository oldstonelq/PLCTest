using PlcTest.Models;
using PlcTest.Server.ServerInterface;
using System;

namespace PlcTest.Server.ServerProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱 MC 3E 二进制服务端协议实现——负责请求解析和响应构建
    /// </summary>
    public class MelsecMc3EServerProtocol : IServerProtocol
    {
        /// <summary>
        /// 验证请求报文头
        /// </summary>
        public bool ValidateRequestHeader(byte[] data)
        {
            if (data == null || data.Length < 11)
                return false;
            if (data[0] != 0x50 || data[1] != 0x00)
                return false;
            return true;
        }

        /// <summary>
        /// 解析请求报文为结构化数据
        /// </summary>
        public RequestInfo ParseRequest(byte[] data)
        {
            var info = new RequestInfo();
            int len = data.Length;

            // 请求数据长度（偏移 7-8）
            ushort reqDataLen = len >= 10 ? BitConverter.ToUInt16(data, 7) : (ushort)0;

            // 命令码在偏移 11（跳过副标题6 + 网络号1 + PC号1 + IO号2 + 站号1 = 11）
            int cmdOffset = 11;
            if (len >= cmdOffset + 4)
            {
                info.Command = BitConverter.ToUInt16(data, cmdOffset);
            }

            // 请求数据区从偏移 15 开始（cmdOffset + 4）
            int requestDataOffset = cmdOffset + 4;
            int requestDataLen = Math.Max(0, reqDataLen - 4);

            if (requestDataLen < 6 || len < requestDataOffset + 6)
            {
                info.Command = 0; // 无效数据
                return info;
            }

            // 起始地址（3 字节，24 位小端）
            int addrOffset = requestDataOffset;
            info.StartAddress = data[addrOffset] | (data[addrOffset + 1] << 8) | (data[addrOffset + 2] << 16);

            // 设备代码
            info.DeviceCode = data[addrOffset + 3];

            // 设备代码 → 统一内存区域：把 MC 3E 的设备码语义收敛在协议层，
            // 上层的服务端逻辑不再出现 0xA8/0x90 这类协议魔法数
            switch (info.DeviceCode)
            {
                case 0xA8: info.DeviceArea = MemoryArea.D; break;
                case 0x90: info.DeviceArea = MemoryArea.M; break;
                case 0x9C: info.DeviceArea = MemoryArea.X; break;
                case 0x9D: info.DeviceArea = MemoryArea.Y; break;
            }

            // 软元件点数
            info.Points = BitConverter.ToUInt16(data, addrOffset + 4);

            // 写入请求时提取数据区
            if (info.Command == 0x1401)
            {
                int dataStart = addrOffset + 6;
                // requestDataLen 从偏移 13 起算（含 sub-command 2字节），
                // dataStart=21，间距 = subcmd(2) + addr(3) + device(1) + points(2) = 8
                int dataLength = requestDataLen - 8;
                if (dataLength > 0 && len >= dataStart + dataLength)
                {
                    info.WriteData = new byte[dataLength];
                    Array.Copy(data, dataStart, info.WriteData, 0, dataLength);
                }
            }

            return info;
        }

        /// <summary>
        /// 构建 MC 3E 响应头（9 字节，不含结束码；结束码由 <see cref="BuildEndCodeResponse"/> 提供）
        /// </summary>
        public byte[] BuildResponseHeader(byte[] request, int responseDataLength)
        {
            byte[] header = new byte[9];
            // 副标题：响应标识
            header[0] = 0xD0;
            header[1] = 0x00;
            // 从请求中复制网络号 / PC号 / IO号 / 站号（偏移 2~6，共 5 字节）
            if (request != null && request.Length >= 7)
                Array.Copy(request, 2, header, 2, 5);
            // 响应数据长度（低字节在前，已包含结束码 2 字节与数据）
            header[7] = (byte)(responseDataLength & 0xFF);
            header[8] = (byte)((responseDataLength >> 8) & 0xFF);
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
        /// 构建位设备读取响应数据（每字节存 2 个位，高4位对应偶数位，低4位对应奇数位）
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
        /// 构建结束码响应数据区（3E 二进制：2 字节小端）
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
        /// 从写入数据区解析位数据（每字节 2 点，高4位=偶数位，低4位=奇数位）
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
