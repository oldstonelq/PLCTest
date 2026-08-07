using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PLCTest.Models
{
    /// <summary>
    /// 枚举类
    /// </summary>
    public class Enums
    {
        /// <summary>
        /// PLC内存区域枚举
        /// </summary>
        public enum MemoryArea
        {
            X,
            Y,
            M,
            D,
            DB,
            R,
            S,
            T,
            C,
            W
        }
        /// <summary>
        /// PLC类型枚举
        /// </summary>
        public enum PLCType
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
            TCP,
            /// <summary>
            /// 串口通讯方式
            /// </summary>
            Serial,
        }
        /// <summary>
        /// 协议类型枚举
        /// </summary>
        public enum ProtocolType
        {
            /// <summary>
            /// 三菱3EASCII协议
            /// </summary>
            MelsecMc3EAscii,
            /// <summary>
            /// 三菱3EBinary协议
            /// </summary>
            MelsecMc3EBinary,
            /// <summary>
            /// 西门子协议
            /// </summary>
            Siemens,
            /// <summary>
            /// 欧姆龙协议
            /// </summary>
            Omron,
            /// <summary>
            /// 汇川协议Moudbus RTU
            /// </summary>
            InovanceModbusRtu,
            /// <summary>
            /// 汇川协议Moudbus TCP
            /// </summary>
            InovanceModbusTcp,
        }

    }
}
