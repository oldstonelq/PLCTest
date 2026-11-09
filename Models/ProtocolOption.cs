using System;
using System.Collections.Generic;
using System.Text;

namespace PlcTest.Models
{
    /// <summary>
    /// 协议私有选项的编辑形态。
    /// </summary>
    public enum ProtocolOptionKind
    {
        /// <summary>枚举下拉（如机型、字节序）</summary>
        Choice = 0,

        /// <summary>整数输入（如机架号、站号）</summary>
        Number = 1,
    }

    /// <summary>
    /// 下拉里的一个可选项。
    /// </summary>
    public sealed class ProtocolOptionValue
    {
        /// <summary>写进 <see cref="ProtocolOptions"/> 的实际值（枚举实例）</summary>
        public object Value { get; private set; }

        /// <summary>界面显示文本</summary>
        public string Text { get; private set; }

        public ProtocolOptionValue(object value, string text)
        {
            Value = value;
            Text = string.IsNullOrEmpty(text) ? Convert.ToString(value) : text;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    /// <summary>
    /// 一条协议私有选项的描述：键、显示名、编辑形态、默认值、取值范围。
    /// <para>
    /// 存在的理由：以前这些参数只能靠 <c>(client.Protocol as InovanceModbusTcpClientProtocol).CpuModel = ...</c>
    /// 这种强制转换去改，界面层根本不知道某个协议有哪些可配项。把描述抽出来以后，
    /// 界面按描述生成控件、Builder 按描述取值，新增一个厂商参数不需要再改窗体。
    /// </para>
    /// </summary>
    public sealed class ProtocolOption
    {
        private static readonly ProtocolOptionValue[] NoChoices = new ProtocolOptionValue[0];

        /// <summary>取值用的键，例如 CpuModel / DataFormat / Rack</summary>
        public string Key { get; private set; }

        /// <summary>界面显示名，例如「机型」「字节序」</summary>
        public string Label { get; private set; }

        /// <summary>编辑形态</summary>
        public ProtocolOptionKind Kind { get; private set; }

        /// <summary>默认值（枚举实例或 int）</summary>
        public object DefaultValue { get; private set; }

        /// <summary>下拉可选项；<see cref="ProtocolOptionKind.Number"/> 时为空</summary>
        public IReadOnlyList<ProtocolOptionValue> Choices { get; private set; }

        /// <summary>允许的最小值，仅 Number 有效</summary>
        public int Minimum { get; private set; }

        /// <summary>允许的最大值，仅 Number 有效</summary>
        public int Maximum { get; private set; }

        /// <summary>补充说明，界面显示在控件右侧</summary>
        public string Hint { get; private set; }

        /// <summary>
        /// 枚举型选项：可选项直接取枚举的所有成员。
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="key">取值键</param>
        /// <param name="label">显示名</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="hint">说明</param>
        public static ProtocolOption Choice<T>(string key, string label, T defaultValue, string hint = null)
            where T : struct
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("Choice 只支持枚举类型", "T");
            }

            var choices = new List<ProtocolOptionValue>();
            foreach (T item in Enum.GetValues(typeof(T)))
            {
                choices.Add(new ProtocolOptionValue(item, item.ToString()));
            }

            return new ProtocolOption
            {
                Key = key,
                Label = label,
                Kind = ProtocolOptionKind.Choice,
                DefaultValue = defaultValue,
                Choices = choices,
                Hint = hint ?? string.Empty,
            };
        }

        /// <summary>
        /// 整数型选项：带闭区间范围校验，越界在界面层就被拦下，不会带着非法值去发帧。
        /// </summary>
        /// <param name="key">取值键</param>
        /// <param name="label">显示名</param>
        /// <param name="defaultValue">默认值</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="hint">说明</param>
        public static ProtocolOption Int(string key, string label, int defaultValue, int min, int max, string hint = null)
        {
            return new ProtocolOption
            {
                Key = key,
                Label = label,
                Kind = ProtocolOptionKind.Number,
                DefaultValue = defaultValue,
                Choices = NoChoices,
                Minimum = min,
                Maximum = max,
                Hint = hint ?? string.Empty,
            };
        }

        /// <summary>
        /// 取某个值在界面上的显示文本：枚举项查下拉表，整数直接转字符串。
        /// </summary>
        public string Format(object value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            foreach (ProtocolOptionValue choice in Choices)
            {
                if (Equals(choice.Value, value))
                {
                    return choice.Text;
                }
            }
            return Convert.ToString(value);
        }

