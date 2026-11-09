using PlcTest.Models;
using PlcTest.Tool;
using PlcTest.Client.ClientInterface;
using System;

namespace PlcTest.Client.ClientProtocolManagement.Siemens
{
    /// <summary>
    /// 西门子 S7-1200 通讯协议（S7comm over TPKT+COTP）
    /// </summary>
    public class SiemensS71200ClientProtocol : IPlcClientProtocol, IS7SessionProtocol, IProtocolOptionHost
    {
        /// <summary>
        /// COTP 目标 TSAP 里的机架号（0–7）。写错会连不上，报文里的 C2 参数会体现出来。
        /// </summary>
        public int Rack { get; set; } = 0;

        /// <summary>
        /// COTP 目标 TSAP 里的槽号（0–31）。S7-1200 通常为 1、S7-300 常见为 2。
        /// </summary>
        public int Slot { get; set; } = 1;

        /// <summary>
        /// Setup Communication 里申请的 PDU 长度，默认 960（0x03C0）。
        /// 部分设备/网络只接受 480，协商失败时可下调。
        /// </summary>
        public int RequestedPduLength { get; set; } = 960;

        /// <summary>
        /// 应用界面传来的协议选项：机架号 / 槽号 / 申请的 PDU 长度。
        /// </summary>
        /// <param name="options">选项集，可能为 null</param>
        public void ApplyOptions(ProtocolOptions options)
        {
            Rack = ProtocolOptionValues.ClampInt(options, "Rack", Rack, 0, 7);
            Slot = ProtocolOptionValues.ClampInt(options, "Slot", Slot, 0, 31);
            RequestedPduLength = ProtocolOptionValues.ClampInt(options, "PduLength", RequestedPduLength, 240, 960);
        }

        /// <summary>
        /// 协议数据单元引用，每条请求加一；响应必须回同一个值
        /// </summary>
        private byte _pduRef = 0;

        /// <summary>
        /// 最近一次发出的请求的 PDU 引用，用于校验响应是不是这条命令的
        /// </summary>
        private ushort _expectedPduRef;

        /// <summary>
        /// Setup Communication 协商出的 PDU 长度，0 表示尚未协商
        /// </summary>
        private int _negotiatedPduLength;

        /// <summary>
        /// 获取 S7 存储区编码
        /// X:0x81(Input)  Y:0x82(Output)  M:0x83(Marker)  D:0x84(DB)
        /// </summary>
        /// <summary>
        /// 获取西门子 S7 存储区代码。
        /// </summary>
        /// <param name="area">存储区域。</param>
        /// <returns>存储区代码。</returns>
        public int GetMemoryCode(MemoryArea area)
        {
            switch (area)
            {
                case MemoryArea.X: return 0x81;
                case MemoryArea.Y: return 0x82;
                case MemoryArea.M: return 0x83;
                //case MemoryArea.DB: return 0x84;
                default:
                    throw new ArgumentOutOfRangeException(nameof(area), "无效的存储区类型");
            }
        }
        /// <summary>
        /// 该存储区允许的访问方式。
        /// S7 的 M/X/Y 既可按字节/字访问，也可按位访问（如 M0.3）。
        /// </summary>
        /// <param name="area">存储区</param>
        /// <returns>允许的访问方式</returns>
        public AreaAccess GetAreaAccess(MemoryArea area)
        {
            switch (area)
            {
                case MemoryArea.X:
                case MemoryArea.Y:
                case MemoryArea.M:
                    return AreaAccess.Both;
                default:
                    throw new ArgumentOutOfRangeException(nameof(area), "无效的存储区类型");
            }
        }

        /// <summary>
        /// DB / 存储区大小由 PLC 组态决定，协议层不做限制。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>最大编号；-1 表示协议不做限制</returns>
        public int GetMaxIndex(MemoryArea memoryArea)
        {
            return -1;
        }

        /// <summary>
        /// 根据响应前缀推断整帧长度：TPKT 头的 [2..3] 为大端总长度（含 TPKT 头本身）。
        /// </summary>
        public int GetResponseFrameLength(byte[] request, byte[] buffer, int available)
        {
            if (buffer == null || available < 4) return -1;
            if (buffer[0] != 0x03) return -1;
            return (buffer[2] << 8) | buffer[3];
        }

