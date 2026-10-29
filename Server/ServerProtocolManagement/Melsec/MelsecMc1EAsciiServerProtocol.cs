using PlcTest.Models;
using PlcTest.Server.ServerProtocolManagement;
using PlcTest.Server.ServerInterface;
using System;
using System.Text;

namespace PlcTest.Server.ServerProtocolManagement.Melsec
{
    /// <summary>
    /// 三菱 MC 1E ASCII 服务端协议实现。
    /// 请求帧（24 字符固定头）：功能码(2) + PLC编号(2) + CPU监视定时器(4) + 软元件代码(4 位十六进制文本) + 起始地址(8) + 点数(4) + [写数据]；
    /// 响应帧：副标题(2) + 结束码(2) + 数据。
    /// </summary>
    public class MelsecMc1EAsciiServerProtocol : IServerProtocol
    {
        /// <summary>
        /// 验证请求报文头（1E ASCII 无外层头，只校验长度）
        /// </summary>
        /// <summary>
        /// 1E ASCII 没有长度字段：读请求固定 24 字符；写请求交给空闲判据收帧。
        /// </summary>
        public int GetRequestFrameLength(byte[] buffer, int available)
        {
            if (buffer == null || available < 2)
            {
                return 0;
            }

            // [0] 站号，[1] 功能码：'0'/'1' 为批量读（固定 24 字符），'2'/'3' 为批量写
            byte func = buffer[1];
            if (func == (byte)'0' || func == (byte)'1')
            {
                return available >= 24 ? 24 : 0;
            }
            return -1;
        }

        public bool ValidateRequestHeader(byte[] data)
        {
            return data != null && data.Length >= 24;
        }

        /// <summary>
        /// 解析请求报文为结构化数据
        /// </summary>
        public RequestInfo ParseRequest(byte[] data)
        {
            var info = new RequestInfo();
            if (data == null || data.Length < 24)
            {
                info.Command = 0;
                return info;
            }

            // 功能码：00=批量位读 01=批量字读 02=批量位写 03=批量字写
            string command = Encoding.ASCII.GetString(data, 0, 2);
            if (command == "00") info.Command = 0x0401;
            else if (command == "01") info.Command = 0x0401;
            else if (command == "02") info.Command = 0x1401;
            else if (command == "03") info.Command = 0x1401;

            // 功能码：'0'/'2' 是位读写，'1'/'3' 是字读写
            info.IsBit = command == "00" || command == "02";

            // 软元件代码（4 位十六进制文本，如 0x4420 → "4420"）
            string codeText = Encoding.ASCII.GetString(data, 8, 4);
            if (!int.TryParse(codeText, System.Globalization.NumberStyles.HexNumber, null, out int deviceCode))
            {
                info.Command = 0;
                return info;
            }
            info.DeviceCode = deviceCode;
            info.DeviceArea = DecodeDeviceArea(deviceCode);

            // 起始地址：8 位十六进制（1E 帧的编号统一按十六进制，与客户端 ToString("X8") 对应）
            string addr = Encoding.ASCII.GetString(data, 12, 8);
            if (!int.TryParse(addr, System.Globalization.NumberStyles.HexNumber, null, out int startAddress))
            {
                info.Command = 0;
                return info;
            }
            info.StartAddress = startAddress;

            // 点数：4 位十六进制（请求方做了高低字节调换，这里换回来）
            string pointText = Encoding.ASCII.GetString(data, 20, 4);
            if (!ushort.TryParse(pointText, System.Globalization.NumberStyles.HexNumber, null, out ushort bigEndian))
            {
                info.Command = 0;
                return info;
            }
            info.Points = (ushort)((bigEndian << 8) | (bigEndian >> 8));

            // 写入数据区
            if (info.Command == 0x1401 && data.Length > 24)
            {
                info.WriteData = new byte[data.Length - 24];
                Array.Copy(data, 24, info.WriteData, 0, info.WriteData.Length);
            }

            return info;
        }

        /// <summary>
        /// 1E 双字符设备码（12 种）
        /// </summary>
        private static MemoryArea? DecodeDeviceArea(int deviceCode)
        {
            return MelsecServerDeviceMapper.From1E(deviceCode);
        }

        /// <summary>
        /// 构建响应头（2 字符副标题，不含结束码；结束码由 <see cref="BuildEndCodeResponse"/> 提供）
        /// </summary>
        public byte[] BuildResponseHeader(byte[] request, int responseDataLength)
        {
            // 1E ASCII 响应只有副标题 2 字符，回显功能码
            return new byte[2]
            {
                (byte)'0',
                (byte)'0'
            };
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
        /// 构建结束码响应数据区（2 个十六进制字符）
        /// </summary>
        public byte[] BuildEndCodeResponse(ushort endCode)
        {
            string text = (endCode & 0xFF).ToString("X2");
            return Encoding.ASCII.GetBytes(text);
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
