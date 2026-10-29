using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlcTest.Models
{
    /// <summary>
    /// MC 3E 协议解析后的结构化请求数据
    /// </summary>
    public class RequestInfo
    {
        /// <summary>
        /// 命令码（0x0401=批量读取, 0x1401=批量写入）
        /// </summary>
        public ushort Command { get; set; }

        /// <summary>
        /// 设备代码（协议层原始值：二进制协议为 1 字节，ASCII/1E 协议为 2 字节双字符码）
        /// </summary>
        public int DeviceCode { get; set; }

        /// <summary>
        /// 设备区域——由协议层从 DeviceCode 映射出的统一语义，服务端业务逻辑只依赖此字段，不感知协议魔法数
        /// </summary>
        public MemoryArea? DeviceArea { get; set; }

        /// <summary>
        /// 起始地址（24位）
        /// </summary>
        public int StartAddress { get; set; }

        /// <summary>
        /// 软元件点数
        /// </summary>
        public ushort Points { get; set; }

        /// <summary>
        /// 本次请求按位还是按字访问。
        /// 同一个软元件可能既能按位又能按字（如三菱的 M），服务端靠这个区分读位表还是字表。
        /// </summary>
        public bool IsBit { get; set; }

        /// <summary>
        /// 写入数据（仅写入请求时有值，读取请求为 null）
        /// </summary>
        public byte[] WriteData { get; set; }
    }
}
