using System;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// Modbus 从站请求的结构化表示。
    /// Modbus 与 MC 协议差异较大（没有结束码、响应带功能码与字节数、RTU 还要 CRC），
    /// 因此单独建模，不复用 <see cref="RequestInfo"/>。
    /// </summary>
    public sealed class ModbusRequestInfo
    {
        /// <summary>从站地址：TCP 为单元标识，RTU 为站号</summary>
        public byte UnitId { get; set; }

        /// <summary>TCP 事务标识（RTU 无意义）</summary>
        public ushort TransactionId { get; set; }

        /// <summary>功能码</summary>
        public byte FunctionCode { get; set; }

        /// <summary>起始地址（线圈地址或寄存器地址）</summary>
        public ushort StartAddress { get; set; }

        /// <summary>数量；0x05 / 0x06 固定为 1</summary>
        public ushort Quantity { get; set; }

        /// <summary>0x05 / 0x06 的写入值</summary>
        public ushort SingleValue { get; set; }

        /// <summary>0x0F / 0x10 的写入数据（已剥掉字节数字段）</summary>
        public byte[] WriteData { get; set; }

        /// <summary>批量写（0x0F / 0x10）</summary>
        public bool IsMultipleWrite
        {
            get { return FunctionCode == 0x0F || FunctionCode == 0x10; }
        }

        /// <summary>是否为写操作</summary>
        public bool IsWrite
        {
            get { return FunctionCode == 0x05 || FunctionCode == 0x06 || IsMultipleWrite; }
        }

        /// <summary>是否操作线圈（位）</summary>
        public bool IsBit
        {
            get { return FunctionCode == 0x01 || FunctionCode == 0x02 || FunctionCode == 0x05 || FunctionCode == 0x0F; }
        }
    }

    /// <summary>
    /// Modbus 从站协议（服务端侧）。
    /// 负责帧定界、请求解析与响应组包；寄存器存取由 <see cref="PlcTest.Server.PlcServer.Inovance.InovancePlcServer"/> 负责。
    /// </summary>
    public interface IModbusServerProtocol
    {
        /// <summary>
        /// 根据已收到的字节推断整帧长度。
        /// </summary>
        /// <returns>大于 0 整帧长度；等于 0 头部不足；小于 0 无法判定</returns>
        int GetRequestFrameLength(byte[] buffer, int available);

        /// <summary>
        /// 解析请求报文；格式非法时返回 null。
        /// </summary>
        ModbusRequestInfo ParseRequest(byte[] data);

        /// <summary>
        /// 构建读线圈 / 离散量的响应。
        /// </summary>
        byte[] BuildReadCoilsResponse(ModbusRequestInfo request, bool[] bits);

        /// <summary>
        /// 构建读保持 / 输入寄存器的响应。
        /// </summary>
        byte[] BuildReadRegistersResponse(ModbusRequestInfo request, short[] words);

        /// <summary>
        /// 构建写响应（0x05 / 0x06 / 0x0F / 0x10 都是回显起始地址与数量）。
        /// </summary>
        byte[] BuildWriteResponse(ModbusRequestInfo request);

        /// <summary>
        /// 构建异常响应。异常码：0x01 非法功能、0x02 非法数据地址、0x03 非法数据值。
        /// </summary>
        byte[] BuildExceptionResponse(ModbusRequestInfo request, byte exceptionCode);
    }
}