        /// <summary>
        /// 将 bool 数组转为 S7 位写入数据（每 bool 占 1 字节，0x00/0x01）
        /// </summary>
        /// <summary>
        /// 将位数组转换为 S7 位写入数据。
        /// </summary>
        /// <param name="bools">位数据。</param>
        /// <returns>写入数据区。</returns>
        public byte[] GetWriteBitData(bool[] bools)
        {
            byte[] mData = new byte[bools.Length];
            for (int i = 0; i < bools.Length; i++)
            {
                mData[i] = bools[i] ? (byte)0x01 : (byte)0x00;
            }
            return mData;
        }

        /// <summary>
        /// 将 short 数组转为 S7 字写入数据（大端序，每字 2 字节）
        /// </summary>
        /// <summary>
        /// 将字数组转换为 S7 字写入数据。
        /// </summary>
        /// <param name="shorts">字数据。</param>
        /// <returns>写入数据区。</returns>
        public byte[] GetWriteWordData(short[] shorts)
        {
            byte[] mData = new byte[shorts.Length * 2];
            for (int i = 0; i < shorts.Length; i++)
            {
                byte[] bytes = BitConverter.GetBytes(shorts[i]);
                mData[i * 2] = bytes[1];
                mData[i * 2 + 1] = bytes[0];
            }
            return mData;
        }

        /// <summary>
        /// 构建 S7 读取命令参数区（功能码 0x04，14 字节）
        /// </summary>
        /// <summary>
        /// 构建 S7 读取命令的数据区。
        /// </summary>
        /// <param name="address">统一地址模型，包含 DB 编号、地址和长度。</param>
        /// <returns>读取参数区。</returns>
        public byte[] BuildReadData(PlcAddress address)
        {
            int memory = GetMemoryCode(address.Area);
            int db = address.DbNumber ?? 0;
            int startAddress = address.Address;
            bool isBit = address.IsBit;
            int length = address.Length;
            byte[] param = new byte[14];
            //功能码
            param[0] = 0x04;
            //通信项数
            param[1] = 0x01;
            //变量指定
            param[2] = 0x12;
            //后续地址描述长度
            param[3] = 0x0A;
            //SyntaxID
            param[4] = 0x10;
            //传输类型: 0x01=位, 0x02=字节
            param[5] = isBit ? (byte)0x01 : (byte)0x02;
            //操作数据长度（大端）。单位跟着传输类型走：
            //  0x01(BIT)  → 位，直接填点数
            //  0x02(BYTE) → 字节，段字数要乘 2（旧实现直接填点数，读 10 字只拿到 5 字）
            int elementCount = isBit ? length : length * 2;
            param[6] = (byte)(elementCount >> 8);
            param[7] = (byte)(elementCount & 0xFF);
            //DB号 (非DB时为0)
            if (memory != 0x84)
            {
                param[8] = 0x00;
                param[9] = 0x00;
            }
            else
            {
                param[8] = (byte)((db >> 8) & 0xFF);
                param[9] = (byte)(db & 0xFF);
            }
            //存储区类型
            param[10] = (byte)memory;
            //起始地址
            int addrVal = startAddress * 8;
            param[11] = (byte)((addrVal >> 16) & 0xFF);
            param[12] = (byte)((addrVal >> 8) & 0xFF);
            param[13] = (byte)(addrVal & 0xFF);
            return param;
        }

