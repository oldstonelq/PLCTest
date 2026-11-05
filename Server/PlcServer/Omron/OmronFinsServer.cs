using PlcTest.Protocols;
using PlcTest.Models;
using PlcTest.Server.ServerInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace PlcTest.Server.PlcServer.Omron
{
    /// <summary>
    /// 欧姆龙 FINS/TCP 从站模拟服务端。
    /// 内部按「区域 + EM 存储体」存放位表与字表，区码解析复用客户端的
    /// <see cref="OmronFinsDeviceCatalog"/>，因此客户端认得的软元件服务端都模拟。
    /// </summary>
    public class OmronFinsServer : IPlcServer, IRegisterTable
    {
        private readonly IServerCommunication _comm;
        private readonly IOmronFinsServerProtocol _protocol;
        private readonly object _memoryLock = new object();

        /// <summary>位表：key = 区域与存储体编码</summary>
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, bool>> _bitTables
            = new ConcurrentDictionary<int, ConcurrentDictionary<int, bool>>();
        /// <summary>字表：key = 区域与存储体编码</summary>
        private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, short>> _wordTables
            = new ConcurrentDictionary<int, ConcurrentDictionary<int, short>>();

        /// <summary>PLC 自身节点号（握手时告知客户端）</summary>
        public byte LocalNode { get; set; }
        /// <summary>分配给客户端的节点号</summary>
        public byte RemoteNode { get; set; }

        /// <summary>服务是否在运行</summary>
        public bool ServerIsOpen
        {
            get { return _comm != null && _comm.IsRunning; }
        }

        /// <summary>已连接客户端数量</summary>
        public int ClientCount
        {
            get { return _comm != null ? _comm.ClientCount : 0; }
        }
        /// <summary>
        /// 本服务端支持的软元件区域：与客户端共用同一份 FINS 软元件表。
        /// </summary>
        public IReadOnlyList<MemoryArea> GetSupportedAreas()
        {
            return OmronFinsDeviceCatalog.All.Select(spec => spec.Area).ToList();
        }
        /// <summary>通讯日志事件（与客户端 <c>IPlcClient.ShowCommMessageEvent</c> 对称）</summary>
        public event Action<string> ShowCommMessageEvent;

        /// <summary>触发通讯日志；事件在协议线程上触发，订阅方自己切回 UI 线程。</summary>
        private void LogComm(string message)
        {
            Action<string> handler = ShowCommMessageEvent;
            if (handler != null)
            {
                try
                {
                    handler(message);
                }
                catch
                {
                    // 日志订阅方的异常不能影响报文处理
                }
            }
        }

        public OmronFinsServer(IServerCommunication comm, IOmronFinsServerProtocol protocol)
        {
            _comm = comm ?? throw new ArgumentNullException(nameof(comm));
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            LocalNode = 1;
            RemoteNode = 2;

            _comm.FrameLengthResolver = _protocol.GetRequestFrameLength;
        }

        /// <summary>开启服务端</summary>
        public void OpenServer()
        {
            if (_comm.IsRunning) return;
            _comm.OnDataReceived += OnDataReceived;
            _comm.Start();
            LogComm("服务端已启动，等待客户端连接");
        }

        /// <summary>关闭服务端</summary>
        public void CloseServer()
        {
            if (!_comm.IsRunning) return;
            _comm.OnDataReceived -= OnDataReceived;
            _comm.Stop();
            LogComm("服务端已停止");
        }

        #region 请求处理

        private void OnDataReceived(byte[] data, IClientSession client)
        {
            try
            {
                // 握手：节点地址请求，回 24 字节
                if (_protocol.IsHandshakeRequest(data))
                {
                    LogComm("节点地址请求: " + BitConverter.ToString(data));
                    byte[] handshake = _protocol.BuildHandshakeResponse(LocalNode, RemoteNode);
                    _comm.SendToClient(handshake, client);
                    LogComm("发送报文: " + BitConverter.ToString(handshake));
                    return;
                }

                OmronFinsRequestInfo request = _protocol.ParseRequest(data);
                if (request == null)
                {
                    // 帧格式非法：FINS 从站不响应，让主站超时重发
                    LogComm("报文格式非法，已丢弃（不回包）: " + BitConverter.ToString(data));
                    return;
                }

                LogComm("接收报文: " + BitConverter.ToString(data));

                byte[] response;
                try
                {
                    response = ProcessRequest(request);
                }
                catch
                {
                    response = _protocol.BuildErrorResponse(request, 0x0001);
                }

                _comm.SendToClient(response, client);
                LogComm("发送报文: " + BitConverter.ToString(response));
            }
            catch (Exception ex)
            {
                LogComm("请求处理异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 按指令代码分发。结束代码：0x1101 区域代码不正确、0x1103 地址溢出、0x1104 长度不符。
        /// </summary>
        private byte[] ProcessRequest(OmronFinsRequestInfo request)
        {
            OmronFinsAreaResolution area = OmronFinsDeviceCatalog.Resolve(request.AreaCode, request.WordAddress);
            if (area == null)
            {
                return _protocol.BuildErrorResponse(request, 0x1101);
            }
            if (request.Points == 0)
            {
                return _protocol.BuildErrorResponse(request, 0x1104);
            }

            int address = ResolveIndex(request, area);

            // 范围校验（位上限是「字编号 × 16 + 位号」，比字上限大）
            int maxIndex = area.IsBit ? area.Spec.MaxBitIndex : area.Spec.MaxWordIndex;
            if (maxIndex >= 0 && (address < 0 || address + request.Points - 1 > maxIndex))
            {
                return _protocol.BuildErrorResponse(request, 0x1103);
            }

            lock (_memoryLock)
            {
                if (request.CommandCode == 0x0101)
                {
                    return HandleRead(request, area, address);
                }
                if (request.CommandCode == 0x0102)
                {
                    return HandleWrite(request, area, address);
                }
                return _protocol.BuildErrorResponse(request, 0x0401);
            }
        }

        /// <summary>
        /// 把 FINS 的「字地址 + 位号」还原成统一地址模型里的编号。
        /// </summary>
        private static int ResolveIndex(OmronFinsRequestInfo request, OmronFinsAreaResolution area)
        {
            int word = request.WordAddress - area.AddressOffset;

            if (area.Spec.Addressing == OmronFinsAddressing.NumberOnly)
            {
                // 定时器 / 计数器：编号即地址
                return word;
            }
            if (area.IsBit)
            {
                return word * 16 + request.BitIndex;
            }
            return word;
        }

        private byte[] HandleRead(OmronFinsRequestInfo request, OmronFinsAreaResolution area, int address)
        {
            int key = TableKey(area);

            if (area.IsBit)
            {
                ConcurrentDictionary<int, bool> table = _bitTables.GetOrAdd(key, k => new ConcurrentDictionary<int, bool>());
                bool[] bits = new bool[request.Points];
                for (int i = 0; i < bits.Length; i++)
                {
                    table.TryGetValue(address + i, out bits[i]);
                }
                return _protocol.BuildReadBitResponse(request, bits);
            }

            ConcurrentDictionary<int, short> words = _wordTables.GetOrAdd(key, k => new ConcurrentDictionary<int, short>());
            short[] values = new short[request.Points];
            for (int i = 0; i < values.Length; i++)
            {
                words.TryGetValue(address + i, out values[i]);
            }
            return _protocol.BuildReadWordResponse(request, values);
        }

        private byte[] HandleWrite(OmronFinsRequestInfo request, OmronFinsAreaResolution area, int address)
        {
            int key = TableKey(area);

            if (area.IsBit)
            {
                if (request.WriteData == null || request.WriteData.Length < request.Points)
                {
                    return _protocol.BuildErrorResponse(request, 0x1104);
                }
                ConcurrentDictionary<int, bool> table = _bitTables.GetOrAdd(key, k => new ConcurrentDictionary<int, bool>());
                for (int i = 0; i < request.Points; i++)
                {
                    table[address + i] = request.WriteData[i] != 0;
                }
                return _protocol.BuildWriteResponse(request);
            }

            if (request.WriteData == null || request.WriteData.Length < request.Points * 2)
            {
                return _protocol.BuildErrorResponse(request, 0x1104);
            }
            ConcurrentDictionary<int, short> words = _wordTables.GetOrAdd(key, k => new ConcurrentDictionary<int, short>());
            for (int i = 0; i < request.Points; i++)
            {
                words[address + i] = (short)(request.WriteData[i * 2] | (request.WriteData[i * 2 + 1] << 8));
            }
            return _protocol.BuildWriteResponse(request);
        }

        /// <summary>
        /// 表 key = 区域 &lt;&lt; 8 | 存储体（非 EM 用 0xFF）。
        /// </summary>
        private static int TableKey(OmronFinsAreaResolution area)
        {
            int bank = area.Bank < 0 ? 0xFF : (area.Bank & 0xFF);
            return ((int)area.Spec.Area << 8) | bank;
        }

        private static int TableKey(MemoryArea memoryArea, int? dbNumber)
        {
            int bank = dbNumber.HasValue ? (dbNumber.Value & 0xFF) : 0xFF;
            return ((int)memoryArea << 8) | bank;
        }

        #endregion

        #region 软元件访问

        public Result<bool> ReadBit(PlcAddress address)
        {
            try
            {
                ConcurrentDictionary<int, bool> table = _bitTables.GetOrAdd(
                    TableKey(address.Area, address.DbNumber), k => new ConcurrentDictionary<int, bool>());
                bool value;
                table.TryGetValue(address.Address, out value);
                return Result<bool>.Success(value);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public Result<bool[]> ReadBitArray(PlcAddress address)
        {
            try
            {
                if (address.Length <= 0) return Result<bool[]>.Fail("读取长度必须大于0");

                ConcurrentDictionary<int, bool> table = _bitTables.GetOrAdd(
                    TableKey(address.Area, address.DbNumber), k => new ConcurrentDictionary<int, bool>());
                bool[] values = new bool[address.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    table.TryGetValue(address.Address + i, out values[i]);
                }
                return Result<bool[]>.Success(values);
            }
            catch (Exception ex)
            {
                return Result<bool[]>.Fail(ex.Message);
            }
        }

        public Result<bool> WriteBit(PlcAddress address, bool value)
        {
            try
            {
                _bitTables.GetOrAdd(TableKey(address.Area, address.DbNumber),
                    k => new ConcurrentDictionary<int, bool>())[address.Address] = value;
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public Result<bool> WriteBitArray(PlcAddress address, bool[] values)
        {
            try
            {
                if (values == null || values.Length == 0) return Result<bool>.Fail("写入数据为空");

                ConcurrentDictionary<int, bool> table = _bitTables.GetOrAdd(
                    TableKey(address.Area, address.DbNumber), k => new ConcurrentDictionary<int, bool>());
                for (int i = 0; i < values.Length; i++)
                {
                    table[address.Address + i] = values[i];
                }
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public Result<short> ReadWord(PlcAddress address)
        {
            try
            {
                ConcurrentDictionary<int, short> table = _wordTables.GetOrAdd(
                    TableKey(address.Area, address.DbNumber), k => new ConcurrentDictionary<int, short>());
                short value;
                table.TryGetValue(address.Address, out value);
                return Result<short>.Success(value);
            }
            catch (Exception ex)
            {
                return Result<short>.Fail(ex.Message);
            }
        }

        public Result<short[]> ReadWordArray(PlcAddress address)
        {
            try
            {
                if (address.Length <= 0) return Result<short[]>.Fail("读取长度必须大于0");

                ConcurrentDictionary<int, short> table = _wordTables.GetOrAdd(
                    TableKey(address.Area, address.DbNumber), k => new ConcurrentDictionary<int, short>());
                short[] values = new short[address.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    table.TryGetValue(address.Address + i, out values[i]);
                }
                return Result<short[]>.Success(values);
            }
            catch (Exception ex)
            {
                return Result<short[]>.Fail(ex.Message);
            }
        }

        public Result<bool> WriteWord(PlcAddress address, short value)
        {
            try
            {
                _wordTables.GetOrAdd(TableKey(address.Area, address.DbNumber),
                    k => new ConcurrentDictionary<int, short>())[address.Address] = value;
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        public Result<bool> WriteWordArray(PlcAddress address, short[] values)
        {
            try
            {
                if (values == null || values.Length == 0) return Result<bool>.Fail("写入数据为空");

                ConcurrentDictionary<int, short> table = _wordTables.GetOrAdd(
                    TableKey(address.Area, address.DbNumber), k => new ConcurrentDictionary<int, short>());
                for (int i = 0; i < values.Length; i++)
                {
                    table[address.Address + i] = values[i];
                }
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        #endregion

        #region 寄存器表

        public int GetRegisterCount(MemoryArea area)
        {
            int total = 0;
            foreach (var pair in _bitTables)
            {
                if ((pair.Key >> 8) == (int)area) total += pair.Value.Count;
            }
            foreach (var pair in _wordTables)
            {
                if ((pair.Key >> 8) == (int)area) total += pair.Value.Count;
            }
            return total;
        }

        public bool ContainsRegister(MemoryArea area, int address)
        {
            foreach (var pair in _bitTables)
            {
                if ((pair.Key >> 8) == (int)area && pair.Value.ContainsKey(address)) return true;
            }
            foreach (var pair in _wordTables)
            {
                if ((pair.Key >> 8) == (int)area && pair.Value.ContainsKey(address)) return true;
            }
            return false;
        }

        public bool TryAddRegister(MemoryArea area, int address)
        {
            if (address < 0) return false;

            bool addedBit = _bitTables.GetOrAdd(TableKey(area, null), k => new ConcurrentDictionary<int, bool>())
                .TryAdd(address, false);
            bool addedWord = _wordTables.GetOrAdd(TableKey(area, null), k => new ConcurrentDictionary<int, short>())
                .TryAdd(address, 0);
            return addedBit || addedWord;
        }

        public void ClearRegisters(MemoryArea area)
        {
            foreach (var pair in _bitTables)
            {
                if ((pair.Key >> 8) == (int)area) pair.Value.Clear();
            }
            foreach (var pair in _wordTables)
            {
                if ((pair.Key >> 8) == (int)area) pair.Value.Clear();
            }
        }

        public void ResetRegisterValues(MemoryArea area, int start, int length)
        {
            if (length <= 0) return;

            // 只改已存在的地址，别用索引器把区间内的地址顺手创建出来
            int end = start + length;
            foreach (var pair in _bitTables)
            {
                if ((pair.Key >> 8) != (int)area) continue;
                foreach (int key in pair.Value.Keys)
                {
                    if (key >= start && key < end) pair.Value[key] = false;
                }
            }
            foreach (var pair in _wordTables)
            {
                if ((pair.Key >> 8) != (int)area) continue;
                foreach (int key in pair.Value.Keys)
                {
                    if (key >= start && key < end) pair.Value[key] = 0;
                }
            }
        }

        /// <summary>
        /// 把指定区域（含 EM 各存储体）的位表与字表全部恢复初始值（**保留地址**）。
        /// 与 <see cref="ClearRegisters"/> 的区别：那个移除地址，这个只把值清零，
        /// 否则界面的地址列表会整片消失、客户端也读不到原本声明过的地址。
        /// </summary>
        public void ResetAllRegisterValues(MemoryArea area)
        {
            foreach (var pair in _bitTables)
            {
                if ((pair.Key >> 8) != (int)area) continue;
                foreach (int key in pair.Value.Keys)
                {
                    pair.Value[key] = false;
                }
            }
            foreach (var pair in _wordTables)
            {
                if ((pair.Key >> 8) != (int)area) continue;
                foreach (int key in pair.Value.Keys)
                {
                    pair.Value[key] = 0;
                }
            }
        }

        #endregion
    }
}
