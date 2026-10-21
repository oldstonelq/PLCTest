using PlcTest.Models;
using PlcTest.Server.ServerInterface;
using System;
using System.Text;

namespace PlcTest.Server.ServerProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱 MC 4E ASCII 服务端协议实现。
    /// 请求：22 字符头 + 20 字符命令区（命令4 + 子命令4 + 设备码2 + 地址6 + 点数4）+ [写数据]；
    /// 响应：18 字符头 + 结束码 4 字符 + 数据。与 3E ASCII 的差异仅在副标题（"4E00"/"D400"）。
    /// </summary>
    public class MelsecMc4EAsciiServerProtocol : IServerProtocol
    {
        /// <summary>
        /// 验证请求报文头（副标题 "4E00"）
        /// </summary>
        public bool ValidateRequestHeader(byte[] data)
        {
            if (data == null || data.Length < 22)
                return false;
            return data[0] == (byte)'4' && data[1] == (byte)'E'
                && data[2] == (byte)'0' && data[3] == (byte)'0';
        }

        /// <summary>
        /// 解析请求报文为结构化数据
        /// </summary>
        public RequestInfo ParseRequest(byte[] data)
        {
            var info = new RequestInfo();
            if (data == null || data.Length < 42)
            {
                info.Command = 0;
                return info;
            }

            // 命令区从偏移 22 开始：命令(4) + 子命令(4)
            string command = Encoding.ASCII.GetString(data, 22, 4);
            info.Command = command == "1401" ? (ushort)0x1401 : (ushort)0x0401;

            // 软元件代码（2 个 ASCII 字符）
            info.DeviceCode = (data[30] << 8) | data[31];
            info.DeviceArea = DecodeDeviceArea(info.DeviceCode);

            // 起始地址：6 位十进制
            string addr = Encoding.ASCII.GetString(data, 32, 6);
            if (!int.TryParse(addr, out int startAddress))
            {
                info.Command = 0;
                return info;
            }
            info.StartAddress = startAddress;

            // 点数：4 位十六进制
            string pointText = Encoding.ASCII.GetString(data, 38, 4);
            if (!ushort.TryParse(pointText, System.Globalization.NumberStyles.HexNumber, null, out ushort points))
            {
                info.Command = 0;
                return info;
            }
            info.Points = points;

            // 写入数据区
            if (info.Command == 0x1401 && data.Length > 42)
            {
                info.WriteData = new byte[data.Length - 42];
                Array.Copy(data, 42, info.WriteData, 0, info.WriteData.Length);
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
                case 0x534D: return MemoryArea.SM;
                case 0x5344: return MemoryArea.SD;
                case 0x582A: return MemoryArea.X;
                case 0x592A: return MemoryArea.Y;
                case 0x4D2A: return MemoryArea.M;
                case 0x4C2A: return MemoryArea.L;
                case 0x462A: return MemoryArea.F;
                case 0x562A: return MemoryArea.V;
                case 0x422A: return MemoryArea.B;
                case 0x442A: return MemoryArea.D;
                case 0x572A: return MemoryArea.W;
                case 0x5453: return MemoryArea.TS;
                case 0x5443: return MemoryArea.TC;
                case 0x544E: return MemoryArea.TN;
                case 0x5353: return MemoryArea.SS;
                case 0x5343: return MemoryArea.SC;
                case 0x534E: return MemoryArea.SN;
                case 0x4353: return MemoryArea.CS;
                case 0x4343: return MemoryArea.CC;
                case 0x434E: return MemoryArea.CN;
                case 0x5342: return MemoryArea.SB;
                case 0x5357: return MemoryArea.SW;
                case 0x532A: return MemoryArea.S;
                case 0x4458: return MemoryArea.DX;
                case 0x4459: return MemoryArea.DY;
                case 0x5A2A: return MemoryArea.Z;
                case 0x522A: return MemoryArea.R;
                case 0x5A52: return MemoryArea.ZR;
                default: return null;
            }
        }

        /// <summary>
        /// 构建响应头（18 字符，不含结束码；结束码由 <see cref="BuildEndCodeResponse"/> 提供）
        /// </summary>
        public byte[] BuildResponseHeader(byte[] request, int responseDataLength)
        {
            byte[] header = new byte[18];
            // 副标题 "D400"
            header[0] = (byte)'D';
            header[1] = (byte)'4';
            header[2] = (byte)'0';
            header[3] = (byte)'0';
            // 回显网络号 / PLC 编号 / 目标模块 IO 编号 / 站号
            if (request != null && request.Length >= 14)
            {
                Array.Copy(request, 4, header, 4, 10);
            }
            // 数据长度：已包含结束码 4 字符与数据字符数
            byte[] lenBytes = Encoding.ASCII.GetBytes(responseDataLength.ToString("X4"));
            Array.Copy(lenBytes, 0, header, 14, 4);
            return header;
        }

        /// <summary>
        /// 构建字设备读取响应数据（每字 4 个十六进制字符）
        /// </summary>
        public byte[] BuildWordReadResponse(short[] words)
        {
            if (words == null || words.Length == 0)
                return new byte[0];

            byte[] data = new byte[words.Length * 4];
            for (int i = 0; i < words.Length; i++)
            {
                string text = ((ushort)words[i]).ToString("X4");
                byte[] bytes = Encoding.ASCII.GetBytes(text);
                Array.Copy(bytes, 0, data, i * 4, 4);
            }
            return data;
        }

        /// <summary>
        /// 构建位设备读取响应数据（每点 1 个字符 '0'/'1'）
        /// </summary>
        public byte[] BuildBitReadResponse(bool[] bits)
        {
            if (bits == null || bits.Length == 0)
                return new byte[0];

            byte[] data = new byte[bits.Length];
            for (int i = 0; i < bits.Length; i++)
                data[i] = bits[i] ? (byte)'1' : (byte)'0';
            return data;
        }

        /// <summary>
        /// 构建结束码响应数据区（4 个十六进制字符）
        /// </summary>
        public byte[] BuildEndCodeResponse(ushort endCode)
        {
            return Encoding.ASCII.GetBytes(endCode.ToString("X4"));
        }

        /// <summary>
        /// 从写入数据区解析位数据（每点 1 个字符 '0'/'1'）
        /// </summary>
        public bool[] ParseWriteBits(byte[] data, int points)
        {
            if (data == null || points <= 0)
                return new bool[0];

            bool[] bits = new bool[points];
            for (int i = 0; i < points && i < data.Length; i++)
                bits[i] = data[i] == (byte)'1';
            return bits;
        }

        /// <summary>
        /// 从写入数据区解析字数据（每字 4 个十六进制字符）
        /// </summary>
        public short[] ParseWriteWords(byte[] data, int points)
        {
            if (data == null || points <= 0)
                return new short[0];

            short[] words = new short[points];
            for (int i = 0; i < points; i++)
            {
                int off = i * 4;
                if (data.Length < off + 4)
                    break;
                string text = Encoding.ASCII.GetString(data, off, 4);
                if (ushort.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out ushort value))
                    words[i] = unchecked((short)value);
            }
            return words;
        }
    }
}