        /// <summary>
        /// 构建 S7 写入命令参数区 + 数据区（功能码 0x05，18 字节参数 + 写入数据）
        /// </summary>
        /// <summary>
        /// 构建 S7 写入命令的数据区。
        /// </summary>
        /// <param name="address">统一地址模型，包含 DB 编号、地址和长度。</param>
        /// <param name="data">写入数据。</param>
        /// <returns>写入参数区与数据区。</returns>
        public byte[] BuildWriteData(PlcAddress address, byte[] data)
        {
            int memory = GetMemoryCode(address.Area);
            int db = address.DbNumber ?? 0;
            int startAddress = address.Address;
            bool isBit = address.IsBit;
            int length = address.Length;
            byte[] param = new byte[18 + data.Length];
            //功能码
            param[0] = 0x05;
            //通信项数
            param[1] = 0x01;
            //变量指定
            param[2] = 0x12;
            //后续地址描述长度
            param[3] = 0x0A;
            //SyntaxID
            param[4] = 0x10;
            //传输类型: 0x01=位, 0x02=字节
            param[5] = isBit ? (byte)0x01 : (byte)0x02;
            //操作数据长度（大端）：0x02(BYTE) 的单位是字节，所以取实际数据的字节数
            int elementCount = isBit ? length : data.Length;
            param[6] = (byte)(elementCount >> 8);
            param[7] = (byte)(elementCount & 0xFF);
            //DB号 (非DB时为0)
            if (memory != 0x84)
            {
                param[8] = 0x00;
                param[9] = 0x00;
            }
            else
            {
                param[8] = (byte)((db >> 8) & 0xFF);
                param[9] = (byte)(db & 0xFF);
            }
            //存储区类型
            param[10] = (byte)memory;
            //起始地址
            int addrVal = startAddress * 8;
            param[11] = (byte)((addrVal >> 16) & 0xFF);
            param[12] = (byte)((addrVal >> 8) & 0xFF);
            param[13] = (byte)(addrVal & 0xFF);
            //预留
            param[14] = 0x00;
            //写入数据类型: 0x03=位, 0x04=字节/字/双字
            param[15] = isBit ? (byte)0x03 : (byte)0x04;

            //数据项长度：S7 这里**按位**填（4 字节 → 0x0020），旧实现填字节数会被 PLC 当成 4 个位
            int bitLength = isBit ? length : data.Length * 8;
            param[16] = (byte)((bitLength >> 8) & 0xFF);
            param[17] = (byte)(bitLength & 0xFF);

            Array.Copy(data, 0, param, 18, data.Length);
            return param;
        }

        /// <summary>
        /// 封装 TPKT(4) + COTP(3) + S7通讯头(10) + 负载，构建完整报文
        /// 前 14 字节为参数区，剩余为数据区
        /// </summary>
        /// <summary>
        /// 封装 TPKT + COTP + S7 头，构建完整报文。
        /// </summary>
        /// <param name="data">S7 参数区与数据区。</param>
        /// <returns>完整通讯报文。</returns>
        public byte[] BuildCompletePack(byte[] data)
        {
            // 参数区恒为 14 字节（功能码 + 项数 + 12 字节 ANY 指针）。
            // 写命令多出来的部分（数据项头 4 字节 + 数据）属于**数据区**，
            // 旧实现按 18 字节算参数区，会把数据项头当成参数交给 PLC，读写全部失败。
            const int paramLen = 14;
            int dataLen = data.Length - paramLen;
            if (dataLen < 0) dataLen = 0;

            int totalLen = 7 + 10 + data.Length;
            byte[] command = new byte[totalLen];

            //TPKT（版本号1个字节+预留1个字节+报文总长度2个字节）
            command[0] = 0x03;
            command[1] = 0x00;
            command[2] = (byte)(totalLen >> 8);
            command[3] = (byte)(totalLen & 0xFF);
            //COTP(长度1个字节+PDU类型1个字节+目标引用1个字节)
            command[4] = 0x02;
            command[5] = 0xF0;
            command[6] = 0x80;
            //Header（协议ID 1个字节+ ACK_data 1个字节 +预留 2个字节 +协议数据单元 2个字节
            //+参数位长度 2个字节 数据长度 2个字节
            //）
            command[7] = 0x32;
            command[8] = 0x01;
            command[9] = 0x00;
            command[10] = 0x00;
            unchecked { _pduRef++; }
            _expectedPduRef = _pduRef;
            command[11] = (byte)(_pduRef >> 8);
            command[12] = (byte)(_pduRef & 0xFF);
            command[13] = (byte)(paramLen >> 8);
            command[14] = (byte)(paramLen & 0xFF);
            command[15] = (byte)(dataLen >> 8);
            command[16] = (byte)(dataLen & 0xFF);

            data.CopyTo(command, 17);
            return command;
        }

