using PlcTest.Models;
using PlcTest.Server.ServerInterface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PlcTest.Server.PlcServer.Siemens
{
    /// <summary>
    /// 西门子 S7-1200 从站模拟服务端。
    /// <para>
    /// 存储用**字节数组**，不是三菱那套「位表 + 字表分离」：
    /// S7 的 <c>MB100</c> / <c>MW100</c> / <c>M100.0</c> 是同一片内存的不同视图，
    /// 分开存会导致「写 MW100 之后读 M100.0 读不到」，真机联调立刻穿帮。
    /// </para>
    /// <para>
    /// 服务端无会话状态：COTP / Setup / 读写每帧独立识别应答，握手不需要记忆
    /// （分帧靠 TPKT 长度，Setup 的 PDU 直接回显请求值）。
    /// </para>
    /// </summary>
    public class S7PlcServer : PlcServerBase
    {
        /// <summary>每个区的字节数。真机 S7-1200 的 M 区通常 8–16KB，这里给 64KB 与 HSL 同量级，够测。</summary>
        private const int AreaSize = 65536;

        private readonly IS7ServerProtocol _protocol;
        private readonly object _memoryLock = new object();

        private readonly byte[] _input = new byte[AreaSize];
        private readonly byte[] _output = new byte[AreaSize];
        private readonly byte[] _memory = new byte[AreaSize];

        /// <summary>
        /// DB 块：按块号**惰性分配**。DB 号可以到 65535，不能像 I/Q/M 那样构造时就铺满
        /// （65535 × 64KB ≈ 4GB）；也只有真正访问过的块才占内存。
        /// </summary>
        private readonly ConcurrentDictionary<int, byte[]> _dbBlocks
            = new ConcurrentDictionary<int, byte[]>();

        /// <summary>
        /// 界面上「已初始化」的字地址。字节数组是全量预分配的，这里只决定软元件视图显示哪些行。
        /// key 由「区域 + DB 号」合成，见 <see cref="TableKey"/>。
        /// </summary>
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, byte>> _registered
            = new ConcurrentDictionary<int, ConcurrentDictionary<int, byte>>();

        /// <summary>
        /// 区域 + 块号合成一个 key。DB1.DBW0 与 DB2.DBW0 是两块内存，
        /// 用 (area, address) 两元组做 key 会把它们合并成一条记录。
        /// </summary>
        private static int TableKey(MemoryArea area, int dbNumber)
        {
            return ((int)area << 16) ^ (dbNumber & 0xFFFF);
        }

        /// <summary>构造</summary>
        /// <param name="comm">服务端通讯方式</param>
        /// <param name="protocol">S7 协议实现</param>
        public S7PlcServer(IServerCommunication comm, IS7ServerProtocol protocol)
            : base(comm)
        {
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            Communication.FrameLengthResolver = _protocol.GetRequestFrameLength;
        }

        /// <summary>本服务端模拟的区域，与客户端声明的软元件表对称。</summary>
        public override IReadOnlyList<MemoryArea> GetSupportedAreas()
        {
            return _protocol.GetSupportedAreas();
        }

        #region 请求处理

        /// <summary>发响应并记一条日志。基类只暴露 <c>Communication.SendToClient</c>，这里收一下口。</summary>
        private void Send(byte[] data, IClientSession client)
        {
            Communication.SendToClient(data, client);
            LogFrame(true, data);
        }

        /// <summary>
        /// 处理一帧：先分类再分派。握手与读写是独立的四类，互不依赖顺序——
        /// 模拟器不追求「必须先握手」，但会把跳过的步骤写进日志便于排查。
        /// </summary>
        protected override void HandleFrame(byte[] data, IClientSession client)
        {
            switch (_protocol.ClassifyRequest(data))
            {
                case S7RequestKind.CotpConnect:
                    LogFrame(false, data, "COTP 连接请求");
                    Send(_protocol.BuildCotpConnectResponse(data), client);
                    return;

                case S7RequestKind.SetupCommunication:
                    LogFrame(false, data, "Setup Communication");
                    Send(_protocol.BuildSetupCommunicationResponse(data), client);
                    return;

                case S7RequestKind.ReadVar:
                    LogFrame(false, data);
                    HandleRead(data, client);
                    return;

                case S7RequestKind.WriteVar:
                    LogFrame(false, data);
                    HandleWrite(data, client);
                    return;

                default:
                    LogFrame(false, data, "报文不属于本协议，已丢弃");
                    return;
            }
        }

        private void HandleRead(byte[] data, IClientSession client)
        {
            S7RequestInfo request;
            string error;
            if (!_protocol.TryParseReadRequest(data, out request, out error))
            {
                // 整条作业都不成立（区码不认识、项数非法、报文截断）→ 作业级错误，
                // 不冒充「某一项读不到」，否则客户端会以为其他项是好的
                LogComm("读请求解析失败：" + error);
                Send(_protocol.BuildJobErrorResponse(data, 0x04, 0x81, 0x05), client);
                return;
            }

            // 多变量：逐项读，某一项失败只影响该项。真实 PLC 也是这样回——
            // 第二个地址越界时，第一个地址的数据照样返回
            var results = new List<S7ItemResult>(request.Count);
            for (int i = 0; i < request.Count; i++)
            {
                results.Add(ReadItem(request.Items[i], i));
            }
            Send(_protocol.BuildReadResponse(data, results), client);
        }

        /// <summary>读一项；失败返回带错误码的项，不抛异常也不中断整帧。</summary>
        private S7ItemResult ReadItem(S7ItemRequest item, int index)
        {
            string error;
            if (item.IsBit)
            {
                bool[] bits;
                if (!TryReadBits(item, out bits, out error))
                {
                    LogComm($"第 {index + 1} 项位读失败：{error}");
                    return S7ItemResult.Fail(true, 0x05);
                }
                return S7ItemResult.OfBits(bits);
            }

            short[] words;
            if (!TryReadWords(item, out words, out error))
            {
                LogComm($"第 {index + 1} 项字读失败：{error}");
                return S7ItemResult.Fail(false, 0x05);
            }
            return S7ItemResult.OfWords(words);
        }

        private void HandleWrite(byte[] data, IClientSession client)
        {
            S7RequestInfo request;
            string error;
            if (!_protocol.TryParseWriteRequest(data, out request, out error))
            {
                LogComm("写请求解析失败：" + error);
                Send(_protocol.BuildJobErrorResponse(data, 0x05, 0x81, 0x05), client);
                return;
            }

            // 同样逐项写：写成功或失败都要给每项一个返回码，长度必须与请求项数一致
            var errorCodes = new List<ushort>(request.Count);
            for (int i = 0; i < request.Count; i++)
            {
                if (TryWrite(request.Items[i], out error))
                {
                    errorCodes.Add(0);
                }
                else
                {
                    LogComm($"第 {i + 1} 项写失败：{error}");
                    errorCodes.Add(0x05);
                }
            }
            Send(_protocol.BuildWriteResponse(data, errorCodes), client);
        }

        #endregion

        #region 存储原语

        /// <summary>
        /// 按区域（和 DB 号）取存储。返回 null 表示该区域不支持。
        /// DB 号非法（≤0 或 &gt;65535）同样返回 null，由调用方回错误码。
        /// </summary>
        private byte[] BufferOf(MemoryArea area, int dbNumber)
        {
            switch (area)
            {
                case MemoryArea.X:
                    return _input;
                case MemoryArea.Y:
                    return _output;
                case MemoryArea.M:
                    return _memory;
                case MemoryArea.DB:
                    if (dbNumber <= 0 || dbNumber > 65535)
                    {
                        return null;
                    }
                    return _dbBlocks.GetOrAdd(dbNumber, key => new byte[AreaSize]);
                default:
                    return null;
            }
        }

        /// <summary>
        /// 读连续位。<paramref name="S7RequestInfo.ByteOffset"/> 是字节偏移，
        /// 位索引按「字节 × 8」展开——与客户端 <c>addrVal = Address * 8</c> 的口径一致。
        /// </summary>
        private bool TryReadBits(S7ItemRequest request, out bool[] bits, out string error)
        {
            bits = null;
            error = null;

            byte[] buffer = BufferOf(request.Area, request.DbNumber);
            if (buffer == null)
            {
                error = "不支持的存储区：" + request.Area;
                return false;
            }
            if (request.ByteOffset < 0 || request.Length <= 0)
            {
                error = $"地址或长度非法（偏移 {request.ByteOffset}、长度 {request.Length}）";
                return false;
            }

            int startBit = request.ByteOffset * 8;
            int lastByte = (startBit + request.Length - 1) / 8;
            if (lastByte >= buffer.Length)
            {
                // 明确报错，不学 HSL 那样「越界读返回全 0 且报成功」——
                // 那样客户端拿到半真半假的数据，还以为是 PLC 里就是 0
                error = $"越界：位 {startBit} + {request.Length} 超过 {request.Area} 区 {buffer.Length} 字节";
                return false;
            }

            lock (_memoryLock)
            {
                bits = new bool[request.Length];
                for (int i = 0; i < bits.Length; i++)
                {
                    int bit = startBit + i;
                    bits[i] = (buffer[bit >> 3] & (1 << (bit & 7))) != 0;
                }
            }
            return true;
        }

        /// <summary>读连续字，大端。</summary>
        private bool TryReadWords(S7ItemRequest request, out short[] words, out string error)
        {
            words = null;
            error = null;

            byte[] buffer = BufferOf(request.Area, request.DbNumber);
            if (buffer == null)
            {
                error = "不支持的存储区：" + request.Area;
                return false;
            }

            int byteCount = request.Length * 2;
            if (request.ByteOffset < 0 || request.Length <= 0
                || request.ByteOffset + byteCount > buffer.Length)
            {
                error = $"越界：字节 {request.ByteOffset} + {byteCount} 超过 {request.Area} 区 {buffer.Length} 字节";
                return false;
            }

            lock (_memoryLock)
            {
                words = new short[request.Length];
                for (int i = 0; i < words.Length; i++)
                {
                    int at = request.ByteOffset + i * 2;
                    words[i] = (short)((buffer[at] << 8) | buffer[at + 1]);
                }
            }
            return true;
        }

        /// <summary>
        /// 写入。位写有两种流派：逐点 1 字节（本项目客户端）与按位打包（标准），
        /// 用「数据长度 == 点数」还是「== 点数/8」来区分，两种都接受——否则只能二选一地「自洽」。
        /// </summary>
        private bool TryWrite(S7ItemRequest request, out string error)
        {
            error = null;

            byte[] buffer = BufferOf(request.Area, request.DbNumber);
            if (buffer == null)
            {
                error = "不支持的存储区：" + request.Area;
                return false;
            }
            if (request.WriteData == null || request.WriteData.Length == 0)
            {
                error = "写请求没有数据";
                return false;
            }

            if (request.IsBit)
            {
                return TryWriteBits(buffer, request, out error);
            }
            return TryWriteWords(buffer, request, out error);
        }

        private bool TryWriteBits(byte[] buffer, S7ItemRequest request, out string error)
        {
            error = null;

            int startBit = request.ByteOffset * 8;
            int lastByte = (startBit + request.Length - 1) / 8;
            if (request.ByteOffset < 0 || request.Length <= 0 || lastByte >= buffer.Length)
            {
                error = $"越界：位 {startBit} + {request.Length} 超过 {request.Area} 区 {buffer.Length} 字节";
                return false;
            }

            bool packed = request.WriteData.Length != request.Length;
            int required = packed ? (request.Length + 7) / 8 : request.Length;
            if (request.WriteData.Length < required)
            {
                error = $"位写数据不足：需要 {required} 字节，实收 {request.WriteData.Length} 字节";
                return false;
            }

            lock (_memoryLock)
            {
                for (int i = 0; i < request.Length; i++)
                {
                    bool value = packed
                        ? (request.WriteData[i >> 3] & (1 << (i & 7))) != 0
                        : request.WriteData[i] != 0;

                    int bit = startBit + i;
                    int index = bit >> 3;
                    byte mask = (byte)(1 << (bit & 7));
                    if (value)
                    {
                        buffer[index] |= mask;
                    }
                    else
                    {
                        buffer[index] &= (byte)~mask;
                    }
                }
            }
            return true;
        }

        private bool TryWriteWords(byte[] buffer, S7ItemRequest request, out string error)
        {
            error = null;

            int byteCount = request.Length * 2;
            if (request.ByteOffset < 0 || request.Length <= 0
                || request.ByteOffset + byteCount > buffer.Length)
            {
                error = $"越界：字节 {request.ByteOffset} + {byteCount} 超过 {request.Area} 区 {buffer.Length} 字节";
                return false;
            }
            if (request.WriteData.Length < byteCount)
            {
                error = $"字写数据不足：需要 {byteCount} 字节，实收 {request.WriteData.Length} 字节";
                return false;
            }

            lock (_memoryLock)
            {
                Array.Copy(request.WriteData, 0, buffer, request.ByteOffset, byteCount);
            }
            return true;
        }

        #endregion

        #region IPlcServer（界面直接调用，不经协议）

        /// <summary>
        /// 读单个位。地址里的 <c>Address</c> 按**字节**理解（M100 → 字节 100 的 bit0），
        /// 与协议路径的「位地址 = 字节 × 8」保持一致；不是「第 100 个位」。
        /// </summary>
        public override Result<bool> ReadBit(PlcAddress address)
        {
            bool[] bits;
            if (!TryReadBits(ToItem(address, 1, true), out bits, out string error))
            {
                return Result<bool>.Fail(error);
            }
            return Result<bool>.Success(bits[0]);
        }

        /// <summary>读连续位</summary>
        public override Result<bool[]> ReadBitArray(PlcAddress address)
        {
            bool[] bits;
            if (!TryReadBits(ToItem(address, address.Length, true), out bits, out string error))
            {
                return Result<bool[]>.Fail(error);
            }
            return Result<bool[]>.Success(bits);
        }

        /// <summary>写单个位</summary>
        public override Result<bool> WriteBit(PlcAddress address, bool value)
        {
            return WriteBitArray(address, new[] { value });
        }

        /// <summary>写连续位</summary>
        public override Result<bool> WriteBitArray(PlcAddress address, bool[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Result<bool>.Fail("写入数据不能为空");
            }

            var request = ToItem(address, values.Length, true);
            request.WriteData = new byte[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                request.WriteData[i] = values[i] ? (byte)1 : (byte)0;
            }

            string error;
            if (!TryWrite(request, out error))
            {
                return Result<bool>.Fail(error);
            }
            return Result<bool>.Success(true);
        }

        /// <summary>读单个字（大端）</summary>
        public override Result<short> ReadWord(PlcAddress address)
        {
            short[] words;
            if (!TryReadWords(ToItem(address, 1, false), out words, out string error))
            {
                return Result<short>.Fail(error);
            }
            return Result<short>.Success(words[0]);
        }

        /// <summary>读连续字</summary>
        public override Result<short[]> ReadWordArray(PlcAddress address)
        {
            short[] words;
            if (!TryReadWords(ToItem(address, address.Length, false), out words, out string error))
            {
                return Result<short[]>.Fail(error);
            }
            return Result<short[]>.Success(words);
        }

        /// <summary>写单个字</summary>
        public override Result<bool> WriteWord(PlcAddress address, short value)
        {
            return WriteWordArray(address, new[] { value });
        }

        /// <summary>写连续字</summary>
        public override Result<bool> WriteWordArray(PlcAddress address, short[] values)
        {
            if (values == null || values.Length == 0)
            {
                return Result<bool>.Fail("写入数据不能为空");
            }

            var request = ToItem(address, values.Length, false);
            request.WriteData = new byte[values.Length * 2];
            for (int i = 0; i < values.Length; i++)
            {
                request.WriteData[i * 2] = (byte)((values[i] >> 8) & 0xFF);
                request.WriteData[i * 2 + 1] = (byte)(values[i] & 0xFF);
            }

            string error;
            if (!TryWrite(request, out error))
            {
                return Result<bool>.Fail(error);
            }
            return Result<bool>.Success(true);
        }

        private static S7ItemRequest ToItem(PlcAddress address, int length, bool isBit)
        {
            return new S7ItemRequest
            {
                Area = address.Area,
                DbNumber = address.DbNumber ?? 0,
                ByteOffset = address.Address,
                IsBit = isBit,
                Length = length,
            };
        }

        #endregion

        #region IRegisterTable（界面用）

        /// <summary>
        /// 按「区域 + 地址」加入模拟表。没有块号的签名对 DB 区无效——
        /// DB 必须带块号调用 <see cref="TryAddRegister(PlcAddress)"/>，否则存到哪一块无从判断。
        /// </summary>
        public override bool TryAddRegister(MemoryArea area, int address)
        {
            return TryAddRegister(area, 0, address);
        }

        /// <summary>
        /// 按完整地址加入模拟表。DB 区用 <c>address.DbNumber</c> 决定是哪一块，其余区域忽略块号。
        /// </summary>
        public override bool TryAddRegister(PlcAddress address)
        {
            return address != null && TryAddRegister(address.Area, address.DbNumber ?? 0, address.Address);
        }

        private bool TryAddRegister(MemoryArea area, int dbNumber, int address)
        {
            byte[] buffer = BufferOf(area, dbNumber);
            // address 是**字节偏移**（与 ReadWord/WriteWord 同口径），不是字编号：
            // 写成 address*2 会让界面初始化 D100 实际清到字节 200，读 MW100 时看到的还是旧值
            if (buffer == null || address < 0 || address + 1 >= buffer.Length)
            {
                return false;
            }

            var table = _registered.GetOrAdd(
                TableKey(area, dbNumber), key => new ConcurrentDictionary<int, byte>());
            if (!table.TryAdd(address, 0))
            {
                return false;
            }

            lock (_memoryLock)
            {
                buffer[address] = 0;
                buffer[address + 1] = 0;
            }
            return true;
        }

        /// <summary>已登记的字地址数量。DB 区按块汇总，所以是「所有 DB 块加起来的行数」。</summary>
        public override int GetRegisterCount(MemoryArea area)
        {
            int total = 0;
            foreach (KeyValuePair<int, ConcurrentDictionary<int, byte>> pair in _registered)
            {
                if ((pair.Key >> 16) == (int)area)
                {
                    total += pair.Value.Count;
                }
            }
            return total;
        }

        /// <summary>该字地址是否已登记。无块号的签名对 DB 区按 0 号块查，实际总是不存在。</summary>
        public override bool ContainsRegister(MemoryArea area, int address)
        {
            return ContainsRegister(area, 0, address);
        }

        private bool ContainsRegister(MemoryArea area, int dbNumber, int address)
        {
            ConcurrentDictionary<int, byte> table;
            return _registered.TryGetValue(TableKey(area, dbNumber), out table)
                && table.ContainsKey(address);
        }

        /// <summary>移除整个区域的登记（DB 区会清掉所有块），不动内存数据。</summary>
        public override void ClearRegisters(MemoryArea area)
        {
            foreach (int key in new List<int>(_registered.Keys))
            {
                if ((key >> 16) == (int)area)
                {
                    ConcurrentDictionary<int, byte> removed;
                    _registered.TryRemove(key, out removed);
                }
            }
        }

        /// <summary>按「区域 + 起始 + 长度」清零。无块号的签名对 DB 区按 0 号块处理（即无效）。</summary>
        public override void ResetRegisterValues(MemoryArea area, int start, int length)
        {
            ResetRegisterValues(area, 0, start, length);
        }

        /// <summary>按完整地址清零区间：DB 区用 <c>address.DbNumber</c> 定位块。</summary>
        public override void ResetRegisterValues(PlcAddress address, int length)
        {
            if (address != null)
            {
                ResetRegisterValues(address.Area, address.DbNumber ?? 0, address.Address, length);
            }
        }

        private void ResetRegisterValues(MemoryArea area, int dbNumber, int start, int length)
        {
            byte[] buffer = BufferOf(area, dbNumber);
            if (buffer == null || start < 0 || length <= 0)
            {
                return;
            }

            int byteCount = length * 2;
            if (start + byteCount > buffer.Length)
            {
                return;
            }

            lock (_memoryLock)
            {
                Array.Clear(buffer, start, byteCount);
            }
        }

        /// <summary>把该区域已登记的字全部清零（DB 区覆盖所有块），地址保留。</summary>
        public override void ResetAllRegisterValues(MemoryArea area)
        {
            foreach (KeyValuePair<int, ConcurrentDictionary<int, byte>> pair in _registered)
            {
                if ((pair.Key >> 16) != (int)area)
                {
                    continue;
                }

                int dbNumber = pair.Key & 0xFFFF;
                foreach (int address in pair.Value.Keys)
                {
                    ResetRegisterValues(area, dbNumber, address, 1);
                }
            }
        }

        #endregion
    }
}
