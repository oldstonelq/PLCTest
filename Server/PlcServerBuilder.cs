using PlcTest.Models;
using PlcTest.Protocols;
using PlcTest.Server.PlcServer.Inovance;
using PlcTest.Server.PlcServer.Melsec;
using PlcTest.Server.PlcServer.Omron;
using PlcTest.Server.PlcServer.Siemens;
using PlcTest.Server.ServerCommunication;
using PlcTest.Server.ServerInterface;
using PlcTest.Server.ServerProtocolManagement.Inovance;
using PlcTest.Server.ServerProtocolManagement.Melsec;
using PlcTest.Server.ServerProtocolManagement.Omron;
using PlcTest.Server.ServerProtocolManagement.Siemens;
using System;
using System.IO.Ports;

namespace PlcTest.Server
{
    /// <summary>
    /// PLC服务端构建器，结构与客户端 <see cref="PlcTest.Client.PlcClientBuilder"/> 一致：
    /// 只保存「通讯方式 + 已选协议」两个字段，每个 UseXxx 只做一次赋值，
    /// 由 <see cref="Build"/> 按协议实现的接口决定用哪个服务端。
    /// <para>
    /// 三族服务端协议没有公共基接口（MC 是 <see cref="IServerProtocol"/>、
    /// Modbus 是 <see cref="IModbusServerProtocol"/>、FINS 是 <see cref="IOmronFinsServerProtocol"/>），
    /// 所以协议字段用 object 持有、在 Build 里用 <c>is</c> 分派——这与客户端
    /// 「<c>_protocol is IOmronFinsClientProtocol</c> 就用带握手的客户端」是同一套写法。
    /// </para>
    /// </summary>
    public class PlcServerBuilder
    {
        /// <summary>
        /// 服务端通讯方式实例
        /// </summary>
        private IServerCommunication _communication;
        /// <summary>
        /// 已选的服务端协议实例（三族接口互不继承，见类注释）
        /// </summary>
        private object _protocol;
        /// <summary>
        /// 汇川机型：只在使用汇川协议时有意义，Build 分派到 <see cref="InovancePlcServer"/> 时传入
        /// </summary>
        private InovanceCpuModel _inovanceCpuModel = InovanceCpuModel.H3U;

        #region 通讯方式
        /// <summary>
        /// 使用TCP通讯方式，指定监听的IP地址和端口号
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        public PlcServerBuilder UseTcp(string ip, int port)
        {
            _communication = new TcpServerCommunication(ip, port);
            return this;
        }
        /// <summary>
        /// 使用串口通讯方式，指定串口名称、波特率、校验位、数据位和停止位
        /// </summary>
        /// <param name="portName">串口号</param>
        /// <param name="baudRate">波特率</param>
        /// <param name="parity">校验位</param>
        /// <param name="dataBits">数据位</param>
        /// <param name="stopBits">停止位</param>
        /// <returns></returns>
        public PlcServerBuilder UseSerial(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            _communication = new SerialServerCommunication(portName, baudRate, parity, dataBits, stopBits);
            return this;
        }
        #endregion

        #region 按枚举选协议
        /// <summary>
        /// 按协议枚举选择服务端协议：界面只调一次，不必自己 switch 九个 case。
        /// <para>
        /// 只覆盖「有服务端实现」的协议；西门子 S7 目前没有从站实现，传进来会直接抛异常，
        /// 而不是构造出一个不会应答的服务端。
        /// </para>
        /// </summary>
        /// <param name="protocol">协议类型</param>
        /// <returns>构建器本身，便于链式调用</returns>
        /// <exception cref="ArgumentOutOfRangeException">该协议没有服务端实现</exception>
        public PlcServerBuilder UseProtocol(ProtocolType protocol)
        {
            switch (protocol)
            {
                case ProtocolType.MelsecMc3EBinary: return UseMelsecMc3EBinary();
                case ProtocolType.MelsecMc3EAscii: return UseMelsecMc3EAscii();
                case ProtocolType.MelsecMc4EBinary: return UseMelsecMc4EBinary();
                case ProtocolType.MelsecMc4EAscii: return UseMelsecMc4EAscii();
                case ProtocolType.MelsecMc1EBinary: return UseMelsecMc1EBinary();
                case ProtocolType.MelsecMc1EAscii: return UseMelsecMc1EAscii();
                case ProtocolType.InovanceModbusTcp: return UseInovanceModbusTcp();
                case ProtocolType.InovanceModbusRtu: return UseInovanceModbusRtu();
                case ProtocolType.OmronFinsTcp: return UseOmronFinsTcp();
                case ProtocolType.SiemensS71200: return UseSiemensS71200();
                default:
                    throw new ArgumentOutOfRangeException(nameof(protocol), protocol,
                        "该协议还没有服务端实现");
            }
        }
        #endregion

        #region 通讯协议（三菱 MC）
        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的3E帧Binary模式模拟PLC
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc3EBinary()
        {
            _protocol = new MelsecMc3EBinaryServerProtocol();
            return this;
        }
        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的3E帧ASCII模式模拟PLC
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc3EAscii()
        {
            _protocol = new MelsecMc3EAsciiServerProtocol();
            return this;
        }
        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的4E帧Binary模式模拟PLC
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc4EBinary()
        {
            _protocol = new MelsecMc4EBinaryServerProtocol();
            return this;
        }
        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的4E帧ASCII模式模拟PLC
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc4EAscii()
        {
            _protocol = new MelsecMc4EAsciiServerProtocol();
            return this;
        }
        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的1E帧Binary模式模拟PLC（无外层头，适合串口/直接帧）
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc1EBinary()
        {
            _protocol = new MelsecMc1EBinaryServerProtocol();
            return this;
        }
        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的1E帧ASCII模式模拟PLC（无外层头，适合串口/直接帧）
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc1EAscii()
        {
            _protocol = new MelsecMc1EAsciiServerProtocol();
            return this;
        }
        #endregion

