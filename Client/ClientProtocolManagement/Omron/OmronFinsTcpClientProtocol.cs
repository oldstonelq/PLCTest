using PlcTest.Client.ClientInterface;
using PlcTest.Models;
using System;
using System.Text;

namespace PlcTest.Client.ClientProtocolManagement.Omron
{
    /// <summary>
    /// 欧姆龙 FINS/TCP 客户端协议。
    /// 两层结构：外层 FINS/TCP 外壳（16 字节）+ 内层 FINS 帧（10 字节头 + 指令代码 + 参数）。
    /// 依据 OMRON W342《FINS 通信指令手册》第 5 章；外层封装参考 FINS/TCP 网关约定。
    /// </summary>
    public class OmronFinsTcpClientProtocol : IPlcClientProtocol, IDataFormatConfigurable
    {
        /// <summary>外层 FINS/TCP 头长度</summary>
        public const int OuterHeaderLength = 16;
        /// <summary>内层 FINS 帧头长度</summary>
        public const int FinsHeaderLength = 10;
        /// <summary>指令代码长度</summary>
        public const int CommandLength = 2;
        /// <summary>结束代码长度</summary>
        public const int EndCodeLength = 2;
        /// <summary>响应固定头长度（外层 + 帧头 + 指令代码 + 结束代码）</summary>
        public const int ResponseHeaderLength = OuterHeaderLength + FinsHeaderLength + CommandLength + EndCodeLength;

        /// <summary>单次读写允许的最大点数（CS/CJ 系列单帧上限 999）</summary>
        public const int MaxPointsPerRequest = 999;

        private const uint CommandNodeAddressRequest = 0x00000002 - 2;   // 0x00000000
        private const uint CommandFinsFrame = 0x00000002;

        /// <summary>PLC 节点号（DM 目标节点），握手后回填</summary>
        public byte Da1 { get; set; }
        /// <summary>本机节点号（源节点），握手后回填</summary>
        public byte Sa1 { get; set; }
        /// <summary>目标网络号</summary>
        public byte Dna { get; set; }
        /// <summary>源网络号</summary>
        public byte Sna { get; set; }
        /// <summary>目标单元地址</summary>
        public byte Da2 { get; set; }
        /// <summary>源单元地址</summary>
        public byte Sa2 { get; set; }
        /// <summary>
        /// 32 位数据（占两个连续字）的字节序与字序。
        /// 欧姆龙用 DMOV 之类的指令把 32 位数写到 D 区时是**低字在前**，即 <see cref="DataFormat.CDAB"/>。
        /// </summary>
        public DataFormat DataFormat { get; set; } = DataFormat.CDAB;
        /// <summary>是否已完成节点地址握手</summary>
        public bool IsHandshaked { get; set; }

        #region 区域能力

        /// <summary>
        /// 返回该区的字区码。FINS 的位/字是两个区码，
        /// 按位访问时另取 <see cref="OmronFinsDeviceSpec.BitCode"/>。
        /// </summary>
        public int GetMemoryCode(MemoryArea memoryArea)
        {
            OmronFinsDeviceSpec spec = OmronFinsDeviceCatalog.Get(memoryArea);
            return spec.SupportsWord ? spec.WordCode : spec.BitCode;
        }

        /// <summary>
        /// 该区允许的访问方式。
        /// </summary>
        public AreaAccess GetAreaAccess(MemoryArea memoryArea)
        {
            OmronFinsDeviceSpec spec;
            if (!OmronFinsDeviceCatalog.Contains(memoryArea))
            {
                throw new ArgumentOutOfRangeException(nameof(memoryArea), memoryArea, "欧姆龙 FINS 不支持该软元件");
            }
            spec = OmronFinsDeviceCatalog.Get(memoryArea);

            if (spec.SupportsBit && spec.SupportsWord) return AreaAccess.Both;
            if (spec.SupportsBit) return AreaAccess.Bit;
            return AreaAccess.Word;
        }

