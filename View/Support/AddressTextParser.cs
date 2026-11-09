using PlcTest.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace PlcTest.View.Support
{
    /// <summary>
    /// 地址文本要按位还是按字解析。
    /// <para>
    /// 位/字不是从字符串里猜出来的，而是「区域能力 + 用户选择」的结果：
    /// 三菱的 M 区既能按位也能按字（M100 一个位、M100 起 16 点算一个字），
    /// 只看 "M100" 无法判断用户想要哪种，所以由调用方把意图传进来。
    /// </para>
    /// </summary>
    public enum AddressValueKind
    {
        /// <summary>按位访问（bool）</summary>
        Bit = 0,
        /// <summary>按字访问（short）</summary>
        Word = 1,
    }

    /// <summary>
    /// 地址文本解析：把用户敲的 "M100" / "d200" 变成 <see cref="PlcAddress"/>，以及反向格式化。
    /// <para>
    /// 为什么要有它：旧窗体把地址拆成「区域下拉 + 起始地址 + 长度」三个控件，
    /// 区域只能从下拉里选，输入 "DB1.DBW0" 或 "M0.3" 这种复合写法根本表达不了；
    /// 而新窗体要支持手敲地址，就必须有一处**唯一**的解析规则，不能让每个窗体各写一套 Split。
    /// </para>
    /// <para>
    /// 解析口径：
    /// <list type="bullet">
    /// <item>软元件代码就是 <see cref="MemoryArea"/> 的枚举名（M / X / D / TS / CIO / ZR …），
    /// 不用 catalog 里的 <c>Name</c>——1E 表的名字带尾空格（"D "），那是发帧用的 ASCII 代码，不是给用户看的。</item>
    /// <item>大小写不敏感，中间与首尾的空格（含全角空格）一律忽略。</item>
    /// <item>最长前缀优先：SM100 先命中 SM 而不是 S，ZR0 先命中 ZR 而不是 Z，DX100 先命中 DX 而不是 D。</item>
    /// <item>只解析「区域 + 偏移」，长度由界面单独填；位偏移（M0.3）与 S7 DB 区暂时明确拒绝并给出原因，
    /// 不做「猜一个最接近的」这种静默降级。</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class AddressTextParser
    {
        /// <summary>
        /// 解析地址文本。
        /// </summary>
        /// <param name="text">用户输入，例如 "M100"、"d200"、"TS5"</param>
        /// <param name="capability">当前协议的能力快照，决定哪些区域可解析、能否按位/按字</param>
        /// <param name="kind">这次要按位还是按字访问</param>
        /// <returns>成功时 Data 为 <see cref="PlcAddress"/>；失败时 ErrorMsg 是可直接显示给用户的中文原因</returns>
        public static Result<PlcAddress> Parse(string text, ProtocolCapability capability, AddressValueKind kind)
        {
            if (capability == null || capability.IsEmpty)
            {
                return Fail("尚未取得协议能力信息，无法解析地址");
            }

            string normalized = Normalize(text);
            if (normalized.Length == 0)
            {
                return Fail("请输入地址，例如 " + capability.SuggestExample(kind == AddressValueKind.Bit));
            }

            // DB 区在 MemoryArea 里还没有对应项（README 待办：S7 DB 区）。这里给一句明确的话，
            // 不要让用户看到「无法识别的地址 DB1.DBW0」然后自己去猜是不是敲错了。
            if (normalized.StartsWith("DB", StringComparison.Ordinal))
            {
                return Fail("暂不支持 S7 DB 区地址（如 DB1.DBW0），请先使用 "
                    + capability.SuggestExample(kind == AddressValueKind.Bit) + " 这类地址");
            }

            MemoryArea area;
            string numberText;
            if (TryMatch(capability.Areas, normalized, out area, out numberText))
            {
                return Build(area, numberText, capability, kind);
            }

            // 协议没命中，再用全部软元件代码试一次：能区分「当前协议不支持这个区」和「压根没这个区」，
            // 这两种错误的处理方式不一样（前者换设备，后者是自己敲错了）。
            MemoryArea knownArea;
            string knownNumber;
            if (TryMatch(AllAreas(), normalized, out knownArea, out knownNumber))
            {
                return Fail("当前协议 " + capability.DisplayName + " 不支持 " + knownArea + " 区");
            }

            if (normalized.IndexOf('.') >= 0)
            {
                // "M0.3"：PlcAddress 只有 区域 + 偏移 + 是否为位，没有「字内第几位」，
                // 硬解析会把 M0.3 变成 M0，读到的点跟用户想的不是同一个——宁可拒绝。
                return Fail("暂不支持位偏移写法（如 M0.3），请使用整点或整字地址");
            }

            return Fail("无法识别的地址：" + text.Trim()
                + "，格式为「软元件代码 + 偏移」，例如 " + capability.SuggestExample(kind == AddressValueKind.Bit));
        }

        /// <summary>
        /// 反向格式化：把地址还原成用户可以继续编辑的文本，如 "M100"。
        /// <para>
        /// 不能用 <see cref="PlcAddress.ToString"/>——它为了调试会给字地址加 "W" 后缀（"DW100"），
        /// 那个后缀不是合法输入，回填到输入框里再解析就会失败。
        /// </para>
        /// </summary>
        public static string Format(PlcAddress address)
        {
            if (address == null)
            {
                return string.Empty;
            }

            string db = address.DbNumber.HasValue ? "DB" + address.DbNumber.Value + "." : string.Empty;
            return db + address.Area + address.Address;
        }

        #region 解析细节

        /// <summary>
        /// 数值部分交给 <see cref="PlcAddress"/>，同时按 kind 校验区域能力。
        /// </summary>
        private static Result<PlcAddress> Build(
            MemoryArea area, string numberText, ProtocolCapability capability, AddressValueKind kind)
        {
            int address;
            if (!int.TryParse(numberText, out address))
            {
                return Fail(area + " 区地址数值超出范围：" + numberText);
            }

            if (kind == AddressValueKind.Bit)
            {
                if (!capability.SupportsBit(area))
                {
                    return Fail(DescribeAccess(area, capability, true));
                }
                return Result<PlcAddress>.Success(PlcAddress.Bit(area, address));
            }

            if (!capability.SupportsWord(area))
            {
                return Fail(DescribeAccess(area, capability, false));
            }
            return Result<PlcAddress>.Success(PlcAddress.Word(area, address));
        }

        /// <summary>
        /// 拼一句能说清「为什么这个区不能这样访问」的话，而不是笼统的「地址非法」。
        /// </summary>
        private static string DescribeAccess(MemoryArea area, ProtocolCapability capability, bool wantBit)
        {
            string reason;
            if (capability.SupportsBit(area))
            {
                reason = "只能按位";
            }
            else if (capability.SupportsWord(area))
            {
                reason = "只能按字";
            }
            else
            {
                reason = "该协议未声明可访问方式";
            }

            return area + " 区不支持按" + (wantBit ? "位" : "字") + "访问（" + reason + "）";
        }

        /// <summary>
        /// 在候选区域里做最长前缀匹配，并要求剩余部分是纯数字。
        /// </summary>
        private static bool TryMatch(
            IReadOnlyList<DeviceAreaInfo> candidates, string text, out MemoryArea area, out string numberText)
        {
            area = MemoryArea.D;
            numberText = null;
            if (candidates == null || candidates.Count == 0)
            {
                return false;
            }

            var areas = new List<MemoryArea>(candidates.Count);
            foreach (DeviceAreaInfo info in candidates)
            {
                areas.Add(info.Area);
            }
            return TryMatch(areas, text, out area, out numberText);
        }

        private static bool TryMatch(
            IList<MemoryArea> candidates, string text, out MemoryArea area, out string numberText)
        {
            area = MemoryArea.D;
            numberText = null;

            // 最长前缀优先：SM 必须先于 S 命中，ZR 先于 Z，DX 先于 D。
            // 同长度之间不会互相干扰（SB / SD 只有完全相同的写法才会同时命中，而代码唯一）。
            var ordered = new List<MemoryArea>(candidates);
            ordered.Sort((a, b) => b.ToString().Length.CompareTo(a.ToString().Length));

            foreach (MemoryArea candidate in ordered)
            {
                string code = candidate.ToString();
                if (text.Length <= code.Length)
                {
                    continue;
                }
                if (string.CompareOrdinal(text, 0, code, 0, code.Length) != 0)
                {
                    continue;
                }

                string tail = text.Substring(code.Length);
                if (IsAllDigits(tail))
                {
                    area = candidate;
                    numberText = tail;
                    return true;
                }
            }

            return false;
        }

        private static IList<MemoryArea> _allAreas;

        /// <summary>
        /// 全部软元件代码（不限协议），只用于判断「这个写法本身是不是存在」。
        /// </summary>
        private static IList<MemoryArea> AllAreas()
        {
            if (_allAreas == null)
            {
                var values = new List<MemoryArea>();
                foreach (MemoryArea area in Enum.GetValues(typeof(MemoryArea)))
                {
                    values.Add(area);
                }
                _allAreas = values;
            }
            return _allAreas;
        }

        private static bool IsAllDigits(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] < '0' || text[i] > '9')
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 去掉所有空白并转大写。允许用户写成 "m 100" 这种带空格的形式，
        /// 因为从手册复制的地址经常带空格。
        /// </summary>
        private static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }
                builder.Append(char.ToUpperInvariant(c));
            }
            return builder.ToString();
        }

        /// <summary>
        /// 入参校验失败：报文没有发出，调用方按 Validation 处理即可。
        /// </summary>
        private static Result<PlcAddress> Fail(string message)
        {
            return Result<PlcAddress>.Fail(message, ErrorKind.Validation);
        }

        #endregion
    }
}
