
using PLCTest.Models;
using PLCTest.SeverInterface;
using PLCTest.SeverProtocolManagement.Melsec;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using static PLCTest.Models.Enums;

namespace PLCTest.PLCSever.Melsec
{
    /// <summary>
    /// 三菱PLC-MC协议模拟服务端（3E帧数据结构）
    /// </summary>
    public class MelsecMc3EPLCSever : IPLCServer
    {
        /// <summary>
        /// 服务端通讯接口
        /// </summary>
        private readonly ISeverCommunication _comm;

        /// <summary>
        /// 模拟PLC内存访问锁
        /// </summary>
        private readonly object memoryLock = new object();

        /// <summary>
        /// 服务端协议接口（负责 MC 3E 报文解析与构建）
        /// </summary>
        private readonly ISeverProtocol _protocol;

        /// <summary>
        /// D 寄存器（字设备）
        /// </summary>
        private readonly ConcurrentDictionary<int, short> dRegisters = new ConcurrentDictionary<int, short>();

        /// <summary>
        /// M 线圈（位设备）
        /// </summary>
        private readonly ConcurrentDictionary<int, bool> mBits = new ConcurrentDictionary<int, bool>();

        /// <summary>
        /// 服务是否开启
        /// </summary>
        public bool SeverIsOpen => _comm != null && _comm.IsRunning;

        /// <summary>
        /// 连接客户端数量
        /// </summary>
        public int ClientCount => _comm != null ? _comm.ClientCount : 0;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="comm">服务端通讯方式</param>
        /// <param name="protocol">协议实现（可选，默认使用 MC 3E 协议）</param>
        public MelsecMc3EPLCSever(ISeverCommunication comm, ISeverProtocol protocol = null)
        {
            _comm = comm ?? throw new ArgumentNullException(nameof(comm));
            _protocol = protocol ?? new MelsecMc3ESeverProtocol();
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
        /// 客户端数据到达时的处理（委托协议解析，Service 层只负责业务逻辑）
        /// </summary>
        private void OnDataReceived(byte[] byt)
        {
            try
            {
                if (!_protocol.ValidateRequestHeader(byt)) return;

                var request = _protocol.ParseRequest(byt);
                if (request.Command == 0) return;

                byte[] responseData;
                try
                {
                    responseData = ProcessRequest(request);
                }
                catch
                {
                    responseData = new byte[2] { 0x01, 0x00 };
                }

                byte[] respHeader = _protocol.BuildResponseHeader(byt, responseData.Length);
                byte[] resp = new byte[respHeader.Length + responseData.Length];
                Array.Copy(respHeader, 0, resp, 0, respHeader.Length);
                Array.Copy(responseData, 0, resp, respHeader.Length, responseData.Length);
                _comm.SendToClient(resp);
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
            lock (memoryLock)
            {
                switch (request.Command)
                {
                    case 0x0401: return HandleRead(request);
                    case 0x1401: return HandleWrite(request);
                    default: return new byte[2] { 0x01, 0x00 };
                }
            }
        }

        /// <summary>
        /// 处理批量读取——从寄存器读取数据，委托协议构建响应字节
        /// </summary>
        private byte[] HandleRead(RequestInfo request)
        {
            switch (request.DeviceCode)
            {
                case 0xA8: // D 寄存器
                    {
                        short[] words = new short[request.Points];
                        for (int i = 0; i < request.Points; i++)
                            dRegisters.TryGetValue(request.StartAddress + i, out words[i]);
                        return _protocol.BuildWordReadResponse(words);
                    }
                case 0x90: // M 线圈
                    {
                        bool[] bits = new bool[request.Points];
                        for (int i = 0; i < request.Points; i++)
                            mBits.TryGetValue(request.StartAddress + i, out bits[i]);
                        return _protocol.BuildBitReadResponse(bits);
                    }
                default:
                    return new byte[2] { 0x01, 0x00 };
            }
        }

        /// <summary>
        /// 处理批量写入——解析写入数据并存入寄存器
        /// </summary>
        private byte[] HandleWrite(RequestInfo request)
        {
            switch (request.DeviceCode)
            {
                case 0xA8: // D 寄存器
                    if (request.WriteData != null)
                    {
                        for (int i = 0; i < request.Points; i++)
                        {
                            int off = i * 2;
                            if (request.WriteData.Length >= off + 2)
                                dRegisters[request.StartAddress + i] = BitConverter.ToInt16(request.WriteData, off);
                        }
                    }
                    return new byte[2] { 0x00, 0x00 };
                case 0x90: // M 线圈（位设备：每字节存 2 个位，高4位=偶数位，低4位=奇数位）
                    if (request.WriteData != null)
                    {
                        int byteCount = (request.Points + 1) / 2;
                        for (int bIdx = 0; bIdx < byteCount && bIdx < request.WriteData.Length; bIdx++)
                        {
                            int addr0 = request.StartAddress + bIdx * 2;
                            if (addr0 < request.StartAddress + request.Points)
                                mBits[addr0] = (request.WriteData[bIdx] & 0x10) != 0;

                            int addr1 = request.StartAddress + bIdx * 2 + 1;
                            if (addr1 < request.StartAddress + request.Points)
                                mBits[addr1] = (request.WriteData[bIdx] & 0x01) != 0;
                        }
                    }
                    return new byte[2] { 0x00, 0x00 };
                default:
                    return new byte[2] { 0x01, 0x00 };
            }
        }
        /// <summary>
        /// 读取单个位寄存器的值。
        /// </summary>
        /// <param name="address">统一地址模型。</param>
        /// <returns>读取结果。</returns>
        public Result<bool> ReadBit(PLCAddress address)
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
        /// <param name="address">统一地址模型，长度从 <see cref="PLCAddress.Length"/> 读取。</param>
        /// <returns>读取结果。</returns>
        public Result<bool[]> ReadBitArray(PLCAddress address)
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
        public Result<bool> WriteBit(PLCAddress address, bool value)
        {
            try
            {
                if (address.Area == MemoryArea.M || address.Area == MemoryArea.X || address.Area == MemoryArea.Y)
                {
                    lock (memoryLock)
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
        public Result<bool> WriteBitArray(PLCAddress address, bool[] values)
        {
            try
            {
                if (values == null || values.Length == 0)
                    return Result<bool>.Fail("写入数组不能为空");

                if (address.Area == MemoryArea.M || address.Area == MemoryArea.X || address.Area == MemoryArea.Y)
                {
                    lock (memoryLock)
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
        public Result<short> ReadWord(PLCAddress address)
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
        /// <param name="address">统一地址模型，长度从 <see cref="PLCAddress.Length"/> 读取。</param>
        /// <returns>读取结果。</returns>
        public Result<short[]> ReadWordArray(PLCAddress address)
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
        public Result<bool> WriteWord(PLCAddress address, short value)
        {
            try
            {
                if (address.Area == MemoryArea.D)
                {
                    lock (memoryLock)
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
        public Result<bool> WriteWordArray(PLCAddress address, short[] values)
        {
            try
            {
                if (values == null || values.Length == 0)
                    return Result<bool>.Fail("写入数组不能为空");

                if (address.Area == MemoryArea.D)
                {
                    lock (memoryLock)
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
            if (area == MemoryArea.D)
                return dRegisters.Count;
            return mBits.Count;
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
            lock (memoryLock)
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
            lock (memoryLock)
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