        #region 响应解析（按头部长度定位，不写死偏移）
        /// <summary>TPKT(4) + COTP(3)，S7 头从这里开始</summary>
        private const int S7HeaderOffset = 7;
        /// <summary>Job 请求头 10 字节</summary>
        private const int S7JobHeaderLength = 10;
        /// <summary>Ack-Data 响应头 12 字节：比请求多出 [错误类][错误码] 两字节</summary>
        private const int S7AckDataHeaderLength = 12;
        /// <summary>请求参数区起点：TPKT + COTP + 10 字节 Job 头</summary>
        private const int S7JobParamOffset = S7HeaderOffset + S7JobHeaderLength;
        /// <summary>响应参数区起点：TPKT + COTP + 12 字节 Ack-Data 头</summary>
        private const int S7AckDataParamOffset = S7HeaderOffset + S7AckDataHeaderLength;
        /// <summary>
        /// Ack-Data 头里 [错误类][错误码] 的位置。
        /// 这两字节只在响应里存在，旧实现把参数区起点按 10 字节头算成 17，
        /// 于是把错误类当成了功能码、把参数当成了数据项——真机上读写会全部解析失败。
        /// </summary>
        private const int S7ErrorClassOffset = S7HeaderOffset + S7JobHeaderLength;

        /// <summary>
        /// 定位响应里的数据项：跳过 TPKT + COTP + 12 字节响应头 + 参数区。
        /// 参数区长度取自 S7 头（第 13–14 字节），所以带错误信息、多变量的响应也能正确定位。
        /// </summary>
        /// <param name="data">完整响应报文</param>
        /// <param name="dataStart">数据项起点</param>
        /// <param name="error">失败原因</param>
        /// <returns>true 表示报文结构可用</returns>
        private bool TryLocateDataItem(byte[] data, out int dataStart, out string error)
        {
            dataStart = 0;
            error = null;

            if (data == null || data.Length < S7AckDataParamOffset)
            {
                error = "返回数据长度错误";
                return false;
            }
            if (data[0] != 0x03 || data[S7HeaderOffset] != 0x32)
            {
                error = "不是 S7 报文（TPKT 版本或协议 ID 不符）";
                return false;
            }
            if (data[S7HeaderOffset + 1] != 0x03)
            {
                error = $"不是 S7 响应帧（ROSCTR = 0x{data[S7HeaderOffset + 1]:X2}）";
                return false;
            }

            // 响应头里的 [错误类][错误码] 非 0 表示 PLC 直接拒了这条作业（地址错、无权限、资源不足…）。
            // 不在这里拦住的话，错误会被当成「读到 0」或「返回码异常」，排障信息全丢。
            byte errorClass = data[S7ErrorClassOffset];
            byte errorCode = data[S7ErrorClassOffset + 1];
            if (errorClass != 0 || errorCode != 0)
            {
                error = $"PLC 拒绝请求：错误类 0x{errorClass:X2}（{GetS7ErrorClassMsg(errorClass)}），"
                    + $"错误码 0x{errorCode:X2}（{GetS7ErrorMsg(errorCode)}）";
                return false;
            }

            // PDU 引用必须与最近一条请求一致，否则可能收到的是上一条命令的迟到响应
            int pduRef = (data[S7HeaderOffset + 4] << 8) | data[S7HeaderOffset + 5];
            if (_expectedPduRef != 0 && pduRef != _expectedPduRef)
            {
                error = $"PDU 引用不匹配（期望 0x{_expectedPduRef:X4}，收到 0x{pduRef:X4}）";
                return false;
            }

            int paramLength = (data[S7HeaderOffset + 6] << 8) | data[S7HeaderOffset + 7];
            dataStart = S7AckDataParamOffset + paramLength;
            // 写响应只有 1 字节返回码，读响应才有 4 字节数据项头，
            // 所以这里只要求 1 字节，具体长度由各解析方法按需再查。
            if (data.Length < dataStart + 1)
            {
                error = "响应缺少数据项（参数区长度与实际报文不符）";
                return false;
            }
            return true;
        }

        /// <summary>
        /// 解析 S7 写入响应报文
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <returns>返回写入结果</returns>
        public Result<bool> ParseWriteResponse(byte[] data)
        {
            int dataStart;
            string error;
            if (!TryLocateDataItem(data, out dataStart, out error))
            {
                return Result<bool>.Fail(error);
            }

            byte returnCode = data[dataStart];
            if (returnCode == 0xFF)
            {
                return Result<bool>.Success(true);
            }

            // 失败时部分 PLC 会多回 1 字节错误码，有就一起报出来便于对照手册
            byte errorClass = data[dataStart];
            byte errorCode = data.Length > dataStart + 1 ? data[dataStart + 1] : (byte)0x00;
            return Result<bool>.Fail(
                $"写入失败：返回码 0x{returnCode:X2}，错误类 0x{errorClass:X2}，错误码 0x{errorCode:X2}（{GetS7ErrorMsg(errorCode)}）");
        }

