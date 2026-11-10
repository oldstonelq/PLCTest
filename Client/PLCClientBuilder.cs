using PlcTest.Client.ClientProtocolManagement.Inovance;
using PlcTest.Client.ClientProtocolManagement.Melsec;
using PlcTest.Client.ClientProtocolManagement.Omron;
using PlcTest.Client.ClientProtocolManagement.Siemens;
using PlcTest.Client.PlcClient;
using PlcTest.Client.PlcClient.Omron;
using PlcTest.Client.PlcClient.Siemens;
using PlcTest.Client.ClientCommunication;
using PlcTest.Client.ClientInterface;
using PlcTest.Models;
using System;
using System.IO.Ports;

namespace PlcTest.Client
{
    /// <summary>
    /// PLC客户端构建器，结构与服务端 <see cref="PlcTest.Server.PlcServerBuilder"/> 一致：
    /// 只保存「通讯方式 + 已选协议」两个字段，每个 UseXxx 只做一次赋值，
    /// 由 <see cref="Build"/> 按协议实现的接口决定用哪个客户端。
    /// </summary>
    public class PlcClientBuilder
    {
        /// <summary>
        /// 通讯方式实例，用于与 PLC 进行数据交换
        /// </summary>
        private IClientCommunication _communication;
        /// <summary>
        /// 通讯协议实例，用于处理 PLC 通讯协议的具体实现
        /// </summary>
        private IPlcClientProtocol _protocol;

        /// <summary>
        /// 协议私有选项（机型、字节序、机架槽号……）。在 <see cref="Build"/> 里交给协议应用，
        /// 所以与 UseProtocol 的先后顺序无所谓。
        /// </summary>
        private ProtocolOptions _options = new ProtocolOptions();

        /// <summary>
        /// 设置协议私有选项。键的含义见 <see cref="PlcTest.Protocols.ProtocolOptionCatalog"/>；
        /// 协议不认识的多余键会被忽略，不会抛异常。
        /// </summary>
        /// <param name="options">选项集，可传 null 表示不设置</param>
        /// <returns>构建器本身，便于链式调用</returns>
        public PlcClientBuilder UseOptions(ProtocolOptions options)
        {
            _options = options ?? new ProtocolOptions();
            return this;
        }

        /// <summary>
        /// 设置单个选项，等价于 <c>UseOptions(new ProtocolOptions().Set(key, value))</c>，便于调用方按需拼。
        /// </summary>
        public PlcClientBuilder SetOption(string key, object value)
        {
            _options.Set(key, value);
            return this;
        }