        /// <summary>
        /// 编号上限。FINS 的位地址是「字编号 × 16 + 位号」，比字编号大得多，
        /// 这里统一返回位上限——按字访问时字编号必然小于它，不会被误拦。
        /// </summary>
        public int GetMaxIndex(MemoryArea memoryArea)
        {
            OmronFinsDeviceSpec spec = OmronFinsDeviceCatalog.Get(memoryArea);
            return spec.SupportsBit ? spec.MaxBitIndex : spec.MaxWordIndex;
        }

        #endregion

        #region 帧长

        /// <summary>
        /// 外层 [4..7] 大端为「帧总长 - 8」，据此推断整帧长度。
        /// </summary>
        public int GetResponseFrameLength(byte[] request, byte[] buffer, int available)
        {
            if (buffer == null || available < 8)
            {
                return -1;
            }

            uint length = ReadUInt32BigEndian(buffer, 4);
            if (length == 0 || length > 0xFFFF)
            {
                return -1;
            }
            return (int)(8 + length);
        }

        #endregion

        #region 数据打包

        /// <summary>
        /// 位数据打包：每点 1 字节，ON = 0x01、OFF = 0x00。
        /// </summary>
        public byte[] GetWriteBitData(bool[] bools)
        {
            if (bools == null) return new byte[0];
            byte[] data = new byte[bools.Length];
            for (int i = 0; i < bools.Length; i++)
            {
                data[i] = bools[i] ? (byte)0x01 : (byte)0x00;
            }
            return data;
        }

        /// <summary>
        /// 字数据打包：每字 2 字节，低字节在前。
        /// </summary>
        public byte[] GetWriteWordData(short[] shorts)
        {
            if (shorts == null) return new byte[0];
            byte[] data = new byte[shorts.Length * 2];
            for (int i = 0; i < shorts.Length; i++)
            {
                data[i * 2] = (byte)shorts[i];
                data[i * 2 + 1] = (byte)(shorts[i] >> 8);
            }
            return data;
        }

        #endregion

        #region 命令构建

        /// <summary>
        /// 构建读取命令：01 01 + 区码 + 起始地址(3) + 读取数量(2，大端)。
        /// </summary>
        public byte[] BuildReadData(PlcAddress address)
        {
            byte code = ResolveAreaCode(address);
            int wordAddress = ResolveWordAddress(address);

            return BuildCommand(0x01, 0x01, code, wordAddress, address.Length);
        }

        /// <summary>
        /// 构建写入命令：01 02 + 区码 + 起始地址(3) + 写入数量(2) + 数据。
        /// </summary>
        public byte[] BuildWriteData(PlcAddress address, byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            byte code = ResolveAreaCode(address);
            int wordAddress = ResolveWordAddress(address);
            // 位写每点 1 字节、字写每点 2 字节
            int expected = address.IsBit ? address.Length : address.Length * 2;
            if (data.Length < expected)
            {
                throw new ArgumentException($"写入数据 {data.Length} 字节，少于 {expected} 字节（{address.Length} 点）", nameof(data));
            }

            byte[] command = BuildCommand(0x01, 0x02, code, wordAddress, address.Length);
            byte[] result = new byte[command.Length + expected];
            Array.Copy(command, result, command.Length);
            Array.Copy(data, 0, result, command.Length, expected);
            return result;
        }

