using PlcTest.Client.ClientProtocolManagement.Inovance;
using PlcTest.Client.ClientProtocolManagement.Melsec;
using PlcTest.Client.ClientProtocolManagement.Omron;
using PlcTest.Client.ClientProtocolManagement.Siemens;
using PlcTest.Client.PlcClient;
using PlcTest.Client.ClientCommunication;
using PlcTest.Client.ClientInterface;
using PlcTest.Models;
using System;
using System.IO.Ports;

namespace PlcTest.Client
{
    /// <summary>
    /// PLC客户端构建器，用于创建和配置 PLC 客户端实例
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
        /// 当前已选协议所属厂商，用于在 Build 阶段拦住"协议与厂商不匹配"的组合
        /// </summary>
        private PlcType? _vendor;

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

        #region 通讯协议
        /// <summary>
        /// 汇川PLC通讯协议，使用Modbus TCP协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseInovanceModbusTcp()
        {
            _protocol = new InovanceModbusTcpClientProtocol();
            _vendor = PlcType.Inovance;

            return this;
        }
        /// <summary>
        /// 汇川PLC通讯协议，使用Modbus RTU协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseInovanceModbusRtu()
        {
            _protocol = new InovanceModbusRtuClientProtocol();
            _vendor = PlcType.Inovance;

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的1E帧进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc1EBinary()
        {
            _protocol = new MelsecMc1EBinaryClientProtocol();
            _vendor = PlcType.Melsec;

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的1E帧进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc1EAscii()
        {
            _protocol = new MelsecMc1EAsciiClientProtocol();
            _vendor = PlcType.Melsec;

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E ASCII模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc3EAscii()
        {
            _protocol = new MelsecMc3EAsciiClientProtocol();
            _vendor = PlcType.Melsec;

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E Binary模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc3EBinary()
        {
            _protocol = new MelsecMc3EBinaryClientProtocol();
            _vendor = PlcType.Melsec;

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的4E ASCII模式进行数据交换。
        /// </summary>
        public PlcClientBuilder UseMelsecMc4EAscii()
        {
            _protocol = new MelsecMc4EAsciiClientProtocol();
            _vendor = PlcType.Melsec;

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E Binary模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc4EBinary()
        {
            _protocol = new MelsecMc4EBinaryClientProtocol();
            _vendor = PlcType.Melsec;

            return this;
        }
        /// <summary>
        /// 欧姆龙PLC通讯协议，使用FINS TCP协议进行数据交换。
        /// 注意：当前仅保留协议扩展位，协议实现尚未完成，调用即抛出 NotSupportedException。
        /// </summary>
        public PlcClientBuilder UseOmronFinsTcp()
        {
            throw new NotSupportedException("欧姆龙 FINS TCP 协议尚未实现，请改用已实现的协议。");
        }
        /// <summary>
        /// 西门子PLC通讯协议，使用S7协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseSiemensS71200()
        {
            _protocol = new SiemensS71200ClientProtocol();
            _vendor = PlcType.Siemens;

            return this;
        }
        #endregion

        #region 构建客户端
        /// <summary>
        /// 构建汇川PLC客户端实例，使用指定的通讯方式和协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public IPlcClient BuildInovancePlcClient()
        {
            return Build(PlcType.Inovance, nameof(BuildInovancePlcClient));
        }
        /// <summary>
        /// 构建三菱PLC客户端实例，使用指定的通讯方式和协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public IPlcClient BuildMelsecMcPlcClient()
        {
            return Build(PlcType.Melsec, nameof(BuildMelsecMcPlcClient));
        }

        /// <summary>
        /// 构建欧姆龙PLC客户端实例。
        /// 注意：客户端 8 个读写方法尚未实现，调用即抛出 NotSupportedException。
        /// </summary>
        public IPlcClient BuildOmronPlcClient()
        {
            throw new NotSupportedException("欧姆龙 PLC 客户端尚未实现。");
        }
        /// <summary>
        /// 构建西门子PLC客户端实例，使用指定的通讯方式和协议进行数据交换
        /// </summary>
        public IPlcClient BuildSiemensPlcClient()
        {
            return Build(PlcType.Siemens, nameof(BuildSiemensPlcClient));
        }

        /// <summary>
        /// 统一构建入口：校验通讯方式、协议是否已选，以及协议与厂商是否匹配。
        /// 拼错组合（如 UseInovanceModbusTcp + BuildMelsecMcPlcClient）在这里直接失败，
        /// 而不是构造出一个报文格式不对、现场表现为"读到的数据是垃圾"的客户端。
        /// </summary>
        /// <param name="expectedVendor">调用方期望的厂商</param>
        /// <param name="buildMethod">调用方方法名，仅用于错误信息</param>
        private IPlcClient Build(PlcType expectedVendor, string buildMethod)
        {
            if (_communication == null)
            {
                throw new InvalidOperationException("请先配置通讯方式（UseTcp / UseSerial）。");
            }
            if (_protocol == null)
            {
                throw new InvalidOperationException(
                    $"{buildMethod} 之前必须先选择协议，例如 UseMelsecMc3EBinary()、UseMelsecMc3EAscii() 等。");
            }
            if (_vendor != expectedVendor)
            {
                throw new InvalidOperationException(
                    $"协议与厂商不匹配：{buildMethod} 需要 {expectedVendor} 协议，当前选中的是 {_vendor} 协议的 {_protocol.GetType().Name}。");
            }
            return new CommunicationPlcClient(_communication, _protocol);
        }
        #endregion
    }
}