        #region 通讯协议（汇川 Modbus）
        /// <summary>
        /// 汇川 PLC 服务端，使用 Modbus TCP 从站模拟（功能码 01/03/05/06/0F/10）
        /// </summary>
        /// <param name="cpuModel">机型：影响支持的软元件区域（Easy 比 H3U 少一半）</param>
        /// <returns></returns>
        public PlcServerBuilder UseInovanceModbusTcp(InovanceCpuModel cpuModel = InovanceCpuModel.H3U)
        {
            _protocol = new InovanceModbusTcpServerProtocol();
            _inovanceCpuModel = cpuModel;
            return this;
        }
        /// <summary>
        /// 汇川 PLC 服务端，使用 Modbus RTU 从站模拟（功能码 01/03/05/06/0F/10，带 CRC16）
        /// </summary>
        /// <param name="cpuModel">机型：影响支持的软元件区域（Easy 比 H3U 少一半）</param>
        /// <returns></returns>
        public PlcServerBuilder UseInovanceModbusRtu(InovanceCpuModel cpuModel = InovanceCpuModel.H3U)
        {
            _protocol = new InovanceModbusRtuServerProtocol();
            _inovanceCpuModel = cpuModel;
            return this;
        }
        #endregion

        #region 通讯协议（欧姆龙 FINS）
        /// <summary>
        /// 欧姆龙 PLC 服务端，使用 FINS/TCP 从站模拟（含节点地址握手，支持 0101 读 / 0102 写）
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseOmronFinsTcp()
        {
            _protocol = new OmronFinsTcpServerProtocol();
            return this;
        }

        /// <summary>
        /// 西门子 S7-1200 服务端协议。
        /// 服务端模拟 I / Q / M 三个区的字节内存，DB 区暂不模拟（缺 DB 号维度）。
        /// </summary>
        /// <returns>构建器本身</returns>
        public PlcServerBuilder UseSiemensS71200()
        {
            _protocol = new SiemensS71200ServerProtocol();
            return this;
        }
        #endregion

        /// <summary>
        /// 协议私有选项。服务端目前只有汇川机型一项，而机型原本存在构建器字段上
        /// （<see cref="UseInovanceModbusTcp"/> 直接接收），所以在 <see cref="Build"/> 里合并：
        /// 选项里没给就用字段上的值。
        /// </summary>
        private ProtocolOptions _options = new ProtocolOptions();

        /// <summary>
        /// 设置协议私有选项；服务端不认识的多余键会被忽略。
        /// </summary>
        /// <param name="options">选项集，可传 null</param>
        /// <returns>构建器本身，便于链式调用</returns>
        public PlcServerBuilder UseOptions(ProtocolOptions options)
        {
            _options = options ?? new ProtocolOptions();
            return this;
        }

        #region 构建服务端
        /// <summary>
        /// 构建服务端实例：按协议实现的接口选服务端类型。
        /// <para>
        /// 六个三菱协议共用 <see cref="MelsecPlcServer"/>、两个汇川协议共用 <see cref="InovancePlcServer"/>、
        /// 欧姆龙用 <see cref="OmronFinsServer"/>，差异都只在协议对象上，所以这里是**能力判断**而不是厂商判断。
        /// </para>
        /// </summary>
        /// <returns>服务端实例</returns>
        /// <exception cref="InvalidOperationException">通讯方式或协议未配置</exception>
        public IPlcServer Build()
        {
            if (_communication == null)
            {
                throw new InvalidOperationException("未配置服务端通讯方式，请先调用 UseTcp 或 UseSerial");
            }
            if (_protocol == null)
            {
                throw new InvalidOperationException(
                    "未配置服务端协议，请先调用 UseProtocol(...) 或 UseMelsecMc3EBinary / UseInovanceModbusTcp / UseOmronFinsTcp 等");
            }

            // S7 的存储是「字节数组 + 位/字共享」，与三菱的位字分离表完全不同，需要专门的服务端
            if (_protocol is IS7ServerProtocol s7)
            {
                return new S7PlcServer(_communication, s7);
            }

            // FINS 有节点地址握手，需要专门的服务端
            if (_protocol is IOmronFinsServerProtocol omron)
            {
                return new OmronFinsServer(_communication, omron);
            }

            // 汇川按机型过滤软元件表，机型也要传进去
            if (_protocol is IModbusServerProtocol modbus)
            {
                // 机型可能来自 UseInovanceModbusTcp(model)，也可能来自选项集（界面统一走选项）
                InovanceCpuModel cpuModel = _options.GetValue("CpuModel", _inovanceCpuModel);
                return new InovancePlcServer(_communication, modbus, cpuModel);
            }

            // 剩下的按 MC 语义处理（三菱 1E / 3E / 4E，含 ASCII 与 Binary）
            if (_protocol is IServerProtocol melsec)
            {
                return new MelsecPlcServer(_communication, melsec);
            }

            throw new InvalidOperationException(
                "无法识别的服务端协议：" + _protocol.GetType().Name + "，它既不是 MC、Modbus 也不是 FINS 协议");
        }
        #endregion
    }
}