        /// <summary>
        /// 8 字节命令：指令代码(2) + 区码(1) + 字地址(2，大端) + 位号(1) + 数量(2，大端)。
        /// 官方示例：读 D00010 起 10 字 → <c>01 01 82 00 0A 00 00 0A</c>。
        /// </summary>
        private static byte[] BuildCommand(byte command1, byte command2, byte areaCode, int wordAddress, int points)
        {
            if (points < 1 || points > MaxPointsPerRequest)
            {
                throw new ArgumentOutOfRangeException(nameof(points), points, $"单次点数必须在 1~{MaxPointsPerRequest} 之间");
            }

            int bitIndex = wordAddress & 0xFF;      // 低位字节承载位号
            int word = wordAddress >> 8;

            byte[] data = new byte[8];
            data[0] = command1;
            data[1] = command2;
            data[2] = areaCode;
            // 字地址 2 字节：高字节在前（官方示例 00 0A 即 0x000A）
            data[3] = (byte)(word >> 8);
            data[4] = (byte)word;
            // 位号：字访问时恒 0
            data[5] = (byte)bitIndex;
            // 数量 2 字节：高字节在前
            data[6] = (byte)(points >> 8);
            data[7] = (byte)points;
            return data;
        }

        /// <summary>
        /// 取区码：EM 按 bank 动态算，其余查表。
        /// </summary>
        private static byte ResolveAreaCode(PlcAddress address)
        {
            OmronFinsDeviceSpec spec = OmronFinsDeviceCatalog.Get(address.Area);

            if (address.Area == MemoryArea.EM)
            {
                int bank = address.DbNumber ?? 0;
                return address.IsBit
                    ? OmronFinsDeviceCatalog.GetEmBitCode(bank)
                    : OmronFinsDeviceCatalog.GetEmWordCode(bank);
            }

            byte code = address.IsBit ? spec.BitCode : spec.WordCode;
            if (code == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(address),
                    address.IsBit ? $"{spec.Name}({address.Area}) 不支持按位访问" : $"{spec.Name}({address.Area}) 不支持按字访问");
            }
            return code;
        }

        /// <summary>
        /// 把统一地址换算成「字地址 &lt;&lt; 8 | 位号」，并叠加计数器区的 0x8000 偏移。
        /// </summary>
        private static int ResolveWordAddress(PlcAddress address)
        {
            OmronFinsDeviceSpec spec = OmronFinsDeviceCatalog.Get(address.Area);

            int wordAddress;
            int bitIndex;
            spec.ResolveAddress(address.Address, address.IsBit, out wordAddress, out bitIndex);

            wordAddress += OmronFinsDeviceCatalog.GetAddressOffset(address.Area);

            if (wordAddress < 0 || wordAddress > 0xFFFF)
            {
                throw new ArgumentOutOfRangeException(nameof(address), wordAddress, "换算后的字地址超出 2 字节范围");
            }
            if (bitIndex < 0 || bitIndex > 15)
            {
                throw new ArgumentOutOfRangeException(nameof(address), bitIndex, "位号必须在 0~15 之间");
            }

            return (wordAddress << 8) | bitIndex;
        }

        /// <summary>
        /// 拼接完整报文：外层 16 字节（FINS + 长度 + 命令 2 + 错误码 0）+ FINS 帧头 10 字节 + 命令体。
        /// </summary>
        public byte[] BuildCompletePack(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            byte[] frame = new byte[OuterHeaderLength + FinsHeaderLength + data.Length];

            // 外层：定界符
            frame[0] = (byte)'F';
            frame[1] = (byte)'I';
            frame[2] = (byte)'N';
            frame[3] = (byte)'S';
            // 长度 = 帧总长 - 8（大端）
            int length = frame.Length - 8;
            frame[4] = (byte)(length >> 24);
            frame[5] = (byte)(length >> 16);
            frame[6] = (byte)(length >> 8);
            frame[7] = (byte)length;
            // 命令：0x00000002 发 FINS 帧
            frame[8] = 0;
            frame[9] = 0;
            frame[10] = 0;
            frame[11] = (byte)CommandFinsFrame;
            // 错误码：请求固定 0
            frame[12] = 0;
            frame[13] = 0;
            frame[14] = 0;
            frame[15] = 0;

            // FINS 帧头 10 字节
            int h = OuterHeaderLength;
            frame[h + 0] = 0x80;              // ICF
            frame[h + 1] = 0x00;              // RSV
            frame[h + 2] = 0x02;              // GCT
            frame[h + 3] = Dna;               // DNA
            frame[h + 4] = Da1;               // DA1
            frame[h + 5] = Da2;               // DA2
            frame[h + 6] = Sna;               // SNA
            frame[h + 7] = Sa1;               // SA1
            frame[h + 8] = Sa2;               // SA2
            frame[h + 9] = 0x00;              // SID

            Array.Copy(data, 0, frame, OuterHeaderLength + FinsHeaderLength, data.Length);
            return frame;
        }

