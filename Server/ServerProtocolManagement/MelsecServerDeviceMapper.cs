using PlcTest.Client.ClientProtocolManagement.Melsec;
using PlcTest.Models;

namespace PlcTest.Server.ServerProtocolManagement
{
    /// <summary>
    /// 把请求里的软元件代码翻译成统一区域。
    /// 服务端不再各写一份 switch——码表直接取自客户端同一份 catalog，
    /// 因此「客户端认得哪些软元件，服务端就模拟哪些」，不会有第三套口径。
    /// </summary>
    internal static class MelsecServerDeviceMapper
    {
        /// <summary>
        /// 3E / 4E 二进制：1 字节设备码（如 0xA8 = D、0x91 = SM）。
        /// </summary>
        public static MemoryArea? From3EBinary(int code)
        {
            foreach (Melsec3E4EDeviceSpec spec in Melsec3E4EDeviceCatalog.All)
            {
                if (spec.BinaryCode == code)
                {
                    return spec.Area;
                }
            }
            return null;
        }

        /// <summary>
        /// 3E / 4E ASCII：双字符码（如 "D*" = 0x442A）。
        /// 手册允许把第 2 个字符的 '*' 写成空格（20H），这里一并兼容。
        /// </summary>
        public static MemoryArea? From3EAscii(int code)
        {
            foreach (Melsec3E4EDeviceSpec spec in Melsec3E4EDeviceCatalog.All)
            {
                if (spec.AsciiCodeWord == code)
                {
                    return spec.Area;
                }
                // "D " → "D*"
                if ((code & 0xFF) == 0x20 && spec.AsciiCodeWord == ((code & 0xFF00) | 0x2A))
                {
                    return spec.Area;
                }
            }
            return null;
        }

        /// <summary>
        /// 1E：双字符码，二进制与 ASCII 用同一套（12 种）。
        /// </summary>
        public static MemoryArea? From1E(int code)
        {
            foreach (Melsec1EDeviceSpec spec in Melsec1EDeviceCatalog.All)
            {
                if (spec.Code == code)
                {
                    return spec.Area;
                }
            }
            return null;
        }
    }
}
