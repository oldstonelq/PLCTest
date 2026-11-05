using PlcTest.Models;
using System;
using System.Collections.Generic;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// Unified PLC server contract describing the soft devices a client can read and write.
    /// </summary>
    public interface IPlcServer
    {
        /// <summary>True while the server is listening.</summary>
        bool ServerIsOpen { get; }

        /// <summary>
        /// 本服务端支持的软元件区域，供界面「软元件视图」列行（与客户端的同名方法对称）。
        /// </summary>
        IReadOnlyList<MemoryArea> GetSupportedAreas();
        /// <summary>Number of currently connected clients.</summary>
        int ClientCount { get; }

        /// <summary>Starts listening for client connections.</summary>
        void OpenServer();

        /// <summary>Stops listening and releases the listener resources.</summary>
        void CloseServer();

        Result<bool> ReadBit(PlcAddress address);
        Result<bool[]> ReadBitArray(PlcAddress address);
        Result<bool> WriteBit(PlcAddress address, bool value);
        Result<bool> WriteBitArray(PlcAddress address, bool[] values);
        Result<short> ReadWord(PlcAddress address);
        Result<short[]> ReadWordArray(PlcAddress address);
        Result<bool> WriteWord(PlcAddress address, short value);
        Result<bool> WriteWordArray(PlcAddress address, short[] values);

        /// <summary>
        /// 通讯日志事件：收到 / 发出的原始报文、以及丢弃、异常等提示。
        /// 由协议线程触发，订阅方（界面）需要自己切回 UI 线程再更新控件。
        /// </summary>
        event Action<string> ShowCommMessageEvent;
    }
}