        #endregion

        #region 响应解析

        /// <summary>
        /// 构建节点地址请求（握手）帧：20 字节，长度字段 0x0C，命令字 0。
        /// </summary>
        public byte[] BuildHandshakeFrame()
        {
            byte[] frame = new byte[20];
            frame[0] = (byte)'F';
            frame[1] = (byte)'I';
            frame[2] = (byte)'N';
            frame[3] = (byte)'S';
            frame[4] = 0;
            frame[5] = 0;
            frame[6] = 0;
            frame[7] = 0x0C;
            frame[8] = 0;
            frame[9] = 0;
            frame[10] = 0;
            frame[11] = (byte)CommandNodeAddressRequest;
            // [12..19] 全 0
            return frame;
        }

        /// <summary>
        /// 解析握手响应（24 字节）：[12..15] 外层错误码、[19] 本机节点、[23] PLC 节点。
        /// </summary>
        /// <param name="data">响应字节</param>
        /// <param name="sa1">输出：本机节点号</param>
        /// <param name="da1">输出：PLC 节点号</param>
        /// <param name="error">失败原因</param>
        /// <returns>是否成功</returns>
        public bool ParseHandshakeResponse(byte[] data, out byte sa1, out byte da1, out string error)
        {
            sa1 = 0;
            da1 = 0;
            error = null;

            if (data == null || data.Length < 24)
            {
                error = "握手响应长度不足（应至少 24 字节）";
                return false;
            }
            if (data[0] != 'F' || data[1] != 'I' || data[2] != 'N' || data[3] != 'S')
            {
                error = "握手响应不是 FINS 报文";
                return false;
            }

            uint errorCode = ReadUInt32BigEndian(data, 12);
            if (errorCode != 0)
            {
                error = $"FINS 节点地址握手失败，错误码 {errorCode}：{DescribeHandshakeError(errorCode)}";
                return false;
            }

            sa1 = data[19];
            da1 = data[23];
            Sa1 = sa1;
            Da1 = da1;
            IsHandshaked = true;
            return true;
        }

        /// <summary>
        /// 握手错误码说明（FINS/TCP 节点地址请求专用）。
        /// </summary>
        public static string DescribeHandshakeError(uint code)
        {
            switch (code)
            {
                case 1: return "消息头不是 FINS";
                case 2: return "数据长度太长";
                case 3: return "该命令不支持";
                case 20: return "超过连接上限";
                case 21: return "指定的节点已经处于连接中";
                case 22: return "尝试连接受保护的网络节点，该节点未配置到 PLC";
                case 23: return "客户端的网络节点超过正常范围";
                case 24: return "客户端的网络节点已被使用";
                case 25: return "所有网络节点均已被使用";
                default: return "未知错误";
            }
        }

        /// <summary>
        /// 解析写响应（只有结束码，无数据）。
        /// </summary>
        public Result<bool> ParseWriteResponse(byte[] data)
        {
            string error;
            int offset;
            if (!TryValidateResponse(data, out error, out offset))
            {
                return Fail<bool>(error, data);
            }

            Result<bool> result = new Result<bool>();
            result.IsSuccess = true;
            result.Data = true;
            return result;
        }

