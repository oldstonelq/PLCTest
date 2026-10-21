using PlcTest.Models;
using System;

namespace PlcTest.Client.ClientInterface
{
    /// <summary>
    /// 基础PLC设备接口
    /// </summary>
    public interface IPlcClient
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
        Result<bool> ReadBit(PlcAddress address);
        /// <summary>
        /// 读取位数组。
        /// </summary>
        /// <param name="address">PLC地址。</param>
        /// <returns>读取结果</returns>
        Result<bool[]> ReadBitArray(PlcAddress address);
        /// <summary>
        /// 写入位数据
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">位数据</param>
        /// <returns>写入结果</returns>
        Result<bool> WriteBit(PlcAddress address, bool value);
        /// <summary>
        /// 写入位数组
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="values">位数据数组</param>
        /// <returns>写入结果</returns>
        Result<bool> WriteBitArray(PlcAddress address, bool[] values);
        /// <summary>
        /// 读取字数据
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <returns>读取结果</returns>
        Result<short> ReadWord(PlcAddress address);
        /// <summary>
        /// 读取字数组。
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <returns>读取结果</returns>
        Result<short[]> ReadWordArray(PlcAddress address);
        /// <summary>
        /// 写入字数据
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="value">字数据</param>
        /// <returns>写入结果</returns>
        Result<bool> WriteWord(PlcAddress address, short value);
        /// <summary>
        /// 写入字数组
        /// </summary>
        /// <param name="address">PLC地址</param>
        /// <param name="values">字数据数组</param>
        /// <returns>写入结果</returns>
        Result<bool> WriteWordArray(PlcAddress address, short[] values);
        /// <summary>
        /// 交互信息委托
        /// </summary>
         event Action<string> ShowCommMessageEvent;
    }
}
