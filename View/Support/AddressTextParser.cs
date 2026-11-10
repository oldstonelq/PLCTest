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
    /// <item>只解析「区域 + 偏移」，长度由界面单独填；位偏移（M0.3）明确拒绝，不做「猜一个最接近的」这种静默降级。</item>
    /// <item>S7 的 DB 区是唯一的例外，按 <c>DB1.DBW0</c> 单独解析（块号 + 块内字节偏移），
    /// 因为它的写法是「区域 + 两个数字」，通用匹配装不下。</item>
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

            // DB 区必须先拦下来：它带块号（DB1.DBW0），交给下面的「软元件代码 + 纯数字」匹配会把
            // "DB100" 误当成「DB 区第 100 字」——那样 DbNumber 是 null，发帧时实际读的是 DB0。
            if (normalized.StartsWith("DB", StringComparison.Ordinal))
            {
                return ParseDb(normalized, capability, kind);
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

            // DB 区不能走下面的通用拼接：那会得到 "DB1.DB0"，而合法写法是 "DB1.DBW0"
            if (address.Area == MemoryArea.DB)
            {
                return "DB" + (address.DbNumber ?? 0) + ".DBW" + address.Address;
            }

            string db = address.DbNumber.HasValue ? "DB" + address.DbNumber.Value + "." : string.Empty;
            return db + address.Area + address.Address;
        }

        #region 解析细节

        /// <summary>
        /// 解析 S7 的 DB 区地址，形如 <c>DB1.DBW0</c>：DB 块号 + 块内字节偏移，中间点分隔。
        /// <para>
        /// 为什么单独一个方法：DB 区是唯一「区域 + 两个数字」的写法，通用匹配只认
        /// 「软元件代码 + 一个数字」，硬套会把块号丢掉。宽度标记（DBW / DBB / DBD / DBX）
        /// 也在这里区分——目前只实现了按字，其余三种明确拒绝，而不是悄悄当成字去读。
        /// </para>
        /// </summary>
        private static Result<PlcAddress> ParseDb(string text, ProtocolCapability capability, AddressValueKind kind)
        {
            if (!capability.Supports(MemoryArea.DB))
            {
                return Fail("当前协议 " + capability.DisplayName + " 不支持 DB 区地址");
            }

            if (kind == AddressValueKind.Bit)
            {
                // 位偏移还没做（PlcAddress 没有字内位号）。与其发一条注定失败的帧，
                // 不如在这里说清楚该换成哪个区。
                return Fail("DB 区目前只支持按字访问（如 DB1.DBW0），按位请用 M / X / Y 区");
            }

            int dot = text.IndexOf('.');
            if (dot < 0)
            {
                return Fail("DB 区地址要写成 DB<块号>.DBW<偏移>，例如 DB1.DBW0");
            }

            int dbNumber;
            string dbText = text.Substring(2, dot - 2);
            if (dbText.Length == 0 || !int.TryParse(dbText, out dbNumber) || dbNumber < 1 || dbNumber > 65535)
            {
                return Fail("DB 块号必须是 1–65535 之间的整数：" + text);
            }

            string body = text.Substring(dot + 1);
            if (body.StartsWith("DBX", StringComparison.Ordinal))
            {
                return Fail("暂不支持 DB 区位偏移（如 DB1.DBX0.3），请使用整字地址 DB1.DBW0");
            }
            if (body.StartsWith("DBB", StringComparison.Ordinal) || body.StartsWith("DBD", StringComparison.Ordinal))
            {
                return Fail("暂不支持 DB 区字节/双字读写（DB1.DBB0 / DB1.DBD0），请使用整字地址 DB1.DBW0");
            }
            if (!body.StartsWith("DBW", StringComparison.Ordinal))
            {
                return Fail("DB 区地址要写成 DB<块号>.DBW<偏移>，例如 DB1.DBW0");
            }

            int offset;
            string offsetText = body.Substring(3);
            if (!int.TryParse(offsetText, out offset) || offset < 0 || offset > 65535)
            {
                return Fail("DB 区偏移必须是 0–65535 之间的整数：" + text);
            }

            return Result<PlcAddress>.Success(PlcAddress.Word(MemoryArea.DB, offset, dbNumber));
        }

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