        /// <summary>
        /// 解析读位响应：数据区每点 1 字节（01 = ON）。
        /// </summary>
        public Result<bool[]> ParseReadBitResponse(byte[] data, int size)
        {
            string error;
            int offset;
            if (!TryValidateResponse(data, out error, out offset))
            {
                return Fail<bool[]>(error, data);
            }

            Result<bool[]> result = new Result<bool[]>();
            if (data.Length < offset + size)
            {
                result.ErrorMsg = $"返回数据不足：需要 {size} 字节，实际 {data.Length - offset} 字节";
                return result;
            }

            bool[] bits = new bool[size];
            for (int i = 0; i < size; i++)
            {
                bits[i] = data[offset + i] != 0;
            }

            result.IsSuccess = true;
            result.Data = bits;
            return result;
        }

        /// <summary>
        /// 解析读字响应：数据区每字 2 字节，低字节在前。
        /// </summary>
        public Result<short[]> ParseReadWordResponse(byte[] data, int size)
        {
            string error;
            int offset;
            if (!TryValidateResponse(data, out error, out offset))
            {
                return Fail<short[]>(error, data);
            }

            Result<short[]> result = new Result<short[]>();
            if (data.Length < offset + size * 2)
            {
                result.ErrorMsg = $"返回数据不足：需要 {size * 2} 字节，实际 {data.Length - offset} 字节";
                return result;
            }

            short[] words = new short[size];
            for (int i = 0; i < size; i++)
            {
                words[i] = (short)(data[offset + i * 2] | (data[offset + i * 2 + 1] << 8));
            }

            result.IsSuccess = true;
            result.Data = words;
            return result;
        }

        /// <summary>
        /// 校验响应：两层错误码都要查——外层是传输/握手错误，内层结束码是命令执行错误。
        /// 只有两层都通过，offset 才是数据起点。
        /// </summary>
        private bool TryValidateResponse(byte[] data, out string error, out int dataOffset)
        {
            error = null;
            dataOffset = ResponseHeaderLength;

            if (data == null || data.Length < ResponseHeaderLength)
            {
                error = $"响应长度不足：至少需要 {ResponseHeaderLength} 字节，实际 {(data == null ? 0 : data.Length)}";
                return false;
            }
            if (data[0] != 'F' || data[1] != 'I' || data[2] != 'N' || data[3] != 'S')
            {
                error = "响应不是 FINS 报文（定界符不符）";
                return false;
            }

            // 第一层：外层错误码 [12..15]
            uint outerError = ReadUInt32BigEndian(data, 12);
            if (outerError != 0)
            {
                error = $"FINS 传输层错误，错误码 {outerError}：{DescribeHandshakeError(outerError)}";
                return false;
            }

            // 第二层：结束代码 [28..29]（16 外层 + 10 帧头 + 2 指令代码之后）
            int endCodeOffset = OuterHeaderLength + FinsHeaderLength + CommandLength;
            int endCode = (data[endCodeOffset] << 8) | data[endCodeOffset + 1];
            if (endCode != 0)
            {
                error = $"FINS 命令执行失败，结束代码 0x{endCode:X4}";
                return false;
            }

            return true;
        }

        private static Result<T> Fail<T>(string message, byte[] data)
        {
            Result<T> result = new Result<T>();
            result.ErrorMsg = message;
            if (data != null && data.Length >= ResponseHeaderLength)
            {
                int endCodeOffset = OuterHeaderLength + FinsHeaderLength + CommandLength;
                int endCode = (data[endCodeOffset] << 8) | data[endCodeOffset + 1];
                if (endCode != 0)
                {
                    result.Kind = ErrorKind.DeviceEndCode;
                    result.EndCode = (ushort)endCode;
                }
            }
            return result;
        }

        private static uint ReadUInt32BigEndian(byte[] data, int offset)
        {
            return ((uint)data[offset] << 24) | ((uint)data[offset + 1] << 16)
                   | ((uint)data[offset + 2] << 8) | data[offset + 3];
        }

        #endregion
    }
}
