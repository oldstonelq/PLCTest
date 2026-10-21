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
    public class MelsecPlcServer : IPlcServer, IRegisterTable
    {
        /// <summary>
        /// 服务端通讯接口
        /// </summary>
        private readonly IServerCommunication _comm;

        /// <summary>
        /// 模拟PLC内存访问锁
        /// </summary>
        private readonly object _memoryLock = new object();

        /// <summary>
        /// 服务端协议接口（负责报文解析与构建）
        /// </summary>
        private readonly IServerProtocol _protocol;

        /// <summary>
        /// D 寄存器（字设备）
        /// </summary>
        private readonly ConcurrentDictionary<int, short> dRegisters = new ConcurrentDictionary<int, short>();

        /// <summary>
        /// M/X/Y 线圈（位设备，共用一张位表）
        /// </summary>
        private readonly ConcurrentDictionary<int, bool> mBits = new ConcurrentDictionary<int, bool>();

        /// <summary>
        /// 服务是否开启
        /// </summary>
        public bool ServerIsOpen => _comm != null && _comm.IsRunning;

        /// <summary>
        /// 连接客户端数量
        /// </summary>
        public int ClientCount => _comm != null ? _comm.ClientCount : 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comm">服务端通讯方式</param>
        /// <param name="protocol">协议实现</param>
        public MelsecPlcServer(IServerCommunication comm, IServerProtocol protocol)
        {
            _comm = comm ?? throw new ArgumentNullException(nameof(comm));
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
        }

        /// <summary>
        /// 开启服务端
        /// </summary>
        public void OpenServer()
        {
            if (_comm.IsRunning) return;
            _comm.OnDataReceived += OnDataReceived;
            _comm.Start();
        }

        /// <summary>
        /// 关闭服务端
        /// </summary>
        public void CloseServer()
        {
            if (!_comm.IsRunning) return;
            _comm.OnDataReceived -= OnDataReceived;
            _comm.Stop();
        }

        /// <summary>
        /// 客户端数据到达时的处理（委托协议解析，服务层只负责业务逻辑）
        /// </summary>
        private void OnDataReceived(byte[] byt, IClientSession client)
        {
            try
            {
                if (!_protocol.ValidateRequestHeader(byt)) return;

                var request = _protocol.ParseRequest(byt);
                if (request.Command == 0)
                {
                    // 解析失败也要回结束码，避免客户端一直等到超时
                    byte[] errData = _protocol.BuildEndCodeResponse(0x0001);
                    byte[] errHeader = _protocol.BuildResponseHeader(byt, errData.Length);
                    byte[] errResp = new byte[errHeader.Length + errData.Length];
                    Array.Copy(errHeader, 0, errResp, 0, errHeader.Length);
                    Array.Copy(errData, 0, errResp, errHeader.Length, errData.Length);
                    _comm.SendToClient(errResp, client);
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
                _comm.SendToClient(resp, client);
            }
            catch
            {
                // 忽略解析异常，避免影响接收循环
            }
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
        /// 处理批量读取——从寄存器读取数据，返回「结束码 + 数据」的数据区
        /// </summary>
        private byte[] HandleRead(RequestInfo request)
        {
            byte[] payload;
            switch (request.DeviceArea)
            {
                case MemoryArea.D: // D 寄存器
                    {
                        short[] words = new short[request.Points];
                        for (int i = 0; i < request.Points; i++)
                            dRegisters.TryGetValue(request.StartAddress + i, out words[i]);
                        payload = _protocol.BuildWordReadResponse(words);
                        break;
                    }
                case MemoryArea.M: // M 线圈
                case MemoryArea.X: // X 输入位（与 M 共用位表）
                case MemoryArea.Y: // Y 输出位（与 M 共用位表）
                    {
                        bool[] bits = new bool[request.Points];
                        for (int i = 0; i < request.Points; i++)
                            mBits.TryGetValue(request.StartAddress + i, out bits[i]);
                        payload = _protocol.BuildBitReadResponse(bits);
                        break;
                    }
                default:
                    return _protocol.BuildEndCodeResponse(0x0001);
            }
            return Concat(_protocol.BuildEndCodeResponse(0x0000), payload);
        }

        /// <summary>
        /// 处理批量写入——解析写入数据并存入寄存器，返回「结束码」数据区
        /// </summary>
        private byte[] HandleWrite(RequestInfo request)
        {
            switch (request.DeviceArea)
            {
                case MemoryArea.D: // D 寄存器
                    {
                        short[] words = _protocol.ParseWriteWords(request.WriteData, request.Points);
                        if (words != null)
                        {
                            for (int i = 0; i < request.Points && i < words.Length; i++)
                                dRegisters[request.StartAddress + i] = words[i];
                        }
                        return _protocol.BuildEndCodeResponse(0x0000);
                    }
                case MemoryArea.M: // M 线圈
                case MemoryArea.X: // X 输入位
                case MemoryArea.Y: // Y 输出位
                    {
                        bool[] bits = _protocol.ParseWriteBits(request.WriteData, request.Points);
                        if (bits != null)
                        {
                            for (int i = 0; i < request.Points && i < bits.Length; i++)
                                mBits[request.StartAddress + i] = bits[i];
                        }
                        return _protocol.BuildEndCodeResponse(0x0000);
                    }
                default:
                    return _protocol.BuildEndCodeResponse(0x0001);
            }
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
        /// 读取单个位寄存器的值。
        /// </summary>
        /// <param name="address">统一地址模型。</param>
        /// <returns>读取结果。</returns>
        public Result<bool> ReadBit(PlcAddress address)
        {
            try
            {
                if (address.Area == MemoryArea.M || address.Area == MemoryArea.X || address.Area == MemoryArea.Y)
                {
                    if (mBits.TryGetValue(address.Address, out bool val))
                        return Result<bool>.Success(val);
                    return Result<bool>.Success(false);
                }
                return Result<bool>.Fail($"MemoryArea.{address.Area} 不支持位读取");
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 读取位寄存器数组。
        /// </summary>
        /// <param name="address">统一地址模型，长度从 <see cref="PlcAddress.Length"/> 读取。</param>
        /// <returns>读取结果。</returns>
        public Result<bool[]> ReadBitArray(PlcAddress address)
        {
            try
            {
                if (address.Length <= 0)
                    return Result<bool[]>.Fail("读取长度必须大于0");

                if (address.Area == MemoryArea.M || address.Area == MemoryArea.X || address.Area == MemoryArea.Y)
                {
                    var result = new bool[address.Length];
                    for (int i = 0; i < address.Length; i++)
                    {
                        mBits.TryGetValue(address.Address + i, out bool val);
                        result[i] = val;
                    }
                    return Result<bool[]>.Success(result);
                }
                return Result<bool[]>.Fail($"MemoryArea.{address.Area} 不支持位数组读取");
            }
            catch (Exception ex)
            {
                return Result<bool[]>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 写入单个位寄存器。
        /// </summary>
        /// <param name="address">统一地址模型。</param>
        /// <param name="value">写入值。</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteBit(PlcAddress address, bool value)
        {
            try
            {
                if (address.Area == MemoryArea.M || address.Area == MemoryArea.X || address.Area == MemoryArea.Y)
                {
                    lock (_memoryLock)
                    {
                        mBits[address.Address] = value;
                    }
                    return Result<bool>.Success(true);
                }
                return Result<bool>.Fail($"MemoryArea.{address.Area} 不支持位写入");
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 写入位寄存器数组。
        /// </summary>
        /// <param name="address">统一地址模型。</param>
        /// <param name="values">位数据数组。</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteBitArray(PlcAddress address, bool[] values)
        {
            try
            {
                if (values == null || values.Length == 0)
                    return Result<bool>.Fail("写入数组不能为空");

                if (address.Area == MemoryArea.M || address.Area == MemoryArea.X || address.Area == MemoryArea.Y)
                {
                    lock (_memoryLock)
                    {
                        for (int i = 0; i < values.Length; i++)
                        {
                            mBits[address.Address + i] = values[i];
                        }
                    }
                    return Result<bool>.Success(true);
                }
                return Result<bool>.Fail($"MemoryArea.{address.Area} 不支持位数组写入");
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 读取单个字寄存器的值。
        /// </summary>
        /// <param name="address">统一地址模型。</param>
        /// <returns>读取结果。</returns>
        public Result<short> ReadWord(PlcAddress address)
        {
            try
            {
                if (address.Area == MemoryArea.D)
                {
                    dRegisters.TryGetValue(address.Address, out short val);
                    return Result<short>.Success(val);
                }
                return Result<short>.Fail($"MemoryArea.{address.Area} 不支持字读取");
            }
            catch (Exception ex)
            {
                return Result<short>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 读取字寄存器数组。
        /// </summary>
        /// <param name="address">统一地址模型，长度从 <see cref="PlcAddress.Length"/> 读取。</param>
        /// <returns>读取结果。</returns>
        public Result<short[]> ReadWordArray(PlcAddress address)
        {
            try
            {
                if (address.Length <= 0)
                    return Result<short[]>.Fail("读取长度必须大于0");

                if (address.Area == MemoryArea.D)
                {
                    var result = new short[address.Length];
                    for (int i = 0; i < address.Length; i++)
                    {
                        dRegisters.TryGetValue(address.Address + i, out short val);
                        result[i] = val;
                    }
                    return Result<short[]>.Success(result);
                }
                return Result<short[]>.Fail($"MemoryArea.{address.Area} 不支持字数组读取");
            }
            catch (Exception ex)
            {
                return Result<short[]>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 写入单个字寄存器。
        /// </summary>
        /// <param name="address">统一地址模型。</param>
        /// <param name="value">写入值。</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteWord(PlcAddress address, short value)
        {
            try
            {
                if (address.Area == MemoryArea.D)
                {
                    lock (_memoryLock)
                    {
                        dRegisters[address.Address] = value;
                    }
                    return Result<bool>.Success(true);
                }
                return Result<bool>.Fail($"MemoryArea.{address.Area} 不支持字写入");
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 写入字寄存器数组。
        /// </summary>
        /// <param name="address">统一地址模型。</param>
        /// <param name="values">字数据数组。</param>
        /// <returns>写入结果。</returns>
        public Result<bool> WriteWordArray(PlcAddress address, short[] values)
        {
            try
            {
                if (values == null || values.Length == 0)
                    return Result<bool>.Fail("写入数组不能为空");

                if (address.Area == MemoryArea.D)
                {
                    lock (_memoryLock)
                    {
                        for (int i = 0; i < values.Length; i++)
                        {
                            dRegisters[address.Address + i] = values[i];
                        }
                    }
                    return Result<bool>.Success(true);
                }
                return Result<bool>.Fail($"MemoryArea.{address.Area} 不支持字数组写入");
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        #region 辅助方法（供UI层使用）
        /// <summary>
        /// 获取指定区域的寄存器数量
        /// </summary>
        public int GetRegisterCount(MemoryArea area)
        {
            switch (area)
            {
                case MemoryArea.D:
                    return dRegisters.Count;
                case MemoryArea.M:
                case MemoryArea.X:
                case MemoryArea.Y:
                    return mBits.Count;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 检查指定地址是否存在寄存器
        /// </summary>
        public bool ContainsRegister(MemoryArea area, int address)
        {
            if (area == MemoryArea.D)
                return dRegisters.ContainsKey(address);
            return mBits.ContainsKey(address);
        }

        /// <summary>
        /// 尝试添加寄存器（仅在不存在时添加）
        /// </summary>
        public bool TryAddRegister(MemoryArea area, int address)
        {
            if (address < 0)
                return false;

            if (area == MemoryArea.D)
                return dRegisters.TryAdd(address, 0);
            return mBits.TryAdd(address, false);
        }

        /// <summary>
        /// 清空指定区域的所有寄存器
        /// </summary>
        public void ClearRegisters(MemoryArea area)
        {
            if (area == MemoryArea.D)
                dRegisters.Clear();
            else
                mBits.Clear();
        }

        /// <summary>
        /// 清空指定区域指定范围内的寄存器值（设为默认值，不清除Key）
        /// </summary>
        public void ResetRegisterValues(MemoryArea area, int start, int length)
        {
            lock (_memoryLock)
            {
                if (area == MemoryArea.D)
                {
                    for (int i = start; i < start + length; i++)
                    {
                        if (dRegisters.ContainsKey(i))
                            dRegisters[i] = 0;
                    }
                }
                else
                {
                    for (int i = start; i < start + length; i++)
                    {
                        if (mBits.ContainsKey(i))
                            mBits[i] = false;
                    }
                }
            }
        }

        /// <summary>
        /// 重置指定区域所有寄存器的值
        /// </summary>
        public void ResetAllRegisterValues(MemoryArea area)
        {
            lock (_memoryLock)
            {
                if (area == MemoryArea.D)
                {
                    foreach (var key in dRegisters.Keys)
                    {
                        dRegisters[key] = 0;
                    }
                }
                else
                {
                    foreach (var key in mBits.Keys)
                    {
                        mBits[key] = false;
                    }
                }
            }
        }
        #endregion
    }
}
