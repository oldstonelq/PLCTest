using PlcTest.Models;
using System;

namespace PlcTest.Tool
{
    /// <summary>
    /// 协议选项取值的公共辅助：夹范围、按默认值兜底。
    /// <para>
    /// 选项值来自界面输入，而协议里的字段往往只有一两个字节的空间（网络号、站号、机架槽号）。
    /// 与其每个协议各写一份 <c>if (v &lt; 0) return 0;</c>，不如集中在这里，
    /// 取值行为一致，出错时也只改一处。
    /// </para>
    /// </summary>
    public static class ProtocolOptionValues
    {
        /// <summary>
        /// 取整数并夹到闭区间；超出时贴边而不是抛异常——选项越界在对话框阶段已经被拦下，
        /// 这里只是兜底，避免拼帧时写坏字节。
        /// </summary>
        public static int ClampInt(ProtocolOptions options, string key, int fallback, int min, int max)
        {
            int value = options == null ? fallback : options.GetValue(key, fallback);
            if (value < min)
            {
                return min;
            }
            if (value > max)
            {
                return max;
            }
            return value;
        }

        /// <summary>
        /// 取一个字节范围的选项（0–255）。
        /// </summary>
        public static byte ClampByte(ProtocolOptions options, string key, byte fallback)
        {
            return (byte)ClampInt(options, key, fallback, 0, 255);
        }

        /// <summary>
        /// 取枚举选项；值不是该枚举类型时返回默认值（例如选项来自另一份协议的表单）。
        /// </summary>
        public static TEnum GetEnum<TEnum>(ProtocolOptions options, string key, TEnum fallback)
            where TEnum : struct
        {
            if (options == null)
            {
                return fallback;
            }

            object value = options.Get(key);
            if (value is TEnum)
            {
                return (TEnum)value;
            }

            if (value == null)
            {
                return fallback;
            }

            try
            {
                return (TEnum)Enum.Parse(typeof(TEnum), Convert.ToString(value), true);
            }
            catch (Exception)
            {
                return fallback;
            }
        }
    }
}