        /// <summary>
        /// 解析 S7 读位响应报文
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度（位数）</param>
        /// <returns>位数据数组</returns>
        public Result<bool[]> ParseReadBitResponse(byte[] data, int size)
        {
            int dataStart;
            string error;
            if (!TryLocateDataItem(data, out dataStart, out error))
            {
                return Result<bool[]>.Fail(error);
            }

            byte returnCode = data[dataStart];
            if (returnCode != 0xFF)
            {
                return Result<bool[]>.Fail(
                    $"读取失败：返回码 0x{returnCode:X2}，错误码 0x{data[dataStart + 1]:X2}（{GetS7ErrorMsg(data[dataStart + 1])}）");
            }

            // 数据项头 4 字节（返回码 / 传输类型 / 位长度），数据从 +4 开始；位数据每点占 1 字节
            if (data.Length < dataStart + 4)
            {
                return Result<bool[]>.Fail("响应数据项不完整");
            }
            int payloadStart = dataStart + 4;
            int bitLength = (data[dataStart + 2] << 8) | data[dataStart + 3];
            int payloadBytes = (bitLength + 7) / 8;
            if (data.Length - payloadStart < payloadBytes || data.Length - payloadStart < size)
            {
                return Result<bool[]>.Fail("返回数据长度不足");
            }

            bool[] realData = new bool[size];
            for (int i = 0; i < size; i++)
            {
                realData[i] = data[payloadStart + i] != 0x00;
            }
            return Result<bool[]>.Success(realData);
        }

        /// <summary>
        /// 解析 S7 读字响应报文
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度（字数）</param>
        /// <returns>字数据数组</returns>
        public Result<short[]> ParseReadWordResponse(byte[] data, int size)
        {
            int dataStart;
            string error;
            if (!TryLocateDataItem(data, out dataStart, out error))
            {
                return Result<short[]>.Fail(error);
            }

            byte returnCode = data[dataStart];
            if (returnCode != 0xFF)
            {
                return Result<short[]>.Fail(
                    $"读取失败：返回码 0x{returnCode:X2}，错误码 0x{data[dataStart + 1]:X2}（{GetS7ErrorMsg(data[dataStart + 1])}）");
            }

            int payloadStart = dataStart + 4;
            int expectedBytes = size * 2;
            if (data.Length - payloadStart < expectedBytes)
            {
                return Result<short[]>.Fail("返回数据长度不足");
            }

            short[] realData = new short[size];
            for (int i = 0; i < size; i++)
            {
                realData[i] = (short)((data[payloadStart + i * 2] << 8) | data[payloadStart + i * 2 + 1]);
            }
            return Result<short[]>.Success(realData);
        }
        #endregion

        #region IS7SessionProtocol（COTP 连接 + Setup Communication）
        /// <summary>
        /// 是否已完成两段握手。仅用于诊断：重复 Connect 会重新握手（TCP 会话已经换了）。
        /// </summary>
        public bool IsSessionEstablished { get; private set; }

        /// <summary>
        /// 握手协商出的 PDU 长度，0 表示尚未协商
        /// </summary>
        public int NegotiatedPduLength
        {
            get { return _negotiatedPduLength; }
        }

        /// <summary>
        /// COTP 连接请求（Connection Request）：22 字节，PDU 类型 0xE0。
        /// TP 参数里 C2 是目标 TSAP —— 0x03 表示「按机架/插槽寻址」，0x01 = rack0 / slot1（S7-1200 默认槽号 1）。
        /// </summary>
        public byte[] BuildCotpConnectRequest()
        {
            byte[] frame = new byte[22];
            // TPKT：版本 / 预留 / 总长 22
            frame[0] = 0x03;
            frame[1] = 0x00;
            frame[2] = 0x00;
            frame[3] = 0x16;
            // COTP：长度 17 / PDU 类型 CR / 目标引用 0x0000 / 源引用 0x0001 / class 0
            frame[4] = 0x11;
            frame[5] = 0xE0;
            frame[6] = 0x00;
            frame[7] = 0x00;
            frame[8] = 0x00;
            frame[9] = 0x01;
            frame[10] = 0x00;
            // C1 源 TSAP
            frame[11] = 0xC1;
            frame[12] = 0x02;
            frame[13] = 0x10;
            frame[14] = 0x00;
            // C2 目标 TSAP：连接类型 0x03 + rack/slot（0x01 = rack0 / slot1）
            frame[15] = 0xC2;
            frame[16] = 0x02;
            frame[17] = 0x03;
            // 机架号占高 3 位、槽号占低 5 位；默认 rack0/slot1 = 0x01
            frame[18] = (byte)(((Rack & 0x07) << 5) | (Slot & 0x1F));
            // C0 TPDU 大小 = 1024
            frame[19] = 0xC0;
            frame[20] = 0x01;
            frame[21] = 0x0A;
            return frame;
        }

