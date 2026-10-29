using PlcTest.Models;
using PlcTest.Client.ClientInterface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlcTest.Client.ClientProtocolManagement.Inovance
{
    /// <summary>
    /// 使用 Modbus TCP 实现的汇川（Inovance）简单协议适配器。
    /// 将请求 PDU 构建为 Modbus PDU，并通过 BuildCompletePack 封装为 MBAP + PDU。
    /// 该实现支持读取/写入线圈（位）和保持寄存器（字）。
    /// </summary>
    public class InovanceModbusTcpClientProtocol : IPlcClientProtocol
    {
        /// <summary>
        /// 事务标识符（Transaction Identifier），用于区分请求和响应。每次发送请求时递增。
        /// </summary>
        private ushort _transactionId = 0;
        /// <summary>
        /// 单元标识符，设备地址，一般默认1
        /// </summary>
        private readonly byte _unitId = 1;
        /// <summary>
        /// 获取寄存器地址的真实编码（返回 byte，以兼容通用接口）。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>寄存器地址的真实编码</returns>
        /// <summary>
        /// 适用机型，决定可用软元件与编号范围。默认 H3U。
        /// </summary>
        public InovanceCpuModel CpuModel { get; set; } = InovanceCpuModel.H3U;

        public int GetMemoryCode(MemoryArea memoryArea)
        {
            // 返回该软元件在 Modbus 地址空间中的起始地址（真实地址 = 起始地址 + 编号）
            return InovanceDeviceCatalog.Get(memoryArea, CpuModel).BaseAddress;
        }
        /// <summary>
        /// 该软元件区域允许的访问方式。
        /// D/R 在本实现中既可当保持寄存器（字）访问，也可当线圈（位）访问，两者地址空间相互独立。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>允许的访问方式</returns>
        public AreaAccess GetAreaAccess(MemoryArea memoryArea)
        {
            // 线圈区只能按位（01/05），保持寄存器区只能按字（03/06/16），由软元件表统一声明
            return InovanceDeviceCatalog.Get(memoryArea, CpuModel).Access;
        }

        /// <summary>
        /// 软元件编号上限取自 <see cref="InovanceDeviceCatalog"/>，按 <see cref="CpuModel"/> 机型取值。
        /// 超出范围的地址在 Modbus 里会落到别的软元件上，必须提前拦下。
        /// </summary>
        /// <param name="memoryArea">寄存器区域</param>
        /// <returns>最大编号；-1 表示协议不做限制</returns>
        public int GetMaxIndex(MemoryArea memoryArea)
        {
            return InovanceDeviceCatalog.Get(memoryArea, CpuModel).GetMaxIndex(CpuModel);
        }

        /// <summary>
        /// 根据响应前缀推断整帧长度：MBAP 的 [4..5] 为大端"其后字节数"（含单元标识），整帧 = 6 + 该值。
        /// </summary>
        public int GetResponseFrameLength(byte[] request, byte[] buffer, int available)
        {
            if (buffer == null || available < 6) return -1;
            return 6 + ((buffer[4] << 8) | buffer[5]);
        }
        /// <summary>
        /// 写入位数据转Byte数组
        /// </summary>
        /// <param name="bools">位数据</param>
        /// <returns>打包后的字节数组</returns>
        public byte[] GetWriteBitData(bool[] bools)
        {
            // 按 Modbus coil 格式，低位先行打包（LSB first）
            int byteCount = (bools.Length + 7) / 8;
            byte[] data = new byte[byteCount];
            for (int i = 0; i < bools.Length; i++)
            {
                if (bools[i])
                {
                    int b = i / 8;
                    int bit = i % 8;
                    data[b] |= (byte)(1 << bit);
                }
            }
            return data;
        }
        /// <summary>
        /// 写入字数据转Byte数组
        /// </summary>
        /// <param name="shorts">字数据</param>
        /// <returns打包后的字节数组></returns>
        public byte[] GetWriteWordData(short[] shorts)
        {
            // Modbus registers are big-endian per register
            byte[] data = new byte[shorts.Length * 2];
            for (int i = 0; i < shorts.Length; i++)
            {
                data[i * 2] = (byte)((shorts[i] >> 8) & 0xFF);
                data[i * 2 + 1] = (byte)(shorts[i] & 0xFF);
            }
            return data;
        }
        /// <summary>
        /// 创建读取数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <returns>读取命令报文</returns>
        public byte[] BuildReadData(PlcAddress address)
        {
            InovanceDeviceSpec spec = InovanceDeviceCatalog.Get(address.Area, CpuModel);
            EnsureSameSegment(spec, address);

            // 真实地址 = 所在段的起始地址 + 段内偏移 × 每点寄存器数
            ushort start = (ushort)spec.ResolveStartAddress(address.Address);
            // 线圈按点计数；寄存器按 16 位寄存器计数（32 位软元件每点算两个）
            ushort quantity = (ushort)spec.ResolveRegisterCount(address.Address, address.Length);
            EnsureQuantityInRange(address.IsBit, false, quantity);
            List<byte> pdu = new List<byte>();
            if (address.IsBit)
            {
                pdu.Add(0x01); // Read Coils
                pdu.Add((byte)(start >> 8));
                pdu.Add((byte)(start & 0xFF));
                pdu.Add((byte)(quantity >> 8));
                pdu.Add((byte)(quantity & 0xFF));
            }
            else
            {
                pdu.Add(0x03); // Read Holding Registers
                pdu.Add((byte)(start >> 8));
                pdu.Add((byte)(start & 0xFF));
                pdu.Add((byte)(quantity >> 8));
                pdu.Add((byte)(quantity & 0xFF));
            }
            return pdu.ToArray();
        }

        /// <summary>
        /// 分段软元件（如汇川 C200–C255 的 32 位计数器）不允许一次访问跨段：
        /// 跨过 C199/C200 边界后地址与寄存器宽度都不连续，读出来的值没有意义。
        /// </summary>
        private static void EnsureSameSegment(InovanceDeviceSpec spec, PlcAddress address)
        {
            if (!spec.CanAccess(address.Address, address.Length))
            {
                throw new ArgumentOutOfRangeException(nameof(address),
                    $"{spec.Name}({address.Area}) 从 {address.Address} 起连续 {address.Length} 点跨越了地址段边界，请分段读写");
            }
        }

        /// <summary>
        /// Modbus 单帧有数量上限（手册 9.4.2），超出后 PLC 只会回一个异常码，
        /// 这里在拼包前拦住，给出能看懂的原因。
        /// </summary>
        private static void EnsureQuantityInRange(bool isBit, bool isWrite, int quantity)
        {
            int max = isBit
                ? (isWrite ? InovanceModbusLimits.MaxWriteCoils : InovanceModbusLimits.MaxReadCoils)
                : (isWrite ? InovanceModbusLimits.MaxWriteRegisters : InovanceModbusLimits.MaxReadRegisters);

            if (quantity > max)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), quantity,
                    $"一次最多{(isWrite ? "写" : "读")} {max} 个{(isBit ? "线圈" : "寄存器")}，当前请求 {quantity} 个");
            }
        }
        /// <summary>
        /// 创建写入数据包
        /// </summary>
        /// <param name="address">PLC地址模型</param>
        /// <param name="data">写入数据</param>
        /// <returns>写命令报文</returns>
        public byte[] BuildWriteData(PlcAddress address, byte[] data)
        {
            InovanceDeviceSpec spec = InovanceDeviceCatalog.Get(address.Area, CpuModel);
            EnsureSameSegment(spec, address);

            ushort start = (ushort)spec.ResolveStartAddress(address.Address);
            ushort quantity = (ushort)spec.ResolveRegisterCount(address.Address, address.Length);
            EnsureQuantityInRange(address.IsBit, true, quantity);
            List<byte> pdu = new List<byte>();
            if (address.IsBit)
            {
                if (quantity == 1)
                {
                    // Write Single Coil (0x05)
                    pdu.Add(0x05);
                    pdu.Add((byte)(start >> 8));
                    pdu.Add((byte)(start & 0xFF));
                    bool on = (data.Length > 0 && (data[0] & 0x01) != 0);
                    pdu.Add((byte)(on ? 0xFF : 0x00));
                    pdu.Add(0x00);
                }
                else
                {
                    // Write Multiple Coils (0x0F)
                    pdu.Add(0x0F);
                    pdu.Add((byte)(start >> 8));
                    pdu.Add((byte)(start & 0xFF));
                    pdu.Add((byte)(quantity >> 8));
                    pdu.Add((byte)(quantity & 0xFF));
                    pdu.Add((byte)data.Length);
                    pdu.AddRange(data);
                }
            }
            else
            {
                // 写入字节数必须与寄存器个数对上。32 位软元件每点占两个寄存器，
                // quantity 至少为 2，因此不会落到 0x06——32 位寄存器本来就只支持 0x10。
                if (data == null || data.Length != quantity * 2)
                {
                    throw new ArgumentException(
                        $"写入 {(data == null ? 0 : data.Length)} 字节，与 {quantity} 个寄存器（{quantity * 2} 字节）不匹配",
                        nameof(data));
                }

                if (quantity == 1)
                {
                    // Write Single Register (0x06)
                    pdu.Add(0x06);
                    pdu.Add((byte)(start >> 8));
                    pdu.Add((byte)(start & 0xFF));
                    pdu.AddRange(data);
                }
                else
                {
                    // Write Multiple Registers (0x10)
                    pdu.Add(0x10);
                    pdu.Add((byte)(start >> 8));
                    pdu.Add((byte)(start & 0xFF));
                    pdu.Add((byte)(quantity >> 8));
                    pdu.Add((byte)(quantity & 0xFF));
                    pdu.Add((byte)data.Length);
                    pdu.AddRange(data);
                }
            }
            return pdu.ToArray();
        }
        /// <summary>
        /// 拼接完整数据包
        /// </summary>
        /// <param name="data">命令报文</param>
        /// <returns>完整命令报文</returns>
        public byte[] BuildCompletePack(byte[] data)
        {
            // MBAP header: Transaction ID (2) + Protocol ID (2=0) + Length (2) + Unit ID (1)
            _transactionId++;
            ushort trans = _transactionId;
            ushort protocolId = 0;
            ushort length = (ushort)(data.Length + 1); // unit id + pdu
            byte[] mbap = new byte[7];
            mbap[0] = (byte)(trans >> 8);
            mbap[1] = (byte)(trans & 0xFF);
            mbap[2] = (byte)(protocolId >> 8);
            mbap[3] = (byte)(protocolId & 0xFF);
            mbap[4] = (byte)(length >> 8);
            mbap[5] = (byte)(length & 0xFF);
            mbap[6] = _unitId;
            byte[] packet = new byte[mbap.Length + data.Length];
            Array.Copy(mbap, 0, packet, 0, mbap.Length);
            Array.Copy(data, 0, packet, mbap.Length, data.Length);
            return packet;
        }
        /// <summary>
        /// 校验响应 MBAP 头的事务 ID 与单元 ID 是否与请求一致
        /// </summary>
        /// <param name="data">完整响应报文</param>
        /// <returns>校验失败返回错误信息，成功返回 null</returns>
        private string ValidateMbap(byte[] data)
        {
            ushort trans = (ushort)((data[0] << 8) | data[1]);
            if (trans != _transactionId)
                return $"事务 ID 不匹配: 期望 0x{_transactionId:X4}，实际 0x{trans:X4}";
            if (data[6] != _unitId)
                return $"单元 ID 不匹配: 期望 0x{_unitId:X2}，实际 0x{data[6]:X2}";
            return null;
        }
        /// <summary>
        /// 解析写返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <returns>返回写入结果</returns>
        public Result<bool> ParseWriteResponse(byte[] data)
        {
            Result<bool> result = new Result<bool>();
            if (data == null || data.Length < 8)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            string mbapError = ValidateMbap(data);
            if (mbapError != null)
            {
                result.ErrorMsg = mbapError;
                return result;
            }
            int pduOffset = 7;
            byte func = data[pduOffset];
            if ((func & 0x80) != 0)
            {
                byte ex = data[pduOffset + 1];
                result.ErrorMsg = $"Modbus 异常响应: 0x{ex:X2}";
                result.Kind = ErrorKind.DeviceEndCode;
                result.EndCode = ex;
                return result;
            }
            // 写响应应回显请求功能码：单线圈/单寄存器/多线圈/多寄存器
            if (func != 0x05 && func != 0x06 && func != 0x0F && func != 0x10)
            {
                result.ErrorMsg = "功能码不匹配";
                return result;
            }
            result.IsSuccess = true;
            result.Data = true;
            return result;
        }
        /// <summary>
        /// 解析读位数据返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>位数据数组</returns>
        public Result<bool[]> ParseReadBitResponse(byte[] data, int size)
        {
            Result<bool[]> result = new Result<bool[]>();
            if (data == null || data.Length < 9)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            string mbapError = ValidateMbap(data);
            if (mbapError != null)
            {
                result.ErrorMsg = mbapError;
                return result;
            }
            int pduOffset = 7;
            byte func = data[pduOffset];
            if ((func & 0x80) != 0)
            {
                byte ex = data[pduOffset + 1];
                result.ErrorMsg = $"Modbus 异常响应: 0x{ex:X2}";
                result.Kind = ErrorKind.DeviceEndCode;
                result.EndCode = ex;
                return result;
            }
            if (func != 0x01)
            {
                result.ErrorMsg = "功能码不匹配";
                return result;
            }
            byte byteCount = data[pduOffset + 1];
            if (data.Length < pduOffset + 2 + byteCount)
            {
                result.ErrorMsg = "返回数据长度不足";
                return result;
            }
            // 字节数必须覆盖期望点数，否则按点数循环会越界读
            if (byteCount < (size + 7) / 8)
            {
                result.ErrorMsg = "返回字节数与期望不符";
                return result;
            }
            bool[] bits = new bool[size];
            for (int i = 0; i < size; i++)
            {
                int b = i / 8;
                int bit = i % 8;
                bits[i] = (data[pduOffset + 2 + b] & (1 << bit)) != 0;
            }
            result.IsSuccess = true;
            result.Data = bits;
            return result;
        }
        /// <summary>
        /// 解析读字数据返回
        /// </summary>
        /// <param name="data">返回数据</param>
        /// <param name="size">读取数据长度</param>
        /// <returns>字数据数组</returns>
        public Result<short[]> ParseReadWordResponse(byte[] data, int size)
        {
            Result<short[]> result = new Result<short[]>();
            if (data == null || data.Length < 9)
            {
                result.ErrorMsg = "返回数据长度错误";
                return result;
            }
            string mbapError = ValidateMbap(data);
            if (mbapError != null)
            {
                result.ErrorMsg = mbapError;
                return result;
            }
            int pduOffset = 7;
            byte func = data[pduOffset];
            if ((func & 0x80) != 0)
            {
                byte ex = data[pduOffset + 1];
                result.ErrorMsg = $"Modbus 异常响应: 0x{ex:X2}";
                result.Kind = ErrorKind.DeviceEndCode;
                result.EndCode = ex;
                return result;
            }
            if (func != 0x03)
            {
                result.ErrorMsg = "功能码不匹配";
                return result;
            }
            byte byteCount = data[pduOffset + 1];
            if (data.Length < pduOffset + 2 + byteCount)
            {
                result.ErrorMsg = "返回数据长度不足";
                return result;
            }
            // 按实际返回的字节数解析：32 位软元件（如汇川 C200–C255）每点返回两个寄存器，
            // 若只按请求点数截断，32 位值会被砍掉一半。
            int regCount = byteCount / 2;
            if (regCount < size)
            {
                result.ErrorMsg = "返回字节数与期望不符";
                return result;
            }
            short[] regs = new short[regCount];
            for (int i = 0; i < regCount; i++)
            {
                int idx = pduOffset + 2 + i * 2;
                regs[i] = (short)((data[idx] << 8) | data[idx + 1]);
            }
            result.IsSuccess = true;
            result.Data = regs;
            return result;
        }
    }
}