        #region 通讯方式
        /// <summary>
        /// 使用TCP通讯方式，指定PLC的IP地址和端口号
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        public PlcClientBuilder UseTcp(string ip, int port)
        {
            _communication = new TcpClientCommunication(ip, port);
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
        public PlcClientBuilder UseSerial(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            _communication = new SerialPortClientCommunication(portName, baudRate, parity, dataBits, stopBits);
            return this;
        }
        #endregion

        #region 按枚举选协议
        /// <summary>
        /// 按协议枚举选择协议：界面（以及任何按 <see cref="ProtocolType"/> 取协议的地方）都从这里走，
        /// 新增协议只需要在本方法补一个 case，不必再改窗体里的 switch。
        /// <para>
        /// 这里只负责「选得出协议」，界面上是否显示该协议由 <see cref="ProtocolInfoAttribute"/> 的能力位决定
        /// （例如西门子当前四个开关全关，界面上不可选，但 API 仍可按需构建）。
        /// </para>
        /// </summary>
        /// <param name="protocol">协议类型</param>
        /// <returns>构建器本身，便于链式调用</returns>
        /// <exception cref="ArgumentOutOfRangeException">枚举值没有对应的协议实现</exception>
        public PlcClientBuilder UseProtocol(ProtocolType protocol)
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
                        "PlcClientBuilder.UseProtocol 未登记的协议，请补一个 case");
            }
        }
        #endregion

        #region 通讯协议（三菱 MC）
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的1E帧 Binary 模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc1EBinary()
        {
            _protocol = new MelsecMc1EBinaryClientProtocol();
            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的1E帧 ASCII 模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc1EAscii()
        {
            _protocol = new MelsecMc1EAsciiClientProtocol();
            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E帧 Binary 模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc3EBinary()
        {
            _protocol = new MelsecMc3EBinaryClientProtocol();
            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E帧 ASCII 模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc3EAscii()
        {
            _protocol = new MelsecMc3EAsciiClientProtocol();
            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的4E帧 Binary 模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc4EBinary()
        {
            _protocol = new MelsecMc4EBinaryClientProtocol();
            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的4E帧 ASCII 模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc4EAscii()
        {
            _protocol = new MelsecMc4EAsciiClientProtocol();
            return this;
        }
        #endregion

        #region 通讯协议（汇川 Modbus）
        /// <summary>
        /// 汇川PLC通讯协议，使用Modbus TCP协议进行数据交换
        /// </summary>
        /// <param name="format">32 位数据的排列方式，默认低字在前（CDAB）</param>
        /// <returns></returns>
        public PlcClientBuilder UseInovanceModbusTcp(DataFormat format = DataFormat.CDAB)
        {
            _protocol = new InovanceModbusTcpClientProtocol { DataFormat = format };
            return this;
        }
        /// <summary>
        /// 汇川PLC通讯协议，使用Modbus RTU协议进行数据交换
        /// </summary>
        /// <param name="format">32 位数据的排列方式，默认低字在前（CDAB）</param>
        /// <returns></returns>
        public PlcClientBuilder UseInovanceModbusRtu(DataFormat format = DataFormat.CDAB)
        {
            _protocol = new InovanceModbusRtuClientProtocol { DataFormat = format };
            return this;
        }
        #endregion

        #region 通讯协议（欧姆龙 FINS）
        /// <summary>
        /// 欧姆龙PLC通讯协议，使用 FINS/TCP 进行数据交换。
        /// 建连后需做节点地址握手，<see cref="Build()"/> 会自动换成 OmronPlcClient。
        /// </summary>
        /// <param name="format">32 位数据的排列方式，默认低字在前（CDAB）</param>
        /// <returns></returns>
        public PlcClientBuilder UseOmronFinsTcp(DataFormat format = DataFormat.CDAB)
        {
            _protocol = new OmronFinsTcpClientProtocol { DataFormat = format };
            return this;
        }
        #endregion

        #region 通讯协议（西门子 S7）
        /// <summary>
        /// 西门子PLC通讯协议，使用S7协议进行数据交换。
        /// 当前界面未开放（官方手册未到位，帧格式未逐条核对），API 仍可按需构建。
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseSiemensS71200()
        {
            _protocol = new SiemensS71200ClientProtocol();
            return this;
        }
        #endregion

        #region 构建客户端
        /// <summary>
        /// 构建客户端实例。协议是欧姆龙 FINS 时自动用 <see cref="OmronPlcClient"/>（建连后要握手），
        /// 其余厂商共用 <see cref="CommunicationPlcClient"/>。
        /// <para>
        /// 判断依据是协议实现的接口，不是厂商枚举：新增一个需要特殊客户端的厂商时，
        /// 只要它的协议实现对应接口即可，不必再各个 BuildXxx 里配对。
        /// </para>
        /// </summary>
        /// <returns>已配置好的客户端</returns>
        /// <exception cref="InvalidOperationException">通讯方式或协议未配置</exception>
        public IPlcClient Build()
        {
            if (_communication == null)
            {
                throw new InvalidOperationException("请先配置通讯方式（UseTcp / UseSerial）。");
            }
            if (_protocol == null)
            {
                throw new InvalidOperationException("请先选择协议（UseProtocol 或 UseMelsecMc3EBinary 等）。");
            }

            // 协议私有选项在建客户端之前应用：机型会影响软元件表，机架/槽号会影响 COTP 帧
            IProtocolOptionHost optionHost = _protocol as IProtocolOptionHost;
            if (optionHost != null)
            {
                optionHost.ApplyOptions(_options);
            }

            // FINS 必须在建连后做节点地址握手，用专门的客户端
            if (_protocol is IOmronFinsClientProtocol)
            {
                return new OmronPlcClient(_communication, _protocol);
            }

            // S7 建连后还要做 COTP + Setup Communication 两段握手
            if (_protocol is IS7SessionProtocol)
            {
                return new S7PlcClient(_communication, _protocol);
            }

            return new CommunicationPlcClient(_communication, _protocol);
        }
        #endregion
    }
}
