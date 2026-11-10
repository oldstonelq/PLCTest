using PlcTest.Models;
using PlcTest.Server.ServerInterface;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace PlcTest.Server.PlcServer.Melsec
{
    /// <summary>
    /// 三菱 MC 协议模拟服务端通用实现。
    /// 寄存器表、读写分发、批量请求处理都在这里；帧结构的差异（1E/3E/4E、二进制/ASCII）
    /// 全部交给注入的 <see cref="IServerProtocol"/>。
    /// </summary>
    public class MelsecPlcServer : PlcServerBase
    {
        /// <summary>
        /// 模拟PLC内存访问锁
        /// </summary>
        private readonly object _memoryLock = new object();

        /// <summary>
        /// 单次请求允许的最大点数（防止异常报文申请超大数组）
        /// </summary>
        private const int MaxPointsPerRequest = 2000;

        /// <summary>
        /// 服务端协议接口（负责报文解析与构建）
        /// </summary>
        private readonly IServerProtocol _protocol;

        /// <summary>
        /// 字设备表：按软元件区域分开存（D / SD / R / TN / W / ZR …）
        /// </summary>
        private readonly ConcurrentDictionary<MemoryArea, ConcurrentDictionary<int, short>> _wordTables
            = new ConcurrentDictionary<MemoryArea, ConcurrentDictionary<int, short>>();

        /// <summary>
        /// 位设备表：按软元件区域分开存（M / X / Y / TS / B …）
        /// 同一区域既能按位又能按字时（如 M），位与字各存一份、互不联动。
        /// </summary>
        private readonly ConcurrentDictionary<MemoryArea, ConcurrentDictionary<int, bool>> _bitTables
            = new ConcurrentDictionary<MemoryArea, ConcurrentDictionary<int, bool>>();

        /// <summary>取（必要时创建）某个区域的字表</summary>
        private ConcurrentDictionary<int, short> WordTable(MemoryArea area)
        {
            return _wordTables.GetOrAdd(area, key => new ConcurrentDictionary<int, short>());
        }

        /// <summary>取（必要时创建）某个区域的位表</summary>
        private ConcurrentDictionary<int, bool> BitTable(MemoryArea area)
        {
            return _bitTables.GetOrAdd(area, key => new ConcurrentDictionary<int, bool>());
        }

        /// <summary>
        /// 本服务端支持的软元件区域：直接问协议（1E 用 1E 的表、3E/4E 用 3E4E 的表）。
        /// </summary>
        public override IReadOnlyList<MemoryArea> GetSupportedAreas()
        {
            return _protocol.GetSupportedAreas();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comm">服务端通讯方式</param>
        /// <param name="protocol">协议实现</param>
        public MelsecPlcServer(IServerCommunication comm, IServerProtocol protocol)
            : base(comm)
        {
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));

            // 把"这一帧有多长"的判断交给协议，通讯层不再写死某一种帧头
            Communication.FrameLengthResolver = _protocol.GetRequestFrameLength;
        }

        /// <summary>
        /// 客户端数据到达时的处理（委托协议解析，服务层只负责业务逻辑）
        /// </summary>
        protected override void HandleFrame(byte[] byt, IClientSession client)
        {
            if (!_protocol.ValidateRequestHeader(byt))
            {
                LogFrame(false, byt, "报文头不属于本协议，已丢弃");
                return;
            }

            LogFrame(false, byt);

            var request = _protocol.ParseRequest(byt);
            if (request.Command == 0)
            {
                LogComm("请求解析失败，回结束码 0x0001");
                // 解析失败也要回结束码，避免客户端一直等到超时
                byte[] errData = _protocol.BuildEndCodeResponse(0x0001);
                byte[] errHeader = _protocol.BuildResponseHeader(byt, errData.Length);
                byte[] errResp = new byte[errHeader.Length + errData.Length];
                Array.Copy(errHeader, 0, errResp, 0, errHeader.Length);
                Array.Copy(errData, 0, errResp, errHeader.Length, errData.Length);
                Communication.SendToClient(errResp, client);
                LogFrame(true, errResp);
                return;
            }

            byte[] responseData;
            try
            {
                responseData = ProcessRequest(request);
            }
            catch
            {
                responseData = _protocol.BuildEndCodeResponse(0x0001);
            }

            byte[] respHeader = _protocol.BuildResponseHeader(byt, responseData.Length);
            byte[] resp = new byte[respHeader.Length + responseData.Length];
            Array.Copy(respHeader, 0, resp, 0, respHeader.Length);
            Array.Copy(responseData, 0, resp, respHeader.Length, responseData.Length);
            Communication.SendToClient(resp, client);
            LogFrame(true, resp);
        }

        /// <summary>
        /// 根据命令码分发处理
        /// </summary>
        private byte[] ProcessRequest(RequestInfo request)
        {
            lock (_memoryLock)
            {
                switch (request.Command)
                {
                    case 0x0401: return HandleRead(request);
                    case 0x1401: return HandleWrite(request);
                    default: return _protocol.BuildEndCodeResponse(0x0001);
                }
            }
        }

        /// <summary>
        /// 处理批量读取——按请求的位/字与软元件区域读对应表，返回「结束码 + 数据」的数据区
        /// </summary>
        private byte[] HandleRead(RequestInfo request)
        {
            if (!request.DeviceArea.HasValue)
            {
                return _protocol.BuildEndCodeResponse(0x0001);
            }
            if (request.Points == 0 || request.Points > MaxPointsPerRequest)
            {
                return _protocol.BuildEndCodeResponse(0x0001);
            }

            MemoryArea area = request.DeviceArea.Value;
            byte[] payload;
            if (request.IsBit)
            {
                var table = BitTable(area);
                bool[] bits = new bool[request.Points];
                for (int i = 0; i < request.Points; i++)
                {
                    table.TryGetValue(request.StartAddress + i, out bits[i]);
                }
                payload = _protocol.BuildBitReadResponse(bits);
            }
            else
            {
                var table = WordTable(area);
                short[] words = new short[request.Points];
                for (int i = 0; i < request.Points; i++)
                {
                    table.TryGetValue(request.StartAddress + i, out words[i]);
                }
                payload = _protocol.BuildWordReadResponse(words);
            }
            return Concat(_protocol.BuildEndCodeResponse(0x0000), payload);
        }

        /// <summary>
        /// 处理批量写入——按请求的位/字与软元件区域写入对应表，返回「结束码」数据区
        /// </summary>
        private byte[] HandleWrite(RequestInfo request)
        {
            if (!request.DeviceArea.HasValue)
            {
                return _protocol.BuildEndCodeResponse(0x0001);
            }

            MemoryArea area = request.DeviceArea.Value;
            if (request.IsBit)
            {
                bool[] bits = _protocol.ParseWriteBits(request.WriteData, request.Points);
                if (bits != null)
                {
                    var table = BitTable(area);
                    for (int i = 0; i < request.Points && i < bits.Length; i++)
                    {
                        table[request.StartAddress + i] = bits[i];
                    }
                }
                return _protocol.BuildEndCodeResponse(0x0000);
            }

            short[] words = _protocol.ParseWriteWords(request.WriteData, request.Points);
            if (words != null)
            {
                var table = WordTable(area);
                for (int i = 0; i < request.Points && i < words.Length; i++)
                {
                    table[request.StartAddress + i] = words[i];
                }
            }
            return _protocol.BuildEndCodeResponse(0x0000);
        }

        /// <summary>
        /// 拼接两段字节数组
        /// </summary>
        private static byte[] Concat(byte[] a, byte[] b)
        {
            if (a == null || a.Length == 0) return b ?? new byte[0];
            if (b == null || b.Length == 0) return a;
            byte[] result = new byte[a.Length + b.Length];
            Array.Copy(a, 0, result, 0, a.Length);
            Array.Copy(b, 0, result, a.Length, b.Length);
            return result;
        }

        /// <summary>
        /// 读取单个位。
        /// </summary>
        /// <param name="address">统一地址模型。</param>
        /// <returns>读取结果。</returns>
        public override Result<bool> ReadBit(PlcAddress address)
        {
            try
            {
                bool val;
                BitTable(address.Area).TryGetValue(address.Address, out val);
                return Result<bool>.Success(val);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 读取位数组。
        /// </summary>
        /// <param name="address">统一地址模型，长度从 <see cref="PlcAddress.Length"/> 读取。</param>
        /// <returns>读取结果。</returns>
        public override Result<bool[]> ReadBitArray(PlcAddress address)
        {
            try
            {
                if (address.Length <= 0)
                {
                    return Result<bool[]>.Fail("读取长度必须大于0");
                }

                var table = BitTable(address.Area);
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

        /// <summary>
        /// 写入单个位。
        /// </summary>
        public override Result<bool> WriteBit(PlcAddress address, bool value)
        {
            try
            {
                BitTable(address.Area)[address.Address] = value;
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 写入位数组。
        /// </summary>
        public override Result<bool> WriteBitArray(PlcAddress address, bool[] values)
        {
            try
            {
                if (values == null || values.Length == 0)
                {
                    return Result<bool>.Fail("写入数据为空");
                }

                var table = BitTable(address.Area);
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

        /// <summary>
        /// 读取单个字。
        /// </summary>
        public override Result<short> ReadWord(PlcAddress address)
        {
            try
            {
                short val;
                WordTable(address.Area).TryGetValue(address.Address, out val);
                return Result<short>.Success(val);
            }
            catch (Exception ex)
            {
                return Result<short>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 读取字数组。
        /// </summary>
        public override Result<short[]> ReadWordArray(PlcAddress address)
        {
            try
            {
                if (address.Length <= 0)
                {
                    return Result<short[]>.Fail("读取长度必须大于0");
                }

                var table = WordTable(address.Area);
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

        /// <summary>
        /// 写入单个字。
        /// </summary>
        public override Result<bool> WriteWord(PlcAddress address, short value)
        {
            try
            {
                WordTable(address.Area)[address.Address] = value;
                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 写入字数组。
        /// </summary>
        public override Result<bool> WriteWordArray(PlcAddress address, short[] values)
        {
            try
            {
                if (values == null || values.Length == 0)
                {
                    return Result<bool>.Fail("写入数据为空");
                }

                var table = WordTable(address.Area);
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

        #region 辅助方法（供UI层使用）
        /// <summary>
        /// 获取指定区域已建立的寄存器数量（位表优先）
        /// </summary>
        public override int GetRegisterCount(MemoryArea area)
        {
            ConcurrentDictionary<int, bool> bits;
            if (_bitTables.TryGetValue(area, out bits) && bits.Count > 0)
            {
                return bits.Count;
            }
            ConcurrentDictionary<int, short> words;
            if (_wordTables.TryGetValue(area, out words))
            {
                return words.Count;
            }
            return 0;
        }

        /// <summary>
        /// 检查指定地址在该区域的位表或字表中是否存在
        /// </summary>
        public override bool ContainsRegister(MemoryArea area, int address)
        {
            ConcurrentDictionary<int, bool> bits;
            if (_bitTables.TryGetValue(area, out bits) && bits.ContainsKey(address))
            {
                return true;
            }
            ConcurrentDictionary<int, short> words;
            return _wordTables.TryGetValue(area, out words) && words.ContainsKey(address);
        }

        /// <summary>
        /// 尝试添加寄存器（位表与字表各加一个初始值）
        /// </summary>
        public override bool TryAddRegister(MemoryArea area, int address)
        {
            if (address < 0)
            {
                return false;
            }
            bool addedBit = BitTable(area).TryAdd(address, false);
            bool addedWord = WordTable(area).TryAdd(address, 0);
            return addedBit || addedWord;
        }

        /// <summary>
        /// 清空指定区域的位表与字表
        /// </summary>
        public override void ClearRegisters(MemoryArea area)
        {
            ConcurrentDictionary<int, bool> bits;
            if (_bitTables.TryGetValue(area, out bits))
            {
                bits.Clear();
            }
            ConcurrentDictionary<int, short> words;
            if (_wordTables.TryGetValue(area, out words))
            {
                words.Clear();
            }
        }

        /// <summary>
        /// 把指定区间内的寄存器值恢复为初始值（保留 Key）
        /// </summary>
        public override void ResetRegisterValues(MemoryArea area, int start, int length)
        {
            if (length <= 0)
            {
                return;
            }

            // 只改已存在的地址：用索引器直接赋值会把区间内的地址"顺手创建"出来，
            // 地址集合一变，界面行数和 GetRegisterCount 都跟着漂
            int end = start + length;
            ConcurrentDictionary<int, bool> bits;
            if (_bitTables.TryGetValue(area, out bits))
            {
                foreach (int key in bits.Keys)
                {
                    if (key >= start && key < end)
                    {
                        bits[key] = false;
                    }
                }
            }
            ConcurrentDictionary<int, short> words;
            if (_wordTables.TryGetValue(area, out words))
            {
                foreach (int key in words.Keys)
                {
                    if (key >= start && key < end)
                    {
                        words[key] = 0;
                    }
                }
            }
        }

        /// <summary>
        /// 把指定区域的位表与字表全部恢复初始值（**保留地址**）。
        /// 与 <see cref="ClearRegisters"/> 的区别：那个是移除地址本身，这个只把值清零。
        /// 若这里图省事调 ClearRegisters，地址会整片消失，界面表格随之清空，
        /// 客户端也会读不到原本声明过的地址——这正是"ClearAll 把寄存器删掉"那个 bug。
        /// </summary>
        public override void ResetAllRegisterValues(MemoryArea area)
        {
            ConcurrentDictionary<int, bool> bits;
            if (_bitTables.TryGetValue(area, out bits))
            {
                foreach (int key in bits.Keys)
                {
                    bits[key] = false;
                }
            }
            ConcurrentDictionary<int, short> words;
            if (_wordTables.TryGetValue(area, out words))
            {
                foreach (int key in words.Keys)
                {
                    words[key] = 0;
                }
            }
        }
        #endregion
    }
}
