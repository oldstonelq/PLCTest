using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PlcTest.Models
{
    /// <summary>
    /// PLC内存区域枚举
    /// </summary>
    public enum MemoryArea
    {
        SM,
        SD,
        X,
        Y,
        M,
        L,
        F,
        V,
        B,
        D,
        W,
        TS,
        TC,
        TN,
        SS,
        SC,
        SN,
        CS,
        CC,
        CN,
        SB,
        SW,
        S,
        DX,
        DY,
        Z,
        R,
        ZR,
    }
    /// <summary>
    /// 软元件区域允许的访问方式。
    /// 由协议实现声明（协议才知道某个区域在该协议下能否按位/按字访问），
    /// 客户端不再自己维护"哪些区域是位、哪些是字"的另一套规则。
    /// </summary>
    [Flags]
    public enum AreaAccess
    {
        /// <summary>不支持访问</summary>
        None = 0,
        /// <summary>支持按位访问</summary>
        Bit = 1,
        /// <summary>支持按字访问</summary>
        Word = 2,
        /// <summary>既能按位也能按字访问（如三菱 M/X/Y：按字访问时每字 16 点）</summary>
        Both = Bit | Word,
    }
    /// <summary>
    /// PLC类型枚举
    /// </summary>
    public enum PlcType
    {
        /// <summary>
        /// 三菱
        /// </summary>
        Melsec,
        /// <summary>
        /// 西门子
        /// </summary>
        Siemens,
        /// <summary>
        /// 欧姆龙
        /// </summary>
        Omron,
        /// <summary>
        /// 汇川
        /// </summary>
        Inovance
    }
    /// <summary>
    /// 通讯方式枚举
    /// </summary>
    public enum CommunicationType
    {
        /// <summary>
        /// 标准TCP通讯方式
        /// </summary>
        Tcp,
        /// <summary>
        /// 串口通讯方式
        /// </summary>
        Serial,
    }
    /// <summary>
    /// 通讯协议枚举（客户端与服务端统一登记）。
    /// 新增协议时只需在此枚举增加一项并标注 <see cref="ProtocolInfoAttribute"/>，
    /// 连接设置对话框的下拉框会自动出现，无需再改界面代码。
    /// </summary>
    public enum ProtocolType
    {
        /// <summary>
        /// 三菱 MC 3E Binary（TCP，客户端/服务端）
        /// </summary>
        [ProtocolInfo("三菱 MC 3E Binary", serverTcp: true)]
        MelsecMc3EBinary,
        /// <summary>
        /// 三菱 MC 3E ASCII（TCP，客户端/服务端）
        /// </summary>
        [ProtocolInfo("三菱 MC 3E ASCII", serverTcp: true)]
        MelsecMc3EAscii,
        /// <summary>
        /// 三菱 MC 4E Binary（TCP，客户端/服务端）
        /// </summary>
        [ProtocolInfo("三菱 MC 4E Binary", serverTcp: true)]
        MelsecMc4EBinary,
        /// <summary>
        /// 三菱 MC 4E ASCII（TCP，客户端/服务端）
        /// </summary>
        [ProtocolInfo("三菱 MC 4E ASCII", serverTcp: true)]
        MelsecMc4EAscii,
        /// <summary>
        /// 三菱 MC 1E Binary（TCP/串口，客户端/服务端仅串口）
        /// </summary>
        [ProtocolInfo("三菱 MC 1E Binary", clientSerial: true, serverSerial: true)]
        MelsecMc1EBinary,
        /// <summary>
        /// 三菱 MC 1E ASCII（TCP/串口，客户端/服务端仅串口）
        /// </summary>
        [ProtocolInfo("三菱 MC 1E ASCII", clientSerial: true, serverSerial: true)]
        MelsecMc1EAscii,
        /// <summary>
        /// 汇川 Modbus TCP
        /// </summary>
        [ProtocolInfo("汇川 Modbus TCP", serverTcp: true)]
        InovanceModbusTcp,
        /// <summary>
        /// 汇川 Modbus RTU（仅串口）
        /// </summary>
        [ProtocolInfo("汇川 Modbus RTU", clientTcp: false, clientSerial: true, serverSerial: true)]
        InovanceModbusRtu,
        /// <summary>
        /// 西门子 S7-1200
        /// </summary>
        [ProtocolInfo("西门子 S7-1200")]
        SiemensS71200,
    }
    /// <summary>
    /// 协议元信息：界面显示名与各场景（客户端/服务端、TCP/串口）下是否可用。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ProtocolInfoAttribute : Attribute
    {
        /// <summary>
        /// 界面显示名
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 客户端模式下是否支持 TCP
        /// </summary>
        public bool ClientTcp { get; }

        /// <summary>
        /// 客户端模式下是否支持串口
        /// </summary>
        public bool ClientSerial { get; }

        /// <summary>
        /// 服务端模式下是否支持 TCP
        /// </summary>
        public bool ServerTcp { get; }

        /// <summary>
        /// 服务端模式下是否支持串口
        /// </summary>
        public bool ServerSerial { get; }

        public ProtocolInfoAttribute(string displayName,
            bool clientTcp = true,
            bool clientSerial = false,
            bool serverTcp = false,
            bool serverSerial = false)
        {
            DisplayName = displayName;
            ClientTcp = clientTcp;
            ClientSerial = clientSerial;
            ServerTcp = serverTcp;
            ServerSerial = serverSerial;
        }
    }
    /// <summary>
    /// ProtocolType 扩展：读取协议元信息，按通讯方式/客户端服务端过滤可选协议。
    /// </summary>
    public static class ProtocolTypeExtensions
    {
        /// <summary>
        /// 读取协议元信息；未标注返回 null。
        /// </summary>
        public static ProtocolInfoAttribute GetProtocolInfo(this ProtocolType protocol)
        {
            var field = typeof(ProtocolType).GetField(protocol.ToString());
            return field?.GetCustomAttributes(typeof(ProtocolInfoAttribute), false)
                .FirstOrDefault() as ProtocolInfoAttribute;
        }

        /// <summary>
        /// 读取协议的界面显示名。
        /// </summary>
        public static string GetDisplayName(this ProtocolType protocol)
        {
            return protocol.GetProtocolInfo()?.DisplayName ?? protocol.ToString();
        }

        /// <summary>
        /// 按通讯方式与客户端/服务端模式过滤可选协议。
        /// </summary>
        public static IEnumerable<ProtocolType> GetProtocols(CommunicationType communicationType, bool isServer)
        {
            bool tcp = communicationType == CommunicationType.Tcp;
            foreach (ProtocolType protocol in Enum.GetValues(typeof(ProtocolType)))
            {
                var info = protocol.GetProtocolInfo();
                if (info == null)
                {
                    continue;
                }

                bool available = isServer
                    ? (tcp ? info.ServerTcp : info.ServerSerial)
                    : (tcp ? info.ClientTcp : info.ClientSerial);

                if (available)
                {
                    yield return protocol;
                }
            }
        }
    }
}
