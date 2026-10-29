using PlcTest.Models;
using System;
using System.Collections.Generic;

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
        /// 当前协议支持的全部软元件区域。
        /// 界面用它填充选择框，避免让用户选到这台 PLC 上根本不存在的软元件。
        /// </summary>
        /// <returns>受支持的区域，按枚举声明顺序</returns>
        IReadOnlyList<MemoryArea> GetSupportedAreas();
        /// <summary>
        /// 该软元件区域在当前协议下允许的访问方式。
        /// 界面据此决定本次读写走位分支还是字分支，不再硬编码"是不是 D"。
        /// </summary>
        /// <param name="area">软元件区域</param>
        /// <returns>允许的访问方式</returns>
        AreaAccess GetAreaAccess(MemoryArea area);
        /// <summary>
        /// 连接：同步建立连接并把结果返回给调用方，成功后才启动后台心跳（断线自动重连）。
        /// 连接失败不再静默，调用方可以直接把 ErrorMsg 显示出来。
        /// </summary>
        /// <returns>连接结果</returns>
        Result<bool> Connect();
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
