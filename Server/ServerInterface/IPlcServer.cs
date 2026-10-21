using PlcTest.Models;
using System;

namespace PlcTest.Server.ServerInterface
{
    /// <summary>
    /// Unified PLC server contract describing the soft devices a client can read and write.
    /// </summary>
    public interface IPlcServer
    {
        /// <summary>True while the server is listening.</summary>
        bool ServerIsOpen { get; }

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
    }
}
