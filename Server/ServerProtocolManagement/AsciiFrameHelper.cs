namespace PlcTest.Server.ServerProtocolManagement
{
    /// <summary>
    /// MC 协议 ASCII 帧的公共解析工具。
    /// </summary>
    internal static class AsciiFrameHelper
    {
        /// <summary>
        /// 把若干位十六进制 ASCII 文本解析成整数（如 "0018" → 0x18）。
        /// </summary>
        /// <param name="data">原始字节</param>
        /// <param name="offset">起始偏移</param>
        /// <param name="count">十六进制字符个数</param>
        /// <param name="value">解析结果</param>
        /// <returns>全部字符都是合法十六进制数字时返回 true</returns>
        public static bool TryParseHex(byte[] data, int offset, int count, out int value)
        {
            value = 0;
            if (data == null || offset < 0 || count <= 0 || offset + count > data.Length)
            {
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                int digit = HexValue(data[offset + i]);
                if (digit < 0)
                {
                    return false;
                }
                value = value * 16 + digit;
            }
            return true;
        }

        private static int HexValue(byte b)
        {
            if (b >= '0' && b <= '9') return b - '0';
            if (b >= 'A' && b <= 'F') return b - 'A' + 10;
            if (b >= 'a' && b <= 'f') return b - 'a' + 10;
            return -1;
        }
    }
}
