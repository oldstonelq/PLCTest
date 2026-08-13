using PLCTest.Client.ClientProtocolManagement.Inovance;
using PLCTest.Client.ClientProtocolManagement.Melsec;
using PLCTest.Client.PLCClient.Inovance;
using PLCTest.ClientCommunication;
using PLCTest.ClientProtocolManagement.Melsec;
using PLCTest.PLCClient.Melsec;
using PLCTest.PLCInterface;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLCTest.Models
{
    /// <summary>
    /// PLC客户端构建器，用于创建和配置 PLC 客户端实例
    /// </summary>
    public class PLCClientBuilder
    {
        /// <summary>
        /// 通讯方式实例，用于与 PLC 进行数据交换
        /// </summary>
        private IClientCommunication _communication;
        /// <summary>
        /// 通讯协议实例，用于处理 PLC 通讯协议的具体实现
        /// </summary>
        private IPLCClientProtocol _protocol;

        #region 通讯方式
        /// <summary>
        /// 使用TCP通讯方式，指定PLC的IP地址和端口号
        /// </summary>
        /// <param name="ip">IP地址</param>
        /// <param name="port">端口号</param>
        /// <returns></returns>
        public PLCClientBuilder UseTcp(string ip, int port)
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
        public PLCClientBuilder UseSerial(string portName, int baudRate, Parity parity, int dataBits, StopBits stopBits)
        {
            _communication = new SerialCommunication(portName, baudRate, parity, dataBits, StopBits.One);
            return this;
        }
        #endregion

        #region 通讯协议
        /// <summary>
        /// 汇川PLC通讯协议，使用Modbus TCP协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public PLCClientBuilder UseInovanceModbusTcp()
        {
            var protocol = new InovanceModbusTcpClientProtocol();
            _protocol = protocol;
            return this;
        }
        /// <summary>
        /// 汇川PLC通讯协议，使用Modbus RTU协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public PLCClientBuilder UseInovanceModbusRtu()
        {
            var protocol = new InovanceModbusRtuClientProtocol();
            _protocol = protocol;
            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的31 ASCII模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PLCClientBuilder UseMelsecMc1EBinary()
        {
            var protocol = new MelsecMc1EBinaryClientProtocol();
            _protocol = protocol;
            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的31 ASCII模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PLCClientBuilder UseMelsecMc1EAscii()
        {
            var protocol = new MelsecMc1EAsciiClientProtocol();
            _protocol = protocol;
            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E ASCII模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PLCClientBuilder UseMelsecMc3EAscii()
        {
            var protocol = new MelsecMc3EAsciiClientProtocol();
            _protocol = protocol;
            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E Binary模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PLCClientBuilder UseMelsecMc3EBinary()
        {
            var protocol = new MelsecMc3EBinaryClientProtocol();
            _protocol = protocol;
            return this;
        }
        public PLCClientBuilder UseMelsecMc4EAscii()
        {
            var protocol = new MelsecMc4EAsciiClientProtocol();
            _protocol = protocol;
            return this;
        }
        /// <summary>
        /// 三菱PLC通讯协议，使用MC协议的3E Binary模式进行数据交换
        /// </summary>
        /// <returns></returns>
        public PLCClientBuilder UseMelsecMc4EBinary()
        {
            var protocol = new MelsecMc4EBinaryClientProtocol();
            _protocol = protocol;
            return this;
        }
        #endregion

        #region 构建客户端
        /// <summary>
        /// 构建汇川PLC客户端实例，使用指定的通讯方式和协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public IPLCClient BuildInovanceClient()
        {
            return new InovanceTcpPLCClient(_communication, _protocol);
        }
        /// <summary>
        /// 构建三菱PLC客户端实例，使用指定的通讯方式和协议进行数据交换
        /// </summary>
        /// <returns></returns>
        public IPLCClient BuildMelsecMcPLCClient()
        {
            return new MelsecMcPLCClient(_communication, _protocol);
        }
        #endregion
    }
}
