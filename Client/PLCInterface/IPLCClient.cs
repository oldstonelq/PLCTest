using PLCTest.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static PLCTest.Models.Enums;

namespace PLCTest.PLCInterface
{
    /// <summary>
    /// 基础PLC设备接口
    /// </summary>
    public interface IPLCClient
    {
        /// <summary>
        /// 连接状态
        /// </summary>
        bool IsConnected { get; }
        /// <summary>
        /// 连接
        /// </summary>
        void Connect();
        /// <summary>
        /// 关闭连接
        /// </summary>
        void Disconnect();
        /// <summary>
        /// 读取位数据
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <returns>读取结果</returns>
        Result<bool> ReadBit(PLCAddress address);
        /// <summary>
        /// 读取位数组。
        /// </summary>
        /// <param name="address">PLC地址。</param>
        /// <returns>读取结果</returns>
        Result<bool[]> ReadBitArray(PLCAddress address);
        /// <summary>
        /// 写入位数据
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">位数据</param>
        /// <returns>写入结果</returns>
        Result<bool> WriteBit(PLCAddress address, bool value);
        /// <summary>
        /// 写入位数组
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="values">位数据数组</param>
        /// <returns>写入结果</returns>
        Result<bool> WriteBitArray(PLCAddress address, bool[] values);
        /// <summary>
        /// 读取字数据
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <returns>读取结果</returns>
        Result<short> ReadWord(PLCAddress address);
        /// <summary>
        /// 读取字数组。
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <returns>读取结果</returns>
        Result<short[]> ReadWordArray(PLCAddress address);
        /// <summary>
        /// 写入字数据
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">字数据</param>
        /// <returns>写入结果</returns>
        Result<bool> WriteWord(PLCAddress address, short value);
        /// <summary>
        /// 写入字数组
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="values">字数据数组</param>
        /// <returns>写入结果</returns>
        Result<bool> WriteWordArray(PLCAddress address, short[] values);
        /// <summary>
        /// 交互信息委托
        /// </summary>
         event Action<string> ShowCommMessageEvent;
    }
}
