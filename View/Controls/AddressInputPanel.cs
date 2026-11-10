using PlcTest.Models;
using PlcTest.View.Support;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PlcTest.View.Controls
{
    /// <summary>
    /// 地址输入栏：文本框是主输入，区域下拉与偏移框是从属控件，三者双向同步。
    /// <para>
    /// 为什么以字符串为主：<c>M100</c>、<c>DB1.DBW0</c>、<c>M0.3</c> 这类写法用「区域下拉 + 偏移框」
    /// 表达不了；而纯字符串又让人记不住当前协议有哪些软元件。所以两边都要：
    /// 敲字能直接改下拉，点下拉也能回填文本，任何一边变了另一边立刻跟上。
    /// </para>
    /// <para>
    /// 位 / 字不是从地址里猜的：三菱 M 区两者都支持，只能由用户选。
    /// 但如果用户选了「位」却敲了 <c>D100</c>（D 只支持字），这里会自动切到字并说明原因——
    /// 已知唯一可行的解析方式时，让人先看到错误再回头改下拉是多余的一步。
    /// </para>
    /// </summary>
    public partial class AddressInputPanel : UserControl
    {
        private ProtocolCapability _capability = ProtocolCapability.Empty;
        private PlcAddress _address;
        private string _error;

        /// <summary>正在按代码回填控件，忽略由此产生的事件，避免「文本改下拉、下拉改文本」来回触发</summary>
        private bool _syncing;

        /// <summary>地址或长度变化时触发（解析失败也会触发，调用方靠 <see cref="IsValid"/> 判断）</summary>
        public event EventHandler AddressChanged;

        /// <summary>当前协议的能力快照；赋值后重建区域下拉框</summary>
        public ProtocolCapability Capability
        {
            get { return _capability; }
            set
            {
                _capability = value ?? ProtocolCapability.Empty;
                // 换了协议，上一份解析结果就作废了（比如从三菱换到汇川后 M 区只剩按位）
                _address = null;
                _error = null;

                FillAreaCombo();

                if (!string.IsNullOrEmpty(_tbAddress.Text))
                {
                    ReparseFromText();
                }
                else
                {
                    RefreshHint();
                }
            }
        }

        /// <summary>最近一次解析成功的地址；解析失败为 null</summary>
        public PlcAddress Address
        {
            get { return _address; }
        }

        /// <summary>地址文本（主输入）</summary>
        public string AddressText
        {
            get { return _tbAddress.Text; }
            set
            {
                _tbAddress.Text = value ?? string.Empty;
                ReparseFromText();
            }
        }

        /// <summary>本次读写点数，最小 1</summary>
        public int Length
        {
            get
            {
                int length;
                return int.TryParse(_tbLength.Text, out length) && length > 0 ? length : 1;
            }
            set { _tbLength.Text = (value < 1 ? 1 : value).ToString(); }
        }

        /// <summary>按位还是按字</summary>
        public AddressValueKind Kind
        {
            get { return _cmbKind.SelectedIndex == 1 ? AddressValueKind.Word : AddressValueKind.Bit; }
            set
            {
                _cmbKind.SelectedIndex = value == AddressValueKind.Word ? 1 : 0;
                ReparseFromText();
            }
        }

        /// <summary>地址是否可用（能解析且符合当前协议能力）</summary>
        public bool IsValid
        {
            get { return _address != null; }
        }

        /// <summary>解析失败的原因；可用时为空</summary>
        public string ErrorMessage
        {
            get { return _error; }
        }

        /// <summary>
        /// 构造：五个字段与提示行都在 Designer 里（可直接在设计器中查看/调整），
        /// 这里只挂事件——同步规则见 ReparseFromText / SyncFromAreaOrOffset。
        /// </summary>
        public AddressInputPanel()
        {
            InitializeComponent();

            _tbAddress.TextChanged += (s, e) => ReparseFromText();
            _cmbArea.SelectedIndexChanged += (s, e) => SyncFromAreaOrOffset();
            _tbOffset.TextChanged += (s, e) => SyncFromAreaOrOffset();
            _tbLength.TextChanged += (s, e) => ReparseFromText();
            _cmbKind.SelectedIndexChanged += (s, e) => ReparseFromText();
        }

        /// <summary>
        /// 由外部（例如左侧软元件列表）填一个初始地址，如 "M0"。
        /// </summary>
        public void SetAddress(string text)
        {
            AddressText = text;
        }

        #region 同步

        /// <summary>
        /// 文本 → 下拉 / 偏移。解析成功才回填，失败时保留用户原来的下拉选择（否则每敲一个字符下拉都会跳）。
        /// </summary>
        private void ReparseFromText()
        {
            if (_syncing)
            {
                return;
            }

            if (_capability.IsEmpty)
            {
                _address = null;
                _error = "尚未取得协议能力信息";
                RefreshHint();
                return;
            }

            string text = _tbAddress.Text;
            Result<PlcAddress> result = AddressTextParser.Parse(text, _capability, Kind);

            // 「位」下敲了只支持字的区域（如 D100）：换一种方式就能解析，直接切过去并说明
            if (!result.IsSuccess && text.Trim().Length > 0)
            {
                AddressValueKind other = Kind == AddressValueKind.Bit ? AddressValueKind.Word : AddressValueKind.Bit;
                Result<PlcAddress> retry = AddressTextParser.Parse(text, _capability, other);
                if (retry.IsSuccess)
                {
                    _syncing = true;
                    _cmbKind.SelectedIndex = other == AddressValueKind.Word ? 1 : 0;
                    _syncing = false;
                    result = retry;
                }
            }

            if (result.IsSuccess)
            {
                _address = result.Data;
                _address.Length = Length;
                _error = null;

                _syncing = true;
                SelectAreaQuietly(result.Data.Area);
                _tbOffset.Text = result.Data.Address.ToString();
                _syncing = false;
            }
            else
            {
                _address = null;
                _error = result.ErrorMsg;
            }

            RefreshHint();
            OnAddressChanged();
        }

        /// <summary>
        /// 下拉 / 偏移 → 文本。
        /// </summary>
        private void SyncFromAreaOrOffset()
        {
            if (_syncing)
            {
                return;
            }

            MemoryArea area;
            if (!TryGetSelectedArea(out area))
            {
                RefreshHint();
                return;
            }

            // 选区时若当前位/字能力不匹配，自动切到该区唯一可行的方式
            AutoSwitchKind(area);

            int offset;
            if (!int.TryParse(_tbOffset.Text, out offset) || offset < 0)
            {
                _address = null;
                _error = "偏移必须是非负整数";
                RefreshHint();
                OnAddressChanged();
                return;
            }

            _syncing = true;
            if (area == MemoryArea.DB)
            {
                // DB 区的块号不在这几个输入框里——偏移框放的是「块内偏移」。
                // 直接用 Format 会拼出 DB0.DBW0（DbNumber 为 null 时按 0 输出），被块号校验拒掉，
                // 所以沿用地址里已有的块号；用户还没敲过就用 1，让他接着改。
                int dbNumber = _address != null
                    && _address.Area == MemoryArea.DB
                    && _address.DbNumber.HasValue
                    ? _address.DbNumber.Value
                    : 1;
                _tbAddress.Text = "DB" + dbNumber + ".DBW" + offset;
            }
            else
            {
                _tbAddress.Text = AddressTextParser.Format(PlcAddress.Word(area, offset));
            }
            _syncing = false;

            // 文本已经由上面写好，这里做一次完整解析（带上长度、能力校验），保证 _address 与界面一致
            ReparseFromText();
        }

        /// <summary>
        /// 区域换了以后，按该区的位/字能力纠正当前选择。
        /// </summary>
        private void AutoSwitchKind(MemoryArea area)
        {
            AreaAccess access = _capability.GetAccess(area);
            if ((access & AreaAccess.Bit) != 0 && (access & AreaAccess.Word) != 0)
            {
                return;
            }

            AddressValueKind target;
            if ((access & AreaAccess.Bit) != 0)
            {
                target = AddressValueKind.Bit;
            }
            else if ((access & AreaAccess.Word) != 0)
            {
                target = AddressValueKind.Word;
            }
            else
            {
                return;
            }

            if (target == Kind)
            {
                return;
            }

            _syncing = true;
            _cmbKind.SelectedIndex = target == AddressValueKind.Word ? 1 : 0;
            _syncing = false;
        }

        private bool TryGetSelectedArea(out MemoryArea area)
        {
            area = MemoryArea.D;
            if (_cmbArea.SelectedItem == null)
            {
                return false;
            }
            return Enum.TryParse(_cmbArea.SelectedItem.ToString(), out area);
        }

        private void FillAreaCombo()
        {
            _syncing = true;
            _cmbArea.Items.Clear();
            foreach (DeviceAreaInfo info in _capability.Areas)
            {
                _cmbArea.Items.Add(info.Area.ToString());
            }

            if (_cmbArea.Items.Count > 0)
            {
                int preferred = _cmbArea.Items.IndexOf(_capability.DefaultArea.ToString());
                _cmbArea.SelectedIndex = preferred >= 0 ? preferred : 0;
            }
            _syncing = false;
        }

        private void SelectAreaQuietly(MemoryArea area)
        {
            int index = _cmbArea.Items.IndexOf(area.ToString());
            if (index >= 0 && _cmbArea.SelectedIndex != index)
            {
                _cmbArea.SelectedIndex = index;
            }
        }

        #endregion

        #region 提示

        /// <summary>
        /// 提示行：可用时显示「区域 · 偏移 · 方式 · 长度」，不可用时显示原因（红字）。
        /// </summary>
        private void RefreshHint()
        {
            if (_labHint == null)
            {
                return;
            }

            if (_address == null && _tbAddress != null && _tbAddress.Text.Trim().Length == 0)
            {
                // 刚打开还没输入：给个示例，但不要用红色报错吓人
                _labHint.ForeColor = Color.FromArgb(95, 94, 90);
                _labHint.Text = "输入地址，例如 " + _capability.SuggestExample(Kind == AddressValueKind.Bit);
                return;
            }

            if (_address != null)
            {
                DeviceAreaInfo info = _capability.Find(_address.Area);
                string name = info != null ? info.Name : string.Empty;
                _labHint.ForeColor = Color.FromArgb(15, 110, 86);
                _labHint.Text = string.Format("{0}{1} · {2} · {3} · {4} 点",
                    _address.Area, _address.Address, name,
                    Kind == AddressValueKind.Bit ? "按位" : "按字", Length);
            }
            else
            {
                _labHint.ForeColor = Color.FromArgb(163, 45, 45);
                _labHint.Text = _error ?? string.Empty;
            }
        }

        private void OnAddressChanged()
        {
            EventHandler handler = AddressChanged;
            if (handler != null)
            {
                handler(this, EventArgs.Empty);
            }
        }

        #endregion

    }
}