        /// <summary>
        /// 解析 COTP 连接确认（Connection Confirm，PDU 类型 0xD0）。
        /// </summary>
        public bool ParseCotpConnectResponse(byte[] data, out string error)
        {
            error = null;
            if (data == null || data.Length < 7)
            {
                error = "COTP 连接响应过短";
                return false;
            }
            if (data[0] != 0x03)
            {
                error = "不是 TPKT 报文（版本号不是 0x03）";
                return false;
            }
            // 0x80 = DR（断开请求）：机架/槽号写错、连接资源占满时 PLC 常用它拒绝
            if (data[5] == 0x80)
            {
                error = "PLC 拒绝 COTP 连接（收到 DR 0x80）：检查机架号/槽号，或 PLC 连接资源是否已占满";
                return false;
            }
            if (data[5] != 0xD0)
            {
                error = $"COTP 连接失败（PDU 类型 0x{data[5]:X2}，期望 0xD0）";
                return false;
            }
            // COTP 长度字段（data[4]）是从它后面算起的字节数，对不上说明帧被截断
            if (data[4] + 5 > data.Length)
            {
                error = $"COTP 连接确认被截断（长度字段 {data[4]}，报文共 {data.Length} 字节）";
                return false;
            }
            // CC 的目标引用必须回显我们 CR 里的源引用（0x0001），否则不是这条连接的应答
            int dstRef = (data[6] << 8) | data[7];
            if (dstRef != 0x0001)
            {
                error = $"COTP 目标引用不匹配（期望 0x0001，收到 0x{dstRef:X4}）";
                return false;
            }
            return true;
        }

        /// <summary>
        /// S7 的 Setup Communication 请求：25 字节，参数区 8 字节（功能码 0xF0 + 协商 PDU 长度）。
        /// 这里按常见实现申请 0x03C0（960），实际以 PLC 回包为准。
        /// </summary>
        public byte[] BuildSetupCommunicationRequest()
        {
            byte[] frame = new byte[25];
            // TPKT
            frame[0] = 0x03;
            frame[1] = 0x00;
            frame[2] = 0x00;
            frame[3] = 0x19;
            // COTP（DT 数据），引用与连接阶段保持一致
            frame[4] = 0x02;
            frame[5] = 0xF0;
            frame[6] = 0x80;
            // S7 头：协议 ID 0x32 / ROSCTR 0x01(请求) / 冗余 0x0000 / PDU 引用 0x0000 / 参数 8 / 数据 0
            frame[7] = 0x32;
            frame[8] = 0x01;
            frame[9] = 0x00;
            frame[10] = 0x00;
            frame[11] = 0x00;
            frame[12] = 0x00;
            frame[13] = 0x00;
            frame[14] = 0x08;
            frame[15] = 0x00;
            frame[16] = 0x00;
            // 参数：功能码 0xF0 / 预留 / 最大并行任务 3 / 最大并行任务 3 / 申请的 PDU 长度（默认 0x03C0）
            frame[17] = 0xF0;
            frame[18] = 0x00;
            frame[19] = 0x00;
            frame[20] = 0x03;
            frame[21] = 0x00;
            frame[22] = 0x03;
            frame[23] = (byte)((RequestedPduLength >> 8) & 0xFF);
            frame[24] = (byte)(RequestedPduLength & 0xFF);
            return frame;
        }

