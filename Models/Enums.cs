using System;
using System.Collections.Generic;
using System.Linq;
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
}