        /// <summary>
        /// 校验并转换一个外部值（通常来自界面）：范围越界返回 false 并给出原因。
        /// </summary>
        public bool TryAccept(object value, out string error)
        {
            error = null;

            if (value == null)
            {
                error = Label + " 不能为空";
                return false;
            }

            if (Kind == ProtocolOptionKind.Choice)
            {
                foreach (ProtocolOptionValue choice in Choices)
                {
                    if (Equals(choice.Value, value))
                    {
                        return true;
                    }
                }
                error = Label + " 的取值不在可选项内：" + Convert.ToString(value);
                return false;
            }

            int number;
            try
            {
                number = Convert.ToInt32(value);
            }
            catch (Exception)
            {
                error = Label + " 必须是整数：" + Convert.ToString(value);
                return false;
            }

            if (number < Minimum || number > Maximum)
            {
                error = string.Format("{0} 超出范围 {1}–{2}：{3}", Label, Minimum, Maximum, number);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 调试用：Key=默认值（区间）。
        /// </summary>
        public override string ToString()
        {
            return Kind == ProtocolOptionKind.Choice
                ? string.Format("{0}={1}", Key, Format(DefaultValue))
                : string.Format("{0}={1}[{2}..{3}]", Key, DefaultValue, Minimum, Maximum);
        }
    }

    /// <summary>
    /// 一组协议私有选项的取值：只是「键 → 值」的袋子，不含任何协议知识。
    /// <para>
    /// 界面按 <see cref="ProtocolOption"/> 描述往里填，Builder 在 Build() 时把它交给
    /// <c>IProtocolOptionHost</c> 实现；协议自己决定认识哪几个键，不认识的就忽略。
    /// </para>
    /// </summary>
    public sealed class ProtocolOptions
    {
        private readonly Dictionary<string, object> _values =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>空集合（每次返回新实例，避免共享可变状态）</summary>
        public static ProtocolOptions Empty
        {
            get { return new ProtocolOptions(); }
        }

        /// <summary>已设置的选项个数</summary>
        public int Count
        {
            get { return _values.Count; }
        }

        /// <summary>
        /// 设置一个值；返回自身，便于链式调用。
        /// </summary>
        public ProtocolOptions Set(string key, object value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("选项键不能为空", "key");
            }

            _values[key] = value;
            return this;
        }

        /// <summary>是否设置过该键</summary>
        public bool Contains(string key)
        {
            return !string.IsNullOrEmpty(key) && _values.ContainsKey(key);
        }

        /// <summary>取值；没有设置返回 null</summary>
        public object Get(string key)
        {
            object value;
            return !string.IsNullOrEmpty(key) && _values.TryGetValue(key, out value) ? value : null;
        }

        /// <summary>
        /// 取强类型值；没设置或类型不符时返回 <paramref name="fallback"/>。
        /// 协议实现里用它拿默认值，避免每个键都判空。
        /// </summary>
        public T GetValue<T>(string key, T fallback)
        {
            object value = Get(key);
            if (value == null)
            {
                return fallback;
            }

            if (value is T)
            {
                return (T)value;
            }

            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        /// <summary>
        /// 复制一份（界面点「取消」后要丢弃编辑结果时用）。
        /// </summary>
        public ProtocolOptions Clone()
        {
            var copy = new ProtocolOptions();
            foreach (KeyValuePair<string, object> pair in _values)
            {
                copy.Set(pair.Key, pair.Value);
            }
            return copy;
        }

        /// <summary>
        /// 按描述拼一行人类可读文本，如「机型=Easy · 字节序=CDAB」。
        /// 只输出真正设置过的键；<paramref name="schema"/> 为空时退化为「键=值」。
        /// </summary>
        public string Describe(IReadOnlyList<ProtocolOption> schema)
        {
            if (_values.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();

            if (schema != null && schema.Count > 0)
            {
                foreach (ProtocolOption option in schema)
                {
                    if (!_values.ContainsKey(option.Key))
                    {
                        continue;
                    }
                    AppendPair(builder, option.Label, option.Format(_values[option.Key]));
                }
                return builder.ToString();
            }

            foreach (KeyValuePair<string, object> pair in _values)
            {
                AppendPair(builder, pair.Key, Convert.ToString(pair.Value));
            }
            return builder.ToString();
        }

        private static void AppendPair(StringBuilder builder, string label, string value)
        {
            if (builder.Length > 0)
            {
                builder.Append(" · ");
            }
            builder.Append(label).Append('=').Append(value);
        }

        public override string ToString()
        {
            return Describe(null);
        }
    }
}
