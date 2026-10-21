using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PlcTest.Tool
{
    /// <summary>
    /// 其他工具类
    /// </summary>
    public class OtherTool
    {
        /// <summary>
        /// 校验字符串是否是IP地址，并返回标准化的IP地址对象
        /// </summary>
        /// <param name="input">输入字符串</param>
        /// <param name="ipAddr">输入转化</param>
        /// <returns></returns>
        public static bool CheckStandardIPv4(string input, out IPAddress ipAddr)
        {
            ipAddr = null;
            //删除首尾空格
            string raw = input.Trim();
            //分割字符串
            string[] parts = raw.Split('.');

            // 校验分段数量
            if (parts.Length != 4)
                return false;

            int[] nums = new int[4];
            for (int i = 0; i < 4; i++)
            {
                string seg = parts[i].Trim();
                // 空段：192..168.1
                if (string.IsNullOrEmpty(seg))
                    return false;
                // 禁止前导零，按需注释此行
                if (seg.Length > 1 && seg[0] == '0')
                    return false;
                // 转数字
                if (!int.TryParse(seg, out int num))
                    return false;
                // 范围校验
                if (num < 0 || num > 255)
                    return false;
                nums[i] = num;
            }

            // 拼接标准无零IP
            string standardIp = $"{nums[0]}.{nums[1]}.{nums[2]}.{nums[3]}";
            return IPAddress.TryParse(standardIp, out ipAddr);
        }

        /// <summary>
        /// 字符串转 int，附带区间校验
        /// </summary>
        /// <param name="input">输入文本</param>
        /// <param name="value">输出数值</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <returns>true=解析成功且在区间内</returns>
        public static bool StringTryParseInt(string input, out int value, int min = int.MinValue, int max = int.MaxValue)
        {
            value = 0;
            if (!int.TryParse(input, out var temp))
                return false;

            if (temp < min || temp > max)
                return false;

            value = temp;
            return true;
        }

        /// <summary>
        /// 字符串转 short，附带区间校验
        /// </summary>
        public static bool StringTryParseShort(string input, out short value, short min = short.MinValue, short max = short.MaxValue)
        {
            value = 0;
            if (!short.TryParse(input, out var temp))
                return false;

            if (temp < min || temp > max)
                return false;

            value = temp;
            return true;
        }

        /// <summary>
        /// 获取软件版本号
        /// </summary>
        /// <returns></returns>
        public static string GetSoftwareVersion()
        {
            return "(Version:" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + ")"; // 返回软件版本号
        }
    }
}
