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
        /// 设备代码（0xA8=D寄存器, 0x90=M线圈, 0x9C=X输入, 0x9D=Y输出）
        /// </summary>
        public byte DeviceCode { get; set; }

        /// <summary>
        /// 起始地址（24位）
        /// </summary>
        public int StartAddress { get; set; }

        /// <summary>
        /// 软元件点数
        /// </summary>
        public ushort Points { get; set; }

        /// <summary>
        /// 写入数据（仅写入请求时有值，读取请求为 null）
        /// </summary>
        public byte[] WriteData { get; set; }
    }
}
