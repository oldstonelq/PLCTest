using PlcTest.Server.PlcServer.Melsec;
using PlcTest.Server.ServerCommunication;
using PlcTest.Server.ServerInterface;
using PlcTest.Server.ServerProtocolManagement.Melsec;
using System;
using System.IO.Ports;

namespace PlcTest.Server
{
    /// <summary>
    /// PLC服务端构建器，与客户端 <see cref="PlcTest.Client.PlcClientBuilder"/> 对称：
    /// 逐项配置通讯方式与服务端协议，最后由 <see cref="Build"/> 创建服务端实例。
    /// </summary>
    public class PlcServerBuilder
    {
        /// <summary>
        /// 服务端通讯方式实例
        /// </summary>
        private IServerCommunication _communication;
        /// <summary>
        /// 服务端协议实例（可选，为 null 时由具体服务端使用默认协议）
        /// </summary>
        private IServerProtocol _protocol;
        /// <summary>
        /// 服务端工厂：通讯方式与协议确定后，按协议类型创建对应的服务端
        /// </summary>
        private Func<IServerCommunication, IServerProtocol, IPlcServer> _serverFactory;

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

        #region 通讯协议
        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的3E帧Binary模式模拟PLC
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc3E()
        {
            _protocol = new MelsecMc3EServerProtocol();
            _serverFactory = (comm, protocol) => new MelsecMc3EPlcServer(comm, protocol);
            return this;
        }

        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的3E帧ASCII模式模拟PLC
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc3EAscii()
        {
            _protocol = new MelsecMc3EAsciiServerProtocol();
            _serverFactory = (comm, protocol) => new MelsecPlcServer(comm, protocol);
            return this;
        }

        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的4E帧Binary模式模拟PLC
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc4EBinary()
        {
            _protocol = new MelsecMc4EBinaryServerProtocol();
            _serverFactory = (comm, protocol) => new MelsecPlcServer(comm, protocol);
            return this;
        }

        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的4E帧ASCII模式模拟PLC
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc4EAscii()
        {
            _protocol = new MelsecMc4EAsciiServerProtocol();
            _serverFactory = (comm, protocol) => new MelsecPlcServer(comm, protocol);
            return this;
        }

        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的1E帧Binary模式模拟PLC（无外层头，适合串口/直接帧）
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc1EBinary()
        {
            _protocol = new MelsecMc1EBinaryServerProtocol();
            _serverFactory = (comm, protocol) => new MelsecPlcServer(comm, protocol);
            return this;
        }

        /// <summary>
        /// 三菱PLC服务端协议，使用MC协议的1E帧ASCII模式模拟PLC（无外层头，适合串口/直接帧）
        /// </summary>
        /// <returns></returns>
        public PlcServerBuilder UseMelsecMc1EAscii()
        {
            _protocol = new MelsecMc1EAsciiServerProtocol();
            _serverFactory = (comm, protocol) => new MelsecPlcServer(comm, protocol);
            return this;
        }
        #endregion

        /// <summary>
        /// 构建服务端实例
        /// </summary>
        /// <returns>服务端实例</returns>
        public IPlcServer Build()
        {
            if (_communication == null)
            {
                throw new InvalidOperationException("未配置服务端通讯方式，请先调用 UseTcp 或 UseSerial");
            }
            if (_serverFactory == null)
            {
                throw new InvalidOperationException("未配置服务端协议，请先调用 UseMelsecMc3E / UseMelsecMc3EAscii / UseMelsecMc4EBinary / UseMelsecMc4EAscii / UseMelsecMc1EBinary / UseMelsecMc1EAscii");
            }
            return _serverFactory(_communication, _protocol);
        }
    }
}
