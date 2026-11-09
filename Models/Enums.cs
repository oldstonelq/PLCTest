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

        // ---- 以下为欧姆龙（FINS）专用区 ----
        /// <summary>CIO 区（Core I/O，内继电器区）</summary>
        CIO,
        /// <summary>HR 区（Holding Relay，保持继电器）</summary>
        HR,
        /// <summary>AR 区（Auxiliary Relay，辅助继电器）</summary>
        AR,
        /// <summary>EM 区（Extended Memory，扩展存储区，按 bank 划分，bank 存于 DbNumber）</summary>
        EM,
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
    /// <remarks>
    /// 枚举值显式赋值：避免将来在中间插入协议时，已经写进配置或日志的数值跟着错位。
    /// <para>
    /// 串口能力（<c>clientSerial</c> / <c>serverSerial</c>）当前全部关闭——串口链路尚未实测，
    /// 界面只提供 TCP。原本只跑串口的 1E 与 Modbus RTU 因此改成「帧直接跑在 TCP 上」：
    /// 帧格式与串口完全一致（1E 无外层头、RTU 带 CRC16），**不是** Modbus TCP 的 MBAP 头。
    /// 恢复串口时把对应端的串口参数补回 <c>true</c> 即可，两者可以并存。
    /// </para>
    /// <para>
    /// 西门子只有客户端（<c>clientTcp</c>），服务端还没实现：从站要做同样的 COTP + Setup Communication
    /// 两段握手再逐条应答，工作量约为其他协议的两倍。客户端侧的两段握手与读写帧已按公开报文样本核对，
    /// 真机验证时先看日志里的「S7 会话已建立：协商 PDU 长度 N」，握手不过会直接返回失败原因。
    /// </para>
    /// </remarks>
    public enum ProtocolType
    {
        /// <summary>
        /// 三菱 MC 3E Binary（TCP，客户端/服务端）
        /// </summary>
        [ProtocolInfo("三菱 MC 3E Binary", clientTcp: true, serverTcp: true, clientSerial: false, serverSerial: false)]
        MelsecMc3EBinary = 0,
        /// <summary>
        /// 三菱 MC 3E ASCII（TCP，客户端/服务端）
        /// </summary>
        [ProtocolInfo("三菱 MC 3E ASCII", clientTcp: true, serverTcp: true, clientSerial: false, serverSerial: false)]
        MelsecMc3EAscii = 1,
        /// <summary>
        /// 三菱 MC 4E Binary（TCP，客户端/服务端）
        /// </summary>
        [ProtocolInfo("三菱 MC 4E Binary", clientTcp: true, serverTcp: true, clientSerial: false, serverSerial: false)]
        MelsecMc4EBinary = 2,
        /// <summary>
        /// 三菱 MC 4E ASCII（TCP，客户端/服务端）
        /// </summary>
        [ProtocolInfo("三菱 MC 4E ASCII", clientTcp: true, serverTcp: true, clientSerial: false, serverSerial: false)]
        MelsecMc4EAscii = 3,
        /// <summary>
        /// 三菱 MC 1E Binary（TCP，客户端/服务端；帧格式与串口一致，无外层头）
        /// </summary>
        [ProtocolInfo("三菱 MC 1E Binary", clientTcp: true, serverTcp: true, clientSerial: false, serverSerial: false)]
        MelsecMc1EBinary = 4,
        /// <summary>
        /// 三菱 MC 1E ASCII（TCP，客户端/服务端；帧格式与串口一致，无外层头）
        /// </summary>
        [ProtocolInfo("三菱 MC 1E ASCII", clientTcp: true, serverTcp: true, clientSerial: false, serverSerial: false)]
        MelsecMc1EAscii = 5,
        /// <summary>
        /// 汇川 Modbus TCP（TCP，客户端/服务端）
        /// </summary>
        [ProtocolInfo("汇川 Modbus TCP", clientTcp: true, serverTcp: true, clientSerial: false, serverSerial: false)]
        InovanceModbusTcp = 6,
        /// <summary>
        /// 汇川 Modbus RTU（RTU 帧带 CRC16，直接跑在 TCP 上；标准 Modbus TCP 请用上面的汇川 Modbus TCP）
        /// </summary>
        [ProtocolInfo("汇川 Modbus RTU", clientTcp: true, serverTcp: true, clientSerial: false, serverSerial: false)]
        InovanceModbusRtu = 7,
        /// <summary>
        /// 西门子 S7-1200（TCP，客户端可用；服务端尚未实现）
        /// </summary>
        [ProtocolInfo("西门子 S7-1200", clientTcp: true, serverTcp: false, clientSerial: false, serverSerial: false)]
        SiemensS71200 = 8,
        /// <summary>
        /// 欧姆龙 FINS/TCP（TCP，客户端/服务端）
        /// </summary>
        [ProtocolInfo("欧姆龙 FINS TCP", clientTcp: true, serverTcp: true, clientSerial: false, serverSerial: false)]
        OmronFinsTcp = 9,
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

        public ProtocolInfoAttribute(string displayName, bool clientTcp,bool clientSerial, bool serverTcp ,bool serverSerial)
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
