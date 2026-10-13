using PlcTest.Client.ClientProtocolManagement.Inovance;
using PlcTest.Client.ClientProtocolManagement.Melsec;
using PlcTest.Client.ClientProtocolManagement.Omron;
using PlcTest.Client.ClientProtocolManagement.Siemens;
using PlcTest.Client.PlcClient.Inovance;
using PlcTest.Client.PlcClient.Omron;
using PlcTest.Client.ClientCommunication;
using PlcTest.Client.PlcClient.Melsec;
using PlcTest.Client.PlcClient.Siemens;
using PlcTest.Client.PlcInterface;
using System.IO.Ports;

namespace PlcTest.Models
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

            return this;
        }
        /// <summary>
        /// 汇川PLC通讯协议，使用Modbus RTU协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseInovanceModbusRtu()
        {
            _protocol = new InovanceModbusRtuClientProtocol();

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的31 ASCII模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc1EBinary()
        {
            _protocol = new MelsecMc1EBinaryClientProtocol();

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的31 ASCII模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc1EAscii()
        {
            _protocol = new MelsecMc1EAsciiClientProtocol();

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E ASCII模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc3EAscii()
        {
            _protocol = new MelsecMc3EAsciiClientProtocol();

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E Binary模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc3EBinary()
        {
            _protocol = new MelsecMc3EBinaryClientProtocol();

            return this;
        }
        public PlcClientBuilder UseMelsecMc4EAscii()
        {
            _protocol = new MelsecMc4EAsciiClientProtocol();

            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E Binary模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseMelsecMc4EBinary()
        {
            _protocol = new MelsecMc4EBinaryClientProtocol();

            return this;
        }
        /// <summary>
        /// 欧姆龙PLC通讯协议，使用FINS TCP协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public PlcClientBuilder UseOmronFinsTcp()
        {
            _protocol = new OmronFinsTcpClientProtocol();

            return this;
        }
        /// <summary>
        /// 西门子PLC通讯协议，使用S7协议进行数据交换
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
        /// 构建汇川PLC客户端实例，使用指定的通讯方式和协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public IPlcClient BuildInovancePlcClient()
        {
            return new InovancePlcClient(_communication, _protocol);
        }
        /// <summary>
        /// 构建三菱PLC客户端实例，使用指定的通讯方式和协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public IPlcClient BuildMelsecMcPlcClient()
        {
            return new MelsecPlcClient(_communication, _protocol);
        }

        public IPlcClient BuildOmronPlcClient()
        {
            return new OmronPlcClient(_communication, _protocol);
        }
        public IPlcClient BuildSiemensPlcClient()
        {
            return new SiemensPlcClient(_communication, _protocol);
        }
        #endregion
    }
}
