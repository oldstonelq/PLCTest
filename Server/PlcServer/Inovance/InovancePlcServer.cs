using PlcTest.Protocols;
using PlcTest.Models;
using PlcTest.Server.ServerInterface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlcTest.Server.PlcServer.Inovance
{
    /// <summary>
    /// 汇川 Modbus 从站模拟服务端。
    /// 内部维护两张按 Modbus 地址索引的表：线圈（位）与保持寄存器（16 位），
    /// 地址映射与客户端共用 <see cref="InovanceDeviceCatalog"/>。
    /// </summary>
    public class InovancePlcServer : PlcServerBase
    {
        /// <summary>Modbus 地址空间大小（0 ~ 65535）</summary>
        private const int AddressSpace = 65536;

        private readonly IModbusServerProtocol _protocol;
        private readonly object _memoryLock = new object();

        /// <summary>线圈表（按 Modbus 地址索引）</summary>
        private readonly bool[] _coils = new bool[AddressSpace];
        /// <summary>保持寄存器表（按 Modbus 地址索引）</summary>
        private readonly short[] _registers = new short[AddressSpace];

        /// <summary>软元件地址映射所用的机型</summary>
        public InovanceCpuModel CpuModel { get; set; }

        /// <summary>
        /// 本服务端支持的软元件区域：按当前机型从软元件表取（Easy 比 H3U 少一半）。
        /// </summary>
        public override IReadOnlyList<MemoryArea> GetSupportedAreas()
        {
            return InovanceDeviceCatalog.All
                .Where(spec => InovanceDeviceCatalog.Contains(spec.Area, CpuModel))
                .Select(spec => spec.Area)
                .ToList();
        }
        public InovancePlcServer(IServerCommunication comm, IModbusServerProtocol protocol, InovanceCpuModel cpuModel = InovanceCpuModel.H3U)
            : base(comm)
        {
            _protocol = protocol ?? throw new ArgumentNullException(nameof(protocol));
            CpuModel = cpuModel;

            // 把"这一帧有多长"的判断交给协议：TCP 看 MBAP 长度，RTU 按功能码推算
            Communication.FrameLengthResolver = _protocol.GetRequestFrameLength;
        }

        #region 请求处理

        protected override void HandleFrame(byte[] data, IClientSession client)
        {
            ModbusRequestInfo request = _protocol.ParseRequest(data);
            if (request == null)
            {
                // 报文非法（长度不足 / CRC 不符）：Modbus 从站应当静默丢弃，不回包
                LogComm("报文非法（长度不足或 CRC 不符），已丢弃: " + BitConverter.ToString(data));
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
                response = _protocol.BuildExceptionResponse(request, 0x04);
            }

            Communication.SendToClient(response, client);
            LogComm("发送报文: " + BitConverter.ToString(response));
        }

        /// <summary>
        /// 按功能码分发。异常码：0x01 非法功能、0x02 非法数据地址、0x03 非法数据值。
        /// </summary>
        private byte[] ProcessRequest(ModbusRequestInfo request)
        {
            lock (_memoryLock)
            {
                if (request.StartAddress + Math.Max((int)request.Quantity, 1) - 1 > 0xFFFF)
                {
                    return _protocol.BuildExceptionResponse(request, 0x02);
                }

                switch (request.FunctionCode)
                {
                    // 读线圈 / 离散量
                    case 0x01:
                    case 0x02:
                        {
                            if (request.Quantity < 1 || request.Quantity > InovanceModbusLimits.MaxReadCoils)
                                return _protocol.BuildExceptionResponse(request, 0x03);

                            bool[] bits = new bool[request.Quantity];
                            for (int i = 0; i < bits.Length; i++)
                            {
                                bits[i] = _coils[request.StartAddress + i];
                            }
                            return _protocol.BuildReadCoilsResponse(request, bits);
                        }

                    // 读保持寄存器 / 输入寄存器
                    case 0x03:
                    case 0x04:
                        {
                            if (request.Quantity < 1 || request.Quantity > InovanceModbusLimits.MaxReadRegisters)
                                return _protocol.BuildExceptionResponse(request, 0x03);

                            short[] words = new short[request.Quantity];
                            for (int i = 0; i < words.Length; i++)
                            {
                                words[i] = _registers[request.StartAddress + i];
                            }
                            return _protocol.BuildReadRegistersResponse(request, words);
                        }

                    // 写单个线圈：只有 0xFF00 表示 ON
                    case 0x05:
                        {
                            if (request.SingleValue != 0xFF00 && request.SingleValue != 0x0000)
                                return _protocol.BuildExceptionResponse(request, 0x03);

                            _coils[request.StartAddress] = request.SingleValue == 0xFF00;
                            return _protocol.BuildWriteResponse(request);
                        }

                    // 写单个寄存器
                    case 0x06:
                        _registers[request.StartAddress] = (short)request.SingleValue;
                        return _protocol.BuildWriteResponse(request);

                    // 写多个线圈
                    case 0x0F:
                        {
                            if (request.Quantity > InovanceModbusLimits.MaxWriteCoils)
                                return _protocol.BuildExceptionResponse(request, 0x03);
                            if (request.WriteData == null || request.WriteData.Length * 8 < request.Quantity)
                                return _protocol.BuildExceptionResponse(request, 0x03);

                            for (int i = 0; i < request.Quantity; i++)
                            {
                                _coils[request.StartAddress + i] =
                                    (request.WriteData[i / 8] & (1 << (i % 8))) != 0;
                            }
                            return _protocol.BuildWriteResponse(request);
                        }

                    // 写多个寄存器
                    case 0x10:
                        {
                            if (request.Quantity > InovanceModbusLimits.MaxWriteRegisters)
                                return _protocol.BuildExceptionResponse(request, 0x03);
                            if (request.WriteData == null || request.WriteData.Length < request.Quantity * 2)
                                return _protocol.BuildExceptionResponse(request, 0x03);

                            for (int i = 0; i < request.Quantity; i++)
                            {
                                _registers[request.StartAddress + i] =
                                    (short)((request.WriteData[i * 2] << 8) | request.WriteData[i * 2 + 1]);
                            }
                            return _protocol.BuildWriteResponse(request);
                        }

                    default:
                        return _protocol.BuildExceptionResponse(request, 0x01);
                }
            }
        }

        #endregion

        #region 软元件访问

        /// <summary>
        /// 把统一的软元件地址换算成 Modbus 地址与位/字属性。
        /// </summary>
        private bool TryResolve(PlcAddress address, out int modbusAddress, out bool isBit, out string error)
        {
            modbusAddress = 0;
            isBit = false;
            error = null;

            if (address == null)
            {
                error = "地址不能为空";
                return false;
            }

            try
            {
                InovanceDeviceSpec spec = InovanceDeviceCatalog.Get(address.Area, CpuModel);
                modbusAddress = spec.ResolveStartAddress(address.Address);
                isBit = spec.Space == InovanceAddressSpace.Coil;
                if (modbusAddress < 0 || modbusAddress >= AddressSpace)
                {
                    error = $"地址换算后超出 Modbus 范围: {modbusAddress}";
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public override Result<bool> ReadBit(PlcAddress address)
        {
            int modbusAddress;
            bool isBit;
            string error;
            if (!TryResolve(address, out modbusAddress, out isBit, out error))
                return Result<bool>.Fail(error);
            if (!isBit)
                return Result<bool>.Fail($"MemoryArea.{address.Area} 不是位区域");

            lock (_memoryLock) { return Result<bool>.Success(_coils[modbusAddress]); }
        }

        public override Result<bool[]> ReadBitArray(PlcAddress address)
        {
            int modbusAddress;
            bool isBit;
            string error;
            if (!TryResolve(address, out modbusAddress, out isBit, out error))
                return Result<bool[]>.Fail(error);
            if (!isBit)
                return Result<bool[]>.Fail($"MemoryArea.{address.Area} 不是位区域");
            if (address.Length <= 0)
                return Result<bool[]>.Fail("读取长度必须大于0");
            if (modbusAddress + address.Length > AddressSpace)
                return Result<bool[]>.Fail("读取范围超出 Modbus 地址空间");

            lock (_memoryLock)
            {
                bool[] values = new bool[address.Length];
                Array.Copy(_coils, modbusAddress, values, 0, address.Length);
                return Result<bool[]>.Success(values);
            }
        }

        public override Result<bool> WriteBit(PlcAddress address, bool value)
        {
            int modbusAddress;
            bool isBit;
            string error;
            if (!TryResolve(address, out modbusAddress, out isBit, out error))
                return Result<bool>.Fail(error);
            if (!isBit)
                return Result<bool>.Fail($"MemoryArea.{address.Area} 不是位区域");

            lock (_memoryLock) { _coils[modbusAddress] = value; }
            return Result<bool>.Success(true);
        }

        public override Result<bool> WriteBitArray(PlcAddress address, bool[] values)
        {
            int modbusAddress;
            bool isBit;
            string error;
            if (!TryResolve(address, out modbusAddress, out isBit, out error))
                return Result<bool>.Fail(error);
            if (!isBit)
                return Result<bool>.Fail($"MemoryArea.{address.Area} 不是位区域");
            if (values == null || values.Length == 0)
                return Result<bool>.Fail("写入数据为空");
            if (modbusAddress + values.Length > AddressSpace)
                return Result<bool>.Fail("写入范围超出 Modbus 地址空间");

            lock (_memoryLock) { Array.Copy(values, 0, _coils, modbusAddress, values.Length); }
            return Result<bool>.Success(true);
        }

        public override Result<short> ReadWord(PlcAddress address)
        {
            int modbusAddress;
            bool isBit;
            string error;
            if (!TryResolve(address, out modbusAddress, out isBit, out error))
                return Result<short>.Fail(error);
            if (isBit)
                return Result<short>.Fail($"MemoryArea.{address.Area} 不是字区域");

            lock (_memoryLock) { return Result<short>.Success(_registers[modbusAddress]); }
        }

        public override Result<short[]> ReadWordArray(PlcAddress address)
        {
            int modbusAddress;
            bool isBit;
            string error;
            if (!TryResolve(address, out modbusAddress, out isBit, out error))
                return Result<short[]>.Fail(error);
            if (isBit)
                return Result<short[]>.Fail($"MemoryArea.{address.Area} 不是字区域");
            if (address.Length <= 0)
                return Result<short[]>.Fail("读取长度必须大于0");
            if (modbusAddress + address.Length > AddressSpace)
                return Result<short[]>.Fail("读取范围超出 Modbus 地址空间");

            lock (_memoryLock)
            {
                short[] values = new short[address.Length];
                Array.Copy(_registers, modbusAddress, values, 0, address.Length);
                return Result<short[]>.Success(values);
            }
        }

        public override Result<bool> WriteWord(PlcAddress address, short value)
        {
            int modbusAddress;
            bool isBit;
            string error;
            if (!TryResolve(address, out modbusAddress, out isBit, out error))
                return Result<bool>.Fail(error);
            if (isBit)
                return Result<bool>.Fail($"MemoryArea.{address.Area} 不是字区域");

            lock (_memoryLock) { _registers[modbusAddress] = value; }
            return Result<bool>.Success(true);
        }

        public override Result<bool> WriteWordArray(PlcAddress address, short[] values)
        {
            int modbusAddress;
            bool isBit;
            string error;
            if (!TryResolve(address, out modbusAddress, out isBit, out error))
                return Result<bool>.Fail(error);
            if (isBit)
                return Result<bool>.Fail($"MemoryArea.{address.Area} 不是字区域");
            if (values == null || values.Length == 0)
                return Result<bool>.Fail("写入数据为空");
            if (modbusAddress + values.Length > AddressSpace)
                return Result<bool>.Fail("写入范围超出 Modbus 地址空间");

            lock (_memoryLock) { Array.Copy(values, 0, _registers, modbusAddress, values.Length); }
            return Result<bool>.Success(true);
        }

        #endregion

        #region 寄存器表

        /// <summary>
        /// 该区域在当前机型下的编号上限；不支持的软元件返回 -1。
        /// </summary>
        private int GetMaxIndex(MemoryArea area)
        {
            InovanceDeviceSpec spec;
            if (!InovanceDeviceCatalog.Contains(area, CpuModel))
                return -1;
            spec = InovanceDeviceCatalog.Get(area, CpuModel);
            return spec.GetMaxIndex(CpuModel);
        }

        public override int GetRegisterCount(MemoryArea area)
        {
            int maxIndex = GetMaxIndex(area);
            return maxIndex >= 0 ? maxIndex + 1 : 0;
        }

        public override bool ContainsRegister(MemoryArea area, int address)
        {
            int maxIndex = GetMaxIndex(area);
            return maxIndex >= 0 && address >= 0 && address <= maxIndex;
        }

        public override bool TryAddRegister(MemoryArea area, int address)
        {
            return ContainsRegister(area, address);
        }

        public override void ClearRegisters(MemoryArea area)
        {
            ResetAllRegisterValues(area);
        }

        public override void ResetRegisterValues(MemoryArea area, int start, int length)
        {
            if (length <= 0) return;

            int modbusAddress;
            bool isBit;
            string error;
            if (!TryResolve(new PlcAddress(area, start, false, null, length), out modbusAddress, out isBit, out error))
                return;
            if (modbusAddress + length > AddressSpace) return;

            lock (_memoryLock)
            {
                if (isBit) Array.Clear(_coils, modbusAddress, length);
                else Array.Clear(_registers, modbusAddress, length);
            }
        }

        /// <summary>
        /// 把指定区域的全部寄存器恢复初始值（保留地址）。
        /// 必须**按地址段逐段清**：`CN` 的 C0–C199 在 0xF400、C200–C255 在 0xF700，
        /// 只按基地址清前 N 个寄存器会漏掉 32 位那一段。
        /// </summary>
        public override void ResetAllRegisterValues(MemoryArea area)
        {
            InovanceDeviceSpec spec;
            try
            {
                spec = InovanceDeviceCatalog.Get(area, CpuModel);
            }
            catch
            {
                // 该区域在当前机型下不存在，没什么可清的
                return;
            }

            lock (_memoryLock)
            {
                foreach (InovanceRegisterSegment segment in spec.Segments)
                {
                    int points = segment.EndIndex - segment.StartIndex + 1;
                    int start = segment.BaseAddress;
                    int count = points * segment.RegistersPerPoint;

                    if (start < 0 || start >= AddressSpace || count <= 0)
                    {
                        continue;
                    }
                    if (start + count > AddressSpace)
                    {
                        count = AddressSpace - start;
                    }

                    if (spec.Space == InovanceAddressSpace.Coil)
                    {
                        Array.Clear(_coils, start, count);
                    }
                    else
                    {
                        Array.Clear(_registers, start, count);
                    }
                }
            }
        }

        #endregion
    }
}
