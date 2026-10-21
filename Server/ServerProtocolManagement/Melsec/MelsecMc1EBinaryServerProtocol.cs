using PlcTest.Models;
using PlcTest.Server.ServerInterface;
using System;

namespace PlcTest.Server.ServerProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱 MC 1E 二进制服务端协议实现。
    /// 请求帧（12 字节固定头）：功能码(1) + PLC号(1) + 定时器(2) + 地址(4) + 设备码(2) + 点数(2) + [写数据]；
    /// 响应帧：副标题(1) + 结束码(2) + 数据。
    /// </summary>
    public class MelsecMc1EBinaryServerProtocol : IServerProtocol
    {
        /// <summary>
        /// 验证请求报文头（1E 无外层头，只校验长度与功能码）
        /// </summary>
        public bool ValidateRequestHeader(byte[] data)
        {
            if (data == null || data.Length < 12)
                return false;
            // 功能码 0x00/0x01 读、0x02/0x03 写
            return data[0] <= 0x03;
        }

        /// <summary>
        /// 解析请求报文为结构化数据
        /// </summary>
        public RequestInfo ParseRequest(byte[] data)
        {
            var info = new RequestInfo();
            if (data == null || data.Length < 12)
            {
                info.Command = 0;
                return info;
            }

            // 功能码：0/1 读，2/3 写（位/字的区别由软元件区域决定）
            info.Command = data[0] <= 0x01 ? (ushort)0x0401 : (ushort)0x1401;

            // 起始地址（4 字节小端，24 位有效）
            info.StartAddress = data[4] | (data[5] << 8) | (data[6] << 16);

            // 设备码：2 字节小端（低字节先），高字节为第 1 个 ASCII 字符
            info.DeviceCode = data[8] | (data[9] << 8);
            info.DeviceArea = DecodeDeviceArea(info.DeviceCode);

            // 点数（2 字节小端）
            info.Points = BitConverter.ToUInt16(data, 10);

            // 写入数据区
            if (info.Command == 0x1401 && data.Length > 12)
            {
                info.WriteData = new byte[data.Length - 12];
                Array.Copy(data, 12, info.WriteData, 0, info.WriteData.Length);
            }

            return info;
        }

        /// <summary>
        /// 设备码（双字符）→ 内存区域
        /// </summary>
        private static MemoryArea? DecodeDeviceArea(int deviceCode)
        {
            switch (deviceCode)
            {
                case 0x4420: return MemoryArea.D;
                case 0x5220: return MemoryArea.R;
                case 0x544E: return MemoryArea.TN;
                case 0x5453: return MemoryArea.TS;
                case 0x434E: return MemoryArea.CN;
                case 0x4353: return MemoryArea.CS;
                case 0x5820: return MemoryArea.X;
                case 0x5920: return MemoryArea.Y;
                case 0x4D20: return MemoryArea.M;
                case 0x5320: return MemoryArea.S;
                default: return null;
            }
        }

        /// <summary>
        /// 构建响应头（1 字节副标题，不含结束码；结束码由 <see cref="BuildEndCodeResponse"/> 提供）
        /// </summary>
        public byte[] BuildResponseHeader(byte[] request, int responseDataLength)
        {
            // 副标题：读响应 0x80，写响应 0x81
            byte subHeader = (request != null && request.Length > 0 && request[0] <= 0x01) ? (byte)0x80 : (byte)0x81;
            return new byte[1] { subHeader };
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