        /// <summary>
        /// 解析 Setup Communication 响应，取回 PLC 允许的 PDU 长度。
        /// </summary>
        public bool ParseSetupCommunicationResponse(byte[] data, out int pduLength, out string error)
        {
            pduLength = 0;
            error = null;

            if (data == null || data.Length < S7AckDataParamOffset + 8)
            {
                error = "Setup Communication 响应过短";
                return false;
            }
            if (data[0] != 0x03 || data[S7HeaderOffset] != 0x32)
            {
                error = "不是 S7 报文（TPKT 版本或协议 ID 不符）";
                return false;
            }
            if (data[S7HeaderOffset + 1] != 0x03)
            {
                error = $"Setup Communication 失败（ROSCTR = 0x{data[S7HeaderOffset + 1]:X2}）";
                return false;
            }

            // 建链被拒时，PLC 会在这里给出错误类/错误码（如 0x81 应用关系、0x83 资源不足）。
            // 不判断的话就是 HSL 那种「假成功」：握手看着过了，第一条 Read 才报错，排障信息全丢。
            byte errorClass = data[S7ErrorClassOffset];
            byte errorCode = data[S7ErrorClassOffset + 1];
            if (errorClass != 0 || errorCode != 0)
            {
                error = $"PLC 拒绝建链：错误类 0x{errorClass:X2}（{GetS7ErrorClassMsg(errorClass)}），"
                    + $"错误码 0x{errorCode:X2}（{GetS7ErrorMsg(errorCode)}）";
                return false;
            }

            // Setup Communication 的参数区固定 8 字节、数据区为空，且报文总长应正好等于
            // 19（TPKT+COTP+12 字节 Ack-Data 头）+ 参数 + 数据。三者对不上就说明帧结构自相矛盾，
            // 宁可拒绝也不要按固定偏移硬读出个 0 当协商结果（后续分段会全错）。
            int paramLength = (data[S7HeaderOffset + 6] << 8) | data[S7HeaderOffset + 7];
            int dataLength = (data[S7HeaderOffset + 8] << 8) | data[S7HeaderOffset + 9];
            if (paramLength != 8 || dataLength != 0
                || data.Length != S7AckDataParamOffset + paramLength + dataLength)
            {
                error = $"Setup Communication 响应结构异常：参数区长度 {paramLength}、数据长度 {dataLength}，报文共 {data.Length} 字节";
                return false;
            }
            if (data[S7AckDataParamOffset] != 0xF0)
            {
                error = $"Setup Communication 失败（功能码 0x{data[S7AckDataParamOffset]:X2}）";
                return false;
            }

            pduLength = (data[S7AckDataParamOffset + 6] << 8) | data[S7AckDataParamOffset + 7];
            _negotiatedPduLength = pduLength;
            IsSessionEstablished = true;
            return true;
        }
        #endregion
        /// <summary>
        /// S7 错误码对应的中文描述
        /// </summary>
        /// <summary>
        /// 将 S7 返回码转换为中文错误信息。
        /// </summary>
        /// <param name="errorCode">返回码。</param>
        /// <returns>错误描述。</returns>
        private static string GetS7ErrorMsg(byte errorCode)
        {
            switch (errorCode)
            {
                case 0x00: return "无错误";
                case 0x01: return "硬件故障";
                case 0x03: return "对象不允许访问";
                case 0x05: return "地址越界 / 参数非法";
                case 0x06: return "数据类型不支持";
                case 0x07: return "数据类型不一致";
                case 0x0A: return "对象不存在";
                case 0xFF: return "";
                default: return $"S7错误码: 0x{errorCode:X2}";
            }
        }

        /// <summary>
        /// Ack-Data 头里「错误类」的含义。握手阶段最常见的是 0x81（应用关系没建起来，
        /// 多为机架/槽号或连接资源问题）和 0x83（资源不足，连接数占满）。
        /// </summary>
        /// <param name="errorClass">错误类字节</param>
        /// <returns>中文描述</returns>
        private static string GetS7ErrorClassMsg(byte errorClass)
        {
            switch (errorClass)
            {
                case 0x00: return "无错误";
                case 0x81: return "应用关系问题（机架/槽号错误或连接未建立）";
                case 0x82: return "对象定义错误（地址或 DB 不存在）";
                case 0x83: return "资源不足（PLC 连接数已占满）";
                case 0x84: return "服务处理错误";
                case 0x85: return "供给错误（状态不允许）";
                case 0x87: return "访问错误（权限或保护级别）";
                default: return $"S7错误类: 0x{errorClass:X2}";
            }
        }
    }
}
